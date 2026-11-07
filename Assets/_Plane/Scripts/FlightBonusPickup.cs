using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Plane
{
    /// <summary>Bonus physique à usage unique, séparé des pièces et de leur quota.</summary>
    [RequireComponent(typeof(SphereCollider))]
    internal sealed class FlightBonusPickup : MonoBehaviour
    {
        private FlightPowerUps owner;
        private FlightBonusKind kind;
        private Transform visual;
        private float collectedAt = float.NegativeInfinity;
        private Transform collector;
        private Vector3 collectedPosition;
        private Quaternion collectedRotation;
        private const float CollectAnimationSeconds = .32f;

        internal static Color Accent(FlightBonusKind kind) => kind == FlightBonusKind.Magnet
            ? new Color(.06f, .84f, 1f) : new Color(.86f, .36f, 1f);

        internal static void Create(Transform parent, Vector3 position, Vector3 direction, FlightBonusKind kind,
            FlightPowerUps owner, Material material)
        {
            bool magnet = kind == FlightBonusKind.Magnet;
            var item = new GameObject(magnet ? "Bonus_Aimant" : "Bonus_PointsX2", typeof(SphereCollider));
            item.transform.SetParent(parent, false);
            item.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
            var trigger = item.GetComponent<SphereCollider>();
            trigger.radius = 14f;
            Collectible.RestrictToPlane(trigger);
            var pickup = item.AddComponent<FlightBonusPickup>();
            pickup.owner = owner;
            pickup.kind = kind;
            pickup.visual = new GameObject("Symbole").transform;
            pickup.visual.SetParent(item.transform, false);

            Color accent = Accent(kind);
            Block(pickup.visual, material, Vector3.zero, new Vector3(15f, 13f, 1.2f), accent * .34f);
            if (magnet)
            {
                Block(pickup.visual, material, new Vector3(-3.5f, 0f, -1f), new Vector3(2f, 8f, 1f), accent);
                Block(pickup.visual, material, new Vector3(3.5f, 0f, -1f), new Vector3(2f, 8f, 1f), accent);
                Block(pickup.visual, material, new Vector3(0f, -3.5f, -1f), new Vector3(8f, 2f, 1f), accent);
                Block(pickup.visual, material, new Vector3(-3.5f, 3.6f, -1.2f), new Vector3(2.2f, 2f, 1f), Color.white);
                Block(pickup.visual, material, new Vector3(3.5f, 3.6f, -1.2f), new Vector3(2.2f, 2f, 1f), Color.white);
            }
            else
            {
                Block(pickup.visual, material, new Vector3(-3.5f, 0f, -1f), new Vector3(7f, 1.5f, 1f), Color.white, 45f);
                Block(pickup.visual, material, new Vector3(-3.5f, 0f, -1f), new Vector3(7f, 1.5f, 1f), Color.white, -45f);
                for (int i = -1; i <= 1; i++)
                    Block(pickup.visual, material, new Vector3(2.8f, 3.5f * i, -1f), new Vector3(4.5f, 1.35f, 1f), accent);
                Block(pickup.visual, material, new Vector3(4.4f, 1.75f, -1f), new Vector3(1.35f, 3.5f, 1f), accent);
                Block(pickup.visual, material, new Vector3(1.2f, -1.75f, -1f), new Vector3(1.35f, 3.5f, 1f), accent);
            }
        }

        private static void Block(Transform parent, Material material, Vector3 position, Vector3 scale, Color color, float angle = 0f)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var collider = part.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            var renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var colors = new MaterialPropertyBlock();
            colors.SetColor("_BaseColor", color);
            colors.SetColor("_EmissionColor", color * .65f);
            renderer.SetPropertyBlock(colors);
        }

        private void Update()
        {
            if (collectedAt >= 0f)
            {
                float progress = Mathf.Clamp01((Time.time - collectedAt) / CollectAnimationSeconds);
                if (kind == FlightBonusKind.Magnet && collector != null)
                {
                    // Rétrécit avant d'atteindre l'avion pour garder la vue dégagée.
                    visual.position = Vector3.Lerp(collectedPosition, collector.position, progress * progress);
                    visual.rotation = collectedRotation * Quaternion.Euler(0f, progress * 105f, 0f);
                    visual.localScale = Vector3.one * (1f - progress) * (1f - progress);
                }
                else
                {
                    visual.position = collectedPosition + Vector3.up * (progress * 6f);
                    visual.rotation = collectedRotation * Quaternion.Euler(0f, progress * 72f, progress * 38f);
                    float snap = 1f + .25f * Mathf.Sin(progress * Mathf.PI) - progress * progress;
                    visual.localScale = Vector3.one * snap;
                }
                if (progress >= 1f) gameObject.SetActive(false);
                return;
            }
            visual.localPosition = Vector3.up * (Mathf.Sin(Time.time * 2.5f) * .8f);
            visual.localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * 1.8f) * 24f, 0f);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (collectedAt >= 0f || !owner.TryActivate(kind, other)) return;
            collectedAt = Time.time;
            GetComponent<SphereCollider>().enabled = false;
            collector = other.attachedRigidbody.transform;
            collectedPosition = visual.position;
            collectedRotation = visual.rotation;
            FlightBonusBurst.Play(transform.parent, collectedPosition, transform.rotation, kind, Accent(kind), collector);
        }
    }
}
