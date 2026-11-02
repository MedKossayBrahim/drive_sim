using UnityEngine;

namespace Project.Plane
{
    public enum FlightAssistance { Easy, Pro }
    /// <summary>Vol arcade sur Rigidbody. Repère du corps : Z avant, Y haut, X droite.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody))]
    public sealed class PlaneController : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField, Tooltip("Facultatif : sans asset, les valeurs par défaut de PlaneSettings sont utilisées.")]
        private PlaneSettings settings = null;
        [SerializeField, Tooltip("Facultatif : position du centre de masse dans la hiérarchie de l'avion.")]
        private Transform centerOfMass = null;

        [Header("Entrées — alimentées par le script d'inputs dans Update")]
        [SerializeField, Range(0f, 1f)] private float throttle = 0f;
        [SerializeField, Range(-1f, 1f), Tooltip("Positif = cabrer.")] private float pitch = 0f;
        [SerializeField, Range(-1f, 1f), Tooltip("Positif = aile droite vers le bas.")] private float roll = 0f;
        [SerializeField, Range(-1f, 1f), Tooltip("Positif = lacet à droite.")] private float yaw = 0f;
        [SerializeField, Range(0f, 1f)] private float flaps = 0f;
        [SerializeField] private bool gearDeployed = true;

        [Header("Affichage des forces")]
        [SerializeField] private bool showForceGizmos = true;
        [SerializeField, Min(0f), Tooltip("Longueur affichée en mètres par newton.")]
        private float forceGizmoScale = 0.001f;

        [Header("Assistance de la mission")]
        [SerializeField] private FlightAssistance assistance = FlightAssistance.Easy;
        private PlaneSensors sensors;
        private float engineThrottle;
        private Rigidbody body;
        private PlaneSettings defaultSettings;
        private bool customCenterApplied;
        private Vector3 thrustForce, liftForce, dragForce;
        public float EngineThrottle => engineThrottle;
        public bool DirectTiltControl { get; set; }
        public bool FallingNoseDown { get; set; }
        public FlightAssistance Assistance => assistance;
        public bool IsAirborne => sensors != null ? !sensors.HasGroundAltitude || sensors.AltitudeAGL > 3f :
            !Physics.Raycast(body.worldCenterOfMass, Vector3.down, 3f, LayerMask.GetMask("Ground"), QueryTriggerInteraction.Ignore);
        public void SetAssistance(FlightAssistance value) => assistance = value;
        public void ToggleAssistance() => assistance = assistance == FlightAssistance.Easy ? FlightAssistance.Pro : FlightAssistance.Easy;
        public void ResetFlightState() { engineThrottle = 0f; FallingNoseDown = false; }
        public float Throttle { get => throttle; set => throttle = Mathf.Clamp01(value); }
        public float Pitch { get => pitch; set => pitch = Mathf.Clamp(value, -1f, 1f); }
        public float Roll { get => roll; set => roll = Mathf.Clamp(value, -1f, 1f); }
        public float Yaw { get => yaw; set => yaw = Mathf.Clamp(value, -1f, 1f); }
        public float Flaps { get => flaps; set => flaps = Mathf.Clamp01(value); }
        public bool GearDeployed { get => gearDeployed; set => gearDeployed = value; }
        public float ForwardSpeed { get; private set; }
        public bool IsStalling { get; private set; }
        public Rigidbody Body => body;
        public PlaneSettings Settings => settings != null ? settings : defaultSettings;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            sensors = GetComponent<PlaneSensors>();
            if (settings == null)
            {
                // Copie en mémoire seulement : aucun asset partagé n'est modifié à l'exécution.
                defaultSettings = ScriptableObject.CreateInstance<PlaneSettings>();
                defaultSettings.hideFlags = HideFlags.HideAndDontSave;
            }
            body.useGravity = true;
            // Les forces ci-dessous gèrent elles-mêmes la traînée et l'amortissement.
            body.linearDamping = 0f;
            body.angularDamping = 0f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            ConfigureBody(Settings);
        }

        private void ConfigureBody(PlaneSettings config)
        {
            if (!Mathf.Approximately(body.mass, config.Mass)) body.mass = config.Mass;
            body.maxAngularVelocity = config.MaxAngularSpeed * Mathf.Deg2Rad;
            if (centerOfMass != null)
            {
                body.centerOfMass = transform.InverseTransformPoint(centerOfMass.position);
                customCenterApplied = true;
            }
            else if (customCenterApplied)
            {
                body.ResetCenterOfMass();
                customCenterApplied = false;
            }
        }

        private void FixedUpdate()
        {
            PlaneSettings config = Settings;
            if (config == null || body.isKinematic) return;
            ConfigureBody(config);
            Quaternion rotation = body.rotation;
            Vector3 forwardAxis = rotation * Vector3.forward, upAxis = rotation * Vector3.up;
            Vector3 velocity = body.linearVelocity;
            ForwardSpeed = Vector3.Dot(velocity, forwardAxis);
            float forward = Mathf.Max(0f, ForwardSpeed);
            float recovery = Mathf.Clamp01(forward / config.StallSpeed);
            IsStalling = forward < config.StallSpeed;
            bool cut = throttle <= config.ThrottleCutoff;
            engineThrottle = config.EngineResponseTime <= 0f ? throttle :
                Mathf.MoveTowards(engineThrottle, throttle, Time.fixedDeltaTime / config.EngineResponseTime);
            // Une coupure ou un réservoir vide annule la poussée ce pas, sans arrêter brutalement l'hélice.
            thrustForce = forwardAxis * (cut ? 0f : engineThrottle * config.MaxThrust);
            float liftFactor = IsStalling ? Mathf.Lerp(config.StalledLiftFraction, 1f, Mathf.Pow(recovery, 4f)) : 1f;
            float lift = forward * forward * config.LiftCoefficient * liftFactor * (1f + flaps * config.FlapsExtraLift);
            lift = Mathf.Min(lift, body.mass * Physics.gravity.magnitude * config.MaxLiftLoadFactor);
            if (cut && !FallingNoseDown && forward > config.StallSpeed * .5f)
            {
                // Assistance arcade au plané : jamais de poussée cachée. Le piqué correct transforme l'altitude en vitesse.
                float sink = -new Vector2(velocity.x, velocity.z).magnitude / config.GlideRatio;
                float availableVerticalLift = body.mass * (Physics.gravity.magnitude + (sink - velocity.y) * config.GlideSinkResponse);
                float verticalSupport = Mathf.Max(0f, availableVerticalLift);
                if (IsStalling) verticalSupport = Mathf.Min(verticalSupport, body.mass * Physics.gravity.magnitude);
                float glideLift = verticalSupport / Mathf.Max(.2f, Vector3.Dot(upAxis, Vector3.up));
                // Cambrure automatique arcade : permet le vrai plané jusqu'au seuil, puis effondrement continu.
                lift = Mathf.Min(body.mass * Physics.gravity.magnitude * config.MaxLiftLoadFactor, glideLift) * liftFactor;
            }
            liftForce = upAxis * lift;
            float drag = config.DragCoefficient + flaps * config.FlapsExtraDrag + (gearDeployed ? config.GearExtraDrag : 0f) +
                (cut ? config.EngineOffDrag : 0f);
            dragForce = -velocity * (velocity.magnitude * drag);
            body.AddForce(thrustForce + liftForce + dragForce, ForceMode.Force);

            float authority = Mathf.Lerp(config.MinimumControlAuthority, 1f, Mathf.Clamp01(forward / config.FullControlSpeed));
            Vector3 angular = Quaternion.Inverse(rotation) * body.angularVelocity;
            Vector3 torque = new Vector3(-pitch * config.PitchAcceleration, yaw * config.YawAcceleration, -roll * config.RollAcceleration) * authority;
            if (DirectTiltControl)
            {
                Vector3 levelUp = Vector3.ProjectOnPlane(Vector3.up, forwardAxis);
                float bank = levelUp.sqrMagnitude > .001f ? -Vector3.SignedAngle(levelUp, upAxis, forwardAxis) : 0f;
                float attitude = Mathf.Asin(Mathf.Clamp(forwardAxis.y, -1f, 1f)) * Mathf.Rad2Deg;
                float neutralGain = assistance == FlightAssistance.Easy ? config.EasyAssistance : config.ProAssistance;
                float pitchGain = Mathf.Abs(pitch) < config.StabilizationInputDeadZone ? neutralGain : 1f;
                float rollGain = Mathf.Abs(roll) < config.StabilizationInputDeadZone ? neutralGain : 1f;
                torque.x = -(pitch * config.TiltPitchAngle - attitude) * Mathf.Deg2Rad * config.TiltPitchResponse * pitchGain * authority;
                torque.z = -Mathf.DeltaAngle(bank, roll * config.TiltBankAngle) * Mathf.Deg2Rad * config.TiltRollResponse * rollGain * authority;
                torque.x -= angular.x * config.TiltAngularDamping;
                torque.z -= angular.z * config.TiltAngularDamping;
            }
            else if (config.AutoStabilization)
            {
                Vector3 level = Quaternion.Inverse(rotation) * Vector3.Cross(upAxis, Vector3.up);
                float gain = assistance == FlightAssistance.Easy ? config.EasyAssistance : config.ProAssistance;
                if (Mathf.Abs(pitch) <= config.StabilizationInputDeadZone) torque.x += level.x * config.PitchLevelStrength * authority * gain;
                if (Mathf.Abs(roll) <= config.StabilizationInputDeadZone) torque.z += level.z * config.RollLevelStrength * authority * gain;
            }
            torque -= Vector3.Scale(angular, config.AngularDamping);
            if (IsAirborne && forward > config.StallSpeed * .5f)
            {
                float bankSine = Vector3.Dot(rotation * Vector3.right, Vector3.down);
                float bankCosine = Mathf.Max(.3f, Vector3.Dot(upAxis, Vector3.up));
                float desiredYawRate = Physics.gravity.magnitude * bankSine / bankCosine / Mathf.Max(10f, forward) * config.BankToTurn;
                // Le lacet automatique est autour de la verticale mondiale, pas du Z de l'avion incliné.
                Vector3 correction = Quaternion.Inverse(rotation) * Vector3.up *
                    ((desiredYawRate - Vector3.Dot(body.angularVelocity, Vector3.up)) * config.CoordinatedTurnResponse);
                torque += correction;
            }
            if (IsStalling && IsAirborne)
                torque.x += config.StallNoseDownAcceleration * (1f - recovery) * Mathf.Clamp01(velocity.magnitude / config.StallSpeed);
            if (FallingNoseDown && IsAirborne)
                torque.x += config.FallingNoseDownAcceleration * (1f - Mathf.Clamp01(Mathf.Max(0f, pitch)));
            body.AddRelativeTorque(torque, ForceMode.Acceleration);
        }

        private void OnDisable() { thrustForce = liftForce = dragForce = Vector3.zero; engineThrottle = 0f; }
        private void OnDestroy()
        {
            if (defaultSettings != null) Destroy(defaultSettings);
        }

        private void OnDrawGizmosSelected()
        {
            if (!showForceGizmos) return;
            Vector3 origin = body != null ? body.worldCenterOfMass :
                centerOfMass != null ? centerOfMass.position : transform.position;
            Gizmos.color = Color.white;
            Gizmos.DrawWireSphere(origin, 0.3f);
            DrawForce(origin, thrustForce, Color.red);
            DrawForce(origin, liftForce, Color.green);
            DrawForce(origin, dragForce, Color.cyan);
        }
        private void DrawForce(Vector3 origin, Vector3 force, Color color)
        {
            Gizmos.color = color;
            Vector3 vector = force * forceGizmoScale;
            Gizmos.DrawLine(origin, origin + vector);
            if (vector.sqrMagnitude < 0.0001f) return;
            Vector3 side = Vector3.Cross(vector.normalized, Vector3.up);
            if (side.sqrMagnitude < 0.001f) side = Vector3.Cross(vector.normalized, Vector3.right);
            float tip = Mathf.Min(1f, vector.magnitude * 0.2f);
            Gizmos.DrawLine(origin + vector, origin + vector - vector.normalized * tip + side.normalized * tip * 0.5f);
            Gizmos.DrawLine(origin + vector, origin + vector - vector.normalized * tip - side.normalized * tip * 0.5f);
        }
    }
}
