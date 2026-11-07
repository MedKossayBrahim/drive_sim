using UnityEngine;

namespace BikeGame
{
    /// <summary>
    /// The bike prefab keeps its colliders on child objects (impact boxes on the
    /// wheels), so a tiny forwarder is added to each of them. All reports go to
    /// one BikeImpactFeedback handler on the rig root.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BikeCollisionProxy : MonoBehaviour
    {
        [HideInInspector] public BikeImpactFeedback feedback;

        private void OnCollisionEnter(Collision collision)
        {
            if (feedback != null)
                feedback.ReportCollision(collision, this);
        }
    }
}
