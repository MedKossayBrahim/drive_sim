using UnityEngine;
using UnityEngine.InputSystem;

namespace Project.Plane
{
    /// <summary>Diagnostic léger, activable avec F3, sans dépendance à une UI de jeu.</summary>
    public sealed class PlaneInputDebugOverlay : MonoBehaviour
    {
        [SerializeField] private PlaneInput input = null;
        [SerializeField] private bool visible = false;
        [SerializeField] private Vector2 position = new Vector2(15f, 90f);
        [SerializeField, Range(12, 36)] private int fontSize = 18;
        private GUIStyle style;
        private void OnEnable()
        {
            if (Application.isMobilePlatform && !Application.isEditor) { visible = false; enabled = false; }
        }
        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame) visible = !visible;
        }
        private void OnGUI()
        {
            if ((Application.isMobilePlatform && !Application.isEditor) || !visible || input == null || input.Controller == null) return;
            if (style == null) style = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, wordWrap = true };
            style.fontSize = fontSize;
            var controller = input.Controller;
            string text = $"Sky Courier — {input.Mode}{(input.IsPaused ? " / PAUSE" : "")}\n" +
                $"Gaz {controller.Throttle:F2} | Pitch {controller.Pitch:F2} | Roll {controller.Roll:F2} | Yaw {controller.Yaw:F2}\n" +
                $"Vitesse {controller.ForwardSpeed:F1} m/s | Volets {controller.Flaps:F0} | Train {controller.GearDeployed}\n" +
                $"Capteur : {input.SensorStatus} | Calibré : {input.IsCalibrated}\n" +
                $"Accéléromètre : {Accelerometer.current != null} | Gravité : {GravitySensor.current != null} | Attitude : {AttitudeSensor.current != null}";
            GUI.Box(new Rect(position.x, position.y, Mathf.Min(720f, Screen.width - position.x), fontSize * 8f), text, style);
        }
#if UNITY_EDITOR
        public void Configure(PlaneInput target) => input = target;
#endif
    }
}
