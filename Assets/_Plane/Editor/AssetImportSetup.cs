#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Project.Plane
{
    /// <summary>Prépare des assets dérivés sans réécrire les modèles sources.</summary>
    public sealed class AssetImportSetup : EditorWindow
    {
        private const string Models = "Assets/_Plane/Models";
        private const string Prefabs = "Assets/_Plane/Prefabs/Imported";
        private const string Materials = "Assets/_Plane/Materials/Imported";
        private const string Textures = "Assets/_Plane/Textures/Imported";
        private const string Report = "docs/import_report.md";
        private const string VerifiedPlaneHash = "46da438bc0ff0b82996763c959c210603db7ff341682e5372addb360700f07fa";

        [Serializable]
        private sealed class ScaleRule
        {
            [SerializeField] private string model;
            [SerializeField, Min(0.001f)] private float scale = 1f;
            [SerializeField] private bool groundPivot;
            public string Model => model;
            public float Scale => scale;
            public bool GroundPivot => groundPivot;
            public ScaleRule(string name, float value, bool ground = false)
            { model = name; scale = value; groundPivot = ground; }
        }

        private enum NoseDirection { VerifiedSource, PositiveX, NegativeX, PositiveZ, NegativeZ }

        [SerializeField] private ScaleRule[] scaleRules =
        {
            new ScaleRule("plane_body", 11.4f), new ScaleRule("plane_propeller", 5f),
            new ScaleRule("checkpoint_ring", 4.35f), new ScaleRule("crate", 2.3f),
            new ScaleRule("drop_target", 21f, true), new ScaleRule("hangar", 47f, true),
            new ScaleRule("control_tower", 28.5f, true), new ScaleRule("pine_tree", 17f, true),
            new ScaleRule("rocks", 5.7f, true), new ScaleRule("wind_turbine", 114f, true)
        };
        [SerializeField] private NoseDirection planeNose = NoseDirection.VerifiedSource;
        [SerializeField] private int androidMaxSize = 1024;
        [SerializeField] private int smallAndroidMaxSize = 512;
        [SerializeField] private TextureImporterFormat androidFormat = TextureImporterFormat.ASTC_6x6;
        private bool preparing;

        [MenuItem("Sky Courier/0 - Prepare Imported Assets", false, 0)]
        public static void PrepareImportedAssets()
        {
            GetWindow<AssetImportSetup>("Import Sky Courier").Prepare();
        }

        [MenuItem("Sky Courier/Imported Assets Settings", false, 1)]
        private static void OpenSettings() => GetWindow<AssetImportSetup>("Import Sky Courier");

        private void OnGUI()
        {
            EditorGUILayout.HelpBox("Réglages de préparation. Le menu 0 lance la génération ; " +
                "les réglages ci-dessous permettent de la relancer. Les sources restent intactes.", MessageType.Info);
            var settings = new SerializedObject(this);
            settings.Update();
            EditorGUILayout.PropertyField(settings.FindProperty("scaleRules"), true);
            EditorGUILayout.PropertyField(settings.FindProperty("planeNose"));
            EditorGUILayout.PropertyField(settings.FindProperty("androidMaxSize"));
            EditorGUILayout.PropertyField(settings.FindProperty("smallAndroidMaxSize"));
            EditorGUILayout.PropertyField(settings.FindProperty("androidFormat"));
            settings.ApplyModifiedProperties();
            using (new EditorGUI.DisabledScope(preparing || EditorApplication.isPlayingOrWillChangePlaymode))
                if (GUILayout.Button("Préparer les assets et écrire le rapport")) Prepare();
        }

        private void Prepare()
        {
            if (preparing || EditorApplication.isPlayingOrWillChangePlaymode) return;
            preparing = true;
            var entries = new List<string>();
            var problems = new List<string>();
            var sourceHashes = new Dictionary<string, string>();
            int completed = 0;
            try
            {
                ValidateSettings();
                // Le parcours disque inclut les GLB dont le package n'a pas encore réussi l'import.
                var paths = Directory.GetFiles(Models, "*", SearchOption.AllDirectories)
                    .Select(NormalizePath).Where(IsModel).OrderBy(p => p, StringComparer.Ordinal).ToArray();
                if (paths.Length == 0) throw new InvalidOperationException("Aucun modèle trouvé.");
                foreach (string path in paths) sourceHashes[path] = HashFile(path);
                EnsureFolder(Prefabs);
                EnsureFolder(Materials);
                EnsureFolder(Textures);
                for (int i = 0; i < paths.Length; i++)
                {
                    string path = paths[i];
                    EditorUtility.DisplayProgressBar("Sky Courier — préparation", path, (float)i / paths.Length);
                    var notes = new List<string>();
                    try
                    {
                        ConfigureModelImporter(path, notes);
                        PrepareModel(path, notes, entries);
                        completed++;
                    }
                    catch (Exception exception)
                    {
                        notes.Add("ÉCHEC : " + exception.Message);
                        Debug.LogError($"[Sky Courier] {path} : {exception}");
                    }
                    foreach (string note in notes.Distinct()) problems.Add($"`{path}` : {note}");
                }
            }
            catch (Exception exception)
            {
                problems.Add("Préparation interrompue : " + exception.Message);
                Debug.LogException(exception);
            }
            finally
            {
                try
                {
                    AssetDatabase.SaveAssets();
                    foreach (var pair in sourceHashes)
                        if (HashFile(pair.Key) != pair.Value)
                            problems.Add("SOURCE MODIFIÉE, vérifier immédiatement : " + pair.Key);
                    WriteReport(entries, problems, completed, sourceHashes.Count);
                    Debug.Log($"[Sky Courier] {completed}/{sourceHashes.Count} prefabs préparés. " +
                        $"Rapport : {Report}. Les limites de l'importeur et les contrôles manuels y sont détaillés.");
                }
                finally
                {
                    EditorUtility.ClearProgressBar();
                    preparing = false;
                }
            }
        }

        private void ValidateSettings()
        {
            if (scaleRules == null || scaleRules.Any(r => r == null || string.IsNullOrWhiteSpace(r.Model)
                || float.IsNaN(r.Scale) || float.IsInfinity(r.Scale) || r.Scale <= 0f))
                throw new InvalidOperationException("Les règles doivent avoir un nom et une échelle positive finie.");
            if (scaleRules.GroupBy(r => r.Model).Any(g => g.Count() > 1))
                throw new InvalidOperationException("Une règle d'échelle apparaît plusieurs fois.");
            if (!IsTextureSize(androidMaxSize) || !IsTextureSize(smallAndroidMaxSize))
                throw new InvalidOperationException("Les tailles Android doivent être des puissances de deux entre 32 et 16384.");
            if (!androidFormat.ToString().StartsWith("ASTC_", StringComparison.Ordinal))
                throw new InvalidOperationException("Choisir une compression ASTC pour Android.");
        }

        private static bool IsTextureSize(int n) => n >= 32 && n <= 16384 && (n & (n - 1)) == 0;
        private static string NormalizePath(string path) => path.Replace('\\', '/');
        private static bool IsModel(string path)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            return extension == ".fbx" || extension == ".glb" || extension == ".gltf"
                || extension == ".obj" || extension == ".dae" || extension == ".dxf";
        }

        private static void ConfigureModelImporter(string path, List<string> notes)
        {
            var importer = AssetImporter.GetAtPath(path);
            if (importer is ModelImporter model)
            {
                if (model.isReadable || model.addCollider)
                {
                    model.isReadable = false;
                    model.addCollider = false;
                    model.SaveAndReimport();
                }
                return;
            }
            if (importer == null || importer.GetType().FullName != "GLTFast.Editor.GltfImporter")
                throw new InvalidOperationException("Importeur absent ou inconnu. Résoudre glTFast 6.20.0 dans Package Manager.");

            // GltfImporter est interne : on utilise ses champs sérialisés, sans dépendance C# privée.
            var settings = new SerializedObject(importer);
            settings.Update();
            var textureRead = settings.FindProperty("importSettings.texturesReadable");
            if (textureRead != null && textureRead.propertyType == SerializedPropertyType.Boolean)
                textureRead.boolValue = false;
            else notes.Add("glTFast : réglage TexturesReadable non trouvé dans cette version.");
            if (settings.ApplyModifiedPropertiesWithoutUndo()) importer.SaveAndReimport();
            // La version 6.20 ne propose ni Read/Write des meshes ni génération de colliders.
            // Ne pas inventer un ModelImporter ou une propriété m_IsReadable sur ce ScriptedImporter.
            notes.Add("glTFast : Read/Write des meshes non exposé ; TexturesReadable désactivé si disponible. " +
                "Pas d'option Generate Colliders ; les colliders éventuels sont retirés du prefab dérivé.");
        }

        private void PrepareModel(string path, List<string> notes, List<string> entries)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            string relative = path.Substring(Models.Length + 1);
            string key = NormalizePath(Path.ChangeExtension(relative, null));
            string prefabPath = Prefabs + "/" + key + ".prefab";
            var rule = scaleRules.FirstOrDefault(r => r.Model == name);
            float scale = rule?.Scale ?? 1f;
            int maxSize = name == "pine_tree" || name == "crate" || name == "rocks"
                ? smallAndroidMaxSize : androidMaxSize;
            // Réimporter les dépendances avant d'instancier le modèle évite les références invalidées.
            foreach (string dependency in AssetDatabase.GetDependencies(path, true))
                if (AssetImporter.GetAtPath(dependency) is TextureImporter)
                    ConfigureTexture(dependency, maxSize, notes);
            var imageCopies = ExtractEmbeddedImages(path, key, maxSize, notes);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null) throw new InvalidOperationException("Le modèle n'a pas de GameObject importé.");
            Scene scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject(name);
                SceneManager.MoveGameObjectToScene(root, scene);
                root.transform.localScale = Vector3.one * scale;
                var holder = new GameObject("Model");
                SceneManager.MoveGameObjectToScene(holder, scene);
                holder.transform.SetParent(root.transform, false);
                // Copie autonome : les modifications ne sont jamais appliquées au modèle importé.
                var instance = Object.Instantiate(asset, holder.transform, false);
                instance.name = asset.name;
                foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                foreach (var collider in instance.GetComponentsInChildren<Collider2D>(true)) Object.DestroyImmediate(collider);
                if (name == "plane_body") OrientPlane(path, holder.transform, notes);
                var converted = new Dictionary<Material, Material>();
                foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        var original = materials[i];
                        if (original == null) { notes.Add("Un slot de matériau est vide."); continue; }
                        if (!converted.TryGetValue(original, out var prepared))
                        {
                            prepared = PrepareMaterial(original, path, key, converted.Count, maxSize, imageCopies, notes);
                            converted.Add(original, prepared);
                        }
                        materials[i] = prepared;
                    }
                    renderer.sharedMaterials = materials;
                }
                Bounds bounds = ModelBounds(root);
                if (rule != null && rule.GroundPivot)
                {
                    // La translation est exprimée dans l'espace du parent déjà mis à l'échelle.
                    holder.transform.localPosition += Vector3.up * (-bounds.min.y / scale);
                    bounds = ModelBounds(root);
                    if (Mathf.Abs(bounds.min.y) > 0.001f) notes.Add("Pivot au sol hors tolérance de 1 mm.");
                }
                long triangles = CountTriangles(root);
                EnsureFolder(NormalizePath(Path.GetDirectoryName(prefabPath)));
                var saved = PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool success);
                if (!success || saved == null) throw new IOException("Échec d'enregistrement du prefab : " + prefabPath);
                string dimensions = VectorText(bounds.size);
                Debug.Log($"[Sky Courier] {prefabPath} — {triangles} triangles ; dimensions {dimensions} m ; " +
                    $"min {VectorText(bounds.min)} ; max {VectorText(bounds.max)} ; scale {scale}.", saved);
                entries.Add($"| `{relative}` | `{prefabPath}` | {triangles} | {Number(scale)} | " +
                    $"{dimensions} | {VectorText(bounds.min)} | {VectorText(bounds.max)} | {(rule?.GroundPivot == true ? "Sol" : "Source")} |");
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }

        private void OrientPlane(string path, Transform holder, List<string> notes)
        {
            Vector3 nose;
            if (planeNose == NoseDirection.VerifiedSource)
            {
                if (HashFile(path) != VerifiedPlaneHash)
                {
                    notes.Add("Nez non vérifié : source différente. Choisir explicitement son axe dans Imported Assets Settings.");
                    return;
                }
                // Vue de dessus du fichier identifié : moteur arrondi en -X glTF, empennage en +X.
                // glTFast inverse X à l'import : le nez devient +X Unity. Z est l'envergure.
                nose = Vector3.right;
                notes.Add("Nez vérifié pour le SHA-256 connu : -X glTF devient +X Unity ; " +
                    "rotation du conteneur Model de -90° Y pour viser +Z. L'axe long Z est l'envergure.");
            }
            else
            {
                nose = planeNose == NoseDirection.PositiveX ? Vector3.right :
                    planeNose == NoseDirection.NegativeX ? Vector3.left :
                    planeNose == NoseDirection.PositiveZ ? Vector3.forward : Vector3.back;
                notes.Add("Axe du nez fourni manuellement : " + planeNose);
            }
            // Le cas -Z donne exactement 180° autour de Y ; +Z donne 0°.
            float yaw = Vector3.SignedAngle(nose, Vector3.forward, Vector3.up);
            holder.localRotation = Quaternion.Euler(0f, yaw, 0f);
            float alignment = Vector3.Dot(holder.localRotation * nose, Vector3.forward);
            if (alignment < 0.9999f) throw new InvalidOperationException("Le test du nez vers Z+ a échoué.");
            notes.Add($"Test direction : dot(rotation × nez, Z+) = {Number(alignment)} ; rotation Y = {Number(yaw)}°.");
        }

        private static Bounds ModelBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true)
                .Where(r => r is MeshRenderer || r is SkinnedMeshRenderer).ToArray();
            if (renderers.Length == 0) throw new InvalidOperationException("Aucun renderer de mesh pour calculer les bounds.");
            // Boîte AABB de rendu, valable avec Read/Write désactivé et pour les skins en pose initiale.
            Bounds result = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) result.Encapsulate(renderer.bounds);
            return result;
        }

        private static long CountTriangles(GameObject root)
        {
            long count = 0;
            var meshes = root.GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh)
                .Concat(root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Select(r => r.sharedMesh));
            // On compte les instances, sans lire mesh.triangles (qui nécessite Read/Write).
            foreach (var mesh in meshes)
            {
                if (mesh == null) continue;
                for (int i = 0; i < mesh.subMeshCount; i++)
                {
                    long n = mesh.GetIndexCount(i);
                    if (mesh.GetTopology(i) == MeshTopology.Triangles) count += n / 3;
                    else if (mesh.GetTopology(i) == MeshTopology.Quads) count += n / 4 * 2;
                }
            }
            return count;
        }

        private Material PrepareMaterial(Material original, string sourceModelPath, string key, int index, int maxSize,
            List<Texture2D> embedded, List<string> notes)
        {
            var copy = new Material(original);
            string shader = original.shader != null ? original.shader.name : "";
            // Les Shader Graphs de glTFast sont déjà compatibles URP : préserver leurs paramètres PBR.
            bool compatible = shader.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal)
                || shader.StartsWith("Shader Graphs/glTF", StringComparison.Ordinal);
            if (!compatible)
            {
                var lit = Shader.Find("Universal Render Pipeline/Lit");
                if (lit == null) { Object.DestroyImmediate(copy); throw new InvalidOperationException("URP/Lit introuvable."); }
                Object.DestroyImmediate(copy);
                copy = new Material(lit);
                CopyColor(original, copy, "_BaseColor", "_BaseColor", "_Color", "baseColorFactor");
                CopyColor(original, copy, "_EmissionColor", "_EmissionColor", "emissiveFactor");
                CopyFloat(original, copy, "_Metallic", "_Metallic", "metallicFactor");
                CopyFloat(original, copy, "_Smoothness", "_Smoothness", "_Glossiness");
                if (original.HasProperty("roughnessFactor")) copy.SetFloat("_Smoothness", 1f - original.GetFloat("roughnessFactor"));
                CopyTexture(original, copy, "_BaseMap", "_BaseMap", "_MainTex", "baseColorTexture");
                CopyTexture(original, copy, "_BumpMap", "_BumpMap", "normalTexture");
                CopyTexture(original, copy, "_EmissionMap", "_EmissionMap", "emissiveTexture");
                CopyTexture(original, copy, "_OcclusionMap", "_OcclusionMap");
                CopyTexture(original, copy, "_MetallicGlossMap", "_MetallicGlossMap");
                CopyFloat(original, copy, "_BumpScale", "_BumpScale");
                ConfigureSurface(original, copy);
                if (copy.GetTexture("_BumpMap") != null) copy.EnableKeyword("_NORMALMAP");
                if (copy.GetTexture("_MetallicGlossMap") != null) copy.EnableKeyword("_METALLICSPECGLOSSMAP");
                if (copy.GetTexture("_EmissionMap") != null || copy.GetColor("_EmissionColor").maxColorComponent > 0f)
                    copy.EnableKeyword("_EMISSION");
                notes.Add("Matériau converti vers URP/Lit : " + original.name + ". Vérifier le rendu (conversion PBR partielle).");
                if (original.HasProperty("metallicRoughnessTexture") && original.GetTexture("metallicRoughnessTexture") != null)
                    notes.Add("Texture metallic/roughness glTF à reconditionner manuellement pour URP/Lit.");
            }
            foreach (string property in copy.GetTexturePropertyNames())
            {
                var texture = copy.GetTexture(property) as Texture2D;
                if (texture == null) continue;
                string texturePath = AssetDatabase.GetAssetPath(texture);
                if (AssetImporter.GetAtPath(texturePath) is TextureImporter)
                    ConfigureTexture(texturePath, maxSize, notes);
                else if (texturePath == sourceModelPath && embedded.Count == 1)
                {
                    // Les neuf GLB générés actuels ont une seule image PNG de baseColor.
                    // Remplacement par les octets originaux : pas de RenderTexture ni de réencodage.
                    copy.SetTexture(property, embedded[0]);
                }
                else if (!string.IsNullOrEmpty(texturePath) && !texturePath.StartsWith("Resources/", StringComparison.Ordinal))
                    notes.Add("Texture sans remappage certain : " + texture.name + ". Vérifier son Android override.");
            }
            string destination = Materials + "/" + key + "/material_" + index.ToString("D3") + ".mat";
            EnsureFolder(NormalizePath(Path.GetDirectoryName(destination)));
            var existing = AssetDatabase.LoadAssetAtPath<Material>(destination);
            if (existing != null)
            {
                EditorUtility.CopySerialized(copy, existing);
                Object.DestroyImmediate(copy);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(copy, destination);
            return copy;
        }

        private static void ConfigureSurface(Material source, Material target)
        {
            float mode = source.HasProperty("_Mode") ? source.GetFloat("_Mode") : 0f;
            bool transparent = mode >= 2f || source.HasProperty("_Surface") && source.GetFloat("_Surface") > 0f;
            bool clip = mode == 1f || source.HasProperty("_AlphaClip") && source.GetFloat("_AlphaClip") > 0f;
            target.SetFloat("_Surface", transparent ? 1f : 0f);
            target.SetFloat("_AlphaClip", clip ? 1f : 0f);
            CopyFloat(source, target, "_Cutoff", "_Cutoff");
            CopyFloat(source, target, "_Cull", "_Cull");
            target.SetFloat("_SrcBlend", (float)(transparent ? BlendMode.SrcAlpha : BlendMode.One));
            target.SetFloat("_DstBlend", (float)(transparent ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
            target.SetFloat("_ZWrite", transparent ? 0f : 1f);
            if (clip) target.EnableKeyword("_ALPHATEST_ON");
            if (transparent) target.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            target.SetOverrideTag("RenderType", transparent ? "Transparent" : clip ? "TransparentCutout" : "Opaque");
            target.renderQueue = transparent ? (int)RenderQueue.Transparent : clip ? (int)RenderQueue.AlphaTest : -1;
        }

        private static void CopyTexture(Material from, Material to, string destination, params string[] candidates)
        {
            foreach (string source in candidates)
                if (from.HasProperty(source) && from.GetTexture(source) != null)
                {
                    to.SetTexture(destination, from.GetTexture(source));
                    to.SetTextureScale(destination, from.GetTextureScale(source));
                    to.SetTextureOffset(destination, from.GetTextureOffset(source));
                    return;
                }
        }
        private static void CopyColor(Material from, Material to, string destination, params string[] candidates)
        { foreach (string source in candidates) if (from.HasProperty(source)) { to.SetColor(destination, from.GetColor(source)); return; } }
        private static void CopyFloat(Material from, Material to, string destination, params string[] candidates)
        { foreach (string source in candidates) if (from.HasProperty(source)) { to.SetFloat(destination, from.GetFloat(source)); return; } }

        private void ConfigureTexture(string path, int maxSize, List<string> notes)
        {
            if (!path.StartsWith("Assets/_Plane/", StringComparison.Ordinal))
            { notes.Add("Texture hors périmètre, non modifiée : " + path); return; }
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("TextureImporter absent : " + path);
            var android = importer.GetPlatformTextureSettings("Android");
            int size = maxSize;
            bool changed = !android.overridden || android.maxTextureSize != size || android.format != androidFormat
                || importer.isReadable || android.textureCompression != TextureImporterCompression.Compressed;
            android.name = "Android";
            android.overridden = true;
            android.maxTextureSize = size;
            android.format = androidFormat;
            android.textureCompression = TextureImporterCompression.Compressed;
            importer.isReadable = false;
            if (changed) { importer.SetPlatformTextureSettings(android); importer.SaveAndReimport(); }
        }

        // Structures minimales du chunk JSON GLB : JsonUtility requiert des champs sérialisables.
        [Serializable] private sealed class GlbDocument { public GlbImage[] images = null; public GlbView[] bufferViews = null; }
        [Serializable] private sealed class GlbImage { public string mimeType = null; public int bufferView = -1; }
        [Serializable] private sealed class GlbView { public int buffer = 0; public int byteOffset = 0; public int byteLength = 0; }

        private List<Texture2D> ExtractEmbeddedImages(string modelPath, string key, int maxSize, List<string> notes)
        {
            var result = new List<Texture2D>();
            if (Path.GetExtension(modelPath).ToLowerInvariant() != ".glb") return result;
            byte[] bytes = File.ReadAllBytes(modelPath);
            GlbDocument document = null;
            int binaryOffset = -1, binaryLength = 0;
            using (var stream = new MemoryStream(bytes, false))
            using (var reader = new BinaryReader(stream))
            {
                if (reader.ReadUInt32() != 0x46546C67 || reader.ReadUInt32() != 2 || reader.ReadUInt32() != bytes.Length)
                    throw new InvalidDataException("En-tête GLB invalide.");
                while (stream.Position < stream.Length)
                {
                    int length = checked((int)reader.ReadUInt32());
                    uint type = reader.ReadUInt32();
                    if (length < 0 || length > stream.Length - stream.Position) throw new InvalidDataException("Chunk GLB invalide.");
                    if (type == 0x4E4F534A) document = JsonUtility.FromJson<GlbDocument>(Encoding.UTF8.GetString(reader.ReadBytes(length)));
                    else
                    {
                        if (type == 0x004E4942) { binaryOffset = checked((int)stream.Position); binaryLength = length; }
                        stream.Position += length;
                    }
                }
            }
            if (document?.images == null) return result;
            // Une correspondance unique évite de confondre baseColor, normal et textures PBR.
            if (document.images.Length != 1)
            { notes.Add("Plusieurs images GLB : extraction/remappage automatique ignorés par sécurité."); return result; }
            var image = document.images[0];
            if (image.bufferView < 0 || document.bufferViews == null || image.bufferView >= document.bufferViews.Length)
            { notes.Add("Image GLB externe ou sans bufferView : extraction ignorée."); return result; }
            var view = document.bufferViews[image.bufferView];
            if (binaryOffset < 0 || view.buffer != 0 || view.byteOffset < 0 || view.byteLength <= 0
                || (long)view.byteOffset + view.byteLength > binaryLength) throw new InvalidDataException("Image hors du chunk BIN.");
            string extension = image.mimeType == "image/png" ? ".png" : image.mimeType == "image/jpeg" ? ".jpg" : null;
            if (extension == null) { notes.Add("Format d'image embarquée non pris en charge : " + image.mimeType); return result; }
            string path = Textures + "/" + key + "/image_0" + extension;
            EnsureFolder(NormalizePath(Path.GetDirectoryName(path)));
            var imageBytes = new byte[view.byteLength];
            Buffer.BlockCopy(bytes, binaryOffset + view.byteOffset, imageBytes, 0, view.byteLength);
            if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(imageBytes)) File.WriteAllBytes(path, imageBytes);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            ConfigureTexture(path, maxSize, notes);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) throw new IOException("Texture dérivée non importée : " + path);
            result.Add(texture);
            return result;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = NormalizePath(Path.GetDirectoryName(path));
            EnsureFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(path))))
                throw new IOException("Impossible de créer le dossier " + path);
        }
        private static string HashFile(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        private static string Number(float value) => value.ToString("0.######", CultureInfo.InvariantCulture);
        private static string VectorText(Vector3 v) => $"{Number(v.x)} × {Number(v.y)} × {Number(v.z)}";

        private static void WriteReport(List<string> entries, List<string> problems, int completed, int total)
        {
            var text = new StringBuilder("# Rapport de préparation des assets\n\n");
            text.AppendLine($"Exécution Unity : {DateTimeOffset.Now:O}. Version : {Application.unityVersion}. " +
                $"Prefabs enregistrés : **{completed}/{total}**.\n");
            text.AppendLine("Bounds AABB de rendu en mètres dans une scène de prévisualisation, racine à l'origine, " +
                "après échelle, rotation et déplacement du pivot. Pose initiale ; animations non évaluées. " +
                "Triangles comptés par instance de mesh, sans activer Read/Write.\n");
            text.AppendLine("| Modèle | Prefab | Triangles | Échelle uniforme racine | Dimensions X × Y × Z (m) | Min XYZ (m) | Max XYZ (m) | Pivot |\n" +
                "| --- | --- | --- | --- | --- | --- | --- | --- |");
            foreach (string entry in entries) text.AppendLine(entry);
            text.AppendLine("\n## Problèmes, limites et tests\n");
            foreach (string problem in problems.Distinct()) text.AppendLine("- " + problem.Replace("\n", " "));
            if (problems.Count == 0) text.AppendLine("Aucun problème détecté automatiquement.");
            text.AppendLine("\n## Ajustements manuels\n\n" +
                "- Vérifier la Console et le rendu des prefabs, particulièrement les matériaux convertis et les transparences.\n" +
                "- Vérifier visuellement le nez de plane_body vers Z+ ; si la source change, sélectionner son axe dans Imported Assets Settings.\n" +
                "- Vérifier que les six pivots au sol ont min Y = 0 ; assembler l'hélice et régler ses positions séparément.\n" +
                "- Android : confirmer ASTC dans les Texture Import Settings et sur appareil ; les copies de textures sont utilisées par les matériaux dérivés.\n" +
                "- glTFast 6.20 n'expose pas Read/Write des meshes ; ne pas confondre cette limite avec TexturesReadable.\n" +
                "- Les prefabs sont régénérés aux mêmes chemins : effectuer les modifications de gameplay dans des variants.\n" +
                "- Aucun collider de gameplay n'est créé ; les ajouter plus tard selon les besoins.\n");
            text.AppendLine("## Fichiers et sources\n\n" +
                "Créés ou actualisés : Prefabs/Imported, Materials/Imported, Textures/Imported sous Assets/_Plane, " +
                "métadonnées d'import et docs/import_report.md. Les fichiers modèles binaires sont contrôlés par SHA-256.\n\n" +
                "Références : [importeur glTFast](https://github.com/Unity-Technologies/com.unity.cloud.gltfast/blob/main/Packages/com.unity.cloud.gltfast/Editor/Scripts/GltfImporter.cs), " +
                "[réglages glTFast](https://github.com/Unity-Technologies/com.unity.cloud.gltfast/blob/main/Packages/com.unity.cloud.gltfast/Runtime/Scripts/ImportSettings.cs), " +
                "[conversion des axes](https://github.com/Unity-Technologies/com.unity.cloud.gltfast/blob/main/Packages/com.unity.cloud.gltfast/Runtime/Scripts/Jobs.cs).\n");
            string destination = Path.GetFullPath(Path.Combine(Application.dataPath, "..", Report));
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.WriteAllText(destination, text.ToString(), new UTF8Encoding(false));
        }
    }
}
#endif
