using UnityEngine;
using UnityEngine.InputSystem;
using InputSystemGyroscope = UnityEngine.InputSystem.Gyroscope;

[RequireComponent(typeof(Rigidbody))]
public class GyroCarController : MonoBehaviour
{
    [Header("Car Settings")]
    [Min(1f)] public float maxSpeed = 10f;
    [Min(0f)] public float acceleration = 5f;
    [Min(0f)] public float turnSpeed = 80f;
    [Min(1f)] public float driveResponseMultiplier = 2f;
    public float centerOfMassOffset = -0.5f;
    [Min(0f)] public float downForce = 10f;

    [Tooltip("How quickly the car slows down when no throttle is applied, in m/s².")]
    [Min(0f)] public float coastDeceleration = 2.5f;

    [Header("Gyroscope Settings")]
    public bool useGyroscope = true;
    [Min(0.1f)] public float gyroSteerSensitivity = 1f;
    [Min(0.1f)] public float gyroAccelSensitivity = 0.8f;
    [Range(0.1f, 1f)] public float steeringResponseMultiplier = 0.5f;
    [Min(0.1f)] public float steeringSmoothing = 5f;
    [Range(0f, 0.5f)] public float gyroDeadZone = 0.1f;
    [Range(5f, 60f)] public float tiltAngleForFullInput = 35f;

    [Header("Calibration")]
    public bool calibrateOnStart = true;

    [Tooltip("Invert steering if tilting left turns right.")]
    public bool invertSteering = false;

    [Tooltip("Invert acceleration if tilting forward gives reverse.")]
    public bool invertAcceleration = false;

    [Header("Debug")]
    public bool showDebugOverlay = true;
    public bool logInput = true;
    [Range(0.1f, 5f)] public float logInterval = 1f;

    [Header("Orientation")]
    public ScreenOrientation lockedOrientation = ScreenOrientation.LandscapeLeft; // or LandscapeRight
    private bool calibrating;

    private Rigidbody rb;
    private bool gyroEnabled;
    private bool calibrated;
    private AttitudeSensor attitudeSensor;
    private InputSystemGyroscope gyroscope;

    private Quaternion initialRotation;

    private float steerInput;
    private float accelerateInput;
    private float steeringVelocity;

    private Vector3 rawGyroAngles;
    private float pitch;
    private float roll;

    private string inputSource = "Initializing";
    private Quaternion rawGyroAttitude;
    private Vector3 rawRotationRate;
    
    private float lastLogTime;
    private bool movementDiagnosticLogged;


    void Awake()
    {
        Screen.orientation = lockedOrientation;
        // We map axes for landscape ourselves, so stop the Input System from rotating them.
        InputSystem.settings.compensateForScreenOrientation = false;
        
        rb = GetComponent<Rigidbody>();
        maxSpeed = Mathf.Max(maxSpeed, 1f);
        acceleration = Mathf.Max(acceleration, 0.1f);

        if (rb != null)
        {
            rb.centerOfMass = new Vector3(0f, centerOfMassOffset, 0f);

            // Keep the car upright while allowing yaw steering around the Y axis.
            rb.constraints = RigidbodyConstraints.FreezeRotationX |
                             RigidbodyConstraints.FreezeRotationZ;

            if (rb.isKinematic)
            {
                Debug.LogWarning("[GyroCarController] Rigidbody was kinematic. Switching it to dynamic for car physics.");
                rb.isKinematic = false;
            }
        }
    }

    void Start()
    {
        InputSystem.onDeviceChange += OnDeviceChange;
        InitializeGyroscope();
        
        lastLogTime = Time.time;
    }
    
