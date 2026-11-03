using UnityEngine;

public sealed class ObstacleSpawner : MonoBehaviour
{
    [SerializeField] Transform boat;
    [SerializeField] BoatGameManager gameManager;
    [SerializeField] Material rockMaterial;
    [SerializeField] Material woodMaterial;
    [SerializeField] Material buoyMaterial;
    [SerializeField] float startInterval = 1.9f;
    [SerializeField] float minInterval = 1.2f;
    [SerializeField] float spawnAhead = 42f;
    [SerializeField] float laneSpacing = 2.45f;
    float timer;
    float elapsed;
    int lastLane = -10;

    public void Configure(Transform player, BoatGameManager manager, Material rock, Material wood, Material buoy)
    {
        boat = player; gameManager = manager; rockMaterial = rock; woodMaterial = wood; buoyMaterial = buoy;
    }
    public void ResetSpawner() { timer = 0f; elapsed = 0f; lastLane = -10; }

    void Update()
    {
        if (boat == null || (gameManager != null && gameManager.IsGameOver)) return;
        elapsed += Time.deltaTime;
        timer += Time.deltaTime;
        float interval = Mathf.Lerp(startInterval, minInterval, Mathf.Clamp01(elapsed / 150f));
        if (timer >= interval) { timer = 0f; Spawn(); }
    }

    void Spawn()
    {
        int lane = Random.Range(0, 5);
        if (lane == lastLane) lane = (lane + Random.Range(1, 5)) % 5;
        lastLane = lane;
        var kind = (Obstacle.Kind)Random.Range(0, 4);
        var obstacle = new GameObject(kind.ToString());
        obstacle.transform.position = new Vector3((lane - 2) * laneSpacing, 0f, boat.position.z + spawnAhead + Random.Range(0f, 5f));
        var rb = obstacle.AddComponent<Rigidbody>(); rb.isKinematic = true; rb.useGravity = false; rb.interpolation = RigidbodyInterpolation.Interpolate;
        var hitbox = obstacle.AddComponent<BoxCollider>(); hitbox.center = new Vector3(0f, 0.45f, 0f);
        float drift = 0.45f + Mathf.Min(elapsed * 0.012f, 1.4f);
        obstacle.AddComponent<Obstacle>().Configure(kind, boat, drift);
        switch (kind)
        {
            case Obstacle.Kind.Rock: BuildRock(obstacle.transform, hitbox); break;
            case Obstacle.Kind.Log: BuildLog(obstacle.transform, hitbox); break;
            case Obstacle.Kind.Buoy: BuildBuoy(obstacle.transform, hitbox); break;
            case Obstacle.Kind.Debris: BuildDebris(obstacle.transform, hitbox); break;
        }
    }

    void BuildRock(Transform root, BoxCollider hitbox)
    {
        hitbox.size = new Vector3(1.75f, 1.1f, 1.55f); hitbox.center = new Vector3(0f, 0.52f, 0f);
        AddPart(root, PrimitiveType.Sphere, new Vector3(0f, 0.47f, 0f), new Vector3(1.4f, 0.88f, 1.2f), rockMaterial, Quaternion.Euler(0f, Random.Range(0f, 360f), Random.Range(-8f, 8f)));
        AddPart(root, PrimitiveType.Sphere, new Vector3(-0.48f, 0.34f, 0.22f), new Vector3(0.78f, 0.62f, 0.72f), rockMaterial);
        AddPart(root, PrimitiveType.Sphere, new Vector3(0.42f, 0.36f, -0.24f), new Vector3(0.7f, 0.68f, 0.65f), rockMaterial);
    }

    void BuildLog(Transform root, BoxCollider hitbox)
    {
        hitbox.size = new Vector3(0.9f, 0.65f, 2.4f); hitbox.center = new Vector3(0f, 0.37f, 0f);
        var alongZ = Quaternion.Euler(90f, 0f, Random.Range(-10f, 10f));
        AddPart(root, PrimitiveType.Cylinder, new Vector3(0f, 0.4f, 0f), new Vector3(0.34f, 1.05f, 0.34f), woodMaterial, alongZ);
        AddPart(root, PrimitiveType.Cylinder, new Vector3(0f, 0.4f, 0.72f), new Vector3(0.29f, 0.07f, 0.29f), rockMaterial, alongZ);
        AddPart(root, PrimitiveType.Cylinder, new Vector3(0f, 0.4f, -0.72f), new Vector3(0.29f, 0.07f, 0.29f), rockMaterial, alongZ);
    }

    void BuildBuoy(Transform root, BoxCollider hitbox)
    {
        hitbox.size = new Vector3(0.92f, 1.2f, 0.92f); hitbox.center = new Vector3(0f, 0.55f, 0f);
        AddPart(root, PrimitiveType.Sphere, new Vector3(0f, 0.48f, 0f), new Vector3(0.82f, 0.7f, 0.82f), buoyMaterial);
        AddPart(root, PrimitiveType.Cylinder, new Vector3(0f, 0.95f, 0f), new Vector3(0.12f, 0.37f, 0.12f), buoyMaterial);
        AddPart(root, PrimitiveType.Cube, new Vector3(0f, 1.19f, 0f), new Vector3(0.43f, 0.15f, 0.09f), woodMaterial);
    }

    void BuildDebris(Transform root, BoxCollider hitbox)
    {
        hitbox.size = new Vector3(1.3f, 0.52f, 1.7f); hitbox.center = new Vector3(0f, 0.28f, 0f);
        AddPart(root, PrimitiveType.Cube, new Vector3(0f, 0.24f, 0f), new Vector3(0.24f, 0.24f, 1.65f), woodMaterial, Quaternion.Euler(0f, 0f, -5f));
        AddPart(root, PrimitiveType.Cube, new Vector3(-0.34f, 0.26f, -0.13f), new Vector3(0.18f, 0.25f, 1.08f), rockMaterial, Quaternion.Euler(0f, -17f, 7f));
        AddPart(root, PrimitiveType.Cube, new Vector3(0.37f, 0.25f, 0.12f), new Vector3(0.19f, 0.22f, 0.9f), woodMaterial, Quaternion.Euler(0f, 21f, -8f));
    }

    static void AddPart(Transform parent, PrimitiveType type, Vector3 localPosition, Vector3 scale, Material material, Quaternion? rotation = null)
    {
        var part = GameObject.CreatePrimitive(type);
        part.name = type.ToString(); part.transform.SetParent(parent, false); part.transform.localPosition = localPosition; part.transform.localScale = scale;
        if (rotation.HasValue) part.transform.localRotation = rotation.Value;
        var collider = part.GetComponent<Collider>();
        if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
        part.GetComponent<Renderer>().sharedMaterial = material;
    }
}
