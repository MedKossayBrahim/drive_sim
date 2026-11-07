using System.Collections.Generic;
using UnityEngine;

namespace Project.Plane
{
    /// <summary>Valide le contact avec la vraie piste et l'arrêt ; un survol ou un posé ailleurs ne gagne jamais.</summary>
    [DefaultExecutionOrder(700), DisallowMultipleComponent]
    public sealed class LandingZone : MonoBehaviour
    {
        [SerializeField] private FlightMissionSettings settings;
        [SerializeField] private BoxCollider runway;
        [SerializeField] private LandingMission mission;
        [SerializeField] private PlaneInput input;
        [SerializeField] private PlaneSensors sensors;
        [SerializeField] private PlaneHealth health;
        [SerializeField] private PlaneLandingContact contacts;
        private Rigidbody body;
        private readonly List<Collider> aircraftColliders=new List<Collider>();
        private float held;
        public BoxCollider Runway=>runway;
        public bool IsGroundedOnRunway { get; private set; }
        public float LandingProgress=>settings!=null?Mathf.Clamp01(held/settings.StoppedHoldSeconds):0f;
        public Vector3 AimPoint=>runway!=null?runway.bounds.center:transform.position;
        private void Awake(){if(input!=null)body=input.GetComponent<Rigidbody>();}
        private void OnEnable(){held=0f;IsGroundedOnRunway=false;}
        private void FixedUpdate()
        {
            if(settings==null||runway==null||mission==null||body==null||mission.IsTerminal||health.IsCrashed)return;
            IsGroundedOnRunway=CheckGroundContact();
            if(!IsGroundedOnRunway){held=0f;return;}
            float headingError=Vector3.Angle(Vector3.ProjectOnPlane(body.rotation*Vector3.forward,Vector3.up),runway.transform.forward);
            bool attitude=Mathf.Abs(sensors.RollDegrees)<=settings.MaximumBank&&headingError<=settings.MaximumHeadingError&&
                sensors.PitchDegrees>=-5f&&sensors.PitchDegrees<=15f;
            bool gentle=Mathf.Abs(body.linearVelocity.y)<=settings.MaximumVerticalSpeed;
            var horizontal=Vector3.ProjectOnPlane(body.linearVelocity,Vector3.up);
            // Freinage de roues uniquement après un vrai posé doux, gaz coupés. Aucun frein invisible en vol.
            if(attitude&&gentle&&input.Controller.Throttle<=.2f&&horizontal.sqrMagnitude>.001f)
            {
                float acceleration=Mathf.Min(settings.WheelBrakeAcceleration,horizontal.magnitude/Time.fixedDeltaTime);
                body.AddForce(-horizontal.normalized*acceleration,ForceMode.Acceleration);
            }
            bool stopped=horizontal.magnitude<=settings.MaximumStoppedSpeed&&input.Controller.Throttle<=.15f;
            if(mission.CanLand&&attitude&&gentle&&stopped)
            {
                held+=Time.fixedDeltaTime;
                if(held>=settings.StoppedHoldSeconds)mission.CompleteLanding();
            }
            else held=0f;
        }
        private bool CheckGroundContact()
        {
            if(contacts==null||!contacts.HasContact(runway))return false;
            Vector3 local=runway.transform.InverseTransformPoint(body.worldCenterOfMass)-runway.center;
            if(Mathf.Abs(local.x)>runway.size.x*.5f||Mathf.Abs(local.z)>runway.size.z*.5f)return false;
            if(!Physics.Raycast(body.worldCenterOfMass,Vector3.down,out var hit,12f,LayerMask.GetMask("Ground"),QueryTriggerInteraction.Ignore)||hit.collider!=runway)return false;
            float bottom=float.PositiveInfinity;
            aircraftColliders.Clear();body.GetComponentsInChildren(false,aircraftColliders);
            foreach(var collider in aircraftColliders)
                if(collider.enabled&&!collider.isTrigger&&collider.attachedRigidbody==body)bottom=Mathf.Min(bottom,collider.bounds.min.y);
            float gap=bottom-hit.point.y;
            return float.IsFinite(bottom)&&gap>=-.3f&&gap<=settings.MaximumGroundGap&&Vector3.Dot(hit.normal,Vector3.up)>.95f;
        }
#if UNITY_EDITOR
        public void Configure(FlightMissionSettings config,BoxCollider surface,LandingMission coordinator,PlaneInput plane)
        {settings=config;runway=surface;mission=coordinator;input=plane;sensors=plane.GetComponent<PlaneSensors>();health=plane.GetComponent<PlaneHealth>();contacts=plane.GetComponent<PlaneLandingContact>();}
#endif
        private void OnDrawGizmosSelected()
        {if(runway==null)return;Gizmos.color=Color.cyan;Gizmos.DrawWireCube(runway.bounds.center+Vector3.up*4f,new Vector3(runway.bounds.size.x,8f,runway.bounds.size.z));}
    }
}
