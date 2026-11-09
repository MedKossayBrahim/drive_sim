using DriveSim.Menu;
using UnityEngine;
using UnityEngine.UI;

namespace DriveSim.User
{
    // Connexion, inscription et Face ID, dans le même habillage que l'accueil.
    public sealed class AuthScreen : MonoBehaviour
    {
        CanvasScaler _scaler;
        Image _night;
        Image _road;
        RectTransform _logoRoot;
        RectTransform _header;
        Text _heading;
        RectTransform _blurbRoot;
        Text _blurb;
        RectTransform _card;

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
        public GameObject SimPanel;
        public Button SimSuccessButton;
        public Button SimFailButton;
        public GameObject EnablePanel;
        public Button EnableYesButton;
        public Button EnableNoButton;
        public GameObject HomePanel;
        public Text Hello;
        public Text Details;
        public Button LogoutButton;
        public Selectable[] LockDuringRequest;

        public void Construct()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;

            _scaler = gameObject.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.referenceResolution = new Vector2(1080f, 1920f);
            _scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            _night = UiKit.Image("Night", transform, UiKit.Night, UiKit.Pixel, true);
            UiKit.Stretch(_night.rectTransform, 0f, 0f, 0f, 0f);
            _road = UiKit.Image("Road", transform, Color.white, UiKit.Backdrop(), false);
            UiKit.Stretch(_road.rectTransform, 0f, 0f, 0f, 0f);

            _logoRoot = CreateLogo();
            _header = CreateHeader();
            _blurbRoot = CreateBlurb();

            var card = UiKit.Image("Card", transform, UiKit.Panel, UiKit.Round(18), true, Image.Type.Sliced);
            _card = card.rectTransform;
            UiKit.Lip(card.gameObject, new Color(0f, 0f, 0f, 0.45f), 12f);

            Content = CreateScroll(card.transform);
            Status = AddText(Content, "Status", "", 24, UiKit.Soft, 72, FontStyle.Bold, TextAnchor.MiddleCenter);
            BiometricButton = AddButton(Content, "Biometric", "CONNEXION BIOMÉTRIQUE", UiKit.Green, Color.white, out BiometricLabel);
            DisplayName = AddField(Content, "Name", "Pseudo", false);
            NameRow = DisplayName.gameObject;
            Email = AddField(Content, "Email", "E-mail", false);
            Email.keyboardType = TouchScreenKeyboardType.EmailAddress;
            Password = AddField(Content, "Password", "Mot de passe, 6 caractères minimum", true);
            PrimaryButton = AddButton(Content, "Primary", "SE CONNECTER", UiKit.Yellow, UiKit.Ink, out PrimaryLabel);
            SwitchButton = AddButton(Content, "Switch", "CRÉER UN COMPTE", new Color(0.07f, 0.12f, 0.18f, 1f), UiKit.Cyan, out SwitchLabel);
            SimPanel = CreateChoicePanel(Content, "Sim", "Choisis le résultat simulé.", 300,
                "BIOMÉTRIE RÉUSSIE", "BIOMÉTRIE REFUSÉE", out SimSuccessButton, out SimFailButton);
            EnablePanel = CreateChoicePanel(Content, "Enable", "Le visage reste sur cet appareil. Le mot de passe reste le secours.", 340,
                "ACTIVER", "PLUS TARD", out EnableYesButton, out EnableNoButton);
            HomePanel = CreateHome(Content);
            AddText(Content, "Footer", "Le téléphone vérifie le visage. Le serveur vérifie le compte.",
                20, UiKit.Soft, 64, FontStyle.Normal, TextAnchor.MiddleCenter);
            SetPage("CONNEXION", "Entre ton e-mail. Le bouton jaune ouvre le garage.");
            ApplyLayout(false);

            LockDuringRequest = new Selectable[]
            {
                Email, Password, DisplayName, BiometricButton, PrimaryButton, SwitchButton,
                SimSuccessButton, SimFailButton, EnableYesButton, EnableNoButton, LogoutButton
            };

            SimPanel.SetActive(false);
            EnablePanel.SetActive(false);
            HomePanel.SetActive(false);
            BiometricButton.gameObject.SetActive(false);
            NameRow.SetActive(false);
        }

