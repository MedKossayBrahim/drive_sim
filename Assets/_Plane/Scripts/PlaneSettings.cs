using UnityEngine;

namespace Project.Plane
{
    /// <summary>Réglages SI du modèle de vol arcade : mètres, secondes, kilogrammes, newtons.</summary>
    [CreateAssetMenu(fileName = "PlaneSettings", menuName = "Sky Courier/Plane Settings")]
    public sealed class PlaneSettings : ScriptableObject
    {
        [Header("Rigidbody")]
        [SerializeField, Min(1f)] private float mass = 1200f;
        [SerializeField, Min(0.1f), Tooltip("Limite en degrés/seconde ; convertie en radians pour le Rigidbody.")]
        private float maxAngularSpeed = 120f;

        [Header("Forces")]
        [SerializeField, Min(0f)] private float maxThrust = 18000f;
        [SerializeField, Min(0f), Tooltip("Portance en N = coefficient × vitesse avant au carré.")]
        private float liftCoefficient = 12f;
        [SerializeField, Min(1f), Tooltip("Plafond arcade de portance, en multiples du poids.")]
        private float maxLiftLoadFactor = 3f;
        [SerializeField, Min(0f), Tooltip("Traînée en N = coefficient × vitesse totale au carré.")]
        private float dragCoefficient = 0.9f;
        [SerializeField, Min(0f)] private float flapsExtraDrag = 1.6f;
        [SerializeField, Min(0f)] private float gearExtraDrag = 0.6f;
        [SerializeField, Min(0f), Tooltip("Portance supplémentaire avec les volets complètement sortis.")]
        private float flapsExtraLift = 0.4f;

        [Header("Décrochage")]
        [SerializeField, Min(0.1f)] private float stallSpeed = 22f;
        [SerializeField, Range(0f, 1f)] private float stalledLiftFraction = 0.15f;
        [SerializeField, Min(0f), Tooltip("Accélération angulaire à piquer, en radians/s².")]
        private float stallNoseDownAcceleration = 0.8f;

        [Header("Commandes — accélérations angulaires en radians/s²")]
        [SerializeField, Min(0f)] private float pitchAcceleration = 1.3f;
        [SerializeField, Min(0f)] private float rollAcceleration = 2.3f;
        [SerializeField, Min(0f)] private float yawAcceleration = 0.7f;
        [SerializeField, Min(0.1f)] private float fullControlSpeed = 30f;
        [SerializeField, Range(0f, 1f), Tooltip("Autorité restante à l'arrêt pour les commandes arcade.")]
        private float minimumControlAuthority = 0.15f;
        [SerializeField, Min(0f), Tooltip("Amortissement des vitesses angulaires locales X/Y/Z.")]
        private Vector3 angularDamping = new Vector3(1.6f, 1.8f, 1.8f);

        [Header("Stabilisation automatique")]
        [SerializeField] private bool autoStabilization = true;
        [SerializeField, Min(0f)] private float pitchLevelStrength = 0.65f;
        [SerializeField, Min(0f)] private float rollLevelStrength = 1.2f;
        [SerializeField, Range(0f, 0.5f)] private float stabilizationInputDeadZone = 0.05f;

