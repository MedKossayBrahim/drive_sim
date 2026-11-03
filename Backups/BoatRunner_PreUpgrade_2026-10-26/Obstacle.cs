using UnityEngine;

public class Obstacle : MonoBehaviour
{
    public enum Kind { Rock, Rubble, Buoy }
    [SerializeField] Kind kind;
    [SerializeField] float driftSpeed = 0f;
    public void Configure(Kind obstacleKind, float drift) { kind = obstacleKind; driftSpeed = drift; }
    void Update()
    {
        transform.position += Vector3.back * (driftSpeed * Time.deltaTime);
        if (Camera.main != null && transform.position.z < Camera.main.transform.position.z - 20f) Destroy(gameObject);
    }
    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        var game = FindFirstObjectByType<BoatGameManager>();
        if (game != null) game.HitObstacle();
    }
}

