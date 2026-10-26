using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace DriveSim.Submarine
{
    /// <summary>
    /// Sonar system. Active pings reveal the environment but increase detection.
    /// Passive sonar shows moving contacts without noise.
    /// The core tension: to see, you must be heard.
    /// </summary>
    public class SonarSystem : MonoBehaviour
    {
        [Header("Active Ping")]
        public float pingRange = 200f;
        public float pingCooldown = 3f;
        public float pingNoiseBurst = 0.6f;       // noise spike from pinging
        public float pingRevealDuration = 5f;      // how long revealed objects stay visible
        public float pingCost = 5f;               // battery cost

        [Header("Detection")]
        public float detectionDecayRate = 0.1f;
        public float detectionThreshold = 0.7f;   // above this = creature tracks
        public float ambientNoiseFloor = 0.05f;

        [Header("Passive Sonar")]
        public float passiveRange = 150f;
        public float passiveUpdateInterval = 0.5f;

        [Header("State")]
        [SerializeField] private float _detectionLevel;
        [SerializeField] private float _pingCooldownTimer;
        [SerializeField] private bool _canPing = true;
        [SerializeField] private List<SonarContact> _contacts = new List<SonarContact>();

        public float DetectionLevel => _detectionLevel;
        public bool CanPing => _canPing;
        public float PingCooldownRemaining => _pingCooldownTimer;
        public List<SonarContact> Contacts => _contacts;

        private SubmarineController _controller;
        private BatterySystem _battery;
        private float _passiveTimer;
        private bool _isPassiveMode = false;

        // Events
        public event System.Action<Vector3, float> OnPing;           // position, range
        public event System.Action<float> OnDetectionChanged;
        public event System.Action<SonarContact> OnNewContact;
        public event System.Action OnPingCooldownReady;

        void Awake()
        {
            _controller = GetComponent<SubmarineController>();
            _battery = GetComponent<BatterySystem>();
        }

        void Update()
        {
            UpdatePingCooldown();
            UpdateDetection();
            UpdatePassiveSonar();
        }

        void UpdatePingCooldown()
        {
            if (_pingCooldownTimer > 0f)
            {
                _pingCooldownTimer -= Time.deltaTime;
                if (_pingCooldownTimer <= 0f)
                {
                    _pingCooldownTimer = 0f;
                    _canPing = true;
                    OnPingCooldownReady?.Invoke();
                }
            }
        }

        void UpdateDetection()
        {
            // Decay detection
            _detectionLevel = Mathf.Max(ambientNoiseFloor,
                _detectionLevel - detectionDecayRate * Time.deltaTime);

            // Add engine noise
            if (_controller != null)
            {
                float engineNoise = _controller.NoiseLevel * 0.3f;
                _detectionLevel = Mathf.Max(_detectionLevel, engineNoise);
            }

            OnDetectionChanged?.Invoke(_detectionLevel);
        }

        void UpdatePassiveSonar()
        {
            _passiveTimer -= Time.deltaTime;
            if (_passiveTimer <= 0f)
            {
                _passiveTimer = passiveUpdateInterval;
                ScanPassive();
            }
        }

        void ScanPassive()
        {
            _contacts.Clear();

            // Find all sonar targets in range
            Collider[] hits = Physics.OverlapSphere(transform.position, passiveRange);
            foreach (var hit in hits)
            {
                var target = hit.GetComponent<SonarTarget>();
                if (target == null || !target.IsMoving) continue;

                float dist = Vector3.Distance(transform.position, hit.transform.position);
                if (dist > passiveRange) continue;

                var contact = new SonarContact
                {
                    position = hit.transform.position,
                    type = target.TargetType,
                    distance = dist,
                    isMoving = true,
                    lastSeen = Time.time
                };
                _contacts.Add(contact);
                OnNewContact?.Invoke(contact);
            }
        }

        /// <summary>
        /// Fire an active sonar ping.
        /// </summary>
        public void Ping()
        {
            if (!_canPing) return;
            if (_battery != null && !_battery.ConsumeBattery(pingCost)) return;

            _canPing = false;
            _pingCooldownTimer = pingCooldown;

            // Noise spike
            _detectionLevel = Mathf.Min(1f, _detectionLevel + pingNoiseBurst);

            // Reveal all targets in range
            Collider[] hits = Physics.OverlapSphere(transform.position, pingRange);
            foreach (var hit in hits)
            {
                var target = hit.GetComponent<SonarTarget>();
                if (target != null)
                {
                    target.Reveal(pingRevealDuration);
                }
            }

            // Visual/audio event
            OnPing?.Invoke(transform.position, pingRange);

            // Add all revealed targets as contacts
            foreach (var hit in hits)
            {
                var target = hit.GetComponent<SonarTarget>();
                if (target == null) continue;

                float dist = Vector3.Distance(transform.position, hit.transform.position);
                var contact = new SonarContact
                {
                    position = hit.transform.position,
                    type = target.TargetType,
                    distance = dist,
                    isMoving = target.IsMoving,
                    lastSeen = Time.time,
                    isRevealed = true
                };
                _contacts.Add(contact);
            }
        }

        public void SetPassiveMode(bool passive)
        {
            _isPassiveMode = passive;
            if (passive)
            {
                pingRange = 0f; // no active pings
            }
            else
            {
                pingRange = 200f;
            }
        }

        public bool IsCreatureTracking()
        {
            return _detectionLevel >= detectionThreshold;
        }

        public float GetDetectionPercent()
        {
            return _detectionLevel;
        }
    }

    public class SonarContact
    {
        public Vector3 position;
        public SonarTargetType type;
        public float distance;
        public bool isMoving;
        public float lastSeen;
        public bool isRevealed;
    }

    public enum SonarTargetType
    {
        Terrain,
        Structure,
        Creature,
        Objective,
        Mine,
        Debris
    }

    /// <summary>
    /// Attach to any object that should appear on sonar.
    /// </summary>
    public class SonarTarget : MonoBehaviour
    {
        public SonarTargetType TargetType = SonarTargetType.Terrain;
        public bool IsMoving = false;
        public bool IsRevealed = false;
        public float RevealTimer = 0f;

        void Update()
        {
            if (IsRevealed)
            {
                RevealTimer -= Time.deltaTime;
                if (RevealTimer <= 0f)
                {
                    IsRevealed = false;
                }
            }
        }

        public void Reveal(float duration)
        {
            IsRevealed = true;
            RevealTimer = duration;
        }
    }
}
