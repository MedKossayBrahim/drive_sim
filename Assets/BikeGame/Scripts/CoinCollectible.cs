using KikiNgao.SimpleBikeControl;
using UnityEngine;

namespace BikeGame
{
    /// <summary>
    /// A spinning collectible coin. The coin root stays still and only the visual
    /// child rotates, so the trigger collider is a static trigger and does not
    /// force physics broadphase updates every frame.
    ///
    /// The bike prefab has its Rigidbody on the root and its colliders on child
    /// objects, so the coin only needs a trigger collider. Unity then reports the
    /// bike's colliders through OnTriggerEnter.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SphereCollider))]
    public class CoinCollectible : MonoBehaviour
    {
        [Header("Visual")]
        [SerializeField] private Transform visual;
        [SerializeField] private float rotationSpeed = 150f;
        [SerializeField] private float bobHeight = 0.06f;
        [SerializeField] private float bobSpeed = 2.2f;

        [Header("Collection")]
        [SerializeField] private Material burstMaterial;

        private Vector3 visualStartLocalPosition;
        private bool collected;

        private void Awake()
        {
            if (visual == null)
            {
                Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
                if (renderers.Length > 0)
                    visual = renderers[0].transform;
            }

            if (visual != null)
                visualStartLocalPosition = visual.localPosition;

            SphereCollider trigger = GetComponent<SphereCollider>();
            if (trigger != null)
                trigger.isTrigger = true;
        }

        private void Update()
        {
            if (collected || visual == null) return;

            // World-space Y keeps the upright coin spinning like a collectible
            // even if the visual has a small local offset.
            visual.Rotate(0f, rotationSpeed * Time.deltaTime, 0f, Space.World);

            if (bobHeight > 0f && bobSpeed > 0f)
            {
                Vector3 position = visualStartLocalPosition;
                position.y += Mathf.Sin(Time.time * bobSpeed) * bobHeight;
                visual.localPosition = position;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (collected || !IsBicycle(other)) return;

            collected = true;

            SphereCollider trigger = GetComponent<SphereCollider>();
            if (trigger != null)
                trigger.enabled = false;

            if (CoinCounter.Instance != null)
                CoinCounter.Instance.AddCoin();

            CoinCollectEffect.Play(transform.position, burstMaterial);
            Destroy(gameObject);
        }

        private static bool IsBicycle(Collider other)
        {
            if (other == null || other is TerrainCollider) return false;

            // The bike root owns the Rigidbody; the root box, wheel colliders and
            // the "Bike Dectector" trigger are all children of that Rigidbody.
            Rigidbody body = other.attachedRigidbody;
            if (body != null)
            {
                if (body.GetComponent<SimpleBike>() != null) return true;
                if (body.GetComponentInParent<SimpleBike>() != null) return true;
            }

            return other.GetComponentInParent<SimpleBike>() != null;
        }
    }
}