        public void SetPage(string title, string blurb)
        {
            if (_heading != null)
                _heading.text = title ?? "";
            if (_blurb != null)
                _blurb.text = blurb ?? "";
        }

        public void SetChrome(bool visible)
        {
            if (_night != null)
                _night.gameObject.SetActive(visible);
            if (_road != null)
                _road.gameObject.SetActive(visible);
            if (_logoRoot != null)
                _logoRoot.gameObject.SetActive(visible);
            if (_header != null)
                _header.gameObject.SetActive(visible);
            if (_blurbRoot != null)
                _blurbRoot.gameObject.SetActive(visible);
            if (_card != null)
                _card.gameObject.SetActive(visible);
        }

        public Vector2 ApplyLayout(bool landscape)
        {
            if (_scaler != null)
                _scaler.referenceResolution = landscape ? new Vector2(1920f, 1080f) : new Vector2(1080f, 1920f);

            if (landscape)
            {
                SetAnchors(_logoRoot, new Vector2(0.38f, 0.78f), new Vector2(0.62f, 0.98f));
                SetAnchors(_header, new Vector2(0.28f, 0.70f), new Vector2(0.72f, 0.78f));
                SetAnchors(_blurbRoot, new Vector2(0.14f, 0.62f), new Vector2(0.86f, 0.70f));
                SetAnchors(_card, new Vector2(0.22f, 0.04f), new Vector2(0.78f, 0.61f));
                _heading.fontSize = 26;
                _blurb.fontSize = 20;
                return new Vector2(0.5f, 0.5f);
            }

            SetAnchors(_logoRoot, new Vector2(0.22f, 0.80f), new Vector2(0.78f, 0.97f));
            SetAnchors(_header, new Vector2(0.08f, 0.73f), new Vector2(0.92f, 0.80f));
            SetAnchors(_blurbRoot, new Vector2(0.06f, 0.66f), new Vector2(0.94f, 0.73f));
            SetAnchors(_card, new Vector2(0.05f, 0.03f), new Vector2(0.95f, 0.65f));
            _heading.fontSize = 28;
            _blurb.fontSize = 20;
            return new Vector2(0.5f, 0.5f);
        }

        RectTransform CreateLogo()
        {
            var root = UiKit.Rect("Logo", transform);
            var frame = UiKit.Image("LogoFrame", root, new Color(0.02f, 0.06f, 0.1f, 0.55f), UiKit.Round(22), true, Image.Type.Sliced);
            UiKit.Stretch(frame.rectTransform, 0f, 0f, 0f, 0f);
            var logo = UiKit.Image("LogoArt", root, Color.white, UiKit.AppLogo(), false);
            logo.preserveAspect = true;
            UiKit.Stretch(logo.rectTransform, 10f, 10f, 10f, 10f);
            return root;
        }

        RectTransform CreateHeader()
        {
            var header = UiKit.Rect("Header", transform);
            var banner = UiKit.Image("TitleBanner", header, UiKit.Cyan, UiKit.Skew(), false);
            UiKit.Stretch(banner.rectTransform, 0f, 0f, 0f, 0f);
            _heading = UiKit.Label(banner.transform, "CONNEXION", 32, UiKit.Ink, FontStyle.Bold, TextAnchor.MiddleCenter);
            UiKit.Stretch(_heading.rectTransform, 36f, 8f, 36f, 8f);
            return header;
        }

        RectTransform CreateBlurb()
        {
            var blurb = UiKit.Rect("Blurb", transform);
            for (int i = 0; i < 3; i++)
            {
                var stripe = UiKit.Image("Stripe", blurb, UiKit.Cyan, UiKit.Pixel, false);
                var rect = stripe.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(10f, 34f);
                rect.anchoredPosition = new Vector2(20f + i * 14f, 0f);
                rect.localEulerAngles = new Vector3(0f, 0f, 32f);
            }
            _blurb = UiKit.Label(blurb, "", 22, Color.white, FontStyle.Normal, TextAnchor.MiddleLeft);
            _blurb.verticalOverflow = VerticalWrapMode.Overflow;
            UiKit.Stretch(_blurb.rectTransform, 74f, 2f, 8f, 2f);
            return blurb;
        }

