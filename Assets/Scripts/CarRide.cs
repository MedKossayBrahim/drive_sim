using DriveSim.Race;
using DriveSim.User;
using UnityEngine;
using UnityEngine.InputSystem;

// Relie la course voiture (SimplePoly) au menu : distance, score et retour au hub.
public sealed class CarRide : MonoBehaviour
{
    public const string BestKey = "CarRunner.BestScore";

    public int Distance { get; private set; }
    public int Score { get; private set; }
    public int BestScore { get; private set; }
    public int Coins => Mathf.Max(0, Distance / 40);

    GyroCarController _car;
    Vector3 _lastPosition;
    float _travelled;
    bool _closed;
    bool _havePosition;
    GUIStyle _titleStyle;
    GUIStyle _bodyStyle;

    void Awake()
    {
        int local = Mathf.Max(0, PlayerPrefs.GetInt(BestKey, 0));
        int cloud = AuthSession.IsSignedIn && AuthSession.Player != null ? AuthSession.Player.BestCar : 0;
        BestScore = Mathf.Max(local, cloud);
    }

    void Start()
    {
        _car = Object.FindAnyObjectByType<GyroCarController>();
        if (_car != null)
        {
            _lastPosition = _car.transform.position;
            _havePosition = true;
        }

        if (Object.FindAnyObjectByType<DriveControlsUI>() == null && _car != null)
        {
            var host = new GameObject("DriveControls");
            host.AddComponent<DriveControlsUI>();
        }
    }

    void Update()
    {
        if (_closed)
            return;

        if (_car == null)
            _car = Object.FindAnyObjectByType<GyroCarController>();

        if (_car != null)
        {
            var position = _car.transform.position;
            if (_havePosition)
                _travelled += Vector3.Distance(_lastPosition, position);
            _lastPosition = position;
            _havePosition = true;
        }

        Distance = Mathf.FloorToInt(_travelled);
        Score = Distance + Coins * 25;
        if (Score > BestScore)
        {
            BestScore = Score;
            PlayerPrefs.SetInt(BestKey, BestScore);
            PlayerPrefs.Save();
        }

        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            Finish(Distance >= 200);
    }

    public void Finish(bool won)
    {
        if (_closed)
            return;
        _closed = true;
        Time.timeScale = 1f;
        if (TransportSession.Current != null)
            TransportSession.FinishFromCar(won || Distance >= 200);
    }

    void OnGUI()
    {
        if (_closed)
            return;
        EnsureStyles();
        GUI.Label(new Rect(18f, 16f, 520f, 34f), "VOITURE", _titleStyle);
        GUI.Label(new Rect(18f, 50f, 560f, 26f), "DISTANCE  " + Distance + " m      SCORE  " + Score, _bodyStyle);
        GUI.Label(new Rect(18f, 76f, 560f, 26f), "RECORD  " + BestScore, _bodyStyle);
        if (GUI.Button(new Rect(18f, 112f, 168f, 40f), "TERMINER"))
            Finish(Distance >= 200);
    }

    void EnsureStyles()
    {
        if (_titleStyle != null)
            return;
        _titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 28,
            fontStyle = FontStyle.Bold
        };
        _titleStyle.normal.textColor = new Color(0.98f, 0.82f, 0.22f);
        _bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold
        };
        _bodyStyle.normal.textColor = Color.white;
    }
}
