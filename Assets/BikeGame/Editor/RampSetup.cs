using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace BikeGame.EditorTools
{
    /// <summary>One-shot setup used to add the downloaded ramp to the forest ride scene.</summary>
    public static class RampSetup
    {
        private const string ScenePath = "Assets/BikeGame/Scenes/ForestBikeRide.unity";
        private const string PrefabPath = "Assets/BikeGame/Ramp/Source/Ramp.fbx";
        private const string MaterialPath = "Assets/BikeGame/Ramp/Materials/Ramp.mat";
        private const string TextureFolder = "Assets/BikeGame/Ramp/Textures";
        private const string PreviewPath = @"C:\Users\FMB SEMAH\AppData\Local\Temp\opencode\ramp_preview.png";

        [MenuItem("Tools/Bike Game/Add Ramp To Forest Ride")]
        public static void SetupFromMenu()
        {
            Setup();
        }

        public static void Setup()
        {
            ConfigureRampImport();
            Material material = CreateRampMaterial();

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            BikeGameBootstrap bootstrap = Object.FindAnyObjectByType<BikeGameBootstrap>();
            if (bootstrap == null)
                throw new System.InvalidOperationException("BikeGameBootstrap was not found in the scene.");

            SerializedObject bootstrapSerialized = new SerializedObject(bootstrap);
            Vector3 spawnPosition = bootstrapSerialized.FindProperty("spawnPosition").vector3Value;
            float spawnYaw = bootstrapSerialized.FindProperty("spawnEulerAngles").vector3Value.y;
            Vector3 forward = Quaternion.Euler(0f, spawnYaw, 0f) * Vector3.forward;
            forward.y = 0f;
            forward.Normalize();

            GameObject existing = GameObject.Find("Jump Ramp");
            if (existing != null)
                Object.DestroyImmediate(existing);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null)
                throw new System.InvalidOperationException("Ramp prefab was not found: " + PrefabPath);

            GameObject ramp = (GameObject)Object.Instantiate(prefab);
            ramp.name = "Jump Ramp";
            SceneManager.MoveGameObjectToScene(ramp, scene);

            if (!TryGetMeshInfo(ramp, out Vector3 meshMin, out Vector3 meshMax,
                    out bool positiveZUp, out float lowAvgY, out float highAvgY))
                throw new System.InvalidOperationException("Ramp has no readable mesh filter.");

            Vector3 meshSize = meshMax - meshMin;
            const float targetWidth = 3.2f;
            const float targetHeight = 1.25f;
            const float targetLength = 4.2f;

            Vector3 scale = new Vector3(
                targetWidth / Mathf.Max(0.0001f, meshSize.x),
                targetHeight / Mathf.Max(0.0001f, meshSize.y),
                targetLength / Mathf.Max(0.0001f, meshSize.z));

            // The downloaded model is a quarter-pipe kicker. Its low edge is the
            // side the bicycle approaches from. Detect whether the mesh rises
            // towards +Z or -Z, then aim that side along the road direction.
            Quaternion rotation = Quaternion.LookRotation(positiveZUp ? forward : -forward, Vector3.up);
            float lowLocalZ = positiveZUp ? meshMin.z : meshMax.z;
            Vector3 lowLocalOffset = new Vector3(0f, 0f, lowLocalZ * scale.z);
            Vector3 rampFront = spawnPosition + forward * 7.0f;
            Vector3 rootPosition = rampFront - rotation * lowLocalOffset;

            float ground = SampleGroundHeight(rampFront);
            const float sink = 0.04f;
            rootPosition.y = ground - meshMin.y * scale.y - sink;

            ramp.transform.SetPositionAndRotation(rootPosition, rotation);
            ramp.transform.localScale = scale;

            AddColliders(ramp, material);
            MarkStatic(ramp);

            GameObject root = bootstrap.gameObject;
            HeartSystem hearts = root.GetComponent<HeartSystem>();
            if (hearts == null)
                hearts = root.AddComponent<HeartSystem>();

            SerializedObject heartsSerialized = new SerializedObject(hearts);
            heartsSerialized.FindProperty("ramp").objectReferenceValue = ramp.transform;
            heartsSerialized.FindProperty("maxHearts").intValue = 3;
            heartsSerialized.ApplyModifiedPropertiesWithoutUndo();

            Renderer[] renderers = ramp.GetComponentsInChildren<Renderer>(true);
            Bounds worldBounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(rootPosition, Vector3.one);
            for (int i = 1; i < renderers.Length; i++)
                worldBounds.Encapsulate(renderers[i].bounds);

            Debug.LogFormat(
                "RAMP SETUP meshMin={0} meshMax={1} positiveZUp={2} lowAvgY={3:0.000} highAvgY={4:0.000} scale={5} root={6} rotation={7} front={8} ground={9:0.00} worldSize={10}",
                meshMin, meshMax, positiveZUp, lowAvgY, highAvgY, scale,
                rootPosition, rotation.eulerAngles, rampFront, ground, worldBounds.size);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            RenderPreview(scene, worldBounds, spawnPosition, forward);
        }

        private static void ConfigureRampImport()
        {
            TextureImporter[] importers =
            {
                ConfigureTexture(Path.Combine(TextureFolder, "Ramp_Base_Color.jpg"), false),
                ConfigureTexture(Path.Combine(TextureFolder, "Ramp_Normal_OGL.jpg"), true),
                ConfigureTexture(Path.Combine(TextureFolder, "Ramp_Metallic.jpg"), false),
                ConfigureTexture(Path.Combine(TextureFolder, "Ramp_Roughness.jpg"), false),
                ConfigureTexture(Path.Combine(TextureFolder, "internal_ground_ao_texture.jpeg"), false)
            };

            for (int i = 0; i < importers.Length; i++)
            {
                if (importers[i] != null)
                    importers[i].SaveAndReimport();
            }

            ModelImporter modelImporter = AssetImporter.GetAtPath(PrefabPath) as ModelImporter;
            if (modelImporter != null)
            {
                modelImporter.isReadable = true;
                modelImporter.importAnimation = false;
                modelImporter.animationType = ModelImporterAnimationType.None;
                modelImporter.importBlendShapes = false;
                modelImporter.importCameras = false;
                modelImporter.importLights = false;
                modelImporter.importVisibility = false;
                modelImporter.globalScale = 1f;
                modelImporter.useFileScale = true;
                modelImporter.SaveAndReimport();
            }

            AssetDatabase.Refresh();
        }

        private static TextureImporter ConfigureTexture(string path, bool normalMap)
        {
            path = path.Replace('\\', '/');
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return null;

            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.textureType = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = !normalMap;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 2;
            return importer;
        }

        private static Material CreateRampMaterial()
        {
            string folder = Path.GetDirectoryName(MaterialPath).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(folder))
                AssetDatabase.CreateFolder("Assets/BikeGame/Ramp", "Materials");

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null)
                throw new System.InvalidOperationException("No URP lit shader was found.");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            material.shader = shader;
            material.name = "Ramp";

            Texture2D baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(
                TextureFolder + "/Ramp_Base_Color.jpg");
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(
                TextureFolder + "/Ramp_Normal_OGL.jpg");

            material.SetTexture("_BaseMap", baseColor);
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", 0.8f);
            material.SetFloat("_Metallic", 0.18f);
            material.SetFloat("_Smoothness", 0.32f);
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_ZWrite", 1f);
            material.EnableKeyword("_NORMALMAP");
            material.enableInstancing = true;

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }

        private static bool TryGetMeshInfo(GameObject ramp, out Vector3 meshMin, out Vector3 meshMax,
            out bool positiveZUp, out float lowAvgY, out float highAvgY)
        {
            meshMin = Vector3.zero;
            meshMax = Vector3.zero;
            positiveZUp = true;
            lowAvgY = 0f;
            highAvgY = 0f;

            MeshFilter filter = ramp.GetComponentInChildren<MeshFilter>(true);
            if (filter == null || filter.sharedMesh == null) return false;

            Mesh mesh = filter.sharedMesh;
            Vector3[] vertices = mesh.vertices;
            if (vertices.Length == 0) return false;

            meshMin = vertices[0];
            meshMax = vertices[0];

            for (int i = 1; i < vertices.Length; i++)
            {
                meshMin = Vector3.Min(meshMin, vertices[i]);
                meshMax = Vector3.Max(meshMax, vertices[i]);
            }

            float depth = Mathf.Max(0.0001f, meshMax.z - meshMin.z);
            float lowSum = 0f;
            int lowCount = 0;
            float highSum = 0f;
            int highCount = 0;

            for (int i = 0; i < vertices.Length; i++)
            {
                float t = (vertices[i].z - meshMin.z) / depth;
                if (t <= 0.2f)
                {
                    lowSum += vertices[i].y;
                    lowCount++;
                }
                if (t >= 0.8f)
                {
                    highSum += vertices[i].y;
                    highCount++;
                }
            }

            lowAvgY = lowCount > 0 ? lowSum / lowCount : meshMin.y;
            highAvgY = highCount > 0 ? highSum / highCount : meshMax.y;
            positiveZUp = highAvgY >= lowAvgY;
            return true;
        }

        private static void AddColliders(GameObject ramp, Material material)
        {
            MeshFilter[] filters = ramp.GetComponentsInChildren<MeshFilter>(true);

            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter filter = filters[i];
                Mesh mesh = filter.sharedMesh;
                if (mesh == null) continue;

                MeshCollider collider = filter.GetComponent<MeshCollider>();
                if (collider == null)
                    collider = filter.gameObject.AddComponent<MeshCollider>();

                collider.sharedMesh = mesh;
                collider.convex = false;
                collider.isTrigger = false;

                Renderer renderer = filter.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Material[] materials = new Material[Mathf.Max(1, renderer.sharedMaterials.Length)];
                    for (int m = 0; m < materials.Length; m++)
                        materials[m] = material;

                    renderer.sharedMaterials = materials;
                    renderer.shadowCastingMode = ShadowCastingMode.On;
                    renderer.receiveShadows = true;
                }
            }
        }

        private static void MarkStatic(GameObject ramp)
        {
            StaticEditorFlags flags = StaticEditorFlags.BatchingStatic |
                                      StaticEditorFlags.OccluderStatic |
                                      StaticEditorFlags.OccludeeStatic;
            GameObjectUtility.SetStaticEditorFlags(ramp, flags);
        }

        private static float SampleGroundHeight(Vector3 position)
        {
            Terrain terrain = Terrain.activeTerrain;
            if (terrain != null)
                return terrain.transform.position.y + terrain.SampleHeight(position);

            if (Physics.Raycast(position + Vector3.up * 80f, Vector3.down,
                    out RaycastHit hit, 400f, ~0, QueryTriggerInteraction.Ignore))
            {
                return hit.point.y;
            }

            return position.y;
        }

        private static void RenderPreview(Scene scene, Bounds rampBounds, Vector3 spawnPosition, Vector3 forward)
        {
            GameObject cameraObject = new GameObject("Ramp Preview Camera");
            SceneManager.MoveGameObjectToScene(cameraObject, scene);

            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 58f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 300f;

            Vector3 target = rampBounds.center + Vector3.up * 0.5f;
            Vector3 cameraPosition = spawnPosition - forward * 5.5f + Vector3.up * 3.4f;
            camera.transform.position = cameraPosition;
            camera.transform.rotation = Quaternion.LookRotation(target - cameraPosition, Vector3.up);

            RenderTexture targetTexture = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            camera.targetTexture = targetTexture;
            camera.Render();

            RenderTexture.active = targetTexture;
            Texture2D image = new Texture2D(targetTexture.width, targetTexture.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, targetTexture.width, targetTexture.height), 0, 0);
            image.Apply();

            File.WriteAllBytes(PreviewPath, image.EncodeToPNG());

            RenderTexture.active = previous;
            camera.targetTexture = null;
            Object.DestroyImmediate(image);
            Object.DestroyImmediate(targetTexture);
            Object.DestroyImmediate(cameraObject);

            Debug.Log("RAMP SETUP preview written to " + PreviewPath);
        }
    }
}
