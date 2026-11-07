using UnityEngine;

namespace Project.Plane
{
    /// <summary>Budget et placement reproductibles ; données sans dépendance UnityEditor.</summary>
    [CreateAssetMenu(menuName = "Sky Courier/World Module Settings")]
    public sealed class WorldModuleSettings : ScriptableObject
    {
        [SerializeField] private int seed = 1741;
        [Header("Pièces")]
        [SerializeField, Min(20f)] private float coinSpacing = 65f;
        [SerializeField, Range(0, 6)] private int bonusCoinsPerRing = 3;
        [SerializeField, Min(0)] private int coinValue = 25;
        [SerializeField, Range(2f, 8f)] private float pickupRadius = 4f;
        [SerializeField, Range(.5f, 5f), Tooltip("Rayon visuel des pièces en mètres, indépendant du collider.")]
        private float coinVisualRadius = 2.8f;
        [Header("Montagnes — hauteurs sous l'alerte 600 m")]
        [SerializeField, Range(8, 48)] private int mountainCount = 28;
        [SerializeField, Range(800f, 3000f)] private float innerRadius = 1800f;
        [SerializeField, Range(1500f, 3900f)] private float outerRadius = 3500f;
        [SerializeField, Range(80f, 500f)] private float minimumPeak = 180f;
        [SerializeField, Range(150f, 560f)] private float maximumPeak = 520f;
        [SerializeField, Range(100f, 400f)] private float minimumBaseRadius = 150f;
        [SerializeField, Range(150f, 500f)] private float maximumBaseRadius = 330f;
        [SerializeField, Min(100f)] private float flightPathClearance = 250f;
        public int Seed => seed;
        public float CoinSpacing => Mathf.Max(20f, coinSpacing);
        public int BonusCoinsPerRing => bonusCoinsPerRing;
        public int CoinValue => Mathf.Max(0, coinValue);
        public float PickupRadius => pickupRadius;
        public float CoinVisualRadius => coinVisualRadius;
        public int MountainCount => mountainCount;
        public float InnerRadius => innerRadius;
        public float OuterRadius => Mathf.Max(innerRadius, outerRadius);
        public float MinimumPeak => Mathf.Min(minimumPeak, MaximumPeak);
        public float MaximumPeak => Mathf.Clamp(maximumPeak, 150f, 560f);
        public float MinimumBaseRadius => minimumBaseRadius;
        public float MaximumBaseRadius => Mathf.Max(minimumBaseRadius, maximumBaseRadius);
        public float FlightPathClearance => Mathf.Max(100f, flightPathClearance);
    }
}
