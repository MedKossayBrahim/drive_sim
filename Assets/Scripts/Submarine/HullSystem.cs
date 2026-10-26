using UnityEngine;
using System;

namespace DriveSim.Submarine
{
    /// <summary>
    /// Hull integrity system. Damage from collisions, pressure, depth charges.
    /// Leaks flood compartments, affecting buoyancy and noise.
    /// </summary>
    public class HullSystem : MonoBehaviour
    {
        [Header("Hull")]
        public float maxHull = 100f;
        public float currentHull = 100f;
        public float leakThreshold = 60f;       // hull below this = leaks
        public float leakRate = 0.5f;           // flooding per second when leaking
        public float repairAmount = 25f;
        public int maxRepairs = 2;

        [Header("Pressure")]
        public float pressureDamageRate = 0.1f;  // damage per second at extreme depth
        public float pressureStartDepth = 3500f;

        [Header("State")]
        [SerializeField] private float _floodLevel;
        [SerializeField] private int _repairsLeft;
        [SerializeField] private bool _isLeaking;
        [SerializeField] private bool _isCritical;

        public float HullPercent => currentHull / maxHull;
        public float FloodLevel => _floodLevel;
        public bool IsLeaking => _isLeaking;
        public bool IsCritical => _isCritical;
        public int RepairsLeft => _repairsLeft;

        private SubmarineController _controller;
        private float _pressureTimer;

        public event Action<float> OnHullChanged;
        public event Action OnLeakStarted;
        public event Action OnLeakStopped;
        public event Action OnCritical;
        public event Action OnHullBreached;

        void Awake()
        {
            _controller = GetComponent<SubmarineController>();
            _repairsLeft = maxRepairs;
        }

        void Update()
        {
            UpdateLeaks();
            UpdatePressure();
            CheckCritical();
        }

        void UpdateLeaks()
        {
            if (currentHull < leakThreshold && !_isLeaking)
            {
                _isLeaking = true;
                OnLeakStarted?.Invoke();
            }
            else if (currentHull >= leakThreshold && _isLeaking)
            {
                _isLeaking = false;
                OnLeakStopped?.Invoke();
            }

            if (_isLeaking)
            {
                _floodLevel += leakRate * Time.deltaTime;
                _floodLevel = Mathf.Clamp(_floodLevel, 0f, 100f);

                // Flooding affects buoyancy (handled by controller via ballast offset)
                // and increases noise
                if (_controller != null)
                {
                    // Add noise from flooding
                }
            }
        }

        void UpdatePressure()
        {
            if (_controller == null) return;
            float depth = _controller.Depth;

            if (depth > pressureStartDepth)
            {
                float excess = depth - pressureStartDepth;
                _pressureTimer += Time.deltaTime;
                if (_pressureTimer > 1f)
                {
                    float damage = pressureDamageRate * (excess / 500f);
                    TakeDamage(damage);
                    _pressureTimer = 0f;

                    if (depth > _controller.crushDepth * 0.9f)
                    {
                        OnHullStress();
                    }
                }
            }
        }

        void CheckCritical()
        {
            if (currentHull < 20f && !_isCritical)
            {
                _isCritical = true;
                OnCritical?.Invoke();
            }
            else if (currentHull >= 20f && _isCritical)
            {
                _isCritical = false;
            }

            if (currentHull <= 0f)
            {
                OnHullBreached?.Invoke();
            }
        }

        public void TakeDamage(float amount)
        {
            currentHull = Mathf.Max(0f, currentHull - amount);
            OnHullChanged?.Invoke(currentHull);

            if (amount > 5f)
            {
                OnHullStress();
            }
        }

        public void Repair()
        {
            if (_repairsLeft <= 0) return;
            _repairsLeft--;
            currentHull = Mathf.Min(maxHull, currentHull + repairAmount);
            _floodLevel = Mathf.Max(0f, _floodLevel - 20f);
            OnHullChanged?.Invoke(currentHull);
        }

        void OnHullStress()
        {
            // Trigger camera shake, sound, visual feedback
            // Handled by external systems listening to events
        }

        public float GetBuoyancyOffset()
        {
            // Flooding adds weight
            return _floodLevel * 0.01f;
        }
    }
}
