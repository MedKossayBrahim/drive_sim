using UnityEngine;
using System.Collections;

namespace DriveSim.Submarine
{
    /// <summary>
    /// Ambient marine life. Fish schools, jellyfish, bioluminescent plankton.
    /// Reacts to submarine light and sonar.
    /// </summary>
    public class MarineLife : MonoBehaviour
    {
        [Header("Fish School")]
        public GameObject fishPrefab;
        public int schoolSize = 20;
        public float schoolRadius = 10f;
        public float swimSpeed = 3f;
        public float turnSpeed = 2f;
        public float scatterRadius = 30f;
        public float scatterSpeed = 12f;

        [Header("Behavior")]
        public float wanderRadius = 50f;
        public float wanderInterval = 5f;
        public float sonarScatterRadius = 80f;
        public float lightAttractionRadius = 40f;

        [Header("Bioluminescence")]
        public bool isBioluminescent = false;
        public Color glowColor = new Color(0.2f, 1f, 0.8f);
        public float glowIntensity = 2f;
        public float pulseSpeed = 1.5f;

        private Vector3 _schoolCenter;
        private Vector3 _wanderTarget;
        private float _wanderTimer;
        private Transform[] _fish;
        private Renderer[] _fishRenderers;
        private MaterialPropertyBlock _propBlock;
        private Vector3[] _fishOffsets;
        private Vector3[] _fishVelocities;
        private bool _isScattered = false;
        private float _scatterTimer;
        private SubmarineController _sub;
        private SonarSystem _sonar;

        void Start()
        {
            _sub = FindFirstObjectByType<SubmarineController>();
            _sonar = FindFirstObjectByType<SonarSystem>();
            _schoolCenter = transform.position;
            _wanderTarget = _schoolCenter;

            if (fishPrefab != null)
            {
                SpawnSchool();
            }
        }

        void SpawnSchool()
        {
            _fish = new Transform[schoolSize];
            _fishRenderers = new Renderer[schoolSize];
            _fishOffsets = new Vector3[schoolSize];
            _fishVelocities = new Vector3[schoolSize];
            _propBlock = new MaterialPropertyBlock();

            for (int i = 0; i < schoolSize; i++)
            {
                Vector3 offset = Random.insideUnitSphere * schoolRadius;
                _fishOffsets[i] = offset;
                _fishVelocities[i] = Random.insideUnitSphere * 0.5f;

                GameObject fish = Instantiate(fishPrefab, transform.position + offset, Random.rotation, transform);
                _fish[i] = fish.transform;
                var r = fish.GetComponent<Renderer>();
                _fishRenderers[i] = r;

                // Bioluminescence
                if (isBioluminescent && r != null)
                {
                    _propBlock.SetColor("_EmissionColor", glowColor * glowIntensity);
                    r.SetPropertyBlock(_propBlock);
                }
            }
        }

        void Update()
        {
            UpdateWander();
            UpdateScatter();
            UpdateFish();
            UpdateBioluminescence();
        }

        void UpdateWander()
        {
            _wanderTimer -= Time.deltaTime;
            if (_wanderTimer <= 0f)
            {
                _wanderTimer = wanderInterval + Random.Range(-1f, 1f);
                _wanderTarget = _schoolCenter + Random.insideUnitSphere * wanderRadius;
            }

            // Move school center toward wander target
            _schoolCenter = Vector3.Lerp(_schoolCenter, _wanderTarget, Time.deltaTime * 0.5f);
        }

        void UpdateScatter()
        {
            // Scatter from sonar pings
            if (_sonar != null && _sonar.PingCooldownRemaining > pingCooldownThreshold())
            {
                // Just pinged
                if (!_isScattered)
                {
                    float dist = Vector3.Distance(_schoolCenter, _sub != null ? _sub.transform.position : Vector3.zero);
                    if (dist < sonarScatterRadius)
                    {
                        _isScattered = true;
                        _scatterTimer = 3f;
                    }
                }
            }

            if (_isScattered)
            {
                _scatterTimer -= Time.deltaTime;
                if (_scatterTimer <= 0f)
                {
                    _isScattered = false;
                }
            }

            // Scatter from submarine light
            if (_sub != null)
            {
                float dist = Vector3.Distance(_schoolCenter, _sub.transform.position);
                if (dist < lightAttractionRadius && _sub.LightsOn)
                {
                    // Fish attracted to light (move toward sub)
                    _wanderTarget = Vector3.Lerp(_wanderTarget, _sub.transform.position, Time.deltaTime * 0.3f);
                }
            }
        }

        float pingCooldownThreshold()
        {
            return 0.1f;
        }

