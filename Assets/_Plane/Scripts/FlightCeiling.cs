using System;
using UnityEngine;

namespace Project.Plane
{
    /// <summary>Limites arcade en coordonnées monde : forces ajoutées après le contrôleur, sans modifier ses commandes.</summary>
    [DefaultExecutionOrder(500), DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(PlaneController))]
    public sealed class FlightCeiling : MonoBehaviour
    {
        [Header("Plafond — mètres MSL, Y monde, même zéro que la piste")]
        [SerializeField, Min(0f)] private float warningAltitude = 600f;
        [SerializeField, Min(1f)] private float ceilingAltitude = 800f;
        [SerializeField, Min(.01f), Tooltip("Marge avant le plafond pour absorber les imprécisions du solveur.")]
        private float ceilingMargin = .15f;
        [SerializeField] private AnimationCurve reductionCurve = AnimationCurve.EaseInOut(0f,0f,1f,1f);
        [SerializeField, Range(0f,1f)] private float liftReduction = .35f;
        [SerializeField, Range(0f,1f)] private float thrustReduction = .25f;
        [SerializeField, Min(0f), Tooltip("Accélération de rappel vertical, en m/s² ; indépendante de la masse.")]
        private float returnAcceleration = 2.5f;
        [SerializeField, Min(.1f), Tooltip("Amortissement de la montée en fonction des mètres restants, en 1/s.")]
        private float climbDamping = 1.2f;
        [SerializeField, Min(1f)] private float maximumClimbSpeed = 32f;
        [SerializeField, Min(0f), Tooltip("Descente commandée maximale dans la zone haute, en m/s. Compense la forte portance arcade à pleine vitesse.")]
        private float commandedDescentSpeed = 12f;
        [SerializeField, Min(0f), Tooltip("Progressivité de la descente commandée, en m/s².")]
        private float commandedDescentAcceleration = 4f;
        [SerializeField, Min(0f)] private float pitchReturnStrength = 6f;
        [SerializeField, Min(0f)] private float pitchDamping = 4f;
        [Header("Protection de la vitesse — seulement moteur actif dans la zone haute")]
        [SerializeField, Min(1f)] private float safeSpeedMultiplier = 1.35f;
        [SerializeField, Min(0f)] private float speedProtectionResponse = 2f;
        [SerializeField, Min(0f)] private float maximumSpeedProtectionAcceleration = 8f;
        [Header("Limite horizontale optionnelle — mètres dans le plan XZ")]
        [SerializeField] private bool limitHorizontalDistance = true;
        [SerializeField] private Vector3 mapCenter = Vector3.zero;
        [SerializeField, Min(100f)] private float horizontalRadius = 4000f;
        [SerializeField, Min(0f)] private float horizontalWarningMargin = 300f;
        [SerializeField, Min(1f)] private float horizontalReturnBand = 600f;
        [SerializeField, Min(0f)] private float horizontalReturnAcceleration = 8f;
        [SerializeField, Min(0f)] private float outwardSpeedDamping = 1.5f;
        [SerializeField, Min(0f)] private float headingReturnStrength = .8f;
        [Header("HUD")]
        [SerializeField] private string warningMessage = "ALTITUDE MAX";
        [SerializeField] private string horizontalMessage = "RETOURNEZ DANS LA ZONE";
        [SerializeField, Min(.1f)] private float blinkPeriod = .8f;
        private Rigidbody body;
        private PlaneController controller;
        private PlaneHealth health;
        public float AltitudeMSL => body != null ? body.position.y : transform.position.y;
        public float WarningAltitude => warningAltitude;
        public float CeilingAltitude => Mathf.Max(warningAltitude+1f,ceilingAltitude);
        public float HorizontalRadius => horizontalRadius;
        public Vector3 MapCenter => mapCenter;
        public bool IsCeilingWarning { get; private set; }
        public bool IsAtCeiling => AltitudeMSL >= CeilingAltitude - Mathf.Max(.01f,ceilingMargin) - .05f;
        public bool IsOutsideZone { get; private set; }
        public string WarningMessage => warningMessage;
        public string HorizontalMessage => horizontalMessage;
        public bool WarningVisible => Mathf.Repeat(Time.unscaledTime,Mathf.Max(.1f,blinkPeriod)) < Mathf.Max(.1f,blinkPeriod)*.65f;
        public event Action<bool> OnCeilingWarning;
        public event Action<bool> OnZoneWarning;
        private void Awake(){body=GetComponent<Rigidbody>();controller=GetComponent<PlaneController>();health=GetComponent<PlaneHealth>();}
        private void FixedUpdate()
        {
            if(body.isKinematic||!controller.enabled||health!=null&&health.IsCrashed){SetWarnings(false,false);return;}
            float altitude=AltitudeMSL;
            bool warning=altitude>=warningAltitude;
            Vector3 radial=body.position-mapCenter;radial.y=0f;
            SetWarnings(warning,limitHorizontalDistance&&radial.magnitude>=Mathf.Max(1f,horizontalRadius-horizontalWarningMargin));
            if(warning)ApplyCeiling(altitude);
            if(limitHorizontalDistance)ApplyHorizontalBoundary(radial);
        }
        private void ApplyCeiling(float altitude)
        {
            float dt=Time.fixedDeltaTime;
            float progress=Mathf.InverseLerp(warningAltitude,CeilingAltitude,altitude);
            float weight=Mathf.Clamp01(reductionCurve!=null?reductionCurve.Evaluate(progress):progress);
            Vector3 forward=body.rotation*Vector3.forward;
            float forwardSpeed=Vector3.Dot(body.linearVelocity,forward);
            var settings=controller.Settings;
            float safeSpeed=settings.StallSpeed*Mathf.Max(1f,safeSpeedMultiplier);
            // Ne réduit pas la poussée à proximité du décrochage et n'invente pas un moteur lorsque les gaz sont coupés.
            float thrustGuard=Mathf.InverseLerp(safeSpeed,safeSpeed*1.5f,forwardSpeed);
            if(controller.Throttle>settings.ThrottleCutoff)
            {
                body.AddForce(-forward*(controller.EngineThrottle*settings.MaxThrust*thrustReduction*weight*thrustGuard));
                float assist=Mathf.Clamp((safeSpeed-forwardSpeed)*speedProtectionResponse,0f,maximumSpeedProtectionAcceleration);
                body.AddForce(forward*(assist*weight),ForceMode.Acceleration);
            }
            // Inclut la portance et la composante montante de la poussée déjà calculées par PlaneController.
            float upwardForce=Mathf.Max(0f,body.GetAccumulatedForce(dt).y);
            body.AddForce(Vector3.down*(upwardForce*liftReduction*weight+body.mass*returnAcceleration*weight));
            ReturnNoseToHorizon(forward,weight,dt);

            // Prévoit le prochain pas en tenant compte des forces ET de la gravité. Aucune téléportation, aucun collider plafond.
            float remaining=CeilingAltitude-Mathf.Max(.01f,ceilingMargin)-altitude;
            float allowedClimb=remaining>=0f?Mathf.Min(maximumClimbSpeed,remaining*climbDamping):Mathf.Max(-5f,remaining*climbDamping);
            if(controller.Pitch<-.05f&&forward.y<-.02f)
            {
                float target=-commandedDescentSpeed*Mathf.Clamp01(-controller.Pitch)*Mathf.Clamp01(-forward.y/.2f)*weight;
                float descent=Mathf.MoveTowards(body.linearVelocity.y,target,commandedDescentAcceleration*dt);
                allowedClimb=Mathf.Min(allowedClimb,descent);
            }
            float predictedClimb=body.linearVelocity.y+(body.GetAccumulatedForce(dt).y/body.mass+(body.useGravity?Physics.gravity.y:0f))*dt;
            if(predictedClimb>allowedClimb)
                body.AddForce(Vector3.down*((predictedClimb-allowedClimb)/dt),ForceMode.Acceleration);
        }
        private void ReturnNoseToHorizon(Vector3 forward,float weight,float dt)
        {
            Vector3 right=Vector3.Cross(Vector3.up,forward);
            if(right.sqrMagnitude<.0001f)right=body.rotation*Vector3.right;
            right.Normalize();
            float pitch=Mathf.Asin(Mathf.Clamp(forward.y,-1f,1f));
            float rate=Vector3.Dot(body.angularVelocity,right);
            float allowedPitch=controller.Settings.TiltPitchAngle*Mathf.Deg2Rad*(1f-weight);
            if(pitch<=allowedPitch&&rate>=0f)return;
            float accumulatedPitch=Vector3.Dot(body.GetAccumulatedTorque(dt),right);
            // Résiste au cabré vers le plafond ; une commande à piquer reste toujours disponible.
            if(accumulatedPitch<0f)body.AddTorque(-right*(accumulatedPitch*weight),ForceMode.Force);
            float correction=(Mathf.Max(0f,pitch-allowedPitch)*pitchReturnStrength-rate*pitchDamping)*weight;
            body.AddTorque(right*correction,ForceMode.Acceleration);
        }
        private void ApplyHorizontalBoundary(Vector3 radial)
        {
            float distance=radial.magnitude;
            if(distance<=horizontalRadius||distance<.01f)return;
            Vector3 outward=radial/distance;
            float weight=Mathf.Clamp01((distance-horizontalRadius)/Mathf.Max(1f,horizontalReturnBand));
            float speedOut=Mathf.Max(0f,Vector3.Dot(body.linearVelocity,outward));
            float acceleration=Mathf.Min(horizontalReturnAcceleration,(horizontalReturnAcceleration+speedOut*outwardSpeedDamping)*weight);
            body.AddForce(-outward*acceleration,ForceMode.Acceleration);
            Vector3 heading=Vector3.ProjectOnPlane(body.rotation*Vector3.forward,Vector3.up);
            if(heading.sqrMagnitude>.01f)
            {
                float angle=Vector3.SignedAngle(heading,-outward,Vector3.up)*Mathf.Deg2Rad;
                float torque=Mathf.Clamp(angle-body.angularVelocity.y,-1f,1f)*headingReturnStrength*weight;
                body.AddTorque(Vector3.up*torque,ForceMode.Acceleration);
            }
        }
        private void SetWarnings(bool ceiling,bool zone)
        {
            if(IsCeilingWarning!=ceiling){IsCeilingWarning=ceiling;OnCeilingWarning?.Invoke(ceiling);}
            if(IsOutsideZone!=zone){IsOutsideZone=zone;OnZoneWarning?.Invoke(zone);}
        }
        private void OnDisable()=>SetWarnings(false,false);
        private void OnValidate()
        {
            warningAltitude=Mathf.Max(0f,warningAltitude);ceilingAltitude=Mathf.Max(warningAltitude+1f,ceilingAltitude);
            ceilingMargin=Mathf.Clamp(ceilingMargin,.01f,1f);horizontalRadius=Mathf.Max(100f,horizontalRadius);
        }
        private void OnDrawGizmosSelected()
        {
            Gizmos.color=new Color(1f,.55f,.1f,.7f);Gizmos.DrawWireCube(new Vector3(mapCenter.x,warningAltitude,mapCenter.z),new Vector3(300,0,300));
            Gizmos.color=Color.red;Gizmos.DrawWireCube(new Vector3(mapCenter.x,CeilingAltitude,mapCenter.z),new Vector3(300,0,300));
            if(!limitHorizontalDistance)return;
            Vector3 previous=mapCenter+Vector3.forward*horizontalRadius;
            for(int i=1;i<=64;i++){float a=i*Mathf.PI*2/64;var next=mapCenter+new Vector3(Mathf.Sin(a),0,Mathf.Cos(a))*horizontalRadius;Gizmos.DrawLine(previous,next);previous=next;}
        }
    }
}
