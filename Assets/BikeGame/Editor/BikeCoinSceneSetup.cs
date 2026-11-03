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
    /// Imports the supplied coin FBX/textures, builds the shared coin material and
    /// prefab, then places coins along the baked forest road and saves the scene.
    ///
    /// Run from the menu or with:
    ///   Unity.exe -batchmode -nographics -projectPath <project> \
    ///       -executeMethod BikeGame.EditorTools.BikeCoinSceneSetup.Run \
    ///       -logFile <log>
    /// </summary>
    public static class BikeCoinSceneSetup
    {
        private const string ScenePath = "Assets/BikeGame/Scenes/ForestBikeRide.unity";
        private const string CoinFolder = "Assets/BikeGame/Coins";
        private const string SourceFolder = CoinFolder + "/Source";
        private const string TextureFolder = CoinFolder + "/Textures";
        private const string MaterialFolder = CoinFolder + "/Materials";
        private const string PrefabFolder = CoinFolder + "/Prefabs";
        private const string ModelPath = SourceFolder + "/Coin_LP1.fbx";
        private const string GoldMaterialPath = MaterialFolder + "/CoinGold.mat";
        private const string BurstMaterialPath = MaterialFolder + "/CoinBurst.mat";
        private const string PrefabPath = PrefabFolder + "/Coin.prefab";

        private const float CoinDiameter = 0.85f;
        private const float CoinHoverHeight = 1.15f;
        private const float TriggerRadius = 0.65f;
        private const float FirstCoinDistance = 2.0f;
        private const float CoinSpacing = 1.8f;

        private static readonly float[] LateralOffsets =
        {
            0f, 0.65f, -0.65f, 0.9f, -0.9f, 0.45f, -0.45f, 0f, 0.7f, -0.7f
        };

        [MenuItem("Tools/Bike Game/Setup Collectible Coins")]
        public static void SetupFromMenu()
        {
            Setup();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Selection.activeGameObject = GameObject.Find("Coins");
        }

        public static void Run()
        {
            try
            {
                Setup();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void Setup()
        {
            EnsureFolders();
            CopySourceAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            ConfigureModel();
            ConfigureTextures();

            Material gold = CreateGoldMaterial();
            Material burst = CreateBurstMaterial();
            GameObject prefab = CreateCoinPrefab(gold, burst);
            int coinCount = PlaceCoins(prefab);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.LogFormat(
                "BikeCoins: setup complete. coins={0}, prefab={1}, material={2}, scene={3}",
                coinCount, PrefabPath, GoldMaterialPath, ScenePath);
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/BikeGame", "Coins");
            EnsureFolder(CoinFolder, "Source");
            EnsureFolder(CoinFolder, "Textures");
            EnsureFolder(CoinFolder, "Materials");
            EnsureFolder(CoinFolder, "Prefabs");
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }

        private static void CopySourceAssets()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string sourceRoot = Path.Combine(projectRoot, "coin");

            CopyIfNewer(Path.Combine(sourceRoot, "source", "Coin_LP1.fbx"),
                Path.Combine(projectRoot, ModelPath));

            string[] textures =
            {
                "Coin_LP1_Gold_BaseColor.png",
                "Coin_LP1_Gold_Metallic.png",
                "Coin_LP1_Gold_Roughness.png",
                "DefaultMaterial_Mixed_AO.png",
                "Gold_Base_Color.png",
                "Gold_Normal_OpenGL.png"
            };

            foreach (string texture in textures)
            {
                CopyIfNewer(Path.Combine(sourceRoot, "textures", texture),
                    Path.Combine(projectRoot, TextureFolder, texture));
            }
        }

        private static void CopyIfNewer(string source, string target)
        {
            if (!File.Exists(source))
            {
                if (!File.Exists(target))
                    throw new FileNotFoundException("Coin source asset not found: " + source);
                return;
            }

            if (File.Exists(target) &&
                File.GetLastWriteTimeUtc(target) >= File.GetLastWriteTimeUtc(source) &&
                new FileInfo(target).Length == new FileInfo(source).Length)
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(source, target, true);
        }

        private static void ConfigureModel()
        {
            ModelImporter importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null)
                throw new InvalidOperationException("Could not load the coin model importer: " + ModelPath);

            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.addCollider = false;
            importer.isReadable = false;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importNormals = ModelImporterNormals.Import;
            importer.SaveAndReimport();
        }

        private static void ConfigureTextures()
        {
            ConfigureTexture(TextureFolder + "/Coin_LP1_Gold_BaseColor.png", false, false);
            ConfigureTexture(TextureFolder + "/Coin_LP1_Gold_Metallic.png", false, true);
            ConfigureTexture(TextureFolder + "/Coin_LP1_Gold_Roughness.png", false, true);
            ConfigureTexture(TextureFolder + "/DefaultMaterial_Mixed_AO.png", false, true);
            ConfigureTexture(TextureFolder + "/Gold_Base_Color.png", false, false);
            ConfigureTexture(TextureFolder + "/Gold_Normal_OpenGL.png", true, true);
        }

        private static void ConfigureTexture(string path, bool normalMap, bool linear)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("Could not load texture importer: " + path);

            importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !linear;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = true;
            importer.isReadable = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 512;
            importer.textureCompression = TextureImporterCompression.Compressed;
            if (normalMap)
                importer.normalmapFilter = TextureImporterNormalFilter.Standard;

            importer.SaveAndReimport();
        }

        private static Material CreateGoldMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new InvalidOperationException("Universal Render Pipeline/Lit shader was not found.");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(GoldMaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, GoldMaterialPath);
            }

            material.shader = shader;
            material.name = "CoinGold";

            Texture2D baseColor = LoadTexture(TextureFolder + "/Coin_LP1_Gold_BaseColor.png");
            Texture2D normal = LoadTexture(TextureFolder + "/Gold_Normal_OpenGL.png");

            material.SetTexture("_BaseMap", baseColor);
            material.SetTexture("_MainTex", baseColor);
            // The supplied albedo is fairly dark; a warm over-white tint keeps the
            // engraved detail while reading as bright gold under the forest light.
            material.SetColor("_BaseColor", new Color(1.35f, 1.2f, 0.85f, 1f));
            material.SetColor("_Color", new Color(1.35f, 1.2f, 0.85f, 1f));

            // A fully metallic surface has no diffuse response, so it rendered as a
            // near-black disc in this scene's low reflection intensity. A mostly
            // dielectric surface keeps the gold albedo visible from every angle.
            material.SetTexture("_MetallicGlossMap", null);
            material.SetFloat("_Metallic", 0.2f);
            material.SetFloat("_Smoothness", 0.45f);
            material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.SetFloat("_WorkflowMode", 1f);
            material.DisableKeyword("_METALLICSPECGLOSSMAP");

            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", 1f);
            material.EnableKeyword("_NORMALMAP");

            material.SetTexture("_OcclusionMap", null);
            material.SetFloat("_OcclusionStrength", 0f);
            material.DisableKeyword("_OCCLUSIONMAP");

            // A soft self-lit gold tint keeps the coins readable in tree shadow
            // without adding extra scene lights.
            material.SetTexture("_EmissionMap", baseColor);
            material.SetColor("_EmissionColor", new Color(1f, 0.82f, 0.42f) * 0.9f);
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;

            material.SetFloat("_Surface", 0f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.SetFloat("_ZWrite", 1f);
            material.SetFloat("_Cull", (float)CullMode.Back);
            material.SetFloat("_EnvironmentReflections", 1f);
            material.SetFloat("_SpecularHighlights", 1f);
            material.SetOverrideTag("RenderType", "Opaque");
            material.renderQueue = -1;
            material.enableInstancing = true;

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateBurstMaterial()
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
                throw new InvalidOperationException("No particle shader was found for the coin burst.");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(BurstMaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, BurstMaterialPath);
            }

            material.shader = shader;
            material.name = "CoinBurst";
            material.mainTexture = Texture2D.whiteTexture;
            material.color = Color.white;
            material.renderQueue = (int)RenderQueue.Transparent;
            material.enableInstancing = false;

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D LoadTexture(string path)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null)
                throw new FileNotFoundException("Coin texture not found: " + path);
            return texture;
        }

        private static GameObject CreateCoinPrefab(Material gold, Material burst)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null)
                throw new FileNotFoundException("Coin model not found: " + ModelPath);

            GameObject root = new GameObject("Coin");

            SphereCollider trigger = root.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = TriggerRadius;
            trigger.center = Vector3.zero;

            CoinCollectible coin = root.AddComponent<CoinCollectible>();

            GameObject visual = Object.Instantiate(model);
            visual.name = "CoinVisual";
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = Vector3.zero;

            // The FBX is authored as a flat disc with a 270 degree axis-correction
            // rotation. Identity makes it stand upright, facing local Z.
            visual.transform.localRotation = Quaternion.identity;

            // Normalise the imported centimetre scale to the requested diameter.
            Physics.SyncTransforms();
            Bounds bounds = GetCombinedBounds(visual);
            float diameter = Mathf.Max(bounds.size.x, bounds.size.y);
            float scale = CoinDiameter / Mathf.Max(0.0001f, diameter);
            visual.transform.localScale *= scale;

            Physics.SyncTransforms();
            bounds = GetCombinedBounds(visual);
            visual.transform.localPosition = -bounds.center;

            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);

            foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterial = gold;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }

            SerializedObject serialized = new SerializedObject(coin);
            serialized.FindProperty("visual").objectReferenceValue = visual.transform;
            serialized.FindProperty("burstMaterial").objectReferenceValue = burst;
            serialized.FindProperty("rotationSpeed").floatValue = 150f;
            serialized.FindProperty("bobHeight").floatValue = 0.06f;
            serialized.FindProperty("bobSpeed").floatValue = 2.2f;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();

            if (prefab == null)
                throw new InvalidOperationException("Failed to save the coin prefab: " + PrefabPath);

            return prefab;
        }

        private static Bounds GetCombinedBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                throw new InvalidOperationException("The coin model has no renderer.");

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);

            return bounds;
        }

        private static int PlaceCoins(GameObject prefab)
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject oldCoins = GameObject.Find("Coins");
            if (oldCoins != null)
                Object.DestroyImmediate(oldCoins);

            // The counter owns the HUD, so reuse it instead of recreating it.
            CoinCounter coinCounter = Object.FindAnyObjectByType<CoinCounter>();
            if (coinCounter == null)
            {
                GameObject counter = new GameObject("Coin Counter");
                SceneManager.MoveGameObjectToScene(counter, scene);
                coinCounter = counter.AddComponent<CoinCounter>();
            }

            BikeGameBootstrap bootstrap = Object.FindAnyObjectByType<BikeGameBootstrap>();
            if (bootstrap == null)
                throw new InvalidOperationException("BikeGameBootstrap was not found in " + ScenePath);

            SerializedObject bootstrapSerialized = new SerializedObject(bootstrap);
            Vector3 spawn = bootstrapSerialized.FindProperty("spawnPosition").vector3Value;
            Vector3 euler = bootstrapSerialized.FindProperty("spawnEulerAngles").vector3Value;

            Vector3 forward = Vector3.ProjectOnPlane(Quaternion.Euler(euler) * Vector3.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward;
            forward.Normalize();

            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null)
                throw new InvalidOperationException("The forest scene has no active terrain.");

            TerrainData terrainData = terrain.terrainData;
            int roadLayer = FindRoadLayer(terrainData);

            GameObject coinsRoot = new GameObject("Coins");
            SceneManager.MoveGameObjectToScene(coinsRoot, scene);

            // The coin disc faces local +Z, so yaw it to look back down the road at
            // the approaching rider. Each coin also gets a different starting phase
            // so the whole line never turns edge-on at the same moment.
            float baseYaw = Mathf.Atan2(-forward.x, -forward.z) * Mathf.Rad2Deg;
            const float phaseStep = 360f / 10f;

            for (int i = 0; i < LateralOffsets.Length; i++)
            {
                float distance = FirstCoinDistance + i * CoinSpacing;
                Vector3 center = spawn + forward * distance;
                Vector3 position = center + right * LateralOffsets[i];

                // Keep every coin on the painted road. If the lateral offset
                // leaves the road, fall back to the centre line.
                if (roadLayer >= 0 && SampleRoad(terrain, terrainData, roadLayer, position) < 0.65f)
                    position = center;

                float ground = terrain.transform.position.y + terrain.SampleHeight(position);
                position.y = ground + CoinHoverHeight;

                GameObject coin = (GameObject)PrefabUtility.InstantiatePrefab(prefab, coinsRoot.transform);
                coin.name = string.Format("Coin {0:00}", i + 1);
                coin.transform.position = position;
                coin.transform.rotation = Quaternion.Euler(0f, baseYaw + i * phaseStep, 0f);

                Debug.LogFormat("BikeCoins: coin {0} at {1} (road={2:0.00})",
                    coin.name, position.ToString("F2"),
                    roadLayer >= 0 ? SampleRoad(terrain, terrainData, roadLayer, position) : -1f);
            }

            SerializedObject counterSerialized = new SerializedObject(coinCounter);
            counterSerialized.FindProperty("totalCoins").intValue = LateralOffsets.Length;
            counterSerialized.FindProperty("collectedCoins").intValue = 0;
            counterSerialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            return LateralOffsets.Length;
        }

        private static int FindRoadLayer(TerrainData data)
        {
            TerrainLayer[] layers = data.terrainLayers;
            for (int i = 0; i < layers.Length; i++)
            {
                if (layers[i] != null &&
                    layers[i].name.IndexOf("road", StringComparison.OrdinalIgnoreCase) >= 0)
                    return i;
            }

            return -1;
        }

        private static float SampleRoad(Terrain terrain, TerrainData data, int layer, Vector3 worldPosition)
        {
            Vector3 local = worldPosition - terrain.transform.position;
            float tx = Mathf.Clamp01(local.x / data.size.x);
            float tz = Mathf.Clamp01(local.z / data.size.z);

            int width = data.alphamapResolution;
            int height = data.alphamapResolution;
            int x = Mathf.Clamp(Mathf.FloorToInt(tx * width), 0, width - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt(tz * height), 0, height - 1);

            return data.GetAlphamaps(x, z, 1, 1)[0, 0, layer];
        }
    }
}
