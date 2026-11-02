using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.Plane
{
    /// <summary>Bloc F3 indépendant, placé sous PlaneInputDebugOverlay sans modifier ce dernier.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(PlaneSensors))]
    public sealed class PlaneSensorsDebugOverlay : MonoBehaviour
    {
        [SerializeField] private bool visible = false;
        [SerializeField] private Vector2 position = new Vector2(15f, 250f);
        [SerializeField, Range(12, 36)] private int fontSize = 18;
        [SerializeField, Min(300f)] private float width = 720f;
        private PlaneSensors sensors;
        private GUIStyle style;
        private void Awake() => sensors = GetComponent<PlaneSensors>();
        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame) visible = !visible;
        }
        private static string Number(float value, string format = "F1") =>
            float.IsNaN(value) || float.IsInfinity(value) ? "--" : value.ToString(format);
        private void OnGUI()
        {
            if (!visible || sensors == null) return;
            if (style == null) style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, wordWrap = true };
            style.fontSize = fontSize;
            style.normal.textColor = sensors.TerrainWarning ? new Color(1f, 0.4f, 0.3f) : Color.white;
            string next = sensors.HasNextCheckpoint ? sensors.NextCheckpoint.transform.parent != null ?
                sensors.NextCheckpoint.transform.parent.name : sensors.NextCheckpoint.name : "aucun";
            string text = "Sky Courier — Capteurs\n" +
                $"Vitesse {sensors.SpeedKmh:F1} km/h | {sensors.SpeedKnots:F1} kt\n" +
                $"MSL {sensors.AltitudeMSL:F1} m | AGL {Number(sensors.AltitudeAGL)} m | V/S {sensors.VerticalSpeed:+0.0;-0.0;0.0} m/s\n" +
                $"Cap {sensors.HeadingDegrees:F0}° | Pitch {sensors.PitchDegrees:F1}° | Roll {sensors.RollDegrees:F1}°\n" +
                $"G {(sensors.HasAccelerationSample ? Number(sensors.GForce, "F2") : "--")} | AoA {Number(sensors.AngleOfAttackDegrees)}° | Stall {sensors.IsStalling}\n" +
                $"Carburant {sensors.FuelLiters:F1}/{sensors.FuelCapacityLiters:F1} L ({sensors.FuelFraction:P0}) | Moteur {(sensors.IsEngineActive() ? "actif" : "coupé")}\n" +
                $"Prochain : {next} | Distance {Number(sensors.DistanceToNextCheckpoint)} m\n" +
                $"Cap cible {Number(sensors.BearingToNextCheckpoint, "F0")}° | Écart {Number(sensors.RelativeBearingToNextCheckpoint, "F0")}°\n" +
                $"Obstacle {(sensors.HasObstacle ? sensors.Obstacle.name : "aucun")} | Distance {Number(sensors.ObstacleDistance)} m | Impact {Number(sensors.TimeToImpact)} s\n" +
                $"{(sensors.TerrainWarning ? "TERRAIN !" : "TERRAIN : normal")}{(sensors.IsLowFuel ? " | CARBURANT FAIBLE" : "")}";
            GUI.Box(new Rect(position.x, position.y, Mathf.Max(1f, Mathf.Min(width, Screen.width - position.x)), fontSize * 13f), text, style);
        }
    }
}
