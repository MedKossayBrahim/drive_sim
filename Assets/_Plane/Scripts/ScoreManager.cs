using System;
using UnityEngine;

namespace Project.Plane
{
    /// <summary>Score global indépendant du score interne du parcours ; une nouvelle scène crée une nouvelle partie.</summary>
    [DefaultExecutionOrder(50), DisallowMultipleComponent]
    public sealed class ScoreManager : MonoBehaviour
    {
        private const string BestKey = "SkyCourier.BestScore";
        [SerializeField] private CheckpointManager checkpoints;
        [SerializeField] private PlaneHealth aircraftHealth;
        [SerializeField, Min(0)] private int pointsPerRing = 100;
        [SerializeField, Min(0)] private int timeBonusPerSecond = 5;
        [SerializeField, Min(0)] private int crashPenalty = 0;
        private int observedPassed;
        private bool completionAwarded;
        public int Score { get; private set; }
        public int CoinsCollected { get; private set; }
        public int CoinPoints { get; private set; }
        public int BestScore { get; private set; }
        public event Action<int> OnScoreChanged;
        public event Action<int, string> OnPointsAwarded;

        private void Awake() => BestScore = Mathf.Max(0, PlayerPrefs.GetInt(BestKey, 0));
        private void OnEnable()
        {
            if (checkpoints != null) checkpoints.OnAllPassed += CourseCompleted;
            if (aircraftHealth != null) aircraftHealth.OnCrashed += Crashed;
        }
        private void OnDisable()
        {
            if (checkpoints != null) checkpoints.OnAllPassed -= CourseCompleted;
            if (aircraftHealth != null) aircraftHealth.OnCrashed -= Crashed;
            PlayerPrefs.Save();
        }
        private void Update() => SynchronizeRings();
        private void SynchronizeRings()
        {
            if (checkpoints == null) return;
            // Le manager n'expose pas d'événement de progression. Lire son compteur public évite de modifier son API.
            int passed = checkpoints.PassedCount;
            if (passed > observedPassed)
            {
                int count = passed - observedPassed;
                observedPassed = passed;
                AddScore((int)Math.Min(int.MaxValue, (long)count * pointsPerRing), "ANNEAU");
            }
            // Une désactivation/réactivation ne permet pas de repointer les mêmes anneaux.
            if (checkpoints.IsComplete) CourseCompleted();
        }
        private void CourseCompleted()
        {
            if (checkpoints == null || completionAwarded) return;
            // Le dernier anneau et le bonus sont attribués dans cet ordre, même avant le prochain Update.
            int missing = Mathf.Max(0, checkpoints.PassedCount - observedPassed);
            observedPassed = Mathf.Max(observedPassed, checkpoints.PassedCount);
            completionAwarded = true;
            if (missing > 0) AddScore((int)Math.Min(int.MaxValue, (long)missing * pointsPerRing), "ANNEAU");
            int bonus = (int)Math.Min(int.MaxValue, Math.Floor(checkpoints.RemainingTime) * timeBonusPerSecond);
            if (bonus > 0) AddScore(bonus, "BONUS TEMPS");
        }
        public void AddScore(int amount, string reason)
        {
            int previous = Score;
            Score = (int)Math.Max(0L, Math.Min(int.MaxValue, (long)Score + amount));
            if (Score > BestScore)
            {
                BestScore = Score;
                PlayerPrefs.SetInt(BestKey, BestScore);
            }
            OnScoreChanged?.Invoke(Score);
            if (Score != previous) OnPointsAwarded?.Invoke(Score - previous, reason ?? string.Empty);
        }
        public void CollectCoin(int value)
        {
            if (CoinsCollected < int.MaxValue) CoinsCollected++;
            int previousScore = Score;
            AddScore(Mathf.Max(0, value), "PIÈCE");
            CoinPoints += Score - previousScore;
        }
        private void Crashed() { if (crashPenalty > 0) AddScore(-crashPenalty, "CRASH"); }
        private void OnApplicationPause(bool paused) { if (paused) PlayerPrefs.Save(); }
#if UNITY_EDITOR
        public void Configure(CheckpointManager course, PlaneHealth health)
        { checkpoints = course; aircraftHealth = health; }
#endif
    }
}
