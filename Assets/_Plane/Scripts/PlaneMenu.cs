using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project.Plane
{
    /// <summary>Pause mobile, préférences persistantes et reprise à vitesse normale.</summary>
    [DefaultExecutionOrder(200), DisallowMultipleComponent]
    public sealed class PlaneMenu : MonoBehaviour
    {
        private const string Pref = "SkyCourier.";
        [SerializeField] private PlaneInput input = null;
        [SerializeField] private PlaneHUD hud = null;
        [SerializeField] private GameObject pausePanel = null, firstSelected = null, hudContent = null;
        [SerializeField] private RectTransform panel = null, safeArea = null;
        [SerializeField] private PlaneMobileSettings mobile = null;
        [SerializeField] private Button touchButton = null, tiltButton = null, easyButton = null, proButton = null, fps30Button = null, fps60Button = null, invertButton = null;
        [SerializeField] private GameObject calibrateButton = null, keyboardButton = null;
        [SerializeField] private TMP_Text invertLabel = null;
        [SerializeField] private CrashHaptics haptics = null;
        [SerializeField] private Button hapticsButton = null;
        [SerializeField] private TMP_Text hapticsLabel = null;
        [SerializeField] private Color accent = new Color(46f/255f,196f/255f,182f/255f);
        private readonly Color idle = new Color(.075f,.12f,.16f,1f);
        private bool wasPaused, restarting, preferencesLoaded, capturedCursor;
        private int frameRate = 30;
        private GameObject previousSelection;
        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible;
        private RectTransform pinnedHeader;
        private ScrollRect settingsScroll;
        private const float HeaderHeight = 184f;
        public int FrameRate => frameRate;
        private void Start()
        {
            if (input == null) return;
            PinResumeButton();
            // Awake des contrôles/capteurs est terminé avant de restaurer les choix du joueur.
            if (PlayerPrefs.HasKey(Pref+"ControlMode"))
            {
                var mode=(ControlMode)Mathf.Clamp(PlayerPrefs.GetInt(Pref+"ControlMode"),0,2);
                if(Application.isMobilePlatform && mode==ControlMode.Keyboard) mode=ControlMode.Tilt;
                input.SetMode(mode);
            }
            input.Controller.SetAssistance((FlightAssistance)Mathf.Clamp(PlayerPrefs.GetInt(Pref+"Assistance",0),0,1));
            bool inverted=PlayerPrefs.GetInt(Pref+"InvertPitch",input.PitchInverted?1:0)!=0;
            if(input.PitchInverted!=inverted)input.TogglePitchInversion();
            SetFrameRate(PlayerPrefs.GetInt(Pref+"FrameRate",30),false);
            preferencesLoaded=true;input.OnModeChanged+=ModeChanged;
            RefreshChoices();
        }
        private void ModeChanged(ControlMode _) { if(preferencesLoaded)SavePreferences(); }
        private void LateUpdate()
        {
            bool paused=input!=null&&input.IsPaused;
            if(pausePanel!=null&&pausePanel.activeSelf!=paused)pausePanel.SetActive(paused);
            if(hudContent!=null&&hudContent.activeSelf==paused)hudContent.SetActive(!paused);
            if(paused&&!wasPaused)
            {
                previousCursorLock=Cursor.lockState;previousCursorVisible=Cursor.visible;capturedCursor=true;
                Cursor.lockState=CursorLockMode.None;Cursor.visible=!Application.isMobilePlatform;
                if (settingsScroll != null)
                {
                    Canvas.ForceUpdateCanvases();
                    settingsScroll.StopMovement();
                    settingsScroll.verticalNormalizedPosition = 1f;
                }
                if(EventSystem.current!=null){previousSelection=EventSystem.current.currentSelectedGameObject;EventSystem.current.SetSelectedGameObject(firstSelected);}
            }
            else if(!paused&&wasPaused)RestoreCursor();
            if(paused)
            {
                if(panel!=null&&safeArea!=null)panel.sizeDelta=new Vector2(Mathf.Min(1160f,safeArea.rect.width-40f),Mathf.Min(800f,safeArea.rect.height-32f));
                if (pinnedHeader != null)
                {
                    pinnedHeader.anchoredPosition = panel.anchoredPosition + Vector2.up * (panel.rect.height * .5f);
                    pinnedHeader.sizeDelta = new Vector2(panel.rect.width, HeaderHeight);
                }
                RefreshChoices();
            }
            wasPaused=paused;
        }
        private void PinResumeButton()
        {
            if (panel == null || firstSelected == null) return;
            var scroll = panel.GetComponent<ScrollRect>();
            if (scroll == null || scroll.content == null || scroll.viewport == null ||
                firstSelected.transform.parent != scroll.content || firstSelected.transform is not RectTransform resume) return;

            settingsScroll = scroll;
            // Le bouton doit être hors de la hiérarchie du ScrollRect pour qu'un glissement ne vole pas le clic.
            pinnedHeader = new GameObject("FixedPauseHeader", typeof(RectTransform)).GetComponent<RectTransform>();
            pinnedHeader.SetParent(panel.parent, false);
            pinnedHeader.anchorMin = pinnedHeader.anchorMax = panel.anchorMin;
            pinnedHeader.pivot = new Vector2(.5f, 1f);
            var title = scroll.content.Find("Title") as RectTransform;
            if (title != null) PlaceHeaderItem(title, 24f, 48f);
            PlaceHeaderItem(resume, 88f, 96f);
            scroll.viewport.offsetMax = new Vector2(scroll.viewport.offsetMax.x, -HeaderHeight - 12f);
        }

        private void PlaceHeaderItem(RectTransform item, float top, float height)
        {
            item.SetParent(pinnedHeader, false);
            item.anchorMin = new Vector2(0f, 1f);
            item.anchorMax = Vector2.one;
            item.offsetMin = new Vector2(32f, -top - height);
            item.offsetMax = new Vector2(-32f, -top);
        }
        private void RefreshChoices()
        {
            if(input==null)return;
            Highlight(touchButton,input.Mode==ControlMode.Touch);Highlight(tiltButton,input.Mode==ControlMode.Tilt);
            Highlight(easyButton,input.Controller.Assistance==FlightAssistance.Easy);Highlight(proButton,input.Controller.Assistance==FlightAssistance.Pro);
            Highlight(fps30Button,frameRate==30);Highlight(fps60Button,frameRate==60);Highlight(invertButton,input.PitchInverted);
            if(invertLabel!=null)invertLabel.text=input.PitchInverted?"ON":"OFF";
            bool vibrates = haptics != null ? haptics.HapticsEnabled : PlayerPrefs.GetInt(CrashHaptics.PreferenceKey, 1) != 0;
            Highlight(hapticsButton, vibrates);
            if(hapticsLabel!=null)hapticsLabel.text=vibrates?"ON":"OFF";
            if(calibrateButton!=null)calibrateButton.SetActive(input.Mode==ControlMode.Tilt);
            if(keyboardButton!=null)keyboardButton.SetActive(Application.isEditor||!Application.isMobilePlatform);
        }
        private void Highlight(Button button,bool selected)
        {
            if(button==null)return;
            // La teinte de sélection reste visible même après un clic et la perte de focus.
            button.image.color=selected?accent:idle;
            var text=button.GetComponentInChildren<TMP_Text>();if(text!=null)text.color=Color.white;
        }
        public void TogglePause(){if(input!=null){if(input.IsPaused)Resume();else input.TogglePause();}}
        public void Resume(){if(input!=null&&input.IsPaused)input.TogglePause();Time.timeScale=1f;}
        public void Recalibrate(){if(input!=null){input.Calibrate();hud?.ShowMessage("INCLINAISON RECALIBRÉE À LA REPRISE",2f);}}
        public void KeyboardMode()=>SetMode(ControlMode.Keyboard);
        public void TouchMode()=>SetMode(ControlMode.Touch);
        public void TiltMode()=>SetMode(ControlMode.Tilt);
        private void SetMode(ControlMode mode)
        {if(input==null)return;input.SetMode(mode);SavePreferences();RefreshChoices();}
        public void EasyAssistance()=>SetAssistance(FlightAssistance.Easy);
        public void ProAssistance()=>SetAssistance(FlightAssistance.Pro);
        private void SetAssistance(FlightAssistance value){if(input==null)return;input.Controller.SetAssistance(value);SavePreferences();RefreshChoices();}
        public void TogglePitchInversion(){if(input==null)return;input.TogglePitchInversion();SavePreferences();RefreshChoices();}
        public void ToggleHaptics()
        {
            bool enabled = haptics != null ? haptics.HapticsEnabled : PlayerPrefs.GetInt(CrashHaptics.PreferenceKey, 1) != 0;
            if(haptics!=null)haptics.SetHapticsEnabled(!enabled);
            else { PlayerPrefs.SetInt(CrashHaptics.PreferenceKey,enabled?0:1);PlayerPrefs.Save(); }
            RefreshChoices();
        }
        public void Fps30()=>SetFrameRate(30,true);
        public void Fps60()=>SetFrameRate(60,true);
        private void SetFrameRate(int value,bool save)
        {
            frameRate=value==60?60:30;
            if(mobile!=null){if(frameRate==60)mobile.Fps60();else mobile.Fps30();}
            if(Application.isMobilePlatform){QualitySettings.vSyncCount=0;Application.targetFrameRate=frameRate;}
            if(save)SavePreferences();RefreshChoices();
        }
        private void SavePreferences()
        {
            if(input==null)return;
            PlayerPrefs.SetInt(Pref+"ControlMode",(int)input.Mode);
            PlayerPrefs.SetInt(Pref+"Assistance",(int)input.Controller.Assistance);
            PlayerPrefs.SetInt(Pref+"InvertPitch",input.PitchInverted?1:0);
            PlayerPrefs.SetInt(Pref+"FrameRate",frameRate);PlayerPrefs.Save();
        }
        public void Restart()
        {
            if(restarting)return;var scene=gameObject.scene;
            if(string.IsNullOrEmpty(scene.path)){hud?.ShowMessage("SCÈNE NON ENREGISTRÉE",3f);return;}
#if !UNITY_EDITOR
            if(!Application.CanStreamedLevelBeLoaded(scene.path)){hud?.ShowMessage("AJOUTER PLANE_SCENE AU BUILD PROFILE",4f);return;}
#endif
            SavePreferences();Resume();restarting=true;
            // Une mission seule se recharge directement : évite deux caméras et AudioListeners simultanés.
            var loadMode=SceneManager.sceneCount==1?LoadSceneMode.Single:LoadSceneMode.Additive;
            // Recharge une mission neuve : réservoir, position, score et états des anneaux sont réinitialisés ensemble.
#if UNITY_EDITOR
            var operation=UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(scene.path,new LoadSceneParameters(loadMode));
#else
            var operation=SceneManager.LoadSceneAsync(scene.path,loadMode);
#endif
            operation.completed+=_=>
            {
                for(int i=SceneManager.sceneCount-1;i>=0;i--){var loaded=SceneManager.GetSceneAt(i);if(loaded!=scene&&loaded.path==scene.path){SceneManager.SetActiveScene(loaded);break;}}
                if(scene.IsValid()&&scene.isLoaded)SceneManager.UnloadSceneAsync(scene);
            };
        }
        public void Quit(){Resume();SavePreferences();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
#else
            Application.Quit();
#endif
        }
        private void RestoreCursor()
        {
            if(!capturedCursor)return;Cursor.lockState=previousCursorLock;Cursor.visible=previousCursorVisible;capturedCursor=false;
            if(EventSystem.current!=null&&previousSelection!=null)EventSystem.current.SetSelectedGameObject(previousSelection);
        }
        private void OnApplicationPause(bool paused){if(paused&&preferencesLoaded)SavePreferences();}
        private void OnDestroy(){if(input!=null)input.OnModeChanged-=ModeChanged;}
        private void OnDisable(){Resume();RestoreCursor();if(pausePanel!=null)pausePanel.SetActive(false);if(hudContent!=null)hudContent.SetActive(true);wasPaused=false;}
    }
}
