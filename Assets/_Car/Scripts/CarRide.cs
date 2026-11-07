using DriveSim.Race;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;

// Pièce posée dans la ville originale. Ne réagit qu'à la voiture.
public sealed class CarCoin : MonoBehaviour
{
    CarRide ride;
    bool taken;
    Transform visual;
    Vector3 basePos;
    float spin;

    public void Bind(CarRide owner)
    {
        ride = owner;
        visual = transform;
        basePos = transform.position;
        spin = Random.Range(0f, 360f);
    }

    void Update()
    {
        if (taken || visual == null)
            return;
        spin += 110f * Time.deltaTime;
        visual.rotation = Quaternion.Euler(90f, spin, 0f);
        visual.position = basePos + Vector3.up * (Mathf.Sin(Time.time * 3f + spin) * 0.12f);
    }

    void OnTriggerEnter(Collider other)
    {
        if (taken || ride == null || other.GetComponentInParent<GyroCarController>() == null)
            return;
        taken = true;
        ride.CollectCoin();
        Destroy(gameObject);
    }
}

public sealed class CarFinish : MonoBehaviour
{
    CarRide ride;

    public void Bind(CarRide owner)
    {
        ride = owner;
    }

    void OnTriggerEnter(Collider other)
    {
        if (ride == null || other.GetComponentInParent<GyroCarController>() == null)
            return;
        ride.ReachFinish();
    }
}

// Enrichit la vraie scène SimplePoly : pièces + score + retour hub.
// Ne reconstruit jamais la carte.
public sealed class CarRide : MonoBehaviour
{
    public const string BestKey = "CarRunner.BestScore";

    public int Coins { get; private set; }
    public int Score { get; private set; }
    public int BestScore { get; private set; }
    public int Distance { get; private set; }
    public bool ReachedFinish { get; private set; }

    GyroCarController car;
    Vector3 spawn;
    bool ready;
    bool closed;
    GUIStyle bodyStyle;
    GUIStyle buttonStyle;

    void Start()
    {
        BestScore = Mathf.Max(0, PlayerPrefs.GetInt(BestKey, 0));
        PrepareOriginalScene();
    }

    void PrepareOriginalScene()
    {
        car = FindFirstObjectByType<GyroCarController>();
        if (car == null)
        {
            Debug.LogError("[CarRide] GyroControlledCar introuvable dans la scène SimplePoly.");
            return;
        }

        spawn = car.transform.position;

        var camera = Camera.main;
        if (camera == null)
        {
            var followCam = GameObject.Find("CarFollowCamera");
            if (followCam != null)
                camera = followCam.GetComponent<Camera>();
        }
        if (camera != null)
        {
            if (camera.GetComponent<UniversalAdditionalCameraData>() == null)
                camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            camera.enabled = true;
            var listener = camera.GetComponent<AudioListener>();
            if (listener != null)
                listener.enabled = true;
        }

        var follow = FindFirstObjectByType<CameraFollow>();
        if (follow != null)
            follow.target = car.transform;

        // Caméras du hub / autres scènes : éteintes pour voir uniquement la ville.
        foreach (var other in FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (camera != null && other == camera)
                continue;
            if (other.gameObject.scene.name == gameObject.scene.name && other.CompareTag("MainCamera"))
                continue;
            if (other.gameObject.scene.name != gameObject.scene.name)
                other.enabled = false;
        }

        if (FindFirstObjectByType<AndroidPermissionRequester>() == null)
            gameObject.AddComponent<AndroidPermissionRequester>();

        var gps = FindFirstObjectByType<GPSWorldTracker>();
        if (gps == null)
            gps = gameObject.AddComponent<GPSWorldTracker>();
        gps.ConfigureForRide(car.transform);

        SpawnCoinsAroundCity();
        SpawnFinishGate();
        ready = true;
    }

    public void CollectCoin()
    {
        if (closed)
            return;
        Coins++;
        RefreshScore();
    }

    public void ReachFinish()
    {
        if (closed || ReachedFinish)
            return;
        ReachedFinish = true;
        RefreshScore();
        Finish(true);
    }

    void Update()
    {
        if (!ready || closed || car == null)
            return;

        Distance = Mathf.Max(
            Distance,
            Mathf.RoundToInt(Vector3.Distance(
                new Vector3(car.transform.position.x, 0f, car.transform.position.z),
                new Vector3(spawn.x, 0f, spawn.z))));
        RefreshScore();

        if (car.transform.position.y < spawn.y - 12f)
            RespawnCar();

        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            Finish(false);
    }

