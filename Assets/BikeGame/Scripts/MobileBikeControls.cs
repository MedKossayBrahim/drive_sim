using KikiNgao.SimpleBikeControl;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace BikeGame
{
    /// <summary>
    /// Lightweight IMGUI touch controls. They are polled directly through the
    /// new Input System, so no EventSystem or UI input module is required.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class MobileBikeControls : MonoBehaviour
    {
        [SerializeField] private bool showControls = true;
        [Tooltip("Temporary on-screen diagnostics: sensor vector, calibrated tilt and final steering.")]
        [SerializeField] private bool showTiltDebug = true;

        [Header("Uphill auto pedal")]
        [Tooltip("In tilt mode the auto pedal amount rises with the slope so the bike keeps climbing instead of stalling.")]
        [SerializeField] private bool uphillAutoPedal = true;
        [Tooltip("Slope in degrees where the uphill pedal assist starts.")]
        [SerializeField] private float uphillStartDegrees = 3f;
        [Tooltip("Slope in degrees that needs full pedal input.")]
        [SerializeField] private float uphillFullDegrees = 12f;
        [Tooltip("How quickly the measured slope follows the terrain.")]
        [SerializeField] private float slopeSmoothing = 6f;

        private InputManager inputManager;
        private Biker biker;
        private TiltSteering tiltSteering;
        private SimpleBike bike;

        private int joystickTouchId = -1;
        private int padTouchId = -1;
        private int boostTouchId = -1;
        private int uiTouchId = -1;

        private const float PadCenterBand = 0.18f;
        private const float PadDragThreshold = 16f;
        private const float PadHoldDelay = 0.12f;
        private const float ThrottleDeadZone = 0.08f;

        private Vector2 joystickVector;
        private bool pedalHeld;
        private bool padDragging;
        private float padHoldTime;
        private Vector2 padTouchStart;
        private bool boostHeld;
        private bool tiltActiveThisFrame;
        private float smoothedSlopeDegrees;

        private bool mouseJoystickActive;
        private bool mousePadActive;
        private bool mouseBoostActive;

        private Texture2D circleTexture;
        private GUIStyle centerLabelStyle;
        private GUIStyle hintStyle;
        private GUIStyle debugStyle;

        private float LayoutScale
        {
            get { return Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 1080f, 0.55f, 1.6f); }
        }

        private Rect SafeArea
        {
            get
            {
                Rect area = Screen.safeArea;
                if (area.width <= 1f || area.height <= 1f)
                    area = new Rect(0f, 0f, Screen.width, Screen.height);
                return area;
            }
        }

        private float JoystickRadius => 120f * LayoutScale;
        private float BoostRadius => 68f * LayoutScale;
        private float PedalRadius => 92f * LayoutScale;

        private Vector2 JoystickCenter
        {
            get
            {
                Rect area = SafeArea;
                return new Vector2(area.x + 160f * LayoutScale, area.y + 150f * LayoutScale);
            }
        }

        private Vector2 BoostCenter
        {
            get
            {
                Rect area = SafeArea;
                return new Vector2(area.xMax - 140f * LayoutScale, area.y + 150f * LayoutScale);
            }
        }

        // Rects are stored in screen space (origin bottom-left, like touch
        // positions) and converted to GUI space only when drawing.
        private Rect ModeButtonRect
        {
            get
            {
                Rect area = SafeArea;
                float height = 52f * LayoutScale;
                return new Rect(area.x + 16f * LayoutScale,
                    area.yMax - 16f * LayoutScale - height,
                    196f * LayoutScale, height);
            }
        }

        private Rect CalibrateButtonRect
        {
            get
            {
                Rect modeRect = ModeButtonRect;
                return new Rect(modeRect.xMax + 10f * LayoutScale, modeRect.y,
                    92f * LayoutScale, modeRect.height);
            }
        }

        private Rect InvertButtonRect
        {
            get
            {
                Rect calibrate = CalibrateButtonRect;
                return new Rect(calibrate.xMax + 10f * LayoutScale, calibrate.y,
                    86f * LayoutScale, calibrate.height);
            }
        }

        private Rect TiltBarRect
        {
            get
            {
                Rect modeRect = ModeButtonRect;
                float height = 16f * LayoutScale;
                return new Rect(modeRect.x, modeRect.y - 10f * LayoutScale - height,
                    380f * LayoutScale, height);
            }
        }

        private void Start()
        {
            ResolveReferences();
        }

        private void Update()
        {
            if (inputManager == null) ResolveReferences();
            if (inputManager == null) return;

            UpdateSlope();

            joystickVector = Vector2.zero;
            pedalHeld = false;
            bool boost = false;

            tiltActiveThisFrame = tiltSteering != null && tiltSteering.IsTiltActive;

            bool anyTouch = PollTouches(ref joystickVector, ref boost, tiltActiveThisFrame);

            if (!anyTouch)
                PollMouse(ref joystickVector, ref boost, tiltActiveThisFrame);

            // The joystick controls forward/backward only. Its X axis is never
            // used for steering so it cannot fight phone-tilt steering in any mode.
            float touchThrottle = Mathf.Abs(joystickVector.y) > 0.08f ? joystickVector.y : 0f;

            // Steering always comes from tilt when tilt is active. In touch
            // mode (tilt off) there is no joystick steering axis either, so
            // the keyboard/gamepad fallback in InputManager remains available.
            float steer = tiltActiveThisFrame ? tiltSteering.SteerAxis : 0f;
            float throttle;

            if (tiltActiveThisFrame)
            {
                // The left pad acts as a vertical throttle stick: push up to
                // accelerate, pull down to brake/reverse. Holding still keeps
                // the old PEDAL full-power action; releasing falls back to the
                // automatic pedal so the bike keeps climbing without input.
                if (padDragging)
                    throttle = joystickVector.y;
                else if (pedalHeld)
                    throttle = 1f;
                else
                    throttle = AutoThrottleForSlope(smoothedSlopeDegrees);
            }
            else
            {
                throttle = touchThrottle;
            }

            inputManager.SetMobileInput(steer, throttle, boost);
            boostHeld = boost;
        }

        /// <summary>
        /// Tracks the riding slope in degrees (positive uphill) so the tilt mode
        /// can add pedal power on climbs. The bike's forward vector is used
        /// because it follows the terrain while the wheels are on the ground;
        /// smoothing keeps bumps from spiking the throttle.
        /// </summary>
        private void UpdateSlope()
        {
            if (bike == null)
            {
                bike = Object.FindAnyObjectByType<SimpleBike>();
                if (bike == null) return;
            }

            float pitch = Mathf.Asin(Mathf.Clamp(bike.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
            float blend = 1f - Mathf.Exp(-Mathf.Max(0.1f, slopeSmoothing) * Time.deltaTime);
            smoothedSlopeDegrees = Mathf.Lerp(smoothedSlopeDegrees, pitch, blend);
            if (Mathf.Abs(smoothedSlopeDegrees) < 0.05f) smoothedSlopeDegrees = 0f;
        }

        /// <summary>
        /// Auto throttle used when the pedal button is not held in tilt mode.
        /// The flat amount (0.55) is unchanged on flat ground; it rises to full
        /// power as the slope approaches <see cref="uphillFullDegrees"/> so the
        /// bike keeps climbing without the player holding PEDAL.
        /// </summary>
        public float AutoThrottleForSlope(float slopeDegrees)
        {
            float amount = tiltSteering != null && tiltSteering.AutoPedal
                ? tiltSteering.AutoPedalAmount
                : 0f;

            if (!uphillAutoPedal)
                return amount;

            float full = Mathf.Max(uphillStartDegrees + 0.5f, uphillFullDegrees);
            float uphill = Mathf.InverseLerp(uphillStartDegrees, full, slopeDegrees);
            return Mathf.Clamp01(Mathf.Lerp(amount, 1f, uphill));
        }

        /// <summary>Smoothed terrain slope in degrees, positive uphill.</summary>
        public float CurrentSlopeDegrees => smoothedSlopeDegrees;

        /// <summary>Auto throttle that tilt mode would apply this frame.</summary>
        public float CurrentAutoThrottle => AutoThrottleForSlope(smoothedSlopeDegrees);

        private bool PollTouches(ref Vector2 axes, ref bool boost, bool tiltActive)
        {
            Touchscreen touchscreen = Touchscreen.current;
            if (touchscreen == null)
            {
                joystickTouchId = -1;
                padTouchId = -1;
                boostTouchId = -1;
                uiTouchId = -1;
                ResetPadState();
                return false;
            }

            bool anyTouch = false;

            foreach (TouchControl touch in touchscreen.touches)
            {
                bool pressed = touch.press.isPressed;
                bool released = touch.press.wasReleasedThisFrame;
                if (pressed) anyTouch = true;
                if (!pressed && !released) continue;

                int touchId = touch.touchId.ReadValue();
                Vector2 position = touch.position.ReadValue();

                if (touchId == uiTouchId)
                {
                    if (!pressed) uiTouchId = -1;
                    continue;
                }

                if (touchId == joystickTouchId)
                {
                    if (pressed)
                    {
                        UpdateJoystickVector(position, ref axes);
                    }
                    else
                    {
                        joystickTouchId = -1;
                    }
                    continue;
                }

                if (touchId == padTouchId)
                {
                    if (pressed)
                    {
                        UpdatePadInput(position, ref axes);
                    }
                    else
                    {
                        padTouchId = -1;
                        ResetPadState();
                        axes = Vector2.zero;
                    }
                    continue;
                }

                if (touchId == boostTouchId)
                {
                    boost = pressed;
                    if (!pressed) boostTouchId = -1;
                    continue;
                }

                if (!touch.press.wasPressedThisFrame) continue;

                if (HandleUiTap(position))
                {
                    uiTouchId = touchId;
                    continue;
                }

                if (tiltActive)
                {
                    if (Vector2.Distance(position, JoystickCenter) <= PedalRadius * 1.35f)
                    {
                        padTouchId = touchId;
                        BeginPadTouch(position);
                        UpdatePadInput(position, ref axes);
                    }
                    else if (Vector2.Distance(position, BoostCenter) <= BoostRadius * 1.45f)
                    {
                        boostTouchId = touchId;
                        boost = true;
                    }

                    continue;
                }

                if (Vector2.Distance(position, JoystickCenter) <= JoystickRadius * 1.7f)
                {
                    joystickTouchId = touchId;
                    UpdateJoystickVector(position, ref axes);
                }
                else if (Vector2.Distance(position, BoostCenter) <= BoostRadius * 1.45f)
                {
                    boostTouchId = touchId;
                    boost = true;
                }
            }

            if (joystickTouchId < 0 && padTouchId < 0) axes = Vector2.zero;
            if (boostTouchId < 0) boost = false;

            return anyTouch;
        }

        private void PollMouse(ref Vector2 axes, ref bool boost, bool tiltActive)
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                mouseJoystickActive = false;
                mousePadActive = false;
                mouseBoostActive = false;
                return;
            }

            Vector2 position = mouse.position.ReadValue();
            bool pressed = mouse.leftButton.isPressed;

            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (HandleUiTap(position))
                {
                    // UI buttons consume the press so the stick does not jump.
                }
                else if (tiltActive && Vector2.Distance(position, JoystickCenter) <= PedalRadius * 1.35f)
                {
                    mousePadActive = true;
                    BeginPadTouch(position);
                    UpdatePadInput(position, ref axes);
                }
                else if (!tiltActive && Vector2.Distance(position, JoystickCenter) <= JoystickRadius * 1.7f)
                {
                    mouseJoystickActive = true;
                }
                else if (Vector2.Distance(position, BoostCenter) <= BoostRadius * 1.45f)
                {
                    mouseBoostActive = true;
                }
            }

            if (mouseJoystickActive)
            {
                if (pressed) UpdateJoystickVector(position, ref axes);
                else
                {
                    mouseJoystickActive = false;
                    axes = Vector2.zero;
                }
            }

            if (mousePadActive)
            {
                if (pressed)
                {
                    UpdatePadInput(position, ref axes);
                }
                else
                {
                    mousePadActive = false;
                    ResetPadState();
                    axes = Vector2.zero;
                }
            }

            if (mouseBoostActive)
            {
                boost = pressed;
                if (!pressed) mouseBoostActive = false;
            }
        }

        private void UpdateJoystickVector(Vector2 position, ref Vector2 axes)
        {
            axes = Vector2.ClampMagnitude((position - JoystickCenter) / JoystickRadius, 1f);
        }

        private void BeginPadTouch(Vector2 position)
        {
            padTouchStart = position;
            padDragging = false;
            padHoldTime = 0f;
            pedalHeld = false;
        }

        private void ResetPadState()
        {
            padDragging = false;
            padHoldTime = 0f;
            pedalHeld = false;
        }

        /// <summary>
        /// Tilt mode uses the left pad as a vertical throttle stick. Moving it
        /// up accelerates, moving it down brakes and reverses. A press that
        /// stays put keeps the original PEDAL full-power action, and once the
        /// finger is released the automatic pedal takes over again. Horizontal
        /// movement is ignored so the stick never fights the phone steering.
        /// </summary>
        private void UpdatePadInput(Vector2 position, ref Vector2 axes)
        {
            float vertical = Mathf.Clamp((position - JoystickCenter).y / JoystickRadius, -1f, 1f);
            float moved = Vector2.Distance(position, padTouchStart) / LayoutScale;

            if (!padDragging && (moved > PadDragThreshold || Mathf.Abs(vertical) > PadCenterBand))
                padDragging = true;

            if (padDragging)
            {
                axes = new Vector2(0f, Mathf.Abs(vertical) > ThrottleDeadZone ? vertical : 0f);
                pedalHeld = false;
            }
            else
            {
                axes = Vector2.zero;
                padHoldTime += Time.deltaTime;
                pedalHeld = padHoldTime >= PadHoldDelay;
            }
        }

        /// <summary>
        /// Returns true when the tap landed on one of the steering-mode buttons.
        /// </summary>
        private bool HandleUiTap(Vector2 screenPoint)
        {
            if (ModeButtonRect.Contains(screenPoint))
            {
                if (tiltSteering != null && tiltSteering.IsAvailable)
                    tiltSteering.ToggleMode();

                return true;
            }

            if (tiltSteering != null && tiltSteering.IsTiltActive)
            {
                if (CalibrateButtonRect.Contains(screenPoint))
                {
                    tiltSteering.BeginCalibration();
                    return true;
                }

                if (InvertButtonRect.Contains(screenPoint))
                {
                    tiltSteering.ToggleInvert();
                    return true;
                }
            }

            return false;
        }

        private void ResolveReferences()
        {
            if (GameManager.Instance != null)
                inputManager = GameManager.Instance.GetInputManager;

            if (biker == null)
                biker = Object.FindAnyObjectByType<Biker>();

            if (bike == null)
                bike = Object.FindAnyObjectByType<SimpleBike>();

            if (tiltSteering == null)
                tiltSteering = GetComponent<TiltSteering>();

            if (tiltSteering == null)
                tiltSteering = Object.FindAnyObjectByType<TiltSteering>();
        }

        private void OnDisable()
        {
            if (inputManager != null)
                inputManager.ClearMobileInput();

            joystickTouchId = -1;
            padTouchId = -1;
            boostTouchId = -1;
            uiTouchId = -1;
            joystickVector = Vector2.zero;
            pedalHeld = false;
            ResetPadState();
            boostHeld = false;
            tiltActiveThisFrame = false;
            mouseJoystickActive = false;
            mousePadActive = false;
            mouseBoostActive = false;
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) OnDisable();
        }

        private void OnGUI()
        {
            if (!showControls || Event.current.type != EventType.Repaint) return;

            EnsureGuiResources();

            float scale = LayoutScale;
            Vector2 joystickCenter = ToGui(JoystickCenter);
            Vector2 boostCenter = ToGui(BoostCenter);

            bool tiltAvailable = tiltSteering != null && tiltSteering.IsAvailable;
            bool tiltActive = tiltSteering != null && tiltSteering.IsTiltActive;

            if (tiltActive && tiltSteering != null)
            {
                // In tilt mode the left pad is a vertical throttle stick:
                // push up to accelerate, pull down to brake/reverse. Steering
                // still comes only from the phone, so the knob only follows
                // the vertical axis and can never suggest a second steering
                // method.
                bool padActive = pedalHeld || padDragging;
                Color padColor = padActive
                    ? new Color(0.35f, 0.85f, 0.55f, 0.80f)
                    : new Color(0.82f, 0.95f, 0.85f, 0.28f);

                DrawCircle(joystickCenter, PedalRadius * 1.25f, new Color(0.01f, 0.04f, 0.03f, 0.34f));
                DrawCircle(joystickCenter, PedalRadius, padColor);

                Vector2 padKnob = joystickCenter +
                                  new Vector2(0f, -joystickVector.y) * PedalRadius * 0.58f;
                DrawCircle(padKnob, PedalRadius * 0.34f, new Color(0.92f, 1f, 0.9f, 0.72f));

                string padLabel = padDragging
                    ? "THROTTLE"
                    : (pedalHeld ? "PEDAL" : (tiltSteering.AutoPedal ? "PEDAL (AUTO)" : "PEDAL"));
                DrawLabel(new Rect(joystickCenter.x - PedalRadius,
                        joystickCenter.y + PedalRadius * 0.9f,
                        PedalRadius * 2f, 36f * scale),
                    padLabel, centerLabelStyle);
            }
            else
            {
                DrawCircle(joystickCenter, JoystickRadius * 1.55f, new Color(0.01f, 0.04f, 0.03f, 0.32f));
                DrawCircle(joystickCenter, JoystickRadius * 1.12f, new Color(0.82f, 0.95f, 0.85f, 0.18f));

                Vector2 knob = joystickCenter +
                                new Vector2(0f, -joystickVector.y) * JoystickRadius * 0.62f;
                DrawCircle(knob, JoystickRadius * 0.46f, new Color(0.92f, 1f, 0.9f, 0.72f));
                DrawLabel(new Rect(joystickCenter.x - JoystickRadius, joystickCenter.y - 16f * scale,
                        JoystickRadius * 2f, 32f * scale), "RIDE", centerLabelStyle);
            }

            bool accelerating = boostHeld;
            DrawCircle(boostCenter, BoostRadius * 1.14f, new Color(0.01f, 0.04f, 0.03f, 0.36f));
            DrawCircle(boostCenter, BoostRadius * 0.9f,
                accelerating ? new Color(0.95f, 0.55f, 0.08f, 0.82f) : new Color(0.95f, 0.82f, 0.35f, 0.52f));
            DrawLabel(new Rect(boostCenter.x - BoostRadius, boostCenter.y - 16f * scale,
                    BoostRadius * 2f, 32f * scale), "BOOST", centerLabelStyle);

            Rect safe = SafeArea;

            Rect modeRect = ToGui(ModeButtonRect);
            DrawPill(modeRect, tiltActive
                ? new Color(0.12f, 0.42f, 0.30f, 0.78f)
                : new Color(0.02f, 0.05f, 0.04f, 0.55f));
            DrawLabel(modeRect, !tiltAvailable
                ? "TILT: NO SENSOR"
                : (tiltActive ? "TILT: ON" : "TILT: OFF"), centerLabelStyle);

            if (tiltActive)
            {
                Rect calibrateRect = ToGui(CalibrateButtonRect);
                DrawPill(calibrateRect, new Color(0.02f, 0.05f, 0.04f, 0.55f));
                DrawLabel(calibrateRect, "CAL", centerLabelStyle);

                Rect invertRect = ToGui(InvertButtonRect);
                DrawPill(invertRect, new Color(0.02f, 0.05f, 0.04f, 0.55f));
                DrawLabel(invertRect, tiltSteering.Invert ? "INV *" : "INV", centerLabelStyle);

                Rect barRect = ToGui(TiltBarRect);
                DrawPill(barRect, new Color(0.02f, 0.05f, 0.04f, 0.5f));

                float axis = Mathf.Clamp(tiltSteering.SteerAxis, -1f, 1f);
                float half = barRect.width * 0.5f;
                Rect fillRect = axis >= 0f
                    ? new Rect(barRect.center.x, barRect.y + 3f * scale, half * axis, barRect.height - 6f * scale)
                    : new Rect(barRect.center.x + half * axis, barRect.y + 3f * scale,
                        -half * axis, barRect.height - 6f * scale);

                if (fillRect.width > 1f)
                    DrawPill(fillRect, new Color(0.55f, 0.95f, 0.75f, 0.9f));

                DrawPill(new Rect(barRect.center.x - 1.5f * scale, barRect.y,
                    3f * scale, barRect.height), new Color(1f, 1f, 1f, 0.85f));
            }

            Rect hintRect = new Rect(safe.x, Screen.height - safe.y - 44f * scale,
                safe.width, 36f * scale);
            DrawLabel(hintRect, tiltActive
                    ? "Tilt to steer  |  pad up/down = ride/brake  |  hold pad = PEDAL  |  BOOST  |  CAL/INV"
                    : "Drag left stick up/down to ride/brake  |  BOOST for speed  |  tap TILT to use the phone",
                hintStyle);

            if (showTiltDebug && tiltActive && tiltSteering != null)
                DrawTiltDebug(safe, scale);
        }

        /// <summary>
        /// Temporary diagnostics requested for on-device steering verification.
        /// Shows the raw sensor vector, calibrated tilt and the steering values
        /// that actually reach the bicycle controller.
        /// </summary>
        private void DrawTiltDebug(Rect safe, float scale)
        {
            float width = Mathf.Min(safe.width - 32f * scale, 640f * scale);
            float height = 92f * scale;
            Rect panel = new Rect(safe.x + 16f * scale, safe.y + 16f * scale, width, height);

            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.58f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = previous;

            Vector3 sensor = tiltSteering.SensorAcceleration;
            float finalAxis = tiltSteering.SteerAxis;
            float inputAxis = inputManager != null ? inputManager.Horizontal : 0f;
            float bikeAngle = bike != null ? bike.CurrentSteerAngleDegrees : 0f;
            float speed = bike != null ? bike.CurrentSpeedKmh : 0f;

            string text = string.Format(
                "sensor ({0:0.00}, {1:0.00}, {2:0.00})   raw {3:0.0}°   cal {4:0.0}°\n" +
                "steer final {5:+0.00;-0.00;0.00}   INPUT h {6:+0.00;-0.00;0.00}   wheel {7:0.0}°\n" +
                "speed {8:0.0} km/h   rider {9}",
                sensor.x, sensor.y, sensor.z,
                tiltSteering.RawTiltDegrees,
                tiltSteering.CalibratedTiltDegrees,
                finalAxis,
                inputAxis,
                bikeAngle,
                speed,
                biker != null && biker.IsRiding ? "mounted" : "not mounted");

            GUI.Label(new Rect(panel.x + 12f * scale, panel.y + 6f * scale,
                panel.width - 24f * scale, panel.height - 12f * scale), text, debugStyle);
        }

        private void EnsureGuiResources()
        {
            if (circleTexture == null)
            {
                const int size = 128;
                circleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
                circleTexture.name = "BikeGame Circle";
                circleTexture.wrapMode = TextureWrapMode.Clamp;
                circleTexture.filterMode = FilterMode.Bilinear;
                circleTexture.hideFlags = HideFlags.DontSave;

                Color32[] pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float nx = ((x + 0.5f) / size) * 2f - 1f;
                        float ny = ((y + 0.5f) / size) * 2f - 1f;
                        float distance = Mathf.Sqrt(nx * nx + ny * ny);

                        // Soft edge. Mathf.SmoothStep() interpolates between two
                        // values, so the edge falloff is done explicitly here.
                        float alpha;

                        if (distance <= 0.86f)
                        {
                            alpha = 1f;
                        }
                        else if (distance >= 1f)
                        {
                            alpha = 0f;
                        }
                        else
                        {
                            float t = (distance - 0.86f) / 0.14f;
                            alpha = 1f - t * t * (3f - 2f * t);
                        }

                        pixels[y * size + x] = new Color32(255, 255, 255,
                            (byte)Mathf.Clamp(Mathf.RoundToInt(alpha * 255f), 0, 255));
                    }
                }

                circleTexture.SetPixels32(pixels);
                circleTexture.Apply();
            }

            if (centerLabelStyle == null)
            {
                centerLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = Mathf.RoundToInt(17f * LayoutScale),
                    fontStyle = FontStyle.Bold
                };
                centerLabelStyle.normal.textColor = Color.white;
            }

            if (hintStyle == null)
            {
                hintStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = Mathf.RoundToInt(18f * LayoutScale),
                    fontStyle = FontStyle.Bold
                };
                hintStyle.normal.textColor = new Color(1f, 1f, 1f, 0.9f);
            }

            if (debugStyle == null)
            {
                debugStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.UpperLeft,
                    fontSize = Mathf.RoundToInt(13f * LayoutScale),
                    fontStyle = FontStyle.Normal,
                    wordWrap = false
                };
                debugStyle.normal.textColor = new Color(0.75f, 1f, 0.85f, 0.95f);
            }
        }

        private Vector2 ToGui(Vector2 screenPoint)
        {
            return new Vector2(screenPoint.x, Screen.height - screenPoint.y);
        }

        private Rect ToGui(Rect screenRect)
        {
            return new Rect(screenRect.x, Screen.height - screenRect.yMax,
                screenRect.width, screenRect.height);
        }

        private void DrawPill(Rect guiRect, Color color)
        {
            if (guiRect.width <= 0f || guiRect.height <= 0f) return;

            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(guiRect, circleTexture, ScaleMode.StretchToFill, true);
            GUI.color = previous;
        }

        private void DrawCircle(Vector2 guiCenter, float radius, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(guiCenter.x - radius, guiCenter.y - radius, radius * 2f, radius * 2f),
                circleTexture, ScaleMode.StretchToFill, true);
            GUI.color = previous;
        }

        private void DrawLabel(Rect rect, string text, GUIStyle style)
        {
            Color previous = GUI.color;
            GUI.color = Color.white;
            GUI.Label(rect, text, style);
            GUI.color = previous;
        }

        private void OnDestroy()
        {
            if (circleTexture != null)
                Destroy(circleTexture);
        }
    }
}
