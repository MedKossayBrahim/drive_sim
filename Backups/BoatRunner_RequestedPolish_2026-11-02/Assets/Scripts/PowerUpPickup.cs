using UnityEngine;
using UnityEngine.Rendering;

public enum BoatPowerUpType
{
    Shield,
    Magnet,
    DoubleScore,
    Turbo
}

[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public sealed class PowerUpPickup : MonoBehaviour
{
    static readonly Color[] typeColors =
    {
        new Color(0.18f, 0.72f, 0.92f),
        new Color(0.63f, 0.35f, 0.90f),
        new Color(0.78f, 0.83f, 0.20f),
        new Color(0.98f, 0.30f, 0.13f)
    };
    static Material[] materials;
    static Material markMaterial;

    [SerializeField] BoatPowerUpType type;
    [SerializeField] float driftSpeed = 0.8f;

    Transform boat;
    BoatGameManager gameManager;
    Rigidbody body;
    Transform visual;
    float phase;
    bool collected;

    public BoatPowerUpType Type => type;

    public void Configure(BoatPowerUpType powerUpType, Transform player, BoatGameManager manager, float drift)
    {
        type = powerUpType;
        boat = player;
        gameManager = manager;
        driftSpeed = drift;
        phase = Random.value * Mathf.PI * 2f;
        body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        var trigger = GetComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, 0.34f, 0f);
        trigger.radius = 0.85f;

        EnsureMaterials();
        visual = RouteToken.CreateVisual(transform, materials[(int)type], markMaterial, "Power-Up Visual");
    }

    void Update()
    {
        if (visual != null)
        {
            float time = Time.time + phase;
            visual.localPosition = new Vector3(0f, 0.1f + Mathf.Sin(time * 2f) * 0.065f, 0f);
            visual.localRotation = Quaternion.Euler(0f, time * 48f, 0f);
        }

        if (boat != null && transform.position.z < boat.position.z - 22f)
            Destroy(gameObject);
    }

    void FixedUpdate()
    {
        if (body == null || gameManager == null || !gameManager.IsPlaying) return;
        Vector3 position = body.position + Vector3.back * driftSpeed * Time.fixedDeltaTime;
        position.y = Mathf.Sin((Time.time + phase) * 1.4f) * 0.025f;
        body.MovePosition(position);
    }

    void OnTriggerEnter(Collider other)
    {
        var collector = other.GetComponentInParent<BoatController>();
        if (collector != null) Collect(collector);
    }

    public void Collect(BoatController collector)
    {
        if (collected || collector == null || gameManager == null || !gameManager.IsPlaying) return;
        collected = true;
        gameManager.ActivatePowerUp(type);
        Destroy(gameObject);
    }

    static void EnsureMaterials()
    {
        if (materials != null && materials.Length == typeColors.Length && markMaterial != null) return;
        materials = new Material[typeColors.Length];
        Shader shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        for (int i = 0; i < materials.Length; i++)
        {
            materials[i] = new Material(shader)
            {
                name = "Boat Power-Up " + ((BoatPowerUpType)i),
                color = typeColors[i],
                enableInstancing = true
            };
            if (materials[i].HasProperty("_Metallic")) materials[i].SetFloat("_Metallic", 0.18f);
            if (materials[i].HasProperty("_Glossiness")) materials[i].SetFloat("_Glossiness", 0.62f);
        }

        markMaterial = new Material(shader)
        {
            name = "Boat Power-Up Mark",
            color = new Color(0.92f, 0.97f, 0.94f),
            enableInstancing = true
        };
    }
}