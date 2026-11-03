using System;
using KikiNgao.SimpleBikeControl;
using UnityEngine;

namespace BikeGame
{
    /// <summary>
    /// Detects real impacts on the bicycle and fires a short vibration.
    /// Only the velocity component along the contact normal counts, so riding or
    /// rolling along the ground never vibrates; rolling into a tree or a wall does.
    /// </summary>
    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
    public class BikeImpactFeedback : MonoBehaviour
    {
        [Header("Impact detection (m/s along the contact normal)")]
        [Tooltip("Minimum impact speed against a solid obstacle (trees, walls, rocks).")]
        [SerializeField] private float obstacleImpactSpeed = 2.5f;

        [Tooltip("Minimum impact speed against the terrain. Higher, so ordinary riding and small bumps stay silent.")]
        [SerializeField] private float groundImpactSpeed = 6f;

        [Tooltip("Impact speed that maps to the maximum vibration duration.")]
        [SerializeField] private float fullStrengthImpactSpeed = 14f;

        [Header("Vibration")]
        [SerializeField] private int minimumDurationMs = 60;
        [SerializeField] private int maximumDurationMs = 180;
        [Tooltip("-1 uses the device default amplitude, 1..255 is a fixed amplitude.")]
        [SerializeField, Range(-1, 255)] private int amplitude = 220;

        [Tooltip("Seconds after one vibration before another impact can vibrate again.")]
        [SerializeField] private float cooldown = 0.6f;

        [Header("Debug")]
        [SerializeField] private bool logImpacts = true;

        private float nextAllowedTime;

        public int ImpactCount { get; private set; }
        public float LastImpactSpeed { get; private set; }
        public int LastVibrationMs { get; private set; }

        public float Cooldown
        {
            get => cooldown;
            set => cooldown = Mathf.Max(0f, value);
        }

        public bool LogImpacts
        {
            get => logImpacts;
            set => logImpacts = value;
        }

        public event Action<float> Impacted;

        private void Awake()
        {
            RebuildProxies();
        }

        /// <summary>
        /// Adds collision forwarders to every solid child collider of the bike.
        /// Safe to call again after the hierarchy changes.
        /// </summary>
        public void RebuildProxies()
        {
            Collider[] colliders = GetComponentsInChildren<Collider>(true);

            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];

                if (collider == null || collider.isTrigger) continue;
                if (collider is WheelCollider) continue;
                if (collider.gameObject == gameObject) continue;

                // The rider is parented to the bike while riding. Foot colliders
                // must never trigger bike impact vibration.
                if (collider.GetComponentInParent<Biker>() != null) continue;

                if (collider.GetComponent<BikeCollisionProxy>() == null)
                {
                    BikeCollisionProxy proxy = collider.gameObject.AddComponent<BikeCollisionProxy>();
                    proxy.feedback = this;
                }
            }
        }

        /// <summary>Called by the collider forwarders and by OnCollisionEnter on the root.</summary>
        public void ReportCollision(Collision collision, BikeCollisionProxy source)
        {
            if (collision == null || collision.collider == null) return;

            // A proxy whose object has left the bike (rider dismounted) must not
            // report impacts any more.
            if (source != null && !source.transform.IsChildOf(transform)) return;

            Collider other = collision.collider;

            if (other.transform.IsChildOf(transform)) return;

            Biker rider = other.GetComponentInParent<Biker>();
            if (rider != null) return;

            float normalSpeed = GetNormalImpactSpeed(collision);
            if (normalSpeed <= 0f) return;

            bool terrain = other is TerrainCollider;
            if (!ShouldVibrate(terrain, normalSpeed)) return;

            TriggerImpact(normalSpeed);
        }

        /// <summary>
        /// Pure threshold test, exposed so the editor tests and tuning tools can
        /// verify ground contact filtering without a live collision.
        /// </summary>
        public bool ShouldVibrate(bool terrainContact, float normalImpactSpeed)
        {
            float threshold = terrainContact ? groundImpactSpeed : obstacleImpactSpeed;
            return normalImpactSpeed >= threshold;
        }

        private void OnCollisionEnter(Collision collision)
        {
            ReportCollision(collision, null);
        }

        private static float GetNormalImpactSpeed(Collision collision)
        {
            Vector3 relativeVelocity = collision.relativeVelocity;
            int contactCount = collision.contactCount;

            if (contactCount == 0)
                return relativeVelocity.magnitude;

            float strongest = 0f;

            for (int i = 0; i < contactCount; i++)
            {
                ContactPoint contact = collision.GetContact(i);
                float alongNormal = Mathf.Abs(Vector3.Dot(relativeVelocity, contact.normal));
                if (alongNormal > strongest) strongest = alongNormal;
            }

            return strongest;
        }

        /// <summary>
        /// Raises one impact. Respects the cooldown, so staying in contact or
        /// hitting several colliders at once can never vibrate continuously.
        /// </summary>
        public void TriggerImpact(float impactSpeed)
        {
            if (impactSpeed <= 0f) return;
            if (Time.time < nextAllowedTime) return;

            nextAllowedTime = Time.time + Mathf.Max(0f, cooldown);

            ImpactCount++;
            LastImpactSpeed = impactSpeed;

            float strength = Mathf.Clamp01(Mathf.InverseLerp(
                Mathf.Max(0.1f, obstacleImpactSpeed),
                Mathf.Max(obstacleImpactSpeed + 0.1f, fullStrengthImpactSpeed),
                impactSpeed));

            LastVibrationMs = Mathf.RoundToInt(
                Mathf.Lerp(minimumDurationMs, maximumDurationMs, strength));

            BikeVibration.Vibrate(LastVibrationMs, amplitude);

            if (logImpacts)
            {
                Debug.LogFormat("BikeImpactFeedback: impact {0:0.0} m/s -> {1} ms vibration.",
                    impactSpeed, LastVibrationMs);
            }

            Impacted?.Invoke(impactSpeed);
        }

        /// <summary>Editor/debug entry point that ignores the cooldown.</summary>
        public void SimulateImpact(float impactSpeed = 8f)
        {
            nextAllowedTime = 0f;
            TriggerImpact(impactSpeed);
        }

        /// <summary>Clears the cooldown, used after respawning the bike.</summary>
        public void ResetCooldown()
        {
            nextAllowedTime = 0f;
        }
    }
}
