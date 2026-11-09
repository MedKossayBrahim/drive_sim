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

            // Let SimpleBike.Start, Biker.Start and PlayerController.Start run.
            yield return null;
            yield return null;

            if (chaseCamera != null)
            {
                cameraTarget = bike.transform;
                chaseCamera.SetTarget(cameraTarget, true);
            }

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
            Debug.LogFormat("BikeGameBootstrap: rider mounted on {0} at {1}.",
                bike.name, bike.transform.position);
        }

        void KeepInRideScene()
        {
            Scene scene = gameObject.scene;
            if (!scene.IsValid())
                return;
            if (GameManager.Instance != null)
                SceneManager.MoveGameObjectToScene(GameManager.Instance.gameObject, scene);
            if (bikeInstance != null)
                SceneManager.MoveGameObjectToScene(bikeInstance, scene);
            if (riderInstance != null)
                SceneManager.MoveGameObjectToScene(riderInstance, scene);
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
            if (camera == null) camera = Camera.main;

            if (camera == null)
            {
                Camera[] cameras = FindObjectsByType<Camera>();
                if (cameras.Length > 0) camera = cameras[0];
            }

            if (camera == null)
            {
                Debug.LogWarning("BikeGameBootstrap: the scene has no camera, the chase camera was not created.");
                return;
            }

            chaseCamera = camera.GetComponent<BikeChaseCamera>();
            if (chaseCamera == null && createChaseCamera)
                chaseCamera = camera.gameObject.AddComponent<BikeChaseCamera>();

            if (chaseCamera != null)
                chaseCamera.SetTarget(null, false);
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
            if (chaseCamera == null) return;

            Transform desired = rider != null && !rider.IsRiding ? rider.transform : bike.transform;
            if (desired == null || cameraTarget == desired) return;

            cameraTarget = desired;
            chaseCamera.SetTarget(desired, true);
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
