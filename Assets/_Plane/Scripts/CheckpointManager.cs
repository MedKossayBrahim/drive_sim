using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Project.Plane
{
    /// <summary>Gère une liste ordonnée, le chronomètre et le score du parcours.</summary>
    [DisallowMultipleComponent]
    public sealed class CheckpointManager : MonoBehaviour
    {
        [SerializeField] private List<Checkpoint> checkpoints = new List<Checkpoint>();
        [SerializeField, Tooltip("Optionnel : avion autorisé. Sinon, chaque checkpoint filtre le layer du Rigidbody.")]
        private Rigidbody aircraft = null;
        [SerializeField, Min(0f), Tooltip("0 = chronomètre sans limite.")] private float timeLimit = 180f;
        [SerializeField, Min(0)] private int scorePerCheckpoint = 100;
        [SerializeField] private bool autoStart = true;
        [SerializeField] private UnityEvent onAllPassed = new UnityEvent();
        [SerializeField] private UnityEvent onTimeExpired = new UnityEvent();
        private readonly HashSet<Checkpoint> subscribed = new HashSet<Checkpoint>();
        private bool started;
        public event Action OnAllPassed;
        public int Score { get; private set; }
        public int PassedCount { get; private set; }
        public float ElapsedTime { get; private set; }
        public float RemainingTime => timeLimit > 0f ? Mathf.Max(0f, timeLimit - ElapsedTime) : 0f;
        public bool IsRunning { get; private set; }
        public bool IsComplete => checkpoints.Count > 0 && PassedCount == checkpoints.Count;
        public IReadOnlyList<Checkpoint> Checkpoints => checkpoints;

        private void OnEnable()
        {
            Subscribe();
            if (started && autoStart) RestartCourse();
        }
        private void Start() { started = true; if (autoStart) RestartCourse(); }
        private void OnDisable() { Unsubscribe(); IsRunning = false; }

        private void Subscribe()
        {
            foreach (var checkpoint in checkpoints)
                if (checkpoint != null && subscribed.Add(checkpoint)) checkpoint.OnPassed += HandlePassed;
        }
        private void Unsubscribe()
        {
            foreach (var checkpoint in subscribed)
                if (checkpoint != null) checkpoint.OnPassed -= HandlePassed;
            subscribed.Clear();
        }

        public void RestartCourse()
        {
            if (!Application.isPlaying) return;
            IsRunning = false;
            var seen = new HashSet<Checkpoint>();
            foreach (var checkpoint in checkpoints)
            {
                if (checkpoint == null || !seen.Add(checkpoint))
                {
                    Debug.LogError("[Sky Courier] Parcours invalide : checkpoint absent ou dupliqué.", this);
                    return;
                }
            }
            if (checkpoints.Count == 0) return;
            Unsubscribe();
            Subscribe();
            PassedCount = 0;
            Score = 0;
            ElapsedTime = 0f;
            foreach (var checkpoint in checkpoints)
            {
                checkpoint.SetRequiredBody(aircraft);
                checkpoint.SetState(CheckpointState.Inactive);
            }
            checkpoints[0].SetState(CheckpointState.Active);
            IsRunning = true;
        }

        private void Update()
        {
            if (!IsRunning) return;
            ElapsedTime += Time.deltaTime;
            if (timeLimit > 0f && ElapsedTime >= timeLimit)
            {
                ElapsedTime = timeLimit;
                IsRunning = false;
                if (PassedCount < checkpoints.Count) checkpoints[PassedCount].SetState(CheckpointState.Inactive);
                onTimeExpired.Invoke();
            }
        }

        private void HandlePassed(Checkpoint checkpoint)
        {
            if (!IsRunning || PassedCount >= checkpoints.Count || checkpoints[PassedCount] != checkpoint) return;
            Score += scorePerCheckpoint;
            PassedCount++;
            if (PassedCount < checkpoints.Count) checkpoints[PassedCount].SetState(CheckpointState.Active);
            else
            {
                IsRunning = false;
                OnAllPassed?.Invoke();
                onAllPassed.Invoke();
            }
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.79f, 0.08f, 0.8f);
            for (int i = 1; i < checkpoints.Count; i++)
                if (checkpoints[i - 1] != null && checkpoints[i] != null)
                    Gizmos.DrawLine(checkpoints[i - 1].transform.position, checkpoints[i].transform.position);
        }

#if UNITY_EDITOR
        public void Configure(List<Checkpoint> ordered)
        {
            checkpoints = ordered;
            for (int i = 0; i < checkpoints.Count; i++)
                checkpoints[i].SetState(i == 0 ? CheckpointState.Active : CheckpointState.Inactive);
        }
#endif
    }
}
