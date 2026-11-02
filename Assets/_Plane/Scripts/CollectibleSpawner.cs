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
            foreach (var item in instances) if (item != null) item.Bind(score, aircraft, health);
        }
#if UNITY_EDITOR
        public void Configure(Collectible source, Vector3[] positions, Collectible[] coins,
            ScoreManager owner, PlaneInput input, PlaneHealth aircraftHealth)
        { prefab = source; localPositions = positions; instances = coins; score = owner; aircraft = input; health = aircraftHealth; }
#endif
    }
}
