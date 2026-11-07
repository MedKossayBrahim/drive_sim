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
    /// <summary>Construction unique de la pause mobile, partagée par les menus 9 et 16.</summary>
    public static class MobileUISetup
    {
        const string ScenePath="Assets/_Plane/Scenes/Plane_Scene.unity";
        static TMP_FontAsset font;
        static Sprite rounded;
        static readonly Color Dark=new Color(.035f,.065f,.09f,1f);
        static readonly Color Cyan=new Color(46f/255f,196f/255f,182f/255f);
        [MenuItem("Sky Courier/16 - Setup Mobile UI",false,160)]
        public static void Setup()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){Debug.LogWarning("[Sky Courier] Quitter Play avant Setup Mobile UI.");return;}
            try
            {
                var scene=SceneManager.GetSceneByPath(ScenePath);
                if(!scene.IsValid()||!scene.isLoaded)scene=EditorSceneManager.OpenScene(ScenePath,
                    SceneManager.sceneCount==1&&!SceneManager.GetActiveScene().isDirty?OpenSceneMode.Single:OpenSceneMode.Additive);
                Configure(scene);AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Sauvegarde de Plane_Scene impossible.");
                Debug.Log("[Sky Courier] Réglages mobiles reconstruits : contrôles, assistance, inversion et fluidité persistants ; train et volets automatiques. Menus 9 et 16 partagent cette construction.");
            }
            catch(Exception exception){Debug.LogException(exception);}
        }
        public static void Configure(Scene scene)
        {
            var roots=scene.GetRootGameObjects();
            var touch=Unique<TouchControlsPanel>(roots);var hud=Unique<PlaneHUD>(roots);var menu=Unique<PlaneMenu>(roots);
            var input=Unique<PlaneInput>(roots);var sky=Unique<SkyboxSwitcher>(roots);var rig=Unique<PlaneCameraRig>(roots);
            font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Plane/UI/TextMeshPro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            if(font==null)throw new InvalidOperationException("Police TMP absente.");
            rounded=IconAsset("MobileRounded",0);
            var safe=touch.transform.Find("SafeArea") as RectTransform;
            var pause=menu.transform.Find("PauseMenu") as RectTransform;
            var pauseSafe=pause.Find("SafeArea") as RectTransform;
            var panel=pauseSafe.Find("Panel") as RectTransform;
            if(safe==null||panel==null)throw new InvalidOperationException("Construire HUD et TouchControls avant la configuration mobile.");
            foreach(var root in new[]{touch.gameObject,hud.gameObject})
            {
                var scaler=root.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;EditorUtility.SetDirty(scaler);
            }
            var shader=AssetDatabase.LoadAssetAtPath<Shader>("Assets/_Plane/VFX/PanoramicBlend.shader");
            var serialized=new SerializedObject(sky);serialized.FindProperty("transitionShader").objectReferenceValue=shader;
            serialized.FindProperty("transitionDuration").floatValue=1f;serialized.ApplyModifiedProperties();
            rig.GetComponent<Camera>().clearFlags=CameraClearFlags.Skybox;
            var mobile=touch.GetComponent<PlaneMobileSettings>()??Undo.AddComponent<PlaneMobileSettings>(touch.gameObject);
            Wire(mobile,"input",input,"fpsLabel",null);
            serialized=new SerializedObject(mobile);serialized.FindProperty("editorOnlyControls").arraySize=0;serialized.ApplyModifiedProperties();

            // Les trois boutons ont exactement la même largeur et la même hauteur.
            DestroyChild(safe,"TopButtons");
            var top=Rect("TopButtons",safe);top.anchorMin=top.anchorMax=top.pivot=Vector2.one;
            top.anchoredPosition=new Vector2(-22,-16);top.sizeDelta=new Vector2(546,112);
            var topLayout=top.gameObject.AddComponent<HorizontalLayoutGroup>();topLayout.spacing=12;
            topLayout.childControlWidth=topLayout.childControlHeight=topLayout.childForceExpandWidth=topLayout.childForceExpandHeight=true;
            var skyButton=Button(top,"Sky","Jour",touch.CycleSky);var skyImage=TopIcon(skyButton,IconAsset("MobileDay",1));
            var cameraButton=Button(top,"Camera","Poursuite",touch.SwitchCamera);TopIcon(cameraButton,IconAsset("MobileCamera",5));
            var settingsButton=Button(top,"Settings","Réglages",menu.TogglePause);TopIcon(settingsButton,IconAsset("MobileSettings",7));
            Wire(touch,"sky",sky,"cameraRig",rig,"hud",hud,"skyLabel",skyButton.GetComponentInChildren<TMP_Text>(),
                "skyIcon",skyImage,"cameraLabel",cameraButton.GetComponentInChildren<TMP_Text>(),"toolbar",top.gameObject);
            serialized=new SerializedObject(touch);serialized.FindProperty("mobileToolbarInstalled").boolValue=true;
            var icons=serialized.FindProperty("skyIcons");icons.arraySize=4;
            string[] names={"MobileDay","MobileSunset","MobileNight","MobileStorm"};
            for(int i=0;i<4;i++)icons.GetArrayElementAtIndex(i).objectReferenceValue=IconAsset(names[i],i+1);
            serialized.ApplyModifiedProperties();
            var stick=safe.Find("Content/JoystickArea") as RectTransform;
            if(stick!=null){stick.anchoredPosition=new Vector2(220,220);stick.sizeDelta=new Vector2(400,380);
                var disc=stick.Find("Base");if(disc!=null){var baseImage=disc.GetComponent<Image>();baseImage.sprite=rounded;baseImage.type=Image.Type.Sliced;
                    var handle=disc.Find("Handle");if(handle!=null){handle.GetComponent<Image>().sprite=rounded;handle.GetComponent<Image>().color=Cyan;}}}
            var slider=safe.Find("Content/ThrottleSlider") as RectTransform;
            if(slider!=null){slider.anchoredPosition=new Vector2(-140,230);slider.sizeDelta=new Vector2(112,340);slider.GetComponent<Image>().sprite=rounded;slider.GetComponent<Image>().type=Image.Type.Sliced;
                var handle=slider.Find("Handle");if(handle!=null){var handleImage=handle.GetComponent<Image>();handleImage.sprite=rounded;handleImage.type=Image.Type.Sliced;handleImage.color=Cyan;}}
            // Supprime l'ancienne barre si elle n'était pas déjà nommée TopButtons.
            var oldToolbar=safe.Find("Toolbar");if(oldToolbar!=null)Undo.DestroyObjectImmediate(oldToolbar.gameObject);
            DestroyChild(safe,"EditorFlightControls");
            Wire(touch,"editorFlightControls",null);
            var adaptive=hud.GetComponent<PlaneHUDLayout>();
            if(adaptive!=null){serialized=new SerializedObject(adaptive);serialized.FindProperty("mobileToolbarInstalled").boolValue=true;
                serialized.FindProperty("touchToolbarSpace").floatValue=176;serialized.ApplyModifiedProperties();adaptive.RefreshLayout();}
            var duplicatePause=hud.transform.Find("SafeArea/Mission/Pause");if(duplicatePause!=null)duplicatePause.gameObject.SetActive(false);
            pause.GetComponent<Image>().color=new Color(0,0,0,.85f);
            foreach(Transform child in panel.Cast<Transform>().ToArray())Undo.DestroyObjectImmediate(child.gameObject);
            foreach(var old in panel.GetComponents<LayoutGroup>())Undo.DestroyObjectImmediate(old);
            var oldFitter=panel.GetComponent<ContentSizeFitter>();if(oldFitter!=null)Undo.DestroyObjectImmediate(oldFitter);
            panel.anchorMin=panel.anchorMax=panel.pivot=new Vector2(.5f,.5f);panel.anchoredPosition=Vector2.zero;panel.sizeDelta=new Vector2(1160,800);
            var image=panel.GetComponent<Image>()??panel.gameObject.AddComponent<Image>();image.sprite=rounded;image.type=Image.Type.Sliced;image.color=Dark;image.raycastTarget=true;
            // Une zone défilante conserve des cibles de 96 px même sur un paysage très étroit.
            var viewport=Rect("Viewport",panel);Stretch(viewport,12);viewport.gameObject.AddComponent<RectMask2D>();
            var content=Rect("Content",viewport);content.anchorMin=new Vector2(0,1);content.anchorMax=Vector2.one;content.pivot=new Vector2(.5f,1);content.sizeDelta=Vector2.zero;
            var layout=content.gameObject.AddComponent<VerticalLayoutGroup>();layout.padding=new RectOffset(20,20,12,12);layout.spacing=16;
            layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
            var fitter=content.gameObject.AddComponent<ContentSizeFitter>();fitter.verticalFit=ContentSizeFitter.FitMode.PreferredSize;
            var scroll=panel.GetComponent<ScrollRect>()??panel.gameObject.AddComponent<ScrollRect>();scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;
            scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=30;
            Label(content,"Title","SKY COURIER · RÉGLAGES",34,48);
            var resume=Button(content,"Resume","REPRENDRE",menu.Resume);resume.image.color=Cyan;
            var controls=Row(content,"Controls","Contrôles");
            var tactile=Button(controls,"Touch","TACTILE",menu.TouchMode);var tilt=Button(controls,"Tilt","INCLINAISON",menu.TiltMode);
            var calibrate=Button(controls,"Calibrate","CALIBRER",menu.Recalibrate);
            var keyboard=Button(controls,"Keyboard","CLAVIER",menu.KeyboardMode);
            var assistance=Row(content,"Assistance","Assistance");
            var easy=Button(assistance,"Easy","FACILE",menu.EasyAssistance);var pro=Button(assistance,"Pro","PRO",menu.ProAssistance);
            var invert=Row(content,"Invert","Inverser l’inclinaison avant/arrière",true);
            var invertButton=Button(invert,"Toggle","OFF",menu.TogglePitchInversion);invertButton.GetComponent<LayoutElement>().flexibleWidth=0;
            invertButton.GetComponent<LayoutElement>().preferredWidth=180;
            var fluidity=Row(content,"FrameRate","Fluidité");var fps30=Button(fluidity,"30","30",menu.Fps30);var fps60=Button(fluidity,"60","60",menu.Fps60);
            var vibration=Row(content,"Vibration","Vibration");
            var vibrationButton=Button(vibration,"Toggle","ON",menu.ToggleHaptics);
            var exit=Row(content,"Exit",null);Button(exit,"Restart","RECOMMENCER",menu.Restart);Button(exit,"Quit","QUITTER",menu.Quit);
            Wire(menu,"input",input,"hud",hud,"pausePanel",pause.gameObject,"firstSelected",resume.gameObject,"hudContent",hud.transform.Find("SafeArea").gameObject,
                "panel",panel,"safeArea",pauseSafe,"mobile",mobile,"touchButton",tactile,"tiltButton",tilt,"easyButton",easy,"proButton",pro,
                "fps30Button",fps30,"fps60Button",fps60,"invertButton",invertButton,"invertLabel",invertButton.GetComponentInChildren<TMP_Text>(),
                "calibrateButton",calibrate.gameObject,"keyboardButton",keyboard.gameObject);
            Wire(menu,"haptics",input.GetComponent<CrashHaptics>(),"hapticsButton",vibrationButton,"hapticsLabel",vibrationButton.GetComponentInChildren<TMP_Text>());
            // Le composant du départ garde son panneau ; les anciens libellés de pause n'ont plus de rôle.
            var launch=touch.GetComponent<PlaneMissionLaunch>();if(launch!=null)Wire(launch,"invertLabel",null,"assistanceLabel",null);
            InstallAutomaticConfiguration();
            foreach(var item in new UnityEngine.Object[]{touch,mobile,menu,hud,sky,panel,rig.GetComponent<Camera>()})EditorUtility.SetDirty(item);
            pause.gameObject.SetActive(false);Canvas.ForceUpdateCanvases();EditorSceneManager.MarkSceneDirty(scene);
        }
        static void InstallAutomaticConfiguration()
        {
            const string path="Assets/_Plane/Prefabs/Plane.prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try{if(root.GetComponent<PlaneAutoConfiguration>()==null){root.AddComponent<PlaneAutoConfiguration>();PrefabUtility.SaveAsPrefabAsset(root,path);}}
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        static T Unique<T>(GameObject[] roots) where T:Component
        {var values=roots.SelectMany(r=>r.GetComponentsInChildren<T>(true)).ToArray();if(values.Length!=1)throw new InvalidOperationException("Un seul "+typeof(T).Name+" est requis.");return values[0];}
        static void DestroyChild(Transform parent,string name){var old=parent.Find(name);if(old!=null)Undo.DestroyObjectImmediate(old.gameObject);}
        static RectTransform Rect(string name,Transform parent){var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(parent,false);r.anchorMin=r.anchorMax=r.pivot=new Vector2(.5f,.5f);return r;}
        static void Stretch(RectTransform rect,float margin=0){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=Vector2.one*margin;rect.offsetMax=-Vector2.one*margin;}
        static TMP_Text Label(Transform parent,string name,string text,int size,float height)
        {
            var r=Rect(name,parent);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.text=text;t.fontSize=size;t.enableAutoSizing=true;t.fontSizeMax=size;t.fontSizeMin=18;
            t.color=Color.white;t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;
            var e=r.gameObject.AddComponent<LayoutElement>();e.minHeight=e.preferredHeight=height;return t;
        }
        static Button Button(Transform parent,string name,string text,UnityAction action)
        {
            var r=Rect(name,parent);var image=r.gameObject.AddComponent<Image>();image.sprite=rounded;image.type=Image.Type.Sliced;image.color=new Color(.075f,.12f,.16f,1);
            var b=r.gameObject.AddComponent<Button>();b.targetGraphic=image;UnityEventTools.AddPersistentListener(b.onClick,action);
            var colors=b.colors;colors.highlightedColor=new Color(.88f,1,1);colors.pressedColor=new Color(.65f,.8f,.8f);colors.selectedColor=Color.white;b.colors=colors;
            var e=r.gameObject.AddComponent<LayoutElement>();e.minWidth=96;e.minHeight=e.preferredHeight=96;e.flexibleWidth=1;
            var label=Label(r,"Label",text,25,0);Stretch(label.rectTransform,8);return b;
        }
        static RectTransform Row(Transform parent,string name,string label,bool wide=false)
        {
            var r=Rect(name,parent);var e=r.gameObject.AddComponent<LayoutElement>();e.minHeight=e.preferredHeight=96;
            var g=r.gameObject.AddComponent<HorizontalLayoutGroup>();g.spacing=12;g.childControlWidth=g.childControlHeight=true;g.childForceExpandWidth=false;g.childForceExpandHeight=true;
            if(label!=null){var title=Label(r,"Label",label,25,96);title.alignment=TextAlignmentOptions.MidlineLeft;var size=title.GetComponent<LayoutElement>();size.minWidth=size.preferredWidth=wide?690:170;size.flexibleWidth=wide?1:0;}
            return r;
        }
        static Image TopIcon(Button button,Sprite sprite)
        {
            var label=button.GetComponentInChildren<TMP_Text>();label.fontSizeMax=label.fontSize=22;label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=new Vector2(1,.4f);
            label.rectTransform.offsetMin=new Vector2(4,5);label.rectTransform.offsetMax=new Vector2(-4,-2);
            var rect=Rect("Icon",button.transform);rect.anchoredPosition=new Vector2(0,17);rect.sizeDelta=new Vector2(36,36);
            var image=rect.gameObject.AddComponent<Image>();image.sprite=sprite;image.color=Cyan;image.raycastTarget=false;return image;
        }
        static void Wire(UnityEngine.Object target,params object[] pairs){var s=new SerializedObject(target);for(int i=0;i<pairs.Length;i+=2)s.FindProperty((string)pairs[i]).objectReferenceValue=(UnityEngine.Object)pairs[i+1];s.ApplyModifiedProperties();}
        static Sprite IconAsset(string name,int shape)
        {
            string path="Assets/_Plane/UI/"+name+".asset";var existing=AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();if(existing!=null)return existing;
            const int size=64;var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){name=name,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                var p=new Vector2((x-31.5f)/64f,(y-31.5f)/64f);float radius=p.magnitude;bool visible=false;
                switch(shape)
                {
                    case 0:var q=new Vector2(Mathf.Max(Mathf.Abs(p.x)-.29f,0),Mathf.Max(Mathf.Abs(p.y)-.29f,0));visible=q.magnitude<=.20f;break;
                    case 1:visible=radius<.21f||(radius>.29f&&radius<.46f&&Mathf.Abs(Mathf.Sin(Mathf.Atan2(p.y,p.x)*4))<.13f);break;
                    case 2:visible=(radius<.27f&&p.y>-.07f)||(Mathf.Abs(p.y+.12f)<.025f&&Mathf.Abs(p.x)<.44f);break;
                    case 3:visible=radius<.38f&&(p-new Vector2(.19f,.13f)).magnitude>.32f;break;
                    case 4:visible=(p-new Vector2(-.18f,.07f)).magnitude<.19f||(p-new Vector2(.07f,.15f)).magnitude<.23f||(Mathf.Abs(p.x)<.36f&&p.y<.08f&&p.y>-.13f)||(p.y<-.19f&&p.y>-.4f&&Mathf.Abs(Mathf.Repeat(p.x+.5f,.18f)-.09f)<.025f);break;
                    case 5:visible=(Mathf.Abs(p.x)<.43f&&Mathf.Abs(p.y)<.28f&&radius>.17f)||(Mathf.Abs(p.x+.2f)<.15f&&p.y>.25f&&p.y<.4f);break;
                    case 7:float outer=.34f+(Mathf.Cos(Mathf.Atan2(p.y,p.x)*8f)>0f?.09f:0f);visible=radius>.14f&&radius<outer;break;
                    case 6:visible=Mathf.Abs(p.y)<.36f&&(Mathf.Abs(p.x-.16f)<.065f||Mathf.Abs(p.x+.16f)<.065f);break;
                }
                texture.SetPixel(x,y,new Color(1,1,1,visible?1:0));
            }
            texture.Apply();AssetDatabase.CreateAsset(texture,path);var sprite=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,shape==0?new Vector4(20,20,20,20):Vector4.zero);
            sprite.name=name;AssetDatabase.AddObjectToAsset(sprite,texture);return sprite;
        }
    }
}
#endif
