using UnityEngine;
using System;
using System.Collections;

namespace DriveSim.Submarine
{
    /// <summary>
    /// Game state manager for "The Silent Descent."
    /// Handles objectives, win/lose conditions, scoring.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public enum GameState { MainMenu, Playing, Paused, Won, Lost, GameOver }

        [Header("Objective")]
        public Transform blackBoxLocation;
        public float extractionTime = 30f;       // seconds to extract black box
        public float blackBoxNoiseRadius = 50f;  // creature aggro radius during extraction

        [Header("Scoring")]
        public int scorePerDepth = 10;
        public int scorePerSurvival = 50;
        public int blackBoxBonus = 5000;
        public int noDamageBonus = 2000;
        public int speedBonus = 1000;

        [Header("State")]
        [SerializeField] private GameState _state = GameState.MainMenu;
        [SerializeField] private float _score = 0f;
        [SerializeField] private float _playTime = 0f;
        [SerializeField] private bool _blackBoxCollected = false;
        [SerializeField] private bool _isExtracting = false;
        [SerializeField] private float _extractionProgress = 0f;
        [SerializeField] private float _maxDepthReached = 0f;

        public GameState State => _state;
        public float Score => _score;
        public float PlayTime => _playTime;
        public bool BlackBoxCollected => _blackBoxCollected;
        public bool IsExtracting => _isExtracting;
        public float ExtractionProgress => _extractionProgress;
        public float MaxDepthReached => _maxDepthReached;

        private SubmarineController _controller;
        private HullSystem _hull;
        private OxygenSystem _oxygen;
        private BatterySystem _battery;
        private SonarSystem _sonar;
        private LeviathanAI _creature;

        public event Action<GameState> OnStateChanged;
        public event Action<float> OnScoreChanged;
        public event Action<float> OnExtractionProgress;
        public event Action OnBlackBoxCollected;
        public event Action<string> OnGameOverReason;

        void Awake()
        {
            _controller = FindFirstObjectByType<SubmarineController>();
            _hull = FindFirstObjectByType<HullSystem>();
            _oxygen = FindFirstObjectByType<OxygenSystem>();
            _battery = FindFirstObjectByType<BatterySystem>();
            _sonar = FindFirstObjectByType<SonarSystem>();
            _creature = FindFirstObjectByType<LeviathanAI>();
        }

        void Start()
        {
            // Subscribe to failure events
            if (_hull != null)
            {
                _hull.OnHullBreached += () => GameOver("Coque rompue. L'océan vous garde.");
            }
            if (_oxygen != null)
            {
                _oxygen.OnOxygenDepleted += () => GameOver("Oxygène épuisé. Silence.");
            }
            if (_battery != null)
            {
                _battery.OnBatteryDepleted += () => GameOver("Énergie perdue. Les ténèbres.");
            }
        }

        void Update()
        {
            if (_state != GameState.Playing) return;

            _playTime += Time.deltaTime;

            // Track max depth
            if (_controller != null)
            {
                _maxDepthReached = Mathf.Max(_maxDepthReached, _controller.Depth);
                _score += scorePerDepth * Time.deltaTime * (_controller.Depth / 1000f);
            }

            // Survival score
            _score += scorePerSurvival * Time.deltaTime;

            // Extraction logic
            if (_isExtracting)
            {
                UpdateExtraction();
            }

            // Check black box proximity
            CheckBlackBoxProximity();

            OnScoreChanged?.Invoke(_score);
        }

        void CheckBlackBoxProximity()
        {
            if (_blackBoxCollected || blackBoxLocation == null || _controller == null) return;

            float dist = Vector3.Distance(_controller.transform.position, blackBoxLocation.position);
            if (dist < 15f && !_isExtracting)
            {
                StartExtraction();
            }
        }

        void StartExtraction()
        {
            _isExtracting = true;
            _extractionProgress = 0f;
        }

        void UpdateExtraction()
        {
            _extractionProgress += Time.deltaTime / extractionTime;
            OnExtractionProgress?.Invoke(_extractionProgress);

            // Extraction increases noise (creature aggro)
            if (_sonar != null)
            {
                // Handled by sonar system
            }

            if (_extractionProgress >= 1f)
            {
                CollectBlackBox();
            }
        }

        void CollectBlackBox()
        {
            _isExtracting = false;
            _blackBoxCollected = true;
            _score += blackBoxBonus;

            // Bonus for no damage
            if (_hull != null && _hull.HullPercent > 0.9f)
            {
                _score += noDamageBonus;
            }

            // Speed bonus
            if (_playTime < 300f)
            {
                _score += speedBonus;
            }

            OnBlackBoxCollected?.Invoke();

            // Win condition
            StartCoroutine(WinAfterDelay(3f));
        }

        IEnumerator WinAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            Win();
        }

        public void StartGame()
        {
            _state = GameState.Playing;
            _score = 0f;
            _playTime = 0f;
            _blackBoxCollected = false;
            _isExtracting = false;
            _extractionProgress = 0f;
            _maxDepthReached = 0f;

            // Reset systems
            if (_hull != null) { _hull.currentHull = _hull.maxHull; }
            if (_oxygen != null) { _oxygen.RestoreOxygen(_oxygen.maxOxygen); }
            if (_battery != null) { _battery.currentBattery = _battery.maxBattery; }

            OnStateChanged?.Invoke(_state);
        }

        public void PauseGame()
        {
            if (_state == GameState.Playing)
            {
                _state = GameState.Paused;
                Time.timeScale = 0f;
                OnStateChanged?.Invoke(_state);
            }
        }

        public void ResumeGame()
        {
            if (_state == GameState.Paused)
            {
                _state = GameState.Playing;
                Time.timeScale = 1f;
                OnStateChanged?.Invoke(_state);
            }
        }

        public void Win()
        {
            _state = GameState.Won;
            OnStateChanged?.Invoke(_state);
        }

        public void GameOver(string reason)
        {
            if (_state == GameState.Lost || _state == GameState.Won) return;
            _state = GameState.Lost;
            OnGameOverReason?.Invoke(reason);
            OnStateChanged?.Invoke(_state);
        }

        public void Restart()
        {
            // Reload scene or reset
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }
    }
}
