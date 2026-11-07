using TMPro;
using UnityEngine;

namespace Project.Plane
{
    /// <summary>Affichage global et retour de points séparé des messages de sécurité du HUD de vol.</summary>
    [DisallowMultipleComponent]
    public sealed class ScoreHUD : MonoBehaviour
    {
        [SerializeField] private ScoreManager score;
        [SerializeField] private PlaneInput input;
        [SerializeField] private TMP_Text total, coins, feedback;
        [SerializeField] private GameObject content;
        [SerializeField, Min(.1f)] private float feedbackDuration = 1.5f;
        [SerializeField] private Vector2 panelSize = new Vector2(720f,156f);
        private float feedbackUntil;
        private string lastFeedback;
        private FlightPowerUps bonuses;
        private Vector3 feedbackScale = Vector3.one;
        private void OnEnable()
        {
            if (feedback != null) feedbackScale = feedback.rectTransform.localScale;
            if (score != null) { score.OnScoreChanged += Refresh; score.OnPointsAwarded += PointsAwarded; Refresh(score.Score); }
        }
        private void OnDisable()
        {
            if (feedback != null) feedback.rectTransform.localScale = feedbackScale;
            if (score != null) { score.OnScoreChanged -= Refresh; score.OnPointsAwarded -= PointsAwarded; }
        }
        private void Refresh(int value)
        {
            if (total != null) total.text = $"SCORE <size=125%>{value}</size>  <size=65%>RECORD {score.BestScore}</size>";
            if (coins != null) coins.text = $"PIÈCES <size=125%>{score.CoinsCollected}</size>";
        }
        private void PointsAwarded(int points, string reason)
        {
            if (feedback == null) return;
            lastFeedback = $"{points:+0;-0;0}  {reason}";
            feedbackUntil = Time.time + feedbackDuration;
        }
        private void Update()
        {
            if (content != null && content.transform is RectTransform panel && panel.parent is RectTransform safe)
                panel.sizeDelta = new Vector2(Mathf.Min(panelSize.x, safe.rect.width * .42f), panelSize.y);
            bool visible = input != null && input.IsMissionStarted && !input.IsPaused;
            if (content != null && content.activeSelf != visible) content.SetActive(visible);
            if (feedback == null || !visible) return;
            if (bonuses == null) bonuses = input.GetComponent<FlightPowerUps>();
            float magnet = bonuses != null ? bonuses.MagnetRemaining : 0f;
            float doublePoints = bonuses != null ? bonuses.DoublePointsRemaining : 0f;
            bool hasBonus = magnet > 0f || doublePoints > 0f;
            float sinceActivation = bonuses != null ? Time.time - bonuses.LastActivationTime : float.PositiveInfinity;
            if (hasBonus && sinceActivation < .7f)
            {
                bool isMagnet = bonuses.LastActivatedBonus == FlightBonusKind.Magnet;
                feedback.text = isMagnet ? "<color=#58E9FF>AIMANT ACTIVÉ</color>" : "<color=#E99AFF>POINTS ×2 ACTIVÉS</color>";
                float pop = Mathf.Sin(Mathf.Clamp01(sinceActivation / .7f) * Mathf.PI);
                feedback.rectTransform.localScale = feedbackScale * (1f + .16f * pop);
            }
            else if (hasBonus)
            {
                feedback.rectTransform.localScale = feedbackScale;
                string magnetLabel = magnet > 0f ? $"<color=#58E9FF>AIMANT {magnet:0.0} s</color>" : "";
                string doubleLabel = doublePoints > 0f ? $"<color=#E99AFF>POINTS ×2 {doublePoints:0.0} s</color>" : "";
                feedback.text = magnetLabel + (magnet > 0f && doublePoints > 0f ? "  ·  " : "") + doubleLabel;
            }
            else
            {
                feedback.rectTransform.localScale = feedbackScale;
                feedback.text = lastFeedback;
            }
            feedback.gameObject.SetActive(hasBonus || Time.time < feedbackUntil);
        }
#if UNITY_EDITOR
        public void Configure(ScoreManager owner, PlaneInput controls, TMP_Text scoreLabel, TMP_Text coinLabel,
            TMP_Text popup, GameObject panel)
        { score = owner; input = controls; total = scoreLabel; coins = coinLabel; feedback = popup; content = panel; }
#endif
    }
}
