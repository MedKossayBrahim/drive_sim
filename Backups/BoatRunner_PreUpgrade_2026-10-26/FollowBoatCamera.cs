using UnityEngine;

public class FollowBoatCamera : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] Vector3 offset = new Vector3(0f, 18f, -1f);
    [SerializeField] float followSharpness = 8f;
    public void SetTarget(Transform value) => target = value;
    void LateUpdate()
    {
        if (target == null) return;
        Vector3 goal = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, goal, 1f - Mathf.Exp(-followSharpness * Time.deltaTime));
        transform.rotation = Quaternion.Euler(90f, 0f, 0f);
    }
}

