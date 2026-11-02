#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Project.Plane
{
    /// <summary>Construction déterministe de l'aéroport, sans modifier les modèles ni SampleScene.</summary>
    public static class AirportBuilder
    {
        private const string Root = "Assets/_Plane";
        private const string Models = Root + "/Models";
        private const string Imported = Root + "/Prefabs/Imported";
        private const string Prefabs = Root + "/Prefabs/Airport";
        private const string Materials = Root + "/Materials/Airport";
        private const string ScenePath = Root + "/Scenes/Plane_Scene.unity";
        private const float SurfaceY = 0.25f;
        private static bool building;

        [MenuItem("Sky Courier/2 - Build Airport", false, 20)]
        public static void BuildAirport()
        {
            if (building || EditorApplication.isPlayingOrWillChangePlaymode) return;
            building = true;
            Scene preview = default;
            try
            {
                // Le catalogue est lu avant la construction ; les noms absents ne provoquent aucun avertissement.
                var catalog = new Catalog();
                EnsureFolder(Prefabs);
                EnsureFolder(Materials);
                EnsureFolder(Root + "/Scenes");
                int ground = EnsureLayer("Ground");
                EnsureLayer("Plane");
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) throw new InvalidOperationException("URP/Lit introuvable.");
                bool createdScene = !File.Exists(ScenePath);
                if (createdScene)
                {
                    string sample = AssetDatabase.FindAssets("SampleScene t:Scene", new[] { "Assets/Scenes" })
                        .Select(AssetDatabase.GUIDToAssetPath)
                        .FirstOrDefault(p => Path.GetFileNameWithoutExtension(p) == "SampleScene");
                    if (sample == null || !AssetDatabase.CopyAsset(sample, ScenePath))
                        throw new IOException("Impossible de dupliquer SampleScene vers " + ScenePath);
                }

                EditorUtility.DisplayProgressBar("Sky Courier — aéroport", "Création des éléments réutilisables", 0.1f);
                preview = EditorSceneManager.NewPreviewScene();
                var context = new BuildContext(catalog, preview, ground, shader);
                GameObject airport = context.Construct();
                string airportPath = Prefabs + "/Airport.prefab";
                var prefab = SavePrefab(airport, airportPath);
                EditorSceneManager.ClosePreviewScene(preview);
                preview = default;

                Scene scene = SceneManager.GetSceneByPath(ScenePath);
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    // Une autre scène sale reste ouverte : aucune modification de l'utilisateur n'est jetée.
                    bool replaceCleanScene = SceneManager.sceneCount == 1 && !SceneManager.GetActiveScene().isDirty;
                    scene = EditorSceneManager.OpenScene(ScenePath,
                        replaceCleanScene ? OpenSceneMode.Single : OpenSceneMode.Additive);
                }
                SceneManager.SetActiveScene(scene);
                foreach (var previous in scene.GetRootGameObjects().Where(g => g.name == "Airport").ToArray())
                    Object.DestroyImmediate(previous);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                instance.name = "Airport";
                EnsureLighting(scene, instance.transform);
                ConfigureCamera(scene, createdScene);
                Validate(scene, instance, ground);
                AssetDatabase.SaveAssets();
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new IOException("Échec de sauvegarde de Plane_Scene.");
                Selection.activeGameObject = instance;
                SceneView.lastActiveSceneView?.Frame(new Bounds(new Vector3(40f, 0f, 0f), new Vector3(500f, 100f, 700f)), false);
                Debug.Log($"[Sky Courier] Aéroport enregistré dans {ScenePath}. " +
                    $"{instance.GetComponentsInChildren<Renderer>(true).Length} renderers ; " +
                    "piste 40×600 m, hangar 25 m de large, tour 20 m de haut, livraison à 1500 m. " +
                    "Relancer remplace uniquement la racine Airport ; SampleScene est conservée.", instance);
            }
            catch (Exception exception) { Debug.LogException(exception); }
            finally
            {
                if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
                EditorUtility.ClearProgressBar();
                building = false;
            }
        }

        private sealed class Catalog
        {
            private readonly Dictionary<string, string> sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            private readonly HashSet<string> prepared = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            public Catalog()
            {
                var paths = AssetDatabase.FindAssets("", new[] { Models })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(p => p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".glb", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(p => p, StringComparer.Ordinal).ToArray();
                foreach (string path in paths)
                    sources[Normalize(Path.ChangeExtension(path.Substring(Models.Length + 1), null))] = path;
                if (AssetDatabase.IsValidFolder(Imported))
                    foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { Imported }))
                        prepared.Add(AssetDatabase.GUIDToAssetPath(guid));
                Debug.Log("[Sky Courier] AssetDatabase.FindAssets — modèles réellement présents :\n" + string.Join("\n", paths));
                Debug.Log("[Sky Courier] AssetDatabase.FindAssets — prefabs Imported :\n" + string.Join("\n", prepared.OrderBy(p => p)));
            }

            public GameObject Resolve(string folder, string name)
            {
                // Liste d'appels explicite : seules les familles autorisées sont consultées.
                if (!sources.TryGetValue(folder + "/" + name, out string source)) return null;
                string derived = Imported + "/" + folder + "/" + Path.GetFileNameWithoutExtension(source) + ".prefab";
                var asset = prepared.Contains(derived) ? AssetDatabase.LoadAssetAtPath<GameObject>(derived) : null;
                if (asset == null) asset = AssetDatabase.LoadAssetAtPath<GameObject>(source);
                if (asset != null) Debug.Log("[Sky Courier] Asset retenu : " + AssetDatabase.GetAssetPath(asset));
                return asset;
            }
        }

        private sealed class BuildContext
        {
            private readonly Catalog catalog;
            private readonly Scene preview;
            private readonly int ground;
            private readonly Dictionary<string, GameObject> prototypes = new Dictionary<string, GameObject>();
            private readonly Material asphalt, grass, white, red, yellow, dark;

            public BuildContext(Catalog catalog, Scene preview, int ground, Shader shader)
            {
                this.catalog = catalog;
                this.preview = preview;
                this.ground = ground;
                asphalt = SolidMaterial("Asphalt", shader, "#2B2B2B", 0.2f);
                grass = SolidMaterial("Grass", shader, "#667858", 0f);
                white = SolidMaterial("Markings_White", shader, "#F1F0E9", 0.1f);
                red = SolidMaterial("Safety_Red", shader, "#C74A3D", 0.1f);
                yellow = SolidMaterial("Safety_Yellow", shader, "#E3B747", 0.1f);
                dark = SolidMaterial("Structures_Dark", shader, "#48525B", 0.15f);
            }

            public GameObject Construct()
            {
                var root = Empty("Airport");
                var pavement = Group(root, "Pavements");
                var runway = Cube("Runway_A", Vector3.zero, new Vector3(40f, 0.5f, 600f), asphalt, true);
                runway.layer = ground;
                Place(SavePrototype("Runway_A", runway), pavement, Vector3.zero);
                Place(SavePrototype("Taxiway", Cube("Taxiway", Vector3.zero,
                    new Vector3(20f, 0.5f, 480f), asphalt, true)), pavement, new Vector3(65f, 0f, 0f));
                Place(SavePrototype("Apron", Cube("Apron", Vector3.zero,
                    new Vector3(100f, 0.5f, 200f), asphalt, true)), pavement, new Vector3(120f, 0f, 0f));
                var connector = SavePrototype("TaxiwayConnector", Cube("TaxiwayConnector", Vector3.zero,
                    new Vector3(65f, 0.5f, 18f), asphalt, true));
                Place(connector, pavement, new Vector3(32.5f, 0f, -180f), 0f, "Taxiway_Entry");
                Place(connector, pavement, new Vector3(32.5f, 0f, 180f), 0f, "Taxiway_Exit");

                var lawn = Primitive(PrimitiveType.Plane, "GrassGround", grass);
                lawn.transform.localScale = new Vector3(400f, 1f, 400f);
                var lawnBox = lawn.AddComponent<BoxCollider>();
                lawnBox.size = new Vector3(10f, 0.1f, 10f);
                lawnBox.center = new Vector3(0f, -0.05f, 0f);
                lawn.layer = ground;
                Place(SavePrototype("GrassGround", lawn), root.transform, new Vector3(0f, -0.25f, 0f));
                Markings(root);
                Buildings(root);
                Decorations(root);
                Delivery(root);
                MarkStatic(root);
                return root;
            }

            private void Markings(GameObject airport)
            {
                var group = Group(airport, "RunwayMarkings");
                var dash = SavePrototype("CenterDash", Cube("CenterDash", Vector3.zero,
                    new Vector3(1f, 0.02f, 20f), white, false));
                for (int i = 0; i < 13; i++) Place(dash, group, new Vector3(0f, 0.27f, -240f + i * 40f), 0f, "CenterDash_" + i.ToString("D2"));
                var threshold = SavePrototype("ThresholdBand", Cube("ThresholdBand", Vector3.zero,
                    new Vector3(32f, 0.02f, 3f), white, false));
                for (int end = -1; end <= 1; end += 2)
                    for (int band = 0; band < 2; band++)
                        Place(threshold, group, new Vector3(0f, 0.27f, end * (270f + band * 10f)), 0f, $"Threshold_{end}_{band}");
                var edge = SavePrototype("RunwayEdge", Cube("RunwayEdge", Vector3.zero,
                    new Vector3(0.25f, 0.02f, 580f), white, false));
                Place(edge, group, new Vector3(-18.5f, 0.27f, 0f), 0f, "RunwayEdge_Left");
                Place(edge, group, new Vector3(18.5f, 0.27f, 0f), 0f, "RunwayEdge_Right");
            }

            private void Buildings(GameObject airport)
            {
                var group = Group(airport, "Buildings");
                var hangar = ModelPrototype("Hangar", "Airport", "hangar", 25f, 0, true, dark);
                var tower = ModelPrototype("ControlTower", "Airport", "control_tower", 20f, 1, true, dark);
                // Le bord proche du hangar est x=100 : environ 80 m au-delà du bord de piste x=20.
                Place(hangar, group, new Vector3(112.5f, SurfaceY, 55f), 180f);
                Place(tower, group, new Vector3(106f, SurfaceY, -65f));
                Place(ModelPrototype("PitsGarage", "Kenney", "pitsGarage", 14f, 0, true, dark), group, new Vector3(150f, SurfaceY, 55f), 180f);
                Place(ModelPrototype("PitsOffice", "Kenney", "pitsOffice", 12f, 0, true, dark), group, new Vector3(150f, SurfaceY, -15f));
                var props = Group(airport, "ApronProps");
                var crate = ModelPrototype("Crate", "Props", "crate", 0.9f, 0, true, yellow);
                for (int i = 0; i < 4; i++) Place(crate, props, new Vector3(130f + 1.5f * i, SurfaceY, -35f), 0f, "Crate_" + i);
            }

            private void Decorations(GameObject airport)
            {
                var barriers = Group(airport, "Barriers");
                var barrierRed = ModelPrototype("BarrierRed", "Kenney", "barrierRed", 3f, 0, true, red);
                var barrierWhite = ModelPrototype("BarrierWhite", "Kenney", "barrierWhite", 3f, 0, true, white);
                for (int i = 0; i < 40; i++) Place(i % 2 == 0 ? barrierRed : barrierWhite, barriers,
                    new Vector3(168f, SurfaceY, -96f + i * 5f), 90f, "Barrier_" + i.ToString("D2"));
                var fences = Group(airport, "Fences");
                var fence = ModelPrototype("Fence", "Kenney", "fenceStraight", 3f, 1, true, dark);
                for (int i = 0; i < 34; i++) Place(fence, fences, new Vector3(180f, -0.25f, -99f + i * 6f), 90f, "Fence_" + i.ToString("D2"));
                var edges = Group(airport, "RunwayFurniture");
                var cone = ModelPrototype("Pylon", "Kenney", "pylon", 1f, 1, true, red, PrimitiveType.Cylinder);
                var lamp = ModelPrototype("LampPost", "Kenney", "lightPostLarge", 7f, 1, true, dark, PrimitiveType.Cylinder);
                for (int side = -1; side <= 1; side += 2)
                {
                    for (int i = 0; i < 13; i++) Place(cone, edges, new Vector3(side * 23f, -0.25f, -240f + i * 40f), 0f, $"Pylon_{side}_{i:D2}");
                    for (int i = 0; i < 8; i++) Place(lamp, edges, new Vector3(side * 30f, -0.25f, -280f + i * 80f), side < 0 ? 90f : -90f, $"Lamp_{side}_{i:D2}");
                }
                var start = Group(airport, "StartArea");
                var arch = ModelPrototype("StartArch", "Kenney", "overhead", 22f, 0, false, dark);
                // Trois boîtes distinctes, surtout pas un collider plein qui boucherait le passage.
                var archCopy = (GameObject)PrefabUtility.InstantiatePrefab(arch, preview);
                Bounds archBounds = BoundsOf(archCopy);
                float column = 0.7f;
                AddBox(archCopy, new Vector3(-archBounds.extents.x + column, archBounds.size.y / 2f, 0f),
                    new Vector3(column, archBounds.size.y, archBounds.size.z));
                AddBox(archCopy, new Vector3(archBounds.extents.x - column, archBounds.size.y / 2f, 0f),
                    new Vector3(column, archBounds.size.y, archBounds.size.z));
                AddBox(archCopy, new Vector3(0f, archBounds.max.y - 0.4f, 0f), new Vector3(archBounds.size.x, 0.8f, archBounds.size.z));
                arch = SavePrototype("StartArch", archCopy);
                Place(arch, start, new Vector3(0f, SurfaceY, -255f));
                Place(ModelPrototype("FlagRed", "Kenney", "flagRed", 6f, 1, false, red), start, new Vector3(-25f, -0.25f, -265f));
                Place(ModelPrototype("FlagGreen", "Kenney", "flagGreen", 6f, 1, false, grass), start, new Vector3(25f, -0.25f, -265f));
                var forest = Group(airport, "ForestBackdrop");
                var large = ModelPrototype("TreeLarge", "Kenney", "treeLarge", 18f, 1, false, grass, PrimitiveType.Cylinder);
                var small = ModelPrototype("TreeSmall", "Kenney", "treeSmall", 12f, 1, false, grass, PrimitiveType.Cylinder);
                for (int row = 0; row < 3; row++)
                    for (int i = 0; i < 18; i++)
                        Place((row + i) % 2 == 0 ? large : small, forest,
                            new Vector3(-130f - row * 45f, -0.25f, -380f + 45f * i + row * 7f),
                            (i * 47 + row * 31) % 360, $"Tree_{row}_{i:D2}");
            }

            private void Delivery(GameObject airport)
            {
                var zone = Empty("DeliveryZone");
                zone.AddComponent<DeliveryZone>();
                var target = ModelPrototype("DropTarget", "Props", "drop_target", 15f, 0, false, red, PrimitiveType.Cylinder);
                Place(target, zone.transform, Vector3.zero);
                var trigger = zone.AddComponent<MeshCollider>();
                // Seul collider de mesh de l'aéroport : un cylindre convexe de trigger, pas un bâtiment.
                trigger.sharedMesh = DeliveryCylinder();
                trigger.convex = true;
                trigger.isTrigger = true;
                Place(SavePrototype("DeliveryZone", zone), airport.transform, new Vector3(0f, -0.25f, 1500f));
            }

            private GameObject ModelPrototype(string key, string folder, string name, float target, int axis,
                bool box, Material fallback, PrimitiveType primitive = PrimitiveType.Cube)
            {
                if (prototypes.TryGetValue(key, out var cached)) return cached;
                var root = Empty(key);
                var source = catalog.Resolve(folder, name);
                var visual = source != null ? Object.Instantiate(source) : Primitive(primitive, "Fallback", fallback);
                if (visual.scene != preview) SceneManager.MoveGameObjectToScene(visual, preview);
                visual.transform.SetParent(root.transform, false);
                visual.name = "Model";
                foreach (var collider in visual.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
                foreach (var collider in visual.GetComponentsInChildren<Collider2D>(true)) Object.DestroyImmediate(collider);
                foreach (var light in visual.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(light);
                Bounds bounds = BoundsOf(root);
                float extent = bounds.size[axis];
                if (extent < 0.00001f) throw new InvalidOperationException("Bounds de modèle invalides : " + key);
                visual.transform.localScale *= target / extent;
                bounds = BoundsOf(root);
                // Corrige aussi les pivots décalés des FBX Kenney, sans modifier leurs sources.
                visual.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
                bounds = BoundsOf(root);
                if (box) { AddBox(root, bounds.center, bounds.size); root.layer = ground; }
                var prefab = SavePrototype(key, root);
                prototypes[key] = prefab;
                Debug.Log($"[Sky Courier] Prototype {key} : dimensions finales {bounds.size.ToString("F3")} m.");
                return prefab;
            }

            private GameObject SavePrototype(string key, GameObject obj)
            {
                // Si l'objet vient déjà d'un prefab, enregistrer une copie autonome plutôt qu'un variant de lui-même.
                if (PrefabUtility.IsPartOfPrefabInstance(obj))
                    PrefabUtility.UnpackPrefabInstance(obj, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
                obj.transform.SetParent(null, true);
                obj.transform.localPosition = Vector3.zero;
                obj.name = key;
                MarkStatic(obj);
                var prefab = SavePrefab(obj, Prefabs + "/" + key + ".prefab");
                Object.DestroyImmediate(obj);
                return prefab;
            }

            private GameObject Empty(string name)
            {
                var obj = new GameObject(name);
                SceneManager.MoveGameObjectToScene(obj, preview);
                return obj;
            }

            private Transform Group(GameObject parent, string name)
            {
                var group = Empty(name);
                group.transform.SetParent(parent.transform, false);
                return group.transform;
            }

            private GameObject Primitive(PrimitiveType type, string name, Material material)
            {
                var obj = GameObject.CreatePrimitive(type);
                SceneManager.MoveGameObjectToScene(obj, preview);
                obj.name = name;
                foreach (var collider in obj.GetComponents<Collider>()) Object.DestroyImmediate(collider);
                obj.GetComponent<Renderer>().sharedMaterial = material;
                return obj;
            }

            private GameObject Cube(string name, Vector3 position, Vector3 scale, Material material, bool collider)
            {
                var obj = Primitive(PrimitiveType.Cube, name, material);
                obj.transform.localPosition = position;
                obj.transform.localScale = scale;
                if (collider) { obj.AddComponent<BoxCollider>(); obj.layer = ground; }
                return obj;
            }

            private GameObject Place(GameObject prefab, Transform parent, Vector3 position, float yaw = 0f, string name = null)
            {
                var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, preview);
                obj.transform.SetParent(parent, false);
                obj.transform.localPosition = position;
                obj.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                if (name != null) obj.name = name;
                return obj;
            }
        }

        private static Bounds BoundsOf(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true)
                .Where(r => r is MeshRenderer || r is SkinnedMeshRenderer).ToArray();
            if (renderers.Length == 0) throw new InvalidOperationException("Aucun mesh visible : " + root.name);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }

        private static void AddBox(GameObject obj, Vector3 center, Vector3 size)
        {
            var box = obj.AddComponent<BoxCollider>();
            box.center = center;
            box.size = size;
        }

        private static void MarkStatic(GameObject root)
        {
            foreach (var transform in root.GetComponentsInChildren<Transform>(true)) transform.gameObject.isStatic = true;
        }

        private static Material SolidMaterial(string name, Shader shader, string hex, float smoothness)
        {
            string path = Materials + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }
            ColorUtility.TryParseHtmlString(hex, out Color color);
            material.shader = shader;
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Mesh DeliveryCylinder()
        {
            const int sides = 24;
            string path = Prefabs + "/DeliveryTriggerCylinder.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            bool create = mesh == null;
            if (create) mesh = new Mesh { name = "DeliveryTriggerCylinder" };
            mesh.Clear();
            var vertices = new Vector3[sides * 2 + 2];
            var indices = new List<int>();
            for (int i = 0; i < sides; i++)
            {
                float angle = 2f * Mathf.PI * i / sides;
                vertices[i] = new Vector3(10f * Mathf.Cos(angle), 0f, 10f * Mathf.Sin(angle));
                vertices[i + sides] = vertices[i] + Vector3.up * 12f;
            }
            int bottom = sides * 2, top = bottom + 1;
            vertices[bottom] = Vector3.zero;
            vertices[top] = Vector3.up * 12f;
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                indices.AddRange(new[] { i, i + sides, j, j, i + sides, j + sides,
                    bottom, i, j, top, j + sides, i + sides });
            }
            mesh.vertices = vertices;
            mesh.triangles = indices.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            if (create) AssetDatabase.CreateAsset(mesh, path);
            else EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static void EnsureLighting(Scene scene, Transform airport)
        {
            var sun = SceneComponents<Light>(scene).FirstOrDefault(l => l.type == LightType.Directional);
            if (sun == null)
            {
                var obj = new GameObject("Directional Light");
                SceneManager.MoveGameObjectToScene(obj, scene);
                obj.transform.SetParent(airport, false);
                obj.transform.rotation = Quaternion.Euler(60f, -30f, 0f);
                sun = obj.AddComponent<Light>();
                sun.type = LightType.Directional;
                sun.intensity = 1.1f;
            }
            RenderSettings.sun = sun;
            var volume = SceneComponents<Volume>(scene).FirstOrDefault(v => v.isGlobal);
            if (volume == null)
            {
                var obj = new GameObject("Global Volume");
                SceneManager.MoveGameObjectToScene(obj, scene);
                obj.transform.SetParent(airport, false);
                volume = obj.AddComponent<Volume>();
                volume.isGlobal = true;
            }
            string path = Materials + "/AirportVolumeProfile.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "AirportVolumeProfile";
                AssetDatabase.CreateAsset(profile, path);
                // Copie profonde : les effets du profil SampleScene ne sont jamais modifiés.
                if (volume.sharedProfile != null)
                    foreach (var component in volume.sharedProfile.components)
                    {
                        var copy = Object.Instantiate(component);
                        profile.components.Add(copy);
                        AssetDatabase.AddObjectToAsset(copy, profile);
                    }
            }
            var tonemapping = GetEffect<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.ACES);
            var bloom = GetEffect<Bloom>(profile);
            bloom.intensity.Override(0.15f);
            bloom.threshold.Override(1f);
            var vignette = GetEffect<Vignette>(profile);
            vignette.intensity.Override(0.15f);
            vignette.smoothness.Override(0.4f);
            EditorUtility.SetDirty(tonemapping);
            EditorUtility.SetDirty(bloom);
            EditorUtility.SetDirty(vignette);
            EditorUtility.SetDirty(profile);
            volume.sharedProfile = profile;
            volume.weight = 1f;
            var sky = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Materials/Sky_Noon.mat");
            if (sky != null) RenderSettings.skybox = sky;
        }

        private static T GetEffect<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet<T>(out var component))
            {
                component = profile.Add<T>(true);
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            component.active = true;
            return component;
        }

        private static void ConfigureCamera(Scene scene, bool overview)
        {
            var camera = SceneComponents<Camera>(scene).FirstOrDefault(c => c.CompareTag("MainCamera"));
            if (camera == null) return;
            var data = camera.GetComponent<UniversalAdditionalCameraData>();
            if (data == null) data = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = true;
            camera.farClipPlane = Mathf.Max(camera.farClipPlane, 4000f);
            camera.clearFlags = CameraClearFlags.Skybox;
            if (overview)
            {
                camera.transform.position = new Vector3(360f, 240f, -430f);
                camera.transform.LookAt(new Vector3(30f, 0f, 20f));
            }
        }

        private static IEnumerable<T> SceneComponents<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));

        private static void Validate(Scene scene, GameObject airport, int ground)
        {
            if (scene.GetRootGameObjects().Count(g => g.name == "Airport") != 1)
                throw new InvalidOperationException("La scène doit contenir une seule racine Airport.");
            var runway = airport.transform.Find("Pavements/Runway_A");
            if (runway == null || runway.GetComponent<BoxCollider>() == null || runway.gameObject.layer != ground)
                throw new InvalidOperationException("Piste ou layer Ground invalide.");
            var zones = airport.GetComponentsInChildren<DeliveryZone>(true);
            if (zones.Length != 1) throw new InvalidOperationException("Il faut exactement une zone de livraison.");
            var meshes = airport.GetComponentsInChildren<MeshCollider>(true);
            if (meshes.Length != 1 || !meshes[0].convex || !meshes[0].isTrigger || meshes[0].GetComponent<DeliveryZone>() == null)
                throw new InvalidOperationException("Seule la zone de livraison doit avoir un MeshCollider convexe trigger.");
            var hangar = airport.transform.Find("Buildings/Hangar");
            var tower = airport.transform.Find("Buildings/ControlTower");
            if (Mathf.Abs(BoundsOf(hangar.gameObject).size.x - 25f) > 0.01f ||
                Mathf.Abs(BoundsOf(tower.gameObject).size.y - 20f) > 0.01f)
                throw new InvalidOperationException("Dimensions finales des bâtiments incorrectes.");
            Debug.Log($"[Sky Courier] Vérification : 1 Airport, 1 DeliveryZone, 1 trigger cylindrique, " +
                $"{SceneComponents<Light>(scene).Count(l => l.type == LightType.Directional)} Directional Light, " +
                $"{SceneComponents<Volume>(scene).Count(v => v.isGlobal)} Global Volume ; bâtiments aux dimensions demandées.");
        }

        private static int EnsureLayer(string name)
        {
            int existing = LayerMask.NameToLayer(name);
            if (existing >= 0) return existing;
            var manager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset").FirstOrDefault();
            var serialized = new SerializedObject(manager);
            var layers = serialized.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                if (!string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)) continue;
                layers.GetArrayElementAtIndex(i).stringValue = name;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(manager);
                AssetDatabase.SaveAssets();
                return i;
            }
            throw new InvalidOperationException("Aucun emplacement libre pour le layer " + name);
        }

        private static GameObject SavePrefab(GameObject obj, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(obj, path, out bool success);
            if (!success || prefab == null) throw new IOException("Enregistrement du prefab impossible : " + path);
            return prefab;
        }
        private static string Normalize(string path) => path.Replace('\\', '/');
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Normalize(Path.GetDirectoryName(path));
            EnsureFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(path))))
                throw new IOException("Création du dossier impossible : " + path);
        }
    }
}
#endif
