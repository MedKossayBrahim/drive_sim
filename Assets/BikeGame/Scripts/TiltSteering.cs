using UnityEngine;
using UnityEngine.InputSystem;

namespace BikeGame
{
    public enum SteeringMode
    {
        Touch = 0,
        Tilt = 1
    }

    /// <summary>
    /// Turns the phone accelerometer into the same normalized steering axis the
    /// touch stick produces. The value is consumed by MobileBikeControls and
    /// forwarded through InputManager.SetMobileInput, so the bike controller
    /// itself is untouched.
    ///
    /// Important Android/Input System detail: sensor devices start disabled.
    /// They must be enabled explicitly, otherwise Accelerometer.current exists
    /// but always reads zero. That was the reason tilt mode looked enabled but
    /// never produced steering.
    /// </summary>
    [DefaultExecutionOrder(-250)]
    public class TiltSteering : MonoBehaviour
    {
        public const string ModePreference = "BikeGame.SteeringMode";
        public const string InvertPreference = "BikeGame.TiltInvert";
        public const string InvertVersionPreference = "BikeGame.TiltInvertVersion";

        // Bumped once so installations that saved Touch in an older build start
        // in tilt mode after this update. A later manual TILT press is remembered.
        private const string ModeVersionPreference = "BikeGame.SteeringModeVersion";
        private const int ModeVersion = 2;

        // Bumped when the tilt axis mapping itself changes. Version 1 cleared the
        // stale inversion left by the old double-rotated landscape axis. Version 2
        // flips the default roll-to-steer sign so tilting left steers left.
        private const int InvertVersion = 2;

        [Header("Mode")]
        [SerializeField] private bool allowTilt = true;
        [SerializeField] private SteeringMode mode = SteeringMode.Touch;

        [Header("Calibration")]
        [Tooltip("Neutral roll in degrees. The player can recalibrate at runtime.")]
        [SerializeField] private float neutralTiltDegrees;
        [Tooltip("Roll away from neutral (in degrees) that is a full steering input.")]
        [SerializeField] private float fullSteerTiltDegrees = 18f;
        [Tooltip("Roll around neutral (in degrees) that is ignored.")]
        [SerializeField] private float deadZoneDegrees = 2.5f;
        [Tooltip("Steering multiplier. Values above 1 reach full lock with less tilt.")]
        [SerializeField] private float sensitivity = 1.2f;
        [SerializeField] private bool invert;
        [Tooltip("Optional extra rotation of the sensor axes. Usually 0.")]
        [SerializeField] private float sensorMountRotation;

        [Header("Wheelie (pitch)")]
        [Tooltip("Tilt back from neutral (in degrees) that is a full wheelie input.")]
        [SerializeField] private float fullWheelieTiltDegrees = 20f;
        [Tooltip("Tilt around neutral (in degrees) that is ignored before a wheelie starts.")]
        [SerializeField] private float wheelieDeadZoneDegrees = 4f;
        [Tooltip("Flip the wheelie direction if the device reports gravity the other way.")]
        [SerializeField] private bool invertWheelie;

        [Header("Smoothing")]
        [Tooltip("Higher is snappier, lower is smoother.")]
        [SerializeField] private float smoothing = 12f;

        [Header("Hands free (optional)")]
        [Tooltip("Pedal automatically in tilt mode while the pedal button is not touched.")]
        [SerializeField] private bool autoPedal = true;
        [SerializeField, Range(0f, 1f)] private float autoPedalAmount = 0.55f;

        private const int CalibrationFrames = 20;

        private float calibratedNeutralDegrees;
        private float smoothedAxis;
        private float rawTiltDegrees;
        private float calibratedTiltDegrees;

        private float calibratedNeutralPitchDegrees;
        private float smoothedWheelieAxis;
        private float rawPitchDegrees;
        private float calibratedPitchDegrees;

        private Vector3 lastAcceleration;
        private bool hasSensorSample;

        private bool calibrating;
        private float calibrationSum;
        private float pitchCalibrationSum;
        private int calibrationSamples;