        void UpdateFish()
        {
            if (_fish == null) return;

            for (int i = 0; i < _fish.Length; i++)
            {
                if (_fish[i] == null) continue;

                Vector3 targetPos;
                if (_isScattered)
                {
                    // Scatter away from submarine
                    Vector3 awayDir = (_fish[i].position - (_sub != null ? _sub.transform.position : Vector3.zero)).normalized;
                    targetPos = _fish[i].position + awayDir * scatterSpeed * Time.deltaTime;
                }
                else
                {
                    // School behavior
                    Vector3 toCenter = _schoolCenter + _fishOffsets[i] - _fish[i].position;
                    targetPos = _fish[i].position + toCenter * 0.5f * Time.deltaTime;

                    // Add wandering
                    targetPos += _fishVelocities[i] * Time.deltaTime;

                    // Update velocity
                    _fishVelocities[i] += Random.insideUnitSphere * 0.1f * Time.deltaTime;
                    _fishVelocities[i] = Vector3.ClampMagnitude(_fishVelocities[i], 1f);
                }

                // Smooth movement
                _fish[i].position = Vector3.Lerp(_fish[i].position, targetPos,
                    (_isScattered ? scatterSpeed : swimSpeed) * Time.deltaTime);

                // Face movement direction
                if (_fishVelocities[i].sqrMagnitude > 0.01f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(_fishVelocities[i]);
                    _fish[i].rotation = Quaternion.Slerp(_fish[i].rotation, targetRot, turnSpeed * Time.deltaTime);
                }
            }
        }

        void UpdateBioluminescence()
        {
            if (!isBioluminescent || _fishRenderers == null) return;

            float pulse = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
            Color curColor = glowColor * (glowIntensity * (0.5f + pulse * 0.5f));

            if (_propBlock == null) _propBlock = new MaterialPropertyBlock();
            _propBlock.SetColor("_EmissionColor", curColor);

            for (int i = 0; i < _fishRenderers.Length; i++)
            {
                if (_fishRenderers[i] != null)
                    _fishRenderers[i].SetPropertyBlock(_propBlock);
            }
        }
    }

    /// <summary>
    /// Bioluminescent plankton. Tiny glowing particles that drift in the water.
    /// Reacts to submarine movement (displaced by wake).
    /// </summary>
    public class BioluminescentPlankton : MonoBehaviour
    {
        [Header("Plankton")]
        public int count = 200;
        public float spawnRadius = 50f;
        public float glowIntensity = 1f;
        public Color glowColor = new Color(0.3f, 1f, 0.8f);
        public float driftSpeed = 0.5f;
        public float wakeDisplacement = 5f;

        private Vector3[] _positions;
        private float[] _sizes;
        private float[] _phases;
        private ParticleSystem _particles;
        private SubmarineController _sub;

        void Start()
        {
            _sub = FindFirstObjectByType<SubmarineController>();
            CreatePlankton();
        }

        void CreatePlankton()
        {
            _positions = new Vector3[count];
            _sizes = new float[count];
            _phases = new float[count];

            for (int i = 0; i < count; i++)
            {
                _positions[i] = transform.position + Random.insideUnitSphere * spawnRadius;
                _sizes[i] = Random.Range(0.05f, 0.2f);
                _phases[i] = Random.Range(0f, Mathf.PI * 2f);
            }

            // Create particle system
            _particles = gameObject.AddComponent<ParticleSystem>();
            var main = _particles.main;
            main.startColor = glowColor;
            main.startSize = 0.1f;
            main.startLifetime = 10f;
            main.maxParticles = count;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = _particles.emission;
            emission.rateOverTime = 0;

            var shape = _particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = spawnRadius;

            // Emit initial particles
            for (int i = 0; i < count; i++)
            {
                var emitParams = new ParticleSystem.EmitParams();
                emitParams.position = _positions[i];
                emitParams.startColor = glowColor;
                emitParams.startSize = _sizes[i];
                _particles.Emit(emitParams, 1);
            }
        }

        void Update()
        {
            if (_particles == null) return;

            // Update particle positions with drift and wake displacement
            var particles = new ParticleSystem.Particle[_particles.particleCount];
            _particles.GetParticles(particles);

            for (int i = 0; i < particles.Length; i++)
            {
                // Drift
                particles[i].position += Vector3.up * driftSpeed * Time.deltaTime * 0.1f;

                // Wake displacement from submarine
                if (_sub != null)
                {
                    Vector3 toSub = particles[i].position - _sub.transform.position;
                    float dist = toSub.magnitude;
                    if (dist < 20f)
                    {
                        Vector3 displacement = -_sub.Velocity * (1f - dist / 20f) * wakeDisplacement * Time.deltaTime;
                        particles[i].position += displacement;
                    }
                }

                // Pulse alpha
                float pulse = (Mathf.Sin(Time.time * 2f + _phases[i % _phases.Length]) + 1f) * 0.5f;
                Color c = glowColor;
                c.a = 0.3f + pulse * 0.7f;
                particles[i].startColor = c;
            }

            _particles.SetParticles(particles, particles.Length);
        }
    }
}
