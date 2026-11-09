using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using System;
using System.Collections;
using DriveSim.Race;
using DriveSim.User;

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
        public float extractionRadius = 15f;     // must stay within this to extract
        public float surfaceDepth = 50f;         // must ascend above this to win
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
        [SerializeField] private bool _needsAscent = false;
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
            CreateMenuButton();
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

        void CreateMenuButton()
        {
            var canvasGo = new GameObject("ReturnMenuCanvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();

            var btnGo = new GameObject("ReturnMenuButton");
            btnGo.transform.SetParent(canvasGo.transform, false);
            var img = btnGo.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.6f);
            var btn = btnGo.AddComponent<Button>();
            btn.onClick.AddListener(ReturnToMenu);
            var rt = btnGo.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(20f, 20f);
            rt.sizeDelta = new Vector2(160f, 60f);

            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(btnGo.transform, false);
            var txt = txtGo.AddComponent<Text>();
            txt.text = "Menu";
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 28;
            var trt = txtGo.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;
        }

        public void ReturnToMenu()
        {
            TransportSession.FinishFromSubmarine(false);
        }

        void Update()
        {
            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                TransportSession.FinishFromSubmarine(false);
                return;
            }

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

            // Ascent win condition after collecting the black box
            if (_needsAscent && _controller != null && _controller.Depth <= surfaceDepth)
            {
                _needsAscent = false;
                Win();
            }

            OnScoreChanged?.Invoke(_score);
        }

        void CheckBlackBoxProximity()
        {
            if (_blackBoxCollected || blackBoxLocation == null || _controller == null) return;

            float dist = Vector3.Distance(_controller.transform.position, blackBoxLocation.position);
            if (dist < extractionRadius && !_isExtracting)
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
            // Cancel extraction if the sub leaves the extraction radius.
            if (_controller != null && blackBoxLocation != null)
            {
                float dist = Vector3.Distance(_controller.transform.position, blackBoxLocation.position);
                if (dist > extractionRadius)
                {
                    _isExtracting = false;
                    _extractionProgress = 0f;
                    OnExtractionProgress?.Invoke(0f);
                    return;
                }
            }

            _extractionProgress += Time.deltaTime / extractionTime;
            OnExtractionProgress?.Invoke(_extractionProgress);

            // Extraction is loud: inject noise so the creature hunts the box.
            if (_sonar != null)
            {
                _sonar.AddDetection(0.02f * Time.deltaTime * 60f);
            }

            // Aggro boost if the creature is within the box's noise radius.
            if (_creature != null && blackBoxLocation != null)
            {
                float creatureDist = Vector3.Distance(_creature.transform.position, blackBoxLocation.position);
                if (creatureDist < blackBoxNoiseRadius && _sonar != null)
                {
                    _sonar.AddDetection(0.05f * Time.deltaTime * 60f);
                }
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

            // Win condition: ascend to the surface with the box.
            _needsAscent = true;
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
            _needsAscent = false;
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
            StartCoroutine(SubmitScore());
        }

        [Serializable]
        class ScoreBody { public float score; public float maxDepth; public float playTime; public int cores; }

        IEnumerator SubmitScore()
        {
            if (!AuthSession.IsSignedIn) yield break;
            var url = AuthConfig.DefaultServerUrl.Trim().TrimEnd('/') + "/scores";
            var body = JsonUtility.ToJson(new ScoreBody
            {
                score = _score,
                maxDepth = _maxDepthReached,
                playTime = _playTime,
                cores = 0
            });
            var bytes = System.Text.Encoding.UTF8.GetBytes(body);
            using var req = new UnityWebRequest(url, "POST");
            req.uploadHandler = new UploadHandlerRaw(bytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.timeout = 12;
            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", "Bearer " + AuthSession.AccessToken);
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning("Score submission failed: " + req.error);
            }
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
