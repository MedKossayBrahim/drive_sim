using UnityEngine;

namespace Project.Plane
{
    internal enum FlightBonusKind { Magnet, DoublePoints }

    /// <summary>Gère les bonus du vol et attire les pièces sans modifier la trajectoire de l'avion.</summary>
    [DisallowMultipleComponent]
    public sealed class FlightPowerUps : MonoBehaviour
    {
        [SerializeField, Min(.1f)] private float magnetSeconds = 4f;
        [SerializeField, Min(.1f)] private float doublePointsSeconds = 6f;
        [SerializeField, Min(1f)] private float magnetRadius = 85f;
        [SerializeField, Min(1f)] private float attractionSpeed = 145f;
        [SerializeField, Min(1f)] private float returnSpeed = 65f;
        private PlaneInput aircraft;
        private PlaneHealth health;
        private Rigidbody body;
        private Collider aircraftCollider;
        private Transform pickupRoot;
        private Collectible[] coins;
        private Vector3[] home;
        private int groundMask;
        private float magnetUntil, doubleUntil;
        private bool movedCoins;

        public float MagnetRemaining => IsPlaying ? Mathf.Max(0f, magnetUntil - Time.time) : 0f;
        public float DoublePointsRemaining => IsPlaying ? Mathf.Max(0f, doubleUntil - Time.time) : 0f;
        public int CoinMultiplier => DoublePointsRemaining > 0f ? 2 : 1;
        internal float LastActivationTime { get; private set; } = float.NegativeInfinity;
        internal FlightBonusKind LastActivatedBonus { get; private set; }
        private bool IsPlaying => aircraft != null && aircraft.isActiveAndEnabled && aircraft.IsMissionStarted &&
            health != null && !health.IsCrashed;

        internal void Configure(PlaneInput input, PlaneHealth planeHealth, Transform course, Collectible[] pieces)
        {
            aircraft = input;
            health = planeHealth;
            aircraft.OnResetCompleted += ResetTimers;
            body = input.GetComponent<Rigidbody>();
            foreach (var candidate in body.GetComponentsInChildren<Collider>())
            {
                if (candidate.enabled && !candidate.isTrigger && candidate.attachedRigidbody == body)
                {
                    aircraftCollider = candidate;
                    break;
                }
            }
            coins = pieces;
            home = new Vector3[coins.Length];
            for (int i = 0; i < coins.Length; i++)
                if (coins[i] != null) home[i] = coins[i].transform.position;
            groundMask = LayerMask.GetMask("Ground");
            pickupRoot = new GameObject("FlightBonusPickups").transform;
            pickupRoot.SetParent(course, false);
            PlaceBonus(.09f, FlightBonusKind.Magnet);
            PlaceBonus(.31f, FlightBonusKind.DoublePoints);
            PlaceBonus(.53f, FlightBonusKind.Magnet);
            PlaceBonus(.77f, FlightBonusKind.DoublePoints);
        }

        private void PlaceBonus(float progress, FlightBonusKind kind)
        {
            int start = Mathf.Clamp(Mathf.RoundToInt((coins.Length - 2) * progress), 0, coins.Length - 2);
            // Utilise un espace libre entre deux pièces, avec une marge autour des groupes d'anneaux.
            for (int i = start; i < Mathf.Min(start + 5, coins.Length - 1); i++)
            {
                if (coins[i] == null || coins[i + 1] == null) continue;
                Vector3 segment = home[i + 1] - home[i];
                if (segment.sqrMagnitude < 42f * 42f || Physics.Linecast(home[i], home[i + 1], groundMask, QueryTriggerInteraction.Ignore)) continue;
                var renderer = coins[i].GetComponentInChildren<Renderer>();
                if (renderer == null) continue;
                FlightBonusPickup.Create(pickupRoot, home[i] + segment * .5f, segment, kind, this, renderer.sharedMaterial);
                return;
            }
        }

        internal bool TryActivate(FlightBonusKind kind, Collider other)
        {
            if (!IsPlaying || aircraft.IsPaused || other.attachedRigidbody != body) return false;
            // Un second bonus du même type relance le compteur.
            if (kind == FlightBonusKind.Magnet) magnetUntil = Time.time + magnetSeconds;
            else doubleUntil = Time.time + doublePointsSeconds;
            LastActivationTime = Time.time;
            LastActivatedBonus = kind;
            return true;
        }

        private void ResetTimers()
        {
            magnetUntil = doubleUntil = 0f;
            LastActivationTime = float.NegativeInfinity;
        }

        private void OnDestroy()
        {
            if (aircraft != null) aircraft.OnResetCompleted -= ResetTimers;
        }

        private void FixedUpdate()
        {
            if (!IsPlaying || aircraft.IsPaused || coins == null) return;
            bool magnetic = MagnetRemaining > 0f;
            if (!magnetic && !movedCoins) return;
            movedCoins = false;
            Vector3 center = body.worldCenterOfMass;
            for (int i = 0; i < coins.Length; i++)
            {
                var coin = coins[i];
                if (coin == null || !coin.isActiveAndEnabled || coin.IsCollected) continue;
                Vector3 position = coin.transform.position;
                bool attract = magnetic && (position - center).sqrMagnitude <= magnetRadius * magnetRadius &&
                    !Physics.Linecast(position, center, groundMask, QueryTriggerInteraction.Ignore);
                Vector3 target = attract ? center : home[i];
                if ((position - target).sqrMagnitude < .0001f && !attract) continue;
                Vector3 next = Vector3.MoveTowards(position, target, (attract ? attractionSpeed : returnSpeed) * Time.fixedDeltaTime);
                coin.transform.position = next;
                if (attract && aircraftCollider != null && (next - center).sqrMagnitude < 25f)
                    coin.TryCollect(aircraftCollider);
                movedCoins |= (next - home[i]).sqrMagnitude > .0001f;
            }
        }
    }
}
