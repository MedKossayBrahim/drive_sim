using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Simple gyro car controller.
///
/// Controls:
///   GAS pedal   -> accelerate forward (in D) or backward (in R)
///   BRAKE pedal -> slow down and stop
///   Gyroscope   -> steer left / right only (never throttle)
///   Lever       -> R / N / D automatic transmission
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class GyroCarController : MonoBehaviour
{
    public enum TransmissionGear
    {
        Reverse,
        Neutral,
        Drive
    }

    [Header("Driving")]
    [Min(1f)] public float maxSpeed = 10f;
    [Min(0f)] public float acceleration = 15f;
    [Min(0f)] public float brakeDeceleration = 16f;
    [Min(0f)] public float coastDeceleration = 3f;
    [Min(0f)] public float turnSpeed = 90f;
    [Range(0f, 1f)] public float highSpeedSteering = 0.65f;
    public float centerOfMassOffset = -0.5f;

    [Tooltip("Acceleration multiplier by current speed. The curve starts strong and eases off near the speed limit.")]
    public AnimationCurve accelerationBySpeed = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(0.65f, 0.55f),
        new Keyframe(1f, 0.12f));

    [Header("Gyroscope Steering")]
    public bool useGyroscope = true;

    [Tooltip("Tilt in degrees that gives full steering lock.")]
    [Min(5f)] public float fullSteerAngle = 30f;

    [Tooltip("Small tilts inside this angle do not steer the car at all.")]
    [Range(0f, 20f)] public float steerDeadZone = 6f;

    [Tooltip("Extra angle before steering re-engages, so it does not flicker at the edge.")]
    [Range(0f, 10f)] public float steerHysteresis = 2f;

    [Tooltip("Higher values make small tilts gentler while full tilt still gives full steering.")]
    [Range(1f, 3f)] public float steerExponent = 1.4f;

    [Min(0.1f)] public float steerSmoothing = 8f;

    [Tooltip("Invert steering if tilting left turns right.")]
    public bool invertSteering = false;

    [Header("Transmission")]
    public TransmissionGear selectedGear = TransmissionGear.Drive;
    [Min(0f)] public float gearChangeStopSpeed = 0.3f;

    [Header("Calibration")]
    public bool calibrateOnStart = true;
    public ScreenOrientation lockedOrientation = ScreenOrientation.LandscapeLeft;

    private Rigidbody rb;

    private AttitudeSensor attitudeSensor;
    private bool gyroReady;
    private bool calibrated;
    private bool calibrating;
    private Quaternion neutralRotation;

    // Pedals (set by DriveControlsUI) and keyboard fallback.
    private float pedalGas;
    private float pedalBrake;
    private float keyboardGas;
    private float keyboardBrake;

    private float steerInput;
    private float steerVelocity;
    private bool insideSteerDeadZone = true;

    public TransmissionGear CurrentGear => selectedGear;
    public float CurrentSpeed => rb != null ? rb.linearVelocity.magnitude : 0f;
    public bool HasGyroscope => gyroReady;

    // ------------------------------------------------------------------
    // Setup
    // ------------------------------------------------------------------

    private void Awake()
    {
        Screen.orientation = lockedOrientation;

        // We handle landscape axes ourselves in ReadDeviceRotation().
        InputSystem.settings.compensateForScreenOrientation = false;

        rb = GetComponent<Rigidbody>();

        // The car felt too slow, so run everything 3x faster. The demo scene
        // still stores the original slower values, so scale them at runtime.
        maxSpeed *= 3f;
        acceleration = Mathf.Max(acceleration, 15f) * 3f;

        rb.centerOfMass = new Vector3(0f, centerOfMassOffset, 0f);
        rb.constraints = RigidbodyConstraints.FreezeRotationX |
                         RigidbodyConstraints.FreezeRotationZ;

        if (rb.isKinematic)
            rb.isKinematic = false;
    }

    private void Start()
    {
        InputSystem.onDeviceChange += OnDeviceChange;
        InitializeGyroscope();
    }

    private void OnDestroy()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
    }

    private void InitializeGyroscope()
    {
        attitudeSensor = AttitudeSensor.current;

        gyroReady = useGyroscope && attitudeSensor != null;

        if (!gyroReady)
        {
            Debug.LogWarning("[GyroCarController] Gyroscope unavailable. Use A / D or arrow keys to steer.");
            return;
        }

        InputSystem.EnableDevice(attitudeSensor);
        gyroReady = attitudeSensor.enabled;

        if (gyroReady && calibrateOnStart)
            Recalibrate();
    }

    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
    {
        if (device is AttitudeSensor &&
            (change == InputDeviceChange.Added || change == InputDeviceChange.Reconnected))
        {
            InitializeGyroscope();
        }
    }

    // Called by AndroidPermissionRequester once sensor permission is granted.
    public void OnPermissionGranted()
    {
        InitializeGyroscope();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && attitudeSensor != null && useGyroscope)
        {
            InputSystem.EnableDevice(attitudeSensor);
            gyroReady = attitudeSensor.enabled;
        }
    }

    private void OnApplicationPause(bool paused)
    {
        if (!paused && attitudeSensor != null && useGyroscope)
        {
            InputSystem.EnableDevice(attitudeSensor);
            gyroReady = attitudeSensor.enabled;
        }
    }

    // ------------------------------------------------------------------
    // Public control API (used by DriveControlsUI)
    // ------------------------------------------------------------------

    public void SetPedalInput(float gas, float brake)
    {
        pedalGas = Mathf.Clamp01(gas);
        pedalBrake = Mathf.Clamp01(brake);
    }

    public void ClearPedalInput()
    {
        pedalGas = 0f;
        pedalBrake = 0f;
    }

    public bool SetReverse() => TrySetGear(TransmissionGear.Reverse);
    public bool SetNeutral() => TrySetGear(TransmissionGear.Neutral);
    public bool SetDrive() => TrySetGear(TransmissionGear.Drive);

    public void ForceRecalibrate() => Recalibrate();

    private bool TrySetGear(TransmissionGear gear)
    {
        if (gear == selectedGear)
            return true;

        if (CurrentSpeed > gearChangeStopSpeed)
        {
            Debug.Log("[GyroCarController] Stop the car before changing gear.");
            return false;
        }

        selectedGear = gear;
        Debug.Log($"[GyroCarController] Gear: {selectedGear}");
        return true;
    }

    // ------------------------------------------------------------------
    // Per-frame input
    // ------------------------------------------------------------------

    private void Update()
    {
        ReadKeyboardInput();
        ReadGearKeys();

        if (gyroReady && attitudeSensor != null && attitudeSensor.enabled &&
            !calibrated && !calibrating)
        {
            Recalibrate();
        }

        steerInput = ComputeSteering();
    }

    private void ReadKeyboardInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            keyboardGas = 0f;
            keyboardBrake = 0f;
            return;
        }

        keyboardGas =
            keyboard.upArrowKey.isPressed ||
            keyboard.wKey.isPressed ||
            keyboard.zKey.isPressed ? 1f : 0f;

        keyboardBrake =
            keyboard.downArrowKey.isPressed ||
            keyboard.sKey.isPressed ||
            keyboard.spaceKey.isPressed ? 1f : 0f;
    }

    private void ReadGearKeys()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        // 1 = R, 2 = N, 3 = D. The letter D is left for steering.
        if (keyboard.digit1Key.wasPressedThisFrame || keyboard.rKey.wasPressedThisFrame)
            SetReverse();
        else if (keyboard.digit2Key.wasPressedThisFrame || keyboard.nKey.wasPressedThisFrame)
            SetNeutral();
        else if (keyboard.digit3Key.wasPressedThisFrame)
            SetDrive();
    }

    // ------------------------------------------------------------------
    // Steering
    // ------------------------------------------------------------------

    private float ComputeSteering()
    {
        float target;

        if (gyroReady && attitudeSensor != null && attitudeSensor.enabled)
        {
            target = GetGyroSteering();
        }
        else
        {
            target = GetKeyboardSteering();
            insideSteerDeadZone = Mathf.Abs(target) < 0.01f;
            steerVelocity = 0f;
        }

        return Mathf.SmoothDamp(
            steerInput,
            target,
            ref steerVelocity,
            1f / steerSmoothing,
            Mathf.Infinity,
            Time.deltaTime);
    }

    private float GetKeyboardSteering()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return 0f;

        float steer = 0f;

        if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed || keyboard.qKey.isPressed)
            steer -= 1f;

        if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed)
            steer += 1f;

        return invertSteering ? -steer : steer;
    }

    private float GetGyroSteering()
    {
        if (!calibrated)
            return 0f;

        Quaternion current = ReadDeviceRotation();
        Quaternion relative = Quaternion.Inverse(neutralRotation) * current;

        // Tilt around the screen normal = left / right roll.
        Vector3 right = relative * Vector3.right;
        float roll = Mathf.Atan2(right.y, right.x) * Mathf.Rad2Deg;

        return RollToSteering(roll);
    }

    /// <summary>
    /// Turns a left/right tilt into steering. Small tilts stay inside a safe
    /// gap and do not affect the car at all.
    /// </summary>
    private float RollToSteering(float rollDegrees)
    {
        float magnitude = Mathf.Abs(rollDegrees);
        float enterGap = Mathf.Max(0f, steerDeadZone);
        float exitGap = enterGap + Mathf.Max(0f, steerHysteresis);

        if (insideSteerDeadZone)
        {
            if (magnitude >= exitGap)
                insideSteerDeadZone = false;
        }
        else if (magnitude <= enterGap)
        {
            insideSteerDeadZone = true;
        }

        if (insideSteerDeadZone)
            return 0f;

        float usableRange = Mathf.Max(0.01f, fullSteerAngle - enterGap);
        float amount = Mathf.Clamp01((magnitude - enterGap) / usableRange);
        amount = Mathf.Pow(amount, Mathf.Max(1f, steerExponent));

        float steer = -Mathf.Sign(rollDegrees) * amount;

        return invertSteering ? -steer : steer;
    }

    private Quaternion ReadDeviceRotation()
    {
        if (attitudeSensor == null)
            return Quaternion.identity;

        Quaternion attitude = attitudeSensor.attitude.ReadValue();

        // Convert Android sensor axes to Unity axes.
        return new Quaternion(attitude.x, attitude.y, -attitude.z, -attitude.w);
    }

    // ------------------------------------------------------------------
    // Calibration
    // ------------------------------------------------------------------

    private void Recalibrate()
    {
        if (calibrating || !gyroReady)
            return;

        StartCoroutine(CalibrationRoutine());
    }

    private IEnumerator CalibrationRoutine()
    {
        calibrating = true;
        calibrated = false;

        // Let the device settle before capturing the neutral pose.
        yield return new WaitForSeconds(0.5f);

        neutralRotation = ReadDeviceRotation();
        calibrated = true;
        calibrating = false;
        insideSteerDeadZone = true;

        Debug.Log($"[GyroCarController] Gyroscope calibrated: {neutralRotation.eulerAngles}");
    }

    // ------------------------------------------------------------------
    // Movement
    // ------------------------------------------------------------------

    private void FixedUpdate()
    {
        if (rb == null)
            return;

        float gas = Mathf.Max(pedalGas, keyboardGas);
        float brake = Mathf.Max(pedalBrake, keyboardBrake);

        ApplyMovement(gas, brake);
    }

    private void ApplyMovement(float gas, float brake)
    {
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.001f)
            forward = transform.forward;
        forward.Normalize();

        // Current speed along the car's forward axis.
        float speed = Vector3.Dot(rb.linearVelocity, forward);

        if (brake > 0.001f)
        {
            // Brake always slows toward zero; it never reverses the car.
            speed = Mathf.MoveTowards(
                speed, 0f, brakeDeceleration * brake * Time.fixedDeltaTime);

            Vector3 lateral = rb.linearVelocity - forward * Vector3.Dot(rb.linearVelocity, forward);
            rb.linearVelocity = lateral + forward * speed;
        }
        else
        {
            float throttle = GetGearThrottle(gas);

            if (Mathf.Abs(throttle) > 0.001f)
            {
                // Use a real force instead of writing linearVelocity directly.
                // Writing a tiny velocity every physics step is cancelled by
                // tyre friction and the car never pulls away.
                float speedLimit = throttle < 0f ? maxSpeed * 0.5f : maxSpeed;
                float speedRatio = Mathf.Clamp01(Mathf.Abs(speed) / speedLimit);
                float accelerationFactor = accelerationBySpeed == null
                    ? 1f
                    : Mathf.Max(0f, accelerationBySpeed.Evaluate(speedRatio));

                rb.AddForce(
                    forward * (throttle * acceleration * accelerationFactor),
                    ForceMode.Acceleration);
                ClampSpeed(forward);
            }
            else if (coastDeceleration > 0f)
            {
                speed = Mathf.MoveTowards(
                    speed, 0f, coastDeceleration * Time.fixedDeltaTime);

                Vector3 lateral = rb.linearVelocity - forward * Vector3.Dot(rb.linearVelocity, forward);
                rb.linearVelocity = lateral + forward * speed;
            }
        }

        ApplySteering(speed);
    }

    private void ClampSpeed(Vector3 forward)
    {
        float speed = Vector3.Dot(rb.linearVelocity, forward);
        float limited = Mathf.Clamp(speed, -maxSpeed * 0.5f, maxSpeed);

        if (Mathf.Approximately(speed, limited))
            return;

        Vector3 lateral = rb.linearVelocity - forward * speed;
        rb.linearVelocity = lateral + forward * limited;
    }

    /// <summary>Gas in Drive is forward, gas in Reverse is backward, Neutral is idle.</summary>
    private float GetGearThrottle(float gas)
    {
        gas = Mathf.Clamp01(gas);

        switch (selectedGear)
        {
            case TransmissionGear.Drive:
                return gas;
            case TransmissionGear.Reverse:
                return -gas;
            default:
                return 0f;
        }
    }

    private void ApplySteering(float speed)
    {
        if (Mathf.Abs(steerInput) < 0.001f)
            return;

        // Do not spin the car while it is standing still.
        if (Mathf.Abs(speed) < 0.05f)
            return;

        // Reverse steering direction while backing up.
        float direction = speed < 0f ? -1f : 1f;

        // Less steering at high speed keeps the car stable.
        float speedRatio = Mathf.Clamp01(Mathf.Abs(speed) / maxSpeed);
        float authority = Mathf.Lerp(1f, highSpeedSteering, speedRatio);

        float turn = steerInput * turnSpeed * authority * direction * Time.fixedDeltaTime;
        rb.MoveRotation(rb.rotation * Quaternion.Euler(0f, turn, 0f));
    }
}
