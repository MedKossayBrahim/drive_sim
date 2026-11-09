using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class ObstacleSpawner : MonoBehaviour
{
    [SerializeField] Transform boat;
    [SerializeField] BoatGameManager gameManager;
    [SerializeField] Material rockMaterial;
    [SerializeField] Material woodMaterial;
    [SerializeField] Material buoyMaterial;
    [SerializeField] Material seaweedMaterial;
    [SerializeField] Material foamMaterial;
    [SerializeField] Material accentMaterial;
    const float FallbackLaneSpacing = 1.47f;

    float timer;
    float elapsed;
    int lastLane = -10;
    float lastSpawnZ = float.NegativeInfinity;
    int lastSafeCorridorStart = 1;
    int lastSafeCorridorLanes = 3;
    int lastWaveObstacleCount;

    public int LastLane => lastLane;
    public float LastSpawnZ => lastSpawnZ;
    public float LaneSpacing => gameManager == null || gameManager.DifficultySettings == null
        ? FallbackLaneSpacing : gameManager.DifficultySettings.laneSpacing;
    public int LastSafeCorridorStart => lastSafeCorridorStart;
    public int LastSafeCorridorLanes => lastSafeCorridorLanes;
    public int LastWaveObstacleCount => lastWaveObstacleCount;

    static Mesh rockMesh;
    static Mesh buoyBodyMesh;
    static Mesh buoyMastMesh;
    static Mesh buoyBandMesh;
    static Mesh driftwoodMesh;
    static Mesh seaweedMesh;
    static Mesh foamPatchMesh;
    static Mesh plankMesh;

    public void Configure(Transform player, BoatGameManager manager, Material slate, Material wood, Material buoy,
        Material seaweed = null, Material foam = null, Material accent = null)
    {
        boat = player;
        gameManager = manager;
        rockMaterial = slate;
        woodMaterial = wood;
        buoyMaterial = buoy;
        seaweedMaterial = seaweed;
        foamMaterial = foam;
        accentMaterial = accent;
    }

    public void ResetSpawner()
    {
        timer = 0f;
        elapsed = 0f;
        lastLane = -10;
        lastSpawnZ = float.NegativeInfinity;
        lastWaveObstacleCount = 0;
        PrefillOpening();
    }

    void PrefillOpening()
    {
        if (boat == null) return;
        float speed = gameManager == null ? 11f : gameManager.CruiseSpeed;
        float firstZ = boat.position.z + speed * 2.15f;
        float secondZ = firstZ + Mathf.Max(8f, speed * 0.85f);
        PrefillWave(firstZ, 1);
        PrefillWave(secondZ, 2);
        timer = -0.15f;
    }

    void PrefillWave(float worldZ, int obstacleCount)
    {
        lastSafeCorridorLanes = 3;
        lastSafeCorridorStart = Random.Range(0, 6 - lastSafeCorridorLanes);
        var lanes = new List<int>(5 - lastSafeCorridorLanes);
        for (int lane = 0; lane < 5; lane++)
            if (lane < lastSafeCorridorStart || lane >= lastSafeCorridorStart + lastSafeCorridorLanes)
                lanes.Add(lane);
        for (int i = lanes.Count - 1; i > 0; i--)
        {
            int swap = Random.Range(0, i + 1);
            (lanes[i], lanes[swap]) = (lanes[swap], lanes[i]);
        }

        int count = Mathf.Clamp(obstacleCount, 1, lanes.Count);
        lastWaveObstacleCount = 0;
        for (int i = 0; i < count; i++)
        {
            int lane = lanes[i];
            if (IsHazardSpaceOccupied(lane, worldZ)) continue;
            CreateHazard(ChooseKind(0f, gameManager == null ? null : gameManager.DifficultySettings), lane, worldZ);
            lastLane = lane;
            lastSpawnZ = worldZ;
            lastWaveObstacleCount++;
        }
    }

    void Update()
    {
        if (boat == null || (gameManager != null && !gameManager.IsPlaying)) return;
        elapsed += Time.deltaTime;
        timer += Time.deltaTime;
        BoatDifficultyConfig config = gameManager == null ? null : gameManager.DifficultySettings;
        float interval = config == null ? 1.9f : Mathf.Lerp(config.obstacleIntervalStart, config.obstacleIntervalMax, gameManager.Difficulty);
        if (timer >= interval)
        {
            timer = 0f;
            Spawn();
        }
    }

    void Spawn()
    {
        if (boat == null) return;
        BoatDifficultyConfig config = gameManager == null ? null : gameManager.DifficultySettings;
        float difficulty = gameManager == null ? 0f : gameManager.Difficulty;
        int corridorMin = config == null ? 3 : config.safeCorridorLanesStart;
        int corridorMax = config == null ? 2 : config.safeCorridorLanesMax;
        lastSafeCorridorLanes = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(corridorMin, corridorMax, difficulty)), 2, 4);
        lastSafeCorridorStart = Random.Range(0, 6 - lastSafeCorridorLanes);

        int desiredCount = config == null ? 1 : Mathf.RoundToInt(Mathf.Lerp(
            config.obstaclesPerWaveStart, config.obstaclesPerWaveMax, difficulty));
        desiredCount = Mathf.Clamp(desiredCount, 1, 5 - lastSafeCorridorLanes);
        var lanes = new List<int>(5 - lastSafeCorridorLanes);
        for (int lane = 0; lane < 5; lane++)
            if (lane < lastSafeCorridorStart || lane >= lastSafeCorridorStart + lastSafeCorridorLanes) lanes.Add(lane);
        for (int i = lanes.Count - 1; i > 0; i--)
        {
            int swap = Random.Range(0, i + 1);
            (lanes[i], lanes[swap]) = (lanes[swap], lanes[i]);
        }

        float spawnDistance = gameManager == null ? 26f : gameManager.SpawnDistance;
        float z = boat.position.z + spawnDistance + Random.Range(0f, 2f);
        float gap = config == null ? 12f : Mathf.Lerp(config.obstacleWaveGapStart, config.obstacleWaveGapMax, difficulty);
        lastSpawnZ = z;
        lastWaveObstacleCount = 0;
        for (int i = 0; i < desiredCount && i < lanes.Count; i++)
        {
            int lane = lanes[i];
            float obstacleZ = z + i * gap;
            if (IsHazardSpaceOccupied(lane, obstacleZ)) continue;
            CreateHazard(ChooseKind(difficulty, config), lane, obstacleZ);
            lastLane = lane;
            lastSpawnZ = obstacleZ;
            lastWaveObstacleCount++;
        }
    }

    Obstacle.Kind ChooseKind(float difficulty, BoatDifficultyConfig config)
    {
        float buoyChance = config == null ? 0.82f : Mathf.Lerp(config.buoyChanceStart, config.buoyChanceMax, difficulty);
        float roll = Random.value;
        if (roll < buoyChance) return Obstacle.Kind.Buoy;
        float remainingRoll = (roll - buoyChance) / Mathf.Max(0.0001f, 1f - buoyChance);
        float rockChance = config == null ? 0.34f : config.rockChanceAfterBuoys;
        if (remainingRoll < rockChance) return Obstacle.Kind.Rock;
        if (remainingRoll < rockChance + 0.36f) return Obstacle.Kind.Driftwood;
        return remainingRoll < rockChance + 0.70f ? Obstacle.Kind.Seaweed : Obstacle.Kind.Debris;
    }

    bool IsHazardSpaceOccupied(int lane, float z)
    {
        float x = (lane - 2) * LaneSpacing;
        Vector3 candidate = new Vector3(x, 0.45f, z);
        foreach (var hazard in FindObjectsByType<Obstacle>())
        {
            var collider = hazard.GetComponent<Collider>();
            if (collider != null && collider.enabled && (collider.ClosestPoint(candidate) - candidate).sqrMagnitude < 1.35f * 1.35f)
                return true;
        }
        foreach (var token in FindObjectsByType<RouteToken>())
        {
            var collider = token.GetComponent<Collider>();
            if (collider != null && collider.enabled && (collider.ClosestPoint(candidate) - candidate).sqrMagnitude < 1.35f * 1.35f)
                return true;
        }
        foreach (var pickup in FindObjectsByType<PowerUpPickup>())
        {
            var collider = pickup.GetComponent<Collider>();
            if (collider != null && collider.enabled && (collider.ClosestPoint(candidate) - candidate).sqrMagnitude < 1.45f * 1.45f)
                return true;
        }
        return false;
    }

    public GameObject CreateHazard(Obstacle.Kind kind, int lane, float worldZ)
    {
        if (boat == null) return null;
        lane = Mathf.Clamp(lane, 0, 4);
        var obstacle = new GameObject(kind.ToString());
        obstacle.transform.SetPositionAndRotation(
            new Vector3((lane - 2) * LaneSpacing, 0f, worldZ),
            Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));

        var body = obstacle.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        var hitbox = obstacle.AddComponent<BoxCollider>();
        hitbox.isTrigger = true;

        BoatDifficultyConfig config = gameManager == null ? null : gameManager.DifficultySettings;
        float difficulty = gameManager == null ? 0f : gameManager.Difficulty;
        float drift = config == null ? 0.45f : Mathf.Lerp(config.obstacleDriftStart, config.obstacleDriftMax, difficulty);
        obstacle.AddComponent<Obstacle>().Configure(kind, boat, drift);
        switch (kind)
        {
            case Obstacle.Kind.Buoy: BuildBuoy(obstacle.transform, hitbox); break;
            case Obstacle.Kind.Rock: BuildRock(obstacle.transform, hitbox); break;
            case Obstacle.Kind.Driftwood: BuildDriftwood(obstacle.transform, hitbox); break;
            case Obstacle.Kind.Seaweed: BuildSeaweed(obstacle.transform, hitbox); break;
            case Obstacle.Kind.Debris: BuildDebris(obstacle.transform, hitbox); break;
        }
        return obstacle;
    }

    void BuildRock(Transform root, BoxCollider hitbox)
    {
        if (rockMesh == null) rockMesh = BuildRockMesh();
        hitbox.size = new Vector3(1.65f, 0.78f, 1.55f);
        hitbox.center = new Vector3(0f, 0.34f, 0f);
        AddMesh(root, "Faceted Low Rock", rockMesh, rockMaterial, new Vector3(0f, 0.03f, 0f),
            new Vector3(1.55f, 0.82f, 1.28f), Quaternion.identity);
    }

    static Mesh BuildRockMesh()
    {
        const int sides = 9;
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        float[] radii = { 0.28f, 0.46f, 0.38f };
        float[] heights = { 0.03f, 0.24f, 0.58f };
        for (int ring = 0; ring < radii.Length; ring++)
            for (int side = 0; side < sides; side++)
            {
                float angle = side * Mathf.PI * 2f / sides;
                float uneven = 0.82f + 0.11f * Mathf.Sin(side * 2.71f + ring * 0.8f) + 0.05f * Mathf.Cos(side * 1.31f);
                vertices.Add(new Vector3(Mathf.Cos(angle) * radii[ring] * uneven,
                    heights[ring] + 0.035f * Mathf.Sin(side * 1.9f + ring),
                    Mathf.Sin(angle) * radii[ring] * uneven));
            }
        for (int ring = 0; ring < radii.Length - 1; ring++)
            for (int side = 0; side < sides; side++)
            {
                int a = ring * sides + side;
                int b = ring * sides + (side + 1) % sides;
                int c = (ring + 1) * sides + (side + 1) % sides;
                int d = (ring + 1) * sides + side;
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
                triangles.Add(a); triangles.Add(c); triangles.Add(d);
            }
        int top = vertices.Count;
        vertices.Add(new Vector3(0.04f, 0.61f, -0.03f));
        for (int side = 0; side < sides; side++)
        {
            int a = (radii.Length - 1) * sides + side;
            int b = (radii.Length - 1) * sides + (side + 1) % sides;
            triangles.Add(a); triangles.Add(b); triangles.Add(top);
        }
        var mesh = new Mesh { name = "Low Faceted Marine Rock", vertices = vertices.ToArray(), triangles = triangles.ToArray() };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
    void BuildBuoy(Transform root, BoxCollider hitbox)
    {
        EnsureMeshes();
        hitbox.size = new Vector3(1.0f, 1.48f, 1.0f);
        hitbox.center = new Vector3(0f, 0.72f, 0f);
        AddMesh(root, "Rounded Float", buoyBodyMesh, buoyMaterial, new Vector3(0f, 0.04f, 0f), Vector3.one, Quaternion.identity);
        AddMesh(root, "Lower Reflective Band", buoyBandMesh, accentMaterial, new Vector3(0f, 0.36f, 0f), Vector3.one, Quaternion.identity);
        AddMesh(root, "Upper Reflective Band", buoyBandMesh, accentMaterial, new Vector3(0f, 0.70f, 0f), Vector3.one, Quaternion.identity);
        AddMesh(root, "Beacon Mast", buoyMastMesh, buoyMaterial, new Vector3(0f, 1.00f, 0f), Vector3.one, Quaternion.identity);
        AddMesh(root, "Beacon Crossbar", plankMesh, accentMaterial, new Vector3(0f, 1.39f, 0f),
            new Vector3(0.48f, 0.075f, 0.12f), Quaternion.identity);
        AddMesh(root, "Top Beacon", buoyMastMesh, buoyMaterial, new Vector3(0f, 1.36f, 0f),
            new Vector3(0.55f, 0.34f, 0.55f), Quaternion.identity);
    }

    void BuildDriftwood(Transform root, BoxCollider hitbox)
    {
        EnsureMeshes();
        hitbox.size = new Vector3(1.05f, 0.72f, 2.9f);
        hitbox.center = new Vector3(0f, 0.34f, 0f);
        AddMesh(root, "Weathered Main Limb", driftwoodMesh, woodMaterial, Vector3.zero, Vector3.one, Quaternion.identity);
        AddMesh(root, "Forked Limb Port", driftwoodMesh, woodMaterial, new Vector3(-0.16f, 0.02f, 0.30f),
            new Vector3(0.50f, 0.72f, 0.48f), Quaternion.Euler(0f, -46f, 0f));
        AddMesh(root, "Forked Limb Starboard", driftwoodMesh, woodMaterial, new Vector3(0.12f, 0.02f, -0.20f),
            new Vector3(0.42f, 0.65f, 0.42f), Quaternion.Euler(0f, 52f, 0f));
    }

    void BuildSeaweed(Transform root, BoxCollider hitbox)
    {
        EnsureMeshes();
        hitbox.size = new Vector3(1.85f, 0.72f, 1.75f);
        hitbox.center = new Vector3(0f, 0.30f, 0f);
        AddMesh(root, "Floating Seaweed Fronds", seaweedMesh, seaweedMaterial, new Vector3(0f, 0.03f, 0f), Vector3.one, Quaternion.identity);
        AddMesh(root, "Small Foam Patches", foamPatchMesh, foamMaterial, new Vector3(0f, 0.035f, 0f), Vector3.one, Quaternion.identity);
    }

    void BuildDebris(Transform root, BoxCollider hitbox)
    {
        EnsureMeshes();
        hitbox.size = new Vector3(1.45f, 0.72f, 1.5f);
        hitbox.center = new Vector3(0f, 0.32f, 0f);
        AddMesh(root, "Crate Floor", plankMesh, woodMaterial, new Vector3(0f, 0.13f, 0f),
            new Vector3(0.95f, 0.13f, 0.86f), Quaternion.Euler(0f, 8f, 0f));
        AddMesh(root, "Crate Side Port", plankMesh, woodMaterial, new Vector3(-0.40f, 0.31f, 0f),
            new Vector3(0.13f, 0.42f, 0.84f), Quaternion.Euler(0f, 0f, -6f));
        AddMesh(root, "Crate Side Starboard", plankMesh, woodMaterial, new Vector3(0.39f, 0.27f, -0.04f),
            new Vector3(0.13f, 0.34f, 0.80f), Quaternion.Euler(0f, 0f, 8f));
        AddMesh(root, "Broken Cross Board", plankMesh, accentMaterial, new Vector3(0.04f, 0.48f, 0.04f),
            new Vector3(0.86f, 0.10f, 0.18f), Quaternion.Euler(0f, -18f, -9f));
        AddMesh(root, "Loose Plank", plankMesh, woodMaterial, new Vector3(0.12f, 0.17f, 0.30f),
            new Vector3(0.16f, 0.09f, 0.80f), Quaternion.Euler(4f, 34f, 6f));
    }

    static void AddMesh(Transform parent, string name, Mesh mesh, Material material, Vector3 position, Vector3 scale, Quaternion rotation)
    {
        if (mesh == null) return;
        var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.transform.localScale = scale;
        part.transform.localRotation = rotation;
        part.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = part.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    static void EnsureMeshes()
    {
        if (buoyBodyMesh == null)
            buoyBodyMesh = BuildLathe("Navigation Buoy Float", new[]
            {
                new Vector2(0.14f, 0.00f), new Vector2(0.27f, 0.10f), new Vector2(0.39f, 0.27f),
                new Vector2(0.47f, 0.46f), new Vector2(0.45f, 0.64f), new Vector2(0.36f, 0.82f),
                new Vector2(0.22f, 0.98f), new Vector2(0.12f, 1.05f)
            }, 16);
        if (buoyMastMesh == null)
            buoyMastMesh = BuildLathe("Navigation Buoy Mast", new[]
            {
                new Vector2(0.07f, 0.00f), new Vector2(0.075f, 0.04f), new Vector2(0.055f, 0.42f), new Vector2(0.035f, 0.46f)
            }, 10);
        if (buoyBandMesh == null) buoyBandMesh = BuildTorus("Buoy Reflective Band", 0.405f, 0.032f, 20, 6);
        if (driftwoodMesh == null) driftwoodMesh = BuildDriftwoodMesh();
        if (seaweedMesh == null) seaweedMesh = BuildSeaweedMesh();
        if (foamPatchMesh == null) foamPatchMesh = BuildFoamPatchesMesh();
        if (plankMesh == null) plankMesh = BuildBoxMesh("Weathered Plank");
    }

    static Mesh BuildLathe(string name, Vector2[] profile, int segments)
    {
        var vertices = new Vector3[profile.Length * segments];
        var triangles = new int[(profile.Length - 1) * segments * 6];
        for (int ring = 0; ring < profile.Length; ring++)
            for (int side = 0; side < segments; side++)
            {
                float angle = side * Mathf.PI * 2f / segments;
                vertices[ring * segments + side] = new Vector3(Mathf.Cos(angle) * profile[ring].x, profile[ring].y, Mathf.Sin(angle) * profile[ring].x);
            }
        int t = 0;
        for (int ring = 0; ring < profile.Length - 1; ring++)
            for (int side = 0; side < segments; side++)
            {
                int next = (side + 1) % segments;
                int a = ring * segments + side;
                int b = ring * segments + next;
                int c = (ring + 1) * segments + next;
                int d = (ring + 1) * segments + side;
                triangles[t++] = a; triangles[t++] = b; triangles[t++] = c;
                triangles[t++] = a; triangles[t++] = c; triangles[t++] = d;
            }
        var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh BuildTorus(string name, float majorRadius, float minorRadius, int majorSegments, int minorSegments)
    {
        var vertices = new Vector3[majorSegments * minorSegments];
        var triangles = new int[majorSegments * minorSegments * 6];
        for (int i = 0; i < majorSegments; i++)
        {
            float u = i * Mathf.PI * 2f / majorSegments;
            for (int j = 0; j < minorSegments; j++)
            {
                float v = j * Mathf.PI * 2f / minorSegments;
                float radius = majorRadius + Mathf.Cos(v) * minorRadius;
                vertices[i * minorSegments + j] = new Vector3(Mathf.Cos(u) * radius, Mathf.Sin(v) * minorRadius, Mathf.Sin(u) * radius);
            }
        }
        int t = 0;
        for (int i = 0; i < majorSegments; i++)
            for (int j = 0; j < minorSegments; j++)
            {
                int a = i * minorSegments + j;
                int b = i * minorSegments + (j + 1) % minorSegments;
                int c = ((i + 1) % majorSegments) * minorSegments + (j + 1) % minorSegments;
                int d = ((i + 1) % majorSegments) * minorSegments + j;
                triangles[t++] = a; triangles[t++] = b; triangles[t++] = c;
                triangles[t++] = a; triangles[t++] = c; triangles[t++] = d;
            }
        var mesh = new Mesh { name = name, vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh BuildDriftwoodMesh()
    {
        const int sides = 10;
        float[] z = { -1.25f, -0.91f, -0.52f, -0.13f, 0.32f, 0.76f, 1.25f };
        float[] radius = { 0.08f, 0.16f, 0.22f, 0.24f, 0.22f, 0.16f, 0.075f };
        float[] cx = { 0.00f, -0.015f, 0.025f, 0.00f, -0.025f, 0.015f, 0.00f };
        float[] cy = { 0.31f, 0.32f, 0.30f, 0.33f, 0.31f, 0.32f, 0.31f };
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        for (int ring = 0; ring < z.Length; ring++)
            for (int side = 0; side < sides; side++)
            {
                float angle = side * Mathf.PI * 2f / sides;
                vertices.Add(new Vector3(cx[ring] + Mathf.Cos(angle) * radius[ring] * 1.15f,
                    cy[ring] + Mathf.Sin(angle) * radius[ring] * 0.88f, z[ring]));
            }
        for (int ring = 0; ring < z.Length - 1; ring++)
            for (int side = 0; side < sides; side++)
            {
                int next = (side + 1) % sides;
                int a = ring * sides + side;
                int b = ring * sides + next;
                int c = (ring + 1) * sides + next;
                int d = (ring + 1) * sides + side;
                triangles.Add(a); triangles.Add(b); triangles.Add(c);
                triangles.Add(a); triangles.Add(c); triangles.Add(d);
            }
        int backCenter = vertices.Count;
        vertices.Add(new Vector3(cx[0], cy[0], z[0]));
        int frontCenter = vertices.Count;
        vertices.Add(new Vector3(cx[z.Length - 1], cy[z.Length - 1], z[z.Length - 1]));
        for (int side = 0; side < sides; side++)
        {
            int next = (side + 1) % sides;
            triangles.Add(backCenter); triangles.Add(next); triangles.Add(side);
            int start = (z.Length - 1) * sides;
            triangles.Add(frontCenter); triangles.Add(start + side); triangles.Add(start + next);
        }
        var mesh = new Mesh { name = "Forked Driftwood Limb", vertices = vertices.ToArray(), triangles = triangles.ToArray() };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh BuildSeaweedMesh()
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        const int segments = 5;
        const int fronds = 8;
        for (int blade = 0; blade < fronds; blade++)
        {
            float angle = blade * Mathf.PI * 2f / fronds + 0.2f;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector2 side = new Vector2(-direction.y, direction.x);
            float length = 0.45f + (blade % 3) * 0.10f;
            float width = 0.095f + (blade % 2) * 0.02f;
            Vector2 origin = new Vector2(Mathf.Cos(angle * 2f) * 0.16f, Mathf.Sin(angle * 2f) * 0.15f);
            int start = vertices.Count;
            for (int segment = 0; segment <= segments; segment++)
            {
                float u = segment / (float)segments;
                float bend = u * u * 0.30f;
                Vector2 center = origin + direction * bend;
                float w = width * (1f - u * 0.82f);
                float y = 0.035f + Mathf.Sin(u * Mathf.PI * 0.5f) * length;
                vertices.Add(new Vector3(center.x - side.x * w, y, center.y - side.y * w));
                vertices.Add(new Vector3(center.x + side.x * w, y + 0.015f, center.y + side.y * w));
            }
            for (int segment = 0; segment < segments; segment++)
            {
                int a = start + segment * 2;
                int b = a + 2;
                int c = b + 1;
                int d = a + 1;
                triangles.Add(a); triangles.Add(c); triangles.Add(b);
                triangles.Add(a); triangles.Add(d); triangles.Add(c);
            }
        }
        var mesh = new Mesh { name = "Floating Seaweed Fronds", vertices = vertices.ToArray(), triangles = triangles.ToArray() };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh BuildFoamPatchesMesh()
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        Vector2[] centers = { new Vector2(-0.34f, 0.03f), new Vector2(0.33f, -0.24f), new Vector2(0.04f, 0.37f) };
        Vector2[] radii = { new Vector2(0.22f, 0.15f), new Vector2(0.18f, 0.13f), new Vector2(0.19f, 0.12f) };
        const int sides = 7;
        for (int patch = 0; patch < centers.Length; patch++)
        {
            int start = vertices.Count;
            vertices.Add(new Vector3(centers[patch].x, 0f, centers[patch].y));
            for (int side = 0; side < sides; side++)
            {
                float angle = side * Mathf.PI * 2f / sides;
                float irregular = 0.82f + 0.13f * Mathf.Sin(angle * 3f + patch * 1.7f) + 0.06f * Mathf.Cos(angle * 2f);
                vertices.Add(new Vector3(
                    centers[patch].x + Mathf.Cos(angle) * radii[patch].x * irregular,
                    0.006f * Mathf.Sin(angle * 2f),
                    centers[patch].y + Mathf.Sin(angle) * radii[patch].y * irregular));
            }
            for (int side = 0; side < sides; side++)
            {
                int current = start + 1 + side;
                int next = start + 1 + (side + 1) % sides;
                triangles.Add(start); triangles.Add(next); triangles.Add(current);
            }
        }
        var mesh = new Mesh { name = "Irregular Foam Flecks", vertices = vertices.ToArray(), triangles = triangles.ToArray() };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh BuildBoxMesh(string name)
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        float x = 0.5f, y = 0.5f, z = 0.5f;
        AddQuad(vertices, triangles, new Vector3(-x, -y, z), new Vector3(x, -y, z), new Vector3(x, y, z), new Vector3(-x, y, z));
        AddQuad(vertices, triangles, new Vector3(x, -y, -z), new Vector3(-x, -y, -z), new Vector3(-x, y, -z), new Vector3(x, y, -z));
        AddQuad(vertices, triangles, new Vector3(x, -y, z), new Vector3(x, -y, -z), new Vector3(x, y, -z), new Vector3(x, y, z));
        AddQuad(vertices, triangles, new Vector3(-x, -y, -z), new Vector3(-x, -y, z), new Vector3(-x, y, z), new Vector3(-x, y, -z));
        AddQuad(vertices, triangles, new Vector3(-x, y, z), new Vector3(x, y, z), new Vector3(x, y, -z), new Vector3(-x, y, -z));
        AddQuad(vertices, triangles, new Vector3(-x, -y, -z), new Vector3(x, -y, -z), new Vector3(x, -y, z), new Vector3(-x, -y, z));
        var mesh = new Mesh { name = name, vertices = vertices.ToArray(), triangles = triangles.ToArray() };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static void AddQuad(List<Vector3> vertices, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        int start = vertices.Count;
        vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
        triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
        triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
    }
}
