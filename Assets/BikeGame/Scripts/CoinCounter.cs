using UnityEngine;
using UnityEngine.UI;

namespace BikeGame
{
    /// <summary>
    /// Keeps the collected coin count and drives the top-right HUD (coin icon plus
    /// amount). The UI is a normal uGUI canvas built by the editor setup tool, so
    /// the text stays crisp at any Android landscape resolution and the count can
    /// be updated on the same frame a coin is picked up.
    /// </summary>
    [DisallowMultipleComponent]
    public class CoinCounter : MonoBehaviour
    {
        public static CoinCounter Instance { get; private set; }

        [SerializeField] private int totalCoins;
        [SerializeField] private int collectedCoins;

        [Header("UI")]
        [SerializeField] private RectTransform safeArea;
        [SerializeField] private Text countLabel;
        [SerializeField] private RectTransform icon;
        [SerializeField] private Color countColor = new Color(1f, 0.95f, 0.78f, 1f);

        private float pulse;
        private Rect lastSafeArea;

        public int CollectedCoins => collectedCoins;
        public int TotalCoins => totalCoins;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            RefreshLabel();
            ApplySafeArea();
        }

        private void Start()
        {
            if (totalCoins <= 0)
                totalCoins = FindObjectsByType<CoinCollectible>().Length;

            RefreshLabel();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        /// <summary>Called by <see cref="CoinCollectible"/> on pickup.</summary>
        public void AddCoin()
        {
            collectedCoins++;
            pulse = 1f;

            // Update the label in the same call so the HUD never lags a frame.
            RefreshLabel();

            Debug.LogFormat("CoinCounter: collected {0}/{1}", collectedCoins, totalCoins);
        }

        private void Update()
        {
            ApplySafeArea();

            if (pulse > 0f)
                pulse = Mathf.MoveTowards(pulse, 0f, Time.unscaledDeltaTime * 5f);

            if (icon != null)
            {
                float scale = 1f + 0.22f * pulse * pulse;
                icon.localScale = new Vector3(scale, scale, 1f);
            }

            if (countLabel != null)
                countLabel.color = Color.Lerp(countColor, Color.white, pulse * 0.6f);
        }

        private void RefreshLabel()
        {
            if (countLabel != null)
                countLabel.text = collectedCoins.ToString();
        }

        /// <summary>
        /// Fits the HUD to the device safe area so notches and rounded corners do
        /// not clip it in landscape.
        /// </summary>
        private void ApplySafeArea()
        {
            if (safeArea == null || Screen.width <= 0 || Screen.height <= 0)
                return;

            Rect safe = Screen.safeArea;
            if (safe == lastSafeArea)
                return;

            lastSafeArea = safe;

            Vector2 min = safe.position;
            Vector2 max = safe.position + safe.size;
            min.x /= Screen.width;
            min.y /= Screen.height;
            max.x /= Screen.width;
            max.y /= Screen.height;

            safeArea.anchorMin = min;
            safeArea.anchorMax = max;
            safeArea.offsetMin = Vector2.zero;
            safeArea.offsetMax = Vector2.zero;
        }
    }
}
