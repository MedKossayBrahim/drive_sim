using UnityEngine;
using System.Collections;

namespace DriveSim.Submarine
{
    /// <summary>
    /// Cinematic third-person camera with smooth follow, underwater sway,
    /// depth-based effects, and contextual shake.
    /// </summary>
    public class SubmarineCamera : MonoBehaviour
    {
        [Header("Follow")]
        public Transform target;
        public Vector3 offset = new Vector3(0f, 5f, -15f);
        public float followSpeed = 3f;
        public float rotationSpeed = 5f;

        [Header("Lag")]
        public float positionLag = 0.15f;
        public float rotationLag = 0.1f;

        [Header("Shake")]
        public float shakeDecay = 5f;
        public float maxShake = 1f;

        [Header("Depth Effects")]
        public float fovBase = 60f;
        public float fovSpeed = 70f;
        public float fovSmooth = 5f;

        [Header("Underwater Sway")]
        public float swayAmplitude = 0.12f;
        public float swayFrequency = 0.4f;
        public float rollSway = 0.3f;

        [Header("Depth Pressure")]
        public float depthFovPush = 2f;        // FOV widens slightly at depth
        public float depthRollMax = 0.6f;       // subtle roll instability at depth

        [Header("Collision Avoidance")]
        public float minDistance = 3f;
        public LayerMask collisionLayers;

        private Vector3 _currentPosition;
        private Quaternion _currentRotation;
        private Vector3 _shakeOffset;
        private float _shakeIntensity;
        private Camera _cam;
        private SubmarineController _controller;
        private float _swayPhaseX;
        private float _swayPhaseY;

        void Awake()
        {
            _cam = GetComponent<Camera>();
            if (_cam == null) _cam = Camera.main;
            _swayPhaseX = Random.value * Mathf.PI * 2f;
            _swayPhaseY = Random.value * Mathf.PI * 2f;
        }

        void Start()
        {
            if (target == null)
            {
                var sub = FindFirstObjectByType<SubmarineController>();
                if (sub != null) target = sub.transform;
            }

            if (target != null)
            {
                _controller = target.GetComponent<SubmarineController>();
                _currentPosition = target.position + target.rotation * offset;
                _currentRotation = target.rotation;
            }
        }

        void LateUpdate()
        {
            if (target == null) return;

            UpdatePosition();
            UpdateRotation();
            UpdateShake();
            UpdateSway();
            UpdateFOV();
            ApplyCollisionAvoidance();
        }

        void UpdatePosition()
        {
            Vector3 desiredPosition = target.position + target.rotation * offset;

            // Smooth follow with lag
            _currentPosition = Vector3.Lerp(_currentPosition, desiredPosition,
                (1f - positionLag) * followSpeed * Time.deltaTime);

            transform.position = _currentPosition + _shakeOffset;
        }

        void UpdateRotation()
        {
            Quaternion desiredRotation = Quaternion.LookRotation(
                target.position - transform.position + Vector3.up * 2f,
                Vector3.up);

            _currentRotation = Quaternion.Slerp(_currentRotation, desiredRotation,
                (1f - rotationLag) * rotationSpeed * Time.deltaTime);

            transform.rotation = _currentRotation;
        }

        void UpdateShake()
        {
            if (_shakeIntensity > 0f)
            {
                _shakeOffset = Random.insideUnitSphere * _shakeIntensity;
                _shakeIntensity = Mathf.Lerp(_shakeIntensity, 0f, shakeDecay * Time.deltaTime);
            }
            else
            {
                _shakeOffset = Vector3.zero;
            }
        }

        void UpdateSway()
        {
            // Underwater sway — gentle rolling motion like floating in current
            float t = Time.time;
            float swayX = Mathf.Sin(t * swayFrequency * Mathf.PI * 2f + _swayPhaseX) * swayAmplitude;
            float swayY = Mathf.Sin(t * swayFrequency * 0.7f * Mathf.PI * 2f + _swayPhaseY) * swayAmplitude * 0.6f;

            // Apply as position offset
            transform.position += transform.right * swayX + transform.up * swayY;

            // Apply subtle roll sway
            float roll = Mathf.Sin(t * swayFrequency * 0.5f * Mathf.PI * 2f) * rollSway;

            // Depth-based instability
            if (_controller != null)
            {
                float depthFactor = Mathf.Clamp01(_controller.Depth / 420f);
                roll += Mathf.Sin(t * 1.3f) * depthRollMax * depthFactor;
            }

            transform.rotation *= Quaternion.Euler(0f, 0f, roll);
        }

        void UpdateFOV()
        {
            if (_cam == null || _controller == null) return;

            float targetFOV = _controller.Speed > 1f ? fovSpeed : fovBase;

            // Depth-based FOV widening (pressure effect)
            float depthFactor = Mathf.Clamp01(_controller.Depth / 420f);
            targetFOV += depthFovPush * depthFactor;

            _cam.fieldOfView = Mathf.Lerp(_cam.fieldOfView, targetFOV, fovSmooth * Time.deltaTime);
        }

        void ApplyCollisionAvoidance()
        {
            if (collisionLayers == 0) return;

            Vector3 toTarget = target.position - transform.position;
            float dist = toTarget.magnitude;

            RaycastHit hit;
            if (Physics.Raycast(transform.position, toTarget.normalized, out hit, dist, collisionLayers))
            {
                float pushDistance = dist - hit.distance + minDistance;
                transform.position -= toTarget.normalized * pushDistance;
            }
        }

        public void Shake(float intensity)
        {
            _shakeIntensity = Mathf.Min(maxShake, _shakeIntensity + intensity);
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            if (target != null)
            {
                _controller = target.GetComponent<SubmarineController>();
                _currentPosition = target.position + target.rotation * offset;
            }
        }
    }
}
