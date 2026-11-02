using System;
using UnityEngine;

namespace Project.Plane
{
    /// <summary>Avertit d'une chute récupérable ; seul un choc dur peut déclencher le crash de PlaneHealth.</summary>
    [DefaultExecutionOrder(-20), DisallowMultipleComponent]
    [RequireComponent(typeof(PlaneController), typeof(PlaneSensors), typeof(PlaneHealth))]
    public sealed class FallCrashDetector : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float prolongedStallSeconds = 4f;
        [SerializeField, Min(0f)] private float minimumFlightAltitude = 5f;
        [SerializeField, Tooltip("Déclenchement indépendant du stall, en m/s vers le bas.")]
        private float uncontrolledVerticalSpeed = -40f;
        [SerializeField, Min(0f)] private float crashImpactSpeed = 20f;
        [SerializeField, Min(0f)] private float recoveryThrottle = .15f;
        [SerializeField, Min(1f)] private float recoveryStallMargin = 1.1f;
        [SerializeField] private float recoveryVerticalSpeed = -12f;
        [SerializeField] private LayerMask groundMask = 1 << 8;
        private PlaneController controller;
        private PlaneSensors sensors;
        private PlaneHealth health;
        private PlaneInput input;
        private Rigidbody body;
        private float stallDuration;
        public bool IsFalling { get; private set; }
        public float StallDuration => stallDuration;
        public event Action<bool> OnFalling;
        private void Awake()
        {
            controller=GetComponent<PlaneController>();sensors=GetComponent<PlaneSensors>();health=GetComponent<PlaneHealth>();
            input=GetComponent<PlaneInput>();body=GetComponent<Rigidbody>();
        }
        private void OnEnable()
        {
            // Awake peut ne pas avoir été appelé dans une configuration faite à la main en édition.
            if(input==null)input=GetComponent<PlaneInput>();if(health==null)health=GetComponent<PlaneHealth>();
            if(input!=null)input.OnResetCompleted+=ResetDetection;
            if(health!=null){health.OnCrashed+=ResetDetection;health.OnRespawned+=ResetDetection;}
        }
        private void FixedUpdate()
        {
            if(health.IsCrashed){ResetDetection();return;}
            bool airborne=!sensors.HasGroundAltitude||sensors.AltitudeAGL>minimumFlightAltitude;
            bool cut=controller.Throttle<=controller.Settings.ThrottleCutoff;
            bool stalled=controller.ForwardSpeed<controller.Settings.StallSpeed;
            // Une brève reprise de vitesse pendant le piqué ne supprime pas la chute si le téléphone reste au neutre.
            // Un plané volontaire, rapide et nez baissé reste autorisé sans explosion automatique.
            bool controlledGlide=controller.Pitch<-.1f&&controller.ForwardSpeed>=controller.Settings.StallSpeed*1.15f;
            if(!airborne||!cut||controlledGlide)stallDuration=0f;
            else if(stalled||stallDuration>0f)stallDuration+=Time.fixedDeltaTime;
            if(airborne&&((cut&&stallDuration>=prolongedStallSeconds)||body.linearVelocity.y<=uncontrolledVerticalSpeed))SetFalling(true);
            bool recovered=controller.Throttle>=recoveryThrottle&&controller.ForwardSpeed>=controller.Settings.StallSpeed*recoveryStallMargin&&body.linearVelocity.y>=recoveryVerticalSpeed;
            bool gentleGround=!airborne&&Mathf.Abs(body.linearVelocity.y)<3f;
            if(recovered||gentleGround)SetFalling(false);
            controller.FallingNoseDown=IsFalling;
        }
        private void OnCollisionEnter(Collision collision)
        {
            if(!IsFalling||health.IsCrashed||(groundMask.value&(1<<collision.gameObject.layer))==0)return;
            float impact=0f;
            for(int i=0;i<collision.contactCount;i++)
            {
                var contact=collision.GetContact(i);
                if(Vector3.Dot(contact.normal,Vector3.up)>.5f)
                    impact=Mathf.Max(impact,Mathf.Abs(Vector3.Dot(collision.relativeVelocity,contact.normal)));
            }
            // Même sécurité que PlaneHealth : ni la vitesse horizontale ni un simple contact doux ne sont létaux.
            if(impact>=crashImpactSpeed)health.ApplyDamage(health.MaximumHealth);
        }
        private void SetFalling(bool value)
        {
            if(IsFalling==value)return;IsFalling=value;
            if(controller!=null)controller.FallingNoseDown=value;
            OnFalling?.Invoke(value);
        }
        public void ResetDetection(){stallDuration=0f;SetFalling(false);if(controller!=null)controller.FallingNoseDown=false;}
        private void OnDisable()
        {
            if(input!=null)input.OnResetCompleted-=ResetDetection;
            if(health!=null){health.OnCrashed-=ResetDetection;health.OnRespawned-=ResetDetection;}
            ResetDetection();
        }
    }
}
