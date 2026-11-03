using UnityEngine;

public sealed class Obstacle : MonoBehaviour
{
    public enum Kind { Rock, Log, Buoy, Debris }
    [SerializeField] Kind kind;
    [SerializeField] Transform boat;
    [SerializeField] float driftSpeed = 0.8f;
    Rigidbody body;

    public void Configure(Kind obstacleKind, Transform player, float drift)
    {
        kind = obstacleKind; boat = player; driftSpeed = drift; body = GetComponent<Rigidbody>();
    }

    void Awake()
    {
        if (body == null) body = GetComponent<Rigidbody>();
        if (boat == null) boat = FindFirstObjectByType<BoatController>()?.transform;
    }

    void FixedUpdate()
    {
        if (body != null && body.isKinematic) body.MovePosition(body.position + Vector3.back * (driftSpeed * Time.fixedDeltaTime));
        else transform.position += Vector3.back * (driftSpeed * Time.fixedDeltaTime);
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
