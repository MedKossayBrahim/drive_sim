using DriveSim.Race;
using UnityEngine;
using UnityEngine.UI;

public sealed class BoatGameManager : MonoBehaviour
{
    const string BestDistanceKey = "BoatRunner.BestDistance";

    [SerializeField] BoatController boat;
    [SerializeField] ObstacleSpawner spawner;
    [SerializeField] RouteTokenSpawner tokenSpawner;
    [SerializeField] Text scoreText;
    [SerializeField] Text distanceText;
    [SerializeField] Text livesText;
    [SerializeField] Text speedText;
    [SerializeField] Text tokenText;
    [SerializeField] Text bestText;
    [SerializeField] Text powerUpsText;
    [SerializeField] GameObject startPanel;
    [SerializeField] GameObject gameOverPanel;
    [SerializeField] Text finalScoreText;
    [SerializeField] int startingLives = 3;
    [SerializeField] int tokenScore = 100;
    [SerializeField] float invincibilityDuration = 1.65f;
    [SerializeField] float shieldDuration = 18f;
    [SerializeField] float magnetDuration = 8f;
    [SerializeField] float doubleScoreDuration = 10f;
    [SerializeField] float turboDuration = 5f;
    [SerializeField] float magnetRadius = 11.5f;
    [SerializeField] float magnetPullSpeed = 9.5f;
    [SerializeField] BoatDifficultyConfig difficultySettings = new BoatDifficultyConfig();

    int lives;
    int tokensCollected;
    int bestDistance;
    int score;
    int distanceScored;
    float shieldUntil;
    float magnetUntil;
    float doubleScoreUntil;
    float turboUntil;
    float startZ;
    float furthestZ;
    float invincibleUntil;
    float runStartedAt;
    float difficultyValue;
    bool gameStarted;
    bool gameOver;
    bool paused;
    bool launchedFromHub;
    BoatRuntimeUI runtimeUI;
    BoatSoundEffects soundEffects;
#if UNITY_EDITOR
    float editorDifficultyOverride = -1f;
    bool editorInvincibleOverride;
#endif

    public bool IsGameOver => gameOver;
    public bool IsPlaying => gameStarted && !gameOver;
    public bool IsPaused => paused;
    public float Difficulty
    {
        get
        {
#if UNITY_EDITOR
            if (editorDifficultyOverride >= 0f) return editorDifficultyOverride;
#endif
            return difficultyValue;
        }
    }
    public BoatDifficultyConfig DifficultySettings => difficultySettings;
    public float CruiseSpeed => Mathf.Lerp(difficultySettings.cruiseSpeedStart, difficultySettings.cruiseSpeedMax, Difficulty);
    public float BoostedSpeed => Mathf.Lerp(difficultySettings.boostedSpeedStart, difficultySettings.boostedSpeedMax, Difficulty);
    public float SpawnDistance => Mathf.Lerp(difficultySettings.spawnDistanceStart, difficultySettings.spawnDistanceMax, Difficulty);
    public bool HasShield => Time.time < shieldUntil;
    public bool HasMagnet => Time.time < magnetUntil;
    public bool HasDoubleScore => Time.time < doubleScoreUntil;
    public bool HasTurbo => Time.time < turboUntil;
    public bool IsInvincible
    {
        get
        {
#if UNITY_EDITOR
            if (editorInvincibleOverride) return true;
#endif
            return Time.time < invincibleUntil || HasTurbo;
        }
    }
    public int ScoreMultiplier => HasDoubleScore ? 2 : 1;
    public float MagnetRadius => magnetRadius;
    public float MagnetPullSpeed => magnetPullSpeed;
    public int Lives => lives;
    public int TokensCollected => tokensCollected;
    public int Distance => boat == null ? 0 : Mathf.Max(0, Mathf.FloorToInt(furthestZ - startZ));
    public int Score => score;
    public int BestDistance => bestDistance;
    public bool LaunchedFromHub => launchedFromHub;

