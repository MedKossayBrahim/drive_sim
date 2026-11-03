using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public sealed class RouteToken : MonoBehaviour
{
    static Mesh medallionMesh;
    static Mesh ringMesh;
    static Mesh compassMesh;

    Transform boat;
    BoatGameManager gameManager;
    Rigidbody body;
    Transform visual;
    float driftSpeed;
    bool collected;

    public void Configure(Transform player, BoatGameManager manager, float drift, Material gold, Material face)
    {
        boat = player;
        gameManager = manager;
        driftSpeed = drift;
        body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;

        var trigger = GetComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, 0.32f, 0f);
        trigger.radius = 0.75f;

        visual = CreateVisual(transform, gold, face);
        visual.localPosition = new Vector3(0f, 0.1f, 0f);
    }

    void Update()
    {
        if (visual == null) return;
        visual.localPosition = new Vector3(0f, 0.1f + Mathf.Sin(Time.time * 2.1f) * 0.055f, 0f);
        visual.localRotation = Quaternion.Euler(0f, Time.time * 22f, 0f);
        if (boat != null && transform.position.z < boat.position.z - 22f)
            Destroy(gameObject);
    }

    void FixedUpdate()
    {
        if (body == null || gameManager == null || !gameManager.IsPlaying) return;
        Vector3 next = body.position + Vector3.back * driftSpeed * Time.fixedDeltaTime;
        IsBeingMagnetized = false;
        if (gameManager.HasMagnet && boat != null)
        {
            Vector3 target = boat.position;
            target.y = next.y;
            Vector3 planarOffset = target - next;
            planarOffset.y = 0f;
            if (planarOffset.sqrMagnitude <= gameManager.MagnetRadius * gameManager.MagnetRadius)
            {
                next += planarOffset.normalized * Mathf.Min(gameManager.MagnetPullSpeed * Time.fixedDeltaTime, planarOffset.magnitude);
                IsBeingMagnetized = true;
            }
        }
        body.MovePosition(next);
    }

    public bool IsBeingMagnetized { get; private set; }

    void OnTriggerEnter(Collider other)
    {
        var collector = other.GetComponentInParent<BoatController>();
        if (collector != null) Collect(collector);
    }

    public void Collect(BoatController collector)
    {
        if (collected || collector == null || gameManager == null || !gameManager.IsPlaying) return;
        collected = true;
        gameManager.CollectToken();
        Destroy(gameObject);
    }

    public static Transform CreateVisual(Transform parent, Material outer, Material symbol, string visualName = "Marine Token Visual")
    {
        EnsureMeshes();
        var root = new GameObject(visualName).transform;
        root.SetParent(parent, false);
        AddMesh(root, "Token Rim", ringMesh, outer);
        AddMesh(root, "Token Face", medallionMesh, outer);
        AddMesh(root, "Compass Mark", compassMesh, symbol);
        return root;
    }

    static void AddMesh(Transform parent, string name, Mesh mesh, Material material)
    {
        var part = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        part.transform.SetParent(parent, false);
        part.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = part.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    static void EnsureMeshes()
    {
        if (medallionMesh == null) medallionMesh = BuildMedallion();
        if (ringMesh == null) ringMesh = BuildRing();
        if (compassMesh == null) compassMesh = BuildCompass();
    }

    static Mesh BuildMedallion()
    {
        const int segments = 24;
        const float radius = 0.38f;
        const float bottom = 0.055f;
        const float top = 0.16f;
        var vertices = new Vector3[segments * 2 + 2];
        var triangles = new int[segments * 12];
        int topCenter = 0;
        int topStart = 1;
        int bottomCenter = segments + 1;
        int bottomStart = segments + 2;
        vertices[topCenter] = new Vector3(0f, top, 0f);
        vertices[bottomCenter] = new Vector3(0f, bottom, 0f);
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            float x = Mathf.Cos(angle) * radius;
            float z = Mathf.Sin(angle) * radius;
            vertices[topStart + i] = new Vector3(x, top, z);
            vertices[bottomStart + i] = new Vector3(x, bottom, z);
        }

        int t = 0;
        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            triangles[t++] = topCenter; triangles[t++] = topStart + next; triangles[t++] = topStart + i;
            triangles[t++] = bottomCenter; triangles[t++] = bottomStart + i; triangles[t++] = bottomStart + next;
            triangles[t++] = topStart + i; triangles[t++] = topStart + next; triangles[t++] = bottomStart + next;
            triangles[t++] = topStart + i; triangles[t++] = bottomStart + next; triangles[t++] = bottomStart + i;
        }
        var mesh = new Mesh { name = "Marine Token Medallion", vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh BuildRing()
    {
        const int majorSegments = 24;
        const int minorSegments = 8;
        const float majorRadius = 0.46f;
        const float minorRadius = 0.09f;
        var vertices = new Vector3[majorSegments * minorSegments];
        var triangles = new int[vertices.Length * 6];
        for (int i = 0; i < majorSegments; i++)
        {
            float u = i * Mathf.PI * 2f / majorSegments;
            for (int j = 0; j < minorSegments; j++)
            {
                float v = j * Mathf.PI * 2f / minorSegments;
                float radius = majorRadius + Mathf.Cos(v) * minorRadius;
                vertices[i * minorSegments + j] = new Vector3(
                    Mathf.Cos(u) * radius,
                    0.13f + Mathf.Sin(v) * minorRadius,
                    Mathf.Sin(u) * radius);
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
        var mesh = new Mesh { name = "Marine Token Rim", vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    static Mesh BuildCompass()
    {
        const int points = 8;
        var vertices = new Vector3[points + 1];
        var triangles = new int[points * 3];
        vertices[0] = new Vector3(0f, 0.205f, 0f);
        for (int i = 0; i < points; i++)
        {
            float angle = i * Mathf.PI / 4f;
            float radius = i % 2 == 0 ? 0.28f : 0.10f;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle) * radius, 0.205f, Mathf.Sin(angle) * radius);
        }
        for (int i = 0; i < points; i++)
        {
            int next = (i + 1) % points;
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = next + 1;
            triangles[i * 3 + 2] = i + 1;
        }
        var mesh = new Mesh { name = "Marine Compass Token Mark", vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}