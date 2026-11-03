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
    bool gameStarted;
    bool gameOver;

    public bool IsGameOver => gameOver;
    public bool IsPlaying => gameStarted && !gameOver;
    public bool HasShield => Time.time < shieldUntil;
    public bool HasMagnet => Time.time < magnetUntil;
    public bool HasDoubleScore => Time.time < doubleScoreUntil;
    public bool HasTurbo => Time.time < turboUntil;
    public bool IsInvincible => Time.time < invincibleUntil || HasTurbo;
    public int ScoreMultiplier => HasDoubleScore ? 2 : 1;
    public float MagnetRadius => magnetRadius;
    public float MagnetPullSpeed => magnetPullSpeed;
    public int Lives => lives;
    public int TokensCollected => tokensCollected;
    public int Distance => boat == null ? 0 : Mathf.Max(0, Mathf.FloorToInt(furthestZ - startZ));
    public int Score => score;
    public int BestDistance => bestDistance;

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

    void Start()
    {
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

        bestDistance = Mathf.Max(0, PlayerPrefs.GetInt(BestDistanceKey, 0));
        ShowStartScreen();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            if (gameOver) Restart();
            else if (!gameStarted) StartGame();
        }

        if (!IsPlaying || boat == null) return;
        furthestZ = Mathf.Max(furthestZ, boat.transform.position.z);
        int currentDistance = Distance;
        if (currentDistance > distanceScored)
        {
            score += (currentDistance - distanceScored) * ScoreMultiplier;
            distanceScored = currentDistance;
        }
        boat.SetTurboActive(HasTurbo);
        boat.SetShieldVisible(HasShield);
        RefreshHud();
        boat.SetInvulnerableFlash(IsInvincible);
    }

    public void StartGame()
    {
        if (boat == null) boat = FindFirstObjectByType<BoatController>();
        if (boat == null) return;

        ClearPowerUps();
        lives = startingLives;
        tokensCollected = 0;
        score = 0;
        distanceScored = 0;
        gameOver = false;
        gameStarted = true;
        invincibleUntil = 0f;
        startZ = boat.transform.position.z;
        furthestZ = startZ;
        boat.SetControlsEnabled(true);
        boat.SetInvulnerableFlash(false);
        if (startPanel != null) startPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (spawner == null) spawner = FindFirstObjectByType<ObstacleSpawner>();
        if (spawner != null) spawner.ResetSpawner();
        if (tokenSpawner == null) tokenSpawner = FindFirstObjectByType<RouteTokenSpawner>();
        if (tokenSpawner != null) tokenSpawner.ResetSpawner();
        RefreshHud();
    }

    public void HitObstacle()
    {
        if (!IsPlaying || IsInvincible) return;
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
        RefreshHud();
    }

    public void ActivatePowerUp(BoatPowerUpType type)
    {
        if (!IsPlaying) return;
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

    float GetPowerUpDuration(BoatPowerUpType type)
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

    void ShowStartScreen()
    {
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
        if (powerUpsText == null) return;
        string lines = "";
        AddPowerUpLine(ref lines, "SHIELD", BoatPowerUpType.Shield);
        AddPowerUpLine(ref lines, "MAGNET", BoatPowerUpType.Magnet);
        AddPowerUpLine(ref lines, "DOUBLE SCORE", BoatPowerUpType.DoubleScore);
        AddPowerUpLine(ref lines, "TURBO", BoatPowerUpType.Turbo);
        powerUpsText.text = lines.Length == 0 ? "POWER-UPS  NONE" : lines.TrimEnd();
    }

    void AddPowerUpLine(ref string lines, string label, BoatPowerUpType type)
    {
        float remaining = GetPowerUpRemaining(type);
        if (remaining <= 0f) return;
        if (lines.Length > 0) lines += "\n";
        lines += label + "  " + remaining.ToString("0.0") + "s";
    }

    void ClearPowerUps()
    {
        shieldUntil = magnetUntil = doubleScoreUntil = turboUntil = 0f;
        if (boat != null)
        {
            boat.SetShieldVisible(false);
            boat.SetTurboActive(false);
        }
    }

    void EndGame()
    {
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
        if (finalScoreText != null)
        {
            finalScoreText.text = "SCORE  " + Score.ToString("0000") +
                "\nDISTANCE  " + Distance.ToString("0000") + " m   |   TOKENS  " + tokensCollected +
                "\nBEST DISTANCE  " + bestDistance.ToString("0000") + " m";
        }
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        RefreshHud();
    }
}