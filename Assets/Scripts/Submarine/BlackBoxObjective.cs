using UnityEngine;
using System;

namespace DriveSim.Submarine
{
    /// <summary>
    /// The black box objective. Player must stay near it for extraction time.
    /// Emits a beacon sound and sonar signature.
    /// </summary>
    public class BlackBoxObjective : MonoBehaviour
    {
        [Header("Visual")]
        public Color glowColor = new Color(1f, 0.9f, 0.2f);
        public float glowIntensity = 3f;
        public float pulseSpeed = 2f;
        public float pulseAmount = 0.5f;

        [Header("Audio")]
        public AudioClip beaconPulse;
        public float pulseInterval = 3f;
        public float beaconRange = 300f;

        [Header("Sonar")]
        public SonarTargetType sonarType = SonarTargetType.Objective;

        private SonarTarget _sonarTarget;
        private Renderer _renderer;
        private Material _material;
        private AudioSource _audioSource;
        private float _pulseTimer;
        private bool _isExtracted = false;
        private GameManager _gameManager;

        void Awake()
        {
            _sonarTarget = GetComponent<SonarTarget>();
            if (_sonarTarget == null)
            {
                _sonarTarget = gameObject.AddComponent<SonarTarget>();
            }
            _sonarTarget.TargetType = sonarType;
            _sonarTarget.IsMoving = false;

            _renderer = GetComponent<Renderer>();
            if (_renderer != null)
            {
                _material = _renderer.material;
            }

            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.spatialBlend = 1f;
            _audioSource.maxDistance = beaconRange;
            _audioSource.rolloffMode = AudioRolloffMode.Linear;

            _gameManager = FindFirstObjectByType<GameManager>();
        }

        void Update()
        {
            if (_isExtracted) return;

            UpdateVisuals();
            UpdateAudio();
        }

        void UpdateVisuals()
        {
            if (_material == null) return;

            // Pulsing glow
            float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
            float fastPulse = (Mathf.Sin(Time.time * pulseSpeed * 3f) + 1f) * 0.5f;

            // Distance-based intensity boost: brighter when submarine is closer
            float distBoost = 1f;
            var sub = FindFirstObjectByType<SubmarineController>();
            if (sub != null)
            {
                float dist = Vector3.Distance(transform.position, sub.transform.position);
                // Ramp up intensity as sub gets within 200m, max boost at 20m
                distBoost = Mathf.Lerp(5.0f, 1f, Mathf.Clamp01((dist - 20f) / 180f));
                // Add urgency pulse when very close
                if (dist < 80f)
                {
                    float urgency = Mathf.Lerp(0f, 1.0f, 1f - Mathf.Clamp01(dist / 80f));
                    distBoost += fastPulse * urgency;
                }
            }

            float intensity = glowIntensity * 2f * (1f - pulseAmount + pulse * pulseAmount) * distBoost;
            _material.SetColor("_EmissionColor", glowColor * intensity);

            // Also scale the object slightly for a breathing effect
            float breathe = 1f + pulse * 0.06f;
            transform.localScale = Vector3.one * breathe;
        }

        void UpdateAudio()
        {
            _pulseTimer -= Time.deltaTime;
            if (_pulseTimer <= 0f)
            {
                _pulseTimer = pulseInterval;
                if (beaconPulse != null && _audioSource != null)
                {
                    _audioSource.PlayOneShot(beaconPulse);
                }
            }
        }

        public void OnExtracted()
        {
            _isExtracted = true;
            if (_sonarTarget != null)
            {
                _sonarTarget.IsRevealed = false;
            }

            // Visual: stop pulsing, turn green
            if (_material != null)
            {
                _material.SetColor("_EmissionColor", Color.green * 2f);
            }

            // Disable collider
            var collider = GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = glowColor;
            Gizmos.DrawWireSphere(transform.position, 15f);
            Gizmos.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0.2f);
            Gizmos.DrawSphere(transform.position, 15f);
        }
    }
}
