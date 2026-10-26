using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DriveSim.User
{
    // Interface du compte, posée par-dessus le garage 3D (AuthBackdrop).
    // Verre dépoli, dégradés, halos animés, spinner, entrée animée.
    public sealed class AuthScreen : MonoBehaviour
    {
        static readonly Color Navy = new Color(0.04f, 0.11f, 0.27f);
        static readonly Color Coral = new Color(1f, 0.42f, 0.38f);
        static readonly Color Mint = new Color(0.3f, 0.92f, 0.78f);
        static readonly Color Cyan = new Color(0.2f, 0.85f, 1f);
        static readonly Color Ink = new Color(0.06f, 0.13f, 0.28f);
        static readonly Color Soft = new Color(0.72f, 0.82f, 0.95f);

        static Font _font;
        static Sprite _flat;
        static Sprite _pill;
        static Sprite _gradient;
        static Sprite _glow;
        static Sprite _vignette;
        static Sprite _ring;

        RectTransform _header;
        RectTransform _card;
        CanvasGroup _rootGroup;
        RectTransform _cardRoot;
        GameObject _busyOverlay;
        CanvasGroup _busyGroup;
        Image _spinner;
        Image[] _orbs;
        Vector2[] _orbAnchors;
        float _orbTime;

        public RectTransform Content;
        public Text Status;
        public InputField Email;
        public InputField Password;
        public InputField DisplayName;
        public GameObject NameRow;
        public Button BiometricButton;
        public Text BiometricLabel;
        public Button PrimaryButton;
        public Text PrimaryLabel;
        public Button SwitchButton;
        public Text SwitchLabel;
        public Button PracticeButton;
        public Text PracticeLabel;
        public GameObject SimPanel;
        public Button SimSuccessButton;
        public Button SimFailButton;
        public GameObject EnablePanel;
        public Button EnableYesButton;
        public Button EnableNoButton;
        public GameObject HomePanel;
        public Text Hello;
        public Text Details;
        public Button PlayButton;
        public Text PlayLabel;
        public Button LogoutButton;
        public Selectable[] LockDuringRequest;

        public void Construct()
        {
            gameObject.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            gameObject.GetComponent<Canvas>().sortingOrder = 200;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            _rootGroup = gameObject.AddComponent<CanvasGroup>();
            _rootGroup.alpha = 0f;

            BuildBackground();
            _header = CreateHeader();
            var card = CreateCard();
            _cardRoot = card.rectTransform;
            Content = CreateScroll(card.transform);

            BuildStatus();
            BuildForm();

            SimPanel = CreateChoicePanel(Content, "Sim", "L'éditeur Unity n'est pas le téléphone.\nChoisis le résultat simulé.", 400,
                "BIOMÉTRIE RÉUSSIE", "BIOMÉTRIE REFUSÉE", out SimSuccessButton, out SimFailButton);
            EnablePanel = CreateChoicePanel(Content, "Enable", "Activer l'ouverture par le visage ?\nLe mot de passe reste le secours.", 430,
                "ACTIVER", "PLUS TARD", out EnableYesButton, out EnableNoButton);
            HomePanel = CreateHome(Content);

            BuildFooter();
            BuildBusyOverlay();

            LockDuringRequest = new Selectable[]
            {
                Email, Password, DisplayName, BiometricButton, PrimaryButton, SwitchButton,
                SimSuccessButton, SimFailButton, EnableYesButton, EnableNoButton, LogoutButton, PlayButton,
                PracticeButton
            };

            SimPanel.SetActive(false);
            EnablePanel.SetActive(false);
            HomePanel.SetActive(false);
            BiometricButton.gameObject.SetActive(false);
            NameRow.SetActive(false);

            ApplyLayout(false);
            StartCoroutine(Entrance());
        }

        void Update()
        {
            // Spinner
            if (_spinner != null && _spinner.gameObject.activeSelf)
                _spinner.rectTransform.Rotate(0f, 0f, -360f * Time.unscaledDeltaTime * 1.2f);

            // Floating glow orbs
            if (_orbs != null)
            {
                _orbTime += Time.unscaledDeltaTime;
                for (int i = 0; i < _orbs.Length; i++)
                {
                    if (_orbs[i] == null) continue;
                    var rect = _orbs[i].rectTransform;
                    rect.anchorMin = _orbAnchors[i];
                    rect.anchorMax = _orbAnchors[i];
                    float dx = Mathf.Sin(_orbTime * 0.18f + i * 2.1f) * 30f;
                    float dy = Mathf.Cos(_orbTime * 0.14f + i * 1.7f) * 24f;
                    rect.anchoredPosition = new Vector2(dx, dy);
                }
            }
        }

        // ------------------------------------------------------------------
        // Background
        // ------------------------------------------------------------------

        void BuildBackground()
        {
            var root = new GameObject("Background");
            root.transform.SetParent(transform, false);
            var rect = root.AddComponent<RectTransform>();
            Stretch(rect);

            var gradient = root.AddComponent<Image>();
            gradient.sprite = Gradient;
            gradient.type = Image.Type.Simple;
            gradient.color = Color.white;
            gradient.raycastTarget = false;

            _orbs = new Image[3];
            _orbAnchors = new[] { new Vector2(0.12f, 0.82f), new Vector2(0.88f, 0.2f), new Vector2(0.5f, 0.4f) };
            var orbColors = new[] { Cyan, Mint, Coral };
            for (int i = 0; i < 3; i++)
            {
                var orb = new GameObject("Glow" + i);
                orb.transform.SetParent(root.transform, false);
                var orbRect = orb.AddComponent<RectTransform>();
                orbRect.anchorMin = orbRect.anchorMax = _orbAnchors[i];
                orbRect.pivot = new Vector2(0.5f, 0.5f);
                orbRect.sizeDelta = new Vector2(i == 2 ? 1100f : 820f, i == 2 ? 1100f : 820f);
                var img = orb.AddComponent<Image>();
                img.sprite = Glow;
                img.color = new Color(orbColors[i].r, orbColors[i].g, orbColors[i].b, i == 2 ? 0.05f : 0.09f);
                img.raycastTarget = false;
                _orbs[i] = img;
            }

            var vignette = new GameObject("Vignette");
            vignette.transform.SetParent(root.transform, false);
            var vRect = vignette.AddComponent<RectTransform>();
            Stretch(vRect);
            var vImg = vignette.AddComponent<Image>();
            vImg.sprite = Vignette;
            vImg.type = Image.Type.Simple;
            vImg.color = Color.white;
            vImg.raycastTarget = false;

            // thin scanline accent at the very top
            var line = new GameObject("TopAccent");
            line.transform.SetParent(root.transform, false);
            var lRect = line.AddComponent<RectTransform>();
            lRect.anchorMin = new Vector2(0f, 1f);
            lRect.anchorMax = new Vector2(1f, 1f);
            lRect.pivot = new Vector2(0.5f, 1f);
            lRect.sizeDelta = new Vector2(0f, 3f);
            lRect.anchoredPosition = Vector2.zero;
            var lImg = line.AddComponent<Image>();
            lImg.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.35f);
            lImg.raycastTarget = false;
        }

        IEnumerator Entrance()
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / 0.55f;
                float ease = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
                _rootGroup.alpha = ease;
                if (_cardRoot != null)
                    _cardRoot.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, ease);
                yield return null;
            }
            _rootGroup.alpha = 1f;
            if (_cardRoot != null) _cardRoot.localScale = Vector3.one;
        }

        // ------------------------------------------------------------------
        // Header
        // ------------------------------------------------------------------

        RectTransform CreateHeader()
        {
            var header = new GameObject("Header");
            header.transform.SetParent(transform, false);
            var rect = header.AddComponent<RectTransform>();
            var layout = header.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 8;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var title = AddText(header.transform, "Title", "DRIVE SIM", 108, Color.white, 140, FontStyle.Bold, TextAnchor.MiddleCenter);
            var outline = title.gameObject.AddComponent<Outline>();
            outline.effectColor = Ink;
            outline.effectDistance = new Vector2(4f, -4f);
            AddDepth(title.gameObject, new Color(0.85f, 0.25f, 0.22f), 12f);

            var tagRow = new GameObject("TagRow");
            tagRow.transform.SetParent(header.transform, false);
            tagRow.AddComponent<LayoutElement>().preferredHeight = 56f;
            var tagLayout = tagRow.AddComponent<HorizontalLayoutGroup>();
            tagLayout.childAlignment = TextAnchor.MiddleCenter;
            tagLayout.childControlWidth = false;
            tagLayout.childControlHeight = false;
            tagLayout.childForceExpandWidth = false;

            var tag = CreateShape("Tag", tagRow.transform, Mint, Shape.Pill);
            tag.raycastTarget = false;
            tag.rectTransform.sizeDelta = new Vector2(330f, 52f);
            AddDepth(tag.gameObject, new Color(0.1f, 0.48f, 0.4f), 6f);
            AddInnerText(tag.transform, "GARAGE DU PILOTE", Ink, TextAnchor.MiddleCenter, 24, FontStyle.Bold);
            return rect;
        }

        // ------------------------------------------------------------------
        // Card
        // ------------------------------------------------------------------

        Image CreateCard()
        {
            var card = CreateShape("Card", transform, new Color(0.04f, 0.1f, 0.22f, 0.82f), Shape.Pill);
            card.raycastTarget = true;
            AddDepth(card.gameObject, new Color(0f, 0.02f, 0.08f, 0.75f), 18f);
            _card = card.rectTransform;

            // Subtle light edge
            var outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.09f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            // Accent bar at the top of the card
            var accent = new GameObject("Accent");
            accent.transform.SetParent(card.transform, false);
            var aRect = accent.AddComponent<RectTransform>();
            aRect.anchorMin = new Vector2(0f, 1f);
            aRect.anchorMax = new Vector2(1f, 1f);
            aRect.pivot = new Vector2(0.5f, 1f);
            aRect.sizeDelta = new Vector2(-120f, 3f);
            aRect.anchoredPosition = new Vector2(0f, -14f);
            var aImg = accent.AddComponent<Image>();
            aImg.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.55f);
            aImg.raycastTarget = false;

            // Spinner (top-right of the card)
            var spinnerObj = new GameObject("Spinner");
            spinnerObj.transform.SetParent(card.transform, false);
            var sRect = spinnerObj.AddComponent<RectTransform>();
            sRect.anchorMin = new Vector2(1f, 1f);
            sRect.anchorMax = new Vector2(1f, 1f);
            sRect.pivot = new Vector2(1f, 1f);
            sRect.sizeDelta = new Vector2(30f, 30f);
            sRect.anchoredPosition = new Vector2(-26f, -26f);
            _spinner = spinnerObj.AddComponent<Image>();
            _spinner.sprite = Ring;
            _spinner.color = Cyan;
            _spinner.raycastTarget = false;
            spinnerObj.SetActive(false);

            return card;
        }

        RectTransform CreateScroll(Transform card)
        {
            var scrollObject = new GameObject("Scroll");
            scrollObject.transform.SetParent(card, false);
            var scrollRect = scrollObject.AddComponent<RectTransform>();
            Stretch(scrollRect);
            scrollRect.offsetMin = new Vector2(6f, 6f);
            scrollRect.offsetMax = new Vector2(-6f, -6f);

            var scroll = scrollObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 36f;

            var viewportObject = new GameObject("Viewport");
            viewportObject.transform.SetParent(scrollObject.transform, false);
            var viewport = viewportObject.AddComponent<RectTransform>();
            Stretch(viewport);
            var viewportImage = viewportObject.AddComponent<Image>();
            viewportImage.sprite = Flat;
            viewportImage.color = new Color(1f, 1f, 1f, 0.01f);
            viewportObject.AddComponent<RectMask2D>();
            scroll.viewport = viewport;

            var contentObject = new GameObject("Content");
            contentObject.transform.SetParent(viewport, false);
            var content = contentObject.AddComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            var layout = contentObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(44, 44, 52, 40);
            layout.spacing = 14;
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = contentObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            return content;
        }

        // ------------------------------------------------------------------
        // Content
        // ------------------------------------------------------------------

        void BuildStatus()
        {
            Status = AddText(Content, "Status", "", 26, Soft, 84, FontStyle.Normal, TextAnchor.MiddleCenter);
        }

        void BuildForm()
        {
            BiometricButton = AddButton(Content, "Biometric", "CONNEXION BIOMÉTRIQUE", Mint, Ink, out BiometricLabel);

            DisplayName = AddField(Content, "Name", "PSEUDO", false, "ID");
            NameRow = DisplayName.gameObject;

            Email = AddField(Content, "Email", "E-MAIL", false, "@");
            Email.keyboardType = TouchScreenKeyboardType.EmailAddress;

            Password = AddField(Content, "Password", "MOT DE PASSE (6 CARACTÈRES MIN.)", true, "•••");

            PrimaryButton = AddButton(Content, "Primary", "SE CONNECTER", Coral, Color.white, out PrimaryLabel);
            SwitchButton = AddButton(Content, "Switch", "CRÉER UN COMPTE", new Color(1f, 1f, 1f, 0.08f), Cyan, out SwitchLabel);
            PracticeButton = AddButton(Content, "Practice", "MODE ENTRAÎNEMENT — SANS COMPTE",
                new Color(1f, 1f, 1f, 0.04f), Mint, out PracticeLabel);
            var practiceLayout = PracticeButton.GetComponent<LayoutElement>();
            if (practiceLayout != null)
            {
                practiceLayout.preferredHeight = 76;
                practiceLayout.minHeight = 76;
            }
        }

        void BuildFooter()
        {
            var footer = AddText(Content, "Footer", "Le téléphone vérifie le visage.\nLe serveur vérifie le compte.",
                20, new Color(Soft.r, Soft.g, Soft.b, 0.7f), 72, FontStyle.Normal, TextAnchor.MiddleCenter);
            var line = footer.gameObject.AddComponent<LayoutElement>();
            line.preferredHeight = 72;
        }

        GameObject CreateHome(Transform parent)
        {
            var panel = CreatePanel(parent, "Home", 660);
            Hello = AddText(panel.transform, "Hello", "Bonjour", 52, Color.white, 100, FontStyle.Bold, TextAnchor.MiddleCenter);
            Details = AddText(panel.transform, "Details", "", 25, Soft, 220, FontStyle.Normal, TextAnchor.UpperLeft);
            PlayButton = AddButton(panel.transform, "Play", "ENTRER DANS LE SIMULATEUR", Mint, Ink, out PlayLabel);
            LogoutButton = AddButton(panel.transform, "Logout", "SE DÉCONNECTER", new Color(1f, 1f, 1f, 0.08f), Coral, out _);
            return panel;
        }

        GameObject CreateChoicePanel(Transform parent, string name, string message, int height, string yes, string no, out Button yesButton, out Button noButton)
        {
            var panel = CreatePanel(parent, name, height);
            AddText(panel.transform, "Message", message, 28, Color.white, 160, FontStyle.Normal, TextAnchor.MiddleCenter);
            yesButton = AddButton(panel.transform, "Yes", yes, Coral, Color.white, out _);
            noButton = AddButton(panel.transform, "No", no, new Color(1f, 1f, 1f, 0.08f), Coral, out _);
            return panel;
        }

        // ------------------------------------------------------------------
        // Layout
        // ------------------------------------------------------------------

        // Retourne le point de l'écran laissé libre pour la voiture 3D.
        public Vector2 ApplyLayout(bool landscape)
        {
            if (landscape)
            {
                SetAnchors(_header, new Vector2(0.02f, 0.76f), new Vector2(0.5f, 0.97f));
                SetAnchors(_card, new Vector2(0.52f, 0.03f), new Vector2(0.98f, 0.97f));
                return new Vector2(0.26f, 0.4f);
            }

            SetAnchors(_header, new Vector2(0f, 0.83f), new Vector2(1f, 0.98f));
            SetAnchors(_card, new Vector2(0.035f, 0.015f), new Vector2(0.965f, 0.57f));
            return new Vector2(0.5f, 0.7f);
        }

        static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // ------------------------------------------------------------------
        // Busy overlay
        // ------------------------------------------------------------------

        void BuildBusyOverlay()
        {
            _busyOverlay = new GameObject("BusyOverlay");
            _busyOverlay.transform.SetParent(transform, false);
            var rect = _busyOverlay.AddComponent<RectTransform>();
            Stretch(rect);
            var img = _busyOverlay.AddComponent<Image>();
            img.color = new Color(0.01f, 0.03f, 0.09f, 0.55f);
            img.raycastTarget = true;
            _busyGroup = _busyOverlay.AddComponent<CanvasGroup>();
            _busyGroup.alpha = 0f;
            _busyGroup.blocksRaycasts = false;
            _busyOverlay.SetActive(false);
        }

        public void SetLoading(bool loading)
        {
            if (_busyOverlay == null) return;
            StopCoroutine(nameof(FadeBusy));
            _busyOverlay.SetActive(true);
            StartCoroutine(nameof(FadeBusy), loading ? 1f : 0f);

            if (_spinner != null)
                _spinner.gameObject.SetActive(loading);
        }

        // Efface l'interface de compte : la scène de jeu prend le relais.
        public void Hide()
        {
            StopAllCoroutines();
            var group = GetComponent<CanvasGroup>();
            if (group != null) group.blocksRaycasts = false;
            StartCoroutine(FadeOut());
        }

        IEnumerator FadeOut()
        {
            var canvas = GetComponent<Canvas>();
            var raycaster = GetComponent<GraphicRaycaster>();
            var group = GetComponent<CanvasGroup>();
            float start = group != null ? group.alpha : 1f;
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / 0.35f;
                if (group != null) group.alpha = Mathf.Lerp(start, 0f, Mathf.Clamp01(t));
                yield return null;
            }
            if (canvas != null) canvas.enabled = false;
            if (raycaster != null) raycaster.enabled = false;
        }

        IEnumerator FadeBusy(float target)
        {
            float start = _busyGroup.alpha;
            float t = 0f;
            _busyGroup.blocksRaycasts = target > 0.5f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / 0.2f;
                _busyGroup.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(t));
                yield return null;
            }
            _busyGroup.alpha = target;
            if (target <= 0f) _busyOverlay.SetActive(false);
        }

        // ------------------------------------------------------------------
        // Builders
        // ------------------------------------------------------------------

        static void AddDepth(GameObject target, Color color, float depth)
        {
            var shadow = target.AddComponent<Shadow>();
            shadow.effectColor = color;
            shadow.effectDistance = new Vector2(0f, -depth);
        }

        static GameObject CreatePanel(Transform parent, string name, int height)
        {
            var panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            panel.AddComponent<RectTransform>();
            var element = panel.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;
            var layout = panel.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 16;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return panel;
        }

        Text AddText(Transform parent, string name, string value, int size, Color color, int height, FontStyle style, TextAnchor anchor)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            var text = textObject.AddComponent<Text>();
            text.font = FontAsset;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            var element = textObject.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;
            return text;
        }

        InputField AddField(Transform parent, string name, string placeholder, bool password, string icon)
        {
            var image = CreateShape(name, parent, new Color(0.08f, 0.15f, 0.3f, 0.85f), Shape.Pill);
            var element = image.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 92;
            element.minHeight = 92;
            AddDepth(image.gameObject, new Color(0f, 0.02f, 0.08f, 0.85f), 5f);

            // subtle light edge
            var outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.08f);
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            // icon
            var iconText = AddIcon(image.transform, icon);

            var input = image.gameObject.AddComponent<InputField>();
            var text = AddInnerTextInsets(image.transform, "", Color.white, TextAnchor.MiddleLeft, 28, FontStyle.Normal, 62f);
            var hint = AddInnerTextInsets(image.transform, placeholder, new Color(0.55f, 0.65f, 0.8f, 0.75f),
                TextAnchor.MiddleLeft, 22, FontStyle.Normal, 62f);
            input.textComponent = text;
            input.placeholder = hint;
            input.lineType = InputField.LineType.SingleLine;
            input.contentType = password ? InputField.ContentType.Password : InputField.ContentType.Standard;
            input.customCaretColor = true;
            input.caretColor = Cyan;
            input.selectionColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.35f);
            return input;
        }

        Text AddInnerTextInsets(Transform parent, string value, Color color, TextAnchor anchor, int size, FontStyle style, float left)
        {
            var textObject = new GameObject(value.Length == 0 ? "Text" : "Hint");
            textObject.transform.SetParent(parent, false);
            var rect = textObject.AddComponent<RectTransform>();
            Stretch(rect);
            rect.offsetMin = new Vector2(left, 8f);
            rect.offsetMax = new Vector2(-24f, -8f);
            var text = textObject.AddComponent<Text>();
            text.font = FontAsset;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        Text AddIcon(Transform parent, string value)
        {
            var textObject = new GameObject("Icon");
            textObject.transform.SetParent(parent, false);
            var rect = textObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = new Vector2(22f, 0f);
            rect.sizeDelta = new Vector2(32f, 40f);
            var text = textObject.AddComponent<Text>();
            text.font = FontAsset;
            text.text = value;
            text.fontSize = 22;
            text.fontStyle = FontStyle.Bold;
            text.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.8f);
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        Button AddButton(Transform parent, string name, string label, Color fill, Color textColor, out Text labelText)
        {
            var image = CreateShape(name, parent, fill, Shape.Pill);
            var element = image.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 96;
            element.minHeight = 96;
            var lip = fill == Color.white ? new Color(0.6f, 0.68f, 0.84f) : Darker(fill);
            AddDepth(image.gameObject, lip, 8f);

            // subtle top highlight
            if (fill.a > 0.2f)
            {
                var hi = new GameObject("Highlight");
                hi.transform.SetParent(image.transform, false);
                var hRect = hi.AddComponent<RectTransform>();
                hRect.anchorMin = new Vector2(0.08f, 1f);
                hRect.anchorMax = new Vector2(0.92f, 1f);
                hRect.pivot = new Vector2(0.5f, 1f);
                hRect.sizeDelta = new Vector2(-40f, 2f);
                hRect.anchoredPosition = new Vector2(0f, -2f);
                var hImg = hi.AddComponent<Image>();
                hImg.color = new Color(1f, 1f, 1f, 0.22f);
                hImg.raycastTarget = false;
            }

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.95f, 0.98f, 1f, 1f);
            colors.pressedColor = new Color(0.8f, 0.82f, 0.86f, 1f);
            colors.disabledColor = new Color(0.5f, 0.5f, 0.55f, 0.5f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            labelText = AddInnerText(image.transform, label, textColor, TextAnchor.MiddleCenter, 28, FontStyle.Bold);
            return button;
        }

        Text AddInnerText(Transform parent, string value, Color color, TextAnchor anchor, int size, FontStyle style)
        {
            var textObject = new GameObject("Text");
            textObject.transform.SetParent(parent, false);
            var rect = textObject.AddComponent<RectTransform>();
            Stretch(rect);
            rect.offsetMin = new Vector2(24f, 8f);
            rect.offsetMax = new Vector2(-24f, -8f);
            var text = textObject.AddComponent<Text>();
            text.font = FontAsset;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        Image CreateShape(string name, Transform parent, Color color, Shape shape)
        {
            var imageObject = new GameObject(name);
            imageObject.transform.SetParent(parent, false);
            var image = imageObject.AddComponent<Image>();
            image.color = color;
            if (shape == Shape.Pill)
            {
                image.sprite = Pill;
                image.type = Image.Type.Sliced;
            }
            else
            {
                image.sprite = Flat;
                image.type = Image.Type.Simple;
            }
            return image;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        static Color Darker(Color color)
        {
            return new Color(color.r * 0.62f, color.g * 0.62f, color.b * 0.62f, 1f);
        }

        // ------------------------------------------------------------------
        // Assets (generated once)
        // ------------------------------------------------------------------

        static Font FontAsset
        {
            get
            {
                if (_font == null)
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (_font == null)
                    _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                return _font;
            }
        }

        static Sprite Flat
        {
            get
            {
                if (_flat != null) return _flat;
                var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                texture.SetPixel(0, 0, Color.white);
                texture.Apply();
                _flat = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
                return _flat;
            }
        }

        static Sprite Pill
        {
            get
            {
                if (_pill == null)
                    _pill = MakeRounded(96, 46);
                return _pill;
            }
        }

        static Sprite Gradient
        {
            get
            {
                if (_gradient != null) return _gradient;
                const int h = 256;
                var tex = new Texture2D(1, h, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp;
                Color top = new Color(0.015f, 0.04f, 0.11f, 1f);
                Color mid = new Color(0.03f, 0.09f, 0.2f, 1f);
                Color bottom = new Color(0.05f, 0.16f, 0.3f, 1f);
                for (int y = 0; y < h; y++)
                {
                    float t = y / (float)(h - 1);
                    Color c = t < 0.5f ? Color.Lerp(top, mid, t * 2f) : Color.Lerp(mid, bottom, (t - 0.5f) * 2f);
                    tex.SetPixel(0, y, c);
                }
                tex.Apply();
                _gradient = Sprite.Create(tex, new Rect(0f, 0f, 1f, h), new Vector2(0.5f, 0.5f), 1f);
                return _gradient;
            }
        }

        static Sprite Glow
        {
            get
            {
                if (_glow != null) return _glow;
                const int size = 128;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp;
                var pixels = new Color[size * size];
                Vector2 c = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
                float max = size * 0.5f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c) / max;
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a;
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
                tex.SetPixels(pixels);
                tex.Apply();
                _glow = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 1f);
                return _glow;
            }
        }

        static Sprite Vignette
        {
            get
            {
                if (_vignette != null) return _vignette;
                const int size = 256;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp;
                var pixels = new Color[size * size];
                Vector2 c = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
                float max = size * 0.5f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c) / max;
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, d)) * 0.55f;
                    pixels[y * size + x] = new Color(0f, 0f, 0f, a);
                }
                tex.SetPixels(pixels);
                tex.Apply();
                _vignette = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 1f);
                return _vignette;
            }
        }

        static Sprite Ring
        {
            get
            {
                if (_ring != null) return _ring;
                const int size = 64;
                const float radius = 24f;
                const float thickness = 4f;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp;
                var pixels = new Color[size * size];
                Vector2 c = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c);
                    float ring = 1f - Mathf.Clamp01(Mathf.Abs(d - radius) / thickness);
                    float angle = Mathf.Atan2(y - c.y, x - c.x);
                    float t = (angle + Mathf.PI) / (Mathf.PI * 2f);
                    float a = ring * Mathf.Lerp(0.1f, 1f, t);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, a);
                }
                tex.SetPixels(pixels);
                tex.Apply();
                _ring = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 1f);
                return _ring;
            }
        }

        static Sprite MakeRounded(int size, int radius)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                    pixels[y * size + x] = new Color(1f, 1f, 1f, CapsuleAlpha(x, y, size, size, radius));
            }
            texture.SetPixels(pixels);
            texture.Apply();
            var border = new Vector4(radius, radius, radius, radius);
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        static float CapsuleAlpha(int x, int y, int width, int height, int radius)
        {
            float px = x + 0.5f;
            float py = y + 0.5f;
            float dx = 0f;
            float dy = 0f;
            if (px < radius) dx = radius - px;
            else if (px > width - radius) dx = px - (width - radius);
            if (py < radius) dy = radius - py;
            else if (py > height - radius) dy = py - (height - radius);
            return Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.75f);
        }

        enum Shape
        {
            Flat,
            Pill
        }
    }
}
