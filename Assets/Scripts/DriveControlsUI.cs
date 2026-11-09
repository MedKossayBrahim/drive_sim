using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>
/// Builds the touch driving controls at runtime:
///   - an automatic-transmission style R N D lever that slides left/right,
///   - gas and brake pedals.
///
/// Nothing has to be added to the scene; the controls appear automatically
/// whenever a GyroCarController is present.
/// </summary>
public class DriveControlsUI : MonoBehaviour
{
    private const int SlotCount = 3;
    private const float SlotSpacing = 90f;

    private static readonly string[] GearLetters = { "R", "N", "D" };

    private static readonly Color GasNormalColor = new Color(0.16f, 0.55f, 0.27f, 0.92f);
    private static readonly Color GasPressedColor = new Color(0.30f, 0.90f, 0.45f, 1f);
    private static readonly Color BrakeNormalColor = new Color(0.62f, 0.16f, 0.16f, 0.92f);
    private static readonly Color BrakePressedColor = new Color(1f, 0.32f, 0.28f, 1f);
    private static readonly Color GearIdleColor = new Color(1f, 1f, 1f, 0.42f);
    private static readonly Color GearActiveColor = new Color(1f, 0.78f, 0.30f, 1f);

    private static Sprite roundedSprite;
    private static Sprite circleSprite;
    private static Font uiFont;

    private GyroCarController car;

    private RectTransform safeAreaRoot;
    private RectTransform leverTrack;
    private RectTransform leverKnob;
    private readonly Text[] gearLabels = new Text[SlotCount];

    private Image gasPedalImage;
    private Image brakePedalImage;
    private bool gasPressed;
    private bool brakePressed;

    private int leverIndex;
    private float leverTargetX;
    private bool draggingLever;
    private int dragPointerId = -1;

    private Text messageText;
    private Color messageColor = Color.white;
    private float messageTimer;
    private Rect lastSafeArea;

