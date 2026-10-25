using UnityEngine;
using UnityEngine.UI;

namespace DriveSim.User
{
    // Interface du compte, posée par-dessus le garage 3D (AuthBackdrop).
    public sealed class AuthScreen : MonoBehaviour
    {
        static readonly Color Navy = new Color(0.04f, 0.11f, 0.27f);
        static readonly Color Coral = new Color(1f, 0.38f, 0.36f);
        static readonly Color Mint = new Color(0.28f, 0.88f, 0.74f);
        static readonly Color Ink = new Color(0.08f, 0.16f, 0.34f);
        static readonly Color Soft = new Color(0.86f, 0.93f, 1f);

        static Font _font;
        static Sprite _flat;
        static Sprite _pill;

        RectTransform _header;
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

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            _header = CreateHeader();

            var card = CreateShape("Card", transform, new Color(Navy.r, Navy.g, Navy.b, 0.86f), Shape.Pill);
            card.raycastTarget = true;
            _card = card.rectTransform;
            AddDepth(card.gameObject, new Color(0f, 0.02f, 0.08f, 0.7f), 14f);

            Content = CreateScroll(card.transform);
            Status = AddText(Content, "Status", "", 28, Soft, 96, FontStyle.Normal, TextAnchor.MiddleCenter);
            BiometricButton = AddButton(Content, "Biometric", "CONNEXION BIOMÉTRIQUE", Mint, Color.white, out BiometricLabel);
            DisplayName = AddField(Content, "Name", "Pseudo", false);
            NameRow = DisplayName.gameObject;
            Email = AddField(Content, "Email", "E-mail", false);
            Email.keyboardType = TouchScreenKeyboardType.EmailAddress;
            Password = AddField(Content, "Password", "Mot de passe, 6 caractères minimum", true);
            PrimaryButton = AddButton(Content, "Primary", "SE CONNECTER", Coral, Color.white, out PrimaryLabel);
            SwitchButton = AddButton(Content, "Switch", "CRÉER UN COMPTE", Color.white, Coral, out SwitchLabel);
            SimPanel = CreateChoicePanel(Content, "Sim", "L'éditeur Unity n'est pas le téléphone. Choisis le résultat simulé.", 360,
                "BIOMÉTRIE RÉUSSIE", "BIOMÉTRIE REFUSÉE", out SimSuccessButton, out SimFailButton);
            EnablePanel = CreateChoicePanel(Content, "Enable", "Activer l'ouverture par le visage sur cet appareil ? Le mot de passe reste le secours.", 390,
                "ACTIVER", "PLUS TARD", out EnableYesButton, out EnableNoButton);
            HomePanel = CreateHome(Content);
            AddText(Content, "Footer", "Le téléphone vérifie le visage. Le serveur vérifie le compte.",
                24, Soft, 80, FontStyle.Normal, TextAnchor.MiddleCenter);
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

        GameObject CreateHome(Transform parent)
        {
            var panel = CreatePanel(parent, "Home", 540);
            Hello = AddText(panel.transform, "Hello", "Bonjour", 48, Color.white, 90, FontStyle.Bold, TextAnchor.MiddleCenter);
            Details = AddText(panel.transform, "Details", "", 28, Soft, 280, FontStyle.Normal, TextAnchor.UpperLeft);
            LogoutButton = AddButton(panel.transform, "Logout", "SE DÉCONNECTER", Color.white, Coral, out _);
            return panel;
        }

        GameObject CreateChoicePanel(Transform parent, string name, string message, int height, string yes, string no, out Button yesButton, out Button noButton)
        {
            var panel = CreatePanel(parent, name, height);
            AddText(panel.transform, "Message", message, 30, Color.white, 150, FontStyle.Normal, TextAnchor.MiddleCenter);
            yesButton = AddButton(panel.transform, "Yes", yes, Coral, Color.white, out _);
            noButton = AddButton(panel.transform, "No", no, Color.white, Coral, out _);
            return panel;
        }

        // Retourne le point de l'écran laissé libre pour la voiture 3D.
        public Vector2 ApplyLayout(bool landscape)
        {
            if (landscape)
            {
                SetAnchors(_header, new Vector2(0.02f, 0.78f), new Vector2(0.5f, 0.97f));
                SetAnchors(_card, new Vector2(0.52f, 0.03f), new Vector2(0.98f, 0.97f));
                return new Vector2(0.26f, 0.4f);
            }

            SetAnchors(_header, new Vector2(0f, 0.84f), new Vector2(1f, 0.98f));
            SetAnchors(_card, new Vector2(0.035f, 0.015f), new Vector2(0.965f, 0.58f));
            return new Vector2(0.5f, 0.71f);
        }

