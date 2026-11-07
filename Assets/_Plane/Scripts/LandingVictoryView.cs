using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Project.Plane
{
    /// <summary>Affiche le bilan réel de la mission et l'anime avec le temps non ralenti.</summary>
    internal sealed class LandingVictoryView : MonoBehaviour
    {
        private const float CardWidth = 920f;
        private const float CardHeight = 664f;
        private static readonly Color Surface = new Color32(11, 23, 42, 255);
        private static readonly Color Tile = new Color32(23, 42, 64, 255);
        private static readonly Color Cyan = new Color32(97, 236, 211, 255);
        private static readonly Color Gold = new Color32(255, 212, 112, 255);
        private static readonly Color Muted = new Color32(176, 201, 222, 255);

        private RectTransform stage, card;
        private CanvasGroup entrance;
        private TMP_Text scoreLabel;
        private TMP_FontAsset font;
        private RectTransform[] sparks;
        private int totalScore, shownScore = -1;
        private float startedAt;

        internal static LandingVictoryView Create(Transform parent, TMP_FontAsset font, LandingMission mission,
            UnityAction restart, UnityAction quit)
        {
            var root = new GameObject("Victory", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            root.AddComponent<PlaneHUDSafeArea>();
            var view = root.AddComponent<LandingVictoryView>();
            view.font = font;
            view.Build(mission, restart, quit);
            view.startedAt = Time.unscaledTime;
            view.Update();
            return view;
        }

        private void Build(LandingMission mission, UnityAction restart, UnityAction quit)
        {
            var score = mission.Score;
            totalScore = score.Score;
            int routePoints = totalScore - score.CoinPoints - mission.LandingPoints;
            stage = Rect("Stage", transform, 0f, 0f, CardWidth, CardHeight);
            card = Rect("VictoryCard", stage, 0f, 0f, CardWidth, CardHeight);
            entrance = card.gameObject.AddComponent<CanvasGroup>();
            Fill("Frame", card, 0f, 0f, CardWidth, CardHeight, Tile);
            Fill("Background", card, 0f, 0f, CardWidth - 4f, CardHeight - 4f, Surface);
            Fill("TopAccent", card, 0f, 330f, 256f, 4f, Cyan);

            var badge = Fill("Badge", card, 0f, 251f, 62f, 62f, Cyan);
            badge.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Fill("CheckShort", card, -9f, 246f, 18f, 5f, Surface).rectTransform.localRotation =
                Quaternion.Euler(0f, 0f, -45f);
            Fill("CheckLong", card, 7f, 251f, 29f, 5f, Surface).rectTransform.localRotation =
                Quaternion.Euler(0f, 0f, 45f);

            Label("Brand", card, "S K Y   C O U R I E R", 0f, 190f, 760f, 28f, 20f, Cyan);
            Label("Title", card, "MISSION RÉUSSIE !", 0f, 147f, 840f, 58f, 52f, Color.white, FontStyles.Bold);
            Label("Subtitle", card, "Atterrissage confirmé à l’aéroport d’arrivée", 0f, 101f, 840f, 32f, 24f, Muted);
            Label("ScoreCaption", card, "SCORE FINAL", 0f, 47f, 840f, 27f, 22f, Muted);
            scoreLabel = Label("ScoreTotal", card, "0", 0f, -8f, 840f, 79f, 70f, Gold, FontStyles.Bold);

            Metric(-280f, "PIÈCES COLLECTÉES", $"{score.CoinsCollected}  /  +{score.CoinPoints:N0} pts", Gold);
            Metric(0f, "POINTS DU PARCOURS", $"{routePoints:N0} pts", Color.white);
            Metric(280f, "BONUS ATTERRISSAGE", $"+{mission.LandingPoints:N0} pts", Cyan);

            int seconds = Mathf.FloorToInt(mission.FlightSeconds);
            Label("FlightSummary", card, $"VOL {seconds / 60:00}:{seconds % 60:00}   ·   ANNEAUX {mission.RingsPassed}/{mission.RingCount}   ·   RECORD {score.BestScore:N0}",
                0f, -193f, 840f, 28f, 22f, Muted);
            ActionButton("Rejouer", "REJOUER", -150f, 536f, Cyan, Surface, restart);
            ActionButton("Quitter", "QUITTER", 280f, 266f, Tile, Color.white, quit);

            // Particules UI décoratives : aucun raycast et aucune allocation pendant l'animation.
            sparks = new RectTransform[22];
            for (int i = 0; i < sparks.Length; i++)
                sparks[i] = Fill("Confetti", stage, 0f, 0f, 6f + i % 3, 12f, i % 2 == 0 ? Cyan : Gold).rectTransform;
        }

        private void Update()
        {
            var safe = (RectTransform)transform;
            float fit = Mathf.Min(1f, Mathf.Min((safe.rect.width - 48f) / CardWidth, (safe.rect.height - 40f) / CardHeight));
            stage.localScale = Vector3.one * Mathf.Max(.01f, fit);
            float elapsed = Time.unscaledTime - startedAt;
            float reveal = Mathf.Clamp01(elapsed / .42f);
            float ease = 1f - Mathf.Pow(1f - reveal, 3f);
            entrance.alpha = ease;
            entrance.interactable = reveal >= 1f;
            card.localScale = Vector3.one * Mathf.Lerp(.91f, 1f, ease);
            card.anchoredPosition = Vector2.down * (1f - ease) * 28f;
            float countProgress = Mathf.Clamp01((elapsed - .18f) / .95f);
            int value = countProgress >= 1f ? totalScore : (int)(totalScore * (double)Mathf.SmoothStep(0f, 1f, countProgress));
            if (value != shownScore)
            {
                shownScore = value;
                scoreLabel.text = value.ToString("N0");
            }
            if (sparks[0].gameObject.activeSelf) AnimateConfetti(elapsed);
        }

        private void AnimateConfetti(float elapsed)
        {
            float progress = Mathf.Clamp01(elapsed / 1.6f);
            for (int i = 0; i < sparks.Length; i++)
            {
                var spark = sparks[i];
                if (progress >= 1f)
                {
                    spark.gameObject.SetActive(false);
                    continue;
                }
                float side = i % 2 == 0 ? -1f : 1f;
                float spread = i / 2f;
                float x = side * (340f + progress * (135f + spread * 8f));
                float y = 235f + spread * 6f + progress * (260f - spread * 28f) - 420f * progress * progress;
                spark.anchoredPosition = new Vector2(x, y);
                spark.localRotation = Quaternion.Euler(0f, 0f, 32f * i + side * progress * 280f);
                spark.localScale = Vector3.one * (1f - Mathf.SmoothStep(0f, 1f, (progress - .58f) / .42f));
            }
        }

        private void Metric(float x, string heading, string value, Color accent)
        {
            var tile = Fill(heading, card, x, -121f, 266f, 106f, Tile).rectTransform;
            Fill("Accent", tile, 0f, 52f, 266f, 2f, accent);
            Label("Caption", tile, heading, 0f, 22f, 246f, 26f, 17f, Muted);
            Label("Value", tile, value, 0f, -15f, 246f, 44f, 32f, accent, FontStyles.Bold);
        }

        private void ActionButton(string name, string caption, float x, float width, Color fill, Color text,
            UnityAction action)
        {
            var image = Fill(name, card, x, -260f, width, 96f, fill);
            image.raycastTarget = true;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(action);
            Label("Label", image.transform, caption, 0f, 0f, width - 24f, 80f, 28f, text, FontStyles.Bold);
        }

        private TMP_Text Label(string name, Transform parent, string text, float x, float y, float width,
            float height, float size, Color color, FontStyles style = FontStyles.Normal)
        {
            var label = Rect(name, parent, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
            label.font = font;
            label.text = text;
            label.fontSize = size;
            label.fontStyle = style;
            label.alignment = TextAlignmentOptions.Center;
            label.color = color;
            label.raycastTarget = false;
            label.enableAutoSizing = true;
            label.fontSizeMin = size * .66f;
            label.fontSizeMax = size;
            return label;
        }

        private static Image Fill(string name, Transform parent, float x, float y, float width, float height, Color color)
        {
            var image = Rect(name, parent, x, y, width, height).gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }
    }
}
