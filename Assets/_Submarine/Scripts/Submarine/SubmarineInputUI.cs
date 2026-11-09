using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DriveSim.Submarine
{
    /// <summary>
    /// Commandes tactiles : manche flottant à gauche, rail de puissance à droite,
    /// quatre commandes en verre (sonar, silence, phares, réparation).
    /// Construit entièrement par code, style cohérent avec le HUD.
    /// </summary>
    public class SubmarineInputUI : MonoBehaviour
    {
        const int MouseId = -2;

        [Header("References")]
        public SubmarineInput input;
        public SubmarineController controller;
        public SonarSystem sonar;
        public BatterySystem battery;
        public HullSystem hull;

        [Header("Joystick")]
        public float joystickRadius = 88f;
        public Color joystickColor = new Color(0.3f, 0.9f, 1f, 0.55f);

        static readonly Color Cyan = new Color(0.3f, 0.9f, 1f);
        static readonly Color Mint = new Color(0.3f, 0.95f, 0.75f);
        static readonly Color Amber = new Color(1f, 0.78f, 0.32f);
        static readonly Color Coral = new Color(1f, 0.45f, 0.4f);
        static readonly Color Ink = new Color(0.78f, 0.9f, 1f);
        static readonly Color Glass = new Color(0.03f, 0.1f, 0.2f, 0.5f);

        static Sprite _circle, _ring, _rounded;

        Canvas _canvas;
        RectTransform _canvasRect;

        RectTransform _base;
        RectTransform _knob;
        CanvasGroup _baseGroup;
        Vector2 _joyHome;
        Vector2 _joyOrigin;
        int _joyId = -1;
        bool _homed;

        RectTransform _rail;
        RectTransform _handle;
        Image _railFill;
        float _throttle;
        int _thrId = -1;

        Image _pingRing, _silentRing, _lightRing, _repairRing;
        Image _pingCooldown;
        Text _pingLabel, _repairLabel, _repairCount;

        void Awake()
        {
            if (input == null) input = FindFirstObjectByType<SubmarineInput>();
            if (controller == null) controller = FindFirstObjectByType<SubmarineController>();
            if (sonar == null) sonar = FindFirstObjectByType<SonarSystem>();
            if (battery == null) battery = FindFirstObjectByType<BatterySystem>();
            if (hull == null) hull = FindFirstObjectByType<HullSystem>();

            BuildUI();
        }

        // ------------------------------------------------------------------
        // Construction
        // ------------------------------------------------------------------

        void BuildUI()
        {
            var canvasObj = new GameObject("TouchControls");
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 90;
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();
            _canvasRect = canvasObj.GetComponent<RectTransform>();

            BuildJoystick(canvasObj.transform);
            BuildThrottle(canvasObj.transform);
            BuildActions(canvasObj.transform);

            LayoutHome();

            if (!Application.isMobilePlatform && !Application.isEditor)
                canvasObj.SetActive(false);
        }

        void BuildJoystick(Transform root)
        {
            var baseObj = new GameObject("JoystickBase");
            baseObj.transform.SetParent(root, false);
            _base = baseObj.AddComponent<RectTransform>();
            _base.anchorMin = _base.anchorMax = new Vector2(0.5f, 0.5f);
            _base.sizeDelta = new Vector2(280f, 280f);
            _baseGroup = baseObj.AddComponent<CanvasGroup>();
            _baseGroup.alpha = 0.55f;

            var disc = baseObj.AddComponent<Image>();
            disc.sprite = Circle;
            disc.color = Glass;
            disc.raycastTarget = false;

            var ring = NewImage(baseObj.transform, "Ring", Ring, new Color(Cyan.r, Cyan.g, Cyan.b, 0.35f),
                new Vector2(270f, 270f), Vector2.zero);
            ring.raycastTarget = false;

            // Réticule
            for (int i = 0; i < 2; i++)
            {
                var tick = new GameObject(i == 0 ? "CrossH" : "CrossV");
                tick.transform.SetParent(baseObj.transform, false);
                var tRect = tick.AddComponent<RectTransform>();
                tRect.sizeDelta = i == 0 ? new Vector2(150f, 1.5f) : new Vector2(1.5f, 150f);
                var tImg = tick.AddComponent<Image>();
                tImg.sprite = Rounded;
                tImg.type = Image.Type.Sliced;
                tImg.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.14f);
                tImg.raycastTarget = false;
            }

            var knobObj = new GameObject("JoystickKnob");
            knobObj.transform.SetParent(baseObj.transform, false);
            _knob = knobObj.AddComponent<RectTransform>();
            _knob.sizeDelta = new Vector2(104f, 104f);
            var knobImg = knobObj.AddComponent<Image>();
            knobImg.sprite = Circle;
            knobImg.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.32f);
            knobImg.raycastTarget = false;

            var knobRing = NewImage(knobObj.transform, "KnobRing", Ring, joystickColor,
                new Vector2(104f, 104f), Vector2.zero);
            knobRing.raycastTarget = false;

            var label = NewText(canvasRoot: root, "SteerLabel", "BARRE", 14, new Color(Ink.r, Ink.g, Ink.b, 0.6f));
            var lRect = label.rectTransform;
            lRect.anchorMin = lRect.anchorMax = new Vector2(0f, 0f);
            lRect.pivot = new Vector2(0.5f, 0f);
            lRect.anchoredPosition = new Vector2(210f, 366f);
            lRect.sizeDelta = new Vector2(200f, 22f);
        }

        void BuildThrottle(Transform root)
        {
            var panel = NewPanel(root, "ThrottleBG", new Vector2(1f, 0f), new Vector2(1f, 0f),
                new Vector2(-60f, 40f), new Vector2(78f, 300f), new Vector2(1f, 0f));
            _rail = panel.GetComponent<RectTransform>();

            var track = NewImage(panel.transform, "Track", Rounded, new Color(1f, 1f, 1f, 0.06f),
                new Vector2(-16f, -16f), Vector2.zero);
            track.type = Image.Type.Sliced;
            track.rectTransform.anchorMin = Vector2.zero;
            track.rectTransform.anchorMax = Vector2.one;
            track.rectTransform.offsetMin = new Vector2(6f, 6f);
            track.rectTransform.offsetMax = new Vector2(-6f, -6f);

            var fillObj = new GameObject("RailFill");
            fillObj.transform.SetParent(panel.transform, false);
            var fRect = fillObj.AddComponent<RectTransform>();
            fRect.anchorMin = Vector2.zero;
            fRect.anchorMax = Vector2.one;
            fRect.offsetMin = new Vector2(10f, 8f);
            fRect.offsetMax = new Vector2(-10f, -8f);
            _railFill = fillObj.AddComponent<Image>();
            _railFill.sprite = Rounded;
            _railFill.type = Image.Type.Filled;
            _railFill.fillMethod = Image.FillMethod.Vertical;
            _railFill.fillOrigin = (int)Image.OriginVertical.Bottom;
            _railFill.fillAmount = 0f;
            _railFill.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.22f);
            _railFill.raycastTarget = false;

            // Graduations
            for (int i = 1; i <= 3; i++)
            {
                var tick = NewImage(panel.transform, "Tick" + i, Rounded, new Color(1f, 1f, 1f, 0.12f),
                    new Vector2(26f, 1.5f), new Vector2(0f, -150f + i * 75f));
                tick.type = Image.Type.Sliced;
                tick.raycastTarget = false;
            }

            var handleObj = new GameObject("ThrottleHandle");
            handleObj.transform.SetParent(panel.transform, false);
            _handle = handleObj.AddComponent<RectTransform>();
            _handle.sizeDelta = new Vector2(96f, 34f);
            _handle.anchoredPosition = Vector2.zero;
            var handleImg = handleObj.AddComponent<Image>();
            handleImg.sprite = Rounded;
            handleImg.type = Image.Type.Sliced;
            handleImg.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.85f);
            handleImg.raycastTarget = false;

            var grip = NewImage(handleObj.transform, "Grip", Rounded, new Color(0.02f, 0.1f, 0.2f, 0.8f),
                new Vector2(64f, 4f), Vector2.zero);
            grip.type = Image.Type.Sliced;
            grip.raycastTarget = false;

            var label = NewText(root, "PowerLabel", "MOTEUR", 14, new Color(Ink.r, Ink.g, Ink.b, 0.6f));
            var lRect = label.rectTransform;
            lRect.anchorMin = lRect.anchorMax = new Vector2(1f, 0f);
            lRect.pivot = new Vector2(0.5f, 0f);
            lRect.anchoredPosition = new Vector2(-60f, 356f);
            lRect.sizeDelta = new Vector2(200f, 22f);
        }

        void BuildActions(Transform root)
        {
            _pingLabel = CreateAction(root, "Ping", "SONAR", new Vector2(-170f, 156f), Cyan,
                () => { if (sonar != null) sonar.Ping(); }, out _pingRing, out _pingCooldown);
            CreateAction(root, "Silent", "SILENCE", new Vector2(-170f, 46f), Mint,
                () => { if (controller != null) controller.ToggleSilentRunning(); }, out _silentRing, out _);
            CreateAction(root, "Light", "PHARES", new Vector2(-280f, 156f), Amber,
                () => { if (battery != null) battery.ToggleLights(); }, out _lightRing, out _);
            _repairLabel = CreateAction(root, "Repair", "RÉPARER", new Vector2(-280f, 46f), Coral,
                () => { if (hull != null) hull.Repair(); }, out _repairRing, out _);

            _repairCount = NewText(_repairLabel.transform, "Count", "", 12, Coral);
            var cRect = _repairCount.rectTransform;
            cRect.anchorMin = cRect.anchorMax = new Vector2(0.5f, 0f);
            cRect.pivot = new Vector2(0.5f, 0f);
            cRect.anchoredPosition = new Vector2(0f, -4f);
            cRect.sizeDelta = new Vector2(60f, 16f);
        }

        Text CreateAction(Transform root, string name, string label, Vector2 position, Color accent,
            UnityEngine.Events.UnityAction onClick, out Image ring, out Image cooldown)
        {
            var obj = new GameObject(name + "Btn");
            obj.transform.SetParent(root, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(116f, 116f);

            var bg = obj.AddComponent<Image>();
            bg.sprite = Circle;
            bg.color = new Color(0.035f, 0.11f, 0.2f, 0.72f);
            bg.raycastTarget = true;

            ring = NewImage(obj.transform, "Accent", Ring, new Color(accent.r, accent.g, accent.b, 0.55f),
                new Vector2(110f, 110f), Vector2.zero);
            ring.raycastTarget = false;

            cooldown = NewImage(obj.transform, "Cooldown", Ring, new Color(accent.r, accent.g, accent.b, 0.9f),
                new Vector2(100f, 100f), Vector2.zero);
            cooldown.type = Image.Type.Filled;
            cooldown.fillMethod = Image.FillMethod.Radial360;
            cooldown.fillOrigin = (int)Image.Origin360.Top;
            cooldown.fillClockwise = true;
            cooldown.fillAmount = 0f;
            cooldown.raycastTarget = false;

            var text = NewText(obj.transform, "Label", label, 18, accent);
            text.fontStyle = FontStyle.Bold;
            text.resizeTextForBestFit = false;

            var button = obj.AddComponent<Button>();
            button.targetGraphic = bg;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
            colors.pressedColor = new Color(0.7f, 0.8f, 0.9f, 1f);
            colors.fadeDuration = 0.06f;
            button.colors = colors;
            button.onClick.AddListener(onClick);
            return text;
        }

        // ------------------------------------------------------------------
        // Retour visuel
        // ------------------------------------------------------------------

        void Update()
        {
            if (_canvas == null || !_canvas.gameObject.activeInHierarchy) return;
            if (!_homed)
            {
                _homed = true;
                LayoutHome();
            }
            UpdatePointers();
            UpdateFeedback();
        }

        void UpdateFeedback()
        {
            if (sonar != null)
            {
                float total = Mathf.Max(0.01f, sonar.pingCooldown);
                _pingCooldown.fillAmount = Mathf.Clamp01(sonar.PingCooldownRemaining / total);
                _pingLabel.color = sonar.CanPing ? Cyan : new Color(Cyan.r, Cyan.g, Cyan.b, 0.35f);
                _pingRing.color = new Color(Cyan.r, Cyan.g, Cyan.b, sonar.CanPing ? 0.55f : 0.16f);
            }

            if (controller != null)
            {
                bool silent = controller.IsSilentRunning;
                _silentRing.color = new Color(Mint.r, Mint.g, Mint.b, silent ? 0.95f : 0.35f);
            }

            if (battery != null)
            {
                bool on = battery.LightsOn;
                _lightRing.color = new Color(Amber.r, Amber.g, Amber.b, on ? 0.95f : 0.35f);
            }

            if (hull != null)
            {
                int left = hull.RepairsLeft;
                bool canRepair = left > 0 && hull.HullPercent < 0.999f;
                _repairRing.color = new Color(Coral.r, Coral.g, Coral.b, canRepair ? 0.85f : 0.2f);
                _repairLabel.color = canRepair ? Coral : new Color(Coral.r, Coral.g, Coral.b, 0.35f);
                _repairCount.text = left > 0 ? "×" + left : "";
            }

            // Le rail suit aussi les entrées clavier.
            if (_thrId == -1)
            {
                float shown = controller != null ? controller.Throttle : _throttle;
                _throttle = Mathf.Lerp(_throttle, shown, Time.deltaTime * 8f);
                ApplyRail();
            }
        }

        void ApplyRail()
        {
            float half = _rail.rect.height * 0.5f - 20f;
            _handle.anchoredPosition = new Vector2(0f, _throttle * half);
            _railFill.fillAmount = Mathf.Abs(_throttle);
            _railFill.color = _throttle < 0f
                ? new Color(Coral.r, Coral.g, Coral.b, 0.35f)
                : new Color(Cyan.r, Cyan.g, Cyan.b, 0.28f);
        }

        // ------------------------------------------------------------------
        // Entrées
        // ------------------------------------------------------------------

        void UpdatePointers()
        {
            var touchscreen = Touchscreen.current;
            if (touchscreen != null)
            {
                var touches = touchscreen.touches;
                for (int i = 0; i < touches.Count; i++)
                {
                    var touch = touches[i];
                    int id = touch.touchId.ReadValue();
                    Vector2 pos = touch.position.ReadValue();

                    if (touch.press.wasPressedThisFrame) BeginPointer(id, pos);
                    else if (touch.press.isPressed) MovePointer(id, pos);
                    else if (touch.press.wasReleasedThisFrame) EndPointer(id);
                }
            }

            var mouse = Mouse.current;
            if (mouse != null)
            {
                if (mouse.leftButton.wasPressedThisFrame) BeginPointer(MouseId, mouse.position.ReadValue());
                else if (mouse.leftButton.isPressed) MovePointer(MouseId, mouse.position.ReadValue());
                else if (mouse.leftButton.wasReleasedThisFrame) EndPointer(MouseId);
            }
        }

        void BeginPointer(int id, Vector2 screenPos)
        {
            if (IsOverUI(id)) return;

            if (screenPos.x < Screen.width * 0.45f && screenPos.y < Screen.height * 0.55f)
            {
                if (_joyId != -1) return;
                if (!ToCanvas(screenPos, out var local)) return;

                _joyId = id;
                var half = _canvasRect.rect.size * 0.5f;
                _joyOrigin = new Vector2(
                    Mathf.Clamp(local.x, -half.x + 150f, -half.x + _canvasRect.rect.width * 0.45f),
                    Mathf.Clamp(local.y, -half.y + 150f, -half.y + _canvasRect.rect.height * 0.55f));
                _base.anchoredPosition = _joyOrigin;
                _knob.anchoredPosition = Vector2.zero;
                _baseGroup.alpha = 0.95f;
                return;
            }

            if (_thrId == -1 && IsOverThrottle(screenPos))
            {
                _thrId = id;
                MoveThrottle(screenPos);
            }
        }

        // Zone de saisie du rail : le rect du rail élargi de 46 px.
        bool IsOverThrottle(Vector2 screenPos)
        {
            if (_rail == null) return false;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rail, screenPos, null, out var local))
                return false;
            var rect = _rail.rect;
            return local.x >= rect.xMin - 46f && local.x <= rect.xMax + 46f
                && local.y >= rect.yMin - 46f && local.y <= rect.yMax + 46f;
        }

        void MovePointer(int id, Vector2 screenPos)
        {
            if (id == _joyId) MoveJoystick(screenPos);
            else if (id == _thrId) MoveThrottle(screenPos);
        }

        void EndPointer(int id)
        {
            if (id == _joyId)
            {
                _joyId = -1;
                _knob.anchoredPosition = Vector2.zero;
                _base.anchoredPosition = _joyHome;
                _baseGroup.alpha = 0.55f;
                if (input != null) input.SetJoystickInput(Vector2.zero);
            }
            else if (id == _thrId)
            {
                _thrId = -1;
            }
        }

        void MoveJoystick(Vector2 screenPos)
        {
            if (!ToCanvas(screenPos, out var local)) return;
            Vector2 delta = Vector2.ClampMagnitude(local - _joyOrigin, joystickRadius);
            _knob.anchoredPosition = delta;
            if (input != null) input.SetJoystickInput(delta / joystickRadius);
        }

        void MoveThrottle(Vector2 screenPos)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rail, screenPos, null, out var local))
                return;

            float half = _rail.rect.height * 0.5f - 20f;
            _throttle = Mathf.Clamp(local.y / half, -1f, 1f);
            ApplyRail();

            if (input != null) input.SetThrottleInput(_throttle);
        }

        static bool IsOverUI(int id)
        {
            var events = EventSystem.current;
            if (events == null) return false;
            return id == MouseId
                ? events.IsPointerOverGameObject()
                : events.IsPointerOverGameObject(id);
        }

        bool ToCanvas(Vector2 screenPos, out Vector2 local)
        {
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screenPos, null, out local);
        }

        void LayoutHome()
        {
            _joyHome = new Vector2(-_canvasRect.rect.width * 0.5f + 210f, -_canvasRect.rect.height * 0.5f + 210f);
            _base.anchoredPosition = _joyHome;
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        static GameObject NewPanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 position, Vector2 size, Vector2 pivot)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            var img = obj.AddComponent<Image>();
            img.sprite = Rounded;
            img.type = Image.Type.Sliced;
            img.color = new Color(0.03f, 0.1f, 0.2f, 0.55f);
            img.raycastTarget = false;

            var outline = obj.AddComponent<Outline>();
            outline.effectColor = new Color(0.35f, 0.8f, 1f, 0.22f);
            outline.effectDistance = new Vector2(1f, -1f);
            return obj;
        }

        static Image NewImage(Transform parent, string name, Sprite sprite, Color color, Vector2 size, Vector2 pos)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            var img = obj.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static Text NewText(Transform canvasRoot, string name, string value, int size, Color color)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(canvasRoot, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(200f, 28f);
            var text = obj.AddComponent<Text>();
            text.text = value;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.font = Font;
            return text;
        }

        static Font Font
        {
            get
            {
                var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                return font;
            }
        }

        static Sprite Circle
        {
            get
            {
                if (_circle != null) return _circle;
                const int size = 128;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp;
                var px = new Color[size * size];
                Vector2 c = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
                float r = size * 0.5f - 1f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c);
                    px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(r - d + 1f));
                }
                tex.SetPixels(px);
                tex.Apply();
                _circle = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
                return _circle;
            }
        }

        static Sprite Ring
        {
            get
            {
                if (_ring != null) return _ring;
                const int size = 128;
                const float radius = 56f;
                const float thickness = 4f;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp;
                var px = new Color[size * size];
                Vector2 c = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c);
                    px[y * size + x] = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01(Mathf.Abs(d - radius) / thickness));
                }
                tex.SetPixels(px);
                tex.Apply();
                _ring = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
                return _ring;
            }
        }

        static Sprite Rounded
        {
            get
            {
                if (_rounded != null) return _rounded;
                const int size = 64;
                const int radius = 16;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp;
                var px = new Color[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float alpha = 1f;
                    float px0 = x + 0.5f, py0 = y + 0.5f;
                    float dx = 0f, dy = 0f;
                    if (px0 < radius) dx = radius - px0;
                    else if (px0 > size - radius) dx = px0 - (size - radius);
                    if (py0 < radius) dy = radius - py0;
                    else if (py0 > size - radius) dy = py0 - (size - radius);
                    if (dx > 0f || dy > 0f) alpha = Mathf.Clamp01(radius - Mathf.Sqrt(dx * dx + dy * dy) + 0.5f);
                    px[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
                tex.SetPixels(px);
                tex.Apply();
                var border = new Vector4(radius, radius, radius, radius);
                _rounded = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                    SpriteMeshType.FullRect, border);
                return _rounded;
            }
        }
    }
}
