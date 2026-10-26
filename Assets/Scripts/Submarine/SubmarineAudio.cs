using UnityEngine;
using System.Collections;

namespace DriveSim.Submarine
{
    /// <summary>
    /// Audio system for the submarine.
    /// Engine hum, sonar ping, hull creaks, ambient ocean, creature sounds.
    /// </summary>
    public class SubmarineAudio : MonoBehaviour
    {
        [Header("Engine")]
        public AudioClip engineLoop;
        public float engineBasePitch = 0.8f;
        public float engineMaxPitch = 1.4f;
        public float engineVolume = 0.6f;
        public float silentVolume = 0.05f;

        [Header("Sonar")]
        public AudioClip sonarPing;
        public AudioClip sonarEcho;
        public float sonarVolume = 0.8f;

        [Header("Hull")]
        public AudioClip hullCreak;
        public AudioClip hullStress;
        public AudioClip leakSound;
        public float creakInterval = 5f;
        public float creakPitchBase = 0.8f;

        [Header("Ambient")]
        public AudioClip oceanAmbient;
        public AudioClip deepAmbient;
        public float ambientVolume = 0.5f;

        [Header("Creature")]
        public AudioClip creatureCall;
        public AudioClip creatureRoar;
        public AudioClip creatureApproach;

        [Header("Alarms")]
        public AudioClip oxygenAlarm;
        public AudioClip batteryAlarm;
        public AudioClip hullAlarm;

        private SubmarineController _controller;
        private HullSystem _hull;
        private OxygenSystem _oxygen;
        private BatterySystem _battery;
        private SonarSystem _sonar;
        private LeviathanAI _creature;
        private DepthZoneManager _depthZone;

        private AudioSource _engineSource;
        private AudioSource _ambientSource;
        private AudioSource _sonarSource;
        private AudioSource _hullSource;
        private AudioSource _creatureSource;
        private AudioSource _alarmSource;

        private float _creakTimer;
        private float _lastDepth;
        private bool _oxygenAlarmPlaying;
        private bool _batteryAlarmPlaying;
        private bool _hullAlarmPlaying;

        void Awake()
        {
            _controller = GetComponent<SubmarineController>();
            _hull = GetComponent<HullSystem>();
            _oxygen = GetComponent<OxygenSystem>();
            _battery = GetComponent<BatterySystem>();
            _sonar = GetComponent<SonarSystem>();
            _depthZone = GetComponent<DepthZoneManager>();

            CreateAudioSources();
        }

        void Start()
        {
            _creature = FindFirstObjectByType<LeviathanAI>();
            SubscribeEvents();
            StartAmbient();
        }

        void CreateAudioSources()
        {
            _engineSource = CreateSource("Engine", true, engineVolume);
            _ambientSource = CreateSource("Ambient", true, ambientVolume);
            _sonarSource = CreateSource("Sonar", false, sonarVolume);
            _hullSource = CreateSource("Hull", false, 0.7f);
            _creatureSource = CreateSource("Creature", false, 0.8f);
            _alarmSource = CreateSource("Alarm", false, 0.7f);
        }

        AudioSource CreateSource(string name, bool loop, float volume)
        {
            GameObject obj = new GameObject(name + "Audio");
            obj.transform.SetParent(transform);
            AudioSource source = obj.AddComponent<AudioSource>();
            source.loop = loop;
            source.volume = volume;
            source.spatialBlend = 0f; // 2D for submarine interior
            source.playOnAwake = false;
            return source;
        }

        void SubscribeEvents()
        {
            if (_sonar != null)
            {
                _sonar.OnPing += (pos, range) => PlaySonarPing();
            }
            if (_hull != null)
            {
                _hull.OnLeakStarted += () => PlayHullSound(leakSound);
                _hull.OnCritical += () => PlayHullSound(hullStress);
            }
            if (_oxygen != null)
            {
                _oxygen.OnOxygenLow += () => PlayAlarm(oxygenAlarm, ref _oxygenAlarmPlaying);
                _oxygen.OnOxygenCritical += () => PlayAlarm(oxygenAlarm, ref _oxygenAlarmPlaying);
            }
            if (_battery != null)
            {
                _battery.OnBatteryCritical += () => PlayAlarm(batteryAlarm, ref _batteryAlarmPlaying);
            }
            if (_creature != null)
            {
                _creature.OnStateChanged += OnCreatureStateChanged;
            }
        }

        void StartAmbient()
        {
            if (oceanAmbient != null && _ambientSource != null)
            {
                _ambientSource.clip = oceanAmbient;
                _ambientSource.Play();
            }
        }

        void Update()
        {
            UpdateEngine();
            UpdateAmbient();
            UpdateHullCreaks();
            UpdateAlarms();
        }

