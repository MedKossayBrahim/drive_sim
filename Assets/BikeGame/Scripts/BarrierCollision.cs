using KikiNgao.SimpleBikeControl;
using UnityEngine;

namespace BikeGame
{
    /// <summary>
    /// Placed on each road-barrier obstacle. When the bicycle (or the rider on
    /// it) collides with this object, exactly one heart is removed via
    /// <see cref="HeartSystem.TakeDamage"/>. The cooldown inside TakeDamage
    /// prevents sustained contact or a multi-collider crash from draining more
    /// than one heart at a time.
    ///
    /// Ordinary terrain contact never reaches this callback because the
    /// TerrainCollider is a separate object. Successful ramp jumps are
    /// unaffected because the ramp has its own MeshCollider and the heart
    /// penalty for bad landings is handled by HeartSystem's jump state machine,
    /// not by this script.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class BarrierCollision : MonoBehaviour
    {
        private void OnCollisionEnter(Collision collision)
        {
            if (HeartSystem.Instance == null) return;
            if (HeartSystem.Instance.IsGameOver) return;

            Collider other = collision.collider;
            if (other == null || other is TerrainCollider) return;

            // Accept the bike root, any wheel-collider parent, or the rider
            // while mounted. This mirrors CoinCollectible.IsBicycle so the same
            // colliders that collect coins also trigger barrier penalties.
            Rigidbody body = other.attachedRigidbody;
            if (body != null)
            {
                if (body.GetComponent<SimpleBike>() != null ||
                    body.GetComponentInParent<SimpleBike>() != null)
                {
                    HeartSystem.Instance.TakeDamage();
                    return;
                }
            }

            if (other.GetComponentInParent<SimpleBike>() != null)
                HeartSystem.Instance.TakeDamage();
        }
    }
}
