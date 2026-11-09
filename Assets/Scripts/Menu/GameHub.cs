using System;
using DriveSim.User;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DriveSim.Menu
{
    // Accueil, missions, véhicules et gains.
    // Écouter PlayRequested(mode, véhicule), puis appeler ReportResult.
    // mode : 0 Classic, 1 Capture the flag, 2 Slalom, 3 Fee Cash.
        // véhicule : 1 Avion, 2 Bateau, 3 Vélo, 4 Voiture.
    public sealed class GameHub : MonoBehaviour
    {
        public static readonly string[] ModeNames = { "Classic", "Capture the flag", "Slalom", "Fee Cash" };

        static readonly string[] ModeTexts =
        {
            "Course contre la montre. Enchaîne les virages et bats ton meilleur temps.",
            "Prends le drapeau et ramène-le sans quitter la piste.",
            "Passe les portes dans l'ordre. Chaque porte manquée coûte du temps.",
            "Ramasse un maximum de pièces avant la fin du chrono."
        };

        static readonly Page[] NavPages = { Page.Shop, Page.Settings, Page.Board, Page.Success };
        static readonly string[] NavLabels = { "BOUTIQUE", "RÉGLAGES", "CLASSEMENT", "SUCCÈS" };
        static readonly string[] NavIcons = { "shop", "gear", "trophy", "medal" };

        public static GameHub Current { get; private set; }

        // Sans écouteur, Jouer montre un résultat de démonstration.
        // Avec un écouteur, le menu se cache : la piste appelle ReportResult au retour.
        public static event Action<int, int> PlayRequested;

        enum Page
        {
            Home,
            Missions,
            Levels,
            Shop,
            Settings,
            Board,
            Success
        }

        sealed class MissionCard
        {
            public RectTransform Rect;
            public Image Frame;
            public Button Button;
        }

        sealed class LevelCard
        {
            public RectTransform Rect;
            public Image Frame;
            public Image Art;
            public Text Number;
            public Text Caption;
            public Image Lock;
            public Image[] Stars;
            public RectTransform Pin;
            public Button Button;
        }

        sealed class ShopRow
        {
            public Image Frame;
            public Text Action;
            public Button Button;
        }

        CanvasScaler _scaler;
        GarageProgress _progress;
        Page _page = Page.Home;
        int _mode;
        int _level = 1;
        int _levelPage;
        bool _landscape = true;
        bool _results;
        bool _toastOn;
        float _toastUntil;
        Vector2 _appliedSize;
        string _playerName = "Pilote";
        string _email = "";
        string _id = "";

        RectTransform _top;
        HorizontalLayoutGroup _topLayout;
        Text _title;
        Text _stats;
        LayoutElement _titleWidth;
        LayoutElement _statsWidth;
        Button _back;
        RectTransform _blurb;
        Text _blurbText;
        RectTransform _homeRoot;
        Text _helloName;
        Text _helloRank;
        Text _statLevel;
        Text _statRecord;
        Text _statCoins;
        Text _continueLabel;
        Image _featuredArt;
        Text _featuredTitle;
        Text _featuredBody;
        Text _featuredLevel;
        RectTransform _helloPanel;
        RectTransform _featuredPanel;
        RectTransform _missionRoot;
        MissionCard[] _missions;
        RectTransform _levelRoot;
        LevelCard[] _levels;
        RectTransform[] _lines;
        RectTransform _extraRoot;
        GameObject _shop;
        GameObject _settings;
        GameObject _board;
        GameObject _success;
        ShopRow[] _shopRows;
        Text[] _boardScore;
        Text[] _boardTime;
        Image[] _goalMarks;
        Text _accountName;
        Text _accountMail;
        Text _accountId;
        Image _musicTrack;
        RectTransform _musicKnob;
        Image _sfxTrack;
        RectTransform _sfxKnob;
        bool _music = true;
        bool _sfx = true;
        Image _faceTrack;
        RectTransform _faceKnob;
        GameObject _faceRow;
        Text _faceNote;
        bool _faceOn;
        Action<bool> _onFace;
        RectTransform _bottom;
        Button[] _navButtons;
        Image[] _navIcons;
        Text[] _navLabelsUi;
        GameObject _playGo;
        Text _playLabel;
        RectTransform _arrowL;
        RectTransform _arrowR;
        Button _arrowLButton;
        Button _arrowRButton;
        GameObject _resultRoot;
        RectTransform _resultColumn;
        LayoutElement _resultPanelWidth;
        LayoutElement _resultButtonWidth;
        Text _resultSubtitle;
        Text _gainValue;
        Text _timeValue;
        Text _recordValue;
        Image[] _resultStars;
        GameObject _toastRoot;
        Text _toastText;
        Button _logout;

        public static GameHub Create()
        {
            var root = new GameObject("GameHub");
            DontDestroyOnLoad(root);
            var hub = root.AddComponent<GameHub>();
            root.SetActive(false);
            return hub;
        }

        public static void ReportResult(int stars, int score, float seconds, int coinsCollected = 0)
        {
            if (Current != null)
                Current.Receive(stars, score, seconds, coinsCollected);
        }

        public void BindLogout(UnityAction action)
        {
            _logout.onClick.AddListener(action);
        }

        public void ConfigureFace(bool available, bool enabled, Action<bool> onToggle)
        {
            _onFace = onToggle;
            _faceOn = enabled;
            if (_faceRow != null)
                _faceRow.SetActive(available);
            if (_faceNote != null)
                _faceNote.gameObject.SetActive(available);
            if (_faceTrack != null)
                PaintSwitch(_faceTrack, _faceKnob, _faceOn);
        }

        public void SetFace(bool enabled)
        {
            _faceOn = enabled;
            if (_faceTrack != null)
                PaintSwitch(_faceTrack, _faceKnob, _faceOn);
        }

        public void Notify(string message)
        {
            Toast(message);
        }

        public void Open(string displayName, string email, string playerId, string message)
        {
            _playerName = string.IsNullOrEmpty(displayName) ? "Pilote" : displayName;
            _email = email ?? "";
            _id = string.IsNullOrEmpty(playerId) ? "local" : playerId;
            _progress = GarageProgress.Load(_id);
            var cloud = AuthSession.Player;
            if (cloud != null && cloud.Id == _id)
                _progress.ApplyCloud(cloud.Coins, cloud.Level, cloud.Score, cloud.BestPlane, cloud.BestBoat, cloud.BestCar, cloud.BestBike);
            _mode = 0;
            _level = 1;
            _levelPage = 0;
            _page = Page.Home;
            _results = false;
            _toastOn = false;
            _toastRoot.SetActive(false);
            gameObject.SetActive(true);
            ApplyLayout(Screen.width > Screen.height);
            if (!string.IsNullOrEmpty(message))
                Toast(message);
            PushProgress();
        }

        public void Close()
        {
            _progress = null;
            _toastOn = false;
            gameObject.SetActive(false);
        }

        public void ApplyLayout(bool landscape)
        {
            _landscape = landscape;
            if (!isActiveAndEnabled || _progress == null || _scaler == null)
                return;
            _scaler.referenceResolution = landscape ? new Vector2(1920f, 1080f) : new Vector2(1080f, 1920f);
            Canvas.ForceUpdateCanvases();
            var size = ((RectTransform)transform).rect.size;
            if (size.x < 2f || size.y < 2f)
                size = _scaler.referenceResolution;
            _appliedSize = size;
            Refresh();
            Layout(size);
        }

        void Awake()
        {
            Current = this;
            Build();
        }

        void OnDestroy()
        {
            if (Current == this)
                Current = null;
        }

        void Update()
        {
            if (_progress == null)
                return;
            var size = ((RectTransform)transform).rect.size;
            if (size.x > 2f && size.y > 2f && (size - _appliedSize).sqrMagnitude > 4f)
            {
                _appliedSize = size;
                Refresh();
                Layout(size);
            }
            WatchToast();
            WatchKeys();
        }

        void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400;
            _scaler = gameObject.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.referenceResolution = new Vector2(1920f, 1080f);
            _scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            var backdrop = UiKit.Image("Backdrop", transform, Color.white, UiKit.Backdrop(), true);
            UiKit.Stretch(backdrop.rectTransform, 0f, 0f, 0f, 0f);

            _levelRoot = FullRect("Levels");
            _missionRoot = FullRect("Missions");
            _homeRoot = FullRect("Home");
            _extraRoot = UiKit.Rect("Extra", transform);
            BuildLevels();
            BuildMissions();
            BuildHome();
            BuildExtraPages();
            BuildBlurb();
            BuildArrows();
            BuildTop();
            BuildBottom();
            BuildPlay();
            BuildResults();
            BuildToast();
        }

        RectTransform FullRect(string name)
        {
            var rect = UiKit.Rect(name, transform);
            UiKit.Stretch(rect, 0f, 0f, 0f, 0f);
            return rect;
        }

        void BuildTop()
        {
            _top = UiKit.Rect("Top", transform);
            _top.anchorMin = new Vector2(0f, 1f);
            _top.anchorMax = new Vector2(1f, 1f);
            _top.pivot = new Vector2(0.5f, 1f);
            _topLayout = _top.gameObject.AddComponent<HorizontalLayoutGroup>();
            _topLayout.childAlignment = TextAnchor.MiddleLeft;
            _topLayout.spacing = 12f;
            _topLayout.padding = new RectOffset(20, 20, 16, 8);
            _topLayout.childControlWidth = true;
            _topLayout.childControlHeight = true;
            _topLayout.childForceExpandWidth = false;
            _topLayout.childForceExpandHeight = false;

            _back = UiKit.Button(_top, "Back", "‹   RETOUR", UiKit.Cyan, UiKit.Ink, 22, UiKit.Skew(), out _);
            UiKit.Prefer(_back, 176f, 62f);
            _back.onClick.AddListener(Back);

            var banner = UiKit.Image("TitleBanner", _top, UiKit.Cyan, UiKit.Skew(), false);
            _titleWidth = UiKit.Prefer(banner, 440f, 62f);
            _title = UiKit.Label(banner.transform, "ACCUEIL", 26, UiKit.Ink, FontStyle.Bold, TextAnchor.MiddleCenter);
            UiKit.Stretch(_title.rectTransform, 28f, 6f, 28f, 6f);

            var spacer = UiKit.Rect("Spacer", _top);
            var flex = spacer.gameObject.AddComponent<LayoutElement>();
            flex.flexibleWidth = 1f;

            _stats = UiKit.Label(_top, "XP", 24, Color.white, FontStyle.Bold, TextAnchor.MiddleRight);
            _stats.horizontalOverflow = HorizontalWrapMode.Overflow;
            _statsWidth = UiKit.Prefer(_stats, 520f, 62f);

            var coins = UiKit.Button(_top, "Coins", "+ 100", UiKit.Gold, UiKit.Ink, 22, UiKit.Skew(), out _);
            UiKit.Prefer(coins, 168f, 62f);
            coins.onClick.AddListener(GrantCoins);
        }

        void BuildBlurb()
        {
            _blurb = UiKit.Rect("Blurb", transform);
            for (int i = 0; i < 3; i++)
            {
                var stripe = UiKit.Image("Stripe", _blurb, UiKit.Cyan, UiKit.Pixel, false);
                var rect = stripe.rectTransform;
                rect.anchorMin = new Vector2(0f, 0.5f);
                rect.anchorMax = new Vector2(0f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(12f, 46f);
                rect.anchoredPosition = new Vector2(24f + i * 16f, 0f);
                rect.localEulerAngles = new Vector3(0f, 0f, 32f);
            }
            _blurbText = UiKit.Label(_blurb, "", 22, Color.white, FontStyle.Normal, TextAnchor.MiddleLeft);
            _blurbText.verticalOverflow = VerticalWrapMode.Overflow;
            UiKit.Stretch(_blurbText.rectTransform, 88f, 4f, 12f, 4f);
        }

        void BuildArrows()
        {
            _arrowLButton = Arrow("ArrowLeft", true, out _arrowL);
            _arrowRButton = Arrow("ArrowRight", false, out _arrowR);
            _arrowLButton.onClick.AddListener(() => Nudge(-1));
            _arrowRButton.onClick.AddListener(() => Nudge(1));
        }

        Button Arrow(string name, bool flip, out RectTransform rect)
        {
            var image = UiKit.Image(name, transform, UiKit.Cyan, UiKit.Round(20), true, Image.Type.Sliced);
            rect = image.rectTransform;
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            UiKit.Tint(button);
            var icon = UiKit.Image("Mark", image.transform, UiKit.Ink, UiKit.Icon("chevron"), false);
            icon.preserveAspect = true;
            var iconRect = icon.rectTransform;
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(28f, 40f);
            if (flip)
                iconRect.localEulerAngles = new Vector3(0f, 0f, 180f);
            return button;
        }

        void BuildHome()
        {
            var hello = UiKit.Image("Hello", _homeRoot, UiKit.Panel, UiKit.Round(18), true, Image.Type.Sliced);
            _helloPanel = hello.rectTransform;
            var layout = hello.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(36, 36, 32, 28);
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var kicker = UiKit.Label(hello.transform, "BONJOUR", 20, UiKit.Cyan, FontStyle.Bold, TextAnchor.MiddleLeft);
            UiKit.Prefer(kicker, 0f, 28f);
            _helloName = UiKit.Label(hello.transform, "Pilote", 48, Color.white, FontStyle.Bold, TextAnchor.MiddleLeft);
            UiKit.Prefer(_helloName, 0f, 64f);
            _helloRank = UiKit.Label(hello.transform, "Recrue", 24, UiKit.Soft, FontStyle.Normal, TextAnchor.MiddleLeft);
            UiKit.Prefer(_helloRank, 0f, 36f);

            var flex = UiKit.Rect("Flex", hello.transform);
            var flexElement = flex.gameObject.AddComponent<LayoutElement>();
            flexElement.flexibleHeight = 1f;
            flexElement.minHeight = 12f;

            Stat(hello.transform, "NIVEAU", out _statLevel);
            Stat(hello.transform, "RECORD", out _statRecord);
            Stat(hello.transform, "PIÈCES", out _statCoins);

            var flex2 = UiKit.Rect("FlexBottom", hello.transform);
            var flex2Element = flex2.gameObject.AddComponent<LayoutElement>();
            flex2Element.flexibleHeight = 1f;
            flex2Element.minHeight = 8f;

            var continueButton = UiKit.Button(hello.transform, "Continue", "COMMENCER", UiKit.Yellow, UiKit.Ink, 26, UiKit.Skew(), out _continueLabel);
            UiKit.Prefer(continueButton, 0f, 84f);
            continueButton.onClick.AddListener(OpenLevels);

            var featured = UiKit.Image("Featured", _homeRoot, Color.white, UiKit.Round(18), true, Image.Type.Sliced);
            _featuredPanel = featured.rectTransform;
            var featuredButton = featured.gameObject.AddComponent<Button>();
            featuredButton.targetGraphic = featured;
            UiKit.Tint(featuredButton);
            UiKit.Lip(featured.gameObject, new Color(0f, 0f, 0f, 0.45f), 12f);
            featuredButton.onClick.AddListener(OpenLevels);

            _featuredArt = UiKit.Image("Art", featured.transform, Color.white, UiKit.Car(0), false);
            UiKit.Stretch(_featuredArt.rectTransform, 10f, 10f, 10f, 10f);
            var shade = UiKit.Image("Shade", featured.transform, new Color(0.01f, 0.03f, 0.06f, 0.72f), UiKit.Pixel, false);
            var shadeRect = shade.rectTransform;
            shadeRect.anchorMin = Vector2.zero;
            shadeRect.anchorMax = new Vector2(1f, 0f);
            shadeRect.pivot = new Vector2(0.5f, 0f);
            shadeRect.sizeDelta = new Vector2(-20f, 196f);
            shadeRect.anchoredPosition = new Vector2(0f, 10f);

            var bar = UiKit.Image("Bar", featured.transform, UiKit.Cyan, UiKit.Round(10), false, Image.Type.Sliced);
            var barRect = bar.rectTransform;
            barRect.anchorMin = new Vector2(0f, 1f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(0.5f, 1f);
            barRect.offsetMin = new Vector2(10f, -78f);
            barRect.offsetMax = new Vector2(-10f, -10f);
            _featuredTitle = UiKit.Label(bar.transform, "Classic", 28, UiKit.Ink, FontStyle.Bold, TextAnchor.MiddleCenter);
            UiKit.Stretch(_featuredTitle.rectTransform, 12f, 4f, 12f, 4f);

            _featuredLevel = UiKit.Label(featured.transform, "Niveau 1", 22, UiKit.Cyan, FontStyle.Bold, TextAnchor.MiddleLeft);
            var levelRect = _featuredLevel.rectTransform;
            levelRect.anchorMin = new Vector2(0f, 0f);
            levelRect.anchorMax = new Vector2(1f, 0f);
            levelRect.pivot = new Vector2(0.5f, 0f);
            levelRect.sizeDelta = new Vector2(-56f, 36f);
            levelRect.anchoredPosition = new Vector2(0f, 142f);
            _featuredBody = UiKit.Label(featured.transform, "", 20, Color.white, FontStyle.Normal, TextAnchor.UpperLeft);
            _featuredBody.verticalOverflow = VerticalWrapMode.Overflow;
            var bodyRect = _featuredBody.rectTransform;
            bodyRect.anchorMin = new Vector2(0f, 0f);
            bodyRect.anchorMax = new Vector2(1f, 0f);
            bodyRect.pivot = new Vector2(0.5f, 0f);
            bodyRect.sizeDelta = new Vector2(-56f, 104f);
            bodyRect.anchoredPosition = new Vector2(0f, 28f);
        }

        void Stat(Transform parent, string caption, out Text value)
        {
            var row = UiKit.Rect(caption, parent);
            UiKit.Prefer(row, 0f, 46f);
            var name = UiKit.Label(row, caption, 20, UiKit.Soft, FontStyle.Bold, TextAnchor.MiddleLeft);
            UiKit.Stretch(name.rectTransform, 0f, 0f, 180f, 0f);
            value = UiKit.Label(row, "—", 24, Color.white, FontStyle.Bold, TextAnchor.MiddleRight);
            UiKit.Stretch(value.rectTransform, 140f, 0f, 0f, 0f);
        }

        void BuildMissions()
        {
            _missions = new MissionCard[ModeNames.Length];
            for (int i = 0; i < _missions.Length; i++)
            {
                int mode = i;
                var frame = UiKit.Image("Mode" + i, _missionRoot, Color.white, UiKit.Round(16), true, Image.Type.Sliced);
                var card = new MissionCard();
                card.Rect = frame.rectTransform;
                card.Frame = frame;
                card.Button = frame.gameObject.AddComponent<Button>();
                card.Button.targetGraphic = frame;
                UiKit.Tint(card.Button);
                UiKit.Lip(frame.gameObject, new Color(0f, 0f, 0f, 0.5f), 14f);
                var art = UiKit.Image("Art", frame.transform, Color.white, UiKit.ModeArt(i), false);
                UiKit.Stretch(art.rectTransform, 8f, 8f, 8f, 8f);
                var bar = UiKit.Image("Bar", frame.transform, UiKit.Cyan, UiKit.Round(8), false, Image.Type.Sliced);
                var barRect = bar.rectTransform;
                barRect.anchorMin = new Vector2(0f, 1f);
                barRect.anchorMax = new Vector2(1f, 1f);
                barRect.pivot = new Vector2(0.5f, 1f);
                barRect.offsetMin = new Vector2(8f, -64f);
                barRect.offsetMax = new Vector2(-8f, -8f);
                var title = UiKit.Label(bar.transform, ModeNames[i], 22, UiKit.Ink, FontStyle.Bold, TextAnchor.MiddleCenter);
                UiKit.Stretch(title.rectTransform, 8f, 4f, 8f, 4f);
                card.Button.onClick.AddListener(() => SelectMode(mode));
                _missions[i] = card;
            }
        }

        void BuildLevels()
        {
            _lines = new RectTransform[Mathf.Max(0, GarageProgress.PageSize - 1)];
            for (int i = 0; i < _lines.Length; i++)
            {
                var line = UiKit.Image("Link", _levelRoot, UiKit.Red, UiKit.Pixel, false);
                _lines[i] = line.rectTransform;
            }

            _levels = new LevelCard[GarageProgress.PageSize];
            for (int i = 0; i < _levels.Length; i++)
            {
                int slot = i;
                var frame = UiKit.Image("Level" + i, _levelRoot, Color.white, UiKit.Round(14), true, Image.Type.Sliced);
                var card = new LevelCard();
                card.Rect = frame.rectTransform;
                card.Frame = frame;
                card.Button = frame.gameObject.AddComponent<Button>();
                card.Button.targetGraphic = frame;
                UiKit.Tint(card.Button);
                UiKit.Lip(frame.gameObject, new Color(0f, 0f, 0f, 0.45f), 10f);
                card.Art = UiKit.Image("Art", frame.transform, Color.white, UiKit.Car(i), false);
                UiKit.Stretch(card.Art.rectTransform, 8f, 8f, 8f, 8f);
                card.Number = UiKit.Label(frame.transform, GarageProgress.TransportNames[i], 34, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
                var numberRect = card.Number.rectTransform;
                numberRect.anchorMin = new Vector2(0f, 1f);
                numberRect.anchorMax = new Vector2(1f, 1f);
                numberRect.pivot = new Vector2(0.5f, 1f);
                numberRect.sizeDelta = new Vector2(-16f, 78f);
                numberRect.anchoredPosition = new Vector2(0f, -10f);
                card.Caption = UiKit.Label(frame.transform, "Véhicule", 16, UiKit.Soft, FontStyle.Bold, TextAnchor.MiddleCenter);
                var captionRect = card.Caption.rectTransform;
                captionRect.anchorMin = new Vector2(0f, 1f);
                captionRect.anchorMax = new Vector2(1f, 1f);
                captionRect.pivot = new Vector2(0.5f, 1f);
                captionRect.sizeDelta = new Vector2(-16f, 24f);
                captionRect.anchoredPosition = new Vector2(0f, -78f);

                card.Stars = new Image[3];
                for (int s = 0; s < 3; s++)
                {
                    var star = UiKit.Image("Star", frame.transform, Color.white, UiKit.Star(), false);
                    var starRect = star.rectTransform;
                    starRect.anchorMin = starRect.anchorMax = new Vector2(0.5f, 0f);
                    starRect.pivot = new Vector2(0.5f, 0f);
                    starRect.sizeDelta = new Vector2(22f, 22f);
                    starRect.anchoredPosition = new Vector2((s - 1) * 26f, 14f);
                    card.Stars[s] = star;
                }

                card.Lock = UiKit.Image("Lock", frame.transform, Color.white, UiKit.Icon("lock"), false);
                card.Lock.preserveAspect = true;
                var lockRect = card.Lock.rectTransform;
                lockRect.anchorMin = lockRect.anchorMax = new Vector2(0.5f, 0.5f);
                lockRect.sizeDelta = new Vector2(64f, 64f);
                lockRect.anchoredPosition = new Vector2(0f, -16f);
                card.Button.onClick.AddListener(() => SelectLevelSlot(slot));

                var pin = UiKit.Image("Pin", _levelRoot, UiKit.Red, UiKit.Circle(), false);
                card.Pin = pin.rectTransform;
                card.Pin.anchorMin = card.Pin.anchorMax = new Vector2(0.5f, 0.5f);
                card.Pin.pivot = new Vector2(0.5f, 0.5f);
                card.Pin.sizeDelta = new Vector2(28f, 28f);
                var dot = UiKit.Image("Dot", pin.transform, Color.white, UiKit.Circle(), false);
                var dotRect = dot.rectTransform;
                dotRect.anchorMin = dotRect.anchorMax = new Vector2(0.5f, 0.5f);
                dotRect.sizeDelta = new Vector2(10f, 10f);
                _levels[i] = card;
            }
        }

        void BuildExtraPages()
        {
            _shop = PageScroll("Shop", BuildShop);
            _settings = PageScroll("Settings", BuildSettings);
            _board = PageScroll("Board", BuildBoard);
            _success = PageScroll("Success", BuildGoals);
        }

        GameObject PageScroll(string name, Action<RectTransform> fill)
        {
            var root = UiKit.Rect(name, _extraRoot);
            UiKit.Stretch(root, 0f, 0f, 0f, 0f);
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28f;

            var viewport = UiKit.Rect("Viewport", root);
            UiKit.Stretch(viewport, 0f, 0f, 0f, 0f);
            var mask = viewport.gameObject.AddComponent<Image>();
            mask.sprite = UiKit.Pixel;
            mask.color = new Color(1f, 1f, 1f, 0.01f);
            viewport.gameObject.AddComponent<RectMask2D>();
            scroll.viewport = viewport;

            var content = UiKit.Rect("Content", viewport);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 8, 28);
            layout.spacing = 14f;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            fill(content);
            return root.gameObject;
        }

        void BuildShop(RectTransform content)
        {
            Note(content, "Titres affichés sous ton pseudo, sur l'accueil.", 56f);
            _shopRows = new ShopRow[GarageProgress.TitleCount];
            for (int i = 0; i < _shopRows.Length; i++)
            {
                int title = i;
                var row = Band(content, 108f);
                var caption = UiKit.Label(row, GarageProgress.TitleNames[i], 28, Color.white, FontStyle.Bold, TextAnchor.MiddleLeft);
                UiKit.Stretch(caption.rectTransform, 24f, 16f, 460f, 16f);
                var button = UiKit.Button(row, "Buy", "ÉQUIPER", UiKit.Yellow, UiKit.Ink, 20, UiKit.Skew(), out var action);
                var buttonRect = button.GetComponent<RectTransform>();
                buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(1f, 0.5f);
                buttonRect.pivot = new Vector2(1f, 0.5f);
                buttonRect.sizeDelta = new Vector2(230f, 64f);
                buttonRect.anchoredPosition = new Vector2(-18f, 0f);
                button.onClick.AddListener(() => OnTitle(title));
                _shopRows[i] = new ShopRow
                {
                    Frame = row.GetComponent<Image>(),
                    Action = action,
                    Button = button
                };
            }
        }

        void BuildSettings(RectTransform content)
        {
            Note(content, "Compte", 36f);
            _accountName = Note(content, "Pseudo", 40f);
            _accountMail = Note(content, "E-mail", 40f);
            _accountId = Note(content, "Identifiant", 56f);
            _accountId.verticalOverflow = VerticalWrapMode.Overflow;
            Note(content, "Connexion", 36f);
            SwitchRow(content, "FACE ID", false, out _faceTrack, out _faceKnob, ToggleFace);
            _faceRow = _faceTrack.transform.parent.gameObject;
            _faceNote = Note(content, "Active le visage ici. La question ne revient qu'à la création du compte.", 72f);
            Note(content, "Son", 36f);
            _music = PlayerPrefs.GetInt("drivesim.music", 1) == 1;
            _sfx = PlayerPrefs.GetInt("drivesim.sfx", 1) == 1;
            SwitchRow(content, "MUSIQUE", _music, out _musicTrack, out _musicKnob, ToggleMusic);
            SwitchRow(content, "EFFETS", _sfx, out _sfxTrack, out _sfxKnob, ToggleSfx);
            Note(content, "Ces choix seront utilisés par le son de la course.", 48f);
            _logout = UiKit.Button(content, "Logout", "SE DÉCONNECTER", UiKit.Yellow, UiKit.Ink, 24, UiKit.Skew(), out _);
            UiKit.Prefer(_logout, 0f, 88f);
        }

        void BuildBoard(RectTransform content)
        {
            Note(content, "Meilleur score par transport, enregistré sur ton compte.", 56f);
            int vehicleCount = GarageProgress.TransportNames.Length;
            _boardScore = new Text[vehicleCount];
            _boardTime = new Text[vehicleCount];
            for (int i = 0; i < vehicleCount; i++)
            {
                var row = Band(content, 96f);
                string boardName = GarageProgress.TransportNames[i];
                var name = UiKit.Label(row, boardName, 26, Color.white, FontStyle.Bold, TextAnchor.MiddleLeft);
                UiKit.Stretch(name.rectTransform, 24f, 16f, 420f, 16f);
                _boardScore[i] = UiKit.Label(row, "—", 24, UiKit.Gold, FontStyle.Bold, TextAnchor.MiddleRight);
                var scoreRect = _boardScore[i].rectTransform;
                scoreRect.anchorMin = new Vector2(1f, 0.5f);
                scoreRect.anchorMax = new Vector2(1f, 1f);
                scoreRect.offsetMin = new Vector2(-390f, 0f);
                scoreRect.offsetMax = new Vector2(-24f, -8f);
                _boardTime[i] = UiKit.Label(row, "", 18, UiKit.Soft, FontStyle.Normal, TextAnchor.UpperRight);
                var timeRect = _boardTime[i].rectTransform;
                timeRect.anchorMin = new Vector2(1f, 0f);
                timeRect.anchorMax = new Vector2(1f, 0.5f);
                timeRect.offsetMin = new Vector2(-390f, 8f);
                timeRect.offsetMax = new Vector2(-24f, 0f);
            }
        }

        void BuildGoals(RectTransform content)
        {
            Note(content, "Les succès se débloquent en jouant.", 48f);
            _goalMarks = new Image[8];
            string[] names =
            {
                "Clé en main", "Première pièce", "Première arrivée", "Trois étoiles",
                "Bourse pleine", "Tous les véhicules", "Quatre départs", "Record personnel"
            };
            string[] details =
            {
                "Ouvrir l'accueil du garage", "Recevoir des pièces", "Terminer l'avion", "Obtenir 3 étoiles",
                "Gagner 1 000 pièces au total", "Débloquer tous les véhicules", "Finir l'avion dans chaque mode", "Battre un de tes scores"
            };
            for (int i = 0; i < names.Length; i++)
            {
                var row = Band(content, 86f);
                var mark = UiKit.Image("Mark", row, UiKit.Gold, UiKit.Star(), false);
                var markRect = mark.rectTransform;
                markRect.anchorMin = markRect.anchorMax = new Vector2(0f, 0.5f);
                markRect.pivot = new Vector2(0f, 0.5f);
                markRect.sizeDelta = new Vector2(36f, 36f);
                markRect.anchoredPosition = new Vector2(20f, 0f);
                var label = UiKit.Label(row, names[i] + "\n<size=16>" + details[i] + "</size>", 24, Color.white, FontStyle.Bold, TextAnchor.MiddleLeft);
                label.verticalOverflow = VerticalWrapMode.Overflow;
                UiKit.Stretch(label.rectTransform, 72f, 8f, 16f, 8f);
                _goalMarks[i] = mark;
            }
        }

        Text Note(Transform parent, string value, float height)
        {
            var text = UiKit.Label(parent, value, 22, UiKit.Soft, FontStyle.Normal, TextAnchor.MiddleLeft);
            UiKit.Prefer(text, 0f, height);
            return text;
        }

        RectTransform Band(Transform parent, float height)
        {
            var image = UiKit.Image("Band", parent, new Color(0.04f, 0.09f, 0.14f, 1f), UiKit.Round(14), false, Image.Type.Sliced);
            UiKit.Prefer(image, 0f, height);
            return image.rectTransform;
        }

        void SwitchRow(Transform parent, string caption, bool on, out Image track, out RectTransform knob, UnityAction toggle)
        {
            var row = Band(parent, 86f);
            var label = UiKit.Label(row, caption, 24, Color.white, FontStyle.Bold, TextAnchor.MiddleLeft);
            UiKit.Stretch(label.rectTransform, 24f, 12f, 140f, 12f);
            var trackImage = UiKit.Image("Track", row, UiKit.Cyan, UiKit.Round(16), true, Image.Type.Sliced);
            track = trackImage;
            var trackRect = trackImage.rectTransform;
            trackRect.anchorMin = trackRect.anchorMax = new Vector2(1f, 0.5f);
            trackRect.pivot = new Vector2(1f, 0.5f);
            trackRect.sizeDelta = new Vector2(92f, 42f);
            trackRect.anchoredPosition = new Vector2(-22f, 0f);
            var button = trackImage.gameObject.AddComponent<Button>();
            button.targetGraphic = trackImage;
            UiKit.Tint(button);
            button.onClick.AddListener(toggle);
            var knobImage = UiKit.Image("Knob", trackImage.transform, Color.white, UiKit.Circle(), false);
            knob = knobImage.rectTransform;
            knob.anchorMin = knob.anchorMax = new Vector2(0.5f, 0.5f);
            knob.pivot = new Vector2(0.5f, 0.5f);
            knob.sizeDelta = new Vector2(30f, 30f);
            PaintSwitch(track, knob, on);
        }

        void BuildBottom()
        {
            var bar = UiKit.Image("Bottom", transform, UiKit.Bar, UiKit.Pixel, true);
            _bottom = bar.rectTransform;
            var layout = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(8, 8, 6, 6);
            layout.spacing = 4f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            _navButtons = new Button[NavLabels.Length];
            _navIcons = new Image[NavLabels.Length];
            _navLabelsUi = new Text[NavLabels.Length];
            for (int i = 0; i < NavLabels.Length; i++)
            {
                int index = i;
                var image = UiKit.Image(NavLabels[i], bar.transform, new Color(1f, 1f, 1f, 0.01f), UiKit.Pixel, true);
                var button = image.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                UiKit.Tint(button);
                var column = image.gameObject.AddComponent<VerticalLayoutGroup>();
                column.childAlignment = TextAnchor.MiddleCenter;
                column.spacing = 2f;
                column.padding = new RectOffset(4, 4, 8, 6);
                column.childControlWidth = true;
                column.childControlHeight = true;
                column.childForceExpandWidth = false;
                column.childForceExpandHeight = false;
                var icon = UiKit.Image("Icon", image.transform, Color.white, UiKit.Icon(NavIcons[i]), false);
                icon.preserveAspect = true;
                UiKit.Prefer(icon, 36f, 36f);
                var label = UiKit.Label(image.transform, NavLabels[i], 15, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
                UiKit.Prefer(label, 150f, 24f);
                button.onClick.AddListener(() => Go(NavPages[index]));
                _navButtons[i] = button;
                _navIcons[i] = icon;
                _navLabelsUi[i] = label;
            }
        }

        void BuildPlay()
        {
            var button = UiKit.Button(transform, "Play", "JOUER", UiKit.Green, Color.white, 40, UiKit.Skew(), out _playLabel);
            _playGo = button.gameObject;
            UiKit.Lip(_playGo, new Color(0.05f, 0.35f, 0.1f, 1f), 6f);
            _playLabel.alignment = TextAnchor.MiddleCenter;
            _playLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiKit.Stretch(_playLabel.rectTransform, 78f, 10f, 124f, 10f);
            button.onClick.AddListener(Play);
            for (int i = 0; i < 3; i++)
            {
                var chevron = UiKit.Image("Chev", button.transform, new Color(1f, 1f, 1f, 0.35f), UiKit.Icon("chevron"), false);
                chevron.preserveAspect = true;
                var rect = chevron.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(30f, 46f);
                rect.anchoredPosition = new Vector2(-34f - i * 26f, 0f);
            }
        }

        void BuildResults()
        {
            _resultRoot = new GameObject("Results");
            _resultRoot.transform.SetParent(transform, false);
            var root = _resultRoot.AddComponent<RectTransform>();
            UiKit.Stretch(root, 0f, 0f, 0f, 0f);
            var dim = _resultRoot.AddComponent<Image>();
            dim.sprite = UiKit.Pixel;
            dim.color = new Color(0.012f, 0.03f, 0.07f, 0.78f);
            dim.raycastTarget = true;

            var column = UiKit.Rect("Column", _resultRoot.transform);
            _resultColumn = column;
            column.anchorMin = column.anchorMax = new Vector2(0.5f, 0.5f);
            column.pivot = new Vector2(0.5f, 0.5f);
            var columnLayout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            columnLayout.spacing = 26f;
            columnLayout.childAlignment = TextAnchor.UpperCenter;
            columnLayout.childControlWidth = true;
            columnLayout.childControlHeight = true;
            columnLayout.childForceExpandWidth = true;
            columnLayout.childForceExpandHeight = false;
            var fitter = column.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var panel = UiKit.Image("Panel", column, new Color(0.035f, 0.09f, 0.14f, 0.98f), UiKit.Round(8), false, Image.Type.Sliced);
            _resultPanelWidth = UiKit.Prefer(panel, 1100f, 0f);
            var panelLayout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            panelLayout.padding = new RectOffset(0, 0, 0, 18);
            panelLayout.spacing = 8f;
            panelLayout.childAlignment = TextAnchor.UpperCenter;
            panelLayout.childControlWidth = true;
            panelLayout.childControlHeight = true;
            panelLayout.childForceExpandWidth = true;
            panelLayout.childForceExpandHeight = false;

            var header = UiKit.Image("Header", panel.transform, UiKit.Cyan, UiKit.Pixel, false);
            UiKit.Prefer(header, 0f, 86f);
            var headerText = UiKit.Label(header.transform, "MISSION ACCOMPLIE", 34, UiKit.Ink, FontStyle.Bold, TextAnchor.MiddleCenter);
            UiKit.Stretch(headerText.rectTransform, 16f, 8f, 16f, 8f);

            var inner = UiKit.Rect("Inner", panel.transform);
            var innerLayout = inner.gameObject.AddComponent<VerticalLayoutGroup>();
            innerLayout.padding = new RectOffset(36, 36, 8, 8);
            innerLayout.spacing = 12f;
            innerLayout.childAlignment = TextAnchor.UpperCenter;
            innerLayout.childControlWidth = true;
            innerLayout.childControlHeight = true;
            innerLayout.childForceExpandWidth = true;
            innerLayout.childForceExpandHeight = false;
            _resultSubtitle = UiKit.Label(inner, "", 22, UiKit.Soft, FontStyle.Bold, TextAnchor.MiddleCenter);
            UiKit.Prefer(_resultSubtitle, 0f, 36f);

            var body = UiKit.Rect("Body", inner);
            UiKit.Prefer(body, 0f, 250f);
            var bodyLayout = body.gameObject.AddComponent<HorizontalLayoutGroup>();
            bodyLayout.spacing = 24f;
            bodyLayout.childAlignment = TextAnchor.MiddleCenter;
            bodyLayout.childControlWidth = true;
            bodyLayout.childControlHeight = true;
            bodyLayout.childForceExpandWidth = true;
            bodyLayout.childForceExpandHeight = false;

            var stars = UiKit.Rect("Stars", body);
            var starsLayout = stars.gameObject.AddComponent<VerticalLayoutGroup>();
            starsLayout.spacing = 6f;
            starsLayout.childControlWidth = true;
            starsLayout.childControlHeight = true;
            starsLayout.childForceExpandWidth = true;
            starsLayout.childForceExpandHeight = false;
            var starsWidth = stars.gameObject.AddComponent<LayoutElement>();
            starsWidth.flexibleWidth = 1.1f;
            _resultStars = new Image[20];
            for (int row = 0; row < 4; row++)
                StarRow(stars, row);

            var numbers = UiKit.Rect("Numbers", body);
            var numbersLayout = numbers.gameObject.AddComponent<VerticalLayoutGroup>();
            numbersLayout.spacing = 4f;
            numbersLayout.childAlignment = TextAnchor.MiddleLeft;
            numbersLayout.childControlWidth = true;
            numbersLayout.childControlHeight = true;
            numbersLayout.childForceExpandWidth = true;
            numbersLayout.childForceExpandHeight = false;
            var numbersWidth = numbers.gameObject.AddComponent<LayoutElement>();
            numbersWidth.flexibleWidth = 0.9f;
            Metric(numbers, "GAIN", out _gainValue);
            Metric(numbers, "TEMPS", out _timeValue);
            Metric(numbers, "RECORD", out _recordValue);

            var buttons = UiKit.Rect("Choices", column);
            _resultButtonWidth = UiKit.Prefer(buttons, 1100f, 92f);
            var choices = buttons.gameObject.AddComponent<HorizontalLayoutGroup>();
            choices.spacing = 22f;
            choices.childAlignment = TextAnchor.MiddleCenter;
            choices.childControlWidth = true;
            choices.childControlHeight = true;
            choices.childForceExpandWidth = true;
            choices.childForceExpandHeight = true;
            ResultButton(buttons, "REJOUER", Replay);
            ResultButton(buttons, "SUIVANT", NextLevel);
            ResultButton(buttons, "ACCUEIL", () => Go(Page.Home));
            _resultRoot.SetActive(false);
        }

        void StarRow(Transform parent, int row)
        {
            var line = UiKit.Rect("Gain" + row, parent);
            UiKit.Prefer(line, 0f, 48f);
            var layout = line.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var caption = UiKit.Label(line, "GAIN", 22, Color.white, FontStyle.Bold, TextAnchor.MiddleLeft);
            UiKit.Prefer(caption, 110f, 40f);
            for (int i = 0; i < 5; i++)
            {
                var star = UiKit.Image("Star", line, UiKit.Gold, UiKit.Star(), false);
                UiKit.Prefer(star, 34f, 34f);
                _resultStars[row * 5 + i] = star;
            }
        }

        void Metric(Transform parent, string caption, out Text value)
        {
            var line = UiKit.Rect(caption, parent);
            UiKit.Prefer(line, 0f, 72f);
            var name = UiKit.Label(line, caption, 20, UiKit.Soft, FontStyle.Bold, TextAnchor.MiddleLeft);
            UiKit.Stretch(name.rectTransform, 8f, 36f, 8f, 0f);
            value = UiKit.Label(line, "0", 28, Color.white, FontStyle.Bold, TextAnchor.MiddleLeft);
            UiKit.Stretch(value.rectTransform, 8f, 0f, 8f, 30f);
        }

        void ResultButton(Transform parent, string caption, UnityAction action)
        {
            var button = UiKit.Button(parent, caption, caption, UiKit.Yellow, UiKit.Ink, 24, UiKit.Skew(), out _);
            var element = UiKit.Prefer(button, 0f, 84f);
            element.flexibleWidth = 1f;
            button.onClick.AddListener(action);
        }

        void BuildToast()
        {
            var toast = UiKit.Image("Toast", transform, new Color(0.02f, 0.06f, 0.1f, 0.94f), UiKit.Round(14), false, Image.Type.Sliced);
            _toastRoot = toast.gameObject;
            _toastText = UiKit.Label(toast.transform, "", 22, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            _toastText.verticalOverflow = VerticalWrapMode.Overflow;
            UiKit.Stretch(_toastText.rectTransform, 18f, 8f, 18f, 8f);
            _toastRoot.SetActive(false);
        }

        void Layout(Vector2 size)
        {
            float left = SafeLeft(size);
            float right = SafeRight(size);
            float topHeight = _landscape ? 96f : 86f;
            _top.offsetMin = new Vector2(0f, -topHeight);
            _top.offsetMax = Vector2.zero;
            _topLayout.padding = new RectOffset(Mathf.RoundToInt(left), Mathf.RoundToInt(right), _landscape ? 16 : 12, 8);
            _titleWidth.preferredWidth = _landscape ? 440f : 230f;
            _statsWidth.preferredWidth = _landscape ? 520f : 280f;
            _title.fontSize = _landscape ? 26 : 18;
            _stats.fontSize = _landscape ? 24 : 18;
            _playLabel.fontSize = _landscape ? 40 : 30;

            float blurbY = topHeight + 6f;
            float blurbH = _landscape ? 64f : 88f;
            float margin = _landscape ? Mathf.Max(left, size.x * 0.12f) : left;
            PlaceTop(_blurb, blurbY, blurbH, margin, _landscape ? margin : right);
            _blurbText.fontSize = _landscape ? 22 : 20;

            float arrowW = 62f;
            float contentLeft = left + arrowW + 18f;
            float contentRight = right + arrowW + 18f;
            float arrowY = _landscape ? size.y * 0.52f : size.y * 0.46f;
            PlaceCenter(_arrowL, left + arrowW * 0.5f, arrowY, arrowW, 112f);
            PlaceCenter(_arrowR, size.x - right - arrowW * 0.5f, arrowY, arrowW, 112f);

            float bottomReserve = _landscape ? 130f : 250f;
            if (_landscape)
            {
                AnchorBottom(_bottom, 0f, 0.70f, 0f, 104f);
                AnchorBottom(_playGo.GetComponent<RectTransform>(), 0.70f, 1f, 0f, 122f);
            }
            else
            {
                AnchorBottom(_bottom, 0f, 1f, 0f, 118f);
                var play = _playGo.GetComponent<RectTransform>();
                play.anchorMin = new Vector2(0f, 0f);
                play.anchorMax = new Vector2(1f, 0f);
                play.pivot = new Vector2(0.5f, 0f);
                play.offsetMin = new Vector2(left, 128f);
                play.offsetMax = new Vector2(-right, 236f);
            }

            float contentTop = blurbY + blurbH + 16f;
            if (_landscape)
            {
                float gap = 28f;
                float innerH = size.y - contentTop - bottomReserve;
                float innerW = size.x - contentLeft - contentRight;
                float leftW = innerW * 0.46f;
                Place(_helloPanel, contentLeft, contentTop, leftW, innerH);
                float cardH = innerH;
                float cardW = Mathf.Min(innerW - leftW - gap, cardH * 0.78f);
                float cardX = contentLeft + leftW + gap + (innerW - leftW - gap - cardW) * 0.5f;
                Place(_featuredPanel, cardX, contentTop, cardW, cardH);
            }
            else
            {
                float innerW = size.x - contentLeft - contentRight;
                float avail = size.y - contentTop - bottomReserve;
                float helloH = Mathf.Max(280f, avail * 0.42f);
                Place(_helloPanel, contentLeft, contentTop, innerW, helloH);
                float cardTop = contentTop + helloH + 16f;
                float maxH = Mathf.Max(220f, size.y - bottomReserve - cardTop);
                float cardW = Mathf.Min(innerW * 0.78f, maxH * 0.78f);
                float cardH = Mathf.Min(maxH, cardW / 0.78f);
                Place(_featuredPanel, contentLeft + (innerW - cardW) * 0.5f, cardTop, cardW, cardH);
            }

            Place(_extraRoot, contentLeft, contentTop, size.x - contentLeft - contentRight, size.y - contentTop - bottomReserve);
            LayoutMissions(size, contentTop, bottomReserve, contentLeft, contentRight);
            LayoutLevels(size, contentTop, bottomReserve, contentLeft, contentRight);

            var toast = _toastRoot.GetComponent<RectTransform>();
            toast.anchorMin = toast.anchorMax = new Vector2(0.5f, 1f);
            toast.pivot = new Vector2(0.5f, 1f);
            toast.sizeDelta = new Vector2(Mathf.Min(size.x - left - right, 760f), 72f);
            toast.anchoredPosition = new Vector2(0f, -(topHeight + 8f));

            float panelWidth = Mathf.Min(size.x - left - right, _landscape ? 1180f : size.x - 24f);
            _resultPanelWidth.preferredWidth = panelWidth;
            _resultButtonWidth.preferredWidth = panelWidth;
            if (_resultRoot.activeSelf)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_resultColumn);
            if (_extraRoot.gameObject.activeSelf)
                LayoutRebuilder.ForceRebuildLayoutImmediate(_extraRoot);
        }

        void LayoutMissions(Vector2 size, float top, float bottomReserve, float left, float right)
        {
            int count = _missions.Length;
            if (_landscape)
            {
                float cardW = Mathf.Clamp(size.x * 0.15f, 200f, 280f);
                float cardH = cardW * 1.32f;
                float step = cardW * 0.84f;
                float total = cardW + step * (count - 1);
                float start = (size.x - total) * 0.5f + cardW * 0.5f;
                float centerY = top + (size.y - top - bottomReserve) * 0.52f;
                for (int i = 0; i < count; i++)
                {
                    bool selected = i == _mode;
                    float y = centerY - (selected ? cardH * 0.06f : 0f);
                    PlaceCenter(_missions[i].Rect, start + i * step, y, cardW, cardH);
                    float tilt = (i - 1.5f) * 5.5f;
                    _missions[i].Rect.localEulerAngles = new Vector3(0f, 0f, selected ? -tilt * 0.2f : -tilt);
                    float scale = selected ? 1.07f : 1f;
                    _missions[i].Rect.localScale = new Vector3(scale, scale, 1f);
                }
            }
            else
            {
                float cardW = (size.x - left - right - 20f) * 0.5f;
                float avail = size.y - top - bottomReserve - 16f;
                float cardH = Mathf.Min(cardW * 1.15f, (avail - 16f) * 0.5f);
                float startX = left + cardW * 0.5f;
                float startY = top + cardH * 0.5f;
                for (int i = 0; i < count; i++)
                {
                    int column = i % 2;
                    int row = i / 2;
                    PlaceCenter(_missions[i].Rect, startX + column * (cardW + 20f), startY + row * (cardH + 16f), cardW, cardH);
                    _missions[i].Rect.localEulerAngles = Vector3.zero;
                    float scale = i == _mode ? 1.03f : 1f;
                    _missions[i].Rect.localScale = new Vector3(scale, scale, 1f);
                }
            }
            _missions[_mode].Rect.SetAsLastSibling();
        }

        void LayoutLevels(Vector2 size, float top, float bottomReserve, float left, float right)
        {
            float avail = size.y - top - bottomReserve;
            for (int i = 0; i < _levels.Length; i++)
            {
                float cx;
                float cy;
                float cardW;
                float cardH;
                int count = Mathf.Max(1, _levels.Length);
                if (_landscape)
                {
                    cardW = Mathf.Clamp(size.x / (count + 1.6f), 118f, 190f);
                    cardH = cardW * 1.38f;
                    float t = count == 1 ? 0.5f : i / (float)(count - 1);
                    cx = Mathf.Lerp(left + cardW * 0.7f, size.x - right - cardW * 0.7f, t);
                    float wave = i % 2 == 0 ? 0.08f : 0.72f;
                    if (i == count - 2)
                        wave = 0.48f;
                    cy = top + cardH * 0.55f + wave * Mathf.Max(24f, avail - cardH - 20f);
                    _levels[i].Rect.localEulerAngles = new Vector3(0f, 0f, i % 2 == 0 ? -6f : 5f);
                }
                else
                {
                    float gap = 6f;
                    cardH = Mathf.Max(88f, (avail - gap * (count - 1)) / count);
                    cardW = Mathf.Min(size.x * 0.4f, cardH * 0.86f);
                    cx = i % 2 == 0 ? left + cardW * 0.65f : size.x - right - cardW * 0.65f;
                    cy = top + cardH * 0.5f + i * (cardH + gap);
                    _levels[i].Rect.localEulerAngles = new Vector3(0f, 0f, i % 2 == 0 ? -4f : 4f);
                }
                int level = _levelPage * GarageProgress.PageSize + i + 1;
                float scale = level == _level ? 1.06f : 1f;
                _levels[i].Rect.localScale = new Vector3(scale, scale, 1f);
                PlaceCenter(_levels[i].Rect, cx, cy, cardW, cardH);
            }

            Canvas.ForceUpdateCanvases();
            int selectedSlot = _level - _levelPage * GarageProgress.PageSize - 1;
            if (selectedSlot >= 0 && selectedSlot < _levels.Length)
                _levels[selectedSlot].Rect.SetAsLastSibling();
            for (int i = 0; i < _levels.Length; i++)
            {
                var corners = new Vector3[4];
                _levels[i].Rect.GetWorldCorners(corners);
                _levels[i].Pin.position = (corners[1] + corners[2]) * 0.5f;
            }
            for (int i = 0; i < _lines.Length; i++)
                PlaceLine(_lines[i], _levels[i].Pin, _levels[i + 1].Pin);
            for (int i = 0; i < _levels.Length; i++)
                _levels[i].Pin.SetAsLastSibling();
        }

        void Refresh()
        {
            if (_progress == null)
                return;
            bool modal = _results;
            bool extra = _page == Page.Shop || _page == Page.Settings || _page == Page.Board || _page == Page.Success;
            bool travel = _page == Page.Home || _page == Page.Missions || _page == Page.Levels;
            _resultRoot.SetActive(modal);
            _top.gameObject.SetActive(!modal);
            _bottom.gameObject.SetActive(!modal);
            _playGo.SetActive(!modal && travel);
            _blurb.gameObject.SetActive(!modal && travel && !_toastOn);
            _arrowL.gameObject.SetActive(!modal && travel);
            _arrowR.gameObject.SetActive(!modal && travel);
            _homeRoot.gameObject.SetActive(!modal && _page == Page.Home);
            _missionRoot.gameObject.SetActive(!modal && _page == Page.Missions);
            _levelRoot.gameObject.SetActive(!modal && _page == Page.Levels);
            _extraRoot.gameObject.SetActive(!modal && extra);
            _shop.SetActive(_page == Page.Shop);
            _settings.SetActive(_page == Page.Settings);
            _board.SetActive(_page == Page.Board);
            _success.SetActive(_page == Page.Success);
            _back.gameObject.SetActive(_page != Page.Home);
            _title.text = TitleOf(_page);
            RefreshStats();
            RefreshNav();
            RefreshBlurb();
            RefreshHome();
            RefreshMissionFrames();
            RefreshLevelCards();
            RefreshShop();
            RefreshBoard();
            RefreshGoals();
            RefreshAccount();
            _arrowLButton.interactable = _page != Page.Levels || _levelPage > 0;
            _arrowRButton.interactable = _page != Page.Levels || _levelPage < GarageProgress.PageCount - 1;
        }

        void RefreshStats()
        {
            _stats.text = "<color=#2ED6DB>XP</color>  " + Num(_progress.Xp) + "     " + Num(_progress.Coins);
        }

        void RefreshNav()
        {
            for (int i = 0; i < _navIcons.Length; i++)
            {
                bool active = _page == NavPages[i];
                var color = active ? UiKit.Cyan : Color.white;
                _navIcons[i].color = color;
                _navLabelsUi[i].color = active ? UiKit.Cyan : new Color(0.82f, 0.88f, 0.94f, 1f);
            }
        }

        void RefreshBlurb()
        {
            if (_page == Page.Home)
            {
                _blurbText.text = ModeNames[_mode] + "    ·    Avion, bateau, vélo, voiture, sous-marin.";
                return;
            }
            if (_page == Page.Levels)
            {
                _blurbText.text = ModeNames[_mode] + "    ·    " + GarageProgress.TransportName(_level);
                return;
            }
            _blurbText.text = ModeTexts[_mode];
        }

        void RefreshHome()
        {
            bool started = _progress.Unlocked(_mode) > 1 || _progress.Stars(_mode, 1) > 0;
            _helloName.text = _playerName;
            _helloRank.text = GarageProgress.TitleNames[_progress.Title];
            _continueLabel.text = started ? "CONTINUER" : "COMMENCER";
            _statLevel.text = _progress.Unlocked(_mode) + " / " + GarageProgress.LevelCount;
            int best = _progress.BestScore(_mode);
            _statRecord.text = best > 0 ? Num(best) : "—";
            _statCoins.text = Num(_progress.Coins);
            _featuredArt.sprite = UiKit.Car(Mathf.Clamp(_level - 1, 0, GarageProgress.LevelCount - 1));
            _featuredTitle.text = GarageProgress.TransportName(_level);
            _featuredBody.text = "1  Avion\n2  Bateau\n3  Velo\n4  Voiture\n5  Sous-marin";
            _featuredLevel.text = "Avion  •  Bateau  •  Velo  •  Voiture  •  Sous-marin";
        }

        void RefreshMissionFrames()
        {
            for (int i = 0; i < _missions.Length; i++)
                _missions[i].Frame.color = i == _mode ? UiKit.Cyan : Color.white;
        }

        void RefreshLevelCards()
        {
            for (int i = 0; i < _levels.Length; i++)
            {
                int level = _levelPage * GarageProgress.PageSize + i + 1;
                bool open = _progress.IsUnlocked(_mode, level);
                bool selected = level == _level;
                var card = _levels[i];
                string name = GarageProgress.TransportName(level);
                card.Number.text = name;
                card.Number.fontSize = name.Length > 6 ? 28 : 34;
                card.Number.color = open ? Color.white : new Color(0.82f, 0.84f, 0.88f, 1f);
                if (card.Caption != null)
                {
                    int record = open ? _progress.BestScoreAt(_mode, level) : 0;
                    card.Caption.text = !open
                        ? "Verrouillé"
                        : record > 0 ? "Record  " + Num(record) : GarageProgress.TransportHint(level);
                }
                card.Frame.color = !open ? new Color(0.62f, 0.66f, 0.72f, 1f) : selected ? UiKit.Cyan : Color.white;
                card.Art.sprite = UiKit.Car(level - 1);
                card.Art.color = open ? Color.white : new Color(0.4f, 0.44f, 0.5f, 1f);
                card.Lock.gameObject.SetActive(!open);
                int stars = _progress.Stars(_mode, level);
                for (int s = 0; s < card.Stars.Length; s++)
                    card.Stars[s].color = s < stars ? UiKit.Gold : new Color(1f, 1f, 1f, 0.22f);
            }
        }

        void RefreshShop()
        {
            if (_shopRows == null)
                return;
            for (int i = 0; i < _shopRows.Length; i++)
            {
                bool owns = _progress.Owns(i);
                bool equipped = owns && _progress.Title == i;
                var row = _shopRows[i];
                row.Frame.color = equipped ? new Color(0.06f, 0.16f, 0.2f, 1f) : new Color(0.04f, 0.09f, 0.14f, 1f);
                row.Button.interactable = !equipped;
                if (equipped)
                    row.Action.text = "ÉQUIPÉ";
                else if (owns)
                    row.Action.text = "ÉQUIPER";
                else if (GarageProgress.TitlePrice[i] <= 0)
                    row.Action.text = "PRENDRE";
                else
                    row.Action.text = Num(GarageProgress.TitlePrice[i]);
            }
        }

        void RefreshBoard()
        {
            if (_boardScore == null)
                return;
            for (int i = 0; i < _boardScore.Length; i++)
            {
                int best = _progress.BestScoreForVehicle(i + 1);
                _boardScore[i].text = best > 0 ? Num(best) : "—";
                _boardTime[i].text = best > 0 ? "Record conservé" : "Pas encore de record";
            }
        }

        void RefreshGoals()
        {
            if (_goalMarks == null || _progress == null)
                return;
            bool[] done =
            {
                true,
                _progress.Lifetime > 0,
                _progress.Missions > 0,
                _progress.BestStars >= 3,
                _progress.Lifetime >= 1000,
                _progress.ReachedFifth(),
                _progress.ClearedEveryMode(),
                _progress.Records > 0
            };
            for (int i = 0; i < _goalMarks.Length && i < done.Length; i++)
                _goalMarks[i].color = done[i] ? UiKit.Gold : new Color(1f, 1f, 1f, 0.18f);
        }

        void RefreshAccount()
        {
            if (_accountName == null)
                return;
            _accountName.text = "Pseudo    " + _playerName;
            _accountMail.text = "E-mail    " + _email;
            _accountId.text = "Identifiant    " + _id;
        }

        void Go(Page page)
        {
            _page = page;
            _results = false;
            var size = ((RectTransform)transform).rect.size;
            if (size.x < 2f)
                size = _appliedSize.x > 2f ? _appliedSize : _scaler.referenceResolution;
            _appliedSize = size;
            Refresh();
            Layout(size);
        }

        void Back()
        {
            if (_results)
            {
                _results = false;
                Go(_page);
                return;
            }
            if (_page != Page.Home)
                Go(Page.Home);
        }

        void Play()
        {
            if (_results)
                return;
            if (_page == Page.Home)
            {
                _level = 1;
                _levelPage = 0;
                Go(Page.Levels);
                return;
            }
            if (_page == Page.Missions)
            {
                OpenLevels();
                return;
            }
            if (_page == Page.Levels)
                StartRace();
        }

        void Nudge(int direction)
        {
            if (_results || _progress == null)
                return;
            if (_page != Page.Home && _page != Page.Missions && _page != Page.Levels)
                return;
            if (_page == Page.Levels)
            {
                int next = Mathf.Clamp(_levelPage + direction, 0, GarageProgress.PageCount - 1);
                if (next == _levelPage)
                    return;
                _levelPage = next;
                int first = _levelPage * GarageProgress.PageSize + 1;
                int last = first + GarageProgress.PageSize - 1;
                if (_level < first || _level > last)
                {
                    int pick = first;
                    for (int level = first; level <= last; level++)
                    {
                        if (_progress.IsUnlocked(_mode, level))
                            pick = level;
                    }
                    _level = pick;
                }
                Go(Page.Levels);
                return;
            }
            _mode = (_mode + direction + GarageProgress.ModeCount) % GarageProgress.ModeCount;
            Go(_page);
        }

        void SelectMode(int mode)
        {
            if (mode == _mode)
            {
                OpenLevels();
                return;
            }
            _mode = mode;
            Go(Page.Missions);
        }

        void OpenLevels()
        {
            _level = _progress.Unlocked(_mode);
            _levelPage = (_level - 1) / GarageProgress.PageSize;
            Go(Page.Levels);
        }

        void SelectLevelSlot(int slot)
        {
            int level = _levelPage * GarageProgress.PageSize + slot + 1;
            if (!_progress.IsUnlocked(_mode, level))
            {
                Toast("Véhicule verrouillé. Termine le précédent.");
                return;
            }
            if (level == _level)
            {
                StartRace();
                return;
            }
            _level = level;
            Go(Page.Levels);
        }

        void StartRace()
        {
            if (_progress == null || !_progress.IsUnlocked(_mode, _level))
            {
                Toast("Véhicule verrouillé. Termine le précédent.");
                return;
            }
            if (!GarageProgress.IsPlayable(_level))
            {
                Toast(GarageProgress.TransportName(_level) + " arrive bientôt.");
                return;
            }
            if (PlayRequested != null)
            {
                PlayRequested.Invoke(_mode, _level);
                gameObject.SetActive(false);
                return;
            }
            int stars = _progress.PreviewStars(_mode, _level);
            int score = 80000 * _level * stars + _progress.Attempts(_mode, _level) * 2500;
            float time = 68f + _level * 3.2f - stars * 5f;
            Receive(stars, score, time, 15 * stars);
        }

        void Receive(int stars, int score, float seconds, int coinsCollected = 0)
        {
            gameObject.SetActive(true);
            if (_progress == null)
                return;
            if (stars <= 0)
            {
                _progress.BankRun(_mode, _level, score, coinsCollected);
                _results = false;
                Go(Page.Levels);
                Toast(coinsCollected > 0
                    ? "Course terminée · +" + Num(coinsCollected) + " pièces"
                    : "Course abandonnée");
                PushProgress();
                return;
            }
            var outcome = _progress.Complete(_mode, _level, stars, score, seconds, coinsCollected);
            _results = true;
            FillResult(stars, score, seconds, outcome);
            var size = ((RectTransform)transform).rect.size;
            if (size.x < 2f)
                size = _appliedSize;
            _appliedSize = size;
            Refresh();
            Layout(size);
            PushProgress();
        }

        void FillResult(int stars, int score, float seconds, GarageProgress.Outcome outcome)
        {
            string tail = outcome.NewRecord ? "   ·   Nouveau record" : outcome.FirstClear ? "   ·   Véhicule réussi" : "";
            _resultSubtitle.text = ModeNames[_mode] + "   ·   " + GarageProgress.TransportName(_level)
                + "   ·   +" + Num(outcome.Reward) + " pièces" + tail;
            _gainValue.text = Num(score);
            _timeValue.text = Clock(seconds);
            _recordValue.text = Num(outcome.BestScore);
            for (int row = 0; row < 4; row++)
            {
                int filled = stars;
                if (row == 1)
                    filled = Mathf.Clamp(stars + 1, 1, 5);
                else if (row == 3)
                    filled = Mathf.Clamp(stars - 1, 1, 5);
                for (int i = 0; i < 5; i++)
                    _resultStars[row * 5 + i].color = i < filled ? UiKit.Gold : new Color(1f, 1f, 1f, 0.22f);
            }
        }

        void Replay()
        {
            _results = false;
            StartRace();
        }

        void NextLevel()
        {
            if (_level >= GarageProgress.LevelCount)
            {
                Toast("Dernier véhicule de ce mode.");
                return;
            }
            if (!_progress.IsUnlocked(_mode, _level + 1))
            {
                Toast("Véhicule suivant encore verrouillé.");
                return;
            }
            _level += 1;
            _levelPage = (_level - 1) / GarageProgress.PageSize;
            Go(Page.Levels);
        }

        void GrantCoins()
        {
            if (_progress == null)
                return;
            _progress.AddCoins(100);
            RefreshStats();
            RefreshHome();
            RefreshShop();
            Toast("+ 100 pièces");
            PushProgress();
        }

        void OnTitle(int title)
        {
            if (_progress == null)
                return;
            if (_progress.Owns(title) && _progress.Title == title)
                return;
            if (!_progress.BuyOrEquip(title))
            {
                Toast("Pas assez de pièces.");
                return;
            }
            RefreshStats();
            RefreshHome();
            RefreshShop();
            Toast("Titre équipé.");
            PushProgress();
        }

        async void PushProgress()
        {
            if (_progress == null || !AuthSession.IsSignedIn)
                return;
            try
            {
                var result = await AuthApiClient.UpdateProgress(
                    AuthConfig.DefaultServerUrl,
                    AuthSession.AccessToken,
                    _progress.Coins,
                    _progress.CloudLevel,
                    _progress.CloudScore,
                    _progress.BestScoreForVehicle(1),
                    _progress.BestScoreForVehicle(2),
                    _progress.BestScoreForVehicle(4),
                    _progress.BestScoreForVehicle(3));
                if (result.Ok && result.Player != null)
                    AuthSession.UpdateProgress(
                        result.Player.Coins,
                        result.Player.Level,
                        result.Player.Score,
                        result.Player.BestPlane,
                        result.Player.BestBoat,
                        result.Player.BestCar,
                        result.Player.BestBike);
            }
            catch (Exception)
            {
                // La progression reste locale si le serveur est indisponible.
            }
        }

        void ToggleFace()
        {
            _faceOn = !_faceOn;
            PaintSwitch(_faceTrack, _faceKnob, _faceOn);
            if (_onFace != null)
                _onFace(_faceOn);
        }

        void ToggleMusic()
        {
            _music = !_music;
            PlayerPrefs.SetInt("drivesim.music", _music ? 1 : 0);
            PlayerPrefs.Save();
            PaintSwitch(_musicTrack, _musicKnob, _music);
        }

        void ToggleSfx()
        {
            _sfx = !_sfx;
            PlayerPrefs.SetInt("drivesim.sfx", _sfx ? 1 : 0);
            PlayerPrefs.Save();
            PaintSwitch(_sfxTrack, _sfxKnob, _sfx);
        }

        static void PaintSwitch(Image track, RectTransform knob, bool on)
        {
            track.color = on ? UiKit.Cyan : new Color(0.14f, 0.18f, 0.24f, 1f);
            knob.anchoredPosition = new Vector2(on ? 22f : -22f, 0f);
        }

        void Toast(string message)
        {
            _toastText.text = message ?? "";
            _toastOn = true;
            _toastRoot.SetActive(true);
            _toastRoot.transform.SetAsLastSibling();
            _blurb.gameObject.SetActive(false);
            _toastUntil = Time.unscaledTime + 2.6f;
        }

        void WatchToast()
        {
            if (_toastOn && Time.unscaledTime >= _toastUntil)
            {
                _toastOn = false;
                _toastRoot.SetActive(false);
                Refresh();
            }
        }

        void WatchKeys()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;
            if (keyboard.escapeKey.wasPressedThisFrame)
                Back();
            else if (keyboard.leftArrowKey.wasPressedThisFrame)
                Nudge(-1);
            else if (keyboard.rightArrowKey.wasPressedThisFrame)
                Nudge(1);
        }

        static string TitleOf(Page page)
        {
            switch (page)
            {
                case Page.Home: return "ACCUEIL";
                case Page.Missions: return "MISSIONS";
                case Page.Levels: return "VÉHICULES";
                case Page.Shop: return "BOUTIQUE";
                case Page.Settings: return "RÉGLAGES";
                case Page.Board: return "CLASSEMENT";
                case Page.Success: return "SUCCÈS";
                default: return "DRIVE SIM";
            }
        }

        static string Num(int value)
        {
            int number = Mathf.Abs(value);
            string digits = number.ToString();
            var text = "";
            for (int i = 0; i < digits.Length; i++)
            {
                if (i > 0 && (digits.Length - i) % 3 == 0)
                    text += " ";
                text += digits[i];
            }
            return value < 0 ? "-" + text : text;
        }

        static string Clock(float seconds)
        {
            int total = Mathf.Max(0, Mathf.RoundToInt(seconds));
            return (total / 60) + ":" + (total % 60).ToString("00");
        }

        float SafeLeft(Vector2 size)
        {
            if (Screen.width <= 0)
                return 18f;
            return Mathf.Max(18f, Screen.safeArea.xMin * size.x / Screen.width);
        }

        float SafeRight(Vector2 size)
        {
            if (Screen.width <= 0)
                return 18f;
            return Mathf.Max(18f, (Screen.width - Screen.safeArea.xMax) * size.x / Screen.width);
        }

        static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        static void PlaceCenter(RectTransform rect, float cx, float cy, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(cx, -cy);
            rect.sizeDelta = new Vector2(width, height);
        }

        static void PlaceTop(RectTransform rect, float y, float height, float left, float right)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -(y + height));
            rect.offsetMax = new Vector2(-right, -y);
        }

        static void AnchorBottom(RectTransform rect, float xMin, float xMax, float y, float height)
        {
            rect.anchorMin = new Vector2(xMin, 0f);
            rect.anchorMax = new Vector2(xMax, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(0f, y);
            rect.offsetMax = new Vector2(0f, y + height);
        }

        static void PlaceLine(RectTransform line, RectTransform a, RectTransform b)
        {
            var parent = line.parent as RectTransform;
            var start = (Vector2)parent.InverseTransformPoint(a.position);
            var end = (Vector2)parent.InverseTransformPoint(b.position);
            var delta = end - start;
            line.anchorMin = line.anchorMax = new Vector2(0.5f, 0.5f);
            line.pivot = new Vector2(0.5f, 0.5f);
            line.sizeDelta = new Vector2(delta.magnitude, 5f);
            line.anchoredPosition = (start + end) * 0.5f;
            line.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }
    }
}