        void UpdateEngine()
        {
            if (_engineSource == null || engineLoop == null) return;

            if (!_engineSource.isPlaying)
            {
                _engineSource.clip = engineLoop;
                _engineSource.Play();
            }

            float throttle = _controller != null ? Mathf.Abs(_controller.Throttle) : 0f;
            bool silent = _controller != null && _controller.IsSilentRunning;

            float targetPitch = Mathf.Lerp(engineBasePitch, engineMaxPitch, throttle);
            float targetVolume = silent ? silentVolume : Mathf.Lerp(silentVolume, engineVolume, throttle + 0.3f);

            _engineSource.pitch = Mathf.Lerp(_engineSource.pitch, targetPitch, Time.deltaTime * 3f);
            _engineSource.volume = Mathf.Lerp(_engineSource.volume, targetVolume, Time.deltaTime * 3f);
        }

        void UpdateAmbient()
        {
            if (_ambientSource == null) return;

            // Crossfade between surface and deep ambient based on depth
            if (_controller != null && deepAmbient != null)
            {
                float depth = _controller.Depth;
                float deepBlend = Mathf.Clamp01(depth / 2000f);

                // Simple approach: adjust volume and pitch
                _ambientSource.volume = ambientVolume * (1f - deepBlend * 0.3f);
                _ambientSource.pitch = 1f - deepBlend * 0.2f;
            }
        }

        void UpdateHullCreaks()
        {
            if (_hullSource == null || hullCreak == null) return;

            _creakTimer -= Time.deltaTime;
            if (_creakTimer <= 0f)
            {
                // Creak more at depth and when stressed
                float depthFactor = _controller != null ? _controller.Depth / 4000f : 0f;
                float stressFactor = _hull != null ? (1f - _hull.HullPercent) : 0f;

                float creakChance = 0.3f + depthFactor * 0.4f + stressFactor * 0.3f;
                if (Random.value < creakChance)
                {
                    _hullSource.pitch = creakPitchBase + Random.Range(-0.2f, 0.2f) + depthFactor * 0.3f;
                    _hullSource.volume = 0.3f + stressFactor * 0.5f;
                    _hullSource.PlayOneShot(hullCreak);
                }

                _creakTimer = creakInterval + Random.Range(-2f, 2f);
            }
        }

        void UpdateAlarms()
        {
            // Stop alarms when conditions improve
            if (_oxygen != null && _oxygen.OxygenPercent > 0.15f && _oxygenAlarmPlaying)
            {
                _alarmSource.Stop();
                _oxygenAlarmPlaying = false;
            }
            if (_battery != null && _battery.BatteryPercent > 0.25f && _batteryAlarmPlaying)
            {
                _alarmSource.Stop();
                _batteryAlarmPlaying = false;
            }
            if (_hull != null && _hull.HullPercent > 0.3f && _hullAlarmPlaying)
            {
                _alarmSource.Stop();
                _hullAlarmPlaying = false;
            }
        }

        void PlaySonarPing()
        {
            if (_sonarSource == null || sonarPing == null) return;
            _sonarSource.pitch = 1f + Random.Range(-0.05f, 0.05f);
            _sonarSource.PlayOneShot(sonarPing);

            // Delayed echo
            StartCoroutine(PlayDelayedEcho());
        }

        IEnumerator PlayDelayedEcho()
        {
            yield return new WaitForSeconds(1.5f);
            if (_sonarSource != null && sonarEcho != null)
            {
                _sonarSource.volume = sonarVolume * 0.4f;
                _sonarSource.PlayOneShot(sonarEcho);
                _sonarSource.volume = sonarVolume;
            }
        }

        void PlayHullSound(AudioClip clip)
        {
            if (_hullSource == null || clip == null) return;
            _hullSource.PlayOneShot(clip);
        }

        void PlayAlarm(AudioClip clip, ref bool isPlaying)
        {
            if (_alarmSource == null || clip == null || isPlaying) return;
            _alarmSource.clip = clip;
            _alarmSource.loop = true;
            _alarmSource.Play();
            isPlaying = true;
        }

        void OnCreatureStateChanged(LeviathanAI.State state)
        {
            if (_creatureSource == null) return;

            switch (state)
            {
                case LeviathanAI.State.Hunting:
                    if (creatureApproach != null)
                        _creatureSource.PlayOneShot(creatureApproach);
                    break;
                case LeviathanAI.State.Attacking:
                    if (creatureRoar != null)
                        _creatureSource.PlayOneShot(creatureRoar);
                    break;
                case LeviathanAI.State.Curious:
                    if (creatureCall != null)
                        _creatureSource.PlayOneShot(creatureCall);
                    break;
            }
        }

        /// <summary>
        /// Apply underwater audio filter (muffled sound).
        /// Call this when entering water.
        /// </summary>
        public void ApplyUnderwaterFilter()
        {
            // Unity Audio LowPassFilter
            var lowPass = gameObject.GetComponent<AudioLowPassFilter>();
            if (lowPass == null) lowPass = gameObject.AddComponent<AudioLowPassFilter>();
            lowPass.cutoffFrequency = 800f;
            lowPass.lowpassResonanceQ = 1f;
        }

        public void RemoveUnderwaterFilter()
        {
            var lowPass = gameObject.GetComponent<AudioLowPassFilter>();
            if (lowPass != null) Destroy(lowPass);
        }
    }
}
