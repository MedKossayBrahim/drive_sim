using UnityEngine;

namespace Project.Plane
{
    /// <summary>Objectif, piste d'arrivée et relief infranchissable ; distances en mètres.</summary>
    [CreateAssetMenu(menuName="Sky Courier/Flight Mission Settings")]
    public sealed class FlightMissionSettings : ScriptableObject
    {
        [SerializeField, Min(1)] private int requiredCoins=15;
        [SerializeField, Min(0)] private int landingBonus=500;
        [SerializeField] private bool requireAllRings=false;
        [Header("Aéroport d'arrivée")]
        [SerializeField] private Vector3 arrivalCenter=new Vector3(0,0,3000);
        [SerializeField, Min(30f)] private float runwayWidth=48f;
        [SerializeField, Min(500f)] private float runwayLength=900f;
        [Header("Atterrissage et freinage au sol")]
        [SerializeField, Min(.1f)] private float maximumStoppedSpeed=5f;
        [SerializeField, Min(.1f)] private float maximumVerticalSpeed=2f;
        [SerializeField, Range(0f,30f)] private float maximumBank=12f;
        [SerializeField, Range(0f,45f)] private float maximumHeadingError=20f;
        [SerializeField, Min(.1f)] private float stoppedHoldSeconds=2f;
        [SerializeField, Range(.05f,.6f)] private float maximumGroundGap=.35f;
        [SerializeField, Min(0f)] private float wheelBrakeAcceleration=8f;
        [Header("Ceinture de montagnes — plus haute que le plafond de vol")]
        [SerializeField, Min(3000f)] private float boundaryFootRadius=3600f;
        [SerializeField, Min(3500f)] private float boundaryRidgeRadius=3850f;
        [SerializeField, Min(4000f)] private float boundaryOuterRadius=4400f;
        [SerializeField, Min(900f)] private float minimumRidgeHeight=1050f;
        [SerializeField, Min(0f)] private float peakVariation=180f;
        [SerializeField] private int mountainSeed=2417;
        public int RequiredCoins=>Mathf.Max(1,requiredCoins);
        public int LandingBonus=>Mathf.Max(0,landingBonus);
        public bool RequireAllRings=>requireAllRings;
        public Vector3 ArrivalCenter=>arrivalCenter;
        public float RunwayWidth=>runwayWidth;
        public float RunwayLength=>runwayLength;
        public float MaximumStoppedSpeed=>maximumStoppedSpeed;
        public float MaximumVerticalSpeed=>maximumVerticalSpeed;
        public float MaximumBank=>maximumBank;
        public float MaximumHeadingError=>maximumHeadingError;
        public float StoppedHoldSeconds=>stoppedHoldSeconds;
        public float MaximumGroundGap=>maximumGroundGap;
        public float WheelBrakeAcceleration=>wheelBrakeAcceleration;
        public float BoundaryFootRadius=>boundaryFootRadius;
        public float BoundaryRidgeRadius=>boundaryRidgeRadius;
        public float BoundaryOuterRadius=>boundaryOuterRadius;
        public float MinimumRidgeHeight=>minimumRidgeHeight;
        public float PeakVariation=>peakVariation;
        public int MountainSeed=>mountainSeed;
    }
}
