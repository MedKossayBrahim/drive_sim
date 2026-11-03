using KikiNgao.SimpleBikeControl;
using UnityEngine;

namespace BikeGame
{
    /// <summary>
    /// Lifts the front wheel when the player tilts the phone back and lets it
    /// come down again when the phone returns to neutral. The motion is driven
    /// by a torque around the bike's lateral axis, so the normal WheelCollider
    /// physics (suspension, grip, steering, drive force) keeps running.
    ///
    /// The input comes from <see cref="TiltSteering.WheelieAxis"/>, which is the
    /// same calibrated accelerometer stream the tilt steering already uses.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public class BikeWheelie : MonoBehaviour
    {
        [Header("Angles")]
        [Tooltip("Wheelie angle at full tilt. Kept well below the point where the bike would flip.")]
        [SerializeField] private float maxWheelieAngle = 26f;
        [Tooltip("Absolute safety limit. Past this angle a strong counter torque is applied.")]
        [SerializeField] private float hardLimitAngle = 32f;
        [Tooltip("Below this pitch (relative to the ground) the controller is idle, so normal riding is untouched.")]
        [SerializeField] private float idleAngle = 3f;

        [Header("Response")]
        [Tooltip("Angular acceleration per radian of angle error while lifting.")]
        [SerializeField] private float liftGain = 200f;
        [Tooltip("Angular acceleration per radian of angle error while coming back down.")]
        [SerializeField] private float returnGain = 130f;
        [Tooltip("How strongly the pitch rotation is damped.")]
        [SerializeField] private float damping = 26f;
        [Tooltip("Maximum angular acceleration the controller may request.")]
        [SerializeField] private float maxAngularAcceleration = 40f;
        [Tooltip("Maximum speed the front wheel may rise, in degrees per second.")]
        [SerializeField] private float maxLiftRate = 120f;
        [Tooltip("Angular acceleration per radian past the hard limit.")]
        [SerializeField] private float hardLimitGain = 500f;
        [Tooltip("Extra damping applied when the lift rate is above the limit.")]
        [SerializeField] private float rateDamping = 100f;

        private Rigidbody body;
        private SimpleBike bike;
        private BikeStabilityAssist stability;
        private TiltSteering tilt;
        private InputManager inputManager;

        private float lastGroundTime = -10f;
        private Vector3 lastGroundNormal = Vector3.up;

        /// <summary>Current pitch above the ground, in degrees. Positive is a wheelie.</summary>
        public float WheelieAngle { get; private set; }

        /// <summary>True while the controller is lifting or lowering the wheel.</summary>
        public bool IsWheelieActive { get; private set; }

        private const float GroundGraceTime = 0.15f;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            bike = GetComponent<SimpleBike>();
            stability = GetComponent<BikeStabilityAssist>();
        }

        private void FixedUpdate()
        {
            if (body == null) return;

            if (tilt == null)
                tilt = FindAnyObjectByType<TiltSteering>();

            if (stability == null)
                stability = GetComponent<BikeStabilityAssist>();

            float input = tilt != null ? Mathf.Clamp01(tilt.WheelieAxis) : 0f;

            if (inputManager == null && GameManager.Instance != null)
                inputManager = GameManager.Instance.GetInputManager;

            bool reversing = inputManager != null && inputManager.vertical < 0f;

            bool canRide = !body.isKinematic &&
                           (bike == null ||
                            (!bike.falling && bike.ReadyToRide() && !reversing));

            WheelCollider rear = bike != null ? bike.rearWheelCollider : null;

            if (!canRide || rear == null)
            {
                // Reversing or no rider: leave the bike alone.
                WheelieAngle = 0f;
                IsWheelieActive = false;
                SetPitchAssistSuppressed(false);
                return;
            }

            Vector3 groundNormal = lastGroundNormal;

            if (rear.GetGroundHit(out WheelHit hit))
            {
                lastGroundTime = Time.fixedTime;
                lastGroundNormal = hit.normal;
                groundNormal = hit.normal;
            }
            else if (Time.fixedTime - lastGroundTime > GroundGraceTime && input <= 0.001f)
            {
                // Airborne with no wheelie request: leave the bike alone.
                WheelieAngle = 0f;
                IsWheelieActive = false;
                SetPitchAssistSuppressed(false);
                return;
            }

            float angle = MeasureWheelieAngle(groundNormal);
            WheelieAngle = angle;

            bool engaged = input > 0.001f || angle > idleAngle;
            IsWheelieActive = engaged;

            // The low-speed pitch part of the stability assist would fight the
            // wheelie, so it is muted while the wheel is up.
            SetPitchAssistSuppressed(engaged);

            if (!engaged)
                return;

            Vector3 right = transform.right;
            float noseUpRate = -Vector3.Dot(body.angularVelocity, right);
            float target = maxWheelieAngle * input;

            float acceleration;

            if (angle > hardLimitAngle)
            {
                // Past the safety limit the player input is ignored and the
                // front wheel is pushed down hard, so the bike cannot flip.
                float over = (angle - hardLimitAngle) * Mathf.Deg2Rad;
                acceleration = -(over * hardLimitGain + Mathf.Max(0f, noseUpRate) * damping);
            }
            else
            {
                float error = (target - angle) * Mathf.Deg2Rad;
                float gain = error >= 0f ? liftGain : returnGain;
                acceleration = error * gain - noseUpRate * damping;
            }

            // Soft cap on the rising speed: a sudden tilt cannot throw the bike
            // over even if the requested angle is reached quickly.
            float liftRateLimit = Mathf.Max(1f, maxLiftRate) * Mathf.Deg2Rad;
            if (noseUpRate > liftRateLimit)
                acceleration -= (noseUpRate - liftRateLimit) * rateDamping;

            acceleration = Mathf.Clamp(acceleration, -maxAngularAcceleration, maxAngularAcceleration);

            if (Mathf.Abs(acceleration) > 0.001f)
                body.AddTorque(right * -acceleration, ForceMode.Acceleration);
        }

        /// <summary>
        /// Pitch of the bike above the ground plane, in degrees. Positive means
        /// the nose is up. Measured from the rear wheel contact normal so a hill
        /// does not look like a wheelie.
        /// </summary>
        private float MeasureWheelieAngle(Vector3 groundNormal)
        {
            Vector3 right = transform.right;
            Vector3 groundUp = Vector3.ProjectOnPlane(groundNormal, right);
            Vector3 bikeUp = Vector3.ProjectOnPlane(transform.up, right);

            if (groundUp.sqrMagnitude < 0.0001f || bikeUp.sqrMagnitude < 0.0001f)
                return 0f;

            groundUp.Normalize();
            bikeUp.Normalize();

            float signed = Mathf.Atan2(
                Vector3.Dot(right, Vector3.Cross(groundUp, bikeUp)),
                Vector3.Dot(groundUp, bikeUp)) * Mathf.Rad2Deg;

            // A nose-up wheelie is a negative rotation around the bike's right
            // axis in Unity, so flip the sign to keep positive = wheelie.
            return -signed;
        }

        private void SetPitchAssistSuppressed(bool suppressed)
        {
            if (stability != null)
                stability.SuppressPitchAssist = suppressed;
        }

        private void OnDisable()
        {
            SetPitchAssistSuppressed(false);
        }
    }
}
