using System.Collections;
using DriveSim.Menu;
using DriveSim.User;
using Project.Plane;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace DriveSim.Race
{
    // Lance le véhicule choisi depuis le hub et renvoie score / pièces au menu.
    public sealed class TransportSession : MonoBehaviour
    {
        public const string PlaneScenePath = "Assets/_Plane/Scenes/Plane_Scene.unity";
        public const string PlaneSceneName = "Plane_Scene";
        public const string BoatScenePath = "Assets/_Boat/Scenes/BoatRunner.unity";
        public const string BoatSceneName = "BoatRunner";
        public const string BikeScenePath = "Assets/BikeGame/Scenes/ForestBikeRide.unity";
        public const string BikeSceneName = "ForestBikeRide";
        public const string CarScenePath = "Assets/SimplePoly City - Low Poly Assets/Demo/SimplePoly City - Low Poly Assets_Demo Scene.unity";
        public const string CarSceneName = "SimplePoly City - Low Poly Assets_Demo Scene";

        public static TransportSession Current { get; private set; }

        int _mode;
        int _vehicle;
        float _startedAt;
        bool _busy;
        bool _reported;
        string _sceneName;
        Scene _rideScene;
        LandingMission _mission;
        ScoreManager _score;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindAnyObjectByType<TransportSession>() != null)
                return;
            var root = new GameObject("TransportSession");
            DontDestroyOnLoad(root);
            root.AddComponent<TransportSession>();
        }

        void Awake()
        {
            if (Current != null && Current != this)
            {
                Destroy(gameObject);
                return;
            }
            Current = this;
            ConfigurePhysicsLayers();
            GameHub.PlayRequested += OnPlayRequested;
        }

        void OnDestroy()
        {
            if (Current == this)
                Current = null;
            GameHub.PlayRequested -= OnPlayRequested;
        }

        void OnPlayRequested(int mode, int vehicle)
        {
            if (_busy)
                return;
            _mode = mode;
            _vehicle = vehicle;
            if (GarageProgress.IsPlane(vehicle))
            {
                StartCoroutine(LaunchPlane());
                return;
            }
            if (GarageProgress.IsBoat(vehicle))
            {
                StartCoroutine(LaunchBoat());
                return;
            }
            if (GarageProgress.IsBike(vehicle))
            {
                StartCoroutine(LaunchBike());
                return;
            }
            if (GarageProgress.IsCar(vehicle))
            {
                StartCoroutine(LaunchCar());
                return;
            }
            GameHub.ReportResult(0, 0, 0f, 0);
            if (GameHub.Current != null)
                GameHub.Current.Notify(GarageProgress.TransportName(vehicle) + " arrive bientôt.");
        }

        IEnumerator LaunchPlane()
        {
            _busy = true;
            _reported = false;
            _startedAt = Time.unscaledTime;
            _mission = null;
            _score = null;
            SetHubWorldVisible(false, PlaneSceneName);
            yield return LoadRide(PlaneScenePath, PlaneSceneName);
            if (!_rideScene.IsValid())
                yield break;

            yield return null;
            BindMission();
            while (_busy && !_reported)
            {
                if (_mission == null)
                    BindMission();
                yield return null;
            }
        }

        IEnumerator LaunchBoat()
        {
            _busy = true;
            _reported = false;
            _startedAt = Time.unscaledTime;
            SetHubWorldVisible(false, BoatSceneName);
            yield return LoadRide(BoatScenePath, BoatSceneName);
            if (!_rideScene.IsValid())
                yield break;

            yield return null;
            if (_rideScene.IsValid())
                SceneManager.SetActiveScene(_rideScene);
            FocusRideCameras(BoatSceneName, true);
            BoatRenderSetup.Apply();
            var manager = FindAnyObjectByType<BoatGameManager>();
            if (manager != null)
                manager.PlayFromHub();
            while (_busy && !_reported)
                yield return null;
        }

        IEnumerator LaunchBike()
        {
            _busy = true;
            _reported = false;
            _startedAt = Time.unscaledTime;
            SetHubWorldVisible(false, BikeSceneName);
            yield return LoadRide(BikeScenePath, BikeSceneName);
            if (!_rideScene.IsValid())
                yield break;

            yield return null;
            if (Object.FindAnyObjectByType<BikeRide>() == null)
            {
                var host = new GameObject("BikeRide");
                SceneManager.MoveGameObjectToScene(host, _rideScene);
                host.AddComponent<BikeRide>();
            }
            while (_busy && !_reported)
                yield return null;
        }

        IEnumerator LaunchCar()
        {
            _busy = true;
            _reported = false;
            _startedAt = Time.unscaledTime;
            SetHubWorldVisible(false, CarSceneName);
            yield return LoadRide(CarScenePath, CarSceneName);
            if (!_rideScene.IsValid())
                yield break;

            yield return null;
            if (_rideScene.IsValid())
                SceneManager.SetActiveScene(_rideScene);
            FocusRideCameras(CarSceneName, true);
            if (Object.FindAnyObjectByType<CarRide>() == null)
            {
                var host = new GameObject("CarRide");
                SceneManager.MoveGameObjectToScene(host, _rideScene);
                host.AddComponent<CarRide>();
            }
            while (_busy && !_reported)
                yield return null;
        }

        IEnumerator LoadRide(string scenePath, string sceneName)
        {
            _sceneName = sceneName;
            AsyncOperation load;
#if UNITY_EDITOR
            load = UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                scenePath,
                new LoadSceneParameters(LoadSceneMode.Additive));
