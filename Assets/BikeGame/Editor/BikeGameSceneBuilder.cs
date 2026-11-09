using System;
using System.Collections.Generic;
using System.IO;
using BikeGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace BikeGame.EditorTools
{
    /// <summary>
    /// Creates Assets/BikeGame/Scenes/ForestBikeRide.unity from the original
    /// BrightDay forest scene and converts the imported built-in shaders to URP.
    ///
    /// The created scene only contains the forest, the main camera with the
    /// chase camera script and one "Bike Game" object holding BikeGameBootstrap
    /// prefab references plus the baked road spawn pose. The bike, rider and
    /// game manager are instantiated at runtime by BikeGameBootstrap.
    /// </summary>
    public static class BikeGameSceneBuilder
    {
        private const string BaseScenePath = "Assets/Supercyan Free Forest Sample/Scenes/BrightDay.unity";
        private const string ScenePath = "Assets/BikeGame/Scenes/ForestBikeRide.unity";
        private const string BikePrefabPath = "Assets/SimpleBikeControl/Prefabs/BikeTest.prefab";
        private const string RiderPrefabPath = "Assets/SimpleBikeControl/Prefabs/AJ.prefab";
        private const string GameManagerPrefabPath = "Assets/SimpleBikeControl/Prefabs/Game Manager.prefab";

        [MenuItem("Tools/Bike Game/Build Forest Ride Scene")]
        public static void BuildFromMenu()
        {
            Build();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Selection.activeGameObject = Object.FindAnyObjectByType<BikeGameBootstrap>()?.gameObject;
        }

        public static void BuildFromCommandLine()
        {
            try
            {
                Build();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void Build()
        {
            EnsureFolders();
            int convertedMaterials = ConvertMaterialsToUrp();

            if (EditorSceneManager.GetActiveScene().path == ScenePath)
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            File.Copy(BaseScenePath, ScenePath, true);
            AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceUpdate);

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Terrain terrain = Object.FindAnyObjectByType<Terrain>();
            if (terrain == null)
                throw new InvalidOperationException("BrightDay scene does not contain a Terrain.");

            Camera camera = Camera.main;
            if (camera == null)
            {
                Camera[] cameras = Object.FindObjectsByType<Camera>();
                if (cameras.Length > 0)
                    camera = cameras[0];
            }

            if (camera == null)
                throw new InvalidOperationException("BrightDay scene does not contain a Camera.");

            SpawnPoint spawn = FindRoadSpawn(terrain);
            SetupCamera(camera, spawn);
            CreateBikeGameRoot(scene, camera, spawn);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            AddSceneToBuildSettings();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.LogFormat(
                "BikeGame: built {0}. Spawn={1}, roadLayer={2}, roadWeight={3:0.00}, pathScore={4:0.0}, convertedMaterials={5}.",
                ScenePath, spawn.Position, spawn.LayerName, spawn.RoadWeight, spawn.PathScore, convertedMaterials);
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/BikeGame"))
                AssetDatabase.CreateFolder("Assets", "BikeGame");
            if (!AssetDatabase.IsValidFolder("Assets/BikeGame/Scenes"))
                AssetDatabase.CreateFolder("Assets/BikeGame", "Scenes");
        }

        private static void SetupCamera(Camera camera, SpawnPoint spawn)
        {
            BikeChaseCamera chaseCamera = camera.GetComponent<BikeChaseCamera>();
            if (chaseCamera == null)
                chaseCamera = camera.gameObject.AddComponent<BikeChaseCamera>();

            Vector3 position = spawn.Position - spawn.Forward * 4.8f + Vector3.up * (1.2f + 1.85f);
            camera.transform.SetPositionAndRotation(
                position,
                Quaternion.LookRotation(spawn.Position + Vector3.up * 1.2f + spawn.Forward * 5f - position, Vector3.up));
            camera.fieldOfView = 65f;

            SerializedObject serialized = new SerializedObject(chaseCamera);
            serialized.FindProperty("target").objectReferenceValue = null;
            serialized.FindProperty("distance").floatValue = 4.8f;
            serialized.FindProperty("height").floatValue = 1.85f;
            serialized.FindProperty("lookAhead").floatValue = 5f;
            var fovProp = serialized.FindProperty("fieldOfView");
            if (fovProp != null) fovProp.floatValue = 65f;
            serialized.FindProperty("positionSmoothTime").floatValue = 0.06f;
            serialized.FindProperty("rotationSharpness").floatValue = 16f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateBikeGameRoot(Scene scene, Camera camera, SpawnPoint spawn)
        {
            GameObject root = new GameObject("Bike Game");
            SceneManager.MoveGameObjectToScene(root, scene);

            BikeGameBootstrap bootstrap = root.AddComponent<BikeGameBootstrap>();

            SerializedObject serialized = new SerializedObject(bootstrap);
            serialized.FindProperty("gameManagerPrefab").objectReferenceValue =
                LoadRequired<GameObject>(GameManagerPrefabPath);
            serialized.FindProperty("bikePrefab").objectReferenceValue =
                LoadRequired<GameObject>(BikePrefabPath);
            serialized.FindProperty("riderPrefab").objectReferenceValue =
                LoadRequired<GameObject>(RiderPrefabPath);
            serialized.FindProperty("useBakedSpawn").boolValue = true;
            serialized.FindProperty("spawnPosition").vector3Value = spawn.Position;
            serialized.FindProperty("spawnEulerAngles").vector3Value = spawn.Rotation.eulerAngles;
            serialized.FindProperty("groundOffset").floatValue = 0.08f;
            serialized.FindProperty("autoMount").boolValue = true;
            serialized.FindProperty("disableWalkingController").boolValue = true;
            serialized.FindProperty("cameraSource").objectReferenceValue = camera;
            serialized.FindProperty("createChaseCamera").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            root.AddComponent<MobileBikeControls>();

            TiltSteering tilt = root.AddComponent<TiltSteering>();
            SerializedObject tiltSerialized = new SerializedObject(tilt);
            tiltSerialized.FindProperty("allowTilt").boolValue = true;
            tiltSerialized.FindProperty("mode").intValue = (int)SteeringMode.Tilt;
            tiltSerialized.FindProperty("fullSteerTiltDegrees").floatValue = 18f;
            tiltSerialized.FindProperty("deadZoneDegrees").floatValue = 2.5f;
            tiltSerialized.FindProperty("sensitivity").floatValue = 1.2f;
            tiltSerialized.FindProperty("smoothing").floatValue = 12f;
            tiltSerialized.FindProperty("autoPedal").boolValue = true;
            tiltSerialized.FindProperty("autoPedalAmount").floatValue = 0.55f;
            tiltSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T LoadRequired<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw new FileNotFoundException("Required asset not found: " + path);
            return asset;
        }

        private static void AddSceneToBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(scene => scene.path == ScenePath);

            // First enabled scene is the startup scene when the game is built,
            // so the bike scene boots first on the phone.
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ------------------------------------------------------------------
        // Road spawn selection
        // ------------------------------------------------------------------

        private struct SpawnPoint
        {
            public Vector3 Position;
            public Vector3 Normal;
            public Vector3 Forward;
            public Quaternion Rotation;
            public float RoadWeight;
            public float PathScore;
            public string LayerName;
        }

        private class Candidate
        {
            public float Tx;
            public float Tz;
            public float Weight;
            public float Slope;
            public Vector3 Position;
            public Vector3 Normal;
        }

        private static SpawnPoint FindRoadSpawn(Terrain terrain)
        {
            TerrainData data = terrain.terrainData;
            int roadLayer = FindRoadLayer(data);

            if (roadLayer < 0)
            {
                Debug.LogWarning("BikeGame: no terrain layer containing 'road' was found. Using a clear fallback spawn.");
                return CreateFallbackSpawn(terrain);
            }

            int width = data.alphamapResolution;
            int height = data.alphamapResolution;
            float[,,] alphamaps = data.GetAlphamaps(0, 0, width, height);
            TreeGrid trees = new TreeGrid(terrain, data.treeInstances);

            List<Candidate> candidates = CollectCandidates(terrain, data, alphamaps, roadLayer, trees, 0.5f, 4f);
            if (candidates.Count == 0)
                candidates = CollectCandidates(terrain, data, alphamaps, roadLayer, trees, 0.5f, 8f);
            if (candidates.Count == 0)
                candidates = CollectCandidates(terrain, data, alphamaps, roadLayer, trees, 0.15f, 18f);

            if (candidates.Count == 0)
            {
                Debug.LogWarning("BikeGame: no acceptable road candidate was found. Using a clear fallback spawn.");
                return CreateFallbackSpawn(terrain);
            }

            candidates.Sort((a, b) => b.Weight.CompareTo(a.Weight));
            int testCount = Mathf.Min(candidates.Count, 250);

            Candidate bestCandidate = null;
            Vector3 bestDirection = Vector3.forward;
            float bestPath = -1f;
            float bestScore = float.NegativeInfinity;

            for (int i = 0; i < testCount; i++)
            {
                Candidate candidate = candidates[i];
                float path = EvaluatePath(terrain, data, alphamaps, roadLayer, trees, candidate, out Vector3 direction);
                float score = path * 10f + candidate.Weight * 2f - candidate.Slope * 1f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestPath = path;
                    bestCandidate = candidate;
                    bestDirection = direction;
                }
            }

            if (bestCandidate == null)
                bestCandidate = candidates[0];

            Vector3 normal = bestCandidate.Normal.sqrMagnitude > 0.01f
                ? bestCandidate.Normal
                : Vector3.up;

            // Keep the bike upright (yaw only). Leaning the spawn with the road
            // cross slope makes the two wheel bike fall over while standing.
            Vector3 forward = Vector3.ProjectOnPlane(bestDirection, Vector3.up);
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.ProjectOnPlane(Vector3.forward, Vector3.up);
            forward.Normalize();

            return new SpawnPoint
            {
                Position = bestCandidate.Position,
                Normal = normal,
                Forward = forward,
                Rotation = Quaternion.LookRotation(forward, Vector3.up),
                RoadWeight = bestCandidate.Weight,
                PathScore = bestPath,
                LayerName = data.terrainLayers[roadLayer].name
            };
        }

        private static List<Candidate> CollectCandidates(
            Terrain terrain,
            TerrainData data,
            float[,,] alphamaps,
            int roadLayer,
            TreeGrid trees,
            float minimumWeight,
            float maximumSlope)
        {
            int width = alphamaps.GetLength(1);
            int height = alphamaps.GetLength(0);
            int step = Mathf.Max(1, Mathf.RoundToInt(Mathf.Min(width, height) / 96f));
            List<Candidate> candidates = new List<Candidate>();

            for (int z = step; z < height - step; z += step)
            {
                for (int x = step; x < width - step; x += step)
                {
                    float weight = alphamaps[z, x, roadLayer];
                    if (weight < minimumWeight) continue;

                    float tx = (x + 0.5f) / width;
                    float tz = (z + 0.5f) / height;
                    Vector3 normal = GetWorldNormal(terrain, data, tx, tz);
                    float slope = Vector3.Angle(normal, Vector3.up);
                    if (slope > maximumSlope) continue;

                    Vector3 position = GetWorldPosition(terrain, data, tx, tz);
                    if (trees.HasTreeNear(new Vector2(position.x, position.z), 2.8f)) continue;

                    candidates.Add(new Candidate
                    {
                        Tx = tx,
                        Tz = tz,
                        Weight = weight,
                        Slope = slope,
                        Position = position,
                        Normal = normal
                    });
                }
            }

            return candidates;
        }

        private static float EvaluatePath(
            Terrain terrain,
            TerrainData data,
            float[,,] alphamaps,
            int roadLayer,
            TreeGrid trees,
            Candidate candidate,
            out Vector3 direction)
        {
            float bestTotal = -1f;
            direction = Vector3.forward;

            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI * 2f / 16f;
                Vector3 testDirection = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                float total = 0f;

                for (int distance = 2; distance <= 24; distance += 2)
                {
                    Vector3 samplePosition = candidate.Position + testDirection * distance;
                    Vector2 normalized = ToNormalized(terrain, data, samplePosition);
                    if (normalized.x < 0.01f || normalized.x > 0.99f ||
                        normalized.y < 0.01f || normalized.y > 0.99f)
                        break;

                    float roadWeight = SampleRoad(alphamaps, roadLayer, normalized.x, normalized.y);
                    if (roadWeight < 0.25f) break;

                    Vector3 sampleNormal = GetWorldNormal(terrain, data, normalized.x, normalized.y);
                    if (Vector3.Angle(sampleNormal, Vector3.up) > 24f) break;
                    if (trees.HasTreeNear(new Vector2(samplePosition.x, samplePosition.z), 1.7f)) break;

                    total += roadWeight;
                }

                if (total > bestTotal)
                {
                    bestTotal = total;
                    direction = testDirection;
                }
            }

            return Mathf.Max(0f, bestTotal);
        }

        private static int FindRoadLayer(TerrainData data)
        {
            TerrainLayer[] layers = data.terrainLayers;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] != null && layers[i].name.IndexOf("road", StringComparison.OrdinalIgnoreCase) >= 0)
                    return i;
            }

            return -1;
        }

        private static SpawnPoint CreateFallbackSpawn(Terrain terrain)
        {
            TerrainData data = terrain.terrainData;
            TreeGrid trees = new TreeGrid(terrain, data.treeInstances);

            for (int z = 0; z <= 32; z++)
            {
                for (int x = 0; x <= 32; x++)
                {
                    float tx = Mathf.Clamp01((x + 0.5f) / 33f);
                    float tz = Mathf.Clamp01((z + 0.5f) / 33f);
                    Vector3 normal = GetWorldNormal(terrain, data, tx, tz);
                    if (Vector3.Angle(normal, Vector3.up) > 15f) continue;

                    Vector3 position = GetWorldPosition(terrain, data, tx, tz);
                    if (trees.HasTreeNear(new Vector2(position.x, position.z), 3f)) continue;

                    Vector3 forward = Vector3.ProjectOnPlane(Vector3.forward, normal).normalized;
                    return new SpawnPoint
                    {
                        Position = position,
                        Normal = normal,
                        Forward = forward,
                        Rotation = Quaternion.LookRotation(forward, normal),
                        RoadWeight = 0f,
                        PathScore = 0f,
                        LayerName = "fallback"
                    };
                }
            }

            Vector3 center = terrain.transform.position + new Vector3(data.size.x * 0.5f, 0f, data.size.z * 0.5f);
            center.y = terrain.transform.position.y + data.GetInterpolatedHeight(0.5f, 0.5f) + 0.1f;
            return new SpawnPoint
            {
                Position = center,
                Normal = Vector3.up,
                Forward = Vector3.forward,
                Rotation = Quaternion.identity,
                RoadWeight = 0f,
                PathScore = 0f,
                LayerName = "fallback"
            };
        }

        private static float SampleRoad(float[,,] alphamaps, int layer, float tx, float tz)
        {
            int width = alphamaps.GetLength(1);
            int height = alphamaps.GetLength(0);
            int x = Mathf.Clamp(Mathf.FloorToInt(tx * width), 0, width - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt(tz * height), 0, height - 1);
            return alphamaps[z, x, layer];
        }

        private static Vector2 ToNormalized(Terrain terrain, TerrainData data, Vector3 worldPosition)
        {
            Vector3 local = worldPosition - terrain.transform.position;
            return new Vector2(local.x / data.size.x, local.z / data.size.z);
        }

        private static Vector3 GetWorldPosition(Terrain terrain, TerrainData data, float tx, float tz)
        {
            tx = Mathf.Clamp01(tx);
            tz = Mathf.Clamp01(tz);
            Vector3 position = terrain.transform.position + new Vector3(tx * data.size.x, 0f, tz * data.size.z);
            position.y = terrain.transform.position.y + data.GetInterpolatedHeight(tx, tz);
            return position;
        }

        private static Vector3 GetWorldNormal(Terrain terrain, TerrainData data, float tx, float tz)
        {
            return (terrain.transform.rotation * data.GetInterpolatedNormal(tx, tz)).normalized;
        }

        private class TreeGrid
        {
            private const float CellSize = 4f;
            private readonly Dictionary<Vector2Int, List<Vector2>> cells = new Dictionary<Vector2Int, List<Vector2>>();

            public TreeGrid(Terrain terrain, TreeInstance[] treeInstances)
            {
                Vector3 origin = terrain.transform.position;
                Vector3 size = terrain.terrainData.size;

                for (int i = 0; i < treeInstances.Length; i++)
                {
                    TreeInstance tree = treeInstances[i];
                    Vector2 position = new Vector2(
                        origin.x + tree.position.x * size.x,
                        origin.z + tree.position.z * size.z);

                    Vector2Int key = CellOf(position);
                    if (!cells.TryGetValue(key, out List<Vector2> list))
                    {
                        list = new List<Vector2>();
                        cells.Add(key, list);
                    }

                    list.Add(position);
                }
            }

            public bool HasTreeNear(Vector2 position, float radius)
            {
                int minX = Mathf.FloorToInt((position.x - radius) / CellSize);
                int maxX = Mathf.FloorToInt((position.x + radius) / CellSize);
                int minZ = Mathf.FloorToInt((position.y - radius) / CellSize);
                int maxZ = Mathf.FloorToInt((position.y + radius) / CellSize);
                float radiusSquared = radius * radius;

                for (int x = minX; x <= maxX; x++)
                {
                    for (int z = minZ; z <= maxZ; z++)
                    {
                        if (!cells.TryGetValue(new Vector2Int(x, z), out List<Vector2> list)) continue;

                        for (int i = 0; i < list.Count; i++)
                        {
                            if ((list[i] - position).sqrMagnitude <= radiusSquared)
                                return true;
                        }
                    }
                }

                return false;
            }

            private static Vector2Int CellOf(Vector2 position)
            {
                return new Vector2Int(
                    Mathf.FloorToInt(position.x / CellSize),
                    Mathf.FloorToInt(position.y / CellSize));
            }
        }

        // ------------------------------------------------------------------
        // Material conversion
        // ------------------------------------------------------------------

        /// <summary>
        /// Converts every material that still uses a built in shader to URP/Lit.
        /// Skybox shaders already work in URP and are left untouched.
        /// </summary>
        private static int ConvertMaterialsToUrp()
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
                throw new InvalidOperationException("Universal Render Pipeline/Lit shader was not found.");

            string[] materialGuids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
            int converted = 0;

            foreach (string guid in materialGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null || material.shader == null) continue;

                string shaderName = material.shader.name;
                if (IsAlreadyConverted(shaderName)) continue;

                Debug.LogFormat("BikeGame: converting material {0} from '{1}'.", path, shaderName);
                ConvertMaterial(material, urpLit);
                converted++;
            }

            AssetDatabase.SaveAssets();
            return converted;
        }

        private static bool IsAlreadyConverted(string shaderName)
        {
            return shaderName.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal) ||
                   shaderName.StartsWith("Shader Graphs/", StringComparison.Ordinal) ||
                   shaderName.StartsWith("Skybox/", StringComparison.Ordinal) ||
                   shaderName.StartsWith("Hidden/", StringComparison.Ordinal) ||
                   shaderName == "Sprites/Default";
        }

        private static void ConvertMaterial(Material material, Shader urpLit)
        {
            string shaderName = material.shader.name;
            string materialName = material.name.ToLowerInvariant();

            Texture mainTexture = GetTexture(material, "_MainTex") ?? GetTexture(material, "_BaseMap");
            Color baseColor = GetColor(material, "_Color", Color.white);
            float metallic = GetFloat(material, "_Metallic", 0f);
            float smoothness = GetFloat(material, "_Glossiness", 0.3f);
            float mode = GetFloat(material, "_Mode", 0f);
            float cutoff = GetFloat(material, "_Cutoff", 0.5f);
            bool premultiply = material.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON");

            Texture bumpMap = GetTexture(material, "_BumpMap");
            float bumpScale = GetFloat(material, "_BumpScale", 1f);
            Texture occlusionMap = GetTexture(material, "_OcclusionMap");
            float occlusionStrength = GetFloat(material, "_OcclusionStrength", 1f);
            Texture emissionMap = GetTexture(material, "_EmissionMap");
            Color emissionColor = GetColor(material, "_EmissionColor", Color.black);
            Vector2 mainScale = GetTextureScale(material, "_MainTex");
            Vector2 mainOffset = GetTextureOffset(material, "_MainTex");
            Vector2 emissionScale = GetTextureScale(material, "_EmissionMap");
            Vector2 emissionOffset = GetTextureOffset(material, "_EmissionMap");

            bool legacyTransparent =
                shaderName.IndexOf("Transparent", StringComparison.OrdinalIgnoreCase) >= 0;
            bool legacyCutout =
                shaderName.IndexOf("Cutout", StringComparison.OrdinalIgnoreCase) >= 0;
            bool particleShader =
                shaderName.IndexOf("Particles", StringComparison.OrdinalIgnoreCase) >= 0 ||
                shaderName.IndexOf("Alpha Blended", StringComparison.OrdinalIgnoreCase) >= 0 ||
                material.HasProperty("_TintColor");

            bool isTransparent = mode == 2f || mode == 3f || particleShader ||
                                 (legacyTransparent && !legacyCutout);

            bool isFoliage = materialName.Contains("leaf") ||
                             materialName.Contains("foliage") ||
                             materialName.Contains("fir");

            bool isCutout = !isTransparent &&
                            (mode == 1f || legacyCutout || isFoliage);

            material.shader = urpLit;

            material.SetTexture("_BaseMap", mainTexture);
            material.SetTexture("_MainTex", mainTexture);
            material.SetTextureScale("_BaseMap", mainScale);
            material.SetTextureOffset("_BaseMap", mainOffset);
            material.SetColor("_BaseColor", baseColor);
            material.SetColor("_Color", baseColor);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);

            if (bumpMap != null)
            {
                material.SetTexture("_BumpMap", bumpMap);
                material.SetFloat("_BumpScale", bumpScale);
            }

            if (occlusionMap != null)
            {
                material.SetTexture("_OcclusionMap", occlusionMap);
                material.SetFloat("_OcclusionStrength", occlusionStrength);
            }

            if (emissionMap != null)
            {
                material.SetTexture("_EmissionMap", emissionMap);
                material.SetTextureScale("_EmissionMap", emissionScale);
                material.SetTextureOffset("_EmissionMap", emissionOffset);
            }

            material.SetColor("_EmissionColor", emissionColor);

            material.SetFloat("_Surface", isTransparent ? 1f : 0f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_AlphaClip", isCutout ? 1f : 0f);
            material.SetFloat("_Cutoff", cutoff);
            material.SetFloat("_ZWrite", isTransparent ? 0f : 1f);

            // Leaves and foliage are single sided cards, keep them visible from
            // both sides like the original forest shaders did.
            if (isFoliage)
                material.SetFloat("_Cull", 0f);

            if (isTransparent)
            {
                BlendMode sourceBlend = premultiply ? BlendMode.One : BlendMode.SrcAlpha;
                material.SetFloat("_SrcBlend", (float)sourceBlend);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetOverrideTag("RenderType", "Transparent");
                material.renderQueue = (int)RenderQueue.Transparent;
            }
            else if (isCutout)
            {
                material.SetFloat("_SrcBlend", (float)BlendMode.One);
                material.SetFloat("_DstBlend", (float)BlendMode.Zero);
                material.SetOverrideTag("RenderType", "TransparentCutout");
                material.renderQueue = (int)RenderQueue.AlphaTest;
            }
            else
            {
                material.SetFloat("_SrcBlend", (float)BlendMode.One);
                material.SetFloat("_DstBlend", (float)BlendMode.Zero);
                material.SetOverrideTag("RenderType", "Opaque");
                material.renderQueue = -1;
            }

            List<string> keywords = new List<string>();
            if (isTransparent)
            {
                keywords.Add("_SURFACE_TYPE_TRANSPARENT");
                if (premultiply) keywords.Add("_ALPHAPREMULTIPLY_ON");
            }
            else if (isCutout)
            {
                keywords.Add("_ALPHATEST_ON");
            }

            if (emissionColor.maxColorComponent > 0.001f)
                keywords.Add("_EMISSION");

            material.shaderKeywords = keywords.ToArray();
            EditorUtility.SetDirty(material);
        }

        private static Texture GetTexture(Material material, string property)
        {
            return material.HasProperty(property) ? material.GetTexture(property) : null;
        }

        private static Color GetColor(Material material, string property, Color fallback)
        {
            return material.HasProperty(property) ? material.GetColor(property) : fallback;
        }

        private static float GetFloat(Material material, string property, float fallback)
        {
            return material.HasProperty(property) ? material.GetFloat(property) : fallback;
        }

        private static Vector2 GetTextureScale(Material material, string property)
        {
            return material.HasProperty(property) ? material.GetTextureScale(property) : Vector2.one;
        }

        private static Vector2 GetTextureOffset(Material material, string property)
        {
            return material.HasProperty(property) ? material.GetTextureOffset(property) : Vector2.zero;
        }
    }
}