    // ------------------------------------------------------------------
    // Bootstrap
    // ------------------------------------------------------------------

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Object.FindAnyObjectByType<GyroCarController>() == null)
            return;

        if (Object.FindAnyObjectByType<DriveControlsUI>() != null)
            return;

        GameObject go = new GameObject("DriveControls");
        go.AddComponent<DriveControlsUI>();
    }

    private void Start()
    {
        car = Object.FindAnyObjectByType<GyroCarController>();
        if (car == null)
        {
            Debug.LogWarning("[DriveControlsUI] No GyroCarController in the scene; touch controls disabled.");
            enabled = false;
            return;
        }

        BuildUI();
    }

    private void OnDisable()
    {
        if (car != null)
            car.ClearPedalInput();
    }

    private void Update()
    {
        if (car == null)
            return;

        RefreshSafeArea();
        ReleaseStuckPedals();

        car.SetPedalInput(gasPressed ? 1f : 0f, brakePressed ? 1f : 0f);

        UpdateGearVisuals();
        UpdatePedalVisuals();
        UpdateMessage();
    }

    // ------------------------------------------------------------------
    // UI construction
    // ------------------------------------------------------------------

    private void BuildUI()
    {
        if (uiFont == null) uiFont = LoadFont();
        if (roundedSprite == null) roundedSprite = CreateRoundedSprite(64, 18);
        if (circleSprite == null) circleSprite = CreateCircleSprite(64);

        GameObject canvasGO = new GameObject("DriveControlsCanvas", typeof(RectTransform));
        canvasGO.transform.SetParent(transform, false);

        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        canvasGO.AddComponent<GraphicRaycaster>();

        EnsureEventSystem();

        safeAreaRoot = CreateRect("SafeArea", canvasGO.transform);
        Stretch(safeAreaRoot);

        BuildTransmissionLever(safeAreaRoot);
        BuildPedals(safeAreaRoot);
        BuildRecalibrateButton(safeAreaRoot);
        BuildHintAndMessage(safeAreaRoot);

        RefreshSafeArea(true);
    }

    private void BuildTransmissionLever(Transform parent)
    {
        RectTransform panel = CreateRect("TransmissionPanel", parent);
        SetAnchored(panel, new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(36f, 30f), new Vector2(470f, 190f));

        Image panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.sprite = roundedSprite;
        panelImage.type = Image.Type.Sliced;
        panelImage.color = new Color(0.03f, 0.04f, 0.07f, 0.66f);
        panelImage.raycastTarget = false;

        Text title = CreateText(panel, "AUTOMATIC  R  N  D", 22, TextAnchor.UpperLeft,
            new Color(1f, 1f, 1f, 0.55f));
        SetAnchored(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(24f, -14f), new Vector2(280f, 30f));

        leverTrack = CreateRect("LeverTrack", panel);
        SetAnchored(leverTrack, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
            new Vector2(0f, 58f), new Vector2(390f, 60f));

        Image trackImage = leverTrack.gameObject.AddComponent<Image>();
        trackImage.sprite = roundedSprite;
        trackImage.type = Image.Type.Sliced;
        trackImage.color = new Color(0.14f, 0.16f, 0.22f, 0.96f);
        trackImage.raycastTarget = true;

        for (int i = 0; i < SlotCount; i++)
        {
            Text label = CreateText(panel, GearLetters[i], 30, TextAnchor.MiddleCenter, GearIdleColor);
            SetAnchored(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f),
                new Vector2(SlotX(i), 112f), new Vector2(60f, 44f));
            gearLabels[i] = label;
        }

        leverKnob = CreateRect("LeverKnob", leverTrack);
        SetAnchored(leverKnob, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            new Vector2(SlotX(CurrentSlot()), 0f), new Vector2(72f, 72f));

        Image knobImage = leverKnob.gameObject.AddComponent<Image>();
        knobImage.sprite = circleSprite;
        knobImage.color = new Color(1f, 0.72f, 0.20f, 1f);
        knobImage.raycastTarget = false;

        RectTransform knobDot = CreateRect("KnobDot", leverKnob);
        Stretch(knobDot, 24f);
        Image dotImage = knobDot.gameObject.AddComponent<Image>();
        dotImage.sprite = circleSprite;
        dotImage.color = new Color(1f, 0.92f, 0.65f, 0.9f);
        dotImage.raycastTarget = false;

        EventTrigger trigger = leverTrack.gameObject.AddComponent<EventTrigger>();
        AddTrigger(trigger, EventTriggerType.PointerDown, OnLeverPointerDown);
        AddTrigger(trigger, EventTriggerType.Drag, OnLeverDrag);
        AddTrigger(trigger, EventTriggerType.PointerUp, OnLeverPointerUp);
        AddTrigger(trigger, EventTriggerType.EndDrag, OnLeverPointerUp);
    }

    private void BuildPedals(Transform parent)
    {
        gasPedalImage = BuildPedal(parent, "GasPedal", "GAS", GasNormalColor,
            new Vector2(-30f, 26f), new Vector2(190f, 320f),
            pressed => gasPressed = pressed);

        brakePedalImage = BuildPedal(parent, "BrakePedal", "BRAKE", BrakeNormalColor,
            new Vector2(-240f, 26f), new Vector2(170f, 250f),
            pressed => brakePressed = pressed);
    }

    private Image BuildPedal(Transform parent, string name, string label, Color color,
        Vector2 anchoredPosition, Vector2 size, System.Action<bool> setPressed)
    {
        RectTransform rt = CreateRect(name, parent);
        SetAnchored(rt, new Vector2(1f, 0f), new Vector2(1f, 0f), anchoredPosition, size);

        Image image = rt.gameObject.AddComponent<Image>();
        image.sprite = roundedSprite;
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = true;

        Text text = CreateText(rt, label, 34, TextAnchor.MiddleCenter,
            new Color(1f, 1f, 1f, 0.95f));
        Stretch(text.rectTransform, 10f);

        EventTrigger trigger = rt.gameObject.AddComponent<EventTrigger>();
        AddTrigger(trigger, EventTriggerType.PointerDown, _ => setPressed(true));
        AddTrigger(trigger, EventTriggerType.PointerUp, _ => setPressed(false));

        return image;
    }

    private void BuildRecalibrateButton(Transform parent)
    {
        RectTransform rt = CreateRect("RecalibrateButton", parent);
        SetAnchored(rt, new Vector2(1f, 1f), new Vector2(1f, 1f),
            new Vector2(-30f, -30f), new Vector2(220f, 64f));

        Image image = rt.gameObject.AddComponent<Image>();
        image.sprite = roundedSprite;
        image.type = Image.Type.Sliced;
        image.color = new Color(0.07f, 0.10f, 0.15f, 0.82f);

        Button button = rt.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.85f, 0.90f, 1f, 1f);
        colors.pressedColor = new Color(0.65f, 0.70f, 0.80f, 1f);
        button.colors = colors;
        button.onClick.AddListener(OnRecalibrateClicked);

        Text text = CreateText(rt, "RECALIBRATE", 24, TextAnchor.MiddleCenter,
            new Color(1f, 1f, 1f, 0.90f));
        Stretch(text.rectTransform, 6f);
    }

    private void BuildHintAndMessage(Transform parent)
    {
        Text hint = CreateText(parent, "Tilt to steer  •  Hold GAS / BRAKE  •  Drag lever R N D",
            24, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.45f));
        SetAnchored(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 26f), new Vector2(1100f, 40f));

        messageText = CreateText(parent, string.Empty, 30, TextAnchor.MiddleCenter,
            new Color(1f, 1f, 1f, 0f));
        SetAnchored(messageText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 84f), new Vector2(1000f, 48f));
    }

    // ------------------------------------------------------------------
    // Gear lever interaction
    // ------------------------------------------------------------------

    private void OnLeverPointerDown(BaseEventData data)
    {
        PointerEventData pointer = data as PointerEventData;
        if (pointer == null || pointer.button != PointerEventData.InputButton.Left)
            return;

        draggingLever = true;
        dragPointerId = pointer.pointerId;
        MoveLeverToPointer(pointer);
    }

    private void OnLeverDrag(BaseEventData data)
    {
        if (!draggingLever)
            return;

        PointerEventData pointer = data as PointerEventData;
        if (pointer == null || pointer.pointerId != dragPointerId)
            return;

        MoveLeverToPointer(pointer);
    }

    private void OnLeverPointerUp(BaseEventData data)
    {
        if (!draggingLever)
            return;

        PointerEventData pointer = data as PointerEventData;
        if (pointer != null && pointer.pointerId != dragPointerId)
            return;

        CommitLever();
    }

    private void MoveLeverToPointer(PointerEventData pointer)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                leverTrack, pointer.position, pointer.pressEventCamera, out Vector2 local))
            return;

        float halfRange = SlotSpacing * (SlotCount - 1) * 0.5f;
        float x = Mathf.Clamp(local.x, -halfRange, halfRange);

        leverKnob.anchoredPosition = new Vector2(x, 0f);
        leverIndex = Mathf.Clamp(
            Mathf.RoundToInt((x + halfRange) / SlotSpacing), 0, SlotCount - 1);
    }

    private void CommitLever()
    {
        draggingLever = false;
        dragPointerId = -1;

        if (!SelectSlot(leverIndex))
        {
            ShowMessage("STOP THE CAR TO CHANGE GEAR", new Color(1f, 0.45f, 0.20f));
            leverIndex = CurrentSlot();
        }

        leverTargetX = SlotX(leverIndex);
    }

    private bool SelectSlot(int slot)
    {
        switch (slot)
        {
            case 0: return car.SetReverse();
            case 1: return car.SetNeutral();
            default: return car.SetDrive();
        }
    }

    private int CurrentSlot()
    {
        if (car == null)
            return 2;

        switch (car.CurrentGear)
        {
            case GyroCarController.TransmissionGear.Reverse: return 0;
            case GyroCarController.TransmissionGear.Neutral: return 1;
            default: return 2;
        }
    }

    private static float SlotX(int slot)
    {
        return (slot - (SlotCount - 1) * 0.5f) * SlotSpacing;
    }

    // ------------------------------------------------------------------
    // Per-frame visuals
    // ------------------------------------------------------------------

    private void UpdateGearVisuals()
    {
        if (leverKnob == null)
            return;

        int current = CurrentSlot();

        if (!draggingLever)
        {
            leverIndex = current;
            leverTargetX = SlotX(current);

            Vector2 position = leverKnob.anchoredPosition;
            position.x = Mathf.MoveTowards(position.x, leverTargetX, 1400f * Time.unscaledDeltaTime);
            position.y = 0f;
            leverKnob.anchoredPosition = position;
        }

        for (int i = 0; i < SlotCount; i++)
        {
            if (gearLabels[i] == null)
                continue;

            bool highlight = draggingLever ? i == leverIndex : i == current;
            gearLabels[i].color = highlight ? GearActiveColor : GearIdleColor;
        }
    }

    private void UpdatePedalVisuals()
    {
        float t = 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime);

        if (gasPedalImage != null)
            gasPedalImage.color = Color.Lerp(
                gasPedalImage.color, gasPressed ? GasPressedColor : GasNormalColor, t);

        if (brakePedalImage != null)
            brakePedalImage.color = Color.Lerp(
                brakePedalImage.color, brakePressed ? BrakePressedColor : BrakeNormalColor, t);
    }

    private void ReleaseStuckPedals()
    {
        if (!gasPressed && !brakePressed)
            return;

        if (AnyPointerPressed())
            return;

        gasPressed = false;
        brakePressed = false;
    }

    private static bool AnyPointerPressed()
    {
        if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            return true;

        if (Touchscreen.current != null)
        {
            foreach (var touch in Touchscreen.current.touches)
            {
                if (touch.press.isPressed)
                    return true;
            }
        }

        return false;
    }

    private void OnRecalibrateClicked()
    {
        if (car == null)
            return;

        car.ForceRecalibrate();
        ShowMessage("SENSOR RE-CENTERED", new Color(0.45f, 1f, 0.55f));
    }

    private void ShowMessage(string text, Color color)
    {
        if (messageText == null)
            return;

        messageText.text = text;
        messageColor = color;
        messageTimer = 2f;
    }

    private void UpdateMessage()
    {
        if (messageText == null)
            return;

        if (messageTimer > 0f)
            messageTimer -= Time.unscaledDeltaTime;

        float alpha = Mathf.Clamp01(messageTimer / 0.4f);
        messageText.color = new Color(messageColor.r, messageColor.g, messageColor.b, alpha);
    }

    private void RefreshSafeArea(bool force = false)
    {
        if (safeAreaRoot == null || Screen.width <= 0 || Screen.height <= 0)
            return;

        Rect area = Screen.safeArea;
        if (!force && area == lastSafeArea)
            return;

        lastSafeArea = area;

        safeAreaRoot.anchorMin = new Vector2(
            area.xMin / Screen.width,
            area.yMin / Screen.height);
        safeAreaRoot.anchorMax = new Vector2(
            area.xMax / Screen.width,
            area.yMax / Screen.height);
        safeAreaRoot.offsetMin = Vector2.zero;
        safeAreaRoot.offsetMax = Vector2.zero;
    }

    // ------------------------------------------------------------------
    // Small helpers
    // ------------------------------------------------------------------

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        GameObject go = new GameObject("EventSystem", typeof(EventSystem));
        InputSystemUIInputModule module = go.AddComponent<InputSystemUIInputModule>();
        module.AssignDefaultActions();
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.localScale = Vector3.one;
        return rt;
    }

    private static void SetAnchored(RectTransform rt, Vector2 anchor, Vector2 pivot,
        Vector2 anchoredPosition, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = size;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void Stretch(RectTransform rt, float inset)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset);
        rt.offsetMax = new Vector2(-inset, -inset);
    }

    private static Text CreateText(Transform parent, string content, int fontSize,
        TextAnchor alignment, Color color)
    {
        RectTransform rt = CreateRect("Text", parent);
        Text text = rt.gameObject.AddComponent<Text>();
        text.font = uiFont;
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        return text;
    }

    private static void AddTrigger(EventTrigger trigger, EventTriggerType type,
        System.Action<BaseEventData> callback)
    {
        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
        entry.callback.AddListener(data => callback(data));
        trigger.triggers.Add(entry);
    }

    private static Font LoadFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font != null)
            return font;

        try
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
        catch
        {
            // Fall through to an OS font.
        }

        if (font == null)
            font = Font.CreateDynamicFontFromOSFont(
                new[] { "Arial", "Roboto", "Droid Sans", "Segoe UI" }, 32);

        return font;
    }

    private static Sprite CreateRoundedSprite(int size, int radius)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "DriveControlsRounded";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.hideFlags = HideFlags.HideAndDontSave;

        Color32[] pixels = new Color32[size * size];
        float r = radius;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f;
                float py = y + 0.5f;
                float dx = Mathf.Max(r - px, px - (size - r), 0f);
                float dy = Mathf.Max(r - py, py - (size - r), 0f);
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(r - distance + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
            new Vector4(radius, radius, radius, radius));
    }

    private static Sprite CreateCircleSprite(int size)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "DriveControlsCircle";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.hideFlags = HideFlags.HideAndDontSave;

        Color32[] pixels = new Color32[size * size];
        float r = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - r;
                float dy = y + 0.5f - r;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(r - distance + 0.5f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f), 100f);
    }
}
