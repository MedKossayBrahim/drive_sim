using System;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace BikeGame.EditorTools
{
    /// <summary>
    /// Builds the finish-line soccer ball from the supplied glTF model in the
    /// project's "ball" folder. Unity has no built-in glTF importer in this
    /// project, so this tool reads the glTF buffers directly, creates a real
    /// Mesh asset, builds a URP material from the supplied base colour, normal
    /// and metallic/roughness textures, saves a prefab under Resources and
    /// updates the LevelCompleteBall in the ride scene.
    ///
    /// Run from Tools > Bike Game > Setup Finish Ball or with:
    ///   Unity.exe -batchmode -projectPath <project> \
    ///       -executeMethod BikeGame.EditorTools.BikeBallSetup.Run -logFile <log>
    /// </summary>
    public static class BikeBallSetup
    {
        private const string ScenePath = "Assets/BikeGame/Scenes/ForestBikeRide.unity";
        private const string BallFolder = "Assets/BikeGame/Ball";
        private const string SourceFolder = BallFolder + "/Source";
        private const string TextureFolder = BallFolder + "/Textures";
        private const string MaterialFolder = BallFolder + "/Materials";
        private const string PrefabFolder = "Assets/Resources";

        private const string MeshPath = BallFolder + "/SoccerBallMesh.asset";
        private const string MaterialPath = MaterialFolder + "/SoccerBall.mat";
        private const string PrefabPath = PrefabFolder + "/SoccerBall.prefab";

        private const string BaseColorPath = TextureFolder + "/Ball_BaseColor.jpg";
        private const string NormalPath = TextureFolder + "/Ball_Normal.png";
        private const string MetallicRoughnessSourcePath = SourceFolder + "/Ball_MetallicRoughness.png";
        private const string MetallicSmoothnessPath = TextureFolder + "/Ball_MetallicSmoothness.png";
        private const string OcclusionPath = TextureFolder + "/Ball_Occlusion.png";

        private const float BallRadius = 1.5f;
        private const int DerivedTextureSize = 512;

        [MenuItem("Tools/Bike Game/Setup Finish Ball")]
        public static void SetupFromMenu()
        {
            Setup();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject ball = GameObject.Find("LevelCompleteBall");
            if (ball != null) Selection.activeGameObject = ball;
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
            CopySourceTextures();
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);

            ConfigureTexture(BaseColorPath, false, false, 1024);
            ConfigureTexture(NormalPath, true, true, 1024);

            ConfigureMetallicRoughnessSource();
            GenerateDerivedTextures();
            ConfigureDerivedTexture(MetallicSmoothnessPath, DerivedTextureSize);
            ConfigureDerivedTexture(OcclusionPath, DerivedTextureSize);
            AssetDatabase.DeleteAsset(MetallicRoughnessSourcePath);

            Mesh mesh = BuildMesh();
            Material material = CreateMaterial();
            GameObject prefab = CreatePrefab(mesh, material);
            UpdateScene();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.LogFormat(
                "BikeBall: setup complete. mesh={0} verts={1}, material={2}, prefab={3}, radius={4}",
                MeshPath, mesh.vertexCount, MaterialPath, prefab != null ? PrefabPath : "missing", BallRadius);
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/BikeGame", "Ball");
            EnsureFolder(BallFolder, "Source");
            EnsureFolder(BallFolder, "Textures");
            EnsureFolder(BallFolder, "Materials");
            EnsureFolder("Assets", "Resources");
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }

        private static void CopySourceTextures()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string sourceRoot = Path.Combine(projectRoot, "ball", "textures");

            CopyIfNewer(Path.Combine(sourceRoot, "initialShadingGroup_baseColor.jpeg"), BaseColorPath);
            CopyIfNewer(Path.Combine(sourceRoot, "initialShadingGroup_normal.png"), NormalPath);
            CopyIfNewer(Path.Combine(sourceRoot, "initialShadingGroup_metallicRoughness.png"),
                MetallicRoughnessSourcePath);
        }

        private static void CopyIfNewer(string source, string target)
        {
            if (!File.Exists(source))
            {
                if (!File.Exists(target))
                    throw new FileNotFoundException("Ball source texture not found: " + source);
                return;
            }

            if (File.Exists(target) &&
                File.GetLastWriteTimeUtc(target) >= File.GetLastWriteTimeUtc(source) &&
                new FileInfo(target).Length == new FileInfo(source).Length)
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.Copy(source, target, true);
        }

        private static void ConfigureTexture(string path, bool normalMap, bool linear, int maxSize)
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
            importer.maxTextureSize = maxSize;
            importer.textureCompression = TextureImporterCompression.Compressed;
            if (normalMap)
                importer.normalmapFilter = TextureImporterNormalFilter.Standard;

            importer.SaveAndReimport();
        }

        private static void ConfigureMetallicRoughnessSource()
        {
            TextureImporter importer = AssetImporter.GetAtPath(MetallicRoughnessSourcePath) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("Could not load texture importer: " + MetallicRoughnessSourcePath);

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static void GenerateDerivedTextures()
        {
            Texture2D source = AssetDatabase.LoadAssetAtPath<Texture2D>(MetallicRoughnessSourcePath);
            if (source == null)
                throw new InvalidOperationException("Could not read the metallic/roughness texture.");

            Color32[] sourcePixels = source.GetPixels32();
            int width = source.width;
            int height = source.height;

            Color32[] metallicSmoothness = new Color32[DerivedTextureSize * DerivedTextureSize];
            Color32[] occlusion = new Color32[DerivedTextureSize * DerivedTextureSize];

            for (int y = 0; y < DerivedTextureSize; y++)
            {
                int sy = Mathf.Clamp(y * height / DerivedTextureSize, 0, height - 1);
                for (int x = 0; x < DerivedTextureSize; x++)
                {
                    int sx = Mathf.Clamp(x * width / DerivedTextureSize, 0, width - 1);
                    Color32 pixel = sourcePixels[sy * width + sx];

                    // URP Lit expects metallic in R and smoothness in A.
                    // glTF stores metallic in B and roughness in G.
                    byte smoothness = (byte)(255 - pixel.g);
                    metallicSmoothness[y * DerivedTextureSize + x] = new Color32(0, 0, 0, smoothness);

                    // URP samples the G channel for occlusion; glTF stores it in R.
                    occlusion[y * DerivedTextureSize + x] = new Color32(pixel.r, pixel.r, pixel.r, 255);
                }
            }

            WritePng(MetallicSmoothnessPath, DerivedTextureSize, DerivedTextureSize, metallicSmoothness);
            WritePng(OcclusionPath, DerivedTextureSize, DerivedTextureSize, occlusion);
        }

        private static void WritePng(string path, int width, int height, Color32[] pixels)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        private static void ConfigureDerivedTexture(string path, int maxSize)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("Could not load texture importer: " + path);

            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = true;
            importer.isReadable = false;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = maxSize;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SaveAndReimport();
        }

        private static Mesh BuildMesh()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string gltfPath = Path.Combine(projectRoot, "ball", "scene.gltf");
            if (!File.Exists(gltfPath))
                throw new FileNotFoundException("Ball glTF not found: " + gltfPath);

            JObject gltf = JObject.Parse(File.ReadAllText(gltfPath));
            string binName = (string)gltf["buffers"][0]["uri"];
            string binPath = Path.Combine(Path.GetDirectoryName(gltfPath) ?? projectRoot, binName);
            byte[] bin = File.ReadAllBytes(binPath);

            JArray nodes = (JArray)gltf["nodes"];
            Matrix4x4 world = Matrix4x4.identity;
            int meshIndex = -1;

            foreach (JToken root in (JArray)gltf["scenes"][0]["nodes"])
            {
                if (FindMeshNode(nodes, (int)root, Matrix4x4.identity, out world, out meshIndex))
                    break;
            }

            if (meshIndex < 0)
                throw new InvalidOperationException("The ball glTF has no mesh node.");

            JObject primitive = (JObject)gltf["meshes"][meshIndex]["primitives"][0];
            JObject attributes = (JObject)primitive["attributes"];

            Vector3[] positions = ReadVec3(gltf, bin, (int)attributes["POSITION"]);
            Vector3[] normals = ReadVec3(gltf, bin, (int)attributes["NORMAL"]);
            Vector2[] uvs = ReadVec2(gltf, bin, (int)attributes["TEXCOORD_0"]);
            int[] indices = ReadIndices(gltf, bin, (int)primitive["indices"]);

            for (int i = 0; i < positions.Length; i++)
            {
                positions[i] = world.MultiplyPoint3x4(positions[i]);
                normals[i] = world.MultiplyVector(normals[i]).normalized;

                // glTF UV origin is top-left, Unity's is bottom-left.
                uvs[i] = new Vector2(uvs[i].x, 1f - uvs[i].y);
            }

            // glTF and Unity both treat counter-clockwise triangles as front
            // facing, so the index order is kept as-is.

            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (mesh == null)
            {
                mesh = new Mesh { name = "SoccerBall" };
                AssetDatabase.CreateAsset(mesh, MeshPath);
            }

            mesh.Clear();
            mesh.vertices = positions;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = indices;
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);

            return mesh;
        }

        private static bool FindMeshNode(JArray nodes, int index, Matrix4x4 parent,
            out Matrix4x4 world, out int meshIndex)
        {
            JObject node = (JObject)nodes[index];
            Matrix4x4 local = Matrix4x4.identity;

            if (node["matrix"] != null)
            {
                JArray matrix = (JArray)node["matrix"];
                local = new Matrix4x4(
                    new Vector4((float)matrix[0], (float)matrix[1], (float)matrix[2], (float)matrix[3]),
                    new Vector4((float)matrix[4], (float)matrix[5], (float)matrix[6], (float)matrix[7]),
                    new Vector4((float)matrix[8], (float)matrix[9], (float)matrix[10], (float)matrix[11]),
                    new Vector4((float)matrix[12], (float)matrix[13], (float)matrix[14], (float)matrix[15]));
            }
            else
            {
                Vector3 translation = node["translation"] != null
                    ? ParseVector3((JArray)node["translation"])
                    : Vector3.zero;
                Quaternion rotation = node["rotation"] != null
                    ? ParseQuaternion((JArray)node["rotation"])
                    : Quaternion.identity;
                Vector3 scale = node["scale"] != null
                    ? ParseVector3((JArray)node["scale"])
                    : Vector3.one;
                local = Matrix4x4.TRS(translation, rotation, scale);
            }

            Matrix4x4 current = parent * local;

            if (node["mesh"] != null)
            {
                world = current;
                meshIndex = (int)node["mesh"];
                return true;
            }

            if (node["children"] != null)
            {
                foreach (JToken child in (JArray)node["children"])
                {
                    if (FindMeshNode(nodes, (int)child, current, out world, out meshIndex))
                        return true;
                }
            }

            world = Matrix4x4.identity;
            meshIndex = -1;
            return false;
        }

        private static Vector3 ParseVector3(JArray values)
        {
            return new Vector3((float)values[0], (float)values[1], (float)values[2]);
        }

        private static Quaternion ParseQuaternion(JArray values)
        {
            return new Quaternion((float)values[0], (float)values[1], (float)values[2], (float)values[3]);
        }

        private static void GetAccessor(JObject gltf, int accessorIndex,
            out int offset, out int count, out int stride, out int componentType)
        {
            JObject accessor = (JObject)gltf["accessors"][accessorIndex];
            JObject bufferView = (JObject)gltf["bufferViews"][(int)accessor["bufferView"]];

            int bufferViewOffset = bufferView["byteOffset"] != null ? (int)bufferView["byteOffset"] : 0;
            int accessorOffset = accessor["byteOffset"] != null ? (int)accessor["byteOffset"] : 0;

            componentType = (int)accessor["componentType"];
            count = (int)accessor["count"];
            int components = TypeComponents((string)accessor["type"]);
            int componentSize = ComponentSize(componentType);

            stride = bufferView["byteStride"] != null
                ? (int)bufferView["byteStride"]
                : components * componentSize;
            offset = bufferViewOffset + accessorOffset;
        }

        private static Vector3[] ReadVec3(JObject gltf, byte[] bin, int accessorIndex)
        {
            GetAccessor(gltf, accessorIndex, out int offset, out int count, out int stride,
                out int componentType);
            if (componentType != 5126)
                throw new InvalidOperationException("Expected a float accessor.");

            Vector3[] values = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                int p = offset + i * stride;
                values[i] = new Vector3(
                    BitConverter.ToSingle(bin, p),
                    BitConverter.ToSingle(bin, p + 4),
                    BitConverter.ToSingle(bin, p + 8));
            }

            return values;
        }

        private static Vector2[] ReadVec2(JObject gltf, byte[] bin, int accessorIndex)
        {
            GetAccessor(gltf, accessorIndex, out int offset, out int count, out int stride,
                out int componentType);
            if (componentType != 5126)
                throw new InvalidOperationException("Expected a float accessor.");

            Vector2[] values = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                int p = offset + i * stride;
                values[i] = new Vector2(BitConverter.ToSingle(bin, p), BitConverter.ToSingle(bin, p + 4));
            }

            return values;
        }

        private static int[] ReadIndices(JObject gltf, byte[] bin, int accessorIndex)
        {
            GetAccessor(gltf, accessorIndex, out int offset, out int count, out int stride,
                out int componentType);

            int[] values = new int[count];
            for (int i = 0; i < count; i++)
            {
                int p = offset + i * stride;
                values[i] = componentType switch
                {
                    5125 => (int)BitConverter.ToUInt32(bin, p),
                    5123 => BitConverter.ToUInt16(bin, p),
                    5121 => bin[p],
                    _ => throw new InvalidOperationException("Unsupported index component type.")
                };
            }

            return values;
        }

        private static int TypeComponents(string type)
        {
            return type switch
            {
                "SCALAR" => 1,
                "VEC2" => 2,
                "VEC3" => 3,
                "VEC4" => 4,
                "MAT4" => 16,
                _ => throw new InvalidOperationException("Unsupported accessor type: " + type)
            };
        }

        private static int ComponentSize(int componentType)
        {
            return componentType switch
            {
                5120 => 1,
                5121 => 1,
                5122 => 2,
                5123 => 2,
                5125 => 4,
                5126 => 4,
                _ => throw new InvalidOperationException("Unsupported component type: " + componentType)
            };
        }

        private static Material CreateMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
                throw new InvalidOperationException("Universal Render Pipeline/Lit shader was not found.");

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            material.shader = shader;
            material.name = "SoccerBall";

            Texture2D baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseColorPath);
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
            Texture2D metallicSmoothness = AssetDatabase.LoadAssetAtPath<Texture2D>(MetallicSmoothnessPath);
            Texture2D occlusion = AssetDatabase.LoadAssetAtPath<Texture2D>(OcclusionPath);

            material.SetTexture("_BaseMap", baseColor);
            material.SetTexture("_MainTex", baseColor);
            material.SetColor("_BaseColor", Color.white);
            material.SetColor("_Color", Color.white);

            material.SetTexture("_BumpMap", normal);
            material.SetFloat("_BumpScale", 1f);
            material.EnableKeyword("_NORMALMAP");

            material.SetTexture("_MetallicGlossMap", metallicSmoothness);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 1f);
            material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.SetFloat("_WorkflowMode", 1f);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");

            material.SetTexture("_OcclusionMap", occlusion);
            material.SetFloat("_OcclusionStrength", 1f);
            material.EnableKeyword("_OCCLUSIONMAP");

            material.SetFloat("_Surface", 0f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.SetFloat("_ZWrite", 1f);
            // The glTF material is double sided; keep the same look in URP.
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetFloat("_EnvironmentReflections", 1f);
            material.SetFloat("_SpecularHighlights", 1f);
            material.SetOverrideTag("RenderType", "Opaque");
            material.renderQueue = -1;
            material.enableInstancing = true;

            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreatePrefab(Mesh mesh, Material material)
        {
            GameObject root = new GameObject("SoccerBall");

            MeshFilter filter = root.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            MeshRenderer renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();

            if (prefab == null)
                throw new InvalidOperationException("Failed to save the ball prefab: " + PrefabPath);

            return prefab;
        }

        private static void UpdateScene()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            GameObject ball = GameObject.Find("LevelCompleteBall");
            if (ball == null)
            {
                LevelCompleteTrigger trigger = Object.FindAnyObjectByType<LevelCompleteTrigger>();
                if (trigger != null) ball = trigger.gameObject;
            }

            if (ball == null)
                throw new InvalidOperationException("LevelCompleteBall was not found in " + ScenePath);

            CapsuleCollider capsule = ball.GetComponent<CapsuleCollider>();
            if (capsule != null)
                Object.DestroyImmediate(capsule, true);

            SphereCollider sphere = ball.GetComponent<SphereCollider>();
            if (sphere == null)
                sphere = ball.AddComponent<SphereCollider>();

            sphere.isTrigger = true;
            sphere.radius = BallRadius;
            sphere.center = Vector3.zero;

            BallVisualSetup visual = ball.GetComponent<BallVisualSetup>();
            if (visual == null)
                visual = ball.AddComponent<BallVisualSetup>();

            SerializedObject serialized = new SerializedObject(visual);
            SerializedProperty modelScale = serialized.FindProperty("modelScale");
            if (modelScale != null)
                modelScale.floatValue = BallRadius;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            if (ball.GetComponent<LevelCompleteTrigger>() == null)
                ball.AddComponent<LevelCompleteTrigger>();

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Failed to save " + ScenePath);

            Debug.LogFormat("BikeBall: scene updated. ball={0} position={1} radius={2}",
                ball.name, ball.transform.position.ToString("F2"), BallRadius);
        }
    }
}