    private void InitializeGyroscope()
    {
        attitudeSensor = AttitudeSensor.current;
        gyroscope = InputSystemGyroscope.current;

        if (!useGyroscope || attitudeSensor == null)
        {
            gyroEnabled = false;
            inputSource = "Keyboard";

            Debug.LogWarning(
                $"[GyroCarController] Attitude sensor unavailable or disabled. " +
                $"useGyroscope={useGyroscope}, AttitudeSensor.current={(attitudeSensor != null ? "available" : "null")}. " +
                "Using keyboard controls."
            );
            return;
        }

        InputSystem.EnableDevice(attitudeSensor);
        gyroEnabled = attitudeSensor.enabled;
        inputSource = gyroEnabled ? "AttitudeSensor" : "Keyboard";

        // Calibrate with delay to let sensor stabilize
        if (gyroEnabled && calibrateOnStart)
        {
            CalibrateGyroscope();
        }

        Debug.Log(
            $"[GyroCarController] AttitudeSensor initialized. Enabled: {gyroEnabled}, " +
            $"Gyroscope fallback: {(gyroscope != null ? "available" : "unavailable")}"
        );
    }
    
    // Called by AndroidPermissionRequester when permission is granted
    public void OnPermissionGranted()
    {
        Debug.Log("[GyroCarController] Permission callback received - checking Input System sensors");
        InitializeGyroscope();
    }

    private void OnDeviceChange(InputDevice device, InputDeviceChange change)
{
    if (device is AttitudeSensor && !gyroEnabled &&
        (change == InputDeviceChange.Added || change == InputDeviceChange.Reconnected))
    {
        InitializeGyroscope();
    }
}

    void OnDestroy()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
    }
    
    void OnApplicationFocus(bool hasFocus)
    {
        // Re-enable gyro when app regains focus (Android lifecycle)
        if (hasFocus && gyroEnabled && attitudeSensor != null)
        {
            InputSystem.EnableDevice(attitudeSensor);
            Debug.Log("[GyroCarController] App focused - AttitudeSensor re-enabled");
        }
    }
    
    void OnApplicationPause(bool isPaused)
    {
        // Handle app pause/resume (Android)
        if (!isPaused && gyroEnabled && attitudeSensor != null)
        {
            InputSystem.EnableDevice(attitudeSensor);
            Debug.Log("[GyroCarController] App resumed - AttitudeSensor re-enabled");
        }
    }

    void Update()
    {
        // Process input every frame for responsiveness
        if (gyroEnabled && attitudeSensor != null && attitudeSensor.enabled)
        {
            if (!calibrated && !calibrating)
                CalibrateGyroscope();

            // Read raw sensor data
            rawGyroAttitude = attitudeSensor.attitude.ReadValue();
            rawRotationRate = gyroscope != null
                ? gyroscope.angularVelocity.ReadValue()
                : Vector3.zero;
            
            // Check if sensor is actually providing data
            if (rawGyroAttitude.x == 0 && rawGyroAttitude.y == 0 && 
                rawGyroAttitude.z == 0 && rawGyroAttitude.w == 0)
            {
                // Sensor not providing data - try re-enabling
                InputSystem.EnableDevice(attitudeSensor);
            }
            
            // Filter only gyro steering so hand movement does not cause abrupt turns.
            GetGyroInput(out float targetSteer, out accelerateInput);
            steerInput = Mathf.SmoothDamp(
                steerInput,
                targetSteer,
                ref steeringVelocity,
                1f / steeringSmoothing
            );
        }
        else
        {
            if (gyroEnabled && (attitudeSensor == null || !attitudeSensor.enabled))
            {
                gyroEnabled = false;
                inputSource = "Keyboard";
                Debug.LogWarning("[GyroCarController] AttitudeSensor is unavailable or disabled. Falling back to keyboard.");
            }

            GetKeyboardInput(out steerInput, out accelerateInput);
            steeringVelocity = 0f;
        }

        // Controlled logging (always log when sensor seems stuck at zero)
        if (logInput && Time.time - lastLogTime >= logInterval)
        {
            lastLogTime = Time.time;
            
            Debug.Log(
                $"[GyroCarController] Source: {inputSource} | " +
                $"Raw Quat: ({rawGyroAttitude.x:F3}, {rawGyroAttitude.y:F3}, {rawGyroAttitude.z:F3}, {rawGyroAttitude.w:F3}) | " +
                $"Calibrated: {calibrated} | " +
                $"Pitch: {pitch:F1}° | Roll: {roll:F1}° | " +
                $"Steer: {steerInput:F2} | Accel: {accelerateInput:F2} | " +
                $"Speed: {(rb != null ? rb.linearVelocity.magnitude.ToString("F1") : "N/A")}"
            );
        }
    }

    void FixedUpdate()
    {
        if (rb == null)
            return;

        ApplyMovement(steerInput, accelerateInput);

        // Apply downforce only while moving.
        if (downForce > 0f)
        {
            rb.AddForce(
                -transform.up * downForce * rb.linearVelocity.magnitude,
                ForceMode.Force
            );
        }
    }

    public void CalibrateGyroscope()
{
    if (calibrating) return;
    if (!useGyroscope) return;

    attitudeSensor = AttitudeSensor.current;
    if (attitudeSensor == null)
    {
        gyroEnabled = false;
        inputSource = "Keyboard";
        return;
    }

    InputSystem.EnableDevice(attitudeSensor);
    gyroEnabled = attitudeSensor.enabled;
    StartCoroutine(CalibrateAfterDelay());
}

