using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Plane
{
    /// <summary>Instruments passifs : les mesures viennent des capteurs, sans intervention sur le vol.</summary>
    [DefaultExecutionOrder(100), DisallowMultipleComponent]
    public sealed class PlaneHUD : MonoBehaviour
    {
        [SerializeField] private PlaneSensors sensors = null;
        [SerializeField] private PlaneInput input = null;
        [SerializeField] private CheckpointManager checkpoints = null;
        [SerializeField] private ScoreManager globalScore = null;
        [SerializeField] private LandingMission landingMission = null;
        [SerializeField] private TMP_Text speed = null, altitude = null, heading = null, gas = null, fuel = null, gForce = null, navigation = null, course = null, message = null;
        [SerializeField] private TMP_Text stallLamp = null, fuelLamp = null, terrainLamp = null;
        [SerializeField] private Image gasFill = null, fuelFill = null;
        [SerializeField] private RectTransform horizon = null, compassTape = null, navigationArrow = null;
        [SerializeField, Min(0f)] private float pitchPixelsPerDegree = 3f;
        [SerializeField, Min(0.1f)] private float compassPixelsPerDegree = 3f;
        [SerializeField] private Color normalColor = new Color(0.2f, 0.95f, 1f);
        [SerializeField] private Color warningColor = new Color(1f, 0.3f, 0.15f);
        [SerializeField] private Color ceilingWarningColor = new Color(1f,.55f,.1f);
        [SerializeField] private Color ceilingLimitColor = new Color(1f,.18f,.12f);
        private FlightCeiling flightCeiling;
        private Color altitudeNormalColor, messageNormalColor;
        private string contextMessage = "";
        private bool showingBoundaryMessage;
        private FallCrashDetector fallDetector;
        private float messageUntil;
        private int previousPassed;
        private bool previousComplete;

        private void OnEnable()
        {
            if(altitude!=null)altitudeNormalColor=altitude.color;
            if(message!=null)messageNormalColor=message.color;
            if (sensors != null)
            {
                sensors.OnStall += Stall; sensors.OnLowFuel += LowFuel;
                sensors.OnFuelEmpty += EmptyFuel; sensors.OnTerrainWarning += Terrain;
                flightCeiling=sensors.GetComponent<FlightCeiling>();
                fallDetector=sensors.GetComponent<FallCrashDetector>();
                if(fallDetector!=null)fallDetector.OnFalling+=Falling;
            }
            previousPassed = checkpoints != null ? checkpoints.PassedCount : 0;
            previousComplete = checkpoints != null && checkpoints.IsComplete;
        }
        private void OnDisable()
        {
            if(altitude!=null)altitude.color=altitudeNormalColor;
            if(message!=null)message.color=messageNormalColor;
            if (sensors == null) return;
            sensors.OnStall -= Stall; sensors.OnLowFuel -= LowFuel;
            sensors.OnFuelEmpty -= EmptyFuel; sensors.OnTerrainWarning -= Terrain;
            if(fallDetector!=null)fallDetector.OnFalling-=Falling;
        }
        // Le contrôleur signale aussi le stall à l'arrêt : ne pas alarmer le pilote stationné au sol.
        private bool ShowStall => sensors != null && sensors.IsStalling &&
            (sensors.HasGroundAltitude ? sensors.AltitudeAGL > 4f : sensors.RawSpeedMetersPerSecond > 5f);
        private void Stall() { if (ShowStall) ShowMessage("DÉCROCHAGE - REPRENDRE DE LA VITESSE", 3f); }
        private void Falling(bool value)
        {
            if(value)ShowMessage("REDRESSEZ / GAZ !",1f);
            else if(message!=null&&message.text=="REDRESSEZ / GAZ !")ShowMessage("",0f);
        }
        private void LowFuel() => ShowMessage("CARBURANT FAIBLE", 4f);
        private void EmptyFuel() => ShowMessage("RESERVOIR VIDE - MOTEUR COUPE", 6f);
        private void Terrain(bool value) { if (value) ShowMessage("TERRAIN - EVITER L'OBSTACLE", 3f); }
        public void ShowMessage(string text, float duration)
        {
            if (message == null) return;
            contextMessage = text ?? string.Empty;
            message.text = contextMessage;
            messageUntil = Time.unscaledTime + Mathf.Max(0f, duration);
            message.gameObject.SetActive(duration > 0f && !string.IsNullOrEmpty(text));
        }
        private static string Reading(float value, string format = "0") => float.IsNaN(value) || float.IsInfinity(value) ? "--" : value.ToString(format);
        private void LateUpdate()
        {
            if (message != null && Time.unscaledTime >= messageUntil) message.gameObject.SetActive(false);
            if (sensors == null) return;
            if(fallDetector!=null&&fallDetector.IsFalling)ShowMessage("REDRESSEZ / GAZ !",1f);
            speed.text = $"<color=#44DFEF>VITESSE</color>  {Reading(sensors.SpeedKmh)} km/h\n<size=80%>{Reading(sensors.SpeedKnots)} kt</size>";
            altitude.text = $"{Reading(sensors.AltitudeMSL)} m ALT  |  {Reading(sensors.AltitudeAGL)} m SOL\nV/S {Reading(sensors.VerticalSpeed, "+0.0;-0.0;0.0")} m/s";
            altitude.color=flightCeiling!=null&&flightCeiling.isActiveAndEnabled&&flightCeiling.IsCeilingWarning ?
                (flightCeiling.IsAtCeiling?ceilingLimitColor:ceilingWarningColor):altitudeNormalColor;
            heading.text = $"CAP {Mathf.RoundToInt(sensors.HeadingDegrees) % 360:000}";
            float throttle = input != null && input.Controller != null ? input.Controller.Throttle : 0f;
            gas.text = $"GAZ {throttle:P0}"; SetBar(gasFill, throttle);
            fuel.text = $"CARBURANT {sensors.FuelFraction:P0}  |  {sensors.FuelLiters:0.0} L";
            SetBar(fuelFill, sensors.FuelFraction);
            gForce.text = $"G {(sensors.HasAccelerationSample ? Reading(sensors.GForce, "0.00") : "--")}";
            Lamp(stallLamp, ShowStall); Lamp(fuelLamp, sensors.IsLowFuel); Lamp(terrainLamp, sensors.TerrainWarning);
            // Le paysage s'incline à l'opposé du roulis ; un cabré abaisse la ligne d'horizon.
            horizon.localRotation = Quaternion.Euler(0f, 0f, sensors.RollDegrees);
            horizon.anchoredPosition = new Vector2(0f, -Mathf.Clamp(sensors.PitchDegrees, -90f, 90f) * pitchPixelsPerDegree);
            compassTape.anchoredPosition = new Vector2(-sensors.HeadingDegrees * compassPixelsPerDegree, 0f);
            bool landing=landingMission!=null&&landingMission.CanLand;
            navigationArrow.gameObject.SetActive(landing||sensors.HasNextCheckpoint);
            if(landing)
            {
                Vector3 delta=landingMission.Arrival.AimPoint-sensors.transform.position;
                float bearing=Mathf.Repeat(Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg,360f);
                navigationArrow.localRotation=Quaternion.Euler(0,0,-Mathf.DeltaAngle(sensors.HeadingDegrees,bearing));
                navigation.text=$"AÉROPORT D’ARRIVÉE\n{delta.magnitude:0} m · CAP {bearing:000}";
            }
            else if (sensors.HasNextCheckpoint)
            {
                navigationArrow.localRotation = Quaternion.Euler(0f, 0f, -sensors.RelativeBearingToNextCheckpoint);
                string ringName = sensors.NextCheckpoint.name == "Trigger" && sensors.NextCheckpoint.transform.parent != null ?
                    sensors.NextCheckpoint.transform.parent.name : sensors.NextCheckpoint.name;
                navigation.text = $"{ringName.Replace("Ring_", "ANNEAU ")}\n{sensors.DistanceToNextCheckpoint:0} m  |  CAP {sensors.BearingToNextCheckpoint:000}";
            }
            else navigation.text = checkpoints != null && checkpoints.IsComplete ? "PARCOURS TERMINE" : "AUCUN ANNEAU";
            if (checkpoints == null) { course.text = "CHRONO -- | SCORE --"; RenderBoundaryMessage(); return; }
            float elapsed = checkpoints.ElapsedTime;
            course.text = $"{(int)elapsed / 60:00}:{(int)elapsed % 60:00}  |  SCORE {(globalScore != null ? globalScore.Score : checkpoints.Score)}\nANNEAUX {checkpoints.PassedCount}/{checkpoints.Checkpoints.Count}";
            if (checkpoints.IsComplete && !previousComplete) ShowMessage(landingMission!=null?"ANNEAUX TERMINÉS — OBJECTIF : ATTERRIR":"PARCOURS TERMINE !", 6f);
            else if (checkpoints.PassedCount > previousPassed) ShowMessage("ANNEAU VALIDE", 2f);
            previousPassed = checkpoints.PassedCount; previousComplete = checkpoints.IsComplete;
            RenderBoundaryMessage();
        }
        private void RenderBoundaryMessage()
        {
            if(message==null)return;
            bool available=flightCeiling!=null&&flightCeiling.isActiveAndEnabled;
            bool warning=available&&(flightCeiling.IsCeilingWarning||flightCeiling.IsOutsideZone);
            // La chute reste prioritaire. Les messages contextuels ne sont pas effacés par le clignotement du plafond.
            if(warning&&(fallDetector==null||!fallDetector.IsFalling))
            {
                bool altitudeWarning=flightCeiling.IsCeilingWarning;
                message.text=altitudeWarning?flightCeiling.WarningMessage:flightCeiling.HorizontalMessage;
                if(altitudeWarning&&flightCeiling.IsOutsideZone)
                    message.text+="\n"+flightCeiling.HorizontalMessage;
                message.color=flightCeiling.IsAtCeiling?ceilingLimitColor:ceilingWarningColor;
                message.gameObject.SetActive(!altitudeWarning||flightCeiling.WarningVisible);
                showingBoundaryMessage=true;
            }
            else if(showingBoundaryMessage)
            {
                showingBoundaryMessage=false;message.color=messageNormalColor;message.text=contextMessage;
                message.gameObject.SetActive(Time.unscaledTime<messageUntil&&!string.IsNullOrEmpty(contextMessage));
            }
        }
        // Fonctionne aussi avec une Image sans sprite ; les visuels pourront être remplacés.
        private static void SetBar(Image image, float value) => image.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
        private void Lamp(TMP_Text label, bool active) => label.color = active ? warningColor : new Color(normalColor.r, normalColor.g, normalColor.b, 0.3f);
    }
}
