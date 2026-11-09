#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Project.Plane
{
    /// <summary>Configure uniquement les entrées du prefab Plane et le panneau tactile de Plane_Scene.</summary>
    public static class PlaneInputSetup
    {
        private const string PrefabPath = "Assets/_Plane/Prefabs/Plane.prefab";
        private const string ScenePath = "Assets/_Plane/Scenes/Plane_Scene.unity";
        private const string ActionsPath = "Assets/_Plane/PlaneInputActions.inputactions";

        [MenuItem("Sky Courier/6 - Add Plane Input To Prefab", false, 60)]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { Debug.LogWarning("[Sky Courier] Quitter Play avant de configurer les entrées."); return; }
            try
            {
                var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
                if (actions == null) throw new InvalidOperationException("PlaneInputActions absent ou non importé.");
                actions.FindActionMap("Plane", true);
                Scene scene = SceneManager.GetSceneByPath(ScenePath);
                if (!scene.IsValid() || !scene.isLoaded)
                    scene = EditorSceneManager.OpenScene(ScenePath,
                        SceneManager.sceneCount == 1 && !SceneManager.GetActiveScene().isDirty ? OpenSceneMode.Single : OpenSceneMode.Additive);
                var existingRoots = scene.GetRootGameObjects().Where(g => g.name == "TouchControls").ToArray();
                if (existingRoots.Any(g => g.GetComponent<TouchControlsPanel>() == null))
                    throw new InvalidOperationException("TouchControls existe sans TouchControlsPanel : le renommer pour préserver cette UI.");
                var planeAsset = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
                if (planeAsset == null) throw new InvalidOperationException("Plane.prefab absent.");
                GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
                try
                {
                    if (root.GetComponent<PlaneController>() == null) throw new InvalidOperationException("PlaneController absent de la racine Plane.");
                    var input = root.GetComponent<PlaneInput>();
                    if (input == null) input = root.AddComponent<PlaneInput>();
                    var serialized = new SerializedObject(input);
                    serialized.FindProperty("inputActions").objectReferenceValue = actions;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null) throw new InvalidOperationException("Sauvegarde du prefab impossible.");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
                var planes = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<PlaneInput>(true))
                    .Where(p => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(p.gameObject) == PrefabPath).ToArray();
                if (planes.Length != 1) throw new InvalidOperationException("Une seule instance de Plane.prefab est requise dans Plane_Scene.");
                var target = planes[0];
                foreach (var existing in existingRoots) Undo.DestroyObjectImmediate(existing);

                var canvasObject = new GameObject("TouchControls", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                SceneManager.MoveGameObjectToScene(canvasObject, scene);
                Undo.RegisterCreatedObjectUndo(canvasObject, "Configurer les contrôles avion");
                var canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 100;
                var scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = 0.5f;
                var safe = Rect("SafeArea", canvasObject.transform);
                Stretch(safe);
                var content = Rect("Content", safe); Stretch(content);
                var panel = canvasObject.AddComponent<TouchControlsPanel>();
                var area = Rect("JoystickArea", content);
                Anchor(area, Vector2.zero, new Vector2(280f, 250f), new Vector2(520f, 420f));
                Image(area, new Color(0f, 0f, 0f, 0.01f), true);
                var disc = Rect("Base", area); disc.sizeDelta = new Vector2(240f, 240f);
                Image(disc, new Color(0.08f, 0.13f, 0.2f, 0.65f));
                var knob = Rect("Handle", disc); knob.sizeDelta = new Vector2(70f, 70f);
                Image(knob, new Color(0.95f, 0.8f, 0.25f, 0.95f));
                var joystick = area.gameObject.AddComponent<VirtualJoystick>(); joystick.Configure(disc, knob);
                var sliderRect = Rect("ThrottleSlider", content);
                Anchor(sliderRect, new Vector2(1f, 0f), new Vector2(-170f, 250f), new Vector2(100f, 350f));
                Image(sliderRect, new Color(0.08f, 0.13f, 0.2f, 0.8f), true);
                var handle = Rect("Handle", sliderRect); handle.sizeDelta = new Vector2(120f, 45f);
                Image(handle, new Color(0.95f, 0.8f, 0.25f, 0.95f));
                var slider = sliderRect.gameObject.AddComponent<ThrottleSlider>(); slider.Configure(handle);
                var gasLabel = Rect("Label", sliderRect);
                Anchor(gasLabel, new Vector2(0.5f, 1f), new Vector2(0f, 40f), new Vector2(180f, 50f));
                Label(gasLabel, "GAZ", 24);

                var top = Rect("TopButtons", safe);
                top.anchorMin = new Vector2(0.03f, 1f); top.anchorMax = new Vector2(0.97f, 1f);
                top.pivot = new Vector2(0.5f, 1f); top.anchoredPosition = new Vector2(0f, -20f); top.sizeDelta = new Vector2(0f, 64f);
                var layout = top.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 10f; layout.childAlignment = TextAnchor.UpperCenter;
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
                Button(top, "Calibrer", panel.Calibrate);
                Button(top, "Volets", panel.ToggleFlaps);
                Button(top, "Train", panel.ToggleGear);
                Button(top, "Reset", panel.ResetPlane);
                Button(top, "Clavier", panel.KeyboardMode);
                Button(top, "Tactile", panel.TouchMode);
                Button(top, "Inclinaison", panel.TiltMode);
                Button(top, "Caméra", panel.SwitchCamera);
                Button(top, "Pause", panel.TogglePause);
                panel.Configure(target, content.gameObject, area.gameObject, safe, top.gameObject);
                var overlay = canvasObject.AddComponent<PlaneInputDebugOverlay>(); overlay.Configure(target);
                target.Configure(actions, joystick, slider);
                EditorUtility.SetDirty(target);
                PrefabUtility.RecordPrefabInstancePropertyModifications(target);

                var systems = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<EventSystem>(true)).ToArray();
                if (systems.Length == 0)
                {
                    var system = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                    SceneManager.MoveGameObjectToScene(system, scene);
                    Undo.RegisterCreatedObjectUndo(system, "Créer EventSystem tactile");
                    ConfigureUIModule(system.GetComponent<InputSystemUIInputModule>());
                }
                else
                {
                    if (systems.Length > 1) Debug.LogWarning("[Sky Courier] Plusieurs EventSystem préexistants : vérifier leur activation.");
                    var system = systems[0];
                    if (system.GetComponent<InputSystemUIInputModule>() == null)
                    {
                        var module = Undo.AddComponent<InputSystemUIInputModule>(system.gameObject);
                        ConfigureUIModule(module);
                        foreach (var legacy in system.GetComponents<StandaloneInputModule>())
                        {
                            Undo.RecordObject(legacy, "Utiliser Input System UI"); legacy.enabled = false;
                            Debug.Log("[Sky Courier] Ancien StandaloneInputModule désactivé au profit d'InputSystemUIInputModule.");
                        }
                    }
                }
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Sauvegarde de Plane_Scene impossible.");
                AssetDatabase.SaveAssets(); Selection.activeGameObject = target.gameObject;
                Debug.Log("[Sky Courier] Entrées configurées : PlaneInput ajouté au prefab, asset assigné, TouchControls recréé sans doublons, " +
                    "joystick/slider liés à l'instance et UI Input System prête. Les autres composants du prefab sont conservés. " +
                    "Sur PC : choisir Touch/Tilt dans PlaneInput pour afficher le panneau ; sur mobile : Touch par défaut.", target);
            }
            catch (Exception exception) { Debug.LogException(exception); }
        }
        private static RectTransform Rect(string name, Transform parent)
        {
            var value = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            value.SetParent(parent, false); return value;
        }
        private static void ConfigureUIModule(InputSystemUIInputModule module)
        {
            // Références persistantes du package officiel, plutôt que des actions temporaires créées en mémoire.
            const string path = "Packages/com.unity.inputsystem/InputSystem/Runtime/Plugins/PlayerInput/DefaultInputActions.inputactions";
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
            if (asset == null) throw new InvalidOperationException("Actions UI officielles absentes du package Input System.");
            var references = AssetDatabase.LoadAllAssetsAtPath(path).OfType<InputActionReference>().ToArray();
            InputActionReference Reference(string name)
            {
                var action = asset.FindAction("UI/" + name, true);
                return references.First(r => r.action != null && r.action.id == action.id);
            }
            module.actionsAsset = asset;
            module.point = Reference("Point"); module.leftClick = Reference("Click");
            module.rightClick = Reference("RightClick"); module.middleClick = Reference("MiddleClick");
            module.scrollWheel = Reference("ScrollWheel"); module.move = Reference("Navigate");
            module.submit = Reference("Submit"); module.cancel = Reference("Cancel");
        }
        private static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static void Anchor(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        { rect.anchorMin = rect.anchorMax = anchor; rect.pivot = new Vector2(0.5f, 0.5f); rect.anchoredPosition = position; rect.sizeDelta = size; }
        private static Image Image(RectTransform rect, Color color, bool raycast = false)
        { var image = rect.gameObject.AddComponent<Image>(); image.color = color; image.raycastTarget = raycast; return image; }
        private static void Label(RectTransform rect, string value, int fontSize)
        {
            var text = rect.gameObject.AddComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value; text.fontSize = fontSize; text.alignment = TextAnchor.MiddleCenter; text.color = Color.white; text.raycastTarget = false;
        }
        private static void Button(RectTransform parent, string caption, UnityEngine.Events.UnityAction action)
        {
            var rect = Rect(caption, parent);
            Image(rect, new Color(0.08f, 0.13f, 0.2f, 0.9f), true);
            var layout = rect.gameObject.AddComponent<LayoutElement>(); layout.minWidth = 110f; layout.preferredHeight = 64f; layout.flexibleWidth = 1f;
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>();
            UnityEventTools.AddPersistentListener(button.onClick, action);
            var label = Rect("Label", rect); Stretch(label); Label(label, caption, 23);
        }
    }
}
#endif
