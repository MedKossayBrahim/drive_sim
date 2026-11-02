#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Project.Plane
{
    /// <summary>Prépare le modèle importé et un parcours ordonné ; ne génère aucune géométrie d'anneau.</summary>
    public static class RingBuilder
    {
        private const string Source = "Assets/_Plane/Models/Checkpoint/checkpoint_ring.glb";
        private const string Imported = "Assets/_Plane/Prefabs/Imported/Checkpoint/checkpoint_ring.prefab";
        private const string Destination = "Assets/_Plane/Prefabs/Checkpoint_Ring.prefab";
        private const string Materials = "Assets/_Plane/Materials/Checkpoints";
        private const string ScenePath = "Assets/_Plane/Scenes/Plane_Scene.unity";
        private const float ModelScale = 4.35f;
        private static readonly string[] States = { "Inactive", "Active", "Passed" };

        [MenuItem("Sky Courier/3 - Create Ring Prefab", false, 30)]
        public static void CreateRingPrefab()
        {
            Scene preview = default;
            try
            {
                VerifyCrossingMath();
                var imported = AssetDatabase.LoadAssetAtPath<GameObject>(Imported);
                var source = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
                if (imported == null || source == null)
                    throw new InvalidOperationException("checkpoint_ring importé absent. Exécuter le menu 0 et vérifier glTFast.");
                EnsureFolder(Materials);
                var raw = source.GetComponentsInChildren<MeshFilter>(true);
                if (raw.Length != 5) throw new InvalidOperationException("Le modèle doit avoir les cinq meshes de l'inventaire.");
                var originalSlots = new Dictionary<Mesh, Material[]>();
                foreach (var filter in raw)
                {
                    var renderer = filter.GetComponent<Renderer>();
                    if (filter.sharedMesh == null || renderer == null) throw new InvalidOperationException("Mesh ou renderer manquant.");
                    originalSlots[filter.sharedMesh] = renderer.sharedMaterials;
                    Debug.Log($"[Sky Courier] Mesh réel : {filter.sharedMesh.name} ; objet : {filter.name} ; " +
                        $"matériaux : {string.Join(", ", renderer.sharedMaterials.Select(m => m.name))}. " +
                        (filter.sharedMesh.name == "ring" ? "Anneau central lumineux." : "Élément décoratif du capture point."));
                }
                var originals = originalSlots.Values.SelectMany(m => m).Distinct().ToArray();
                if (originals.Length != 4) throw new InvalidOperationException("Quatre matériaux d'origine attendus.");
                InspectAnimation(imported);
                var variants = CreateVariants(originals);
                preview = EditorSceneManager.NewPreviewScene();
                var root = new GameObject("Checkpoint_Ring");
                SceneManager.MoveGameObjectToScene(root, preview);
                var pivot = new GameObject("VisualPivot");
                SceneManager.MoveGameObjectToScene(pivot, preview);
                pivot.transform.SetParent(root.transform, false);
                var model = Object.Instantiate(imported);
                SceneManager.MoveGameObjectToScene(model, preview);
                model.transform.SetParent(pivot.transform, false);
                model.name = "ImportedModel";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                // Le prefab du menu 0 est déjà à 4,35 : remplacer l'échelle, surtout ne pas la multiplier à nouveau.
                model.transform.localScale = Vector3.one * ModelScale;
                foreach (var collider in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                foreach (var collider in model.GetComponentsInChildren<Collider2D>(true)) Object.DestroyImmediate(collider);
                foreach (var animator in model.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                foreach (var animation in model.GetComponentsInChildren<Animation>(true))
                { animation.playAutomatically = false; animation.enabled = false; }

                var central = model.GetComponentsInChildren<MeshFilter>(true).Single(f => f.sharedMesh.name == "ring");
                Vector3 dimensions = central.sharedMesh.bounds.size;
                int thin = dimensions.x <= dimensions.y && dimensions.x <= dimensions.z ? 0 : dimensions.y <= dimensions.z ? 1 : 2;
                Vector3 axis = thin == 0 ? Vector3.right : thin == 1 ? Vector3.up : Vector3.forward;
                Vector3 normal = central.transform.localToWorldMatrix.MultiplyVector(axis).normalized;
                // Les bounds du mesh central donnent le plan du trou ; les décorations épaisses ne servent pas de repère.
                Vector3 hole = pivot.transform.InverseTransformPoint(central.GetComponent<Renderer>().bounds.center);
                model.transform.localPosition -= hole;
                pivot.transform.localRotation = Quaternion.FromToRotation(normal, Vector3.forward);
                Vector3 corrected = central.transform.localToWorldMatrix.MultiplyVector(axis).normalized;
                if (Vector3.Dot(corrected, root.transform.forward) < 0.9999f || central.GetComponent<Renderer>().bounds.center.magnitude > 0.001f)
                    throw new InvalidOperationException("Le plan central n'est pas aligné/centré sur Z local.");
                Debug.Log($"[Sky Courier] Orientation : axe mince du mesh ring = {axis}, " +
                    $"rotation VisualPivot = {pivot.transform.localEulerAngles}. " +
                    "Centre du trou à l'origine ; normale alignée sur Z+ (dot ≥ 0,9999). Échelle effective du modèle : 4,35.");

                var bindings = new List<Checkpoint.RendererMaterials>();
                foreach (var filter in model.GetComponentsInChildren<MeshFilter>(true))
                {
                    var renderer = filter.GetComponent<Renderer>();
                    var slots = originalSlots[filter.sharedMesh];
                    bindings.Add(new Checkpoint.RendererMaterials(renderer,
                        slots.Select(m => variants[m][0]).ToArray(), slots.Select(m => variants[m][1]).ToArray(),
                        slots.Select(m => variants[m][2]).ToArray()));
                }
                var triggerObject = new GameObject("Trigger");
                SceneManager.MoveGameObjectToScene(triggerObject, preview);
                triggerObject.transform.SetParent(root.transform, false);
                var sphere = triggerObject.AddComponent<SphereCollider>();
                sphere.isTrigger = true;
                sphere.radius = 8f;
                sphere.center = Vector3.zero;
                var checkpoint = triggerObject.AddComponent<Checkpoint>();
                int plane = LayerMask.NameToLayer("Plane");
                checkpoint.Configure(bindings.ToArray(), plane >= 0 ? 1 << plane : ~0);
                foreach (var transform in root.GetComponentsInChildren<Transform>(true)) transform.gameObject.isStatic = false;
                if (root.GetComponentsInChildren<Collider>(true).Length != 1)
                    throw new InvalidOperationException("Seule la sphère trigger doit avoir un collider.");
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, Destination, out bool success);
                if (!success || prefab == null) throw new IOException("Enregistrement du prefab impossible.");
                AssetDatabase.SaveAssets();
                Selection.activeObject = prefab;
                Debug.Log("[Sky Courier] Checkpoint_Ring.prefab créé : modèle source conservé, diamètre ≈20 m, " +
                    "sphère trigger de rayon 8 m, 5 meshes, 3 familles de 4 matériaux. " +
                    "Animator désactivé sur cette copie, aucune collision solide.", prefab);
            }
            catch (Exception exception) { Debug.LogException(exception); }
            finally { if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview); }
        }

        private static void InspectAnimation(GameObject imported)
        {
            foreach (var clip in AssetDatabase.LoadAllAssetsAtPath(Source).OfType<AnimationClip>())
            {
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                string bindings = string.Join(" ; ", AnimationUtility.GetCurveBindings(clip)
                    .Select(b => b.path + ":" + b.propertyName));
                Debug.Log($"[Sky Courier] Animation réelle : {clip.name}, durée {clip.length:F3} s, " +
                    $"loopTime={settings.loopTime}, isLooping={clip.isLooping}, legacy={clip.legacy}. Courbes : {bindings}. " +
                    "Source ring-spin : rotation axiale Y du nœud ring, 0→135→270→360° à 0/1,5/3/4 s. " +
                    "Lecture désactivée dans le prefab de gameplay pour laisser orientation et pulse sous contrôle du checkpoint.");
            }
            foreach (var animator in imported.GetComponentsInChildren<Animator>(true))
                Debug.Log($"[Sky Courier] Animator importé : {animator.name}, contrôleur assigné = {animator.runtimeAnimatorController != null}.");
        }

        private static Dictionary<Material, Material[]> CreateVariants(Material[] originals)
        {
            ColorUtility.TryParseHtmlString("#FFC914", out Color active);
            ColorUtility.TryParseHtmlString("#2EC4B6", out Color passed);
            var result = new Dictionary<Material, Material[]>();
            foreach (var original in originals)
            {
                var family = new Material[3];
                for (int i = 0; i < States.Length; i++)
                {
                    string name = "Ring_" + States[i] + (original.name == "glow" ? "" : "_" + original.name);
                    string path = Materials + "/" + name + ".mat";
                    var copy = new Material(original) { name = name };
                    Color color = i == 1 ? active : i == 2 ? passed : new Color(0.45f, 0.47f, 0.5f, 0.22f);
                    Color emission = i == 1 ? active * 2f : i == 2 ? passed * 0.35f : Color.black;
                    string colorProperty = copy.HasProperty("baseColorFactor") ? "baseColorFactor" : "_BaseColor";
                    string emissionProperty = copy.HasProperty("emissiveFactor") ? "emissiveFactor" : "_EmissionColor";
                    if (!copy.HasProperty(colorProperty) || !copy.HasProperty(emissionProperty))
                        throw new InvalidOperationException("Propriétés couleur/émission absentes : " + original.name);
                    copy.SetColor(colorProperty, color);
                    copy.SetColor(emissionProperty, emission);
                    // Les matériaux initialement non émissifs exigent aussi le mot-clé du shader.
                    string emissionKeyword = emissionProperty == "emissiveFactor" ? "_EMISSIVE" : "_EMISSION";
                    if (i != 0) copy.EnableKeyword(emissionKeyword);
                    if (i == 0) EnableTransparency(copy);
                    var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (existing != null)
                    {
                        EditorUtility.CopySerialized(copy, existing);
                        Object.DestroyImmediate(copy);
                        EditorUtility.SetDirty(existing);
                        family[i] = existing;
                    }
                    else { AssetDatabase.CreateAsset(copy, path); family[i] = copy; }
                }
                result.Add(original, family);
            }
            return result;
        }

        private static void EnableTransparency(Material material)
        {
            // L'alpha seul ne suffit pas sur un shader opaque : seule exception aux changements couleur/émission.
            // Les propriétés PBR d'origine (rugosité, métal, etc.) et le shader sont conservés.
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            SetFloat(material, "_Surface", 1f);
            SetFloat(material, "_SrcBlend", (float)BlendMode.SrcAlpha);
            SetFloat(material, "_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            SetFloat(material, "_SrcBlendAlpha", (float)BlendMode.One);
            SetFloat(material, "_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            SetFloat(material, "_ZWrite", 0f);
            material.SetShaderPassEnabled("DepthOnly", false);
            material.SetShaderPassEnabled("ShadowCaster", false);
        }
        private static void SetFloat(Material material, string property, float value)
        { if (material.HasProperty(property)) material.SetFloat(property, value); }

        [MenuItem("Sky Courier/4 - Place 8 Rings", false, 40)]
        public static void PlaceEightRings()
        {
            try
            {
                VerifyCrossingMath();
                if (!File.Exists(ScenePath)) throw new FileNotFoundException("Créer Plane_Scene avec le menu 2 avant de placer les anneaux.");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Destination);
                if (prefab == null)
                {
                    CreateRingPrefab();
                    prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Destination);
                }
                if (prefab == null) throw new InvalidOperationException("Créer le prefab avec le menu 3.");
                Scene scene = SceneManager.GetSceneByPath(ScenePath);
                if (!scene.IsValid() || !scene.isLoaded)
                    scene = EditorSceneManager.OpenScene(ScenePath,
                        SceneManager.sceneCount == 1 && !SceneManager.GetActiveScene().isDirty ? OpenSceneMode.Single : OpenSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                var gameplay = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Gameplay");
                if (gameplay == null)
                {
                    gameplay = new GameObject("Gameplay");
                    SceneManager.MoveGameObjectToScene(gameplay, scene);
                }
                if ((gameplay.transform.lossyScale - Vector3.one).sqrMagnitude > 0.00001f)
                    throw new InvalidOperationException("Gameplay doit avoir une échelle uniforme de 1 pour conserver le rayon 8 m.");
                foreach (Transform child in gameplay.transform.Cast<Transform>().Where(t => t.name == "Checkpoints").ToArray())
                    Object.DestroyImmediate(child.gameObject);
                var group = new GameObject("Checkpoints");
                SceneManager.MoveGameObjectToScene(group, scene);
                group.transform.SetParent(gameplay.transform, false);
                group.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var points = new[]
                {
                    new Vector3(0f, 60f, 350f), new Vector3(140f, 80f, 640f),
                    new Vector3(260f, 120f, 930f), new Vector3(180f, 160f, 1220f),
                    new Vector3(-40f, 200f, 1510f), new Vector3(-220f, 170f, 1800f),
                    new Vector3(-260f, 120f, 2090f), new Vector3(-80f, 80f, 2380f)
                };
                var checkpoints = new List<Checkpoint>();
                for (int i = 0; i < points.Length; i++)
                {
                    if (i > 0)
                    {
                        float distance = Vector3.Distance(points[i - 1], points[i]);
                        if (distance < 250f || distance > 400f) throw new InvalidOperationException("Espacement hors limites.");
                        Debug.Log($"[Sky Courier] Distance Ring_{i:D2} → Ring_{i + 1:D2} : {distance:F2} m.");
                    }
                    var ring = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    ring.transform.SetParent(group.transform, false);
                    ring.name = "Ring_" + (i + 1).ToString("D2");
                    Vector3 direction = i == 0 ? points[1] - points[0] : i == points.Length - 1 ? points[i] - points[i - 1] : points[i + 1] - points[i - 1];
                    ring.transform.SetPositionAndRotation(points[i], Quaternion.LookRotation(direction, Vector3.up));
                    var checkpoint = ring.GetComponentInChildren<Checkpoint>();
                    if (checkpoint == null) throw new InvalidOperationException("Script Checkpoint absent.");
                    checkpoints.Add(checkpoint);
                }
                var manager = group.AddComponent<CheckpointManager>();
                manager.Configure(checkpoints);
                if (group.GetComponentsInChildren<Checkpoint>(true).Length != 8 ||
                    gameplay.transform.Cast<Transform>().Count(t => t.name == "Checkpoints") != 1)
                    throw new InvalidOperationException("Parcours dupliqué ou nombre d'anneaux invalide.");
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Enregistrement de Plane_Scene impossible.");
                Selection.activeGameObject = group;
                SceneView.lastActiveSceneView?.Frame(new Bounds(new Vector3(0f, 130f, 1365f), new Vector3(700f, 350f, 2200f)), false);
                Debug.Log("[Sky Courier] 8 anneaux enregistrés sous Gameplay/Checkpoints : altitudes 60–200 m, " +
                    "espacements 250–400 m, manager ordonné, limite 180 s, 100 points/anneau. " +
                    "Assigner le Rigidbody au manager ou placer l'avion sur le layer Plane. Seul Checkpoints a été remplacé.", group);
            }
            catch (Exception exception) { Debug.LogException(exception); }
        }

        private static void VerifyCrossingMath()
        {
            if (!Checkpoint.IsForwardPlaneCrossing(new Vector3(0f, 0f, -1f), new Vector3(0f, 0f, 1f), 20f, 8f) ||
                Checkpoint.IsForwardPlaneCrossing(new Vector3(0f, 0f, 1f), new Vector3(0f, 0f, -1f), -20f, 8f) ||
                Checkpoint.IsForwardPlaneCrossing(new Vector3(0f, 0f, -1f), new Vector3(0f, 0f, 1f), -20f, 8f) ||
                Checkpoint.IsForwardPlaneCrossing(new Vector3(9f, 0f, -1f), new Vector3(9f, 0f, 1f), 20f, 8f) ||
                Checkpoint.IsForwardPlaneCrossing(new Vector3(0f, 0f, -9f), new Vector3(0f, 0f, -7f), 20f, 8f))
                throw new InvalidOperationException("Échec des tests géométriques de franchissement.");
            Debug.Log("[Sky Courier] Tests géométriques OK : bon sens accepté ; recul, vitesse négative, hors rayon et simple entrée rejetés.");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(path))))
                throw new IOException("Dossier impossible à créer : " + path);
        }
    }
}
#endif
