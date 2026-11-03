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
    static Material[] orbMaterials;
    static Material[] iconMaterials;

    [SerializeField] BoatPowerUpType type;
    [SerializeField] float driftSpeed = 0.8f;

    Transform boat;
    BoatGameManager gameManager;
    Rigidbody body;
    Transform visual;
    Transform iconBillboard;
    Camera mainCamera;
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
        visual = new GameObject("Floating Power-Up Orb").transform;
        visual.SetParent(transform, false);
        var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        orb.name = "Colored Glow Orb";
        orb.transform.SetParent(visual, false);
        orb.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        orb.transform.localScale = Vector3.one * 0.96f;
        var orbCollider = orb.GetComponent<Collider>();
        if (orbCollider != null) Destroy(orbCollider);
        var orbRenderer = orb.GetComponent<MeshRenderer>();
        orbRenderer.sharedMaterial = orbMaterials[(int)type];
        orbRenderer.shadowCastingMode = ShadowCastingMode.Off;
        orbRenderer.receiveShadows = false;

        var icon = GameObject.CreatePrimitive(PrimitiveType.Quad);
        icon.name = "Billboard " + type + " Icon";
        icon.transform.SetParent(transform, false);
        icon.transform.localPosition = new Vector3(0f, 1.35f, 0f);
        icon.transform.localScale = Vector3.one * 0.8f;
        var iconCollider = icon.GetComponent<Collider>();
        if (iconCollider != null) Destroy(iconCollider);
        var iconRenderer = icon.GetComponent<MeshRenderer>();
        iconRenderer.sharedMaterial = iconMaterials[(int)type];
        iconRenderer.shadowCastingMode = ShadowCastingMode.Off;
        iconRenderer.receiveShadows = false;
        iconBillboard = icon.transform;
        mainCamera = Camera.main;
    }

    void Update()
    {
        if (visual != null)
        {
            float time = Time.time + phase;
            visual.localPosition = new Vector3(0f, Mathf.Sin(time * 2f) * 0.07f, 0f);
            visual.localRotation = Quaternion.Euler(10f, time * 34f, 8f);
            if (iconBillboard != null)
            {
                iconBillboard.localPosition = new Vector3(0f, 1.35f + Mathf.Sin(time * 2f) * 0.07f, 0f);
                if (mainCamera == null) mainCamera = Camera.main;
                if (mainCamera != null)
                    iconBillboard.rotation = Quaternion.LookRotation(mainCamera.transform.position - iconBillboard.position, Vector3.up);
            }
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
        if (orbMaterials != null && orbMaterials.Length == 4 && iconMaterials != null && iconMaterials.Length == 4) return;
        orbMaterials = new Material[4];
        iconMaterials = new Material[4];
        Shader shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        for (int i = 0; i < orbMaterials.Length; i++)
        {
            Color color = PowerUpIconLibrary.GetColor((BoatPowerUpType)i);
            orbMaterials[i] = new Material(shader)
            {
                name = "Boat Power-Up " + ((BoatPowerUpType)i),
                color = color,
                enableInstancing = true
            };
            if (orbMaterials[i].HasProperty("_Metallic")) orbMaterials[i].SetFloat("_Metallic", 0.12f);
            if (orbMaterials[i].HasProperty("_Glossiness")) orbMaterials[i].SetFloat("_Glossiness", 0.72f);
            if (orbMaterials[i].HasProperty("_EmissionColor"))
            {
                orbMaterials[i].EnableKeyword("_EMISSION");
                orbMaterials[i].SetColor("_EmissionColor", color * 0.72f);
            }
            iconMaterials[i] = PowerUpIconLibrary.CreateBillboardMaterial((BoatPowerUpType)i);
        }
    }
}
