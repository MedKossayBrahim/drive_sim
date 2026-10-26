using UnityEngine;
using System;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DriveSim.Submarine
{
    /// <summary>
    /// Manages depth zones. Each zone has distinct lighting, fog, ambient sound, and hazards.
    /// Drives environmental fog, ambient light, directional sunlight, and URP post-processing
    /// dynamically as the submarine descends through the ocean strata.
    /// </summary>
    public class DepthZoneManager : MonoBehaviour
    {
        [System.Serializable]
        public class DepthZone
        {
            public string name;
            public float minDepth;
            public float maxDepth;
            public Color fogColor;
            public float fogDensity;
            public Color ambientLight;
            public Color waterColor;
            public float lightIntensity;
            public float vignetteIntensity = 0.38f;
            public float chromaticIntensity = 0.12f;
            public AudioClip ambientSound;
            public float sonarRangeModifier = 1f;
        }

        [Header("Zones (Proportioned for 0 - 450m expedition)")]
        public DepthZone[] zones = new DepthZone[]
        {
            // Zone 0: Surface / Sunlit (0 - 65m) — bright turquoise waters, high visibility
            new DepthZone
            {
                name = "Surface",
                minDepth = 0f,
                maxDepth = 65f,
                fogColor = new Color(0.06f, 0.28f, 0.44f),
                fogDensity = 0.0035f,
                ambientLight = new Color(0.20f, 0.38f, 0.52f),
                waterColor = new Color(0.12f, 0.38f, 0.56f),
                lightIntensity = 1.35f,
                vignetteIntensity = 0.32f,
                chromaticIntensity = 0.08f,
                sonarRangeModifier = 1.2f
            },
            // Zone 1: Crépuscule / Twilight (65 - 165m) — royal oceanic blue, fading sunlight
            new DepthZone
            {
                name = "Crépuscule",
                minDepth = 65f,
                maxDepth = 165f,
                fogColor = new Color(0.03f, 0.14f, 0.28f),
                fogDensity = 0.0065f,
                ambientLight = new Color(0.08f, 0.18f, 0.32f),
                waterColor = new Color(0.05f, 0.16f, 0.32f),
                lightIntensity = 0.55f,
                vignetteIntensity = 0.38f,
                chromaticIntensity = 0.12f,
                sonarRangeModifier = 1.0f
            },
            // Zone 2: Minuit / Midnight (165 - 275m) — deep indigo, sunlight nearly vanished
            new DepthZone
            {
                name = "Minuit",
                minDepth = 165f,
                maxDepth = 275f,
                fogColor = new Color(0.015f, 0.06f, 0.15f),
                fogDensity = 0.005f,
                ambientLight = new Color(0.06f, 0.12f, 0.22f),
                waterColor = new Color(0.02f, 0.06f, 0.16f),
                lightIntensity = 0.10f,
                vignetteIntensity = 0.44f,
                chromaticIntensity = 0.16f,
                sonarRangeModifier = 0.85f
            },
            // Zone 3: Abyssal / Fosse (275 - 370m) — dark abyss, zero sun, headlight essential
            new DepthZone
            {
                name = "Abyssal",
                minDepth = 275f,
                maxDepth = 370f,
                fogColor = new Color(0.008f, 0.028f, 0.08f),
                fogDensity = 0.0025f,
                ambientLight = new Color(0.04f, 0.08f, 0.16f),
                waterColor = new Color(0.01f, 0.035f, 0.09f),
                lightIntensity = 0.0f,
                vignetteIntensity = 0.50f,
                chromaticIntensity = 0.22f,
                sonarRangeModifier = 0.70f
            },
            // Zone 4: Hadal / Le Gouffre (370m+) — black trench floor, beacon visible with headlight
            new DepthZone
            {
                name = "Hadal",
                minDepth = 370f,
                maxDepth = 500f,
                fogColor = new Color(0.005f, 0.015f, 0.045f),
                fogDensity = 0.003f,
                ambientLight = new Color(0.03f, 0.06f, 0.12f),
                waterColor = new Color(0.008f, 0.02f, 0.05f),
                lightIntensity = 0.0f,
                vignetteIntensity = 0.52f,
                chromaticIntensity = 0.25f,
                sonarRangeModifier = 0.55f
            }
        };

        [Header("Transition")]
        public float transitionSpeed = 1.8f;

        [Header("References")]
        public Light mainLight;
        public Camera mainCamera;
        public AudioSource ambientSource;
        public Volume postProcessVolume;

        [Header("State")]
        [SerializeField] private int _currentZoneIndex = 0;
        [SerializeField] private int _previousZoneIndex = 0;
        [SerializeField] private float _transitionProgress = 1f;

        public int CurrentZoneIndex => _currentZoneIndex;
        public DepthZone CurrentZone => zones != null && _currentZoneIndex < zones.Length ? zones[_currentZoneIndex] : null;
        public string CurrentZoneName => CurrentZone != null ? CurrentZone.name : "Surface";

        private SubmarineController _controller;
        private float _currentFogDensity;
        private Color _currentFogColor;
        private Color _currentAmbient;
        private float _currentLightIntensity;
        private float _currentVignette;
        private float _currentChroma;

        private Vignette _vignette;
        private ChromaticAberration _chroma;
        private ColorAdjustments _colorAdjustments;

        public event System.Action<int, string> OnZoneChanged;
        public event System.Action<float> OnDepthChanged;

        void Awake()
        {
            _controller = GetComponent<SubmarineController>();
            if (_controller == null) _controller = FindFirstObjectByType<SubmarineController>();

            if (zones != null && zones.Length > 0)
            {
                _currentFogDensity = zones[0].fogDensity;
                _currentFogColor = zones[0].fogColor;
                _currentAmbient = zones[0].ambientLight;
                _currentLightIntensity = zones[0].lightIntensity;
                _currentVignette = zones[0].vignetteIntensity;
                _currentChroma = zones[0].chromaticIntensity;
            }
        }

        void Start()
        {
            ResolvePostProcessing();
        }

        void ResolvePostProcessing()
        {
            if (postProcessVolume == null)
                postProcessVolume = FindFirstObjectByType<Volume>();

            if (postProcessVolume != null && postProcessVolume.sharedProfile != null)
            {
                postProcessVolume.sharedProfile.TryGet(out _vignette);
                postProcessVolume.sharedProfile.TryGet(out _chroma);
                postProcessVolume.sharedProfile.TryGet(out _colorAdjustments);
            }
        }

        void Update()
        {
            if (_controller == null)
            {
                _controller = FindFirstObjectByType<SubmarineController>();
                if (_controller == null) return;
            }

            float depth = _controller.Depth;
            OnDepthChanged?.Invoke(depth);

            // Find current zone based on depth
            int newZone = 0;
            for (int i = 0; i < zones.Length; i++)
            {
                if (depth >= zones[i].minDepth && depth < zones[i].maxDepth)
                {
                    newZone = i;
                    break;
                }
                if (i == zones.Length - 1) newZone = i;
            }

            if (newZone != _currentZoneIndex)
            {
                _previousZoneIndex = _currentZoneIndex;
                _currentZoneIndex = newZone;
                _transitionProgress = 0f;
                OnZoneChanged?.Invoke(newZone, zones[newZone].name);
            }

            // Smooth transition between zones
            if (_transitionProgress < 1f)
            {
                _transitionProgress += Time.deltaTime * transitionSpeed;
                _transitionProgress = Mathf.Clamp01(_transitionProgress);

                var prev = zones[_previousZoneIndex];
                var curr = zones[_currentZoneIndex];

                _currentFogDensity = Mathf.Lerp(prev.fogDensity, curr.fogDensity, _transitionProgress);
                _currentFogColor = Color.Lerp(prev.fogColor, curr.fogColor, _transitionProgress);
                _currentAmbient = Color.Lerp(prev.ambientLight, curr.ambientLight, _transitionProgress);
                _currentLightIntensity = Mathf.Lerp(prev.lightIntensity, curr.lightIntensity, _transitionProgress);
                _currentVignette = Mathf.Lerp(prev.vignetteIntensity, curr.vignetteIntensity, _transitionProgress);
                _currentChroma = Mathf.Lerp(prev.chromaticIntensity, curr.chromaticIntensity, _transitionProgress);
            }

            ApplyZoneVisuals();
        }

        void ApplyZoneVisuals()
        {
            // Environmental fog
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = _currentFogDensity;
            RenderSettings.fogColor = _currentFogColor;

            // Ambient sky lighting
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = _currentAmbient;

            // Directional sun
            if (mainLight != null)
            {
                mainLight.intensity = _currentLightIntensity;
                mainLight.color = Color.Lerp(new Color(0.65f, 0.85f, 1f), _currentFogColor, 0.45f);
            }

            // Camera background
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera != null)
            {
                mainCamera.backgroundColor = _currentFogColor;
            }

            // Dynamically update URP Post Processing volume overrides
            if (_vignette != null)
            {
                _vignette.intensity.Override(_currentVignette);
                _vignette.color.Override(Color.Lerp(new Color(0.01f, 0.04f, 0.08f), Color.black, _currentZoneIndex / 4f));
            }
            if (_chroma != null)
            {
                _chroma.intensity.Override(_currentChroma);
            }
            if (_colorAdjustments != null)
            {
                _colorAdjustments.colorFilter.Override(Color.Lerp(
                    new Color(0.88f, 0.95f, 1f),
                    new Color(0.65f, 0.82f, 0.95f),
                    _currentZoneIndex / 4f));
            }

            // Ambient audio
            if (ambientSource != null && CurrentZone != null && CurrentZone.ambientSound != null)
            {
                if (ambientSource.clip != CurrentZone.ambientSound)
                {
                    ambientSource.clip = CurrentZone.ambientSound;
                    ambientSource.Play();
                }
            }
        }

        public float GetSonarRangeModifier()
        {
            return CurrentZone != null ? CurrentZone.sonarRangeModifier : 1f;
        }

        public Color GetWaterColor()
        {
            return CurrentZone != null ? CurrentZone.waterColor : new Color(0.05f, 0.2f, 0.4f);
        }
    }
}
