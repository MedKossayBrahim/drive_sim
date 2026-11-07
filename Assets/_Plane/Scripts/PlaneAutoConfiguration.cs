using UnityEngine;

namespace Project.Plane
{
    /// <summary>Automatise train et volets via les propriétés publiques, sans ajouter de physique.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(PlaneController),typeof(PlaneSensors))]
    public sealed class PlaneAutoConfiguration : MonoBehaviour
    {
        [SerializeField, Min(1f), Tooltip("Hauteur SOL de rentrée du train après décollage, en mètres.")]
        private float gearRetractAltitude=8f;
        [SerializeField, Min(1f)] private float approachAltitude=25f;
        [SerializeField, Min(1f), Tooltip("Vitesse maximale d'approche en m/s pour sortir le train.")]
        private float approachSpeed=42f;
        [SerializeField, Min(1f), Tooltip("Volets automatiques sous cette vitesse en m/s.")]
        private float flapsExtendSpeed=35f;
        [SerializeField, Min(0f), Tooltip("Hystérésis pour éviter un battement autour du seuil.")]
        private float speedHysteresis=3f;
        [SerializeField, Range(0f,1f)] private float lowSpeedFlaps=.5f;
        private PlaneController controller;
        private PlaneSensors sensors;
        private PlaneInput input;
        private bool airborne;
        private void Awake(){controller=GetComponent<PlaneController>();sensors=GetComponent<PlaneSensors>();input=GetComponent<PlaneInput>();}
        private void OnEnable(){if(input==null)input=GetComponent<PlaneInput>();if(input!=null)input.OnResetCompleted+=ResetConfiguration;}
        private void Update()
        {
            if(controller==null||!controller.enabled||input!=null&&input.IsPaused)return;
            float speed=sensors.RawSpeedMetersPerSecond;
            bool high=!sensors.HasGroundAltitude||sensors.AltitudeAGL>=gearRetractAltitude;
            if(high){airborne=true;controller.GearDeployed=false;}
            else if(sensors.HasGroundAltitude&&sensors.AltitudeAGL<=approachAltitude&&speed<=approachSpeed)controller.GearDeployed=true;
            // Sortie en approche également avant de repasser sous la hauteur de rentrée.
            if(airborne&&sensors.HasGroundAltitude&&sensors.AltitudeAGL<=approachAltitude&&sensors.VerticalSpeed<0f&&speed<=approachSpeed)
                controller.GearDeployed=true;
            if(speed<flapsExtendSpeed)controller.Flaps=lowSpeedFlaps;
            else if(speed>flapsExtendSpeed+speedHysteresis)controller.Flaps=0f;
        }
        private void ResetConfiguration(){airborne=false;if(controller!=null){controller.GearDeployed=true;controller.Flaps=lowSpeedFlaps;}}
        private void OnDisable(){if(input!=null)input.OnResetCompleted-=ResetConfiguration;}
    }
}
