using System;
using UnityEngine;

namespace Project.Plane
{
    /// <summary>Télémétrie passive, sans AddForce ni modification du Rigidbody. Z+ est le nord de la scène.</summary>
    [DefaultExecutionOrder(-200), DisallowMultipleComponent, RequireComponent(typeof(PlaneController), typeof(Rigidbody))]
    public sealed class PlaneSensors : MonoBehaviour
    {
        [Header("Références")]
        [SerializeField] private CheckpointManager checkpointManager = null;
        [SerializeField] private bool findManagerInSameScene = true;
        [SerializeField] private Transform altitudeProbe = null;

        [Header("Altitudes — mètres")]
        [SerializeField] private float seaLevelWorldY = 0f;
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField, Min(1f)] private float maximumGroundDistance = 10000f;
        [SerializeField, Min(0.02f), Tooltip("Période des requêtes AGL et obstacles, adaptée au mobile.")]
        private float queryInterval = 0.1f;
        [SerializeField, Min(1f)] private float teleportThreshold = 20f;

        [Header("Carburant — litres, secondes")]
        [SerializeField] private bool simulateFuel = true;
        [SerializeField] private bool engineEnabled = true;
        [SerializeField, Min(0.1f)] private float fuelCapacityLiters = 120f;
        [SerializeField, Range(0f, 1f)] private float initialFuelFraction = 1f;
        [SerializeField, Min(0f)] private float fuelBurnLitersPerSecond = 0.08f;
        [SerializeField, Range(0f, 1f)] private float lowFuelFraction = 0.15f;

        [Header("Prévision TERRAIN — trajectoire rectiligne")]
        [SerializeField] private LayerMask obstacleMask = ~0;
        [SerializeField] private Vector3 obstacleProbeOffset = new Vector3(0f, 0.2f, 4f);
        [SerializeField, Min(0.05f)] private float obstacleRadius = 1f;
        [SerializeField, Range(1f, 10f)] private float warningHorizonSeconds = 5f;
        [SerializeField, Min(0.1f)] private float minimumWarningSpeed = 5f;
        [SerializeField, Min(1f)] private float maximumObstacleDistance = 1000f;
        [SerializeField, Tooltip("Vise la vitesse réelle ; désactiver pour viser uniquement le nez.")]
        private bool followVelocityForWarning = true;

        [Header("Bruit des instruments — les alarmes et le carburant restent exacts")]
        [SerializeField] private bool measurementNoise = false;
        [SerializeField, Min(0f)] private float speedNoiseMetersPerSecond = 0.15f;
        [SerializeField, Min(0f)] private float altitudeNoiseMeters = 0.25f;
        [SerializeField, Min(0f)] private float angleNoiseDegrees = 0.2f;
        [SerializeField, Min(0f)] private float gNoise = 0.015f;
        [SerializeField, Min(0f)] private float noiseFrequency = 2f;
        [SerializeField] private float noiseSeed = 17.3f;

        private const float StandardGravity = 9.80665f;
        private Rigidbody body;
        private PlaneController controller;
        private Vector3 previousVelocity, previousPosition, previousLevelUp = Vector3.up;
        private bool hasVelocityHistory, previousStall, lowFuelReported, emptyReported;
        private float nextQueryAt, nextManagerSearchAt, rawHeading, rawAgl = float.NaN;
        private readonly RaycastHit[] queryHits = new RaycastHit[64];

