using System.Collections;
using TMPro;
using UnityEngine;

namespace Project.Plane
{
    /// <summary>Réglages Android locaux au jeu ; aucune préférence du projet n'est modifiée dans l'éditeur.</summary>
    [DefaultExecutionOrder(1000), DisallowMultipleComponent]
    public sealed class PlaneMobileSettings : MonoBehaviour
    {
        [SerializeField] private PlaneInput input = null;
        [SerializeField] private TMP_Text fpsLabel = null;
        [SerializeField] private GameObject[] editorOnlyControls = new GameObject[0];
        [SerializeField, Tooltip("30 par défaut ; le menu permet de sélectionner 60 pendant la session.")]
        private int targetFrames = 30;
        public int TargetFrames => targetFrames;
        private static bool IsPhone => Application.isMobilePlatform && !Application.isEditor;
        private void OnEnable()
        {
            foreach(var control in editorOnlyControls)if(control!=null)control.SetActive(Application.isEditor);
            if(IsPhone)
            {
                Screen.autorotateToPortrait = Screen.autorotateToPortraitUpsideDown = false;
                Screen.autorotateToLandscapeLeft = Screen.autorotateToLandscapeRight = true;
                Screen.orientation = ScreenOrientation.LandscapeLeft;
                Screen.sleepTimeout = SleepTimeout.NeverSleep;
                QualitySettings.vSyncCount = 0;
                ApplyFrameRate();
                StartCoroutine(EnableLandscapeRotation());
                // Les deux diagnostics restent disponibles dans l'éditeur seulement.
                foreach(var root in gameObject.scene.GetRootGameObjects())
                {
                    foreach(var overlay in root.GetComponentsInChildren<PlaneInputDebugOverlay>(true))overlay.enabled=false;
                    foreach(var overlay in root.GetComponentsInChildren<PlaneSensorsDebugOverlay>(true))overlay.enabled=false;
                }
            }
            RefreshLabel();
        }
        private IEnumerator EnableLandscapeRotation()
        {
            yield return null;
            // Démarre à gauche, puis autorise uniquement les deux orientations paysage.
            if(IsPhone)Screen.orientation = ScreenOrientation.AutoRotation;
        }
        private void Start()
        {
            if(IsPhone && input != null && input.Mode == ControlMode.Keyboard) input.SetMode(ControlMode.Tilt);
        }
        private void LateUpdate()
        {
            // Passe après PlaneMenu : ouvrir Pause ne fait jamais apparaître un curseur Android.
            if(IsPhone){Cursor.visible=false;Cursor.lockState=CursorLockMode.None;}
        }
        public void Fps30(){targetFrames=30;ApplyFrameRate();RefreshLabel();}
        public void Fps60(){targetFrames=60;ApplyFrameRate();RefreshLabel();}
        private void ApplyFrameRate(){if(IsPhone)Application.targetFrameRate=targetFrames==60?60:30;}
        private void RefreshLabel(){if(fpsLabel!=null)fpsLabel.text="FLUIDITE : "+targetFrames+" FPS";}
    }
}
