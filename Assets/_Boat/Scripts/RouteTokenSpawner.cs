using UnityEngine;

public sealed class RouteTokenSpawner : MonoBehaviour
{
    [SerializeField] Transform boat;
    [SerializeField] BoatGameManager gameManager;
    [SerializeField] ObstacleSpawner hazardSpawner;
    [SerializeField] Material goldMaterial;
    [SerializeField] Material faceMaterial;
    const float FallbackLaneSpacing = 1.47f;

    float timer;
    float elapsed;
    float lastTokenZ = float.NegativeInfinity;
    int lastTokenLane = -1;
    float powerUpTimer;
    float nextPowerUpDelay = 20f;
    float powerUpRetryTimer;
    float LaneSpacing => hazardSpawner != null ? hazardSpawner.LaneSpacing
        : gameManager == null ? FallbackLaneSpacing : gameManager.DifficultySettings.laneSpacing;

    public void Configure(Transform player, BoatGameManager manager, ObstacleSpawner hazards, Material gold, Material face)
    {
        boat = player;
        gameManager = manager;
        hazardSpawner = hazards;
        goldMaterial = gold;
        faceMaterial = face;
    }

    public void ResetSpawner()
    {
        BoatDifficultyConfig config = gameManager == null ? null : gameManager.DifficultySettings;
        float firstInterval = config == null ? 2.8f : config.coinIntervalStart;
        float firstDelay = config == null ? 0.35f : config.firstCoinDelay;
        timer = Mathf.Max(0f, firstInterval - firstDelay);
        elapsed = 0f;
        lastTokenZ = float.NegativeInfinity;
        lastTokenLane = -1;
        powerUpTimer = 0f;
        powerUpRetryTimer = 0f;
        nextPowerUpDelay = RandomPowerUpInterval();
        PrefillOpening();
    }

    void PrefillOpening()
    {
        if (boat == null) return;
        float speed = gameManager == null ? 11f : gameManager.CruiseSpeed;
        float z = boat.position.z + speed * 2.0f;
        if (!TryChooseFreeLane(z, out int trailLane)) return;
        BoatDifficultyConfig config = gameManager == null ? null : gameManager.DifficultySettings;
        int trailCount = config == null ? 3 : config.coinTrailCountStart;
        float spacing = config == null ? 2.15f : config.coinTrailSpacing;
        for (int i = 0; i < trailCount; i++)
        {
            float tokenZ = z + i * spacing;
            int lane = trailLane;
            if (!IsLanePositionFree(lane, tokenZ) && !TryChooseFreeLaneInternal(tokenZ, out lane, false)) continue;
            lastTokenLane = lane;
            lastTokenZ = tokenZ;
            CreateToken(lane, tokenZ);
        }
    }

    void Start()
    {
        if (boat == null) boat = FindAnyObjectByType<BoatController>()?.transform;
        if (gameManager == null) gameManager = FindAnyObjectByType<BoatGameManager>();
        if (hazardSpawner == null) hazardSpawner = FindAnyObjectByType<ObstacleSpawner>();
    }

    void Update()
    {
        if (boat == null || (gameManager != null && !gameManager.IsPlaying)) return;
        elapsed += Time.deltaTime;
        timer += Time.deltaTime;
        BoatDifficultyConfig config = gameManager == null ? null : gameManager.DifficultySettings;
        float interval = config == null ? 2.8f : Mathf.Lerp(config.coinIntervalStart, config.coinIntervalMax, gameManager.Difficulty);
        if (timer >= interval)
        {
            timer = 0f;
            Spawn();
        }

        powerUpTimer += Time.deltaTime;
        powerUpRetryTimer += Time.deltaTime;
        float guarantee = config == null ? 25f : config.powerUpGuaranteedInterval;
        if ((powerUpTimer >= nextPowerUpDelay || powerUpTimer >= guarantee) && powerUpRetryTimer >= 0.55f)
        {
            powerUpRetryTimer = 0f;
            if (SpawnPowerUp())
            {
                powerUpTimer = 0f;
                nextPowerUpDelay = RandomPowerUpInterval();
            }
        }
    }

