using UnityEngine;
using System;

namespace DriveSim.Submarine
{
    /// <summary>
    /// Mobile magnetometer / compass sensor.
    /// Reads the phone's real compass heading and exposes it
    /// for a diegetic compass display in the submarine HUD.
    /// </summary>
    public class CompassSensor : MonoBehaviour
    {
        [Header("Compass")]
        public float smoothing = 4f;

        [Header("State (read-only)")]
        [SerializeField] private float _heading;
        [SerializeField] private bool _isActive;

        public float Heading => _heading;
        public bool IsActive => _isActive;

        public event Action<float> OnHeadingChanged;

        private float _smoothedHeading;

        void Start()
        {
            Input.compass.enabled = true;
            _isActive = Input.compass.enabled;
            if (!_isActive)
                Debug.LogWarning("CompassSensor: compass not available on this device.");
        }

        void Update()
        {
            if (!_isActive) return;

            float raw = Input.compass.trueHeading;
            // Smooth shortest-path angle
            float diff = Mathf.DeltaAngle(_smoothedHeading, raw);
            _smoothedHeading = Mathf.MoveTowardsAngle(_smoothedHeading, raw, Mathf.Abs(diff) * Time.deltaTime * smoothing);
            _heading = _smoothedHeading;

            OnHeadingChanged?.Invoke(_heading);
        }
    }
}