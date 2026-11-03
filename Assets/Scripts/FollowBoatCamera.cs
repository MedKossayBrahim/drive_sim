using UnityEngine;

public sealed class FollowBoatCamera : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] Vector3 offset = new Vector3(0f, 10.5f, -13f);
    [SerializeField] Vector3 lookAhead = new Vector3(0f, 0.45f, 9f);
    [SerializeField] float followSharpness = 5f;
    [SerializeField] float aimSharpness = 7f;
    [SerializeField] float hitShakeDuration = 0.28f;
    [SerializeField] float hitShakeStrength = 0.24f;
    Camera cameraComponent;
    BoatGameManager gameManager;
    float defaultFov;
    float shakeRemaining;
    float shakeStrength;

    void Awake()
    {
        cameraComponent = GetComponent<Camera>();
        if (cameraComponent == null) cameraComponent = Camera.main;
        if (cameraComponent != null) defaultFov = cameraComponent.fieldOfView;
        gameManager = FindFirstObjectByType<BoatGameManager>();
    }

    public void SetTarget(Transform value) => target = value;
    public void Configure(Transform value, Vector3 followOffset, Vector3 ahead)
    {
        target = value; offset = followOffset; lookAhead = ahead;
    }
    void LateUpdate()
    {
        if (gameManager == null) gameManager = FindFirstObjectByType<BoatGameManager>();
        if (cameraComponent != null && gameManager != null)
        {
            float desiredFov = Mathf.Lerp(gameManager.DifficultySettings.cameraFovStart,
                gameManager.DifficultySettings.cameraFovMax, gameManager.Difficulty);
            cameraComponent.fieldOfView = Mathf.Lerp(cameraComponent.fieldOfView, desiredFov,
                1f - Mathf.Exp(-2.5f * Time.unscaledDeltaTime));
        }
        if (target == null) return;
        Vector3 goal = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, goal, 1f - Mathf.Exp(-followSharpness * Time.deltaTime));
        if (shakeRemaining > 0f)
        {
            shakeRemaining = Mathf.Max(0f, shakeRemaining - Time.unscaledDeltaTime);
            float strength = shakeStrength * Mathf.Clamp01(shakeRemaining / hitShakeDuration);
            float t = Time.unscaledTime * 37f;
            Vector3 shake = new Vector3(Mathf.PerlinNoise(t, 2.4f) - 0.5f,
                Mathf.PerlinNoise(6.1f, t) - 0.5f, 0f) * strength;
            transform.position += shake;
        }
        Quaternion aim = Quaternion.LookRotation(target.position + lookAhead - transform.position, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, aim, 1f - Mathf.Exp(-aimSharpness * Time.deltaTime));
    }

    public void TriggerHitShake()
    {
        shakeRemaining = hitShakeDuration;
        shakeStrength = hitShakeStrength;
    }
}
