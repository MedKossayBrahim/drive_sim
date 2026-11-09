#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project.Plane
{
    /// <summary>Installe le Tilt direct, le détecteur de chute et les commandes de mission sans toucher aux checkpoints.</summary>
    public static class TiltFlightSetup
    {
        const string ScenePath="Assets/_Plane/Scenes/Plane_Scene.unity";
        const string PrefabPath="Assets/_Plane/Prefabs/Plane.prefab";
        [MenuItem("Sky Courier/17 - Setup Tilt Flight",false,170)]
        public static void Setup()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){Debug.LogWarning("[Sky Courier] Quitter Play avant Setup Tilt Flight.");return;}
            try
            {
                var root=PrefabUtility.LoadPrefabContents(PrefabPath);
                try
                {
                    if(root.GetComponent<PlaneHealth>()==null||root.GetComponent<PlaneSensors>()==null)throw new InvalidOperationException("Santé et capteurs requis.");
                    if(root.GetComponent<FallCrashDetector>()==null)root.AddComponent<FallCrashDetector>();
                    ConfigureInput(root.GetComponent<PlaneInput>());
                    var settings=new SerializedObject(root.GetComponent<PlaneController>()).FindProperty("settings").objectReferenceValue;
                    if(settings!=null)EditorUtility.SetDirty(settings); // Sérialise les nouveaux paramètres avec leurs valeurs par défaut.
                    if(PrefabUtility.SaveAsPrefabAsset(root,PrefabPath)==null)throw new InvalidOperationException("Sauvegarde du prefab impossible.");
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
                var scene=SceneManager.GetSceneByPath(ScenePath);
                if(!scene.IsValid()||!scene.isLoaded)scene=EditorSceneManager.OpenScene(ScenePath,
                    SceneManager.sceneCount==1&&!SceneManager.GetActiveScene().isDirty?OpenSceneMode.Single:OpenSceneMode.Additive);
                ConfigureScene(scene);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Sauvegarde de Plane_Scene impossible.");
                Debug.Log("[Sky Courier] Tilt direct et chute prêts : 4° / 40°, lissage 12, Tilt Android par défaut, Décoller calibre au départ, Facile/Pro et inversion dans Pause. Plané nominal 8:1 train rentré ; Health et Checkpoint*.cs inchangés.");
            }
            catch(Exception exception){Debug.LogException(exception);}
        }
        static void ConfigureInput(PlaneInput input)
        {
            if(input==null)throw new InvalidOperationException("PlaneInput absent.");var s=new SerializedObject(input);
            s.FindProperty("useMobileDefault").boolValue=true;s.FindProperty("maximumTiltDegrees").floatValue=40;
            s.FindProperty("tiltDeadZoneDegrees").floatValue=4;s.FindProperty("tiltAxisSmoothing").floatValue=12;
            s.FindProperty("tiltResponseExponent").floatValue=1.5f;
            s.FindProperty("requireMobileTakeoffConfirmation").boolValue=true;s.ApplyModifiedPropertiesWithoutUndo();
        }
        public static void ConfigureScene(Scene scene)
        {
            var roots=scene.GetRootGameObjects();
            var input=roots.SelectMany(r=>r.GetComponentsInChildren<PlaneInput>(true)).Single();
            var touch=roots.SelectMany(r=>r.GetComponentsInChildren<TouchControlsPanel>(true)).Single();
            var manager=roots.SelectMany(r=>r.GetComponentsInChildren<CheckpointManager>(true)).Single();
            ConfigureInput(input);PrefabUtility.RecordPrefabInstancePropertyModifications(input);
            var launch=touch.GetComponent<PlaneMissionLaunch>()??Undo.AddComponent<PlaneMissionLaunch>(touch.gameObject);
            var hud=roots.SelectMany(r=>r.GetComponentsInChildren<PlaneHUD>(true)).Single();
            var panel=hud.transform.Find("PauseMenu/SafeArea/Panel") as RectTransform;
            var safe=touch.transform.Find("SafeArea");if(panel==null||safe==null)throw new InvalidOperationException("Interface mobile existante requise.");
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Plane/UI/TextMeshPro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            var old=panel.Find("FlightAssistance");if(old!=null)Undo.DestroyObjectImmediate(old.gameObject);
            var row=Rect("FlightAssistance",panel);var layout=row.gameObject.AddComponent<HorizontalLayoutGroup>();layout.spacing=10;
            layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=layout.childForceExpandHeight=true;
            var element=row.gameObject.AddComponent<LayoutElement>();element.minHeight=element.preferredHeight=96;
            row.SetSiblingIndex(Mathf.Min(5,panel.childCount-1));panel.sizeDelta=new Vector2(panel.sizeDelta.x,Mathf.Max(900,panel.sizeDelta.y));
            var invert=Button(row,"TANGAGE\nNORMAL",launch.TogglePitchInversion,font);
            var assistance=Button(row,"ASSISTANCE\nFACILE",launch.ToggleAssistance,font);
            var oldPrompt=safe.Find("TakeoffPrompt");if(oldPrompt!=null)Undo.DestroyObjectImmediate(oldPrompt.gameObject);
            var prompt=Rect("TakeoffPrompt",safe);prompt.sizeDelta=new Vector2(620,220);
            var image=prompt.gameObject.AddComponent<Image>();image.color=new Color(.01f,.04f,.07f,.95f);
            var vertical=prompt.gameObject.AddComponent<VerticalLayoutGroup>();vertical.padding=new RectOffset(20,20,16,16);vertical.spacing=14;
            vertical.childControlWidth=vertical.childControlHeight=true;vertical.childForceExpandWidth=true;vertical.childForceExpandHeight=false;
            var label=Label(prompt,"Tenez le téléphone dans la position neutre de pilotage",font);label.GetComponent<LayoutElement>().preferredHeight=64;
            Button(prompt,"DECOLLER",launch.TakeOff,font);prompt.gameObject.SetActive(false);
            var serialized=new SerializedObject(launch);
            serialized.FindProperty("input").objectReferenceValue=input;serialized.FindProperty("checkpoints").objectReferenceValue=manager;
            serialized.FindProperty("takeoffPrompt").objectReferenceValue=prompt.gameObject;
            serialized.FindProperty("invertLabel").objectReferenceValue=invert;serialized.FindProperty("assistanceLabel").objectReferenceValue=assistance;
            serialized.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(launch);EditorSceneManager.MarkSceneDirty(scene);
        }
        static RectTransform Rect(string name,Transform parent)
        {var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);return rect;}
        static TMP_Text Label(Transform parent,string text,TMP_FontAsset font)
        {
            var rect=Rect("Label",parent);var label=rect.gameObject.AddComponent<TextMeshProUGUI>();label.font=font;label.text=text;label.fontSize=25;
            label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;label.enableAutoSizing=true;label.fontSizeMin=18;label.fontSizeMax=25;
            rect.gameObject.AddComponent<LayoutElement>();return label;
        }
        static TMP_Text Button(Transform parent,string text,UnityAction callback,TMP_FontAsset font)
        {
            var rect=Rect(text.Replace('\n','_'),parent);var image=rect.gameObject.AddComponent<Image>();image.color=new Color(.04f,.19f,.24f,1);
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;UnityEventTools.AddPersistentListener(button.onClick,callback);
            var size=rect.gameObject.AddComponent<LayoutElement>();size.minHeight=size.preferredHeight=96;size.minWidth=96;size.flexibleWidth=1;
            var label=Label(rect,text,font);label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;
            label.rectTransform.offsetMin=label.rectTransform.offsetMax=Vector2.zero;return label;
        }
    }
}
#endif
