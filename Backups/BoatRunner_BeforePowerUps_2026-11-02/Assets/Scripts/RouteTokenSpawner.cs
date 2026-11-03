using UnityEngine;

public sealed class RouteTokenSpawner : MonoBehaviour
{
    [SerializeField] Transform boat;
    [SerializeField] BoatGameManager gameManager;
    [SerializeField] ObstacleSpawner hazardSpawner;
    [SerializeField] Material goldMaterial;
    [SerializeField] Material faceMaterial;
    [SerializeField] float initialInterval = 4.4f;
    [SerializeField] float minimumInterval = 3.3f;
    [SerializeField] float firstTokenDelay = 1.2f;
    [SerializeField] float spawnAhead = 38f;
    [SerializeField] float laneSpacing = 2.45f;

    float timer;
    float elapsed;
    float lastTokenZ = float.NegativeInfinity;
    int lastTokenLane = -1;

    public void Configure(Transform player, BoatGameManager manager, ObstacleSpawner hazards, Material gold, Material face)
    {
        boat = player;
        gameManager = manager;
        hazardSpawner = hazards;
        goldMaterial = gold;
        faceMaterial = face;
        if (hazards != null) laneSpacing = hazards.LaneSpacing;
    }

    public void ResetSpawner()
    {
        timer = Mathf.Max(0f, initialInterval - firstTokenDelay);
        elapsed = 0f;
        lastTokenZ = float.NegativeInfinity;
        lastTokenLane = -1;
    }

    void Start()
    {
        if (boat == null) boat = FindFirstObjectByType<BoatController>()?.transform;
        if (gameManager == null) gameManager = FindFirstObjectByType<BoatGameManager>();
        if (hazardSpawner == null) hazardSpawner = FindFirstObjectByType<ObstacleSpawner>();
    }

    void Update()
    {
        if (boat == null || (gameManager != null && !gameManager.IsPlaying)) return;
        elapsed += Time.deltaTime;
        timer += Time.deltaTime;
        float interval = Mathf.Lerp(initialInterval, minimumInterval, Mathf.Clamp01(elapsed / 180f));
        if (timer < interval) return;
        timer = 0f;
        Spawn();
    }

    void Spawn()
    {
        if (boat == null) return;
        float z = boat.position.z + spawnAhead + Random.Range(0f, 7f);
        int lane = ChooseLane(z);
        lastTokenLane = lane;
        lastTokenZ = z;
        CreateToken(lane, z);
    }

    int ChooseLane(float z)
    {
        int selected = Random.Range(0, 5);
        for (int attempt = 0; attempt < 12; attempt++)
        {
            int candidate = Random.Range(0, 5);
            if (candidate == lastTokenLane && Mathf.Abs(z - lastTokenZ) < 16f) continue;
            if (hazardSpawner != null && candidate == hazardSpawner.LastLane &&
                Mathf.Abs(z - hazardSpawner.LastSpawnZ) < 12f) continue;
            bool blocked = false;
            foreach (var hazard in FindObjectsByType<Obstacle>(FindObjectsSortMode.None))
            {
                int hazardLane = Mathf.RoundToInt(hazard.transform.position.x / laneSpacing) + 2;
                if (hazardLane == candidate && Mathf.Abs(hazard.transform.position.z - z) < 10f)
                {
                    blocked = true;
                    break;
                }
            }
            if (blocked) continue;
            selected = candidate;
            break;
        }
        return selected;
    }

    public GameObject CreateToken(int lane, float worldZ)
    {
        if (boat == null) return null;
        lane = Mathf.Clamp(lane, 0, 4);
        var token = new GameObject("Marine Token");
        token.transform.SetPositionAndRotation(
            new Vector3((lane - 2) * laneSpacing, 0f, worldZ),
            Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        token.AddComponent<Rigidbody>();
        token.AddComponent<SphereCollider>();
        float drift = 0.45f + Mathf.Min(elapsed * 0.012f, 1.4f);
        token.AddComponent<RouteToken>().Configure(boat, gameManager, drift, goldMaterial, faceMaterial);
        return token;
    }
}