        [Header("Plané et moteur — réglages arcade")]
        [SerializeField, Min(2f), Tooltip("Distance horizontale / hauteur perdue au meilleur plané, train rentré et nez vers -atan(1/finesse).")]
        private float glideRatio = 8f;
        [SerializeField, Min(0f), Tooltip("Traînée quadratique supplémentaire lorsque les gaz sont coupés ; 0,3 donne environ 8:1 à 35 m/s avec les autres valeurs par défaut.")]
        private float engineOffDrag = 0.3f;
        [SerializeField, Min(0f), Tooltip("Temps de montée de 0 à 100 % du régime moteur. À gaz zéro, la poussée est immédiatement nulle ; l'hélice ralentit avec cette inertie.")]
        private float engineResponseTime = 1f;
        [SerializeField, Min(0.1f), Tooltip("Réactivité du soutien arcade au plané, en 1/s. La portance s'effondre sous stallSpeed ; assistance suspendue en chute incontrôlée.")]
        private float glideSinkResponse = 1f;
        [SerializeField, Range(0f, 0.1f)] private float throttleCutoff = 0.01f;
        [Header("Tilt direct et virage coordonné")]
        [SerializeField, Range(5f, 70f)] private float tiltBankAngle = 35f;
        [SerializeField, Range(5f, 60f)] private float tiltPitchAngle = 25f;
        [SerializeField, Min(0f)] private float tiltRollResponse = 12f;
        [SerializeField, Min(0f)] private float tiltPitchResponse = 8f;
        [SerializeField, Min(0f)] private float tiltAngularDamping = 5f;
        [SerializeField, Range(0f, 3f), Tooltip("Gain du virage g*tan(roulis)/vitesse. 1 coordonne naturellement le virage sans entrée de lacet.")]
        private float bankToTurn = 1f;
        [SerializeField, Min(0f)] private float coordinatedTurnResponse = 3f;
        [Header("Assistance au retour à plat")]
        [SerializeField, Min(0f), Tooltip("Gain de maintien d'assiette Tilt quand le téléphone revient au neutre.")]
        private float easyAssistance = 1.2f;
        [SerializeField, Min(0f), Tooltip("Maintien plus faible en Pro ; les commandes actives gardent toute leur réactivité.")]
        private float proAssistance = 0.18f;
        [SerializeField, Min(0f), Tooltip("Couple supplémentaire à piquer lors d'une chute confirmée, en rad/s².")]
        private float fallingNoseDownAcceleration = 1.2f;

        public float GlideRatio => Mathf.Max(2f, glideRatio);
        public float EngineOffDrag => Mathf.Max(0f, engineOffDrag);
        public float EngineResponseTime => Mathf.Max(0f, engineResponseTime);
        public float GlideSinkResponse => Mathf.Max(0.1f, glideSinkResponse);
        public float ThrottleCutoff => Mathf.Clamp(throttleCutoff, 0f, 0.1f);
        public float TiltBankAngle => tiltBankAngle;
        public float TiltPitchAngle => tiltPitchAngle;
        public float TiltRollResponse => tiltRollResponse;
        public float TiltPitchResponse => tiltPitchResponse;
        public float TiltAngularDamping => tiltAngularDamping;
        public float BankToTurn => bankToTurn;
        public float CoordinatedTurnResponse => coordinatedTurnResponse;
        public float EasyAssistance => easyAssistance;
        public float ProAssistance => proAssistance;
        public float FallingNoseDownAcceleration => fallingNoseDownAcceleration;

        public float Mass => Mathf.Max(1f, mass);
        public float MaxAngularSpeed => Mathf.Max(0.1f, maxAngularSpeed);
        public float MaxThrust => Mathf.Max(0f, maxThrust);
        public float LiftCoefficient => Mathf.Max(0f, liftCoefficient);
        public float MaxLiftLoadFactor => Mathf.Max(1f, maxLiftLoadFactor);
        public float DragCoefficient => Mathf.Max(0f, dragCoefficient);
        public float FlapsExtraDrag => Mathf.Max(0f, flapsExtraDrag);
        public float GearExtraDrag => Mathf.Max(0f, gearExtraDrag);
        public float FlapsExtraLift => Mathf.Max(0f, flapsExtraLift);
        public float StallSpeed => Mathf.Max(0.1f, stallSpeed);
        public float StalledLiftFraction => Mathf.Clamp01(stalledLiftFraction);
        public float StallNoseDownAcceleration => Mathf.Max(0f, stallNoseDownAcceleration);
        public float PitchAcceleration => Mathf.Max(0f, pitchAcceleration);
        public float RollAcceleration => Mathf.Max(0f, rollAcceleration);
        public float YawAcceleration => Mathf.Max(0f, yawAcceleration);
        public float FullControlSpeed => Mathf.Max(0.1f, fullControlSpeed);
        public float MinimumControlAuthority => Mathf.Clamp01(minimumControlAuthority);
        public Vector3 AngularDamping => Vector3.Max(Vector3.zero, angularDamping);
        public bool AutoStabilization => autoStabilization;
        public float PitchLevelStrength => Mathf.Max(0f, pitchLevelStrength);
        public float RollLevelStrength => Mathf.Max(0f, rollLevelStrength);
        public float StabilizationInputDeadZone => Mathf.Clamp(stabilizationInputDeadZone, 0f, 0.5f);
    }
}