        RectTransform CreateHeader()
        {
            var header = new GameObject("Header");
            header.transform.SetParent(transform, false);
            var rect = header.AddComponent<RectTransform>();
            var layout = header.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.spacing = 6;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var title = AddText(header.transform, "Title", "DRIVE SIM", 104, Color.white, 130, FontStyle.Bold, TextAnchor.MiddleCenter);
            var outline = title.gameObject.AddComponent<Outline>();
            outline.effectColor = Ink;
            outline.effectDistance = new Vector2(4f, -4f);
            AddDepth(title.gameObject, new Color(0.8f, 0.22f, 0.2f), 10f);

            var tagRow = new GameObject("TagRow");
            tagRow.transform.SetParent(header.transform, false);
            var tagElement = tagRow.AddComponent<LayoutElement>();
            tagElement.preferredHeight = 60;
            var tagLayout = tagRow.AddComponent<HorizontalLayoutGroup>();
            tagLayout.childAlignment = TextAnchor.MiddleCenter;
            tagLayout.childControlWidth = false;
            tagLayout.childControlHeight = false;
            tagLayout.childForceExpandWidth = false;

            var tag = CreateShape("Tag", tagRow.transform, Mint, Shape.Pill);
            tag.raycastTarget = false;
            tag.rectTransform.sizeDelta = new Vector2(330f, 56f);
            AddDepth(tag.gameObject, new Color(0.12f, 0.55f, 0.45f), 6f);
            AddInnerText(tag.transform, "GARAGE DU PILOTE", Ink, TextAnchor.MiddleCenter, 26, FontStyle.Bold);
            return rect;
        }

        static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // Ombre décalée vers le bas : donne l'épaisseur des boutons de jeux mobiles.
        static void AddDepth(GameObject target, Color color, float depth)
        {
            var shadow = target.AddComponent<Shadow>();
            shadow.effectColor = color;
            shadow.effectDistance = new Vector2(0f, -depth);
        }

        static Color Darker(Color color)
        {
            return new Color(color.r * 0.68f, color.g * 0.68f, color.b * 0.68f, 1f);
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
            layout.padding = new RectOffset(48, 48, 56, 48);
            layout.spacing = 16;
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

        InputField AddField(Transform parent, string name, string placeholder, bool password)
        {
            var image = CreateShape(name, parent, Color.white, Shape.Pill);
            var element = image.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 100;
            element.minHeight = 100;
            AddDepth(image.gameObject, new Color(0.02f, 0.05f, 0.14f, 0.8f), 6f);
            var input = image.gameObject.AddComponent<InputField>();
            var text = AddInnerText(image.transform, "", Ink, TextAnchor.MiddleLeft, 30, FontStyle.Normal);
            var hint = AddInnerText(image.transform, placeholder, new Color(0.45f, 0.52f, 0.64f), TextAnchor.MiddleLeft, 28, FontStyle.Normal);
            input.textComponent = text;
            input.placeholder = hint;
            input.lineType = InputField.LineType.SingleLine;
            input.contentType = password ? InputField.ContentType.Password : InputField.ContentType.Standard;
            input.customCaretColor = true;
            input.caretColor = Ink;
            return input;
        }

        Button AddButton(Transform parent, string name, string label, Color fill, Color textColor, out Text labelText)
        {
            var image = CreateShape(name, parent, fill, Shape.Pill);
            var element = image.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 104;
            element.minHeight = 104;
            var lip = fill == Color.white ? new Color(0.6f, 0.68f, 0.84f) : Darker(fill);
            AddDepth(image.gameObject, lip, 10f);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.94f, 0.94f, 0.94f, 1f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.78f, 1f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.75f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.05f;
            button.colors = colors;
            labelText = AddInnerText(image.transform, label, textColor, TextAnchor.MiddleCenter, 32, FontStyle.Bold);
            return button;
        }

        Text AddInnerText(Transform parent, string value, Color color, TextAnchor anchor, int size, FontStyle style)
        {
            var textObject = new GameObject("Text");
            textObject.transform.SetParent(parent, false);
            var rect = textObject.AddComponent<RectTransform>();
            Stretch(rect);
            rect.offsetMin = new Vector2(28f, 10f);
            rect.offsetMax = new Vector2(-28f, -10f);
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
