using BikeGame;
using DriveSim.Race;
using DriveSim.User;
using UnityEngine;
using UnityEngine.InputSystem;

// Relie la course vélo au menu : pièces de la forêt, score et retour au compte.
public sealed class BikeRide : MonoBehaviour
{
    public const string BestKey = "BikeRunner.BestScore";

    public int Coins => CoinCounter.Instance != null ? CoinCounter.Instance.CollectedCoins : 0;
    public int Score { get; private set; }
    public int BestScore { get; private set; }
    public bool FinishedLevel { get; private set; }

    bool closed;
    GUIStyle titleStyle;
    GUIStyle bodyStyle;

    void Awake()
    {
        int local = Mathf.Max(0, PlayerPrefs.GetInt(BestKey, 0));
        int cloud = AuthSession.IsSignedIn && AuthSession.Player != null ? AuthSession.Player.BestBike : 0;
        BestScore = Mathf.Max(local, cloud);
    }

    void Update()
    {
        if (closed)
            return;

        var complete = Object.FindAnyObjectByType<LevelCompleteTrigger>();
        if (complete != null && complete.IsComplete)
            FinishedLevel = true;

        Score = Coins * 100 + (FinishedLevel ? 500 : 0);
        if (Score > BestScore)
        {
            BestScore = Score;
            PlayerPrefs.SetInt(BestKey, BestScore);
            PlayerPrefs.Save();
        }

        var keyboard = Keyboard.current;
        if (FinishedLevel || (keyboard != null && keyboard.escapeKey.wasPressedThisFrame))
            Finish(FinishedLevel);
    }

    public void Finish(bool won)
    {
        if (closed)
            return;
        closed = true;
        Time.timeScale = 1f;
        if (TransportSession.Current != null)
            TransportSession.FinishFromBike(won || FinishedLevel);
    }

    void OnGUI()
    {
        if (closed)
            return;
        EnsureStyles();
        GUI.Label(new Rect(18f, 16f, 520f, 34f), "VÉLO", titleStyle);
        GUI.Label(new Rect(18f, 50f, 560f, 26f), "PIÈCES  " + Coins + "      SCORE  " + Score, bodyStyle);
        GUI.Label(new Rect(18f, 76f, 560f, 26f), "RECORD  " + BestScore, bodyStyle);
        if (GUI.Button(new Rect(18f, 112f, 168f, 40f), "TERMINER"))
            Finish(false);
    }

    void EnsureStyles()
    {
        if (titleStyle != null)
            return;
        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 28,
            fontStyle = FontStyle.Bold
        };
        titleStyle.normal.textColor = new Color(0.98f, 0.82f, 0.22f);
        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold
        };
        bodyStyle.normal.textColor = Color.white;
    }
}
