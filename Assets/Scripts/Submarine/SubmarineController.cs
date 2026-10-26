using UnityEngine;
using System.Collections;

namespace DriveSim.Submarine
{
    /// <summary>
    /// Core submarine movement controller.
    /// Heavy inertia, ballast-based depth, no instant stops.
    /// Feels like a machine, not a fish.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class SubmarineController : MonoBehaviour
    {
        [Header("Hull")]
        public float mass = 9000f;
        public float maxSpeed = 14f;
        public float reverseSpeed = 6f;

        [Header("Thrust")]
        public float thrustForce = 30000f;
        public float thrustResponse = 4f;       // how fast throttle ramps up
        public float dragCoefficient = 0.25f;

        [Header("Steering")]
        public float yawTorque = 10000f;
        public float pitchTorque = 8000f;
        public float rollTorque = 5000f;
        public float steeringResponse = 6f;
        public float maxYawRate = 70f;         // degrees/sec
        public float maxPitchRate = 50f;

        [Header("Ballast")]
        public float ballastForce = 18000f;
        public float ballastResponse = 3f;
        public float neutralBuoyancyDepth = 0f; // auto-adjusts
        public float maxDepth = 4200f;
        public float crushDepth = 4200f;

        [Header("Roll")]
        public float autoLevelStrength = 4f;     // auto-level when not steering
        public float rollDamping = 5f;

        [Header("State (read-only)")]
        [SerializeField] private float _throttle;
        [SerializeField] private float _targetThrottle;
        [SerializeField] private float _ballast;
        [SerializeField] private float _targetBallast;
        [SerializeField] private float _depth;
        [SerializeField] private float _speed;
        [SerializeField] private bool _isSilentRunning;
        [SerializeField] private float _noiseLevel;

        // Public read-only state
        public float Throttle => _throttle;
        public float Ballast => _ballast;
        public float Depth => _depth;
        public float Speed => _speed;
        public bool IsSilentRunning => _isSilentRunning;
        public float NoiseLevel => _noiseLevel;
        public Vector3 Velocity => _rb != null ? _rb.linearVelocity : Vector3.zero;

        private Rigidbody _rb;
        private float _inputThrottle;
        private float _inputYaw;
        private float _inputPitch;
        private float _inputBallast;
        private float _baseNoise;
        private bool _lightsOn = true;

        // Events
        public System.Action<float> OnDepthChanged;
        public System.Action<float> OnSpeedChanged;
        public System.Action<float> OnNoiseChanged;
        public System.Action OnHullStress;
        public System.Action OnCrushWarning;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.mass = mass;
            _rb.linearDamping = dragCoefficient;
            _rb.angularDamping = 2f;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        void FixedUpdate()
        {
            UpdateDepth();
            UpdateThrottle();
            UpdateBallast();
            ApplyThrust();
            ApplySteering();
            ApplyAutoLevel();
            UpdateNoise();
            UpdateSpeed();

            // Crush depth check
            if (_depth > crushDepth * 0.9f)
            {
                OnCrushWarning?.Invoke();
            }
        }

        void UpdateDepth()
        {
            _depth = -transform.position.y;
            OnDepthChanged?.Invoke(_depth);
        }

        void UpdateThrottle()
        {
            // Ramp throttle toward input
            _targetThrottle = _inputThrottle;
            _throttle = Mathf.Lerp(_throttle, _targetThrottle, thrustResponse * Time.fixedDeltaTime);
        }

        void UpdateBallast()
        {
            _targetBallast = _inputBallast;
            _ballast = Mathf.Lerp(_ballast, _targetBallast, ballastResponse * Time.fixedDeltaTime);
        }

        void ApplyThrust()
        {
            // Forward thrust
            float thrust = _throttle * thrustForce;
            if (_throttle < 0)
                thrust *= reverseSpeed / maxSpeed;

            Vector3 thrustVector = transform.forward * thrust;
            _rb.AddForce(thrustVector, ForceMode.Force);

            // Ballast (vertical force)
            float ballastVertical = _ballast * ballastForce;
            _rb.AddForce(Vector3.up * ballastVertical, ForceMode.Force);

            // Water drag (additional to rigidbody damping)
            Vector3 drag = -_rb.linearVelocity * _rb.linearVelocity.magnitude * 0.5f * dragCoefficient;
            _rb.AddForce(drag, ForceMode.Force);

            // Clamp speed
            if (_rb.linearVelocity.magnitude > maxSpeed && _throttle >= 0)
            {
                _rb.linearVelocity = _rb.linearVelocity.normalized * maxSpeed;
            }
            else if (_throttle < 0 && _rb.linearVelocity.magnitude > reverseSpeed)
            {
                _rb.linearVelocity = _rb.linearVelocity.normalized * reverseSpeed;
            }
        }

        void ApplySteering()
        {
            // Yaw
            float yawInput = _inputYaw;
            float currentYawRate = _rb.angularVelocity.y * Mathf.Rad2Deg;
            float yawTorqueApplied = yawInput * yawTorque;
            _rb.AddTorque(transform.up * yawTorqueApplied, ForceMode.Force);

            // Pitch
            float pitchInput = _inputPitch;
            _rb.AddTorque(transform.right * pitchInput * pitchTorque, ForceMode.Force);

            // Clamp angular velocity
            Vector3 angVel = _rb.angularVelocity;
            angVel.y = Mathf.Clamp(angVel.y, -maxYawRate * Mathf.Deg2Rad, maxYawRate * Mathf.Deg2Rad);
            angVel.x = Mathf.Clamp(angVel.x, -maxPitchRate * Mathf.Deg2Rad, maxPitchRate * Mathf.Deg2Rad);
            _rb.angularVelocity = angVel;
        }

        void ApplyAutoLevel()
        {
            // When not pitching, slowly level the sub (pitch + roll)
            if (Mathf.Abs(_inputPitch) < 0.1f)
            {
                float pitchAngle = transform.eulerAngles.x;
                if (pitchAngle > 180f) pitchAngle -= 360f;
                float pitchCorrection = -pitchAngle * autoLevelStrength * 0.6f;
                _rb.AddTorque(transform.right * pitchCorrection, ForceMode.Force);

                float rollAngle = transform.eulerAngles.z;
                if (rollAngle > 180f) rollAngle -= 360f;
                float correctionTorque = -rollAngle * autoLevelStrength;
                _rb.AddTorque(transform.forward * correctionTorque, ForceMode.Force);
            }

            // Damp roll
            _rb.AddTorque(-_rb.angularVelocity.z * rollDamping * transform.forward, ForceMode.Force);
            // Damp pitch
            _rb.AddTorque(-_rb.angularVelocity.x * (rollDamping * 0.6f) * transform.right, ForceMode.Force);
        }

        void UpdateNoise()
        {
            // Base noise from engine
            _baseNoise = Mathf.Abs(_throttle) * 0.6f;

            // Silent running reduces noise
            if (_isSilentRunning)
            {
                _baseNoise *= 0.15f;
                // Silent running costs battery (handled by BatterySystem)
            }

            // Speed noise
            _baseNoise += (_speed / maxSpeed) * 0.3f;

            // Ping noise (handled by SonarSystem, adds temporarily)
            _noiseLevel = Mathf.Lerp(_noiseLevel, _baseNoise, 5f * Time.fixedDeltaTime);
            _noiseLevel = Mathf.Clamp01(_noiseLevel);

            OnNoiseChanged?.Invoke(_noiseLevel);
        }

        void UpdateSpeed()
        {
            _speed = _rb.linearVelocity.magnitude;
            OnSpeedChanged?.Invoke(_speed);
        }

        // --- Public Input Methods ---

        public void SetThrottle(float value)
        {
            _inputThrottle = Mathf.Clamp(value, -1f, 1f);
        }

        public void SetYaw(float value)
        {
            _inputYaw = Mathf.Clamp(value, -1f, 1f);
        }

        public void SetPitch(float value)
        {
            _inputPitch = Mathf.Clamp(value, -1f, 1f);
        }

        public void SetBallast(float value)
        {
            _inputBallast = Mathf.Clamp(value, -1f, 1f);
        }

        public void ToggleSilentRunning()
        {
            _isSilentRunning = !_isSilentRunning;
        }

        public void SetSilentRunning(bool value)
        {
            _isSilentRunning = value;
        }

        public void ToggleLights()
        {
            _lightsOn = !_lightsOn;
            // Notify lights (handled by visual controller)
        }

        public bool LightsOn => _lightsOn;

        // --- Collision Handling ---

        void OnCollisionEnter(Collision collision)
        {
            // L'impulsion dépend de la masse (12 t) : on l'écrête pour rester jouable.
            float impactForce = collision.impulse.magnitude;
            float damage = Mathf.Max(0f, impactForce - 6000f) * 0.00003f;

            if (damage > 1f)
            {
                OnHullStress?.Invoke();
                // Notify hull system
                var hull = GetComponent<HullSystem>();
                if (hull != null) hull.TakeDamage(damage);

                // Noise spike
                _noiseLevel = Mathf.Min(1f, _noiseLevel + damage * 0.1f);
            }
        }

        void OnTriggerEnter(Collider other)
        {
            // Handle trigger zones (depth zones, objectives, etc.)
        }

        // --- Utility ---

        public float GetDepthZone()
        {
            if (_depth < 200) return 0;      // Surface
            if (_depth < 500) return 1;      // Twilight
            if (_depth < 1500) return 2;     // Midnight
            if (_depth < 3000) return 3;     // Abyssal
            return 4;                         // Hadal
        }

        public bool IsAtCrushDepth()
        {
            return _depth > crushDepth * 0.85f;
        }
    }
}