#else
            load = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
#endif
            if (load == null)
            {
                SetHubWorldVisible(true, sceneName);
                _busy = false;
                GameHub.ReportResult(0, 0, 0f, 0);
                yield break;
            }

            while (!load.isDone)
                yield return null;

            _rideScene = SceneManager.GetSceneByName(sceneName);
            if (_rideScene.IsValid())
                SceneManager.SetActiveScene(_rideScene);
        }

        void BindMission()
        {
            _mission = FindAnyObjectByType<LandingMission>();
            _score = FindAnyObjectByType<ScoreManager>();
            if (_mission == null)
                return;
            _mission.OnStateChanged -= OnMissionState;
            _mission.OnStateChanged += OnMissionState;
        }

        void OnMissionState(FlightMissionState state)
        {
            // L'écran de fin plane reste ; le retour hub passe par Quit / FinishFromPlane.
        }

        public static void FinishFromPlane(bool won)
        {
            if (Current != null)
                Current.StartCoroutine(Current.FinishPlaneRoutine(won));
        }

        public static void FinishFromBoat(bool won)
        {
            if (Current != null)
                Current.StartCoroutine(Current.FinishBoatRoutine(won));
        }

        public static void FinishFromBike(bool won)
        {
            if (Current != null)
                Current.StartCoroutine(Current.FinishBikeRoutine(won));
        }

        public static void FinishFromCar(bool won)
        {
            if (Current != null)
                Current.StartCoroutine(Current.FinishCarRoutine(won));
        }

        public static void RestartPlane()
        {
            if (Current == null || !Current._busy)
                return;
            Current.StartCoroutine(Current.ReloadPlane());
        }

        IEnumerator ReloadPlane()
        {
            _reported = false;
            _startedAt = Time.unscaledTime;
            UnbindMission();
            if (_rideScene.IsValid() && _rideScene.isLoaded)
            {
                var unload = SceneManager.UnloadSceneAsync(_rideScene);
                while (unload != null && !unload.isDone)
                    yield return null;
            }

            yield return LoadRide(PlaneScenePath, PlaneSceneName);
            if (!_rideScene.IsValid())
                yield break;
            yield return null;
            BindMission();
        }

        IEnumerator FinishPlaneRoutine(bool won)
        {
            if (_reported)
                yield break;
            _reported = true;

            int score = _score != null ? _score.Score : 0;
            int coins = _score != null ? _score.CoinsCollected : 0;
            float seconds = Mathf.Max(1f, Time.unscaledTime - _startedAt);
            int stars = 0;
            if (won)
            {
                stars = 1;
                if (coins >= 25)
                    stars = 2;
                if (coins >= 40)
                    stars = 3;
            }

            yield return UnloadRide();
            GameHub.ReportResult(stars, score, seconds, coins);
        }

        IEnumerator FinishBoatRoutine(bool won)
        {
            if (_reported)
                yield break;
            _reported = true;
            Time.timeScale = 1f;

            var manager = FindAnyObjectByType<BoatGameManager>();
            int score = manager != null ? manager.Score : 0;
            int coins = manager != null ? manager.TokensCollected : 0;
            int distance = manager != null ? manager.Distance : 0;
            float seconds = Mathf.Max(1f, Time.unscaledTime - _startedAt);
            int stars = 0;
            if (won && (score > 0 || coins > 0 || distance > 30))
            {
                stars = 1;
                if (distance >= 400)
                    stars = 2;
                if (distance >= 900)
                    stars = 3;
            }

            yield return UnloadRide();
            GameHub.ReportResult(stars, score, seconds, coins);
        }

        IEnumerator FinishBikeRoutine(bool won)
        {
            if (_reported)
                yield break;
            _reported = true;
            Time.timeScale = 1f;

            var ride = Object.FindAnyObjectByType<BikeRide>();
            int coins = ride != null ? ride.Coins : 0;
            int score = ride != null ? ride.Score : coins * 100;
            float seconds = Mathf.Max(1f, Time.unscaledTime - _startedAt);
            int stars = 0;
            if (won || coins >= 8)
            {
                stars = 1;
                if (coins >= 15)
                    stars = 2;
                if (coins >= 25)
                    stars = 3;
            }

            yield return UnloadRide();
            GameHub.ReportResult(stars, score, seconds, coins);
        }

        IEnumerator FinishCarRoutine(bool won)
        {
            if (_reported)
                yield break;
            _reported = true;
            Time.timeScale = 1f;

            var ride = Object.FindAnyObjectByType<CarRide>();
            int distance = ride != null ? ride.Distance : 0;
            int coins = ride != null ? ride.Coins : 0;
            int score = ride != null ? ride.Score : distance;
            float seconds = Mathf.Max(1f, Time.unscaledTime - _startedAt);
            int stars = 0;
            if (won || distance >= 200)
            {
                stars = 1;
                if (distance >= 500)
                    stars = 2;
                if (distance >= 1000)
                    stars = 3;
            }

            yield return UnloadRide();
            GameHub.ReportResult(stars, score, seconds, coins);
        }

        IEnumerator UnloadRide()
        {
            UnbindMission();
            if (_rideScene.IsValid() && _rideScene.isLoaded)
            {
                var unload = SceneManager.UnloadSceneAsync(_rideScene);
                while (unload != null && !unload.isDone)
                    yield return null;
            }

            Time.timeScale = 1f;
            SetHubWorldVisible(true, _sceneName);
            _busy = false;
        }

        void UnbindMission()
        {
            if (_mission != null)
                _mission.OnStateChanged -= OnMissionState;
            _mission = null;
        }

        static void SetHubWorldVisible(bool visible, string rideScene)
        {
            if (AuthBackdrop.Current != null)
                AuthBackdrop.Current.gameObject.SetActive(visible);

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.IsValid() || !scene.isLoaded)
                    continue;
                if (scene.name == rideScene)
                    continue;
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.name == "AuthBridge" || root.name == "GameHub" || root.name == "TransportSession" || root.name == "AuthGarage")
                        continue;
                    root.SetActive(visible);
                }
            }

            var systems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var system in systems)
            {
                bool inRide = system.gameObject.scene.name == rideScene;
                system.gameObject.SetActive(visible ? !inRide : inRide);
            }

            FocusRideCameras(rideScene, !visible);
        }

        static void FocusRideCameras(string rideScene, bool rideActive)
        {
            var cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var camera in cameras)
            {
                bool inRide = camera.gameObject.scene.name == rideScene;
                camera.enabled = rideActive ? inRide : !inRide;
                if (camera.enabled && camera.GetComponent<UniversalAdditionalCameraData>() == null)
                    camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            }

            var listeners = Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            bool oneListener = false;
            foreach (var listener in listeners)
            {
                bool inRide = listener.gameObject.scene.name == rideScene;
                bool shouldEnable = rideActive ? inRide : !inRide;
                listener.enabled = shouldEnable && !oneListener;
                if (listener.enabled)
                    oneListener = true;
            }
        }

        static void ConfigurePhysicsLayers()
        {
            int plane = LayerMask.NameToLayer("Plane");
            int ground = LayerMask.NameToLayer("Ground");
            int checkpoint = LayerMask.NameToLayer("Checkpoint");
            int cargo = LayerMask.NameToLayer("Cargo");
            if (plane < 0 || ground < 0 || checkpoint < 0 || cargo < 0)
                return;
            for (int i = 0; i < 32; i++)
                Physics.IgnoreLayerCollision(checkpoint, i, i != plane);
            Physics.IgnoreLayerCollision(plane, ground, false);
            Physics.IgnoreLayerCollision(plane, cargo, false);
            Physics.IgnoreLayerCollision(cargo, ground, false);
        }
    }
}
