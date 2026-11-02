using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

namespace Project.Plane
{
    public enum ControlMode { Keyboard, Touch, Tilt }

    /// <summary>Lit les entrées dans Update ; transmet les commandes au contrôleur sans appliquer de forces.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(PlaneController))]
    public sealed class PlaneInput : MonoBehaviour
    {
        [Serializable] public sealed class ModeEvent : UnityEvent<ControlMode> { }
        [Header("Actions et commandes tactiles")]
        [SerializeField] private InputActionAsset inputActions = null;
        [SerializeField] private VirtualJoystick joystick = null;
        [SerializeField] private ThrottleSlider throttleSlider = null;
        [SerializeField] private ControlMode mode = ControlMode.Keyboard;
        [SerializeField, Tooltip("Au démarrage mobile, remplace Keyboard par Tilt. Désactiver pour imposer Keyboard.")]
        private bool useMobileDefault = true;
        [SerializeField, Min(0f)] private float throttleChangeSpeed = 0.35f;
        [SerializeField, Min(0f), Tooltip("Réactivité exponentielle ; 0 = réponse immédiate.")]
        private float axisSmoothing = 8f;
        [Header("Joystick tactile")]
        [SerializeField, Min(0f)] private float touchAxisSmoothing = 14f;
        [SerializeField, Range(.5f, 2f)] private float touchSensitivity = 1.2f;
        [SerializeField, Range(.5f, 1.5f), Tooltip("Moins de 1 : réponse plus sensible aux petits déplacements du pouce.")]
        private float touchResponseExponent = .85f;

        [Header("Inclinaison")]
        [SerializeField] private bool useGyroscope = false;
        [SerializeField, Range(0f, 20f)] private float tiltDeadZoneDegrees = 3f;
        [SerializeField, Min(0.01f)] private float tiltSensitivity = 1f;
        [SerializeField, Range(5f, 85f)] private float maximumTiltDegrees = 35f;
        [SerializeField] private bool invertPitch = false;
        [SerializeField] private bool simulateTiltWithMouseInEditor = false;
        [SerializeField, Min(0f)] private float mouseDegreesPerPixel = 0.1f;

        [SerializeField, Min(0f)] private float tiltAxisSmoothing = 16f;
        [SerializeField, Tooltip("Sur téléphone, attendre Décoller avant de lire les commandes et de calibrer.")]
        private bool requireMobileTakeoffConfirmation = true;
        [Header("Événements configurables")]
        [SerializeField] private ModeEvent onModeChanged = new ModeEvent();
        [SerializeField] private UnityEvent onCalibrated = new UnityEvent();
        [SerializeField] private UnityEvent onCameraSwitch = new UnityEvent();
        [SerializeField] private UnityEvent onReset = new UnityEvent();
        [SerializeField] private UnityEvent onPauseChanged = new UnityEvent();
        private PlaneController controller;
        private Rigidbody body;
        private InputActionAsset runtimeActions;
        private InputActionMap map;
        private InputAction throttleAction, pitchRollAction, yawAction, flapsAction, gearAction, cameraAction, resetAction, pauseAction;
        private ControlMode appliedMode;
        private Vector2 smoothedAxes, neutralMouse;
        private Vector3 neutralGravity;
        private Vector3 calibratedPitchAxis, calibratedRollAxis;
        private Quaternion neutralAttitude;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;
        private ScreenOrientation calibratedOrientation;
        private string calibratedSource = "";
        private bool calibrated, calibrationRequested, resetRequested, paused;
        private bool previousGyroscope, previousMouseSimulation;
        private float throttleValue, previousTimeScale = 1f;
        public event Action<ControlMode> OnModeChanged;
        public event Action OnCalibrated;
        public event Action OnResetCompleted;
        public bool IsMissionStarted { get; private set; } = true;
        public bool PitchInverted => invertPitch;
        public void TogglePitchInversion() { invertPitch = !invertPitch; smoothedAxes = Vector2.zero; }
        public void BeginMission()
        {
            IsMissionStarted = true; throttleValue = 0f;
            if (throttleSlider != null) throttleSlider.SetValue(0f);
            Calibrate();
        }
        public ControlMode Mode => mode;
        public bool IsPaused => paused;
        public bool IsCalibrated => calibrated;
        public PlaneController Controller => controller;
        public string SensorStatus { get; private set; } = "Non utilisé";
        public bool SimulatingTilt => Application.isEditor && simulateTiltWithMouseInEditor;

