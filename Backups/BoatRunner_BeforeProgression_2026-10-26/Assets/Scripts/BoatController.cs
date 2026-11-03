using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public sealed class BoatController : MonoBehaviour
{
    [Header("Speed")]
    [SerializeField] float cruiseSpeed = 8f;
    [SerializeField] float maxSpeed = 14f;
    [SerializeField] float brakeSpeed = 2.5f;
    [SerializeField] float acceleration = 5f;
    [SerializeField] float braking = 10f;
    [Header("Steering")]
    [SerializeField] float maxLateralSpeed = 7f;
    [SerializeField] float steeringResponse = 18f;
    [SerializeField] float turnAngle = 24f;
    [SerializeField] float horizontalLimit = 7f;
    [Header("Boat feel")]
    [SerializeField] float bobHeight = 0.045f;
    [SerializeField] float bobFrequency = 2.2f;
    [SerializeField] float rollAngle = 7f;
    Rigidbody body;
    Transform visuals;
    Vector3 initialPosition;
    Quaternion initialRotation;
    Vector3 visualsBasePosition;
    float speed;
    float steering;
    float lateralSpeed;
    bool controlsEnabled = true;
    bool invulnerableFlash;
    public float CurrentSpeed => speed;
    public float Steering => steering;
    public float HorizontalLimit => horizontalLimit;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        GetComponent<Collider>().isTrigger = true;
        initialPosition = body.position;
        initialRotation = body.rotation;
        speed = cruiseSpeed;
        var visualTransform = transform.Find("BoatVisuals");
        if (visualTransform != null) { visuals = visualTransform; visualsBasePosition = visuals.localPosition; }
    }

    void Update()
    {
        if (controlsEnabled)
        {
            float targetSteering = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) targetSteering -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) targetSteering += 1f;
            steering = Mathf.MoveTowards(steering, targetSteering, steeringResponse * Time.deltaTime);
        }
        if (visuals != null)
        {
            float bob = Mathf.Sin(Time.time * bobFrequency) * bobHeight;
            visuals.localPosition = visualsBasePosition + Vector3.up * bob;
            visuals.localRotation = Quaternion.Euler(0f, 0f, -steering * rollAngle);
            if (invulnerableFlash) visuals.gameObject.SetActive(Mathf.FloorToInt(Time.time * 12f) % 2 == 0);
            else if (!visuals.gameObject.activeSelf) visuals.gameObject.SetActive(true);
        }
    }

    void FixedUpdate()
    {
        if (!controlsEnabled) return;
        bool accelerateInput = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
        bool brakeInput = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
        float targetSpeed = accelerateInput ? maxSpeed : cruiseSpeed;
        if (brakeInput) targetSpeed = brakeSpeed;
        speed = Mathf.MoveTowards(speed, targetSpeed, (brakeInput ? braking : acceleration) * Time.fixedDeltaTime);
        lateralSpeed = Mathf.MoveTowards(lateralSpeed, steering * maxLateralSpeed, steeringResponse * Time.fixedDeltaTime);
        Vector3 next = body.position + new Vector3(lateralSpeed, 0f, speed) * Time.fixedDeltaTime;
        next.x = Mathf.Clamp(next.x, -horizontalLimit, horizontalLimit);
        if (Mathf.Abs(next.x) >= horizontalLimit - 0.01f) lateralSpeed = 0f;
        body.MovePosition(next);
        Quaternion targetRotation = Quaternion.Euler(0f, -steering * turnAngle, 0f);
        body.MoveRotation(Quaternion.Slerp(body.rotation, targetRotation, 1f - Mathf.Exp(-7f * Time.fixedDeltaTime)));
    }

    public void SetControlsEnabled(bool enabled) => controlsEnabled = enabled;
    public void SetInvulnerableFlash(bool enabled) => invulnerableFlash = enabled;
    public void ResetBoat()
    {
        if (body == null) body = GetComponent<Rigidbody>();
        body.position = initialPosition;
        body.rotation = initialRotation;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        speed = cruiseSpeed; steering = 0f; lateralSpeed = 0f;
        controlsEnabled = true; invulnerableFlash = false;
        if (visuals != null) { visuals.localPosition = visualsBasePosition; visuals.localRotation = Quaternion.identity; visuals.gameObject.SetActive(true); }
    }
}
