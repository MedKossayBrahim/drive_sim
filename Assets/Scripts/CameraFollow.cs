using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform target;
    public Vector3 offset = new Vector3(0, 5, -10);

    [Header("Follow Settings")]
    public float followSpeed = 10f;
    public float rotationSpeed = 5f;
    public bool lookAtTarget = true;

    [Header("Collision Settings")]
    public bool avoidObstacles = true;
    public float collisionRadius = 0.3f;
    public LayerMask obstacleLayers;

    private Vector3 currentVelocity;
    private float currentDistance;

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 desiredPosition = target.position + target.TransformDirection(offset);
        
        if (avoidObstacles && obstacleLayers != 0)
        {
            desiredPosition = CheckCollisions(target.position, desiredPosition);
        }

        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref currentVelocity, 1f / followSpeed);

        if (lookAtTarget)
        {
            Quaternion targetRotation = Quaternion.LookRotation(target.position - transform.position);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }
    }

    Vector3 CheckCollisions(Vector3 from, Vector3 to)
    {
        Vector3 direction = (to - from).normalized;
        float distance = Vector3.Distance(from, to);
        
        RaycastHit hit;
        if (Physics.SphereCast(from, collisionRadius, direction, out hit, distance, obstacleLayers))
        {
            return hit.point + hit.normal * collisionRadius;
        }
        
        return to;
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    void OnDrawGizmosSelected()
    {
        if (target != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(target.position + target.TransformDirection(offset), 0.5f);
            Gizmos.DrawLine(target.position, target.position + target.TransformDirection(offset));
        }
    }
}