private System.Collections.IEnumerator CalibrateAfterDelay()
{
    calibrating = true;
    yield return new WaitForSeconds(0.5f);   // let the orientation lock and sensor settle
    initialRotation = GetDeviceRotation();
    calibrated = true;
    calibrating = false;
    Debug.Log($"[GyroCarController] Calibrated. Neutral: {initialRotation}");
}
    // Public method to manually recalibrate (can be called from UI button)
    public void ForceRecalibrate()
    {
        calibrated = false;
        CalibrateGyroscope();
        Debug.Log("[GyroCarController] Manual recalibration triggered.");
    }

    Quaternion GetDeviceRotation()
    {
        if (attitudeSensor == null)
            return Quaternion.identity;

        Quaternion attitude = attitudeSensor.attitude.ReadValue();

        // Input System reports Android attitude in sensor coordinates. Convert it
        // to Unity coordinates while leaving screen-orientation compensation off.
        return new Quaternion(
            attitude.x,
            attitude.y,
            -attitude.z,
            -attitude.w
        );
    }

    void GetGyroInput(out float steer, out float accelerate)
    {
        steer = 0f;
        accelerate = 0f;

        if (!calibrated)
        {
            // Will auto-calibrate on next frame
            return;
        }

        // Check if attitude quaternion is actually working
        bool attitudeValid = !(rawGyroAttitude.x == 0 && rawGyroAttitude.y == 0 && 
                               rawGyroAttitude.z == 0 && rawGyroAttitude.w == 0);

        if (!attitudeValid)
        {
            // Fallback: Use rotation rate instead of attitude
            // This is less accurate but better than nothing
            roll = rawRotationRate.y * gyroSteerSensitivity * 30f; // Integrate rotation rate
            pitch = -rawRotationRate.x * gyroAccelSensitivity * 30f;
            
            steer = Mathf.Clamp(roll, -1f, 1f);
            accelerate = pitch > -20f ? 1f : pitch < -20f ? -1f : 0f;
            
            if (Time.frameCount % 120 == 0) // Log occasionally
            {
                Debug.LogWarning($"[GyroCarController] Using rotation rate fallback. Attitude stuck at zero.");
            }
        }
        else
        {
            // Normal attitude-based processing
            Quaternion currentRotation = GetDeviceRotation();

            // Rotation relative to the calibrated neutral pose, expressed in the
            // device's own axes. The Input System already compensates those axes for
            // screen orientation, so we must NOT rotate them again here.
            Quaternion relativeRotation =
                Quaternion.Inverse(initialRotation) * currentRotation;

            // Steering: rotation about the screen normal (local Z). The follow camera
            // sits behind the car, so tilt-left is negated to steer left on screen.
            Vector3 rotatedRight = relativeRotation * Vector3.right;
            roll = Mathf.Atan2(rotatedRight.y, rotatedRight.x) * Mathf.Rad2Deg;

            // Acceleration: with compensation off, device axes remain portrait axes.
            // LandscapeLeft maps screen-up to device +X; LandscapeRight maps it to -X.
            Vector3 rotatedNormal = relativeRotation * Vector3.forward;
            float upSign = Screen.orientation == ScreenOrientation.LandscapeRight ? -1f : 1f;
            pitch = Mathf.Atan2(-upSign * rotatedNormal.x, rotatedNormal.z) * Mathf.Rad2Deg;

            rawGyroAngles = new Vector3(pitch, 0f, roll);

            // Map roll (left/right tilt) to steering. Negated because the camera is
            // behind the car, which mirrors the on-screen direction.
            steer = Mathf.Clamp(
                -roll / tiltAngleForFullInput * gyroSteerSensitivity * steeringResponseMultiplier,
                -1f,
                1f
            );

            // Use the observed pitch threshold as a simple forward/reverse selector.
            accelerate = pitch > -20f ? 1f : pitch < -20f ? -1f : 0f;
        }

        // Apply optional inversions
        if (invertSteering)
            steer = -steer;

        if (invertAcceleration)
            accelerate = -accelerate;

        // Apply dead zones to reduce jitter
        if (Mathf.Abs(steer) < gyroDeadZone)
            steer = 0f;

        if (Mathf.Abs(accelerate) < gyroDeadZone)
            accelerate = 0f;
    }

    void GetKeyboardInput(
        out float steer,
        out float accelerate)
    {
        steer = 0f;
        accelerate = 0f;

        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
            return;

        if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed || keyboard.qKey.isPressed)
            steer -= 1f;

        if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed)
            steer += 1f;

        if (keyboard.upArrowKey.isPressed || keyboard.wKey.isPressed || keyboard.zKey.isPressed)
            accelerate += 1f;

        if (keyboard.downArrowKey.isPressed || keyboard.sKey.isPressed)
            accelerate -= 1f;
    }

    void ApplyMovement(float steer, float accelerate)
    {
        Vector3 driveDirection = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (driveDirection.sqrMagnitude < 0.001f)
            driveDirection = transform.forward;
        else
            driveDirection.Normalize();

        // Speed along the car's forward direction.
        float currentSpeed = Vector3.Dot(
            rb.linearVelocity,
            driveDirection
        );

        if (Mathf.Abs(accelerate) > 0.001f)
        {
            float nextSpeed = currentSpeed +
                              accelerate * acceleration * driveResponseMultiplier * Time.fixedDeltaTime;
            nextSpeed = accelerate > 0f
                ? Mathf.Min(nextSpeed, maxSpeed)
                : Mathf.Max(nextSpeed, -maxSpeed * 0.5f);

            // Preserve lateral and vertical Rigidbody velocity while setting the
            // longitudinal speed commanded by the input. Setting linearVelocity is
            // enough; also calling MovePosition would move the car twice per step.
            Vector3 nonDriveVelocity = rb.linearVelocity - driveDirection * currentSpeed;
            Vector3 nextVelocity = nonDriveVelocity + driveDirection * nextSpeed;
            rb.linearVelocity = nextVelocity;

            LogMovementDiagnostic(driveDirection, accelerate);
        }
        else if (coastDeceleration > 0f)
        {
            // Ease off smoothly when the throttle is released instead of letting
            // the car roll forever.
            float nextSpeed = Mathf.MoveTowards(
                currentSpeed,
                0f,
                coastDeceleration * Time.fixedDeltaTime);

            if (!Mathf.Approximately(nextSpeed, currentSpeed))
            {
                Vector3 nonDriveVelocity = rb.linearVelocity - driveDirection * currentSpeed;
                rb.linearVelocity = nonDriveVelocity + driveDirection * nextSpeed;
            }
        }

        // Steering is allowed even when stationary, so it is easier
        // to test whether steering input is being received.
        if (Mathf.Abs(steer) > 0.001f)
        {
            float direction = currentSpeed < -0.1f ? -1f : 1f;

            // Reduce steering authority as speed rises so the car stays stable
            // instead of spinning out at the new top speed.
            float speedRatio = Mathf.Clamp01(
                Mathf.Abs(currentSpeed) / Mathf.Max(maxSpeed, 0.01f));
            float highSpeedSteering = Mathf.Lerp(1f, 0.7f, speedRatio);

            float turnAmount =
                steer * turnSpeed * highSpeedSteering * direction * Time.fixedDeltaTime;

            Quaternion turnRotation = Quaternion.Euler(
                0f,
                turnAmount,
                0f
            );

            rb.MoveRotation(rb.rotation * turnRotation);
        }
    }

    private void LogMovementDiagnostic(Vector3 driveDirection, float accelerate)
    {
        if (movementDiagnosticLogged)
            return;

        movementDiagnosticLogged = true;
        Debug.Log(
            $"[GyroCarController] Movement speed applied. " +
            $"Accel: {accelerate:F2}, Direction: {driveDirection}, " +
            $"IsKinematic: {rb.isKinematic}, Constraints: {rb.constraints}, " +
            $"Mass: {rb.mass:F1}"
        );
    }

    void OnGUI()
    {
        if (!showDebugOverlay)
            return;

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 18;
        style.normal.textColor = Color.yellow;
        style.normal.background = MakeTex(2, 2, new Color(0, 0, 0, 0.8f));
        style.padding = new RectOffset(10, 10, 5, 5);

        // Check if attitude is stuck at zero
        bool attitudeStuck = (rawGyroAttitude.x == 0 && rawGyroAttitude.y == 0 && 
                              rawGyroAttitude.z == 0 && rawGyroAttitude.w == 0);
        
        string statusIcon = attitudeStuck ? "⚠️" : "✓";
        Color statusColor = attitudeStuck ? Color.red : Color.green;

        string info =
            "=== GYRO CAR DEBUG ===\n" +
            $"Attitude Sensor Available: {attitudeSensor != null}\n" +
            $"Gyro Enabled: {gyroEnabled}\n" +
            $"Calibrated: {calibrated}\n" +
            $"Input Source: {inputSource}\n" +
            $"Sensor Status: {statusIcon} {(attitudeStuck ? "STUCK AT ZERO!" : "Working")}\n" +
            "---\n" +
            $"Raw Attitude: x={rawGyroAttitude.x:F3}, y={rawGyroAttitude.y:F3}, z={rawGyroAttitude.z:F3}, w={rawGyroAttitude.w:F3}\n" +
            $"Rotation Rate: x={rawRotationRate.x:F2}, y={rawRotationRate.y:F2}, z={rawRotationRate.z:F2}\n" +
            (calibrated ? $"Initial Rot: {initialRotation}\n" : "Waiting for calibration...\n") +
            "---\n" +
            $"Pitch (fwd/back): {pitch:F1}°\n" +
            $"Roll (left/right): {roll:F1}°\n" +
            "---\n" +
            $"Steering Input: {steerInput:F2}\n" +
            $"Acceleration Input: {accelerateInput:F2}\n" +
            "---\n" +
            $"Speed: {(rb != null ? rb.linearVelocity.magnitude.ToString("F2") : "N/A")} m/s\n" +
            $"Velocity: {(rb != null ? rb.linearVelocity.ToString("F1") : "N/A")}\n" +
            "---\n" +
            $"Frame: {Time.frameCount}";

        GUI.Label(
            new Rect(10f, 10f, 700f, 500f),
            info,
            style
        );
        
        // Add warning if sensor stuck
        if (attitudeStuck)
        {
            GUIStyle warningStyle = new GUIStyle(GUI.skin.label);
            warningStyle.fontSize = 24;
            warningStyle.normal.textColor = Color.red;
            warningStyle.fontStyle = FontStyle.Bold;
            warningStyle.normal.background = MakeTex(2, 2, new Color(0, 0, 0, 0.9f));
            warningStyle.padding = new RectOffset(20, 20, 10, 10);
            warningStyle.alignment = TextAnchor.MiddleCenter;
            
            GUI.Label(
                new Rect(10f, Screen.height - 100f, Screen.width - 20f, 80f),
                "⚠️ GYROSCOPE SENSOR NOT RESPONDING\nTry: Shake device, restart app, or check permissions",
                warningStyle
            );
        }
    }

    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++)
            pix[i] = col;

        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();

        return result;
    }

    void OnDrawGizmosSelected()
    {
        Rigidbody body = GetComponent<Rigidbody>();

        if (body == null)
            return;

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(
            transform.TransformPoint(body.centerOfMass),
            0.2f
        );

        Gizmos.color = Color.blue;
        Gizmos.DrawRay(
            transform.position,
            transform.forward * 3f
        );
    }
}
