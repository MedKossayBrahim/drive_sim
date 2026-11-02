using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Project.Plane
{
    public enum CheckpointState { Inactive, Active, Passed }

    /// <summary>Valide le franchissement du plan central par un Rigidbody allant vers Z local positif.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(SphereCollider))]
    public sealed class Checkpoint : MonoBehaviour
    {
        [Serializable]
        public sealed class RendererMaterials
        {
            [SerializeField] private Renderer target = null;
            [SerializeField] private Material[] inactive = Array.Empty<Material>();
            [SerializeField] private Material[] active = Array.Empty<Material>();
            [SerializeField] private Material[] passed = Array.Empty<Material>();
            public Renderer Target => target;
            public Material[] ForState(CheckpointState state) => state == CheckpointState.Active ? active :
                state == CheckpointState.Passed ? passed : inactive;
            public RendererMaterials(Renderer renderer, Material[] off, Material[] on, Material[] done)
            { target = renderer; inactive = off; active = on; passed = done; }
        }

        [SerializeField] private CheckpointState state = CheckpointState.Inactive;
        [SerializeField] private RendererMaterials[] rendererMaterials = Array.Empty<RendererMaterials>();
        [SerializeField, Tooltip("Utilisé si le manager ne désigne pas un Rigidbody précis.")]
        private LayerMask acceptedLayers = ~0;
        [SerializeField, Min(0f)] private float pulseFrequency = 1.2f;
        [SerializeField, Range(0f, 0.8f)] private float pulseAmplitude = 0.2f;
        [SerializeField] private UnityEvent onPassed = new UnityEvent();

        private sealed class Contact
        {
            public Vector3 PreviousPosition;
            public readonly HashSet<Collider> Colliders = new HashSet<Collider>();
        }
        private readonly Dictionary<Rigidbody, Contact> contacts = new Dictionary<Rigidbody, Contact>();
        private SphereCollider sphere;
        private Rigidbody requiredBody;
        private MaterialPropertyBlock properties;
        public event Action<Checkpoint> OnPassed;
        public CheckpointState State => state;

        private void Awake() => sphere = GetComponent<SphereCollider>();
        private void OnEnable()
        {
            if (sphere == null) sphere = GetComponent<SphereCollider>();
            ApplyMaterials();
        }

        public void SetState(CheckpointState value)
        {
            state = value;
            contacts.Clear();
            ApplyMaterials();
        }
        public void SetRequiredBody(Rigidbody body) { requiredBody = body; contacts.Clear(); }

        private void ApplyMaterials()
        {
            foreach (var binding in rendererMaterials)
            {
                if (binding == null || binding.Target == null) continue;
                Material[] materials = binding.ForState(state);
                binding.Target.sharedMaterials = materials;
                for (int i = 0; i < materials.Length; i++) binding.Target.SetPropertyBlock(null, i);
            }
        }

        private void Update()
        {
            if (state != CheckpointState.Active) return;
            if (properties == null) properties = new MaterialPropertyBlock();
            float pulse = 1f + pulseAmplitude * Mathf.Sin(Time.time * pulseFrequency * 2f * Mathf.PI);
            foreach (var binding in rendererMaterials)
            {
                if (binding == null || binding.Target == null) continue;
                var materials = binding.ForState(CheckpointState.Active);
                for (int i = 0; i < materials.Length; i++)
                {
                    var material = materials[i];
                    if (material == null) continue;
                    // glTFast et URP/Lit ont des noms de propriété différents.
                    string emission = material.HasProperty("emissiveFactor") ? "emissiveFactor" : "_EmissionColor";
                    if (!material.HasProperty(emission)) continue;
                    properties.Clear();
                    binding.Target.GetPropertyBlock(properties, i);
                    properties.SetColor(emission, material.GetColor(emission) * pulse);
                    binding.Target.SetPropertyBlock(properties, i);
                }
            }
        }

        private bool Accept(Collider other, out Rigidbody body)
        {
            body = other.attachedRigidbody;
            if (state != CheckpointState.Active || body == null) return false;
            if (requiredBody != null) return body == requiredBody;
            return (acceptedLayers.value & (1 << body.gameObject.layer)) != 0;
        }

        private Contact Register(Collider other, Rigidbody body)
        {
            if (!contacts.TryGetValue(body, out var contact))
            {
                contact = new Contact { PreviousPosition = body.worldCenterOfMass };
                contacts.Add(body, contact);
            }
            contact.Colliders.Add(other);
            return contact;
        }

        // Les callbacks de trigger appartiennent à la simulation physique, jamais à Update.
        private void OnTriggerEnter(Collider other)
        {
            if (Accept(other, out var body)) Register(other, body);
        }
        private void OnTriggerStay(Collider other)
        {
            if (!Accept(other, out var body)) return;
            var contact = Register(other, body);
            CheckCrossing(body, contact);
        }
        private void OnTriggerExit(Collider other)
        {
            var body = other.attachedRigidbody;
            if (body == null || !contacts.TryGetValue(body, out var contact)) return;
            if (Accept(other, out _)) CheckCrossing(body, contact);
            contact.Colliders.Remove(other);
            if (contact.Colliders.Count == 0) contacts.Remove(body);
        }

        private void CheckCrossing(Rigidbody body, Contact contact)
        {
            Vector3 previous = transform.InverseTransformPoint(contact.PreviousPosition) - sphere.center;
            Vector3 current = transform.InverseTransformPoint(body.worldCenterOfMass) - sphere.center;
            float velocity = Vector3.Dot(body.linearVelocity, transform.forward);
            contact.PreviousPosition = body.worldCenterOfMass;
            if (!IsForwardPlaneCrossing(previous, current, velocity, sphere.radius)) return;
            // Le changement d'état précède les événements : plusieurs colliders ne donnent qu'un pointage.
            SetState(CheckpointState.Passed);
            OnPassed?.Invoke(this);
            onPassed.Invoke();
        }

        /// <summary>Test géométrique indépendant : arrière vers avant, vitesse positive et intersection dans le trou.</summary>
        public static bool IsForwardPlaneCrossing(Vector3 previous, Vector3 current, float forwardVelocity, float radius)
        {
            if (forwardVelocity <= 0f || previous.z >= 0f || current.z < 0f || radius <= 0f) return false;
            float t = -previous.z / (current.z - previous.z);
            Vector3 intersection = Vector3.LerpUnclamped(previous, current, t);
            return intersection.x * intersection.x + intersection.y * intersection.y <= radius * radius;
        }

        private void OnDisable()
        {
            contacts.Clear();
            ApplyMaterials();
        }

        private void OnDrawGizmos()
        {
            var trigger = GetComponent<SphereCollider>();
            if (trigger == null) return;
            Gizmos.color = state == CheckpointState.Active ? new Color(1f, 0.79f, 0.08f) :
                state == CheckpointState.Passed ? new Color(0.18f, 0.77f, 0.71f) : Color.gray;
            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireSphere(trigger.center, trigger.radius);
            Gizmos.DrawLine(trigger.center, trigger.center + Vector3.forward * 12f);
            Gizmos.DrawLine(trigger.center + Vector3.forward * 12f, trigger.center + new Vector3(-1f, 0f, 10f));
            Gizmos.DrawLine(trigger.center + Vector3.forward * 12f, trigger.center + new Vector3(1f, 0f, 10f));
            Gizmos.matrix = previous;
        }

#if UNITY_EDITOR
        public void Configure(RendererMaterials[] bindings, LayerMask layers)
        {
            rendererMaterials = bindings;
            acceptedLayers = layers;
            state = CheckpointState.Inactive;
            sphere = GetComponent<SphereCollider>();
            ApplyMaterials();
        }
#endif
    }
}
