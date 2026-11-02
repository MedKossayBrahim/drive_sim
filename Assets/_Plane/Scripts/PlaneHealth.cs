using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.Plane
{
    /// <summary>Dégâts de choc et retour au point de départ via le Reset existant de PlaneInput.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(PlaneInput), typeof(PlaneController), typeof(PlaneSensors))]
    public sealed class PlaneHealth : MonoBehaviour
    {
        [Header("Résistance et seuils — m/s")]
        [SerializeField, Min(1f)] private float maximumHealth = 100f;
        [SerializeField, Min(0f)] private float damageSpeed = 5f;
        [SerializeField, Min(0.1f)] private float crashSpeed = 20f;
        [SerializeField, Min(0f)] private float minimumDamage = 8f;
        [SerializeField, Min(0f)] private float damagePerSpeed = 6f;
        [SerializeField, Tooltip("Ignore la vitesse tangentielle : rouler vite ne doit pas transformer un contact doux en crash.")]
        private bool useNormalImpactSpeed = true;
        [SerializeField, Min(0f)] private float damageCooldown = 0.35f;
        [SerializeField, Min(0f)] private float respawnDelay = 3f;
        [SerializeField, Min(0f)] private float respawnProtection = 1.5f;
        [Header("Explosion procédurale")]
        [SerializeField] private Material explosionMaterial = null;
        [SerializeField, Range(8, 120)] private int explosionParticles = 60;
        [SerializeField, Min(0.1f)] private float explosionSize = 2f;
        private PlaneInput input;
        private PlaneController controller;
        private Coroutine respawn;
        private bool previousInputEnabled, previousControllerEnabled;
        private float nextDamageTime;
        public float Health { get; private set; }
        public float MaximumHealth => Mathf.Max(1f, maximumHealth);
        public float HealthFraction => Health / MaximumHealth;
        public bool IsCrashed { get; private set; }
        public float LastImpactSpeed { get; private set; }
        public event Action<float> OnDamaged;
        public event Action OnCrashed;
        public event Action OnRespawned;

        private void Awake()
        {
            input = GetComponent<PlaneInput>(); controller = GetComponent<PlaneController>();
            Health = MaximumHealth; nextDamageTime = Time.time + respawnProtection;
        }
        private void OnCollisionEnter(Collision collision)
        {
            if (IsCrashed || Time.time < nextDamageTime) return;
            float speed = collision.relativeVelocity.magnitude;
            if (useNormalImpactSpeed && collision.contactCount > 0)
            {
                speed = 0f;
                for (int i = 0; i < collision.contactCount; i++)
                    speed = Mathf.Max(speed, Mathf.Abs(Vector3.Dot(collision.relativeVelocity, collision.GetContact(i).normal)));
            }
            LastImpactSpeed = speed;
            if (speed < damageSpeed) return;
            nextDamageTime = Time.time + damageCooldown;
            float amount = speed >= Mathf.Max(damageSpeed + 0.1f, crashSpeed) ? MaximumHealth :
                minimumDamage + (speed - damageSpeed) * damagePerSpeed;
            ApplyDamage(amount);
        }
        public void ApplyDamage(float amount)
        {
            if (IsCrashed || !float.IsFinite(amount) || amount <= 0f) return;
            float actual = Mathf.Min(Health, amount); Health -= actual;
            // Le changement d'état précède les événements pour éviter les crashs multiples.
            bool lethal = Health <= 0f;
            if (lethal)
            {
                IsCrashed = true;
                previousInputEnabled = input.enabled; previousControllerEnabled = controller.enabled;
                input.enabled = false; controller.enabled = false;
                controller.Throttle = controller.Pitch = controller.Roll = controller.Yaw = 0f;
                CreateExplosion(); respawn = StartCoroutine(RespawnAfterDelay());
            }
            OnDamaged?.Invoke(actual);
            if (lethal) OnCrashed?.Invoke();
        }
        private IEnumerator RespawnAfterDelay()
        {
            // Délai en temps de jeu : le bouton Pause peut suspendre la séquence.
            yield return new WaitForSeconds(Mathf.Max(0f, respawnDelay));
            // ResetPlane mémorise la demande ; son FixedUpdate doit être actif pour la traiter.
            input.enabled = true;
            input.ResetPlane();
            yield return new WaitForFixedUpdate();
            // Le menu lie aussi onReset à HandleResetCompleted, ce repli sert à un ajout manuel du composant.
            if (IsCrashed) HandleResetCompleted();
            respawn = null;
        }
        /// <summary>Callback du Reset existant ; aucune duplication de la position, rotation ou vitesse de respawn.</summary>
        public void HandleResetCompleted()
        {
            bool crashed = IsCrashed;
            Health = MaximumHealth; IsCrashed = false; nextDamageTime = Time.time + respawnProtection;
            if (crashed)
            {
                controller.enabled = previousControllerEnabled;
                input.enabled = previousInputEnabled;
                OnRespawned?.Invoke();
            }
        }
        private void CreateExplosion()
        {
            var effect = new GameObject("PlaneCrash_Explosion");
            SceneManager.MoveGameObjectToScene(effect, gameObject.scene);
            effect.transform.position = GetComponent<Rigidbody>().worldCenterOfMass;
            var particles = effect.AddComponent<ParticleSystem>(); particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main; main.loop = false; main.duration = 0.15f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 12f);
            main.startSize = new ParticleSystem.MinMaxCurve(explosionSize * 0.2f, explosionSize);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f,0.25f,0.015f),new Color(1f,0.85f,0.12f));
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.gravityModifier = 0.15f; main.maxParticles = 150;
            var emission = particles.emission; emission.rateOverTime = 0f;
            emission.SetBursts(new[]{new ParticleSystem.Burst(0f,(short)explosionParticles)});
            var shape = particles.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.6f;
            var colors = particles.colorOverLifetime; colors.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[]{new GradientColorKey(Color.white,0f),new GradientColorKey(new Color(.18f,.18f,.18f),.6f)},
                new[]{new GradientAlphaKey(1f,0f),new GradientAlphaKey(0f,1f)}); colors.color = gradient;
            var renderer = effect.GetComponent<ParticleSystemRenderer>();
            if (explosionMaterial != null) renderer.sharedMaterial = explosionMaterial;
            particles.Play(); Destroy(effect,3f);
        }
        private void OnDisable()
        {
            if (respawn != null) { StopCoroutine(respawn); respawn = null; }
            // Ne laisse pas les commandes désactivées si le composant est retiré pendant un crash.
            if (IsCrashed && gameObject.activeInHierarchy && input != null && controller != null)
            { input.enabled = previousInputEnabled; controller.enabled = previousControllerEnabled; IsCrashed = false; }
        }
    }
}