        GameObject CreateHome(Transform parent)
        {
            var panel = CreatePanel(parent, "Home", 420);
            Hello = AddText(panel.transform, "Hello", "Bonjour", 40, Color.white, 72, FontStyle.Bold, TextAnchor.MiddleCenter);
            Details = AddText(panel.transform, "Details", "", 24, UiKit.Soft, 180, FontStyle.Normal, TextAnchor.UpperLeft);
            LogoutButton = AddButton(panel.transform, "Logout", "SE DÉCONNECTER", UiKit.Yellow, UiKit.Ink, out _);
            return panel;
        }

        GameObject CreateChoicePanel(Transform parent, string name, string message, int height, string yes, string no, out Button yesButton, out Button noButton)
        {
            var panel = CreatePanel(parent, name, height);
            AddText(panel.transform, "Message", message, 26, Color.white, 120, FontStyle.Normal, TextAnchor.MiddleCenter);
            yesButton = AddButton(panel.transform, "Yes", yes, UiKit.Green, Color.white, out _);
            noButton = AddButton(panel.transform, "No", no, new Color(0.07f, 0.12f, 0.18f, 1f), UiKit.Cyan, out _);
            return panel;
        }

        static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
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
            layout.spacing = 14;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return panel;
        }

        RectTransform CreateScroll(Transform card)
        {
            var scrollObject = new GameObject("Scroll");
            scrollObject.transform.SetParent(card, false);
            var scrollRect = scrollObject.AddComponent<RectTransform>();
            Stretch(scrollRect);
            var scroll = scrollObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;

            var viewportObject = new GameObject("Viewport");
            viewportObject.transform.SetParent(scrollObject.transform, false);
            var viewport = viewportObject.AddComponent<RectTransform>();
            Stretch(viewport);
            var viewportImage = viewportObject.AddComponent<Image>();
            viewportImage.sprite = UiKit.Pixel;
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
            layout.padding = new RectOffset(36, 36, 32, 28);
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

        Text AddText(Transform parent, string name, string value, int size, Color color, int height, FontStyle style, TextAnchor anchor)
        {
            var text = UiKit.Label(parent, value, size, color, style, anchor);
            text.gameObject.name = name;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            var element = text.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;
            return text;
        }

        InputField AddField(Transform parent, string name, string placeholder, bool password)
        {
            var image = UiKit.Image(name, parent, new Color(0.07f, 0.13f, 0.2f, 1f), UiKit.Round(16), true, Image.Type.Sliced);
            UiKit.Prefer(image, 0f, 96f);
            var input = image.gameObject.AddComponent<InputField>();
            var text = AddInnerText(image.transform, "", Color.white, TextAnchor.MiddleLeft, 30, FontStyle.Normal);
            var hint = AddInnerText(image.transform, placeholder, new Color(0.62f, 0.75f, 0.82f, 1f), TextAnchor.MiddleLeft, 26, FontStyle.Normal);
            input.textComponent = text;
            input.placeholder = hint;
            input.lineType = InputField.LineType.SingleLine;
            input.contentType = password ? InputField.ContentType.Password : InputField.ContentType.Standard;
            input.customCaretColor = true;
            input.caretColor = UiKit.Cyan;
            return input;
        }

        Button AddButton(Transform parent, string name, string label, Color fill, Color textColor, out Text labelText)
        {
            var button = UiKit.Button(parent, name, label, fill, textColor, 28, UiKit.Skew(), out labelText);
            UiKit.Prefer(button, 0f, 92f);
            UiKit.Lip(button.gameObject, new Color(0f, 0f, 0f, 0.35f), 6f);
            return button;
        }

        Text AddInnerText(Transform parent, string value, Color color, TextAnchor anchor, int size, FontStyle style)
        {
            var text = UiKit.Label(parent, value, size, color, style, anchor);
            UiKit.Stretch(text.rectTransform, 28f, 10f, 28f, 10f);
            return text;
        }

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
