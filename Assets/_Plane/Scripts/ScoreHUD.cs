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
        private void OnEnable()
        {
            if (score != null) { score.OnScoreChanged += Refresh; score.OnPointsAwarded += PointsAwarded; Refresh(score.Score); }
        }
        private void OnDisable()
        {
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
            feedback.text = $"{points:+0;-0;0}  {reason}";
            feedbackUntil = Time.time + feedbackDuration;
        }
        private void Update()
        {
            if (content != null && content.transform is RectTransform panel && panel.parent is RectTransform safe)
                panel.sizeDelta = new Vector2(Mathf.Min(panelSize.x, safe.rect.width * .42f), panelSize.y);
            bool visible = input != null && input.IsMissionStarted && !input.IsPaused;
            if (content != null && content.activeSelf != visible) content.SetActive(visible);
            if (feedback != null) feedback.gameObject.SetActive(visible && Time.time < feedbackUntil);
        }
#if UNITY_EDITOR
        public void Configure(ScoreManager owner, PlaneInput controls, TMP_Text scoreLabel, TMP_Text coinLabel,
            TMP_Text popup, GameObject panel)
        { score = owner; input = controls; total = scoreLabel; coins = coinLabel; feedback = popup; content = panel; }
#endif
    }
}