    void Spawn()
    {
        if (boat == null) return;
        BoatDifficultyConfig config = gameManager == null ? null : gameManager.DifficultySettings;
        float spawnAhead = gameManager == null ? 26f : gameManager.SpawnDistance;
        float z = boat.position.z + spawnAhead + Random.Range(0f, 4f);
        if (!TryChooseFreeLane(z, out int trailLane)) return;
        int trailCount = config == null ? 3 : Mathf.RoundToInt(Mathf.Lerp(
            config.coinTrailCountStart, config.coinTrailCountMax, gameManager.Difficulty));
        float spacing = config == null ? 2.15f : config.coinTrailSpacing;
        for (int i = 0; i < trailCount; i++)
        {
            float tokenZ = z + i * spacing;
            int lane = trailLane;
            if (!IsLanePositionFree(lane, tokenZ) && !TryChooseFreeLaneInternal(tokenZ, out lane, false)) continue;
            lastTokenLane = lane;
            lastTokenZ = tokenZ;
            CreateToken(lane, tokenZ);
        }
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
            foreach (var hazard in FindObjectsByType<Obstacle>())
            {
                int hazardLane = Mathf.RoundToInt(hazard.transform.position.x / LaneSpacing) + 2;
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

    bool SpawnPowerUp()
    {
        if (boat == null || gameManager == null) return false;
        float z = boat.position.z + gameManager.SpawnDistance + Random.Range(0f, 4f);
        if (!TryChooseFreeLane(z, out int lane)) return false;
        var type = (BoatPowerUpType)Random.Range(0, 4);
        CreatePowerUp(lane, z, type);
        return true;
    }

    public bool TryChooseFreeLane(float z, out int lane)
    {
        return TryChooseFreeLaneInternal(z, out lane, true);
    }

    bool TryChooseFreeLaneInternal(float z, out int lane, bool avoidRecentTokenLane)
    {
        int start = Random.Range(0, 5);
        for (int offset = 0; offset < 5; offset++)
        {
            int candidate = (start + offset) % 5;
            if (avoidRecentTokenLane && candidate == lastTokenLane && Mathf.Abs(z - lastTokenZ) < 16f) continue;
            if (hazardSpawner != null && candidate == hazardSpawner.LastLane &&
                Mathf.Abs(z - hazardSpawner.LastSpawnZ) < 12f) continue;

            if (!IsLanePositionFree(candidate, z)) continue;

            lane = candidate;
            return true;
        }

        lane = -1;
        return false;
    }

    bool IsLanePositionFree(int lane, float z)
    {
        float x = (lane - 2) * LaneSpacing;
        foreach (var hazard in FindObjectsByType<Obstacle>())
            if (OverlapsSpawnSpace(hazard.GetComponent<Collider>(), x, z)) return false;
        foreach (var token in FindObjectsByType<RouteToken>())
            if (OverlapsSpawnSpace(token.GetComponent<Collider>(), x, z)) return false;
        foreach (var pickup in FindObjectsByType<PowerUpPickup>())
            if (OverlapsSpawnSpace(pickup.GetComponent<Collider>(), x, z)) return false;
        return true;
    }

    float RandomPowerUpInterval()
    {
        BoatDifficultyConfig config = gameManager == null ? null : gameManager.DifficultySettings;
        float minimum = config == null ? 12f : config.powerUpIntervalMinimum;
        float maximum = config == null ? 18f : config.powerUpIntervalMaximum;
        return Random.Range(Mathf.Min(minimum, maximum), Mathf.Max(minimum, maximum));
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
            new Vector3((lane - 2) * LaneSpacing, 0f, worldZ),
            Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        pickup.AddComponent<Rigidbody>();
        pickup.AddComponent<SphereCollider>();
        float difficulty = gameManager == null ? 0f : gameManager.Difficulty;
        BoatDifficultyConfig config = gameManager == null ? null : gameManager.DifficultySettings;
        float drift = config == null ? 0.45f : Mathf.Lerp(config.obstacleDriftStart, config.obstacleDriftMax, difficulty);
        pickup.AddComponent<PowerUpPickup>().Configure(type, boat, gameManager, drift);
        return pickup;
    }
    public GameObject CreateToken(int lane, float worldZ)
    {
        if (boat == null) return null;
        lane = Mathf.Clamp(lane, 0, 4);
        var token = new GameObject("Marine Token");
        token.transform.SetPositionAndRotation(
            new Vector3((lane - 2) * LaneSpacing, 0f, worldZ),
            Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
        token.AddComponent<Rigidbody>();
        token.AddComponent<SphereCollider>();
        float difficulty = gameManager == null ? 0f : gameManager.Difficulty;
        BoatDifficultyConfig config = gameManager == null ? null : gameManager.DifficultySettings;
        float drift = config == null ? 0.45f : Mathf.Lerp(config.obstacleDriftStart, config.obstacleDriftMax, difficulty);
        token.AddComponent<RouteToken>().Configure(boat, gameManager, drift, goldMaterial, faceMaterial);
        return token;
    }
}
