using UnityEngine;
using System.Linq;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class BoatController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] float forwardSpeed = 7f;
    [SerializeField] float steerSpeed = 5f;
    [SerializeField] float horizontalLimit = 5.2f;
    [Header("Accelerometer")]
    [SerializeField] float deadZone = 0.08f;
    [SerializeField] float smoothing = 8f;
    [SerializeField] float calibrationSeconds = 0.35f;
    [Header("Boat feel")]
    [SerializeField] float bobHeight = 0.08f;
    [SerializeField] float bobFrequency = 2f;
    [SerializeField] float rollAngle = 12f;
    Rigidbody body;
    Vector3 startPosition;
    Quaternion startRotation;
    float calibratedX, steer, calibrationTimer, calibrationSum;
    int calibrationSamples;
    bool calibrating, controlsEnabled = true;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.useGravity = false;
        body.constraints = RigidbodyConstraints.FreezeRotation;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        GetComponent<Collider>().isTrigger = true;
        startPosition = transform.position;
        startRotation = transform.rotation;
        EnableAccelerometer();
    }

    void Update()
    {
        if (!controlsEnabled) return;
        float raw = ReadKeyboard();
        if (Mathf.Approximately(raw, 0f) && HasAccelerometer()) raw = Mathf.Clamp(ReadAccelerationX() - calibratedX, -1f, 1f);
        if (calibrating && HasAccelerometer())
        {
            calibrationTimer += Time.unscaledDeltaTime;
            calibrationSum += ReadAccelerationX();
            calibrationSamples++;
            if (calibrationTimer >= calibrationSeconds)
            {
                if (calibrationSamples > 0) calibratedX = calibrationSum / calibrationSamples;
                calibrating = false;
            }
        }
        if (Mathf.Abs(raw) < deadZone) raw = 0f;
        else raw = Mathf.Sign(raw) * Mathf.InverseLerp(deadZone, 1f, Mathf.Abs(raw));
        steer = Mathf.Lerp(steer, raw, 1f - Mathf.Exp(-smoothing * Time.deltaTime));
        float bob = Mathf.Sin(Time.time * bobFrequency) * bobHeight;
        transform.localPosition = new Vector3(transform.localPosition.x, startPosition.y + bob, transform.localPosition.z);
        transform.localRotation = startRotation * Quaternion.Euler(0f, 0f, -steer * rollAngle);
    }

    void FixedUpdate()
    {
        if (!controlsEnabled) return;
        Vector3 p = body.position + Vector3.forward * (forwardSpeed * Time.fixedDeltaTime) + Vector3.right * (steer * steerSpeed * Time.fixedDeltaTime);
        p.x = Mathf.Clamp(p.x, -horizontalLimit, horizontalLimit);
        body.MovePosition(p);
    }

    public void Calibrate()
    {
        if (!HasAccelerometer()) return;
        EnableAccelerometer();
        calibrationTimer = calibrationSum = 0f;
        calibrationSamples = 0;
        calibrating = true;
    }

    static System.Type FindInputType(string name) => System.Type.GetType(name + ", Unity.InputSystem");
    static object CurrentDevice(string typeName)
    {
        var type = FindInputType(typeName);
        return type == null ? null : type.GetProperty("current", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)?.GetValue(null);
    }

    float ReadAccelerationX()
    {
        var device = CurrentDevice("UnityEngine.InputSystem.Accelerometer");
        if (device != null)
        {
            try
            {
                var control = device.GetType().GetProperty("acceleration")?.GetValue(device);
                var read = control?.GetType().GetMethods().FirstOrDefault(m => m.Name == "ReadValue" && m.IsGenericMethod && m.GetParameters().Length == 0);
                if (read != null) return ((Vector3)read.MakeGenericMethod(typeof(Vector3)).Invoke(control, null)).x;
            }
            catch { }
        }
        return Input.acceleration.x;
    }

    bool HasAccelerometer() => CurrentDevice("UnityEngine.InputSystem.Accelerometer") != null || SystemInfo.supportsAccelerometer;

    void EnableAccelerometer()
    {
        var device = CurrentDevice("UnityEngine.InputSystem.Accelerometer");
        var inputSystem = FindInputType("UnityEngine.InputSystem.InputSystem");
        if (device == null || inputSystem == null) return;
        var enable = inputSystem.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static).FirstOrDefault(m => m.Name == "EnableDevice" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType.IsInstanceOfType(device));
        if (enable != null) enable.Invoke(null, new[] { device });
    }

    float ReadKeyboard()
    {
        var keyboard = CurrentDevice("UnityEngine.InputSystem.Keyboard");
        if (keyboard != null)
        {
            float value = 0f;
            if (Pressed(keyboard, "aKey") || Pressed(keyboard, "leftArrowKey")) value -= 1f;
            if (Pressed(keyboard, "dKey") || Pressed(keyboard, "rightArrowKey")) value += 1f;
            return value;
        }
        return (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? -1f : 0f) + (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1f : 0f);
    }

    static bool Pressed(object device, string controlName)
    {
        var control = device.GetType().GetProperty(controlName)?.GetValue(device);
        return control != null && (bool)(control.GetType().GetProperty("isPressed")?.GetValue(control) ?? false);
    }

    public void SetControlsEnabled(bool enabled) => controlsEnabled = enabled;
    public void ResetBoat(Vector3 position)
    {
        body.position = position;
        body.linearVelocity = Vector3.zero;
        steer = 0f;
        transform.localRotation = startRotation;
    }
}



