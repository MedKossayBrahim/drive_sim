using KikiNgao.SimpleBikeControl;
using UnityEngine;

namespace BikeGame
{
    /// <summary>
    /// Third-person camera glued behind the bike, matching the Forest ride framing:
    /// rider in the lower centre, path / ramp ahead.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    [DefaultExecutionOrder(10000)]
    public class BikeChaseCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 pivotOffset = new Vector3(0f, 1.2f, 0f);
        [SerializeField] private float distance = 4.8f;
        [SerializeField] private float height = 1.85f;
        [SerializeField] private float lookAhead = 5f;
        [SerializeField] private float fieldOfView = 65f;
        [SerializeField] private float positionSmoothTime = 0.06f;
        [SerializeField] private float rotationSharpness = 16f;

        private Vector3 positionVelocity;
        private Camera _camera;
        private Rigidbody _targetBody;

        public Transform Target => target;

        void Awake()
        {
            _camera = GetComponent<Camera>();
            ApplyLens();
        }

        public void SetTarget(Transform newTarget, bool snap)
        {
            target = newTarget;
            _targetBody = null;
            if (target != null)
            {
                _targetBody = target.GetComponent<Rigidbody>();
                if (_targetBody == null)
                    _targetBody = target.GetComponentInParent<Rigidbody>();
            }

            positionVelocity = Vector3.zero;
            ApplyLens();
            enabled = true;

            if (snap && target != null)
                SnapToTarget();
        }

        void Start()
        {
            TryAutoAcquireTarget();
            if (target != null)
                SnapToTarget();
        }

        void LateUpdate()
        {
            if (target == null)
                TryAutoAcquireTarget();
            if (target == null)
                return;

            FollowTarget(Time.deltaTime, false);
        }

        public void SnapToTarget()
        {
            if (target == null)
                return;
            FollowTarget(0f, true);
        }

        void FollowTarget(float deltaTime, bool instant)
        {
            Vector3 pivot = GetPivot(out Vector3 flatForward);
            Vector3 desiredPosition = pivot - flatForward * distance + Vector3.up * height;
            Vector3 lookPoint = pivot + flatForward * lookAhead;
            Quaternion desiredRotation = Quaternion.LookRotation(lookPoint - desiredPosition, Vector3.up);

            if (instant || deltaTime <= 0f)
            {
                transform.SetPositionAndRotation(desiredPosition, desiredRotation);
                positionVelocity = Vector3.zero;
                return;
            }

            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref positionVelocity,
                Mathf.Max(0.001f, positionSmoothTime),
                Mathf.Infinity,
                deltaTime);

            float t = 1f - Mathf.Exp(-rotationSharpness * deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, t);
        }

        void TryAutoAcquireTarget()
        {
            SimpleBike bike = FindAnyObjectByType<SimpleBike>();
            if (bike == null)
                return;
            SetTarget(bike.transform, true);
        }

        void ApplyLens()
        {
            if (_camera == null)
                _camera = GetComponent<Camera>();
            if (_camera == null)
                return;

            _camera.enabled = true;
            _camera.fieldOfView = fieldOfView;
            if (!CompareTag("MainCamera"))
                tag = "MainCamera";
        }

        Vector3 GetPivot(out Vector3 flatForward)
        {
            flatForward = Vector3.ProjectOnPlane(target.forward, Vector3.up);

            if (_targetBody != null)
            {
                Vector3 velocity = Vector3.ProjectOnPlane(_targetBody.linearVelocity, Vector3.up);
                if (velocity.sqrMagnitude > 0.35f)
                    flatForward = velocity.normalized;
            }

            if (flatForward.sqrMagnitude < 0.0001f)
                flatForward = Vector3.forward;
            else
                flatForward.Normalize();

            return target.position
                   + Vector3.up * pivotOffset.y
                   + target.right * pivotOffset.x
                   + flatForward * pivotOffset.z;
        }
    }
}
