using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class BoatGameManager : MonoBehaviour
{
    [SerializeField] BoatController boat;
    [SerializeField] Text scoreText;
    [SerializeField] Text livesText;
    [SerializeField] GameObject gameOverPanel;
    [SerializeField] Text finalScoreText;
    [SerializeField] int startingLives = 3;
    [SerializeField] float invincibilityDuration = 1.5f;
    int lives;
    float startZ;
    float invincibleUntil;
    bool gameOver;
    public bool IsInvincible => Time.time < invincibleUntil;
    public void Initialize(BoatController player, Text score, Text livesLabel, GameObject panel, Text finalScore)
    {
        boat = player; scoreText = score; livesText = livesLabel; gameOverPanel = panel; finalScoreText = finalScore;
        lives = startingLives; startZ = boat.transform.position.z;
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        RefreshHud();
    }
    void Update()
    {
        if (gameOver || boat == null) return;
        if (scoreText != null) scoreText.text = "DISTANCE  " + Mathf.FloorToInt(boat.transform.position.z - startZ).ToString("0000");
    }
    public void HitObstacle()
    {
        if (gameOver || IsInvincible) return;
        lives--;
        invincibleUntil = Time.time + invincibilityDuration;
        Handheld.Vibrate();
        RefreshHud();
        if (lives <= 0) EndGame();
    }
    void RefreshHud() { if (livesText != null) livesText.text = "LIVES  " + Mathf.Max(0, lives); }
    void EndGame()
    {
        gameOver = true;
        if (boat != null) boat.SetControlsEnabled(false);
        if (finalScoreText != null) finalScoreText.text = "DISTANCE  " + Mathf.FloorToInt(boat.transform.position.z - startZ).ToString("0000");
        if (gameOverPanel != null) gameOverPanel.SetActive(true);
    }
    public void Restart() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
}

