using KikiNgao.SimpleBikeControl;
using UnityEngine;

namespace BikeGame
{
    /// <summary>
    /// Small arcade style balance assist for the two wheel bike. The original
    /// controller is designed for a flat demo plane: while standing still its
    /// center of mass sits at the contact patch, so the bike tips over on any
    /// sloped terrain. This component applies a gentle upright torque at low
    /// speed and keeps the roll (lean) corrected at every speed, because the
    /// two in-line wheel contacts provide no sideways support while riding.
    /// Pitch is only corrected at low speed so hills keep pitching the bike.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class BikeStabilityAssist : MonoBehaviour
    {
        [Tooltip("Above this speed the pitch part of the assist is disabled.")]
        [SerializeField] private float assistSpeed = 3f;
        [Tooltip("Angular acceleration used by the low-speed pitch assist.")]
        [SerializeField] private float uprightAcceleration = 30f;
        [Tooltip("Angular acceleration used to hold the bike upright in roll. " +
                 "Must beat the gravity torque of the raised riding center of mass.")]
        [SerializeField] private float rollAcceleration = 300f;
        [Tooltip("How strongly the sideways rocking is damped.")]
        [SerializeField] private float rollDamping = 22f;
        [Tooltip("Beyond this tilt the bike is considered fallen and is left alone.")]
        [SerializeField] private float fallenAngle = 70f;

        private Rigidbody body;
        private SimpleBike bike;

        /// <summary>
        /// When true the low-speed pitch correction is skipped. The wheelie
        /// controller sets this while the front wheel is lifted so the two
        /// systems do not fight over the same axis.
        /// </summary>
        public bool SuppressPitchAssist { get; set; }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            bike = GetComponent<SimpleBike>();
        }

        private void FixedUpdate()
        {
            if (body == null || body.isKinematic) return;
            if (bike != null && (bike.falling || !bike.ReadyToRide())) return;

            Vector3 up = transform.up;
            float tilt = Vector3.Angle(up, Vector3.up);
            if (tilt > fallenAngle) return;

            // Axis that rotates the bike's up vector back to world up.
            Vector3 correction = Vector3.Cross(up, Vector3.up);

            // Roll (lean) is corrected at every speed: the front and rear
            // contacts are in line, so nothing stops a sideways tip once the
            // bike is rolling. Only the component around the bike's forward
            // axis belongs to roll, so climbing a slope is not counteracted.
            Vector3 rollAxis = transform.forward;
            Vector3 rollCorrection = Vector3.Project(correction, rollAxis);
            body.AddTorque(rollCorrection * rollAcceleration, ForceMode.Acceleration);

            // Pitch stays low-speed only so the terrain still pitches the bike.
            float speed = body.linearVelocity.magnitude;
            float weight = Mathf.Clamp01(1f - speed / Mathf.Max(0.01f, assistSpeed));
            if (weight > 0.001f && !SuppressPitchAssist)
            {
                Vector3 pitchCorrection = correction - rollCorrection;
                body.AddTorque(pitchCorrection * (uprightAcceleration * weight), ForceMode.Acceleration);
            }

            // Damp the roll component so the assist does not start oscillating.
            Vector3 rollVelocity = Vector3.Project(body.angularVelocity, rollAxis);
            float damping = 1f - Mathf.Exp(-rollDamping * Time.fixedDeltaTime);
            body.angularVelocity -= rollVelocity * damping;
        }
    }
}
