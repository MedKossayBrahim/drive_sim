#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project.Plane
{
    /// <summary>Reconstruit uniquement HUD, en conservant TouchControls et les objets du vol.</summary>
    public static class PlaneHUDSetup
    {
        private const string ScenePath = "Assets/_Plane/Scenes/Plane_Scene.unity";
        private const string FontPath = "Assets/_Plane/UI/TextMeshPro/Resources/Fonts & Materials/LiberationSans SDF.asset";
        private static readonly Color Cyan = new Color(46f/255f, 196f/255f, 182f/255f);
        private static readonly Color Dark = new Color(0.015f, 0.04f, 0.07f, 0.62f);
        private static TMP_FontAsset font;

        [MenuItem("Sky Courier/9 - Build HUD", false, 90)]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogWarning("[Sky Courier] Quitter Play avant Build HUD."); return; }
            try
            {
                font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
                if (font == null) throw new InvalidOperationException("Police TMP officielle absente : " + FontPath);
                Scene scene = SceneManager.GetSceneByPath(ScenePath);
                if (!scene.IsValid() || !scene.isLoaded)
                    scene = EditorSceneManager.OpenScene(ScenePath, SceneManager.sceneCount == 1 && !SceneManager.GetActiveScene().isDirty ? OpenSceneMode.Single : OpenSceneMode.Additive);
                var sensors = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<PlaneSensors>(true)).ToArray();
                var managers = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<CheckpointManager>(true)).ToArray();
                if (sensors.Length != 1 || managers.Length != 1) throw new InvalidOperationException("Un PlaneSensors et un CheckpointManager sont requis dans Plane_Scene.");
                var input = sensors[0].GetComponent<PlaneInput>();
                if (input == null) throw new InvalidOperationException("PlaneInput absent de l'avion.");
                var roots = scene.GetRootGameObjects().Where(g => g.name == "HUD").ToArray();
                if (roots.Any(g => g.GetComponent<PlaneHUD>() == null)) throw new InvalidOperationException("HUD existe sans PlaneHUD : le renommer pour préserver cette UI.");
                foreach (var old in roots) Undo.DestroyObjectImmediate(old);
                var root = Rect("HUD", null);
                SceneManager.MoveGameObjectToScene(root.gameObject, scene);
                Undo.RegisterCreatedObjectUndo(root.gameObject, "Construire HUD avion");
                var canvas = root.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 50;
                var scaler = root.gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f); scaler.matchWidthOrHeight = 0.5f;
                root.gameObject.AddComponent<GraphicRaycaster>();
                var hud = root.gameObject.AddComponent<PlaneHUD>(); var menu = root.gameObject.AddComponent<PlaneMenu>();
                var safe = Rect("SafeArea", root); Stretch(safe); safe.gameObject.AddComponent<PlaneHUDSafeArea>();
                // Cartes périphériques : la piste et l'avion restent dégagés au centre.
                var flight = Rect("FlightInstruments", safe); Background(flight, Dark); Layout(flight, true, 4);
                var speed = Label(flight, "Speed", "VITESSE", 28);
                var altitude = Label(flight, "Altitude", "ALTITUDE", 23);
                var mission = Rect("Mission", safe); Background(mission, Dark); Layout(mission, true, 4);
                var course = Label(mission, "Course", "CHRONO 00:00", 24);
                var pauseButton = Button(mission, "Pause", menu.TogglePause);
                pauseButton.GetComponent<LayoutElement>().minHeight = 48;
                pauseButton.GetComponent<LayoutElement>().preferredHeight = 48;
                var dock = Rect("InstrumentDock", safe); Layout(dock, false, 14);
                var engine = Rect("Engine", dock); Flex(engine, 1f); Background(engine, Dark); Layout(engine, true, 3);
                var gas = Label(engine, "Throttle", "GAZ 0%", 21); var gasFill = Bar(engine, "ThrottleBar");
                var fuel = Label(engine, "Fuel", "CARBURANT -- L", 21); var fuelFill = Bar(engine, "FuelBar");
                var g = Label(engine, "GForce", "G --", 21);

                var compass = Rect("Compass", safe); Place(compass, new Vector2(0.5f, 0.70f), Vector2.zero, new Vector2(400, 72)); Background(compass, Dark);
                var mask = Rect("Viewport", compass); Stretch(mask); mask.gameObject.AddComponent<RectMask2D>();
                var tape = Rect("Tape", mask); Place(tape, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4320, 40));
                // Trois tours : les repères restent continus au franchissement 359 -> 0 degrés.
                for (int degree = -360; degree <= 720; degree += 15)
                {
                    int bearing = (degree % 360 + 360) % 360;
                    string name = bearing == 0 ? "N" : bearing == 90 ? "E" : bearing == 180 ? "S" : bearing == 270 ? "O" : bearing.ToString("000");
                    var tick = Label(tape, "Tick_" + degree, name, 23);
                    Place(tick.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(degree * 3, -12), new Vector2(48, 35));
                }
                var heading = Label(compass, "Heading", "CAP 000", 24); Place(heading.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -15), new Vector2(180, 30));
                var index = Rect("Index", compass); Place(index, new Vector2(0.5f, 0.5f), new Vector2(0,-30), new Vector2(3, 18)); Background(index, Cyan);

                var horizonFrame = Rect("ArtificialHorizon", dock); Flex(horizonFrame, 0.8f); horizonFrame.sizeDelta = new Vector2(190, 140);
                Background(horizonFrame, Dark); horizonFrame.gameObject.AddComponent<RectMask2D>();
                var horizon = Rect("AttitudeImage", horizonFrame); horizon.sizeDelta = new Vector2(1000, 1000);
                var sky = Rect("Sky", horizon); sky.anchorMin = new Vector2(0,0.5f); sky.anchorMax = Vector2.one; sky.offsetMin = sky.offsetMax = Vector2.zero; Background(sky, new Color(0.05f,0.3f,0.5f,0.8f));
                var earth = Rect("Ground", horizon); earth.anchorMin = Vector2.zero; earth.anchorMax = new Vector2(1,0.5f); earth.offsetMin = earth.offsetMax = Vector2.zero; Background(earth, new Color(0.24f,0.17f,0.10f,0.8f));
                var line = Rect("HorizonLine", horizon); line.sizeDelta = new Vector2(1000,3); Background(line, Color.white);
                for (int degrees = -60; degrees <= 60; degrees += 10)
                {
                    if (degrees == 0) continue;
                    var mark = Label(horizon, "Pitch_" + degrees, "- " + degrees + " -", 16);
                    Place(mark.rectTransform, new Vector2(0.5f,0.5f), new Vector2(0,degrees*3), new Vector2(120,28));
                }
                var fixedMark = Label(horizonFrame, "AircraftReference", "-- + --", 24); Stretch(fixedMark.rectTransform); fixedMark.color = Cyan;
                var warnings = Rect("Warnings", safe); Place(warnings, new Vector2(0.5f,0.34f), Vector2.zero, new Vector2(400,36)); Background(warnings,Dark); Layout(warnings,false,8);
                var stall = Label(warnings,"Stall","DÉCROCHAGE",20); var low = Label(warnings,"LowFuel","CARBURANT",20); var terrain = Label(warnings,"Terrain","TERRAIN",20);
                var nav = Rect("Navigation", safe); Place(nav, new Vector2(0.77f,0.49f), Vector2.zero, new Vector2(310,160)); Background(nav,Dark); Layout(nav,true,5);
                var arrow = Label(nav,"Arrow","^",32); var navigation = Label(nav,"NextRing","PROCHAIN ANNEAU",23);
                var message = Label(safe,"ContextMessage","",27); Place(message.rectTransform,new Vector2(0.5f,0.27f),Vector2.zero,new Vector2(900,55)); message.color = Cyan;

                var adaptive = root.gameObject.AddComponent<PlaneHUDLayout>();
                Wire(adaptive,"safeArea",safe,"input",input,"flight",flight,"mission",mission,"compass",compass,
                    "dock",dock,"navigation",nav,"warnings",warnings,"message",message.rectTransform);

                var pause = Rect("PauseMenu", root); Stretch(pause);
                // Canvas au-dessus de TouchControls : le voile intercepte aussi ses boutons quand le jeu est en pause.
                var pauseCanvas = pause.gameObject.AddComponent<Canvas>(); pauseCanvas.overrideSorting = true; pauseCanvas.sortingOrder = 300;
                pause.gameObject.AddComponent<GraphicRaycaster>(); Background(pause, new Color(0,0,0,0.88f),true);
                var pauseSafe = Rect("SafeArea",pause); Stretch(pauseSafe); pauseSafe.gameObject.AddComponent<PlaneHUDSafeArea>();
                var panel = Rect("Panel",pauseSafe); Place(panel,new Vector2(0.5f,0.5f),Vector2.zero,new Vector2(660,840)); Background(panel,Dark); Layout(panel,true,12);
                Wire(hud,"sensors",sensors[0],"input",input,"checkpoints",managers[0],"speed",speed,"altitude",altitude,"heading",heading,"gas",gas,"fuel",fuel,"gForce",g,"navigation",navigation,"course",course,"message",message,"stallLamp",stall,"fuelLamp",low,"terrainLamp",terrain,"gasFill",gasFill,"fuelFill",fuelFill,"horizon",horizon,"compassTape",tape,"navigationArrow",arrow.rectTransform);
                Wire(menu,"input",input,"hud",hud,"pausePanel",pause.gameObject,"hudContent",safe.gameObject);
                pause.gameObject.SetActive(false);
                if (!scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<EventSystem>(true)).Any())
                {
                    var events = new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule)); SceneManager.MoveGameObjectToScene(events,scene);
                    Undo.RegisterCreatedObjectUndo(events,"Créer EventSystem HUD"); events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
                }
                MobileUISetup.Configure(scene);
                Canvas.ForceUpdateCanvases(); adaptive.RefreshLayout(); Canvas.ForceUpdateCanvases(); EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Impossible de sauvegarder Plane_Scene.");
                Selection.activeGameObject=root.gameObject;
                Debug.Log("[Sky Courier] HUD prêt : TMP, SafeArea, instruments, horizon, navigation et menu pause. TouchControls et scripts existants conservés. Relancer remplace seulement HUD.",hud);
            }
            catch(Exception exception) { Debug.LogException(exception); }
        }
        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent,false);
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0.5f,0.5f); return rect;
        }
        private static void Stretch(RectTransform rect) { rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero; }
        private static void Place(RectTransform rect,Vector2 anchor,Vector2 position,Vector2 size)
        { rect.anchorMin=rect.anchorMax=anchor;rect.anchoredPosition=position;rect.sizeDelta=size; }
        private static Image Background(RectTransform rect,Color color,bool raycast=false)
        { var image=rect.gameObject.AddComponent<Image>();image.color=color;image.raycastTarget=raycast;return image; }
        private static TMP_Text Label(Transform parent,string name,string text,int size)
        {
            var rect=Rect(name,parent);var label=rect.gameObject.AddComponent<TextMeshProUGUI>();label.font=font;label.text=text;label.fontSize=size;
            label.color=Color.white;label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;label.textWrappingMode=TextWrappingModes.Normal;label.enableAutoSizing=true;label.fontSizeMax=size;label.fontSizeMin=Mathf.Max(16,size-5);
            var layout=rect.gameObject.AddComponent<LayoutElement>();layout.minHeight=30;layout.preferredHeight=48;layout.flexibleWidth=1;
            return label;
        }
        private static void Flex(RectTransform rect,float weight)
        { var element=rect.GetComponent<LayoutElement>()??rect.gameObject.AddComponent<LayoutElement>();element.minWidth=60;element.flexibleWidth=weight; }
        private static void Layout(RectTransform rect,bool vertical,float spacing)
        {
            HorizontalOrVerticalLayoutGroup group=vertical?(HorizontalOrVerticalLayoutGroup)rect.gameObject.AddComponent<VerticalLayoutGroup>():rect.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.padding=new RectOffset(12,12,8,8);group.spacing=spacing;group.childAlignment=TextAnchor.MiddleCenter;
            group.childControlWidth=group.childControlHeight=true;group.childForceExpandWidth=true;group.childForceExpandHeight=true;
        }
        private static Image Bar(Transform parent,string name)
        {
            var rect=Rect(name,parent); Background(rect,new Color(1,1,1,0.1f));
            var layout=rect.gameObject.AddComponent<LayoutElement>();layout.minHeight=8;layout.preferredHeight=10;
            var child=Rect("Fill",rect);Stretch(child);var image=Background(child,Cyan);image.type=Image.Type.Filled;image.fillMethod=Image.FillMethod.Horizontal;image.fillOrigin=0;return image;
        }
        private static Button Button(Transform parent,string text,UnityAction action)
        {
            var rect=Rect(text,parent);var image=Background(rect,new Color(0.07f,0.25f,0.3f,1),true);
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;UnityEventTools.AddPersistentListener(button.onClick,action);
            var layout=rect.gameObject.AddComponent<LayoutElement>();layout.minHeight=64;layout.preferredHeight=72;layout.flexibleWidth=1;
            var label=Label(rect,"Label",text,27);Stretch(label.rectTransform);return button;
        }
        private static void Wire(UnityEngine.Object target,params object[] pairs)
        {
            var serialized=new SerializedObject(target);
            for(int i=0;i<pairs.Length;i+=2) serialized.FindProperty((string)pairs[i]).objectReferenceValue=(UnityEngine.Object)pairs[i+1];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