    void RespawnCar()
    {
        var body = car.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = spawn;
            body.rotation = Quaternion.identity;
        }
        else
            car.transform.SetPositionAndRotation(spawn, Quaternion.identity);
    }

    void RefreshScore()
    {
        Score = Coins * 100 + Mathf.Max(0, Distance) + (ReachedFinish ? 500 : 0);
        if (Score > BestScore)
        {
            BestScore = Score;
            PlayerPrefs.SetInt(BestKey, BestScore);
            PlayerPrefs.Save();
        }
    }

    public void Finish(bool won)
    {
        if (closed)
            return;
        closed = true;
        RefreshScore();
        if (TransportSession.Current != null)
            TransportSession.FinishFromCar(won || ReachedFinish || Coins >= 8);
    }

    void OnGUI()
    {
        if (closed)
            return;
        EnsureStyles();
        float scale = Mathf.Clamp(Screen.height / 1080f, 0.85f, 1.6f);
        float x = 18f * scale;
        float y = Screen.height - 150f * scale;
        GUI.Label(new Rect(x, y, 480f * scale, 28f * scale), "PIÈCES  " + Coins + "   SCORE  " + Score, bodyStyle);
        GUI.Label(new Rect(x, y + 26f * scale, 480f * scale, 24f * scale), "RECORD  " + BestScore, bodyStyle);
        if (GUI.Button(new Rect(x, y + 54f * scale, 160f * scale, 40f * scale), "TERMINER", buttonStyle))
            Finish(false);
    }

    void EnsureStyles()
    {
        if (bodyStyle != null)
            return;
        bodyStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold
        };
        bodyStyle.normal.textColor = Color.white;
        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold
        };
    }

    void SpawnCoinsAroundCity()
    {
        Vector2[] offsets =
        {
            new Vector2(8f, 12f), new Vector2(-10f, 18f), new Vector2(14f, -8f), new Vector2(-16f, -14f),
            new Vector2(22f, 6f), new Vector2(-24f, 10f), new Vector2(6f, 28f), new Vector2(-8f, -26f),
            new Vector2(32f, -18f), new Vector2(-30f, 22f), new Vector2(18f, 34f), new Vector2(-20f, -32f),
            new Vector2(40f, 14f), new Vector2(-38f, -6f), new Vector2(12f, -40f), new Vector2(-14f, 42f),
            new Vector2(48f, -24f), new Vector2(-46f, 28f), new Vector2(26f, 48f), new Vector2(-28f, -46f),
            new Vector2(56f, 8f), new Vector2(-54f, -16f), new Vector2(4f, 56f), new Vector2(-6f, -54f),
            new Vector2(36f, -36f), new Vector2(-34f, 38f), new Vector2(62f, 20f), new Vector2(-60f, -22f),
            new Vector2(20f, -58f), new Vector2(-18f, 60f), new Vector2(70f, -8f), new Vector2(-68f, 12f)
        };

        for (int i = 0; i < offsets.Length; i++)
        {
            var point = spawn + new Vector3(offsets[i].x, 0f, offsets[i].y);
            point.y = SampleGroundY(point) + 1.05f;
            SpawnCoin(point);
        }
    }

    void SpawnFinishGate()
    {
        var gatePos = spawn + new Vector3(0f, 0f, 90f);
        gatePos.y = SampleGroundY(gatePos) + 0.05f;

        var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        marker.name = "FinishMarker";
        marker.transform.position = gatePos + Vector3.up * 1.2f;
        marker.transform.localScale = new Vector3(3.2f, 1.2f, 3.2f);
        Paint(marker, new Color(0.15f, 0.9f, 0.4f, 0.55f));
        var col = marker.GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;
        marker.AddComponent<CarFinish>().Bind(this);
    }

    void SpawnCoin(Vector3 position)
    {
        var coin = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        coin.name = "Coin";
        coin.transform.position = position;
        coin.transform.localScale = new Vector3(0.75f, 0.06f, 0.75f);
        coin.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        Paint(coin, new Color(0.98f, 0.78f, 0.12f));
        foreach (var c in coin.GetComponents<Collider>())
            Destroy(c);
        var trigger = coin.AddComponent<SphereCollider>();
        trigger.isTrigger = true;
        trigger.radius = 1.35f;
        coin.AddComponent<CarCoin>().Bind(this);
    }

    static float SampleGroundY(Vector3 point)
    {
        if (Physics.Raycast(point + Vector3.up * 80f, Vector3.down, out var hit, 160f))
            return hit.point.y;
        return spawnFallbackY(point);
    }

    static float spawnFallbackY(Vector3 point)
    {
        return point.y;
    }

    static void Paint(GameObject target, Color color)
    {
        var renderer = target.GetComponent<Renderer>();
        if (renderer == null)
            return;
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            return;
        var material = new Material(shader);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.color = color;
        renderer.sharedMaterial = material;
    }
}
