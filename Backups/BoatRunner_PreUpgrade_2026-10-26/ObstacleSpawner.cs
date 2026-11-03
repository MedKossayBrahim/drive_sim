using UnityEngine;

public class ObstacleSpawner : MonoBehaviour
{
    [SerializeField] Transform boat;
    [SerializeField] float startInterval = 1.5f;
    [SerializeField] float minInterval = 0.55f;
    [SerializeField] float spawnAhead = 24f;
    [SerializeField] float laneWidth = 4.8f;
    [SerializeField] float obstacleSpeed = 0.6f;
    float timer;
    float elapsed;
    public void SetBoat(Transform value) => boat = value;
    void Update()
    {
        if (boat == null) return;
        elapsed += Time.deltaTime;
        timer += Time.deltaTime;
        float interval = Mathf.Lerp(startInterval, minInterval, Mathf.Clamp01(elapsed / 100f));
        obstacleSpeed = 0.6f + elapsed * 0.025f;
        if (timer >= interval) { timer = 0f; Spawn(); }
    }
    void Spawn()
    {
        var kind = (Obstacle.Kind)Random.Range(0, 3);
        var shape = kind == Obstacle.Kind.Buoy ? PrimitiveType.Sphere : (kind == Obstacle.Kind.Rubble ? PrimitiveType.Cube : PrimitiveType.Sphere);
        var obj = GameObject.CreatePrimitive(shape);
        obj.name = kind.ToString();
        float x = Random.Range(-laneWidth, laneWidth);
        obj.transform.position = new Vector3(x, kind == Obstacle.Kind.Buoy ? 0.48f : 0.25f, boat.position.z + spawnAhead + Random.Range(-2f, 2f));
        obj.transform.localScale = kind == Obstacle.Kind.Buoy ? new Vector3(0.8f, 0.8f, 0.8f) : (kind == Obstacle.Kind.Rubble ? new Vector3(1.2f, 0.5f, 1.4f) : new Vector3(1.1f, 0.7f, 1.1f));
        var collider = obj.GetComponent<Collider>();
        collider.isTrigger = true;
        var rb = obj.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false;
        obj.AddComponent<Obstacle>().Configure(kind, obstacleSpeed);
        var renderer = obj.GetComponent<Renderer>();
        renderer.material.color = kind == Obstacle.Kind.Buoy ? new Color(1f, 0.48f, 0.08f) : (kind == Obstacle.Kind.Rubble ? new Color(0.35f, 0.31f, 0.27f) : new Color(0.28f, 0.3f, 0.33f));
    }
}

