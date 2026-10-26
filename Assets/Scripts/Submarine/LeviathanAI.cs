using UnityEngine;
using System.Collections;

namespace DriveSim.Submarine
{
    /// <summary>
    /// The creature that hunts by sound.
    /// States: Dormant → Curious → Tracking → Hunting → Attacking
    /// It is blind. It follows noise. It remembers where you pinged.
    /// </summary>
    public class LeviathanAI : MonoBehaviour
    {
        public enum State { Dormant, Curious, Tracking, Hunting, Attacking, Searching }

        [Header("Movement")]
        public float swimSpeed = 8f;
        public float huntSpeed = 16f;
        public float turnSpeed = 2f;
        public float acceleration = 3f;

        [Header("Senses")]
        public float hearingRange = 300f;
        public float trackingRange = 150f;
        public float attackRange = 20f;
        public float memoryDuration = 15f;       // remembers last known position

        [Header("Behavior")]
        public float curiousThreshold = 0.3f;
        public float trackingThreshold = 0.5f;
        public float huntingThreshold = 0.7f;
        public float attackCooldown = 5f;
        public float searchDuration = 10f;

        [Header("Visual")]
        public float glowIntensity = 1f;
        public Color glowColor = new Color(0.2f, 0.8f, 1f);

        [Header("State (read-only)")]
        [SerializeField] private State _currentState = State.Dormant;
        [SerializeField] private Vector3 _lastKnownPosition;
        [SerializeField] private float _timeSinceLastHeard;
        [SerializeField] private float _attackTimer;
        [SerializeField] private float _searchTimer;

        public State CurrentState => _currentState;
        public Vector3 LastKnownPosition => _lastKnownPosition;

        private Transform _target;
        private SonarSystem _sonar;
        private Rigidbody _rb;
        private Vector3 _currentVelocity;
        private float _currentSpeed;
        private System.Collections.Generic.List<Renderer> _glowRenderers;
        private MaterialPropertyBlock _propBlock;
        private float _glowLevel;

        // Events
        public event System.Action<State> OnStateChanged;
        public event System.Action OnAttack;
        public event System.Action OnLostTarget;

        void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            if (_rb == null) _rb = gameObject.AddComponent<Rigidbody>();
            _rb.isKinematic = true;
            _rb.useGravity = false;

