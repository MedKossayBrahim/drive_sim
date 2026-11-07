using System;
using UnityEngine;

namespace Project.Plane
{
    public enum FlightMissionState { Collecting, Approach, Won, Failed }

    /// <summary>Collecter puis atterrir. L'échec est terminal dans cette mission, sans modifier PlaneHealth.</summary>
    [DefaultExecutionOrder(600), DisallowMultipleComponent]
    public sealed class LandingMission : MonoBehaviour
    {
        [SerializeField] private FlightMissionSettings settings;
        [SerializeField] private ScoreManager score;
        [SerializeField] private PlaneInput input;
        [SerializeField] private PlaneHealth health;
        [SerializeField] private PlaneSensors sensors;
        [SerializeField] private CheckpointManager checkpoints;
        [SerializeField] private CollectibleSpawner collectibles;
        [SerializeField] private LandingZone arrival;
        [SerializeField, Min(.1f)] private float guidanceRefreshSeconds = .35f;
        private Rigidbody body;
        private bool freezeRequested;
        private Collectible guidanceCoin;
        private float nextGuidanceRefresh;
        public FlightMissionState State { get; private set; }
        public bool HasTakenOff { get; private set; }
        public bool IsTerminal=>State==FlightMissionState.Won||State==FlightMissionState.Failed;
        public bool CanLand=>State==FlightMissionState.Approach&&HasTakenOff;
        internal bool NeedsCoins => State == FlightMissionState.Collecting && score != null && settings != null
            && score.CoinsCollected < settings.RequiredCoins;
        public float EndedAt { get; private set; }
        public int LandingPoints { get; private set; }
        public float FlightSeconds { get; private set; }
        public int RingsPassed => checkpoints != null ? checkpoints.PassedCount : 0;
        public int RingCount => checkpoints != null ? checkpoints.Checkpoints.Count : 0;
        public FlightMissionSettings Settings=>settings;
        public ScoreManager Score=>score;
        public PlaneInput Input=>input;
        public LandingZone Arrival=>arrival;
        public event Action<FlightMissionState> OnStateChanged;
        private void Awake(){if(input!=null)body=input.GetComponent<Rigidbody>();}
        private void OnEnable()
        {
            State=FlightMissionState.Collecting;HasTakenOff=false;EndedAt=0f;LandingPoints=0;freezeRequested=false;
            FlightSeconds = 0f;
            guidanceCoin = null;
            nextGuidanceRefresh = 0f;
            if(score!=null)score.OnScoreChanged+=ScoreChanged;
            if(health!=null)health.OnCrashed+=Crashed;
        }
        private void OnDisable()
        {
            if(score!=null)score.OnScoreChanged-=ScoreChanged;
            if(health!=null)health.OnCrashed-=Crashed;
        }
        private void ScoreChanged(int _) => RefreshObjective();
        private void RefreshObjective()
        {
            if(IsTerminal||settings==null||score==null)return;
            bool ready=HasTakenOff&&score.CoinsCollected>=settings.RequiredCoins&&
                (!settings.RequireAllRings||checkpoints!=null&&checkpoints.IsComplete);
            SetState(ready?FlightMissionState.Approach:FlightMissionState.Collecting);
        }
        private void FixedUpdate()
        {
            if(freezeRequested&&body!=null)
            {
                if(!body.isKinematic){body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;body.isKinematic=true;}
                freezeRequested=false;
            }
            if(IsTerminal||input==null||sensors==null||!input.IsMissionStarted)return;
            if(!HasTakenOff&&sensors.HasGroundAltitude&&sensors.AltitudeAGL>10f)HasTakenOff=true;
            if (HasTakenOff) FlightSeconds += Time.fixedDeltaTime;
            RefreshObjective();
        }

        internal bool TryGetCoinTarget(out Vector3 target)
        {
            target = default;
            if (!NeedsCoins || collectibles == null || input == null) return false;
            if (Time.time >= nextGuidanceRefresh || (guidanceCoin != null && guidanceCoin.IsCollected))
            {
                guidanceCoin = collectibles.FindGuidanceCoin(input.transform.position, input.transform.forward);
                nextGuidanceRefresh = Time.time + guidanceRefreshSeconds;
            }
            if (guidanceCoin == null || !guidanceCoin.isActiveAndEnabled) return false;
            target = guidanceCoin.transform.position;
            return true;
        }
        public bool CompleteLanding()
        {
            if(!CanLand||health==null||health.IsCrashed||arrival==null||!arrival.IsGroundedOnRunway||arrival.LandingProgress<1f)return false;
            End(FlightMissionState.Won);
            int previousScore = score.Score;
            score.AddScore(settings.LandingBonus,"ATTERRISSAGE RÉUSSI");
            LandingPoints = score.Score - previousScore;
            return true;
        }
        private void Crashed()
        {
            if(IsTerminal)return;
            // Les seules coroutines de PlaneHealth servent au respawn. L'explosion existe déjà et continue seule.
            // Arrêter ce respawn pour laisser au joueur le choix explicite Recommencer, sans éditer PlaneHealth.
            health.StopAllCoroutines();
            End(FlightMissionState.Failed);
        }
        private void End(FlightMissionState result)
        {
            EndedAt=Time.unscaledTime;SetState(result);freezeRequested=true;
            if(input!=null){input.enabled=false;if(input.Controller!=null)input.Controller.enabled=false;}
            if(checkpoints!=null)checkpoints.enabled=false;
            if(collectibles!=null)collectibles.gameObject.SetActive(false);
        }
        private void SetState(FlightMissionState value)
        {if(State==value)return;State=value;OnStateChanged?.Invoke(value);}
#if UNITY_EDITOR
        public void Configure(FlightMissionSettings config,ScoreManager points,PlaneInput controls,CheckpointManager course,
            CollectibleSpawner coins,LandingZone zone)
        {
            settings=config;score=points;input=controls;checkpoints=course;collectibles=coins;arrival=zone;
            health=controls.GetComponent<PlaneHealth>();sensors=controls.GetComponent<PlaneSensors>();
        }
#endif
    }
}
