using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;
using UnityEngine.UI;

namespace DriveSim.User
{
    // Ordre des écrans : inscription, connexion, biométrie, session prête pour l'équipe course.
    public sealed class AuthFlow : MonoBehaviour
    {
        static readonly Queue<string> Incoming = new Queue<string>();
        static readonly object Gate = new object();

        AuthScreen _ui;
        AuthBackdrop _backdrop;
        bool? _landscape;
        string _refresh;
        bool _registerMode;
        bool _busy;
        bool _dismissed;

        static AuthFlow _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindAnyObjectByType<AuthFlow>() != null)
                return;

            // Le simulateur sous-marin a son propre point d'entrée (SubmarineBoot).
            if (SceneManager.GetActiveScene().name == "SubmarineScene")
                return;

            var bridge = new GameObject("AuthBridge");
            DontDestroyOnLoad(bridge);
            bridge.AddComponent<AuthFlow>();
        }

        public static void EnqueueNative(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;
            lock (Gate)
                Incoming.Enqueue(message);
        }

        void Awake()
        {
            _instance = this;
            lock (Gate)
                Incoming.Clear();

            _backdrop = AuthBackdrop.Build();
            _ui = gameObject.AddComponent<AuthScreen>();
            _ui.Construct();
            ApplyOrientation();
            EnsureEventSystem();

            _ui.PrimaryButton.onClick.AddListener(() => { _ = Submit(); });
            _ui.SwitchButton.onClick.AddListener(ToggleMode);
            _ui.BiometricButton.onClick.AddListener(BeginLoad);
            _ui.SimSuccessButton.onClick.AddListener(SimSuccess);
            _ui.SimFailButton.onClick.AddListener(() => EnqueueNative("load:cancel"));
            _ui.EnableYesButton.onClick.AddListener(EnableYes);
            _ui.EnableNoButton.onClick.AddListener(() =>
                FinishEnable(false, "Mot de passe conservé. Vous pourrez activer le visage plus tard."));
            _ui.PlayButton.onClick.AddListener(EnterSimulator);
            _ui.PracticeButton.onClick.AddListener(EnterSimulator);
            _ui.LogoutButton.onClick.AddListener(() => { _ = Logout(); });
            _ui.Password.onSubmit.AddListener(text => { _ = Submit(); });

            ShowCredentials(DeviceAuth.HasSavedToken());
        }

        // Ferme l'écran de compte (appelé par SubmarineBoot au chargement du simulateur).
        public static void Dismiss()
        {
            if (_instance != null)
                _instance.HideAll();
        }

        void HideAll()
        {
            if (_dismissed) return;
            _dismissed = true;
            if (_ui != null) _ui.Hide();
            if (_backdrop != null) Destroy(_backdrop.gameObject);
        }

        void EnterSimulator()
        {
            HideAll();
            if (SceneManager.GetActiveScene().name != "SubmarineScene")
                SceneManager.LoadScene("SubmarineScene");
        }

        async void Start()
        {
            try
            {
                await Boot();
            }
            catch (Exception exception)
            {
                SetBusy(false);
                SetStatus(exception.Message, true);
            }
        }

        void Update()
        {
            if (_dismissed)
                return;
            ApplyOrientation();
            while (true)
            {
                string message;
                lock (Gate)
                {
                    if (Incoming.Count == 0)
                        return;
                    message = Incoming.Dequeue();
                }
                OnNativeResult(message);
            }
        }

        async System.Threading.Tasks.Task Boot()
        {
            var url = CurrentUrl();
            if (url == null)
                return;

            SetStatus("Vérification du serveur...");
            var online = await AuthApiClient.IsReachable(url);
            if (!online)
            {
                SetStatus("Le serveur ne répond pas. Ouvrez le Terminal, allez dans le dossier Backend, puis lancez npm start.", true);
                return;
            }

            var saved = DeviceAuth.HasSavedToken();
            SetStatus(saved
                ? "Serveur prêt. Utilisez la biométrie ou le mot de passe."
                : "Serveur prêt. Créez un compte ou connectez-vous.");

            if (saved && DeviceAuth.IsBiometricAvailable() && !Application.isEditor)
                BeginLoad();
        }

        void ToggleMode()
        {
            if (_busy) return;
            _registerMode = !_registerMode;
            ShowCredentials(DeviceAuth.HasSavedToken());
        }

        async System.Threading.Tasks.Task Submit()
        {
            if (_busy) return;
            var url = CurrentUrl();
            if (url == null) return;

            var email = _ui.Email.text.Trim();
            var password = _ui.Password.text;
            var displayName = _ui.DisplayName.text.Trim();
            if (!email.Contains("@") || !email.Contains("."))
            {
                SetStatus("Entrez un e-mail valide.", true);
                return;
            }
            if (password.Length < 6)
            {
                SetStatus("Le mot de passe doit contenir au moins 6 caractères.", true);
                return;
            }
            if (_registerMode && (displayName.Length < 2 || displayName.Length > 24))
            {
                SetStatus("Le pseudo doit contenir entre 2 et 24 caractères.", true);
                return;
            }

            SetBusy(true);
            SetStatus(_registerMode ? "Création du compte..." : "Connexion...");
            try
            {
                var result = _registerMode
                    ? await AuthApiClient.Register(url, email, password, displayName)
                    : await AuthApiClient.Login(url, email, password);
                if (!result.Ok)
                {
                    SetStatus(result.Error, true);
                    return;
                }

                var profile = await AuthApiClient.Me(url, result.AccessToken);
                if (!profile.Ok)
                {
                    SetStatus(profile.Error, true);
                    return;
                }

                _refresh = result.RefreshToken;
                _ui.Password.text = "";
                AuthSession.Open(profile.Player, result.AccessToken);
                if (DeviceAuth.IsBiometricAvailable() && !DeviceAuth.HasSavedToken())
                {
                    ShowEnable();
                    SetStatus("Compte accepté par le serveur.");
                }
                else
                {
                    ShowHome("Compte accepté par le serveur.");
                }
            }
            catch (Exception exception)
            {
                SetStatus(exception.Message, true);
            }
            finally
            {
                SetBusy(false);
            }
        }

        void BeginLoad()
        {
            if (_busy) return;
            if (Application.isEditor)
            {
                ShowSim();
                SetStatus("L'éditeur simule le téléphone. Choisissez un résultat.");
                return;
            }

            SetBusy(true);
            SetStatus("Regardez la fenêtre du téléphone.");
            try
            {
                DeviceAuth.LoadToken();
            }
            catch (Exception exception)
            {
                SetBusy(false);
                SetStatus(exception.Message, true);
            }
        }

        void SimSuccess()
        {
            if (_busy) return;
            SetBusy(true);
            SetStatus("Le téléphone simulé a accepté. Le serveur vérifie le compte...");
            DeviceAuth.LoadToken();
        }

        void EnableYes()
        {
            if (_busy || string.IsNullOrEmpty(_refresh))
            {
                ShowHome("Connecté.");
                return;
            }

            SetBusy(true);
            SetStatus(Application.isEditor
                ? "Activation simulée dans l'éditeur..."
                : "Confirmez sur la fenêtre du téléphone.");
            try
            {
                DeviceAuth.SaveToken(_refresh);
            }
            catch (Exception exception)
            {
                SetBusy(false);
                SetStatus(exception.Message, true);
            }
        }

        async System.Threading.Tasks.Task FinishBiometricLogin(string refreshToken)
        {
            var url = CurrentUrl();
            if (url == null)
            {
                SetBusy(false);
                ShowCredentials(true);
                return;
            }

            try
            {
                var refreshed = await AuthApiClient.Refresh(url, refreshToken);
                if (!refreshed.Ok)
                {
                    if (refreshed.Unauthorized)
                        DeviceAuth.ClearToken();
                    ShowCredentials(DeviceAuth.HasSavedToken());
                    SetStatus(refreshed.Error, true);
                    return;
                }

                var profile = await AuthApiClient.Me(url, refreshed.AccessToken);
                if (!profile.Ok)
                {
                    ShowCredentials(true);
                    SetStatus(profile.Error, true);
                    return;
                }

                _refresh = refreshed.RefreshToken;
                AuthSession.Open(profile.Player, refreshed.AccessToken);
                ShowHome("Biométrie acceptée par le téléphone, compte accepté par le serveur.");
            }
            catch (Exception exception)
            {
                ShowCredentials(true);
                SetStatus(exception.Message, true);
            }
            finally
            {
                SetBusy(false);
            }
        }

        async System.Threading.Tasks.Task Logout()
        {
            if (_busy) return;
            SetBusy(true);
            try
            {
                var url = CurrentUrl();
                if (url != null && !string.IsNullOrEmpty(_refresh))
                    await AuthApiClient.Logout(url, _refresh);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(exception.Message);
            }
            finally
            {
                _refresh = null;
                DeviceAuth.ClearToken();
                AuthSession.Close();
                _registerMode = false;
                SetBusy(false);
                ShowCredentials(false);
                SetStatus("Vous êtes déconnecté de cet appareil.");
            }
        }

        [Preserve]
        public void OnNativeResult(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            if (message == "save:ok")
            {
                FinishEnable(true, "Connexion biométrique activée sur cet appareil.");
                return;
            }
            if (message == "save:cancel")
            {
                FinishEnable(false, "Activation annulée. Le mot de passe reste disponible.");
                return;
            }
            if (message.StartsWith("save:error:"))
            {
                SetBusy(false);
                ShowEnable();
                SetStatus(message.Substring("save:error:".Length), true);
                return;
            }
            if (message == "load:cancel")
            {
                SetBusy(false);
                ShowCredentials(DeviceAuth.HasSavedToken());
                SetStatus("Biométrie annulée. Vous pouvez utiliser le mot de passe.");
                return;
            }
            if (message.StartsWith("load:error:"))
            {
                SetBusy(false);
                ShowCredentials(DeviceAuth.HasSavedToken());
                SetStatus(message.Substring("load:error:".Length), true);
                return;
            }
            if (message.StartsWith("load:ok:"))
            {
                var token = message.Substring("load:ok:".Length);
                if (string.IsNullOrEmpty(token))
                {
                    SetBusy(false);
                    ShowCredentials(false);
                    SetStatus("Aucune biométrie enregistrée. Connectez-vous avec le mot de passe.", true);
                    return;
                }
                _ = FinishBiometricLogin(token);
            }
        }

        void FinishEnable(bool enabled, string message)
        {
            SetBusy(false);
            ShowHome(message);
            if (!enabled)
                SetStatus(message);
        }

        void ShowCredentials(bool hasSavedToken)
        {
            SetForm(true);
            _ui.NameRow.SetActive(_registerMode);
            _ui.PrimaryLabel.text = _registerMode ? "CRÉER LE COMPTE" : "SE CONNECTER";
            _ui.SwitchLabel.text = _registerMode ? "J'AI DÉJÀ UN COMPTE" : "CRÉER UN COMPTE";
            var biometric = hasSavedToken && DeviceAuth.IsBiometricAvailable();
            _ui.BiometricButton.gameObject.SetActive(biometric);
            _ui.BiometricLabel.text = Application.isEditor ? "SIMULER LA BIOMÉTRIE" : "CONNEXION BIOMÉTRIQUE";
            _ui.SimPanel.SetActive(false);
            _ui.EnablePanel.SetActive(false);
            _ui.HomePanel.SetActive(false);
            Rebuild();
        }

        void ShowSim()
        {
            SetForm(false);
            _ui.BiometricButton.gameObject.SetActive(false);
            _ui.SimPanel.SetActive(true);
            _ui.EnablePanel.SetActive(false);
            _ui.HomePanel.SetActive(false);
            Rebuild();
        }

        void ShowEnable()
        {
            SetForm(false);
            _ui.BiometricButton.gameObject.SetActive(false);
            _ui.SimPanel.SetActive(false);
            _ui.EnablePanel.SetActive(true);
            _ui.HomePanel.SetActive(false);
            Rebuild();
        }

        void ShowHome(string message)
        {
            SetForm(false);
            _ui.BiometricButton.gameObject.SetActive(false);
            _ui.SimPanel.SetActive(false);
            _ui.EnablePanel.SetActive(false);
            _ui.HomePanel.SetActive(true);
            var player = AuthSession.Player;
            _ui.Hello.text = player == null ? "Connecté" : "Bonjour " + player.DisplayName;
            if (_backdrop != null)
                _backdrop.Celebrate();
            _ui.Details.text = player == null
                ? ""
                : "E-mail : " + player.Email + "\nIdentifiant : " + player.Id
                  + "\n\nProfil de pilote prêt.\nLe simulateur sous-marin reprend cette session.";
            SetStatus(message);
            Rebuild();
        }

        void SetForm(bool visible)
        {
            _ui.Email.gameObject.SetActive(visible);
            _ui.Password.gameObject.SetActive(visible);
            _ui.PrimaryButton.gameObject.SetActive(visible);
            _ui.SwitchButton.gameObject.SetActive(visible);
            _ui.PracticeButton.gameObject.SetActive(visible);
            if (!visible)
                _ui.NameRow.SetActive(false);
        }

        string CurrentUrl()
        {
            return AuthConfig.DefaultServerUrl.Trim().TrimEnd('/');
        }

        void SetBusy(bool busy)
        {
            _busy = busy;
            _ui.SetLoading(busy);
            if (_ui.LockDuringRequest == null) return;
            foreach (var item in _ui.LockDuringRequest)
            {
                if (item != null)
                    item.interactable = !busy;
            }
        }

        void SetStatus(string message, bool error = false)
        {
            _ui.Status.text = message ?? "";
            _ui.Status.color = error
                ? new Color(1f, 0.78f, 0.74f)
                : new Color(0.9f, 0.96f, 1f);
        }

        void ApplyOrientation()
        {
            var landscape = Screen.width > Screen.height;
            if (_landscape == landscape)
                return;
            _landscape = landscape;
            var focus = _ui.ApplyLayout(landscape);
            if (_backdrop != null)
                _backdrop.SetFocus(focus);
            Rebuild();
        }

        void Rebuild()
        {
            if (_ui.Content != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_ui.Content);
        }

        static void EnsureEventSystem()
        {
            if (FindAnyObjectByType<EventSystem>() != null)
                return;
            var events = new GameObject("AuthEventSystem");
            DontDestroyOnLoad(events);
            events.AddComponent<EventSystem>();
            var module = events.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }
    }
}
