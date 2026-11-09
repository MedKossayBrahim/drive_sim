using System.Collections;
using KikiNgao.SimpleBikeControl;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BikeGame
{
    /// <summary>
    /// Assembles the playable scene at runtime: it spawns the Game Manager,
    /// the bike and the rider prefabs, hooks up the third person camera and
    /// puts the rider on the bike.
    ///
    /// The scene file only stores prefab references and the spawn pose that the
    /// editor builder baked into it, so the scene does not depend on prefab
    /// instances being unpacked/edited inside the scene.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public class BikeGameBootstrap : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private GameObject gameManagerPrefab;
        [SerializeField] private GameObject bikePrefab;
        [SerializeField] private GameObject riderPrefab;

        [Header("Spawn Pose (baked by Tools > Bike Game > Build Forest Ride Scene)")]
        [SerializeField] private bool useBakedSpawn = true;
        [SerializeField] private Vector3 spawnPosition;
        [SerializeField] private Vector3 spawnEulerAngles;
        [SerializeField] private float groundOffset = 0.08f;

        [Header("Behaviour")]
        [SerializeField] private bool autoMount = true;
        [SerializeField] private bool disableWalkingController = true;

        [Header("Camera")]
        [SerializeField] private Camera cameraSource;
        [SerializeField] private bool createChaseCamera = true;

        private GameObject bikeInstance;
        private GameObject riderInstance;
        private SimpleBike bike;
        private Biker rider;
        private BikeChaseCamera chaseCamera;
        private Transform cameraTarget;

        public SimpleBike Bike => bike;
        public Biker Rider => rider;
        public BikeChaseCamera ChaseCamera => chaseCamera;

        private IEnumerator Start()
        {
            if (!EnsureManagers())
                yield break;

            ResolveCamera();

            if (!SpawnBike())
                yield break;

            SpawnRider();
            ConfigureWalkingController();
            EnsureMobileControls();
            CreateWorldBounds();
            EnsureRoadBarriers();
            EnsureHeartSystem();

            // Let SimpleBike.Start, Biker.Start and PlayerController.Start run.
            yield return null;
            yield return null;

            if (autoMount)
            {
                if (rider == null)
                {
                    Debug.LogError("BikeGameBootstrap: no Biker component on the rider prefab.");
                    yield break;
                }

                if (!rider.IsRiding)
                    rider.MountBike(bike);

                if (!rider.IsRiding)
                {
                    Debug.LogError("BikeGameBootstrap: the rider could not be mounted automatically.");
                    yield break;
                }
            }

            KeepInRideScene();
            BindChaseCamera(true);
            // Hub FocusRideCameras / AuthBackdrop may toggle cams a frame late.
            yield return null;
            BindChaseCamera(true);
            yield return null;
            BindChaseCamera(true);

            Debug.LogFormat("BikeGameBootstrap: rider mounted on {0} at {1}, camera behind player.",
                bike.name, bike.transform.position);
        }

        void LateUpdate()
        {
            // Hard guarantee: Main Camera stays behind the bike every frame.
            if (bike == null)
                return;
            if (chaseCamera == null || chaseCamera.Target == null)
                BindChaseCamera(false);
        }

        void BindChaseCamera(bool snap)
        {
            if (bike == null)
                return;

            if (chaseCamera == null)
                ResolveCamera();
            if (chaseCamera == null)
                return;

            Camera cam = chaseCamera.GetComponent<Camera>();
            if (cam != null)
            {
                cam.enabled = true;
                cam.fieldOfView = 65f;
                if (!cam.CompareTag("MainCamera"))
                    cam.tag = "MainCamera";

                Camera[] all = FindObjectsByType<Camera>(FindObjectsInactive.Include);
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] == cam)
                        continue;
                    all[i].enabled = false;
                }
            }

            Transform desired = rider != null && !rider.IsRiding ? rider.transform : bike.transform;
            cameraTarget = desired;
            chaseCamera.SetTarget(desired, snap);
        }

        void KeepInRideScene()
        {
            Scene scene = gameObject.scene;
            if (!scene.IsValid())
                return;
            // MoveGameObjectToScene only accepts scene roots. After MountBike the
            // rider is parented under the bike, so moving the bike is enough.
            MoveRootToScene(GameManager.Instance != null ? GameManager.Instance.gameObject : null, scene);
            MoveRootToScene(bikeInstance, scene);
            MoveRootToScene(riderInstance, scene);
        }

        static void MoveRootToScene(GameObject go, Scene scene)
        {
            if (go == null || go.transform.parent != null)
                return;
            if (go.scene == scene)
                return;
            SceneManager.MoveGameObjectToScene(go, scene);
        }

        private bool EnsureManagers()
        {
            if (GameManager.Instance != null)
                return true;

            if (gameManagerPrefab == null)
            {
                Debug.LogError("BikeGameBootstrap: no Game Manager prefab assigned.");
                return false;
            }

            GameObject instance = Instantiate(gameManagerPrefab);
            instance.name = "Game Manager";

            if (GameManager.Instance == null)
            {
                Debug.LogError("BikeGameBootstrap: the Game Manager prefab has no GameManager component.");
                return false;
            }

            return true;
        }

        private void ResolveCamera()
        {
            Camera camera = cameraSource;

            // Prefer a camera that already lives in ForestBikeRide.
            if (camera == null || camera.gameObject.scene != gameObject.scene)
            {
                Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Include);
                for (int i = 0; i < cameras.Length; i++)
                {
                    if (cameras[i].gameObject.scene == gameObject.scene)
                    {
                        camera = cameras[i];
                        break;
                    }
                }
            }

            if (camera == null)
                camera = Camera.main;

            if (camera == null)
            {
                Debug.LogWarning("BikeGameBootstrap: the scene has no camera, the chase camera was not created.");
                return;
            }

            camera.enabled = true;
            chaseCamera = camera.GetComponent<BikeChaseCamera>();
            if (chaseCamera == null && createChaseCamera)
                chaseCamera = camera.gameObject.AddComponent<BikeChaseCamera>();
        }

        private bool SpawnBike()
        {
            if (bikePrefab == null)
            {
                Debug.LogError("BikeGameBootstrap: no bike prefab assigned.");
                return false;
            }

            bikeInstance = Instantiate(bikePrefab);
            bikeInstance.name = "Bike";

            Vector3 position = GetSpawnPosition();
            Quaternion rotation = GetSpawnRotation();

            // The bike is a Rigidbody, so the physics pose has to be set too.
            // Setting only the Transform is reverted by the first physics step.
            Rigidbody body = bikeInstance.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.position = position;
                body.rotation = rotation;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            bikeInstance.transform.SetPositionAndRotation(position, rotation);
            Physics.SyncTransforms();

            bike = bikeInstance.GetComponent<SimpleBike>() ??
                   bikeInstance.GetComponentInChildren<SimpleBike>(true);

            if (bike == null)
            {
                Debug.LogError("BikeGameBootstrap: the bike prefab has no SimpleBike component.");
                return false;
            }

            if (bikeInstance.GetComponent<BikeStabilityAssist>() == null)
                bikeInstance.AddComponent<BikeStabilityAssist>();

            if (bikeInstance.GetComponent<BikeImpactFeedback>() == null)
                bikeInstance.AddComponent<BikeImpactFeedback>();

            if (bikeInstance.GetComponent<BikeWheelie>() == null)
                bikeInstance.AddComponent<BikeWheelie>();

            return true;
        }

        private void SpawnRider()
        {
            if (riderPrefab == null)
            {
                Debug.LogWarning("BikeGameBootstrap: no rider prefab assigned, riding without a rider.");
                return;
            }

            riderInstance = Instantiate(riderPrefab);
            riderInstance.name = "AJ";

            Vector3 riderPosition = bike.transform.position + bike.transform.right * 1.35f;
            riderInstance.transform.SetPositionAndRotation(riderPosition, bike.transform.rotation);

            rider = riderInstance.GetComponent<Biker>() ??
                    riderInstance.GetComponentInChildren<Biker>(true);

            if (rider == null)
                Debug.LogWarning("BikeGameBootstrap: the rider prefab has no Biker component.");
        }

        private void ConfigureWalkingController()
        {
            if (!disableWalkingController || riderInstance == null) return;

            PlayerController walker = riderInstance.GetComponent<PlayerController>() ??
                                      riderInstance.GetComponentInChildren<PlayerController>(true);
            if (walker != null)
                walker.disable = true;
        }

        private void EnsureMobileControls()
        {
            if (FindAnyObjectByType<MobileBikeControls>() == null)
                gameObject.AddComponent<MobileBikeControls>();

            if (FindAnyObjectByType<TiltSteering>() == null)
                gameObject.AddComponent<TiltSteering>();
        }

        // Positions from PlaceBarriers (semah) — 4 red barricades on the forest road.
        static readonly float[] BarrierDistances = { 4f, 10f, 18f, 21f };
        static readonly float[] BarrierLateral = { 0.8f, -0.5f, 0.5f, -0.8f };
        const string RedBarricadeResource = "BikeBarriers/Red_barricade";
        const float RedBarricadeTargetWidth = 1.15f;

        void EnsureRoadBarriers()
        {
            if (GameObject.Find("Road Barrier 1") != null)
            {
                // Barrières déjà en scène (outil éditeur) : hors raycast caméra.
                for (int i = 1; i <= BarrierDistances.Length; i++)
                {
                    GameObject existing = GameObject.Find("Road Barrier " + i);
                    if (existing != null)
                        ApplyBarrierCameraIgnore(existing);
                }
                return;
            }

            Terrain terrain = Terrain.activeTerrain;
            Vector3 spawn = useBakedSpawn ? spawnPosition : (bike != null ? bike.transform.position : Vector3.zero);
            float yaw = useBakedSpawn ? spawnEulerAngles.y : (bike != null ? bike.transform.eulerAngles.y : 67.5f);
            Vector3 forward = new Vector3(Mathf.Sin(yaw * Mathf.Deg2Rad), 0f, Mathf.Cos(yaw * Mathf.Deg2Rad));
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            GameObject prefab = Resources.Load<GameObject>(RedBarricadeResource);
            Material mat = BuildRedBarricadeMaterial();

            for (int i = 0; i < BarrierDistances.Length; i++)
            {
                Vector3 pos = spawn + forward * BarrierDistances[i] + right * BarrierLateral[i];
                float ground = terrain != null
                    ? terrain.transform.position.y + terrain.SampleHeight(pos)
                    : spawn.y;

                GameObject barrier;
                if (prefab != null)
                {
                    barrier = Instantiate(prefab);
                    barrier.name = "Road Barrier " + (i + 1);
                    FitRedBarricade(barrier, pos, yaw, ground, mat);
                }
                else
                {
                    // Fallback si le FBX n'est pas encore importé par Unity.
                    barrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    barrier.name = "Road Barrier " + (i + 1);
                    barrier.transform.SetPositionAndRotation(
                        new Vector3(pos.x, ground + 0.3f, pos.z),
                        Quaternion.Euler(0f, yaw, 0f));
                    barrier.transform.localScale = new Vector3(0.8f, 0.6f, 0.3f);
                    if (mat != null)
                    {
                        Renderer rend = barrier.GetComponent<Renderer>();
                        if (rend != null) rend.sharedMaterial = mat;
                    }
                }

                ApplyBarrierCameraIgnore(barrier);

                foreach (var col in barrier.GetComponentsInChildren<Collider>())
                    col.isTrigger = false;
                if (barrier.GetComponent<BarrierCollision>() == null &&
                    barrier.GetComponentInChildren<BarrierCollision>() == null)
                    barrier.AddComponent<BarrierCollision>();
                SceneManager.MoveGameObjectToScene(barrier, gameObject.scene);
            }

            if (prefab != null)
                Debug.Log("BikeGameBootstrap: 4 barrières Red_barricade (read road bar) placées.");
            else
                Debug.LogWarning("BikeGameBootstrap: Red_barricade introuvable dans Resources/BikeBarriers — cubes de secours.");
        }

        static void ApplyBarrierCameraIgnore(GameObject barrier)
        {
            // Ignore Raycast: la chase cam SphereCast ne se plaque plus dessus,
            // tout en gardant la collision physique vélo ↔ barrière.
            int ignoreRaycast = LayerMask.NameToLayer("Ignore Raycast");
            if (ignoreRaycast < 0 || barrier == null)
                return;
            foreach (var t in barrier.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = ignoreRaycast;
        }

        static Material BuildRedBarricadeMaterial()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) lit = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (lit == null) return null;

            var mat = new Material(lit) { name = "RedBarricadeRuntime" };
            Texture2D albedo = Resources.Load<Texture2D>("BikeBarriers/Red_barricade_Material_u1_v1_BaseColor");
            Texture2D normal = Resources.Load<Texture2D>("BikeBarriers/Red_barricade_Material_u1_v1_Normal");
            Texture2D mask = Resources.Load<Texture2D>("BikeBarriers/Red_barricade_Material_u1_v1_RoughnessMetallic");
            Texture2D ao = Resources.Load<Texture2D>("BikeBarriers/Red_barricade_AO");
            if (albedo != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", albedo);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", albedo);
            }
            else
                mat.color = new Color(0.85f, 0.12f, 0.1f, 1f);
            if (normal != null && mat.HasProperty("_BumpMap"))
            {
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
            }
            if (mask != null && mat.HasProperty("_MetallicGlossMap"))
            {
                mat.SetTexture("_MetallicGlossMap", mask);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            if (ao != null && mat.HasProperty("_OcclusionMap"))
                mat.SetTexture("_OcclusionMap", ao);
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", 0.35f);
            return mat;
        }

        static void FitRedBarricade(GameObject barrier, Vector3 routePoint, float yaw, float groundY, Material mat)
        {
            barrier.transform.SetPositionAndRotation(routePoint, Quaternion.Euler(0f, yaw, 0f));
            if (mat != null)
            {
                foreach (var rend in barrier.GetComponentsInChildren<Renderer>())
                    rend.sharedMaterial = mat;
            }

            Bounds bounds = RendererBounds(barrier);
            float width = Mathf.Max(bounds.size.x, bounds.size.z);
            if (width > 0.05f)
                barrier.transform.localScale *= RedBarricadeTargetWidth / width;

            bounds = RendererBounds(barrier);
            Vector3 shift = new Vector3(
                routePoint.x - bounds.center.x,
                groundY - bounds.min.y,
                routePoint.z - bounds.center.z);
            barrier.transform.position += shift;

            if (barrier.GetComponentInChildren<Collider>() == null)
            {
                var box = barrier.AddComponent<BoxCollider>();
                bounds = RendererBounds(barrier);
                box.center = barrier.transform.InverseTransformPoint(bounds.center);
                Vector3 size = bounds.size;
                Vector3 scale = barrier.transform.lossyScale;
                size.x /= Mathf.Max(0.001f, scale.x);
                size.y /= Mathf.Max(0.001f, scale.y);
                size.z /= Mathf.Max(0.001f, scale.z);
                box.size = size;
            }
        }

        static Bounds RendererBounds(GameObject target)
        {
            var renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return new Bounds(target.transform.position, Vector3.one);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        void EnsureHeartSystem()
        {
            if (FindAnyObjectByType<HeartSystem>() != null)
                return;
            gameObject.AddComponent<HeartSystem>();
        }

        /// <summary>
        /// The forest sample is a small island. Invisible walls keep the bike on
        /// it instead of driving off the edge and falling forever.
        /// </summary>
        private void CreateWorldBounds()
        {
            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null) return;

            Vector3 origin = terrain.transform.position;
            Vector3 size = Vector3.Scale(terrain.terrainData.size, terrain.transform.lossyScale);
            float wallHeight = 80f;
            float wallThickness = 4f;
            float bottom = origin.y - 20f;

            GameObject bounds = new GameObject("Bike Game Bounds");
            bounds.transform.SetParent(transform, false);

            CreateWall(bounds.transform, "North",
                new Vector3(origin.x + size.x * 0.5f, bottom + wallHeight * 0.5f, origin.z + size.z + wallThickness * 0.5f),
                new Vector3(size.x + wallThickness * 2f, wallHeight, wallThickness));

            CreateWall(bounds.transform, "South",
                new Vector3(origin.x + size.x * 0.5f, bottom + wallHeight * 0.5f, origin.z - wallThickness * 0.5f),
                new Vector3(size.x + wallThickness * 2f, wallHeight, wallThickness));

            CreateWall(bounds.transform, "East",
                new Vector3(origin.x + size.x + wallThickness * 0.5f, bottom + wallHeight * 0.5f, origin.z + size.z * 0.5f),
                new Vector3(wallThickness, wallHeight, size.z + wallThickness * 2f));

            CreateWall(bounds.transform, "West",
                new Vector3(origin.x - wallThickness * 0.5f, bottom + wallHeight * 0.5f, origin.z + size.z * 0.5f),
                new Vector3(wallThickness, wallHeight, size.z + wallThickness * 2f));
        }

        private static void CreateWall(Transform parent, string name, Vector3 position, Vector3 size)
        {
            GameObject wall = new GameObject(name);
            wall.transform.SetParent(parent, false);
            wall.transform.position = position;

            BoxCollider collider = wall.AddComponent<BoxCollider>();
            collider.size = size;
        }

        /// <summary>Safety net: if the bike ever leaves the island, put it back.</summary>
        private void Update()
        {
            if (bike == null) return;

            UpdateCameraTarget();

            Terrain terrain = Terrain.activeTerrain;
            Vector3 position = bike.transform.position;

            bool belowWorld = position.y < (terrain != null ? terrain.transform.position.y - 30f : -100f);
            bool outsideIsland = false;

            if (terrain != null)
            {
                Vector3 origin = terrain.transform.position;
                Vector3 size = Vector3.Scale(terrain.terrainData.size, terrain.transform.lossyScale);
                const float margin = 3f;

                outsideIsland = position.x < origin.x - margin ||
                                position.x > origin.x + size.x + margin ||
                                position.z < origin.z - margin ||
                                position.z > origin.z + size.z + margin;
            }

            if (!belowWorld && !outsideIsland) return;

            Debug.LogWarningFormat("BikeGameBootstrap: bike left the island at {0}, returning to the start.",
                position.ToString("F1"));

            Rigidbody body = bike.GetRigidbody();
            Vector3 spawn = GetSpawnPosition();
            Quaternion rotation = GetSpawnRotation();

            if (body != null)
            {
                body.position = spawn;
                body.rotation = rotation;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            bike.transform.SetPositionAndRotation(spawn, rotation);
            Physics.SyncTransforms();

            bike.ResetRideState();

            // A respawn must put the player back on the bike, never on foot.
            if (rider != null && !rider.IsRiding)
                rider.MountBike(bike);

            BikeImpactFeedback impactFeedback = bike.GetComponent<BikeImpactFeedback>();
            if (impactFeedback != null) impactFeedback.ResetCooldown();

            if (chaseCamera != null)
            {
                cameraTarget = bike.transform;
                chaseCamera.SetTarget(cameraTarget, true);
            }
        }

        /// <summary>Follows the bike while riding, and the rider while on foot.</summary>
        private void UpdateCameraTarget()
        {
            if (chaseCamera == null)
                ResolveCamera();
            if (chaseCamera == null || bike == null)
                return;

            Transform desired = rider != null && !rider.IsRiding ? rider.transform : bike.transform;
            if (desired == null)
                return;

            if (cameraTarget != desired || chaseCamera.Target != desired)
            {
                cameraTarget = desired;
                chaseCamera.SetTarget(desired, false);
            }
        }

        private Vector3 GetSpawnPosition()
        {
            if (!useBakedSpawn)
                return GetTerrainCentre() + Vector3.up * groundOffset;

            return spawnPosition + Vector3.up * groundOffset;
        }

        private Quaternion GetSpawnRotation()
        {
            return useBakedSpawn ? Quaternion.Euler(spawnEulerAngles) : Quaternion.identity;
        }

        private static Vector3 GetTerrainCentre()
        {
            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null)
                return new Vector3(0f, 0.5f, 0f);

            Vector3 centre = terrain.transform.position + terrain.terrainData.size * 0.5f;
            centre.y = terrain.SampleHeight(centre);
            return centre;
        }
    }
}
