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
    [SerializeField] float minimumPowerUpInterval = 15f;
    [SerializeField] float maximumPowerUpInterval = 25f;

    float timer;
    float elapsed;
    float lastTokenZ = float.NegativeInfinity;
    int lastTokenLane = -1;
    float powerUpTimer;
    float nextPowerUpDelay = 20f;

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
        powerUpTimer = 0f;
        nextPowerUpDelay = Random.Range(minimumPowerUpInterval, maximumPowerUpInterval);
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
        if (timer >= interval)
        {
            timer = 0f;
            Spawn();
        }

        powerUpTimer += Time.deltaTime;
        if (powerUpTimer >= nextPowerUpDelay)
        {
            powerUpTimer = 0f;
            nextPowerUpDelay = Random.Range(minimumPowerUpInterval, maximumPowerUpInterval);
            SpawnPowerUp();
        }
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

    void SpawnPowerUp()
    {
        float z = boat.position.z + spawnAhead + Random.Range(0f, 7f);
        if (!TryChooseFreeLane(z, out int lane)) return;
        var type = (BoatPowerUpType)Random.Range(0, 4);
        CreatePowerUp(lane, z, type);
    }

    public bool TryChooseFreeLane(float z, out int lane)
    {
        int start = Random.Range(0, 5);
        for (int offset = 0; offset < 5; offset++)
        {
            int candidate = (start + offset) % 5;
            if (candidate == lastTokenLane && Mathf.Abs(z - lastTokenZ) < 16f) continue;
            if (hazardSpawner != null && candidate == hazardSpawner.LastLane &&
                Mathf.Abs(z - hazardSpawner.LastSpawnZ) < 12f) continue;

            float x = (candidate - 2) * laneSpacing;
            bool occupied = false;
            foreach (var hazard in FindObjectsByType<Obstacle>(FindObjectsSortMode.None))
            {
                if (OverlapsSpawnSpace(hazard.GetComponent<Collider>(), x, z)) { occupied = true; break; }
            }
            if (occupied) continue;
            foreach (var token in FindObjectsByType<RouteToken>(FindObjectsSortMode.None))
            {
                if (OverlapsSpawnSpace(token.GetComponent<Collider>(), x, z)) { occupied = true; break; }
            }
            if (occupied) continue;
            foreach (var pickup in FindObjectsByType<PowerUpPickup>(FindObjectsSortMode.None))
            {
                if (OverlapsSpawnSpace(pickup.GetComponent<Collider>(), x, z)) { occupied = true; break; }
            }
            if (occupied) continue;

            lane = candidate;
            return true;
        }

        lane = -1;
        return false;
    }

    static bool OverlapsSpawnSpace(Collider existing, float x, float z)
    {
        if (existing == null || !existing.enabled) return false;
        // Use the real collider shape (including rotation) so long, angled hazards
        // do not reserve an unnecessarily large axis-aligned rectangle of lanes.
        Vector3 pickupCenter = new Vector3(x, 0.34f, z);
        Vector3 closest = existing.ClosestPoint(pickupCenter);
        const float pickupRadius = 0.85f;
        const float extraClearance = 0.2f;
        float requiredClearance = pickupRadius + extraClearance;
        return (closest - pickupCenter).sqrMagnitude < requiredClearance * requiredClearance;
    }

    public GameObject CreatePowerUp(int lane, float worldZ, BoatPowerUpType type)
    {
        if (boat == null || gameManager == null) return null;
        lane = Mathf.Clamp(lane, 0, 4);
        var pickup = new GameObject("Power-Up " + type);
        pickup.transform.SetPositionAndRotation(
            new Vector3((lane - 2) * laneSpacing, 0f, worldZ),
            Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        pickup.AddComponent<Rigidbody>();
        pickup.AddComponent<SphereCollider>();
        float drift = 0.45f + Mathf.Min(elapsed * 0.012f, 1.4f);
        pickup.AddComponent<PowerUpPickup>().Configure(type, boat, gameManager, drift);
        return pickup;
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