        public float RawSpeedMetersPerSecond { get; private set; }
        public float SpeedMetersPerSecond { get; private set; }
        public float SpeedKmh => SpeedMetersPerSecond * 3.6f;
        public float SpeedKnots => SpeedMetersPerSecond * 1.9438445f;
        public float AltitudeMSL { get; private set; }
        public float AltitudeAGL { get; private set; } = float.NaN;
        public bool HasGroundAltitude { get; private set; }
        public float VerticalSpeed { get; private set; }
        public float HeadingDegrees { get; private set; }
        public float PitchDegrees { get; private set; }
        public float RollDegrees { get; private set; }
        public float GForce { get; private set; } = 1f;
        public float RawGForce { get; private set; } = 1f;
        public float TotalGForce { get; private set; } = 1f;
        public bool HasAccelerationSample { get; private set; }
        public float AngleOfAttackDegrees { get; private set; } = float.NaN;
        public bool HasAngleOfAttack { get; private set; }
        public bool IsStalling { get; private set; }
        public float FuelLiters { get; private set; }
        public float FuelCapacityLiters => Mathf.Max(0.1f, fuelCapacityLiters);
        public float FuelFraction => Mathf.Clamp01(FuelLiters / FuelCapacityLiters);
        public bool IsLowFuel => simulateFuel && FuelFraction <= lowFuelFraction;
        public bool IsFuelEmpty => simulateFuel && FuelLiters <= 0f;
        public Checkpoint NextCheckpoint { get; private set; }
        public bool HasNextCheckpoint => NextCheckpoint != null;
        public float DistanceToNextCheckpoint { get; private set; } = float.NaN;
        public float BearingToNextCheckpoint { get; private set; } = float.NaN;
        public float RelativeBearingToNextCheckpoint { get; private set; } = float.NaN;
        public Collider Obstacle { get; private set; }
        public bool HasObstacle => Obstacle != null;
        public float ObstacleDistance { get; private set; } = float.PositiveInfinity;
        public float TimeToImpact { get; private set; } = float.PositiveInfinity;
        public bool TerrainWarning { get; private set; }
        public event Action OnStall;
        public event Action OnLowFuel;
        public event Action OnFuelEmpty;
        public event Action<bool> OnTerrainWarning;

        private void Awake()
        {
            body = GetComponent<Rigidbody>(); controller = GetComponent<PlaneController>();
            FuelLiters = FuelCapacityLiters * Mathf.Clamp01(initialFuelFraction);
            ResetMotionHistory();
        }
        private void OnEnable() { if (body != null) ResetMotionHistory(); }
        private void Start() { ResolveManager(); RefreshFuelAlarms(); }