    public void Initialize(BoatController player, Text score, Text livesLabel, GameObject panel, Text finalScore,
        ObstacleSpawner obstacleSpawner, GameObject titlePanel = null, Text bestLabel = null,
        Text distanceLabel = null, Text tokenLabel = null, Text speedLabel = null, RouteTokenSpawner routeTokenSpawner = null)
    {
        boat = player;
        scoreText = score;
        livesText = livesLabel;
        gameOverPanel = panel;
        finalScoreText = finalScore;
        spawner = obstacleSpawner;
        startPanel = titlePanel;
        bestText = bestLabel;
        distanceText = distanceLabel;
        tokenText = tokenLabel;
        speedText = speedLabel;
        tokenSpawner = routeTokenSpawner;
    }

    void Awake()
    {
        BoatRenderSetup.Apply();
    }

    void Start()
    {
        Time.timeScale = 1f;
        if (boat == null) boat = FindFirstObjectByType<BoatController>();
        if (spawner == null) spawner = FindFirstObjectByType<ObstacleSpawner>();
        if (tokenSpawner == null) tokenSpawner = FindFirstObjectByType<RouteTokenSpawner>();
        if (scoreText == null) scoreText = GameObject.Find("Distance")?.GetComponent<Text>();
        if (distanceText == null) distanceText = GameObject.Find("Distance Value")?.GetComponent<Text>();
        if (livesText == null) livesText = GameObject.Find("Lives")?.GetComponent<Text>();
        if (speedText == null) speedText = GameObject.Find("Speed")?.GetComponent<Text>();
        if (tokenText == null) tokenText = GameObject.Find("Token Count")?.GetComponent<Text>();
        if (powerUpsText == null) powerUpsText = GameObject.Find("Power Ups")?.GetComponent<Text>();
        if (bestText == null) bestText = GameObject.Find("Best Distance")?.GetComponent<Text>();
        if (startPanel == null) startPanel = GameObject.Find("Start Panel");
        if (gameOverPanel == null) gameOverPanel = GameObject.Find("Game Over Panel");
        if (finalScoreText == null) finalScoreText = GameObject.Find("Final Distance")?.GetComponent<Text>();

        ApplyMobileSettings();
        runtimeUI = GetComponent<BoatRuntimeUI>();
        if (runtimeUI == null) runtimeUI = gameObject.AddComponent<BoatRuntimeUI>();
        soundEffects = GetComponent<BoatSoundEffects>();
        if (soundEffects == null) soundEffects = gameObject.AddComponent<BoatSoundEffects>();

        bestDistance = Mathf.Max(0, PlayerPrefs.GetInt(BestDistanceKey, 0));
        ShowStartScreen();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P)) TogglePause();
        if (Input.GetKeyDown(KeyCode.R))
        {
            if (gameOver) Restart();
            else if (!gameStarted) StartGame();
        }

        if (!IsPlaying || paused || boat == null)
        {
            runtimeUI?.RefreshPowerUps();
            return;
        }
        difficultyValue = Mathf.Clamp01((Time.time - runStartedAt) / Mathf.Max(1f, difficultySettings.rampDurationSeconds));
        furthestZ = Mathf.Max(furthestZ, boat.transform.position.z);
        int currentDistance = Distance;
        if (currentDistance > distanceScored)
        {
            score += (currentDistance - distanceScored) * ScoreMultiplier;
            distanceScored = currentDistance;
        }
        boat.SetTurboActive(HasTurbo);
        boat.SetShieldVisible(HasShield);
        boat.SetPowerUpVisuals(HasMagnet, HasDoubleScore, HasTurbo);
        RefreshHud();
        boat.SetInvulnerableFlash(IsInvincible);
    }

    public void PlayFromHub()
    {
        launchedFromHub = true;
        StartGame();
    }

    public void LeaveToHub()
    {
        Time.timeScale = 1f;
        paused = false;
        if (launchedFromHub && TransportSession.Current != null)
        {
            TransportSession.FinishFromBoat(false);
            return;
        }
        ShowStartScreen();
    }

    public void StartGame()
    {
        Time.timeScale = 1f;
        paused = false;
        if (boat == null) boat = FindFirstObjectByType<BoatController>();
        if (boat == null) return;

        ClearPowerUps();
        lives = startingLives;
        tokensCollected = 0;
        score = 0;
        distanceScored = 0;
        gameOver = false;
        gameStarted = true;
        runStartedAt = Time.time;
        difficultyValue = 0f;
#if UNITY_EDITOR
        editorDifficultyOverride = -1f;
#endif
        invincibleUntil = 0f;
        startZ = boat.transform.position.z;
        furthestZ = startZ;
        boat.SetControlsEnabled(true);
        boat.SetInvulnerableFlash(false);
        if (startPanel != null) startPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        runtimeUI?.SetPaused(false);
        runtimeUI?.SetPauseButtonVisible(true);
        if (spawner == null) spawner = FindFirstObjectByType<ObstacleSpawner>();
        if (spawner != null) spawner.ResetSpawner();
        if (tokenSpawner == null) tokenSpawner = FindFirstObjectByType<RouteTokenSpawner>();
        if (tokenSpawner != null) tokenSpawner.ResetSpawner();
        RefreshHud();
    }

    public void HitObstacle()
    {
        if (!IsPlaying || IsInvincible) return;
        TriggerHitFeedback();
        if (HasShield)
        {
            shieldUntil = 0f;
            invincibleUntil = Time.time + invincibilityDuration;
            Handheld.Vibrate();
            if (boat != null) boat.SetShieldVisible(false);
            RefreshHud();
            return;
        }

        lives = Mathf.Max(0, lives - 1);
        invincibleUntil = Time.time + invincibilityDuration;
        Handheld.Vibrate();
        RefreshHud();
        if (lives == 0) EndGame();
    }

    public void CollectToken(int amount = 1)
    {
        if (!IsPlaying || amount <= 0) return;
        tokensCollected += amount;
        score += tokenScore * amount * ScoreMultiplier;
        soundEffects?.PlayCoin();
        RefreshHud();
    }

    public void ActivatePowerUp(BoatPowerUpType type)
    {
        if (!IsPlaying) return;
        soundEffects?.PlayPowerUp();
        float expiry = Time.time + GetPowerUpDuration(type);
        switch (type)
        {
            case BoatPowerUpType.Shield: shieldUntil = expiry; if (boat != null) boat.SetShieldVisible(true); break;
            case BoatPowerUpType.Magnet: magnetUntil = expiry; break;
            case BoatPowerUpType.DoubleScore: doubleScoreUntil = expiry; break;
            case BoatPowerUpType.Turbo: turboUntil = expiry; if (boat != null) boat.SetTurboActive(true); break;
        }
        RefreshHud();
    }

    public float GetPowerUpRemaining(BoatPowerUpType type)
    {
        float until = type switch
        {
            BoatPowerUpType.Shield => shieldUntil,
            BoatPowerUpType.Magnet => magnetUntil,
            BoatPowerUpType.DoubleScore => doubleScoreUntil,
            BoatPowerUpType.Turbo => turboUntil,
            _ => 0f
        };
        return Mathf.Max(0f, until - Time.time);
    }

    public float GetPowerUpDuration(BoatPowerUpType type)
    {
        switch (type)
        {
            case BoatPowerUpType.Shield: return shieldDuration;
            case BoatPowerUpType.Magnet: return magnetDuration;
            case BoatPowerUpType.DoubleScore: return doubleScoreDuration;
            case BoatPowerUpType.Turbo: return turboDuration;
            default: return 0f;
        }
    }
    public void Restart()
    {
        foreach (var obstacle in FindObjectsByType<Obstacle>(FindObjectsSortMode.None))
            Destroy(obstacle.gameObject);
        foreach (var token in FindObjectsByType<RouteToken>(FindObjectsSortMode.None))
            Destroy(token.gameObject);
        foreach (var powerUp in FindObjectsByType<PowerUpPickup>(FindObjectsSortMode.None))
            Destroy(powerUp.gameObject);
        if (boat != null) boat.ResetBoat();
        StartGame();
    }

    public void TogglePause()
    {
        if (!IsPlaying) return;
        if (paused) ResumeGame();
        else PauseGame();
    }

    public void PauseGame()
    {
        if (!IsPlaying || paused) return;
        paused = true;
        if (boat != null) boat.SetControlsEnabled(false);
        Time.timeScale = 0f;
        runtimeUI?.SetPaused(true);
    }

    public void ResumeGame()
    {
        if (!paused) return;
        Time.timeScale = 1f;
        paused = false;
        if (boat != null) boat.SetControlsEnabled(true);
        runtimeUI?.SetPaused(false);
    }

