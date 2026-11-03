using UnityEngine;

public sealed class FollowBoatCamera : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] Vector3 offset = new Vector3(0f, 10.5f, -13f);
    [SerializeField] Vector3 lookAhead = new Vector3(0f, 0.45f, 9f);
    [SerializeField] float followSharpness = 5f;
    [SerializeField] float aimSharpness = 7f;
    public void SetTarget(Transform value) => target = value;
    public void Configure(Transform value, Vector3 followOffset, Vector3 ahead)
    {
        target = value; offset = followOffset; lookAhead = ahead;
    }
    void LateUpdate()
    {
        if (target == null) return;
        Vector3 goal = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, goal, 1f - Mathf.Exp(-followSharpness * Time.deltaTime));
        Quaternion aim = Quaternion.LookRotation(target.position + lookAhead - transform.position, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, aim, 1f - Mathf.Exp(-aimSharpness * Time.deltaTime));
    }
}