        private bool hasSimulatedAcceleration;
        private Vector3 simulatedAcceleration;

        private bool hasOrientationOverride;
        private ScreenOrientation orientationOverride;

        private Accelerometer accelerometer;
        private GravitySensor gravitySensor;

        public SteeringMode Mode => mode;
        public float SteerAxis => smoothedAxis;
        public float RawTiltDegrees => rawTiltDegrees;
        public float CalibratedTiltDegrees => calibratedTiltDegrees;
        public float NeutralDegrees => calibratedNeutralDegrees;
        public float WheelieAxis => smoothedWheelieAxis;
        public float RawPitchDegrees => rawPitchDegrees;
        public float CalibratedPitchDegrees => calibratedPitchDegrees;
        public float NeutralPitchDegrees => calibratedNeutralPitchDegrees;
        public bool InvertWheelie => invertWheelie;
        public Vector3 SensorAcceleration => lastAcceleration;
        public bool HasSensorSample => hasSensorSample;
        public bool Invert => invert;
        public bool AutoPedal => autoPedal;
        public float AutoPedalAmount => autoPedalAmount;

        /// <summary>Configurable at runtime and exposed in the Inspector.</summary>
        public float Sensitivity
        {
            get => sensitivity;
            set => sensitivity = Mathf.Clamp(value, 0.1f, 4f);
        }

        /// <summary>Configurable at runtime and exposed in the Inspector.</summary>
        public float DeadZoneDegrees
        {
            get => deadZoneDegrees;
            set => deadZoneDegrees = Mathf.Clamp(value, 0f, 30f);
        }

        /// <summary>Configurable at runtime and exposed in the Inspector.</summary>
        public float FullSteerTiltDegrees
        {
            get => fullSteerTiltDegrees;
            set => fullSteerTiltDegrees = Mathf.Clamp(value, 1f, 60f);
        }

        /// <summary>Configurable at runtime and exposed in the Inspector.</summary>
        public float FullWheelieTiltDegrees
        {
            get => fullWheelieTiltDegrees;
            set => fullWheelieTiltDegrees = Mathf.Clamp(value, 1f, 60f);
        }

        /// <summary>Configurable at runtime and exposed in the Inspector.</summary>
        public float WheelieDeadZoneDegrees
        {
            get => wheelieDeadZoneDegrees;
            set => wheelieDeadZoneDegrees = Mathf.Clamp(value, 0f, 30f);
        }

        public bool IsAvailable =>
            allowTilt &&
            (hasSimulatedAcceleration ||
             (Application.isMobilePlatform &&
              (Accelerometer.current != null || GravitySensor.current != null)));

        public bool IsTiltActive => allowTilt && mode == SteeringMode.Tilt && IsAvailable;

        private void Awake()
        {
            calibratedNeutralDegrees = neutralTiltDegrees;

            EnableSensors();
            SelectStartupMode();

            invert = ResolveInvertPreference();

            // Calibrate both roll and pitch from the natural holding pose. The
            // wheelie works in touch mode too, so it also needs a pitch neutral.
            BeginCalibration();
        }

        /// <summary>
        /// Reads the saved INV state. Version 1 clears the value written by older
        /// builds, which flipped a landscape roll axis that is now corrected, so
        /// steering works on the first start without pressing INV.
        /// </summary>
        private bool ResolveInvertPreference()
        {
            bool savedInvert = PlayerPrefs.GetInt(InvertPreference, invert ? 1 : 0) == 1;

            if (PlayerPrefs.GetInt(InvertVersionPreference, 0) < InvertVersion)
            {
                savedInvert = invert;
                PlayerPrefs.SetInt(InvertPreference, savedInvert ? 1 : 0);
                PlayerPrefs.SetInt(InvertVersionPreference, InvertVersion);
                PlayerPrefs.Save();
            }

            return savedInvert;
        }

        private void OnEnable()
        {
            EnableSensors();
            InputSystem.onDeviceChange += OnDeviceChange;
        }

        private void OnDisable()
        {
            InputSystem.onDeviceChange -= OnDeviceChange;
        }

