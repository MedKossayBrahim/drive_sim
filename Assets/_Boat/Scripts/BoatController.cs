using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public sealed class BoatController : MonoBehaviour
{
    const float FallbackCruiseSpeed = 11f;
    const float FallbackBoostedSpeed = 19.5f;
    [Header("Speed")]
    [SerializeField] float brakeSpeed = 2.5f;
    [SerializeField] float acceleration = 9.5f;
    [SerializeField] float braking = 10f;
    [Header("Steering")]
    [SerializeField] float maxLateralSpeed = 7f;
    [SerializeField] float steeringResponse = 18f;
    [SerializeField] float turnAngle = 24f;
    [SerializeField] float viewportEdgeMargin = 0.55f;
    [SerializeField] float turboSpeedMultiplier = 1.65f;
    [Header("Boat feel")]
    [SerializeField] float bobHeight = 0.045f;
    [SerializeField] float bobFrequency = 2.2f;
    [SerializeField] float rollAngle = 7f;
    static readonly Collider[] turboSweepHits = new Collider[24];
    Rigidbody body;
    BoxCollider hullCollider;
    Transform visuals;
    Camera followCamera;
    BoatGameManager gameManager;
    GameObject shieldBubble;
    LineRenderer[] magnetRings;
    GameObject doubleScoreBillboard;
    LineRenderer turboTrail;
    Camera effectCamera;
    Vector3 initialPosition;
    Quaternion initialRotation;
    Vector3 visualsBasePosition;
    float speed;
    float steering;
    float lateralSpeed;
    bool controlsEnabled = true;
    bool invulnerableFlash;
    bool turboActive;
    float horizontalLimitCurrent;
    public float CurrentSpeed => speed;
    public float Steering => steering;
    public float HorizontalLimit => horizontalLimitCurrent;
    public bool IsTurboActive => turboActive;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        GetComponent<Collider>().isTrigger = true;
        hullCollider = GetComponent<BoxCollider>();
        followCamera = Camera.main;
        effectCamera = followCamera;
        gameManager = FindFirstObjectByType<BoatGameManager>();
        initialPosition = body.position;
        initialRotation = body.rotation;
        speed = FallbackCruiseSpeed;
        var visualTransform = transform.Find("BoatVisuals");
        if (visualTransform != null) { visuals = visualTransform; visualsBasePosition = visuals.localPosition; }
    }

    void Update()
    {
        if (controlsEnabled)
        {
            float targetSteering = 0f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) targetSteering -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) targetSteering += 1f;
            if (Application.isMobilePlatform && SystemInfo.supportsAccelerometer)
            {
                float tilt = Input.acceleration.x;
                const float deadZone = 0.075f;
                if (Mathf.Abs(tilt) > deadZone)
                    targetSteering = Mathf.Clamp((tilt - Mathf.Sign(tilt) * deadZone) / 0.34f, -1f, 1f);
            }
            steering = Mathf.MoveTowards(steering, targetSteering, steeringResponse * Time.deltaTime);
        }
        if (visuals != null)
        {
            float bob = Mathf.Sin(Time.time * bobFrequency) * bobHeight;
            visuals.localPosition = visualsBasePosition + Vector3.up * bob;
            visuals.localRotation = Quaternion.Euler(0f, 0f, -steering * rollAngle);
            if (invulnerableFlash) visuals.gameObject.SetActive(Mathf.FloorToInt(Time.time * 12f) % 2 == 0);
            else if (!visuals.gameObject.activeSelf) visuals.gameObject.SetActive(true);
        }
        UpdatePowerUpVisuals();
    }

    void FixedUpdate()
    {
        if (!controlsEnabled) return;
        bool accelerateInput = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
        bool brakeInput = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);
        float difficultyCruiseSpeed = gameManager == null ? FallbackCruiseSpeed : gameManager.CruiseSpeed;
        float difficultyMaxSpeed = gameManager == null ? FallbackBoostedSpeed : gameManager.BoostedSpeed;
        float accel = gameManager != null && gameManager.DifficultySettings != null
            ? gameManager.DifficultySettings.acceleration
            : acceleration;
        float targetSpeed = accelerateInput ? difficultyMaxSpeed : difficultyCruiseSpeed;
        if (brakeInput) targetSpeed = brakeSpeed;
        if (turboActive) targetSpeed *= turboSpeedMultiplier;
        speed = Mathf.MoveTowards(speed, targetSpeed, (brakeInput ? braking : accel) * Time.fixedDeltaTime);
        lateralSpeed = Mathf.MoveTowards(lateralSpeed, steering * maxLateralSpeed, steeringResponse * Time.fixedDeltaTime);

        Vector3 next = body.position + new Vector3(lateralSpeed, 0f, speed) * Time.fixedDeltaTime;
        float playHalf = gameManager != null && gameManager.DifficultySettings != null
            ? gameManager.DifficultySettings.playAreaHalfWidth
            : 2.94f;
        float minimumX = -playHalf;
        float maximumX = playHalf;
        if (TryGetCameraHorizontalBounds(out float cameraMin, out float cameraMax))
        {
            minimumX = Mathf.Max(cameraMin, -playHalf);
            maximumX = Mathf.Min(cameraMax, playHalf);
            if (minimumX > maximumX)
            {
                float center = (cameraMin + cameraMax) * 0.5f;
                minimumX = maximumX = Mathf.Clamp(center, -playHalf, playHalf);
            }
        }
        next.x = Mathf.Clamp(next.x, minimumX, maximumX);
        horizontalLimitCurrent = Mathf.Max(0f, (maximumX - minimumX) * 0.5f);
        if (Mathf.Abs(next.x - body.position.x) < 0.001f) lateralSpeed = 0f;

        if (turboActive) SweepPickupsAndHazards(body.position, next);
        body.MovePosition(next);
        Quaternion targetRotation = Quaternion.Euler(0f, -steering * turnAngle, 0f);
        body.MoveRotation(Quaternion.Slerp(body.rotation, targetRotation, 1f - Mathf.Exp(-7f * Time.fixedDeltaTime)));
    }

    bool TryGetCameraHorizontalBounds(out float minimumX, out float maximumX)
    {
        minimumX = maximumX = body.position.x;
        if (followCamera == null || !followCamera.isActiveAndEnabled) followCamera = Camera.main;
        if (followCamera == null) return false;

        Vector3 boatViewport = followCamera.WorldToViewportPoint(body.position);
        if (boatViewport.z <= 0f) return false;
        float viewportY = Mathf.Clamp(boatViewport.y, 0.02f, 0.98f);
        Plane waterline = new Plane(Vector3.up, new Vector3(0f, body.position.y, 0f));
        Ray leftRay = followCamera.ViewportPointToRay(new Vector3(0f, viewportY, 0f));
        Ray rightRay = followCamera.ViewportPointToRay(new Vector3(1f, viewportY, 0f));
        if (!waterline.Raycast(leftRay, out float leftDistance) ||
            !waterline.Raycast(rightRay, out float rightDistance) ||
            leftDistance < 0f || rightDistance < 0f)
            return false;

        Vector3 left = leftRay.GetPoint(leftDistance);
        Vector3 right = rightRay.GetPoint(rightDistance);
        float hullHalfWidth = hullCollider != null ? hullCollider.bounds.extents.x : 0f;
        minimumX = left.x + hullHalfWidth + viewportEdgeMargin;
        maximumX = right.x - hullHalfWidth - viewportEdgeMargin;
        if (minimumX > maximumX)
        {
            float center = (left.x + right.x) * 0.5f;
            minimumX = maximumX = center;
        }
        return true;
    }

    void SweepPickupsAndHazards(Vector3 current, Vector3 next)
    {
        if (hullCollider == null || gameManager == null) return;
        Vector3 delta = next - current;
        int samples = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / 0.15f));
        Vector3 halfExtents = Vector3.Scale(hullCollider.size, transform.lossyScale) * 0.5f;
        Vector3 centerOffset = body.rotation * Vector3.Scale(hullCollider.center, transform.lossyScale);

        for (int i = 1; i <= samples; i++)
        {
            Vector3 center = current + delta * (i / (float)samples) + centerOffset;
            int hitCount = Physics.OverlapBoxNonAlloc(center, halfExtents, turboSweepHits, body.rotation,
                Physics.AllLayers, QueryTriggerInteraction.Collide);
            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                Collider hit = turboSweepHits[hitIndex];
                if (hit == null || hit == hullCollider || hit.attachedRigidbody == body) continue;

                if (hit.GetComponentInParent<Obstacle>() != null)
                    gameManager.HitObstacle();

                RouteToken token = hit.GetComponentInParent<RouteToken>();
                if (token != null) token.Collect(this);

                PowerUpPickup powerUp = hit.GetComponentInParent<PowerUpPickup>();
                if (powerUp != null) powerUp.Collect(this);
            }
        }
    }
    public void SetControlsEnabled(bool enabled) => controlsEnabled = enabled;
    public void SetInvulnerableFlash(bool enabled) => invulnerableFlash = enabled;

    public void SetTurboActive(bool active)
    {
        if (turboActive == active) return;
        turboActive = active;
        if (body != null)
            body.collisionDetectionMode = active ? CollisionDetectionMode.ContinuousDynamic : CollisionDetectionMode.Continuous;
    }

    public void SetShieldVisible(bool visible)
    {
        if (visible) EnsureShieldBubble();
        if (shieldBubble != null && shieldBubble.activeSelf != visible)
            shieldBubble.SetActive(visible);
    }

    public void SetPowerUpVisuals(bool magnet, bool doubleScore, bool turbo)
    {
        if (magnet && magnetRings == null) CreateMagnetRings();
        if (doubleScore && doubleScoreBillboard == null) CreateDoubleScoreBillboard();
        if (turbo && turboTrail == null) CreateTurboTrail();
        if (magnetRings != null)
            for (int i = 0; i < magnetRings.Length; i++) magnetRings[i].gameObject.SetActive(magnet);
        if (doubleScoreBillboard != null) doubleScoreBillboard.SetActive(doubleScore);
        if (turboTrail != null) turboTrail.gameObject.SetActive(turbo);
    }

    void CreateMagnetRings()
    {
        magnetRings = new LineRenderer[2];
        Color color = PowerUpIconLibrary.GetColor(BoatPowerUpType.Magnet);
        for (int r = 0; r < magnetRings.Length; r++)
        {
            var ring = new GameObject("Magnet Field Ring " + r);
            ring.transform.SetParent(transform, false);
            ring.transform.localPosition = new Vector3(0f, 0.72f + r * 0.11f, 0f);
            magnetRings[r] = ring.AddComponent<LineRenderer>();
            var line = magnetRings[r];
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 49;
            line.widthMultiplier = 0.045f;
            line.numCapVertices = 2;
            line.material = CreateEffectMaterial("Magnet Field", color);
            line.startColor = line.endColor = color;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            float radius = r == 0 ? 0.90f : 1.08f;
            for (int i = 0; i < line.positionCount; i++)
            {
                float angle = (i / 48f) * Mathf.PI * 2f;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }
            ring.SetActive(false);
        }
    }

    void CreateDoubleScoreBillboard()
    {
        doubleScoreBillboard = GameObject.CreatePrimitive(PrimitiveType.Quad);
        doubleScoreBillboard.name = "Double Score x2";
        doubleScoreBillboard.transform.SetParent(transform, false);
        doubleScoreBillboard.transform.localPosition = new Vector3(0f, 2.2f, 0f);
        doubleScoreBillboard.transform.localScale = new Vector3(0.76f, 0.76f, 1f);
        var collider = doubleScoreBillboard.GetComponent<Collider>();
        if (collider != null) Destroy(collider);
        var renderer = doubleScoreBillboard.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = PowerUpIconLibrary.CreateBillboardMaterial(BoatPowerUpType.DoubleScore);
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        doubleScoreBillboard.SetActive(false);
    }

    void CreateTurboTrail()
    {
        var trailObject = new GameObject("Turbo Wake Trail");
        trailObject.transform.SetParent(transform, false);
        trailObject.transform.localPosition = new Vector3(0f, 0.34f, -1.45f);
        turboTrail = trailObject.AddComponent<LineRenderer>();
        turboTrail.useWorldSpace = false;
        turboTrail.loop = false;
        turboTrail.positionCount = 12;
        turboTrail.widthMultiplier = 0.52f;
        turboTrail.material = CreateEffectMaterial("Turbo Wake", new Color(1f, 0.34f, 0.09f));
        turboTrail.shadowCastingMode = ShadowCastingMode.Off;
        turboTrail.receiveShadows = false;
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(new Color(1f, 0.82f, 0.28f), 0f), new GradientColorKey(new Color(1f, 0.19f, 0.06f), 1f) },
            new[] { new GradientAlphaKey(0.82f, 0f), new GradientAlphaKey(0.28f, 0.68f), new GradientAlphaKey(0f, 1f) });
        turboTrail.colorGradient = gradient;
        for (int i = 0; i < turboTrail.positionCount; i++)
        {
            float t = i / (float)(turboTrail.positionCount - 1);
            turboTrail.SetPosition(i, new Vector3(Mathf.Sin(t * Mathf.PI * 3f) * 0.12f, 0f, -t * 2.8f));
        }
        trailObject.SetActive(false);
    }

    static Material CreateEffectMaterial(string name, Color color)
    {
        Shader shader = BoatRenderSetup.Unlit();
        var material = new Material(shader) { name = name };
        BoatRenderSetup.Paint(material, color);
        if (material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 0.65f);
        }
        return material;
    }

    void UpdatePowerUpVisuals()
    {
        if (doubleScoreBillboard == null || !doubleScoreBillboard.activeSelf) return;
        if (effectCamera == null) effectCamera = Camera.main;
        if (effectCamera != null)
            doubleScoreBillboard.transform.rotation = Quaternion.LookRotation(
                effectCamera.transform.position - doubleScoreBillboard.transform.position, Vector3.up);
    }

    void EnsureShieldBubble()
    {
        if (shieldBubble != null) return;
        shieldBubble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        shieldBubble.name = "Shield Bubble";
        shieldBubble.transform.SetParent(transform, false);
        shieldBubble.transform.localPosition = new Vector3(0f, 0.22f, 0f);
        shieldBubble.transform.localScale = new Vector3(2.25f, 1.55f, 4.05f);
        var collider = shieldBubble.GetComponent<Collider>();
        if (collider != null) collider.enabled = false;

        var renderer = shieldBubble.GetComponent<MeshRenderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        Shader shader = BoatRenderSetup.LitOrUnlit();
        var material = new Material(shader) { name = "Shield Bubble Translucent" };
        BoatRenderSetup.Paint(material, new Color(0.24f, 0.78f, 0.93f, 0.19f));
        if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 3f);
        if (material.HasProperty("_SrcBlend")) material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend")) material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        if (material.HasProperty("_ZWrite")) material.SetInt("_ZWrite", 0);
        material.SetOverrideTag("RenderType", "Transparent");
        material.DisableKeyword("_ALPHATEST_ON");
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.renderQueue = (int)RenderQueue.Transparent;
        renderer.sharedMaterial = material;
        shieldBubble.SetActive(false);
    }
    public void ResetBoat()
    {
        if (body == null) body = GetComponent<Rigidbody>();
        body.position = initialPosition;
        body.rotation = initialRotation;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        speed = gameManager == null ? FallbackCruiseSpeed : gameManager.CruiseSpeed; steering = 0f; lateralSpeed = 0f;
        controlsEnabled = true; invulnerableFlash = false; turboActive = false;
        body.collisionDetectionMode = CollisionDetectionMode.Continuous;
        if (visuals != null) { visuals.localPosition = visualsBasePosition; visuals.localRotation = Quaternion.identity; visuals.gameObject.SetActive(true); }
        SetPowerUpVisuals(false, false, false);
    }
}
