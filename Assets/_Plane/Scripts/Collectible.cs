using UnityEngine;

namespace Project.Plane
{
    /// <summary>Pièce sans physique ajoutée : seule la représentation visuelle tourne et oscille.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(SphereCollider))]
    public sealed class Collectible : MonoBehaviour
    {
        [SerializeField, Min(0)] private int value = 25;
        [SerializeField] private Transform visual;
        [SerializeField] private ScoreManager score;
        [SerializeField] private PlaneInput aircraft;
        [SerializeField] private PlaneHealth health;
        [SerializeField, Min(0f)] private float spinDegreesPerSecond = 90f;
        [SerializeField, Min(0f)] private float bobAmplitude = .35f;
        [SerializeField, Min(0f)] private float bobFrequency = .7f;
        [SerializeField] private float phase;
        [SerializeField, Min(.05f)] private float pickupDuration = .2f;
        [SerializeField] private AudioSource pickupAudio;
        private SphereCollider trigger;
        private FlightPowerUps bonuses;
        private Vector3 basePosition, baseScale;
        private float collectedAt;
        public bool IsCollected { get; private set; }
        private void Awake()
        {
            trigger = GetComponent<SphereCollider>();
            RestrictToPlane(trigger);
            if (visual != null) { basePosition = visual.localPosition; baseScale = visual.localScale; }
        }
        /// <summary>Overrides locaux : aucune modification de la matrice globale ou création de layer.</summary>
        public static void RestrictToPlane(Collider collider)
        {
            int mask = LayerMask.GetMask("Plane");
            collider.isTrigger = true;
            collider.includeLayers = mask;
            collider.excludeLayers = ~mask;
            collider.layerOverridePriority = 100;
        }
        public void Bind(ScoreManager owner, PlaneInput input, PlaneHealth aircraftHealth, FlightPowerUps powerUps = null)
        { score = owner; aircraft = input; health = aircraftHealth; bonuses = powerUps; }
        private void Update()
        {
            if (visual == null) return;
            if (IsCollected)
            {
                float t = Mathf.Clamp01((Time.time - collectedAt) / Mathf.Max(.05f, pickupDuration));
                visual.localScale = baseScale * (1f + t * 1.5f) * (1f - t);
                if (t >= 1f) gameObject.SetActive(false);
                return;
            }
            visual.Rotate(Vector3.up, spinDegreesPerSecond * Time.deltaTime, Space.Self);
            visual.localPosition = basePosition + Vector3.up * (Mathf.Sin(Time.time * bobFrequency * 2f * Mathf.PI + phase) * bobAmplitude);
        }
        private void OnTriggerEnter(Collider other) => TryCollect(other);
        public bool TryCollect(Collider other)
        {
            if (IsCollected || !isActiveAndEnabled || score == null || aircraft == null || health == null ||
                !aircraft.IsMissionStarted || aircraft.IsPaused || health.IsCrashed) return false;
            Rigidbody body = other.attachedRigidbody;
            if (body == null || body.gameObject != aircraft.gameObject || body.gameObject.layer != LayerMask.NameToLayer("Plane")) return false;
            // L'état précède le score : plusieurs colliders de l'avion ne ramassent jamais deux fois la même pièce.
            IsCollected = true;
            collectedAt = Time.time;
            trigger.enabled = false;
            int multiplier = bonuses != null ? bonuses.CoinMultiplier : 1;
            score.CollectCoin((int)System.Math.Min(int.MaxValue, (long)value * multiplier));
            if (pickupAudio != null && pickupAudio.clip != null) pickupAudio.Play();
            return true;
        }
#if UNITY_EDITOR
        public void Configure(Transform mesh, int points, float animationPhase)
        { visual = mesh; value = Mathf.Max(0, points); phase = animationPhase; }
#endif
    }
}
