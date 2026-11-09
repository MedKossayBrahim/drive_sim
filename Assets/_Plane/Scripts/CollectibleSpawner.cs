using UnityEngine;

namespace Project.Plane
{
    /// <summary>Données de placement utilisables sans UnityEditor ; réutilise les pièces préparées dans la scène.</summary>
    [DisallowMultipleComponent]
    public sealed class CollectibleSpawner : MonoBehaviour
    {
        [SerializeField] private Collectible prefab;
        [SerializeField] private Vector3[] localPositions = new Vector3[0];
        [SerializeField] private Collectible[] instances = new Collectible[0];
        [SerializeField] private ScoreManager score;
        [SerializeField] private PlaneInput aircraft;
        [SerializeField] private PlaneHealth health;
        public int PlannedCount => localPositions.Length;
        public int SpawnedCount => instances.Length;

        internal Collectible FindGuidanceCoin(Vector3 position, Vector3 forward)
        {
            int ground = LayerMask.GetMask("Ground");
            var heading = Vector3.ProjectOnPlane(forward, Vector3.up).normalized;
            Collectible best = null;
            float bestCost = float.PositiveInfinity;
            foreach (var coin in instances)
            {
                if (coin == null || !coin.isActiveAndEnabled || coin.IsCollected) continue;

                Vector3 offset = coin.transform.position - position;
                Vector3 flat = Vector3.ProjectOnPlane(offset, Vector3.up);
                // Une pièce derrière reste disponible, mais les pièces devant facilitent la poursuite du vol.
                float alignment = Vector3.Dot(heading, flat.normalized);
                float cost = offset.sqrMagnitude * Mathf.Lerp(3.5f, 1f, (alignment + 1f) * .5f);
                if (cost >= bestCost) continue;
                if (Physics.Linecast(position, coin.transform.position, ground, QueryTriggerInteraction.Ignore)) continue;
                best = coin;
                bestCost = cost;
            }
            return best;
        }

        private void Awake()
        {
            // Repli pour une scène livrée uniquement avec les données. Ne double pas les instances déjà sérialisées.
            if (instances.Length == 0 && prefab != null)
            {
                instances = new Collectible[localPositions.Length];
                for (int i = 0; i < instances.Length; i++)
                {
                    instances[i] = Instantiate(prefab, transform);
                    instances[i].transform.localPosition = localPositions[i];
                }
            }
            // Garantit le layer Plane (les pièces n'acceptent que ce layer).
            int planeLayer = LayerMask.NameToLayer("Plane");
            if (aircraft != null && planeLayer >= 0 && aircraft.gameObject.layer != planeLayer)
                aircraft.gameObject.layer = planeLayer;
            FlightPowerUps bonuses = null;
            if (aircraft != null && score != null && health != null && instances.Length > 1)
            {
                bonuses = aircraft.GetComponent<FlightPowerUps>() ?? aircraft.gameObject.AddComponent<FlightPowerUps>();
                bonuses.Configure(aircraft, health, transform, instances);
            }
            foreach (var item in instances)
                if (item != null) item.Bind(score, aircraft, health, bonuses);
        }
#if UNITY_EDITOR
        public void Configure(Collectible source, Vector3[] positions, Collectible[] coins,
            ScoreManager owner, PlaneInput input, PlaneHealth aircraftHealth)
        { prefab = source; localPositions = positions; instances = coins; score = owner; aircraft = input; health = aircraftHealth; }
#endif
    }
}
