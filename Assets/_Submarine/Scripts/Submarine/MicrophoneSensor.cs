using UnityEngine;
using System;

namespace DriveSim.Submarine
{
    /// <summary>
    /// Mobile microphone sensor.
    /// Captures real-world ambient noise and exposes a 0-1 noise level
    /// that feeds into the submarine's sonar detection system.
    /// The player must stay physically quiet to avoid attracting the creature.
    /// </summary>
    public class MicrophoneSensor : MonoBehaviour
    {
        [Header("Microphone")]
        public int sampleWindow = 128;
        public float sensitivity = 2f;
        public float smoothing = 8f;

        [Header("State (read-only)")]
        [SerializeField] private float _noiseLevel;
        [SerializeField] private bool _isListening;

        public float NoiseLevel => _noiseLevel;
        public bool IsListening => _isListening;

        public event Action<float> OnNoiseChanged;

        private AudioClip _micClip;
        private string _deviceName;
        private float[] _samples;
        private float _smoothedLevel;

        void Start()
        {
            _samples = new float[sampleWindow];
            if (Microphone.devices.Length == 0)
            {
                Debug.LogWarning("MicrophoneSensor: no microphone available.");
                return;
            }
            _deviceName = Microphone.devices[0];
            _micClip = Microphone.Start(_deviceName, true, 1, 44100);
            _isListening = true;
        }

        void Update()
        {
            if (!_isListening || _micClip == null) return;

            int micPosition = Microphone.GetPosition(_deviceName) - sampleWindow;
            if (micPosition < 0) return;

            _micClip.GetData(_samples, micPosition);

            // RMS amplitude
            float sum = 0f;
            for (int i = 0; i < _samples.Length; i++)
                sum += _samples[i] * _samples[i];
            float rms = Mathf.Sqrt(sum / _samples.Length);

            // Scale and smooth
            float raw = Mathf.Clamp01(rms * sensitivity);
            _smoothedLevel = Mathf.Lerp(_smoothedLevel, raw, Time.deltaTime * smoothing);
            _noiseLevel = _smoothedLevel;

            OnNoiseChanged?.Invoke(_noiseLevel);
        }

        void OnDestroy()
        {
            if (_isListening && !string.IsNullOrEmpty(_deviceName))
                Microphone.End(_deviceName);
        }
    }
}