            _propBlock = new MaterialPropertyBlock();
            _glowRenderers = new System.Collections.Generic.List<Renderer>();
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (r.sharedMaterial != null && (r.sharedMaterial.IsKeywordEnabled("_EMISSION") || r.sharedMaterial.HasProperty("_EmissionColor")))
                {
                    _glowRenderers.Add(r);
                }
            }
        }

        void Start()
        {
            // Find the submarine
            var sub = FindFirstObjectByType<SubmarineController>();
            if (sub != null) _target = sub.transform;

            _sonar = _target?.GetComponent<SonarSystem>();
        }

        void Update()
        {
            if (_target == null) return;

            UpdateState();
            UpdateMovement();
            UpdateVisuals();
        }

        void UpdateState()
        {
            float noise = _sonar != null ? _sonar.GetDetectionPercent() : 0f;
            float distToTarget = Vector3.Distance(transform.position, _target.position);

            State newState = _currentState;

            switch (_currentState)
            {
                case State.Dormant:
                    if (noise > curiousThreshold)
                        newState = State.Curious;
                    break;

                case State.Curious:
                    if (noise > trackingThreshold)
                        newState = State.Tracking;
                    else if (noise < curiousThreshold * 0.5f)
                        newState = State.Dormant;
                    break;

                case State.Tracking:
                    if (noise > huntingThreshold)
                        newState = State.Hunting;
                    else if (noise < trackingThreshold * 0.5f)
                    {
                        newState = State.Searching;
                        _searchTimer = searchDuration;
                    }
                    break;

                case State.Hunting:
                    if (distToTarget < attackRange)
                        newState = State.Attacking;
                    else if (noise < huntingThreshold * 0.5f)
                    {
                        newState = State.Searching;
                        _searchTimer = searchDuration;
                    }
                    break;

                case State.Attacking:
                    _attackTimer -= Time.deltaTime;
                    if (_attackTimer <= 0f)
                    {
                        if (distToTarget < attackRange)
                        {
                            // Attack landed
                            AttackTarget();
                        }
                        newState = State.Hunting;
                    }
                    break;

                case State.Searching:
                    _searchTimer -= Time.deltaTime;
                    if (_searchTimer <= 0f)
                    {
                        newState = State.Dormant;
                        OnLostTarget?.Invoke();
                    }
                    else if (noise > trackingThreshold)
                    {
                        newState = State.Tracking;
                    }
                    break;
            }

            if (newState != _currentState)
            {
                _currentState = newState;
                OnStateChanged?.Invoke(newState);

                if (newState == State.Attacking)
                {
                    _attackTimer = attackCooldown;
                    OnAttack?.Invoke();
                }
            }

            // Update last known position when we can hear
            if (noise > trackingThreshold)
            {
                _lastKnownPosition = _target.position;
                _timeSinceLastHeard = 0f;
            }
            else
            {
                _timeSinceLastHeard += Time.deltaTime;
            }
        }

        void UpdateMovement()
        {
            Vector3 targetPos = GetTargetPosition();
            float targetSpeed = GetTargetSpeed();

            // Smoothly adjust speed
            _currentSpeed = Mathf.Lerp(_currentSpeed, targetSpeed, acceleration * Time.deltaTime);

            // Direction to target
            Vector3 toTarget = (targetPos - transform.position).normalized;
            if (toTarget.sqrMagnitude < 0.01f) toTarget = transform.forward;

            // Smooth turn
            Quaternion targetRot = Quaternion.LookRotation(toTarget);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);

            // Move
            _currentVelocity = transform.forward * _currentSpeed;
            transform.position += _currentVelocity * Time.deltaTime;
        }

        Vector3 GetTargetPosition()
        {
            switch (_currentState)
            {
                case State.Dormant:
                case State.Curious:
                    // Slowly circle at distance
                    return _lastKnownPosition + (transform.position - _lastKnownPosition).normalized * hearingRange * 0.5f;

                case State.Tracking:
                case State.Hunting:
                    return _lastKnownPosition;

                case State.Searching:
                    // Search around last known position
                    float angle = Time.time * 2f;
                    Vector3 searchOffset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 30f;
                    return _lastKnownPosition + searchOffset;

                case State.Attacking:
                    return _target.position;

                default:
                    return transform.position;
            }
        }

        float GetTargetSpeed()
        {
            switch (_currentState)
            {
                case State.Dormant: return 0f;
                case State.Curious: return swimSpeed * 0.3f;
                case State.Tracking: return swimSpeed * 0.6f;
                case State.Hunting: return huntSpeed;
                case State.Attacking: return huntSpeed * 1.2f;
                case State.Searching: return swimSpeed * 0.5f;
                default: return 0f;
            }
        }

        void AttackTarget()
        {
            if (_target == null) return;

            // Apply damage to submarine
            var hull = _target.GetComponent<HullSystem>();
            if (hull != null)
            {
                hull.TakeDamage(15f);
            }

            // Knockback
            var controller = _target.GetComponent<SubmarineController>();
            if (controller != null)
            {
                // Add noise spike
            }

            // Reset to hunting after attack
            _currentState = State.Hunting;
        }

        void UpdateVisuals()
        {
            if (_glowRenderers == null || _glowRenderers.Count == 0) return;

            // Glow intensity based on state
            float targetGlow = 0.2f;
            switch (_currentState)
            {
                case State.Dormant: targetGlow = 0.2f; break;
                case State.Curious: targetGlow = 0.45f; break;
                case State.Tracking: targetGlow = 0.75f; break;
                case State.Hunting: targetGlow = 1.1f; break;
                case State.Attacking: targetGlow = 1.4f; break;
                case State.Searching: targetGlow = 0.55f; break;
            }

            float target = targetGlow * glowIntensity;
            _glowLevel = Mathf.Lerp(_glowLevel, target, Time.deltaTime * 3.5f);

            // Shift from cool cyan to predatory crimson-amber when hunting/attacking
            bool aggressive = _currentState == State.Hunting || _currentState == State.Attacking;
            Color threatTint = aggressive ? new Color(1f, 0.22f, 0.18f) : glowColor;
            Color curEmissive = Color.Lerp(glowColor, threatTint, aggressive ? 0.75f : 0f) * (_glowLevel * 2.8f);

            if (_propBlock == null) _propBlock = new MaterialPropertyBlock();
            _propBlock.SetColor("_EmissionColor", curEmissive);

            for (int i = 0; i < _glowRenderers.Count; i++)
            {
                if (_glowRenderers[i] != null)
                    _glowRenderers[i].SetPropertyBlock(_propBlock);
            }
        }

        void OnDrawGizmosSelected()
        {
            // Hearing range
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
            Gizmos.DrawWireSphere(transform.position, hearingRange);

            // Attack range
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, attackRange);

            // Last known position
            if (_lastKnownPosition != Vector3.zero)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(_lastKnownPosition, 2f);
            }
        }
    }
}
