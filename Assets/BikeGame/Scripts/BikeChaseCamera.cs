using UnityEngine;

namespace BikeGame
{
    /// <summary>
    /// Smooth third-person camera for the bike. The bike/player layers are
    /// excluded from collision so the camera never collides with the rider.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class BikeChaseCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 pivotOffset = new Vector3(0f, 1.15f, 0f);
        [SerializeField] private float distance = 5.5f;
        [SerializeField] private float height = 2.25f;
        [SerializeField] private float lookAhead = 2.5f;
        [SerializeField] private float positionSmoothTime = 0.12f;
        [SerializeField] private float rotationSharpness = 10f;
        [SerializeField] private float collisionRadius = 0.25f;
        [SerializeField] private LayerMask collisionMask = ~((1 << 8) | (1 << 9));

        private Vector3 positionVelocity;

        public Transform Target => target;

        public void SetTarget(Transform newTarget, bool snap)
        {
            target = newTarget;
            positionVelocity = Vector3.zero;

            if (snap && target != null)
                SnapToTarget();
        }

        private void Start()
        {
            if (target != null)
                SnapToTarget();
        }

        private void LateUpdate()
        {
            if (target == null) return;

            Vector3 pivot = GetPivot(out Vector3 flatForward);
            Vector3 desiredPosition = GetDesiredPosition(pivot, flatForward);

            transform.position = Vector3.SmoothDamp(
                transform.position,
                desiredPosition,
                ref positionVelocity,
                Mathf.Max(0.001f, positionSmoothTime),
                Mathf.Infinity,
                Time.deltaTime);

            Vector3 lookPoint = pivot + flatForward * lookAhead;
            Quaternion desiredRotation = Quaternion.LookRotation(lookPoint - transform.position, Vector3.up);
            float t = 1f - Mathf.Exp(-rotationSharpness * Time.deltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, t);
        }

        private void SnapToTarget()
        {
            Vector3 pivot = GetPivot(out Vector3 flatForward);
            transform.position = GetDesiredPosition(pivot, flatForward);
            transform.rotation = Quaternion.LookRotation(pivot + flatForward * lookAhead - transform.position, Vector3.up);
            positionVelocity = Vector3.zero;
        }

        private Vector3 GetPivot(out Vector3 flatForward)
        {
            flatForward = Vector3.ProjectOnPlane(target.forward, Vector3.up);
            if (flatForward.sqrMagnitude < 0.0001f)
                flatForward = target.right;

            flatForward.Normalize();
            return target.position + Vector3.up * pivotOffset.y +
                   target.right * pivotOffset.x + target.forward * pivotOffset.z;
        }

        private Vector3 GetDesiredPosition(Vector3 pivot, Vector3 flatForward)
        {
            Vector3 desired = pivot - flatForward * distance + Vector3.up * height;
            Vector3 direction = desired - pivot;
            float length = direction.magnitude;

            if (length > 0.001f &&
                Physics.SphereCast(pivot, collisionRadius, direction.normalized, out RaycastHit hit,
                    length, collisionMask, QueryTriggerInteraction.Ignore))
            {
                desired = pivot + direction.normalized * Mathf.Max(0.35f, hit.distance - 0.08f);
            }

            return desired;
        }
    }
}