        private void SelectStartupMode()
        {
            int savedMode = PlayerPrefs.GetInt(ModePreference, -1);
            int savedVersion = PlayerPrefs.GetInt(ModeVersionPreference, 0);

            if (savedVersion < ModeVersion && Application.isMobilePlatform)
            {
                mode = SteeringMode.Tilt;
                PlayerPrefs.SetInt(ModePreference, (int)mode);
                PlayerPrefs.SetInt(ModeVersionPreference, ModeVersion);
                PlayerPrefs.Save();
                return;
            }

            if (savedMode == (int)SteeringMode.Touch || savedMode == (int)SteeringMode.Tilt)
                mode = (SteeringMode)savedMode;
            else if (Application.isMobilePlatform)
                mode = SteeringMode.Tilt;

            PlayerPrefs.SetInt(ModeVersionPreference, ModeVersion);
        }

        private void Update()
        {
            float decay = 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime);

            if (!IsAvailable)
            {
                // Fade the axes out instead of snapping, otherwise a mode switch
                // would yank the handlebars.
                smoothedAxis = Mathf.Lerp(smoothedAxis, 0f, decay);
                if (Mathf.Abs(smoothedAxis) < 0.001f) smoothedAxis = 0f;

                smoothedWheelieAxis = Mathf.Lerp(smoothedWheelieAxis, 0f, decay);
                if (smoothedWheelieAxis < 0.001f) smoothedWheelieAxis = 0f;
                return;
            }

            EnableSensors();

            Vector3 acceleration = ReadAcceleration();
            if (acceleration.sqrMagnitude < 0.0001f) return;

            lastAcceleration = acceleration;
            hasSensorSample = true;

            float tilt = ComputeTiltDegrees(acceleration);
            rawTiltDegrees = tilt;

            // Pitch is independent of roll, so leaning into a turn never adds a
            // wheelie input. It is read in touch mode as well: the wheelie is a
            // separate action, not part of the steering axis.
            float pitch = ComputePitchDegrees(acceleration);
            rawPitchDegrees = pitch;

            if (calibrating)
            {
                calibrationSum += tilt;
                pitchCalibrationSum += pitch;
                calibrationSamples++;

                if (calibrationSamples >= CalibrationFrames)
                {
                    calibratedNeutralDegrees = Mathf.DeltaAngle(0f, calibrationSum / calibrationSamples);
                    calibratedNeutralPitchDegrees = Mathf.DeltaAngle(0f, pitchCalibrationSum / calibrationSamples);
                    calibrating = false;
                }
            }

            UpdateWheelieAxis(pitch);

            if (!IsTiltActive)
            {
                // Touch mode keeps steering neutral exactly as before.
                smoothedAxis = Mathf.Lerp(smoothedAxis, 0f, decay);
                if (Mathf.Abs(smoothedAxis) < 0.001f) smoothedAxis = 0f;
                return;
            }

            float delta = Mathf.DeltaAngle(calibratedNeutralDegrees, tilt);
            calibratedTiltDegrees = delta;

            float dead = Mathf.Max(0f, deadZoneDegrees);
            float full = Mathf.Max(dead + 0.5f,
                Mathf.Max(1f, fullSteerTiltDegrees) / Mathf.Max(0.05f, sensitivity));

            float magnitude = Mathf.Abs(delta);
            float target = 0f;

            if (magnitude > dead)
            {
                target = Mathf.Clamp01((magnitude - dead) / (full - dead));
                if (delta < 0f) target = -target;
            }

            // Unity's positive WheelCollider steer angle turns the bike right,
            // while a left roll of the phone produces a negative tilt here. The
            // sensor roll axis therefore has to be mirrored so "tilt left" sends
            // a left steer input to SimpleBike. INV still flips this if needed.
            target = -target;

            if (invert) target = -target;
            target = Mathf.Clamp(target, -1f, 1f);

            float blend = 1f - Mathf.Exp(-Mathf.Max(0.1f, smoothing) * Time.unscaledDeltaTime);
            smoothedAxis = Mathf.Lerp(smoothedAxis, target, blend);
            if (Mathf.Abs(smoothedAxis) < 0.0005f) smoothedAxis = 0f;
        }

