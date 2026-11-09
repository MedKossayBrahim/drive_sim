using UnityEngine;
using System;

namespace DriveSim.Submarine
{
    /// <summary>
    /// Oxygen/life support system.
    /// Finite air supply. Consumption increases with exertion (throttle, sonar, stress).
    /// </summary>
    public class OxygenSystem : MonoBehaviour
    {
        [Header("Oxygen")]
        public float maxOxygen = 100f;
        public float currentOxygen = 100f;
        public float baseConsumption = 0.5f;     // per second at rest
        public float exertionMultiplier = 1.5f;  // when throttling
        public float stressMultiplier = 2f;      // when hull critical / creature near
        public float sonarConsumption = 2f;     // per ping

        [Header("State")]
        [SerializeField] private float _consumptionRate;
        [SerializeField] private bool _isLow;
        [SerializeField] private bool _isCritical;
        [SerializeField] private bool _isDepleted;

        public float OxygenPercent => currentOxygen / maxOxygen;
        public float ConsumptionRate => _consumptionRate;
        public bool IsLow => _isLow;
        public bool IsCritical => _isCritical;
        public bool IsDepleted => _isDepleted;

        private SubmarineController _controller;
        private HullSystem _hull;
        private SonarSystem _sonar;
        private LeviathanAI _creature;

        public event Action<float> OnOxygenChanged;
        public event Action OnOxygenLow;
        public event Action OnOxygenCritical;
        public event Action OnOxygenDepleted;

        void Awake()
        {
            _controller = GetComponent<SubmarineController>();
            _hull = GetComponent<HullSystem>();
            _sonar = GetComponent<SonarSystem>();
            _creature = FindFirstObjectByType<LeviathanAI>();
        }

        void Update()
        {
            UpdateConsumption();
            UpdateOxygen();
            CheckThresholds();
        }

        void UpdateConsumption()
        {
            _consumptionRate = baseConsumption;

            // Exertion from movement
            if (_controller != null)
            {
                _consumptionRate += Mathf.Abs(_controller.Throttle) * (exertionMultiplier - 1f) * 0.5f;
            }

            // Stress from hull damage
            if (_hull != null && _hull.IsCritical)
            {
                _consumptionRate *= stressMultiplier;
            }

            // Stress from creature proximity
            if (_creature != null)
            {
                float dist = Vector3.Distance(transform.position, _creature.transform.position);
                if (dist < 100f)
                {
                    _consumptionRate *= 1.5f;
                }
            }

            // Silent running reduces consumption slightly
            if (_controller != null && _controller.IsSilentRunning)
            {
                _consumptionRate *= 0.8f;
            }
        }

        void UpdateOxygen()
        {
            currentOxygen -= _consumptionRate * Time.deltaTime;
            currentOxygen = Mathf.Clamp(currentOxygen, 0f, maxOxygen);
            OnOxygenChanged?.Invoke(currentOxygen);

            if (currentOxygen <= 0f && !_isDepleted)
            {
                _isDepleted = true;
                OnOxygenDepleted?.Invoke();
            }
        }

        void CheckThresholds()
        {
            if (currentOxygen < 30f && !_isLow)
            {
                _isLow = true;
                OnOxygenLow?.Invoke();
            }
            else if (currentOxygen >= 30f && _isLow)
            {
                _isLow = false;
            }

            if (currentOxygen < 10f && !_isCritical)
            {
                _isCritical = true;
                OnOxygenCritical?.Invoke();
            }
            else if (currentOxygen >= 10f && _isCritical)
            {
                _isCritical = false;
            }
        }

        public void ConsumeOxygen(float amount)
        {
            currentOxygen = Mathf.Max(0f, currentOxygen - amount);
            OnOxygenChanged?.Invoke(currentOxygen);
        }

        public void RestoreOxygen(float amount)
        {
            currentOxygen = Mathf.Min(maxOxygen, currentOxygen + amount);
            OnOxygenChanged?.Invoke(currentOxygen);
        }
    }
}