        private void Awake()
        {
            controller = GetComponent<PlaneController>();
            body = GetComponent<Rigidbody>();
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
            throttleValue = controller.Throttle;
            IsMissionStarted = !(Application.isMobilePlatform && !Application.isEditor && requireMobileTakeoffConfirmation);
            if (Application.isMobilePlatform && useMobileDefault && mode == ControlMode.Keyboard) mode = ControlMode.Tilt;
        }
        private void OnEnable()
        {
            // Chaque avion possède ses actions : activer/désactiver ce script n'affecte ni l'UI ni un autre avion.
            if (inputActions != null)
            {
                runtimeActions = Instantiate(inputActions);
                map = runtimeActions.FindActionMap("Plane", true);
                throttleAction = map.FindAction("Throttle", true);
                pitchRollAction = map.FindAction("PitchRoll", true);
                yawAction = map.FindAction("Yaw", true);
                flapsAction = map.FindAction("FlapsToggle", true);
                gearAction = map.FindAction("GearToggle", true);
                cameraAction = map.FindAction("CameraSwitch", true);
                resetAction = map.FindAction("Reset", true);
                pauseAction = map.FindAction("Pause", true);
                map.Enable();
            }
            else Debug.LogWarning("[Sky Courier] PlaneInput : assigner PlaneInputActions ; clavier/manette indisponibles.", this);
            ApplyMode(mode);
        }
        public void SetMode(ControlMode value) { mode = value; ApplyMode(value); }
        private void ApplyMode(ControlMode value)
        {
            if (value == ControlMode.Tilt && !FindSensor(out _, out _, out _, out string source))
            {
                Debug.LogWarning("[Sky Courier] Aucun capteur d'inclinaison disponible : repli en Touch.", this);
                value = ControlMode.Touch;
                SensorStatus = "Capteur absent — Touch";
            }
            mode = appliedMode = value;
            controller.DirectTiltControl = value == ControlMode.Tilt;
            controller.DirectTouchControl = value == ControlMode.Touch;
            previousGyroscope = useGyroscope;
            previousMouseSimulation = simulateTiltWithMouseInEditor;
            smoothedAxes = Vector2.zero;
            controller.Pitch = controller.Roll = controller.Yaw = 0f;
            if (joystick != null) joystick.Release();
            if (throttleSlider != null) throttleSlider.SetValue(throttleValue);
            calibrated = false;
            calibrationRequested = value == ControlMode.Tilt;
            if (value != ControlMode.Tilt && !SensorStatus.StartsWith("Capteur absent")) SensorStatus = "Non utilisé";
            OnModeChanged?.Invoke(value);
            onModeChanged.Invoke(value);
        }

        private void Update()
        {
            if (mode != appliedMode || previousGyroscope != useGyroscope || previousMouseSimulation != simulateTiltWithMouseInEditor)
                ApplyMode(mode);
            // Les boutons restent disponibles pendant la pause ; aucune lecture de l'ancien Input Manager.
            if (pauseAction != null && pauseAction.WasPressedThisFrame()) TogglePause();
            if (resetAction != null && resetAction.WasPressedThisFrame()) ResetPlane();
            if (paused) return;
            if (!IsMissionStarted)
            { controller.Throttle = controller.Pitch = controller.Roll = controller.Yaw = 0f; return; }
            if (flapsAction != null && flapsAction.WasPressedThisFrame()) ToggleFlaps();
            if (gearAction != null && gearAction.WasPressedThisFrame()) ToggleGear();
            if (cameraAction != null && cameraAction.WasPressedThisFrame()) SwitchCamera();
            Vector2 axes = Vector2.zero;
            if (mode == ControlMode.Keyboard)
            {
                if (pitchRollAction != null) axes = pitchRollAction.ReadValue<Vector2>();
                if (throttleAction != null)
                    throttleValue = Mathf.Clamp01(throttleValue + throttleAction.ReadValue<float>() * throttleChangeSpeed * Time.deltaTime);
            }
            else
            {
                if (throttleSlider != null) throttleValue = throttleSlider.Value;
                axes = mode == ControlMode.Touch ? (joystick != null ? joystick.Value : Vector2.zero) : ReadTilt();
                if (mode == ControlMode.Touch)
                    axes = new Vector2(TouchAxis(axes.x), TouchAxis(axes.y));
            }
            float smoothing = mode == ControlMode.Tilt ? tiltAxisSmoothing : mode == ControlMode.Touch ? touchAxisSmoothing : axisSmoothing;
            float blend = smoothing <= 0f ? 1f : 1f - Mathf.Exp(-smoothing * Time.deltaTime);
            smoothedAxes = Vector2.Lerp(smoothedAxes, axes, blend);
            float targetYaw = mode != ControlMode.Tilt && yawAction != null ? yawAction.ReadValue<float>() : 0f;
            controller.Yaw = Mathf.Lerp(controller.Yaw, targetYaw, blend);
            controller.Roll = smoothedAxes.x;
            controller.Pitch = smoothedAxes.y;
            controller.Throttle = throttleValue;
        }

