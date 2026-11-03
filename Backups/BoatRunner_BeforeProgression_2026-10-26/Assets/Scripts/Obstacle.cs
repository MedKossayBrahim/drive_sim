using UnityEngine;

public sealed class Obstacle : MonoBehaviour
{
    public enum Kind { Buoy, Driftwood, Seaweed, Debris }

    [SerializeField] Kind kind;
    [SerializeField] Transform boat;
    [SerializeField] float driftSpeed = 0.8f;
    [SerializeField] float bobAmplitude = 0.05f;
    [SerializeField] float bobFrequency = 1.25f;
    [SerializeField] float yawAmount = 8f;
    [SerializeField] float tiltAmount = 3f;

    Rigidbody body;
    float waterlineY;
    float phase;
    Quaternion startingRotation;

    public void Configure(Kind obstacleKind, Transform player, float drift)
    {
        kind = obstacleKind;
        boat = player;
        driftSpeed = drift;
        body = GetComponent<Rigidbody>();
        waterlineY = transform.position.y;
        startingRotation = transform.rotation;
        phase = Random.value * Mathf.PI * 2f;
        switch (kind)
        {
            case Kind.Buoy: bobAmplitude = 0.055f; yawAmount = 5f; tiltAmount = 2f; break;
            case Kind.Driftwood: bobAmplitude = 0.04f; yawAmount = 12f; tiltAmount = 4f; break;
            case Kind.Seaweed: bobAmplitude = 0.025f; yawAmount = 8f; tiltAmount = 2f; break;
            case Kind.Debris: bobAmplitude = 0.045f; yawAmount = 10f; tiltAmount = 4f; break;
        }
    }

    void Awake()
    {
        if (body == null) body = GetComponent<Rigidbody>();
        if (boat == null) boat = FindFirstObjectByType<BoatController>()?.transform;
        waterlineY = transform.position.y;
        startingRotation = transform.rotation;
        phase = Random.value * Mathf.PI * 2f;
    }

    void FixedUpdate()
    {
        if (body != null && body.isKinematic)
        {
            float time = Time.time + phase;
            Vector3 position = body.position;
            position.z -= driftSpeed * Time.fixedDeltaTime;
            position.y = waterlineY + Mathf.Sin(time * bobFrequency) * bobAmplitude;
            body.MovePosition(position);

            float yaw = Mathf.Sin(time * 0.7f) * yawAmount;
            float pitch = Mathf.Sin(time * 0.9f + 1f) * tiltAmount;
            float roll = Mathf.Cos(time * 0.8f + 0.5f) * tiltAmount;
            body.MoveRotation(startingRotation * Quaternion.Euler(pitch, yaw, roll));
        }
        if (boat != null && transform.position.z < boat.position.z - 22f) Destroy(gameObject);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("Player")) FindFirstObjectByType<BoatGameManager>()?.HitObstacle();
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) FindFirstObjectByType<BoatGameManager>()?.HitObstacle();
    }
}