        /// <summary>
        /// Turns the calibrated pitch into a 0..1 wheelie input. Only a backward
        /// tilt (the top of the phone moving away from the player) lifts the
        /// wheel; tilting forward just lets it come back down.
        /// </summary>
        private void UpdateWheelieAxis(float pitch)
        {
            if (calibrating)
            {
                smoothedWheelieAxis = 0f;
                calibratedPitchDegrees = 0f;
                return;
            }

            float delta = Mathf.DeltaAngle(calibratedNeutralPitchDegrees, pitch);
            calibratedPitchDegrees = delta;

            float dead = Mathf.Max(0f, wheelieDeadZoneDegrees);
            float full = Mathf.Max(dead + 0.5f, Mathf.Max(1f, fullWheelieTiltDegrees));

            float target = 0f;
            float magnitude = Mathf.Abs(delta);

            if (magnitude > dead)
            {
                target = Mathf.Clamp01((magnitude - dead) / (full - dead));
                if (delta < 0f) target = -target;
            }

            if (invertWheelie) target = -target;
            target = Mathf.Clamp01(target);

            float blend = 1f - Mathf.Exp(-Mathf.Max(0.1f, smoothing) * Time.unscaledDeltaTime);
            smoothedWheelieAxis = Mathf.Lerp(smoothedWheelieAxis, target, blend);
            if (Mathf.Abs(smoothedWheelieAxis) < 0.0005f) smoothedWheelieAxis = 0f;
        }