        private float TouchAxis(float value) => Mathf.Sign(value) * Mathf.Clamp01(
            Mathf.Pow(Mathf.Abs(value), touchResponseExponent) * touchSensitivity);

        private bool FindSensor(out GravitySensor gravity, out Accelerometer accelerometer, out AttitudeSensor attitude, out string source)
        {
            gravity = null; accelerometer = null; attitude = null;
            if (SimulatingTilt) { source = "Souris éditeur"; return Mouse.current != null; }
            if (useGyroscope && AttitudeSensor.current != null)
            {
                attitude = AttitudeSensor.current;
                if (!attitude.enabled) InputSystem.EnableDevice(attitude);
                source = "AttitudeSensor"; return true;
            }
            gravity = GravitySensor.current;
            if (gravity != null)
            {
                if (!gravity.enabled) InputSystem.EnableDevice(gravity);
                source = "GravitySensor"; return true;
            }
            accelerometer = Accelerometer.current;
            if (accelerometer != null)
            {
                if (!accelerometer.enabled) InputSystem.EnableDevice(accelerometer);
                source = "Accelerometer"; return true;
            }
            source = "Absent"; return false;
        }
        private static Quaternion ScreenRotation()
        {
            // Correction identique à CompensateDirection du package officiel.
            float z = Screen.orientation == ScreenOrientation.LandscapeLeft ? 90f :
                Screen.orientation == ScreenOrientation.LandscapeRight ? 270f :
                Screen.orientation == ScreenOrientation.PortraitUpsideDown ? 180f : 0f;
            return Quaternion.Euler(0f, 0f, z);
        }
        private Vector2 ReadTilt()
        {
            if (!FindSensor(out var gravity, out var accelerometer, out var attitude, out string source))
            { ApplyMode(ControlMode.Tilt); return Vector2.zero; }
            SensorStatus = source + (useGyroscope && attitude == null && !SimulatingTilt ? " (repli sans gyroscope)" : "");
            if (calibratedSource != source || calibratedOrientation != Screen.orientation) calibrationRequested = true;
            Vector3 direction = Vector3.zero;
            Quaternion rotation = Quaternion.identity;
            Vector2 mouse = Vector2.zero;
            if (SimulatingTilt) mouse = Mouse.current.position.ReadValue();
            else if (attitude != null)
            {
                // Conserve les conversions natives, notamment la reconstruction du W du quaternion Android.
                rotation = attitude.attitude.ReadValue();
                // CompensateRotation multiplie à droite par l'inverse de cette rotation d'écran.
                // Annuler seulement cette correction avant de traiter le mouvement relatif au neutre.
                if (InputSystem.settings.compensateForScreenOrientation) rotation *= ScreenRotation();
                float norm = rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w;
                if (norm < 0.5f) { SensorStatus += " — attente de mesure"; return Vector2.zero; }
                rotation = rotation.normalized;
                // Le repère natif d'attitude a Z vertical. Son inverse donne la gravité dans le téléphone.
                direction = ScreenRotation() * (Quaternion.Inverse(rotation) * Vector3.back);
            }
            else
            {
                // ReadValue conserve aussi le changement de signe et d'unité des capteurs Android.
                direction = gravity != null ? gravity.gravity.ReadValue() : accelerometer.acceleration.ReadValue();
                if (!InputSystem.settings.compensateForScreenOrientation) direction = ScreenRotation() * direction;
                if (direction.sqrMagnitude < 0.0001f) { SensorStatus += " — attente de mesure"; return Vector2.zero; }
                direction.Normalize();
            }
            if (calibrationRequested || !calibrated)
            {
                neutralMouse = mouse; neutralAttitude = rotation; neutralGravity = direction;
                // Adapte le roulis au neutre : téléphone à plat ou tenu presque verticalement.
                calibratedPitchAxis = Vector3.ProjectOnPlane(Vector3.right, neutralGravity).normalized;
                if (calibratedPitchAxis.sqrMagnitude < 0.001f) calibratedPitchAxis = Vector3.up;
                calibratedRollAxis = Vector3.Cross(calibratedPitchAxis, neutralGravity).normalized;
                calibratedSource = source; calibratedOrientation = Screen.orientation;
                calibrationRequested = false; calibrated = true;
                OnCalibrated?.Invoke(); onCalibrated.Invoke();
            }
            Vector2 angles;
            if (SimulatingTilt) angles = (mouse - neutralMouse) * mouseDegreesPerPixel;
            else
            {
                // L'inverse de la rotation du téléphone suit le même sens que le déplacement de la gravité.
                Quaternion delta = attitude != null ? Quaternion.Inverse(rotation) * neutralAttitude : Quaternion.FromToRotation(neutralGravity, direction);
                delta.ToAngleAxis(out float degrees, out Vector3 axis);
                if (degrees > 180f) degrees -= 360f;
                Vector3 rotationVector = Mathf.Abs(degrees) < 0.0001f ? Vector3.zero : axis * degrees;
                if (attitude != null) rotationVector = ScreenRotation() * rotationVector;
                // La gravité/attitude se déplace à l'inverse du téléphone : annule cette inversion sur les deux axes.
                angles = new Vector2(-Vector3.Dot(rotationVector, calibratedRollAxis), -Vector3.Dot(rotationVector, calibratedPitchAxis));
            }
            return new Vector2(NormalizeTilt(angles.x), NormalizeTilt(angles.y) * (invertPitch ? -1f : 1f));
        }
        public float NormalizeTilt(float degrees)
        {
            float dead = Mathf.Min(tiltDeadZoneDegrees, maximumTiltDegrees - 0.1f);
            return Mathf.Sign(degrees) * Mathf.Clamp01(Mathf.Max(0f, Mathf.Abs(degrees) - dead) /
                Mathf.Max(0.1f, maximumTiltDegrees - dead) * tiltSensitivity);
        }
        public void Calibrate()
        {
            calibrated = false; calibrationRequested = true;
            smoothedAxes = Vector2.zero;
            // La première mesure valide de Update définit le neutre et déclenche OnCalibrated.
        }
        public void ToggleFlaps() => controller.Flaps = controller.Flaps >= 0.5f ? 0f : 1f;
        public void ToggleGear() => controller.GearDeployed = !controller.GearDeployed;
        public void SwitchCamera() => onCameraSwitch.Invoke();
        public void TogglePause()
        {
            paused = !paused;
            if (paused) { previousTimeScale = Time.timeScale; Time.timeScale = 0f; }
            else Time.timeScale = previousTimeScale;
            onPauseChanged.Invoke();
        }
        public void ResetPlane()
        {
            if (paused) TogglePause();
            throttleValue = 0f; smoothedAxes = Vector2.zero;
            controller.Throttle = controller.Pitch = controller.Roll = controller.Yaw = 0f;
            if (joystick != null) joystick.Release();
            if (throttleSlider != null) throttleSlider.SetValue(0f);
            resetRequested = true;
        }
        private void FixedUpdate()
        {
            if (!resetRequested) return;
            resetRequested = false;
            body.position = spawnPosition; body.rotation = spawnRotation;
            body.linearVelocity = body.angularVelocity = Vector3.zero;
            body.WakeUp(); controller.ResetFlightState(); onReset.Invoke(); OnResetCompleted?.Invoke();
        }
        private void OnDisable()
        {
            if (controller != null) controller.DirectTouchControl = false;
            map?.Disable();
            if (runtimeActions != null) Destroy(runtimeActions);
            runtimeActions = null; map = null;
            if (controller != null) controller.Throttle = controller.Pitch = controller.Roll = controller.Yaw = 0f;
            if (paused) { Time.timeScale = previousTimeScale; paused = false; }
        }
#if UNITY_EDITOR
        public void Configure(InputActionAsset actions, VirtualJoystick stick = null, ThrottleSlider slider = null)
        { inputActions = actions; joystick = stick; throttleSlider = slider; }
#endif
    }
}