        private void FixedUpdate()
        {
            // Avant EngineCutoff (-100) puis PlaneController (0) : la dernière goutte coupe la poussée ce même pas.
            FuelLiters = Mathf.Clamp(FuelLiters, 0f, FuelCapacityLiters);
            if (simulateFuel && IsEngineActive())
                FuelLiters = Mathf.Max(0f, FuelLiters - Mathf.Clamp01(controller.Throttle) *
                    Mathf.Max(0f, fuelBurnLitersPerSecond) * Time.fixedDeltaTime);
            SampleMotion();
            if (Time.fixedTime >= nextQueryAt)
            {
                nextQueryAt = Time.fixedTime + Mathf.Max(0.02f, queryInterval);
                SampleGround(); SampleObstacles();
            }
            AltitudeAGL = HasGroundAltitude ? Mathf.Max(0f, rawAgl + Noise(2) * altitudeNoiseMeters) : float.NaN;
        }
        private void LateUpdate()
        {
            // Le contrôleur et les callbacks physiques ont déjà actualisé leurs états ; aucun recalcul du stall.
            IsStalling = controller.IsStalling;
            bool enteredStall = IsStalling && !previousStall;
            previousStall = IsStalling;
            if (enteredStall) OnStall?.Invoke();
            RefreshFuelAlarms();
            ResolveManager(); SampleNavigation();
        }
        private void SampleMotion()
        {
            Vector3 velocity = body.linearVelocity;
            Quaternion rotation = body.rotation;
            Vector3 forward = rotation * Vector3.forward, up = rotation * Vector3.up;
            if ((body.position - previousPosition).sqrMagnitude > teleportThreshold * teleportThreshold) hasVelocityHistory = false;
            RawSpeedMetersPerSecond = velocity.magnitude;
            SpeedMetersPerSecond = Mathf.Max(0f, RawSpeedMetersPerSecond + Noise(0) * speedNoiseMetersPerSecond);
            VerticalSpeed = velocity.y + Noise(1) * speedNoiseMetersPerSecond;
            AltitudeMSL = body.position.y - seaLevelWorldY + Noise(2) * altitudeNoiseMeters;
            if (new Vector2(forward.x, forward.z).sqrMagnitude > 0.00001f)
                rawHeading = Mathf.Repeat(Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg, 360f);
            HeadingDegrees = Mathf.Repeat(rawHeading + Noise(3) * angleNoiseDegrees, 360f);
            PitchDegrees = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(forward.y, -1f, 1f)) * Mathf.Rad2Deg + Noise(4) * angleNoiseDegrees, -90f, 90f);
            Vector3 levelUp = Vector3.ProjectOnPlane(Vector3.up, forward);
            if (levelUp.sqrMagnitude < 0.001f) levelUp = Vector3.ProjectOnPlane(previousLevelUp, forward);
            if (levelUp.sqrMagnitude < 0.00001f) levelUp = Vector3.ProjectOnPlane(Vector3.forward, forward);
            previousLevelUp = levelUp.normalized;
            // Positif = aile droite vers le bas, comme l'entrée Roll du contrôleur.
            RollDegrees = Mathf.DeltaAngle(0f, -Vector3.SignedAngle(previousLevelUp, up, forward) + Noise(5) * angleNoiseDegrees);
            HasAccelerationSample = hasVelocityHistory;
            Vector3 acceleration = hasVelocityHistory ? (velocity - previousVelocity) / Time.fixedDeltaTime : Vector3.zero;
            // Accélération propre : 1 g à l'arrêt soutenu, 0 g en chute libre ; G signé sur Y local.
            Vector3 specificAcceleration = acceleration - Physics.gravity;
            RawGForce = Vector3.Dot(specificAcceleration, up) / StandardGravity;
            GForce = RawGForce + Noise(6) * gNoise;
            TotalGForce = specificAcceleration.magnitude / StandardGravity;
            Vector3 localVelocity = Quaternion.Inverse(rotation) * velocity;
            HasAngleOfAttack = localVelocity.z > 1f;
            AngleOfAttackDegrees = HasAngleOfAttack ? Mathf.Atan2(-localVelocity.y, localVelocity.z) * Mathf.Rad2Deg + Noise(7) * angleNoiseDegrees : float.NaN;
            previousVelocity = velocity; previousPosition = body.position; hasVelocityHistory = true;
        }
        private float Noise(int channel) => measurementNoise ?
            Mathf.PerlinNoise(noiseSeed + channel * 13.17f, Time.fixedTime * Mathf.Max(0f, noiseFrequency)) * 2f - 1f : 0f;
        private int ExcludeAircraft(LayerMask mask)
        {
            int plane = LayerMask.NameToLayer("Plane");
            return plane >= 0 ? mask.value & ~(1 << plane) : mask.value;
        }
        private bool IsOwnCollider(Collider collider) => collider.transform.IsChildOf(body.transform);
        private void SampleGround()
        {
            Vector3 origin = altitudeProbe != null ? altitudeProbe.position : body.worldCenterOfMass;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, queryHits, Mathf.Max(1f, maximumGroundDistance),
                ExcludeAircraft(groundMask), QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            HasGroundAltitude = false; rawAgl = float.NaN;
            for (int i = 0; i < count; i++)
            {
                var hit = queryHits[i];
                if (IsOwnCollider(hit.collider) || hit.distance >= nearest) continue;
                nearest = hit.distance; rawAgl = Mathf.Max(0f, body.position.y - hit.point.y); HasGroundAltitude = true;
            }
        }
        private void SampleObstacles()
        {
            Obstacle = null; ObstacleDistance = TimeToImpact = float.PositiveInfinity;
            Vector3 velocity = body.linearVelocity;
            float speed = velocity.magnitude;
            if (speed < minimumWarningSpeed) { SetTerrainWarning(false); return; }
            Vector3 forward = body.rotation * Vector3.forward;
            Vector3 direction = followVelocityForWarning ? velocity / speed : forward;
            Vector3 origin = body.position + body.rotation * obstacleProbeOffset;
            float distance = Mathf.Min(maximumObstacleDistance, speed * warningHorizonSeconds);
            int count = Physics.SphereCastNonAlloc(origin, Mathf.Max(0.05f, obstacleRadius), direction, queryHits,
                Mathf.Max(0f, distance), ExcludeAircraft(obstacleMask), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var hit = queryHits[i];
                if (IsOwnCollider(hit.collider)) continue;
                Vector3 obstacleVelocity = hit.rigidbody != null ? hit.rigidbody.GetPointVelocity(hit.point) : Vector3.zero;
                float closingSpeed = Vector3.Dot(velocity - obstacleVelocity, direction);
                float ttc = closingSpeed > 0.01f ? hit.distance / closingSpeed : float.PositiveInfinity;
                if (Obstacle != null && (ttc > TimeToImpact || (ttc == TimeToImpact && hit.distance >= ObstacleDistance))) continue;
                Obstacle = hit.collider; ObstacleDistance = hit.distance; TimeToImpact = ttc;
            }
            SetTerrainWarning(HasObstacle && TimeToImpact < warningHorizonSeconds);
        }
        private void SetTerrainWarning(bool value)
        {
            if (TerrainWarning == value) return;
            TerrainWarning = value; OnTerrainWarning?.Invoke(value);
        }
        private void ResolveManager()
        {
            if (checkpointManager != null || !findManagerInSameScene || Time.time < nextManagerSearchAt) return;
            nextManagerSearchAt = Time.time + 1f;
            // Recherche limitée à notre scène : n'utilise pas les scènes des autres véhicules.
            foreach (var root in gameObject.scene.GetRootGameObjects())
            {
                checkpointManager = root.GetComponentInChildren<CheckpointManager>();
                if (checkpointManager != null) break;
            }
        }
        private void SampleNavigation()
        {
            NextCheckpoint = null;
            DistanceToNextCheckpoint = BearingToNextCheckpoint = RelativeBearingToNextCheckpoint = float.NaN;
            if (checkpointManager == null || checkpointManager.IsComplete) return;
            int index = checkpointManager.PassedCount;
            var list = checkpointManager.Checkpoints;
            if (index < 0 || index >= list.Count || list[index] == null) return;
            NextCheckpoint = list[index];
            Vector3 delta = NextCheckpoint.transform.position - body.position;
            DistanceToNextCheckpoint = delta.magnitude;
            BearingToNextCheckpoint = new Vector2(delta.x, delta.z).sqrMagnitude > 0.00001f ?
                Mathf.Repeat(Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg, 360f) : rawHeading;
            RelativeBearingToNextCheckpoint = Mathf.DeltaAngle(rawHeading, BearingToNextCheckpoint);
        }
        public bool IsEngineActive() => engineEnabled && (!simulateFuel || FuelLiters > 0f);
        public void SetEngineEnabled(bool value) => engineEnabled = value;
        public void Refuel(float liters)
        {
            if (float.IsNaN(liters) || float.IsInfinity(liters) || liters <= 0f) return;
            FuelLiters = Mathf.Min(FuelCapacityLiters, FuelLiters + liters); RefreshFuelAlarms();
        }
        public void RefillTank() => Refuel(FuelCapacityLiters);
        private void RefreshFuelAlarms()
        {
            bool low = IsLowFuel, empty = IsFuelEmpty;
            bool enteredLow = low && !lowFuelReported, enteredEmpty = empty && !emptyReported;
            lowFuelReported = low; emptyReported = empty;
            if (enteredLow) OnLowFuel?.Invoke();
            if (enteredEmpty) OnFuelEmpty?.Invoke();
        }
        public void ResetMotionHistory()
        {
            if (body == null) return;
            hasVelocityHistory = HasAccelerationSample = false;
            previousPosition = body.position; previousVelocity = body.linearVelocity;
            nextQueryAt = 0f; SetTerrainWarning(false);
            // Un reset de position n'ajoute pas de carburant ; RefillTank est une action séparée.
        }
        private void OnDisable() => SetTerrainWarning(false);
#if UNITY_EDITOR
        public void ConfigureGroundMask(LayerMask mask) => groundMask = mask;
#endif
    }
}
