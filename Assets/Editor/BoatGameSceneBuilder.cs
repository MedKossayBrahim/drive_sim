using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;


[InitializeOnLoad]
static class BoatGameSceneBuilder
{
    const string ScenePath = "Assets/Scenes/BoatRunner.unity";
    static BoatGameSceneBuilder() => EditorApplication.delayCall += BuildIfNeeded;

    static void BuildIfNeeded()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (!System.IO.File.Exists(ScenePath)) BuildScene();
    }

    [MenuItem("Tools/Boat Game/Rebuild Starter Scene")]
    static void BuildScene()
    {
        try { BuildSceneInternal(); } catch (System.Exception exception) { Debug.LogException(exception); }
    }

    static void BuildSceneInternal()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var water = GameObject.CreatePrimitive(PrimitiveType.Plane);
        water.name = "Open Water";
        water.transform.position = new Vector3(0f, -0.12f, 180f);
        water.transform.localScale = new Vector3(12f, 1f, 48f);
        water.GetComponent<Collider>().enabled = false;
        water.GetComponent<Renderer>().sharedMaterial = MakeMaterial("Water Blue", new Color(0.055f, 0.34f, 0.58f));

        var boat = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        boat.name = "Boat";
        boat.tag = "Player";
        boat.transform.position = new Vector3(0f, 0.52f, 0f);
        boat.transform.localScale = new Vector3(0.95f, 0.28f, 1.9f);
        boat.GetComponent<Renderer>().sharedMaterial = MakeMaterial("Boat Ivory", new Color(0.96f, 0.88f, 0.67f));
        var rb = boat.AddComponent<Rigidbody>(); rb.useGravity = false; rb.interpolation = RigidbodyInterpolation.Interpolate; rb.constraints = RigidbodyConstraints.FreezeRotation;
        boat.GetComponent<Collider>().isTrigger = true;
        var controller = boat.AddComponent<BoatController>();

        var cameraGo = new GameObject("Main Camera");
        cameraGo.tag = "MainCamera";
        var cam = cameraGo.AddComponent<Camera>();
        cam.orthographic = true; cam.orthographicSize = 10.7f; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.055f, 0.34f, 0.58f);
        cameraGo.transform.position = new Vector3(0f, 18f, -1f); cameraGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        cameraGo.AddComponent<AudioListener>();
        var follow = cameraGo.AddComponent<FollowBoatCamera>(); follow.SetTarget(boat.transform);

        var spawnerGo = new GameObject("Obstacle Spawner"); var spawner = spawnerGo.AddComponent<ObstacleSpawner>();
        var managerGo = new GameObject("Game Manager"); var manager = managerGo.AddComponent<BoatGameManager>();

        var canvasGo = new GameObject("Game UI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10;
        var scaler = canvasGo.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1080, 1920); scaler.matchWidthOrHeight = 0.5f;
        var eventGo = new GameObject("EventSystem", typeof(EventSystem));
        var inputUiType = System.Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputUiType != null)
        {
            var inputUi = eventGo.AddComponent(inputUiType);
            inputUiType.GetMethods().FirstOrDefault(m => m.Name == "AssignDefaultActions" && m.GetParameters().Length == 0)?.Invoke(inputUi, null);
        }
        else eventGo.AddComponent<StandaloneInputModule>();

        var score = MakeText(canvasGo.transform, "Distance", "DISTANCE  0000", 46, TextAnchor.UpperLeft, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(54f, -54f), new Vector2(520f, 82f));
        var lives = MakeText(canvasGo.transform, "Lives", "LIVES  3", 40, TextAnchor.UpperRight, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-54f, -58f), new Vector2(300f, 72f));

        var panel = new GameObject("Game Over Panel", typeof(RectTransform), typeof(Image)); panel.transform.SetParent(canvasGo.transform, false);
        var panelRect = panel.GetComponent<RectTransform>(); panelRect.anchorMin = Vector2.zero; panelRect.anchorMax = Vector2.one; panelRect.offsetMin = Vector2.zero; panelRect.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0.015f, 0.07f, 0.12f, 0.88f);
        MakeText(panel.transform, "Game Over", "VOYAGE OVER", 74, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.65f), new Vector2(0.5f, 0.65f), Vector2.zero, new Vector2(900f, 130f));
        var finalScore = MakeText(panel.transform, "Final Distance", "DISTANCE  0000", 44, TextAnchor.MiddleCenter, new Vector2(0.5f, 0.54f), new Vector2(0.5f, 0.54f), Vector2.zero, new Vector2(800f, 90f));
        var restart = MakeButton(panel.transform, "Restart", "SAIL AGAIN", new Vector2(0.5f, 0.4f), new Vector2(0.5f, 0.4f), Vector2.zero, new Vector2(360f, 112f));
        restart.onClick.AddListener(manager.Restart);
        manager.Initialize(controller, score, lives, panel, finalScore, spawner);

        EditorSceneManager.SaveScene(scene, ScenePath);
        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        var entry = list.FindIndex(x => x.path == ScenePath);
        var buildScene = new EditorBuildSettingsScene(ScenePath, true);
        if (entry >= 0) list[entry] = buildScene; else list.Add(buildScene);
        EditorBuildSettings.scenes = list.ToArray();
        Debug.Log("Boat Runner scene created at " + ScenePath);
    }

    static Material MakeMaterial(string name, Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) throw new System.InvalidOperationException("No supported shader found for starter materials.");
        var mat = new Material(shader);
        mat.name = name; mat.color = color;
        return mat;
    }
    static Text MakeText(Transform parent, string name, string value, int size, TextAnchor align, Vector2 amin, Vector2 amax, Vector2 pos, Vector2 dimensions)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>(); rect.anchorMin = amin; rect.anchorMax = amax; rect.pivot = new Vector2(amin.x, amin.y); rect.anchoredPosition = pos; rect.sizeDelta = dimensions;
        var text = go.GetComponent<Text>(); text.text = value; text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = size; text.alignment = align; text.color = Color.white; text.horizontalOverflow = HorizontalWrapMode.Overflow; text.verticalOverflow = VerticalWrapMode.Overflow; text.raycastTarget = false;
        return text;
    }
    static Button MakeButton(Transform parent, string name, string label, Vector2 amin, Vector2 amax, Vector2 pos, Vector2 dimensions)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button)); go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>(); rect.anchorMin = amin; rect.anchorMax = amax; rect.pivot = new Vector2(amin.x, amin.y); rect.anchoredPosition = pos; rect.sizeDelta = dimensions;
        var image = go.GetComponent<Image>(); image.color = new Color(0.05f, 0.18f, 0.25f, 0.96f);
        var text = MakeText(go.transform, "Label", label, 30, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        var textRect = text.rectTransform; textRect.offsetMin = Vector2.zero; textRect.offsetMax = Vector2.zero; text.raycastTarget = false;
        return go.GetComponent<Button>();
    }
}











