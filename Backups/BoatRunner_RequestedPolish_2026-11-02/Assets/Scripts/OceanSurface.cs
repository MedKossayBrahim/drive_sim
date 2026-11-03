using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public sealed class OceanSurface : MonoBehaviour
{
    [SerializeField] Transform target;
    [SerializeField] float tileWidth = 260f;
    [SerializeField] float tileLength = 380f;
    public void SetTarget(Transform value) => target = value;
    void LateUpdate()
    {
        if (target == null) return;
        Vector3 position = transform.position;
        position.x = Mathf.Round(target.position.x / (tileWidth * 0.25f)) * (tileWidth * 0.25f);
        position.z = target.position.z;
        transform.position = position;
    }
}
