using UnityEngine;
using System;

namespace DriveSim.Submarine
{
    /// <summary>
    /// Battery/power management.
    /// Everything draws from the same battery: lights, sonar, ballast pumps, life support.
    /// </summary>
    public class BatterySystem : MonoBehaviour
    {
        [Header("Battery")]
        public float maxBattery = 100f;
        public float currentBattery = 100f;

        [Header("Drain Rates (per second)")]
        public float baseDrain = 0.1f;           // life support minimum
        public float lightDrain = 0.3f;          // per light
        public float sonarDrain = 0.5f;          // per ping (instant)
        public float engineDrainMultiplier = 0.2f; // multiplied by throttle
        public float ballastDrain = 0.4f;        // when pumping
        public float silentDrain = -0.1f;        // silent running saves power

        [Header("Recharge")]
        public float rechargeRate = 0f;          // surface recharge (0 = none, set if needed)
        public float rechargeDepth = 50f;        // must be above this to recharge

        [Header("State")]
        [SerializeField] private float _drainRate;
        [SerializeField] private bool _isCritical;
        [SerializeField] private bool _lightsOn = true;

        public float BatteryPercent => currentBattery / maxBattery;
        public float DrainRate => _drainRate;
        public bool IsCritical => _isCritical;
        public bool LightsOn => _lightsOn;

        private SubmarineController _controller;
        private HullSystem _hull;

        public event Action<float> OnBatteryChanged;
        public event Action OnBatteryCritical;
        public event Action OnBatteryDepleted;

        void Awake()
        {
            _controller = GetComponent<SubmarineController>();
            _hull = GetComponent<HullSystem>();
        }

        void Update()
        {
            UpdateDrain();
            UpdateBattery();
            CheckCritical();
        }

        void UpdateDrain()
        {
            _drainRate = baseDrain;

            // Engine drain
            if (_controller != null)
            {
                _drainRate += Mathf.Abs(_controller.Throttle) * engineDrainMultiplier;
            }

            // Lights
            if (_lightsOn)
            {
                _drainRate += lightDrain;
            }

            // Ballast pumping
            if (_controller != null && Mathf.Abs(_controller.Ballast) > 0.1f)
            {
                _drainRate += ballastDrain;
            }

            // Silent running saves power
            if (_controller != null && _controller.IsSilentRunning)
            {
                _drainRate += silentDrain;
            }

            // Leaks drain extra (pumps working harder)
            if (_hull != null && _hull.IsLeaking)
            {
                _drainRate += 0.3f;
            }

            // Recharge near surface
            if (_controller != null && _controller.Depth < rechargeDepth && rechargeRate > 0f)
            {
                _drainRate -= rechargeRate;
            }

            _drainRate = Mathf.Max(0f, _drainRate);
        }

        void UpdateBattery()
        {
            currentBattery -= _drainRate * Time.deltaTime;
            currentBattery = Mathf.Clamp(currentBattery, 0f, maxBattery);
            OnBatteryChanged?.Invoke(currentBattery);

            if (currentBattery <= 0f)
            {
                OnBatteryDepleted?.Invoke();
                // Kill lights, sonar, engine
                _lightsOn = false;
            }
        }

        void CheckCritical()
        {
            if (currentBattery < 15f && !_isCritical)
            {
                _isCritical = true;
                OnBatteryCritical?.Invoke();
            }
            else if (currentBattery >= 15f && _isCritical)
            {
                _isCritical = false;
            }
        }

        public bool ConsumeBattery(float amount)
        {
            if (currentBattery < amount) return false;
            currentBattery -= amount;
            OnBatteryChanged?.Invoke(currentBattery);
            return true;
        }

        public void ToggleLights()
        {
            _lightsOn = !_lightsOn;
            if (_controller != null) _controller.ToggleLights();
        }

        public void SetLights(bool on)
        {
            _lightsOn = on;
        }
    }
}
