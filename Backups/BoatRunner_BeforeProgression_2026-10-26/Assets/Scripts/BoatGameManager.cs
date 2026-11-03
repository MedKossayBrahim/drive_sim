using UnityEngine;
using UnityEngine.UI;

public sealed class BoatGameManager : MonoBehaviour
{
    [SerializeField] BoatController boat;
    [SerializeField] ObstacleSpawner spawner;
    [SerializeField] Text scoreText;
    [SerializeField] Text livesText;
    [SerializeField] Text speedText;
    [SerializeField] GameObject gameOverPanel;
    [SerializeField] Text finalScoreText;
    [SerializeField] int startingLives = 3;
    [SerializeField] float invincibilityDuration = 1.65f;
    int lives;
    float startZ;
    float furthestZ;
    float invincibleUntil;
    bool gameOver;
    public bool IsGameOver => gameOver;
    public bool IsInvincible => Time.time < invincibleUntil;
    public int Lives => lives;
    public int Distance => boat == null ? 0 : Mathf.Max(0, Mathf.FloorToInt(furthestZ - startZ));

    public void Initialize(BoatController player, Text score, Text livesLabel, GameObject panel, Text finalScore, ObstacleSpawner obstacleSpawner)
    {
        boat = player; scoreText = score; livesText = livesLabel; gameOverPanel = panel; finalScoreText = finalScore; spawner = obstacleSpawner;
        BeginRound();
    }

    void Start()
    {
        if (boat == null) boat = FindFirstObjectByType<BoatController>();
        if (spawner == null) spawner = FindFirstObjectByType<ObstacleSpawner>();
        if (scoreText == null) scoreText = GameObject.Find("Distance")?.GetComponent<Text>();
        if (livesText == null) livesText = GameObject.Find("Lives")?.GetComponent<Text>();
        if (speedText == null) speedText = GameObject.Find("Speed")?.GetComponent<Text>();
        if (gameOverPanel == null) gameOverPanel = GameObject.Find("Game Over Panel");
        if (finalScoreText == null) finalScoreText = GameObject.Find("Final Distance")?.GetComponent<Text>();
        if (boat != null) BeginRound();
    }

    void Update()
    {
        if (gameOver && Input.GetKeyDown(KeyCode.R)) Restart();
        if (gameOver || boat == null) return;
        furthestZ = Mathf.Max(furthestZ, boat.transform.position.z);
        if (scoreText != null) scoreText.text = "DISTANCE  " + Distance.ToString("0000");
        if (speedText != null) speedText.text = "SPEED  " + Mathf.RoundToInt(boat.CurrentSpeed * 3.6f) + " km/h";
        boat.SetInvulnerableFlash(IsInvincible);
    }

    public void HitObstacle()
    {
        if (gameOver || IsInvincible) return;
        lives = Mathf.Max(0, lives - 1);
        invincibleUntil = Time.time + invincibilityDuration;
        Handheld.Vibrate();
        RefreshHud();
        if (lives == 0) EndGame();
    }

    public void Restart()
    {
        foreach (var obstacle in FindObjectsByType<Obstacle>(FindObjectsSortMode.None)) Destroy(obstacle.gameObject);
        if (boat != null) boat.ResetBoat();
        if (spawner == null) spawner = FindFirstObjectByType<ObstacleSpawner>();
        if (spawner != null) spawner.ResetSpawner();
        BeginRound();
    }

    void BeginRound()
    {
        lives = startingLives;
        gameOver = false;
        invincibleUntil = 0f;
        if (boat != null)
        {
            startZ = boat.transform.position.z;
            furthestZ = startZ;
            boat.SetControlsEnabled(true);
            boat.SetInvulnerableFlash(false);
        }
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (scoreText != null) scoreText.text = "DISTANCE  0000";
        RefreshHud();
        if (spawner != null) spawner.ResetSpawner();
    }

    void RefreshHud()
    {
        if (livesText != null) livesText.text = "LIVES  " + lives;
    }

    void EndGame()
    {
        gameOver = true;
        if (boat != null) boat.SetControlsEnabled(false);
        if (finalScoreText != null) finalScoreText.text = "DISTANCE  " + Distance.ToString("0000");
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
    }
}