#if UNITY_EDITOR
    public void ForceDifficultyForEditorTest(float value) => editorDifficultyOverride = Mathf.Clamp01(value);
    public void ClearDifficultyEditorTest() => editorDifficultyOverride = -1f;
    public void SetInvincibleForEditorTest(bool value) => editorInvincibleOverride = value;
#endif

    void ShowStartScreen()
    {
        Time.timeScale = 1f;
        paused = false;
        ClearPowerUps();
        gameStarted = false;
        gameOver = false;
        lives = startingLives;
        tokensCollected = 0;
        score = 0;
        distanceScored = 0;
        invincibleUntil = 0f;
        if (boat != null)
        {
            startZ = boat.transform.position.z;
            furthestZ = startZ;
            boat.SetControlsEnabled(false);
            boat.SetInvulnerableFlash(false);
        }
        if (startPanel != null) startPanel.SetActive(true);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        runtimeUI?.SetPaused(false);
        runtimeUI?.SetPauseButtonVisible(false);
        if (spawner == null) spawner = FindFirstObjectByType<ObstacleSpawner>();
        if (spawner != null) spawner.ResetSpawner();
        if (tokenSpawner == null) tokenSpawner = FindFirstObjectByType<RouteTokenSpawner>();
        if (tokenSpawner != null) tokenSpawner.ResetSpawner();
        RefreshHud();
    }

    void RefreshHud()
    {
        if (scoreText != null) scoreText.text = "SCORE  " + Score.ToString("0000");
        if (distanceText != null) distanceText.text = "DISTANCE  " + Distance.ToString("0000") + " m";
        if (livesText != null) livesText.text = "LIVES  " + lives;
        if (speedText != null)
            speedText.text = IsPlaying && boat != null
                ? "SPEED  " + Mathf.RoundToInt(boat.CurrentSpeed * 3.6f) + " km/h"
                : "SPEED  0 km/h";
        if (tokenText != null) tokenText.text = "TOKENS  " + tokensCollected;
        if (bestText != null) bestText.text = "BEST  " + bestDistance.ToString("0000") + " m";
        RefreshPowerUpHud();
    }

    void RefreshPowerUpHud()
    {
        runtimeUI?.RefreshPowerUps();
    }

    void ClearPowerUps()
    {
        shieldUntil = magnetUntil = doubleScoreUntil = turboUntil = 0f;
        if (boat != null)
        {
            boat.SetShieldVisible(false);
            boat.SetTurboActive(false);
            boat.SetPowerUpVisuals(false, false, false);
        }
        runtimeUI?.RefreshPowerUps();
    }

    void EndGame()
    {
        Time.timeScale = 1f;
        paused = false;
        gameOver = true;
        gameStarted = false;
        ClearPowerUps();
        if (boat != null) boat.SetControlsEnabled(false);
        if (Distance > bestDistance)
        {
            bestDistance = Distance;
            PlayerPrefs.SetInt(BestDistanceKey, bestDistance);
            PlayerPrefs.Save();
        }
        if (launchedFromHub && TransportSession.Current != null)
        {
            TransportSession.FinishFromBoat(true);
            return;
        }
        if (finalScoreText != null)
        {
            finalScoreText.text = "SCORE  " + Score.ToString("0000") +
                "\nDISTANCE  " + Distance.ToString("0000") + " m   |   TOKENS  " + tokensCollected +
                "\nBEST DISTANCE  " + bestDistance.ToString("0000") + " m";
        }
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        runtimeUI?.SetPaused(false);
        runtimeUI?.SetPauseButtonVisible(false);
        RefreshHud();
    }

    void TriggerHitFeedback()
    {
        runtimeUI?.FlashDamage();
        soundEffects?.PlayHit();
        if (Camera.main != null) Camera.main.GetComponent<FollowBoatCamera>()?.TriggerHitShake();
    }

    void ApplyMobileSettings()
    {
        if (!Application.isMobilePlatform) return;
        if (QualitySettings.names.Length > 0) QualitySettings.SetQualityLevel(0, true);
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60;
    }
}