        /// <summary>
        /// Device gravity (pointing down) expressed in screen space, where X is
        /// screen-right and Y is screen-up.
        ///
        /// The Input System already rotates sensor values into screen space by
        /// default (InputSettings.compensateForScreenOrientation). Rotating them
        /// a second time here used to turn landscape roll into a different axis.
        /// The fallback keeps the old mapping for projects that disable that
        /// compensation.
        /// </summary>
        private float ComputeTiltDegrees(Vector3 acceleration)
        {
            Vector3 gravity = Vector3.Normalize(acceleration);
            bool compensated = InputSystem.settings != null &&
                               InputSystem.settings.compensateForScreenOrientation;

            Vector2 screen = compensated
                ? new Vector2(gravity.x, gravity.y)
                : ToScreenSpace(gravity);

            if (Mathf.Abs(sensorMountRotation) > 1f)
            {
                float radians = -sensorMountRotation * Mathf.Deg2Rad;
                float cos = Mathf.Cos(radians);
                float sin = Mathf.Sin(radians);
                screen = new Vector2(screen.x * cos - screen.y * sin,
                                     screen.x * sin + screen.y * cos);
            }

            // Upright in landscape: gravity points down the screen (-Y). Rolling
            // the phone so the screen-right edge drops moves gravity towards +X.
            return Mathf.Atan2(screen.x, -screen.y) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Pitch of the device relative to an upright hold, in degrees. Positive
        /// means the top of the phone is tilted away from the player, the same
        /// motion as pulling the handlebars up. Only the gravity component along
        /// the screen normal and the length of the in-screen component are used,
        /// so roll and screen rotation cannot change the value.
        /// </summary>
        private static float ComputePitchDegrees(Vector3 acceleration)
        {
            Vector3 gravity = Vector3.Normalize(acceleration);
            float inScreen = Mathf.Sqrt(gravity.x * gravity.x + gravity.y * gravity.y);
            return Mathf.Atan2(-gravity.z, inScreen) * Mathf.Rad2Deg;
        }

        private Vector2 ToScreenSpace(Vector3 gravity)
        {
            ScreenOrientation orientation = hasOrientationOverride
                ? orientationOverride
                : Screen.orientation;

            if (orientation == ScreenOrientation.AutoRotation)
                orientation = Screen.width > Screen.height
                    ? ScreenOrientation.LandscapeLeft
                    : ScreenOrientation.Portrait;

            switch (orientation)
            {
                case ScreenOrientation.LandscapeLeft:
                    return new Vector2(gravity.y, -gravity.x);

                case ScreenOrientation.LandscapeRight:
                    return new Vector2(-gravity.y, gravity.x);

                case ScreenOrientation.PortraitUpsideDown:
                    return new Vector2(-gravity.x, -gravity.y);

                default:
                    return new Vector2(gravity.x, gravity.y);
            }
        }

        private Vector3 ReadAcceleration()
        {
            if (hasSimulatedAcceleration)
                return simulatedAcceleration;

            // The gravity sensor is less noisy than the raw accelerometer while
            // the bike is moving, but not every Android device exposes it.
            if (gravitySensor != null && gravitySensor.enabled)
            {
                Vector3 gravity = gravitySensor.gravity.ReadValue();
                if (gravity.sqrMagnitude > 0.0001f)
                    return gravity;
            }

            if (accelerometer != null && accelerometer.enabled)
                return accelerometer.acceleration.ReadValue();

            return Vector3.zero;
        }

        private void EnableSensors()
        {
            accelerometer = Accelerometer.current;
            if (accelerometer != null && !accelerometer.enabled)
                InputSystem.EnableDevice(accelerometer);

            gravitySensor = GravitySensor.current;
            if (gravitySensor != null && !gravitySensor.enabled)
                InputSystem.EnableDevice(gravitySensor);
        }

        private void OnDeviceChange(InputDevice device, InputDeviceChange change)
        {
            if (change == InputDeviceChange.Added ||
                change == InputDeviceChange.Reconnected ||
                change == InputDeviceChange.Enabled)
            {
                EnableSensors();
            }
        }

        public void SetMode(SteeringMode newMode)
        {
            if (!allowTilt && newMode == SteeringMode.Tilt) return;

            mode = newMode;
            PlayerPrefs.SetInt(ModePreference, (int)mode);
            PlayerPrefs.SetInt(ModeVersionPreference, ModeVersion);
            PlayerPrefs.Save();

            if (mode == SteeringMode.Tilt)
            {
                EnableSensors();
                BeginCalibration();
            }
        }

        public void ToggleMode()
        {
            SetMode(mode == SteeringMode.Tilt ? SteeringMode.Touch : SteeringMode.Tilt);
        }

        public void SetInvert(bool value)
        {
            invert = value;
            PlayerPrefs.SetInt(InvertPreference, invert ? 1 : 0);
            PlayerPrefs.SetInt(InvertVersionPreference, InvertVersion);
            PlayerPrefs.Save();
        }

        public void ToggleInvert() => SetInvert(!invert);

        public void BeginCalibration()
        {
            calibrating = true;
            calibrationSum = 0f;
            pitchCalibrationSum = 0f;
            calibrationSamples = 0;
        }

        public void SetNeutralCalibration(float degrees)
        {
            calibratedNeutralDegrees = Mathf.DeltaAngle(0f, degrees);
            neutralTiltDegrees = calibratedNeutralDegrees;
            calibrating = false;
            calibratedTiltDegrees = 0f;
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                smoothedAxis = 0f;
                smoothedWheelieAxis = 0f;
                return;
            }

            EnableSensors();
            if (IsTiltActive) BeginCalibration();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) return;

            EnableSensors();
            if (IsTiltActive) BeginCalibration();
        }

        // ---- Editor / automated test hooks -------------------------------------

        /// <summary>
        /// Feeds a fake device acceleration in the same screen-space convention
        /// the Input System produces (X right, Y up, gravity down = -Y).
        /// </summary>
        public void SimulateAcceleration(Vector3 acceleration)
        {
            hasSimulatedAcceleration = true;
            simulatedAcceleration = acceleration;
        }

        public void ClearSimulatedAcceleration() => hasSimulatedAcceleration = false;

        public void SetDebugOrientation(ScreenOrientation orientation)
        {
            hasOrientationOverride = true;
            orientationOverride = orientation;
        }

        public void ClearDebugOrientation() => hasOrientationOverride = false;
    }
}
