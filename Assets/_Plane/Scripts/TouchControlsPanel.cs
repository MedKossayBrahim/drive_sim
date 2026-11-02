using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace Project.Plane
{
    /// <summary>Garde ce composant actif même quand le contenu tactile est masqué.</summary>
    [DisallowMultipleComponent]
    public sealed class TouchControlsPanel : MonoBehaviour
    {
        [SerializeField] private PlaneInput input = null;
        [SerializeField] private GameObject content = null;
        [SerializeField] private GameObject joystickArea = null;
        [SerializeField] private GameObject toolbar = null;
        [SerializeField] private RectTransform safeArea = null;
        [SerializeField, Tooltip("Permet de tester Touch/Tilt sur PC ; Keyboard masque toujours le panneau.")]
        private bool showOnDesktop = true;
        [Header("Interface mobile")]
        [SerializeField] private SkyboxSwitcher sky = null;
        [SerializeField] private PlaneCameraRig cameraRig = null;
        [SerializeField] private PlaneHUD hud = null;
        [SerializeField] private TMP_Text cameraLabel = null, skyLabel = null;
        [SerializeField] private Image skyIcon = null;
        [SerializeField] private Sprite[] skyIcons = new Sprite[4];
        [SerializeField] private GameObject editorFlightControls = null;
        [SerializeField] private bool mobileToolbarInstalled = false;
        private Rect previousSafeArea;
        private int previousWidth, previousHeight;
        private void OnEnable()
        {
            if (input != null) input.OnModeChanged += Refresh;
            Refresh(input != null ? input.Mode : ControlMode.Keyboard);
        }
        private void OnDisable() { if (input != null) input.OnModeChanged -= Refresh; }
        private void Update()
        {
            Refresh(input != null ? input.Mode : ControlMode.Keyboard);
            if (cameraLabel != null && cameraRig != null) cameraLabel.text = cameraRig.Mode == PlaneCameraMode.Chase ? "Poursuite" : cameraRig.Mode == PlaneCameraMode.Cockpit ? "Cockpit" : "Orbite";
            if(skyLabel!=null&&sky!=null)skyLabel.text=SkyName(sky.SelectedPreset);
            if(skyIcon!=null&&sky!=null&&skyIcons!=null&&(int)sky.SelectedPreset<skyIcons.Length)skyIcon.sprite=skyIcons[(int)sky.SelectedPreset];
            if(editorFlightControls!=null)editorFlightControls.SetActive(Application.isEditor && input!=null && !input.IsPaused);
            if (safeArea == null || Screen.width <= 0 || Screen.height <= 0) return;
            Rect area = Screen.safeArea;
            if (area == previousSafeArea && Screen.width == previousWidth && Screen.height == previousHeight) return;
            previousSafeArea = area; previousWidth = Screen.width; previousHeight = Screen.height;
            safeArea.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            safeArea.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            safeArea.offsetMin = safeArea.offsetMax = Vector2.zero;
        }
        private void Refresh(ControlMode value)
        {
            bool visible = input != null && !input.IsPaused && input.IsMissionStarted && value != ControlMode.Keyboard && (Application.isMobilePlatform || showOnDesktop);
            if (content != null && content.activeSelf != visible) content.SetActive(visible);
            // Sur mobile, conserver les boutons de mode pour pouvoir revenir de Keyboard à Touch.
            bool showToolbar = input != null && !input.IsPaused && (visible || Application.isMobilePlatform || (mobileToolbarInstalled && Application.isEditor));
            if (toolbar != null && toolbar.activeSelf != showToolbar) toolbar.SetActive(showToolbar);
            if (joystickArea != null && joystickArea.activeSelf != (value == ControlMode.Touch)) joystickArea.SetActive(value == ControlMode.Touch);
        }
        public void CycleSky()
        {
            if (sky == null) return;
            // NextPreset passe par Apply : un choix manuel désactive le cycle automatique.
            sky.NextPreset(); hud?.ShowMessage("CIEL : " + SkyName(sky.SelectedPreset), 2f);
        }
        private static string SkyName(SkyPresetId id) => id==SkyPresetId.Noon?"Jour":id==SkyPresetId.Sunset?"Coucher":id==SkyPresetId.Night?"Nuit":"Orage";
        public void Calibrate() { if (input != null) input.Calibrate(); }
        public void ToggleFlaps() { if (input != null) input.ToggleFlaps(); }
        public void ToggleGear() { if (input != null) input.ToggleGear(); }
        public void ResetPlane() { if (input != null) input.ResetPlane(); }
        public void KeyboardMode() { if (input != null) input.SetMode(ControlMode.Keyboard); }
        public void TouchMode() { if (input != null) input.SetMode(ControlMode.Touch); }
        public void TiltMode() { if (input != null) input.SetMode(ControlMode.Tilt); }
        public void TogglePause() { if (input != null) input.TogglePause(); }
        public void SwitchCamera() { if (input != null) input.SwitchCamera(); }
#if UNITY_EDITOR
        public void Configure(PlaneInput target, GameObject controls, GameObject stickArea, RectTransform safe, GameObject buttons)
        { input = target; content = controls; joystickArea = stickArea; safeArea = safe; toolbar = buttons; }
#endif
    }
}
