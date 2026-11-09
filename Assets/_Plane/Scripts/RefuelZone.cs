using System.Collections.Generic;
using UnityEngine;

namespace Project.Plane
{
    /// <summary>Recharge une seule fois par pas physique, même si l'avion possède plusieurs colliders.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
    public sealed class RefuelZone : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float litersPerSecond = 10f;
        [SerializeField, Min(0f)] private float maximumSpeed = 2f;
        [SerializeField, Min(0f)] private float maximumVerticalSpeed = 0.5f;
        [SerializeField, Min(0f), Tooltip("Écart maximal entre le bas des colliders et le sol, en mètres.")]
        private float maximumGroundGap = 0.3f;
        [SerializeField] private LayerMask groundMask = 1 << 8;
        [SerializeField, Min(1f)] private float groundProbeDistance = 10f;
        private readonly HashSet<PlaneSensors> refuelled = new HashSet<PlaneSensors>();
        private readonly List<Collider> colliders = new List<Collider>();
        private void Reset() => GetComponent<BoxCollider>().isTrigger = true;
        private void Awake() => GetComponent<BoxCollider>().isTrigger = true;
        private void FixedUpdate() => refuelled.Clear();
        private void OnTriggerStay(Collider other)
        {
            var body = other.attachedRigidbody;
            if (body == null || body.gameObject.layer != LayerMask.NameToLayer("Plane")) return;
            var sensors = body.GetComponent<PlaneSensors>();
            if (sensors == null || refuelled.Contains(sensors) || body.linearVelocity.magnitude > maximumSpeed ||
                Mathf.Abs(body.linearVelocity.y) > maximumVerticalSpeed) return;
            var health = body.GetComponent<PlaneHealth>();
            if (health != null && health.IsCrashed) return;
            if (!Physics.Raycast(body.worldCenterOfMass,Vector3.down,out var hit,groundProbeDistance,groundMask,QueryTriggerInteraction.Ignore) ||
                Vector3.Dot(hit.normal,Vector3.up) < 0.65f) return;
            float bottom = float.PositiveInfinity;
            colliders.Clear(); body.GetComponentsInChildren(false,colliders);
            foreach (var collider in colliders)
                if (collider.enabled && !collider.isTrigger && collider.attachedRigidbody == body)
                    bottom = Mathf.Min(bottom,collider.bounds.min.y);
            // Un avion qui survole lentement la zone ne doit pas pouvoir ravitailler.
            float gap = bottom - hit.point.y;
            if (!float.IsFinite(bottom) || gap > maximumGroundGap || gap < -0.5f) return;
            refuelled.Add(sensors); sensors.Refuel(litersPerSecond * Time.fixedDeltaTime);
        }
        private void OnDisable() => refuelled.Clear();
        private void OnDrawGizmosSelected()
        {
            var box = GetComponent<BoxCollider>(); if(box==null)return;
            Gizmos.matrix=transform.localToWorldMatrix; Gizmos.color=new Color(0.1f,0.9f,1f,0.25f);
            Gizmos.DrawCube(box.center,box.size);Gizmos.color=Color.cyan;Gizmos.DrawWireCube(box.center,box.size);
        }
    }
}
