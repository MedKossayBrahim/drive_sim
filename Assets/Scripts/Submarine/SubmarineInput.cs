using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace DriveSim.Submarine
{
    /// <summary>
    /// Input handling for mobile (touch) and keyboard/gamepad.
    /// Maps to SubmarineController.
    /// </summary>
    public class SubmarineInput : MonoBehaviour
    {
        [Header("References")]
        public SubmarineController controller;
        public SonarSystem sonar;
        public BatterySystem battery;
        public HullSystem hull;
        public SubmarineAudio audioSystem;

        [Header("Sensitivity")]
        public float yawSensitivity = 1f;
        public float pitchSensitivity = 1f;
        public float throttleSensitivity = 1f;

        [Header("Touch Controls")]
        public bool enableTouchControls = true;
        public float joystickRadius = 100f;
        public float throttleZoneWidth = 200f;

        // Touch state
        private Vector2 _leftStickInput;
        private Vector2 _rightStickInput;
        private float _throttleInput;
        private int _leftTouchId = -1;
        private int _rightTouchId = -1;
        private Vector2 _leftTouchStart;
        private Vector2 _rightTouchStart;

        // Keyboard state
        private float _keyThrottle;
        private float _keyYaw;
        private float _keyPitch;
        private float _keyBallast;

        void Awake()
        {
            if (controller == null) controller = GetComponent<SubmarineController>();
            if (sonar == null) sonar = GetComponent<SonarSystem>();
            if (battery == null) battery = GetComponent<BatterySystem>();
            if (hull == null) hull = GetComponent<HullSystem>();
        }

        void Update()
        {
            HandleKeyboardInput();
            HandleTouchInput();
            ApplyInput();
        }

        void HandleKeyboardInput()
        {
            // Throttle: W/S
            _keyThrottle = 0f;
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed) _keyThrottle = 1f;
                if (Keyboard.current.sKey.isPressed) _keyThrottle = -1f;

                // Yaw: A/D
                _keyYaw = 0f;
                if (Keyboard.current.aKey.isPressed) _keyYaw = -1f;
                if (Keyboard.current.dKey.isPressed) _keyYaw = 1f;

                // Pitch: Q/E or Up/Down arrows
                _keyPitch = 0f;
                if (Keyboard.current.qKey.isPressed || Keyboard.current.upArrowKey.isPressed) _keyPitch = 1f;
                if (Keyboard.current.eKey.isPressed || Keyboard.current.downArrowKey.isPressed) _keyPitch = -1f;

                // Ballast: Shift/Ctrl
                _keyBallast = 0f;
                if (Keyboard.current.leftShiftKey.isPressed) _keyBallast = 1f;   // dive
                if (Keyboard.current.leftCtrlKey.isPressed) _keyBallast = -1f;   // surface

                // Sonar ping: Space
                if (Keyboard.current.spaceKey.wasPressedThisFrame)
                {
                    if (sonar != null) sonar.Ping();
                }

                // Toggle silent running: T
                if (Keyboard.current.tKey.wasPressedThisFrame)
                {
                    if (controller != null) controller.ToggleSilentRunning();
                }

                // Toggle lights: L
                if (Keyboard.current.lKey.wasPressedThisFrame)
                {
                    if (battery != null) battery.ToggleLights();
                }

                // Repair: R
                if (Keyboard.current.rKey.wasPressedThisFrame)
                {
                    if (hull != null) hull.Repair();
                }
            }
        }

        void HandleTouchInput()
        {
            if (!enableTouchControls || Touchscreen.current == null) return;

            var touches = Touchscreen.current.touches;

            foreach (var touch in touches)
            {
                if (!touch.press.isPressed) continue;

                Vector2 pos = touch.position.ReadValue();

                // Left half = steering joystick
                if (pos.x < Screen.width * 0.5f)
                {
                    if (_leftTouchId == -1)
                    {
                        _leftTouchId = touch.touchId.ReadValue();
                        _leftTouchStart = pos;
                    }

                    if (touch.touchId.ReadValue() == _leftTouchId)
                    {
                        Vector2 delta = pos - _leftTouchStart;
                        delta = Vector2.ClampMagnitude(delta, joystickRadius);
                        _leftStickInput = delta / joystickRadius;
                    }
                }
                // Right half = throttle + ballast
                else
                {
                    if (_rightTouchId == -1)
                    {
                        _rightTouchId = touch.touchId.ReadValue();
                        _rightTouchStart = pos;
                    }

                    if (touch.touchId.ReadValue() == _rightTouchId)
                    {
                        Vector2 delta = pos - _rightTouchStart;
                        _throttleInput = Mathf.Clamp(delta.y / throttleZoneWidth, -1f, 1f);
                        _rightStickInput.x = Mathf.Clamp(delta.x / throttleZoneWidth, -1f, 1f);
                    }
                }
            }

            // Handle touch release
            foreach (var touch in touches)
            {
                if (touch.press.wasReleasedThisFrame)
                {
                    int id = touch.touchId.ReadValue();
                    if (id == _leftTouchId)
                    {
                        _leftTouchId = -1;
                        _leftStickInput = Vector2.zero;
                    }
                    if (id == _rightTouchId)
                    {
                        _rightTouchId = -1;
                        _throttleInput = 0f;
                        _rightStickInput = Vector2.zero;
                    }
                }
            }
        }

        void ApplyInput()
        {
            if (controller == null) return;

            // Combine keyboard and touch input
            float yaw = _keyYaw + (_leftStickInput.x * yawSensitivity);
            float pitch = _keyPitch + (_leftStickInput.y * pitchSensitivity);
            float throttle = _keyThrottle + (_throttleInput * throttleSensitivity);
            float ballast = _keyBallast + (_rightStickInput.x * 0.5f);

            controller.SetYaw(yaw);
            controller.SetPitch(pitch);
            controller.SetThrottle(throttle);
            controller.SetBallast(ballast);
        }

        // --- Touch Input Helpers ---

        public void SetJoystickInput(Vector2 input)
        {
            _leftStickInput = input;
        }

        public void SetThrottleInput(float value)
        {
            _throttleInput = value;
        }

        // --- UI Button callbacks (for on-screen buttons) ---

        public void OnSonarPingButton()
        {
            if (sonar != null) sonar.Ping();
        }

        public void OnSilentRunningToggle()
        {
            if (controller != null) controller.ToggleSilentRunning();
        }

        public void OnLightsToggle()
        {
            if (battery != null) battery.ToggleLights();
        }

        public void OnRepairButton()
        {
            if (hull != null) hull.Repair();
        }
    }
}
