using UnityEngine;

namespace Project.Plane
{
    /// <summary>Complète les forces du contrôleur : piqué réel et perte de portance en décrochage, quel que soit le mode d'entrée.</summary>
    [DefaultExecutionOrder(400), DisallowMultipleComponent]
    [RequireComponent(typeof(PlaneController),typeof(PlaneSensors),typeof(Rigidbody))]
    public sealed class PlaneFlightEnvelope : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float minimumFlightAltitude = 4f;
        [Header("Piqué commandé")]
        [SerializeField, Range(0f,.3f)] private float diveInputThreshold = .05f;
        [SerializeField, Min(0f)] private float minimumDiveSpeed = 4f;
        [SerializeField, Min(0f)] private float maximumDiveSpeed = 55f;
        [SerializeField, Min(0f)] private float diveResponse = 1.4f;
        [SerializeField, Min(0f)] private float maximumDiveAcceleration = 12f;
        [Header("Décrochage — ne laisse pas le Tilt maintenir artificiellement l'assiette")]
        [SerializeField, Range(10f,75f)] private float stallNoseDownAngle = 35f;
        [SerializeField, Range(10f,80f)] private float fallingNoseDownAngle = 50f;
        [SerializeField, Min(0f)] private float stallPitchResponse = 5f;
        [SerializeField, Min(0f)] private float stallPitchDamping = 3f;
        [SerializeField, Min(0f)] private float maximumStallPitchAcceleration = 4f;
        [SerializeField, Range(0f,1f)] private float stalledWeightSupport = .5f;
        [SerializeField, Range(0f,1f)] private float fallingWeightSupport = .15f;
        private PlaneController controller;
        private PlaneSensors sensors;
        private PlaneHealth health;
        private FallCrashDetector fallDetector;
        private Rigidbody body;
        private void Awake()
        {
            controller=GetComponent<PlaneController>();sensors=GetComponent<PlaneSensors>();body=GetComponent<Rigidbody>();
            health=GetComponent<PlaneHealth>();fallDetector=GetComponent<FallCrashDetector>();
        }
        private void FixedUpdate()
        {
            if(!controller.enabled||body.isKinematic||health!=null&&health.IsCrashed)return;
            if(sensors.HasGroundAltitude&&sensors.AltitudeAGL<=minimumFlightAltitude)return;
            float dt=Time.fixedDeltaTime;
            Vector3 forward=body.rotation*Vector3.forward;
            float forwardSpeed=Vector3.Dot(body.linearVelocity,forward);
            bool cut=controller.Throttle<=controller.Settings.ThrottleCutoff;
            bool falling=cut&&fallDetector!=null&&fallDetector.IsFalling;
            bool stalled=forwardSpeed<controller.Settings.StallSpeed;
            if(stalled||falling)
            {
                // La commande Tilt à zéro ne doit pas annuler le piqué de décrochage.
                Vector3 right=Vector3.Cross(Vector3.up,forward);
                if(right.sqrMagnitude<.0001f)right=body.rotation*Vector3.right;
                right.Normalize();
                float pitch=Mathf.Asin(Mathf.Clamp(forward.y,-1f,1f));
                float rate=Vector3.Dot(body.angularVelocity,right);
                float target=-(falling?fallingNoseDownAngle:stallNoseDownAngle)*Mathf.Deg2Rad;
                float torque=Vector3.Dot(body.GetAccumulatedTorque(dt),right);
                if(torque<0f)body.AddTorque(-right*torque,ForceMode.Force);
                float correction=Mathf.Clamp((pitch-target)*stallPitchResponse-rate*stallPitchDamping,-maximumStallPitchAcceleration,maximumStallPitchAcceleration);
                body.AddTorque(right*correction,ForceMode.Acceleration);
                float support=body.mass*Physics.gravity.magnitude*(falling?fallingWeightSupport:stalledWeightSupport);
                float excess=Mathf.Max(0f,body.GetAccumulatedForce(dt).y-support);
                body.AddForce(Vector3.down*excess);
                return;
            }
            if(controller.Pitch>=-diveInputThreshold||forward.y>=-.02f)return;
            // L'ancienne portance pouvait toujours dépasser le poids à pleine vitesse, même nez vers le bas.
            // Retire seulement l'excès vertical ; ne crée ni poussée ni portance pour sauver un avion sans énergie.
            float targetSink=Mathf.Max(-maximumDiveSpeed,Mathf.Min(-minimumDiveSpeed*Mathf.Abs(controller.Pitch),body.linearVelocity.magnitude*forward.y));
            float desiredAcceleration=Mathf.Clamp((targetSink-body.linearVelocity.y)*diveResponse,-maximumDiveAcceleration,maximumDiveAcceleration);
            float currentAcceleration=body.GetAccumulatedForce(dt).y/body.mass+(body.useGravity?Physics.gravity.y:0f);
            if(currentAcceleration>desiredAcceleration)
                body.AddForce(Vector3.down*(currentAcceleration-desiredAcceleration),ForceMode.Acceleration);
        }
    }
}
