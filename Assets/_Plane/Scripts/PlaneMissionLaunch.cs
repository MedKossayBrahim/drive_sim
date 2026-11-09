using TMPro;
using UnityEngine;

namespace Project.Plane
{
    /// <summary>Départ mobile : calibration dans la position tenue au clic, sans démarrer les gaz automatiquement.</summary>
    [DefaultExecutionOrder(1100), DisallowMultipleComponent]
    public sealed class PlaneMissionLaunch : MonoBehaviour
    {
        [SerializeField] private PlaneInput input = null;
        [SerializeField] private CheckpointManager checkpoints = null;
        [SerializeField] private GameObject takeoffPrompt = null;
        [SerializeField] private TMP_Text invertLabel = null, assistanceLabel = null;
        private bool holdsTimer;
        private void Start()
        {
            if(input!=null&&!input.IsMissionStarted&&checkpoints!=null&&checkpoints.enabled)
            {checkpoints.enabled=false;holdsTimer=true;}
        }
        private void LateUpdate()
        {
            if(input==null)return;
            if(takeoffPrompt!=null)takeoffPrompt.SetActive(!input.IsMissionStarted&&!input.IsPaused);
            if(invertLabel!=null)invertLabel.text="TANGAGE\n"+(input.PitchInverted?"INVERSE":"NORMAL");
            if(assistanceLabel!=null&&input.Controller!=null)assistanceLabel.text="ASSISTANCE\n"+(input.Controller.Assistance==FlightAssistance.Easy?"FACILE":"PRO");
        }
        public void TakeOff()
        {
            if(input==null)return;input.BeginMission();
            if(holdsTimer&&checkpoints!=null){checkpoints.enabled=true;holdsTimer=false;}
            if(takeoffPrompt!=null)takeoffPrompt.SetActive(false);
        }
        public void TogglePitchInversion(){if(input!=null)input.TogglePitchInversion();}
        public void ToggleAssistance(){if(input!=null&&input.Controller!=null)input.Controller.ToggleAssistance();}
        private void OnDisable(){if(holdsTimer&&checkpoints!=null)checkpoints.enabled=true;holdsTimer=false;}
    }
}
