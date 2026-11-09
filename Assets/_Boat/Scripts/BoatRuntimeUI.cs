using UnityEngine;
using UnityEngine.UI;

public sealed class BoatRuntimeUI : MonoBehaviour
{
    sealed class PowerRow
    {
        public GameObject root;
        public RawImage icon;
        public Image bar;
        public Text label;
        public Text timer;
    }

    static readonly BoatPowerUpType[] powerTypes =
    {
        BoatPowerUpType.Shield, BoatPowerUpType.Magnet, BoatPowerUpType.DoubleScore, BoatPowerUpType.Turbo
    };
    static readonly string[] labels = { "SHIELD", "MAGNET", "DOUBLE SCORE", "TURBO" };

    BoatGameManager manager;
    Canvas canvas;
    Text oldPowerUpText;
    PowerRow[] rows;
    GameObject pausePanel;
    Button pauseButton;
    Image flashImage;
    float flashRemaining;
    const float FlashDuration = 0.28f;

    void Awake()
    {
        manager = GetComponent<BoatGameManager>();
        var canvasObject = GameObject.Find("Game UI");
        canvas = canvasObject == null ? FindAnyObjectByType<Canvas>() : canvasObject.GetComponent<Canvas>();
        if (canvas == null) return;
        BuildPowerUpHud();
        BuildPauseUi();
        BuildDamageFlash();
        UpdateStartHint();
    }

    void Update()
    {
        RefreshPowerUps();
        if (flashImage != null)
        {
            flashRemaining = Mathf.Max(0f, flashRemaining - Time.unscaledDeltaTime);
            Color color = flashImage.color;
            color.a = 0.44f * Mathf.Clamp01(flashRemaining / FlashDuration);
            flashImage.color = color;
        }
    }

    public void RefreshPowerUps()
    {
        if (rows == null || manager == null) return;
        int visibleIndex = 0;
        for (int i = 0; i < rows.Length; i++)
        {
            if (rows[i] == null || rows[i].root == null)
                rows[i] = CreatePowerRow(i);
            float remaining = manager.GetPowerUpRemaining(powerTypes[i]);
            bool active = remaining > 0f;
            rows[i].root.SetActive(active);
            if (!active) continue;
            RectTransform rowRect = rows[i].root.GetComponent<RectTransform>();
            rowRect.anchoredPosition = new Vector2(0f, -visibleIndex * 35f);
            visibleIndex++;
            rows[i].label.text = labels[i];
            rows[i].timer.text = remaining.ToString("0.0") + "s";
            rows[i].bar.fillAmount = Mathf.Clamp01(remaining / Mathf.Max(0.01f, manager.GetPowerUpDuration(powerTypes[i])));
        }
        if (oldPowerUpText != null) oldPowerUpText.gameObject.SetActive(visibleIndex > 0);
    }

    public void SetPauseButtonVisible(bool visible)
    {
        if (pauseButton != null) pauseButton.gameObject.SetActive(visible);
    }

    public void SetPaused(bool paused)
    {
        if (pausePanel != null) pausePanel.SetActive(paused);
        SetPauseButtonVisible(!paused && manager != null && manager.IsPlaying);
    }

    public void FlashDamage()
    {
        if (flashImage == null) return;
        flashRemaining = FlashDuration;
        flashImage.transform.SetAsLastSibling();
        Color color = flashImage.color;
        color.a = 0.44f;
        flashImage.color = color;
    }

    void BuildPowerUpHud()
    {
        oldPowerUpText = GameObject.Find("Power Ups")?.GetComponent<Text>();
        if (oldPowerUpText == null) return;
        oldPowerUpText.text = string.Empty;
        oldPowerUpText.enabled = false;
        rows = new PowerRow[powerTypes.Length];
        for (int i = 0; i < rows.Length; i++) rows[i] = CreatePowerRow(i);
        oldPowerUpText.gameObject.SetActive(false);
    }

    PowerRow CreatePowerRow(int index)
    {
        Transform parent = oldPowerUpText.transform;
        var rowObject = new GameObject("Power-Up HUD " + labels[index], typeof(RectTransform));
        rowObject.transform.SetParent(parent, false);
        var rowRect = rowObject.GetComponent<RectTransform>();
        rowRect.anchorMin = rowRect.anchorMax = new Vector2(0f, 1f);
        rowRect.pivot = new Vector2(0f, 1f);
        rowRect.anchoredPosition = Vector2.zero;
        rowRect.sizeDelta = new Vector2(430f, 32f);

        var icon = CreateRawImage("Icon", rowObject.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(0f, 0f), new Vector2(30f, 30f));
        icon.texture = PowerUpIconLibrary.GetTexture(powerTypes[index]);
        icon.color = PowerUpIconLibrary.GetColor(powerTypes[index]);

        Text label = CreateText("Name", rowObject.transform, labels[index], 15, TextAnchor.MiddleLeft,
            new Vector2(38f, 0f), new Vector2(116f, 30f));
        label.color = new Color(0.95f, 0.98f, 0.99f);

        var barBack = CreateImage("Timer Track", rowObject.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
            new Vector2(160f, 0f), new Vector2(146f, 10f), new Color(0.025f, 0.11f, 0.17f, 0.88f));
        var fillObject = new GameObject("Timer Fill", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fillObject.transform.SetParent(barBack.transform, false);
        var fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(1f, 1f);
        fillRect.pivot = new Vector2(0f, 0.5f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        var fill = fillObject.GetComponent<Image>();
        fill.color = PowerUpIconLibrary.GetColor(powerTypes[index]);
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillOrigin = (int)Image.OriginHorizontal.Left;

        Text timer = CreateText("Time", rowObject.transform, "", 14, TextAnchor.MiddleRight,
            new Vector2(318f, 0f), new Vector2(63f, 30f));
        timer.color = Color.white;
        return new PowerRow { root = rowObject, icon = icon, bar = fill, label = label, timer = timer };
    }

    void BuildPauseUi()
    {
        pauseButton = CreateButton("Pause Button", "PAUSE", canvas.transform,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-34f, -36f), new Vector2(150f, 56f),
            () => manager?.TogglePause());

        pausePanel = new GameObject("Pause Panel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        pausePanel.transform.SetParent(canvas.transform, false);
        var panelRect = pausePanel.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        var panelImage = pausePanel.GetComponent<Image>();
        panelImage.color = new Color(0.015f, 0.075f, 0.12f, 0.88f);
        panelImage.raycastTarget = true;
        CreateCenteredText("Paused Title", "PAUSED", pausePanel.transform, new Vector2(0f, 115f), new Vector2(440f, 90f), 48);
        CreateButton("Resume Button", "RESUME", pausePanel.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), new Vector2(240f, 64f),
            () => manager?.ResumeGame());
        CreateButton("Pause Restart Button", "RESTART", pausePanel.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -66f), new Vector2(240f, 64f),
            () => manager?.Restart());
        CreateButton("Pause Menu Button", "RETOUR", pausePanel.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -146f), new Vector2(240f, 64f),
            () => manager?.LeaveToHub());
        pausePanel.SetActive(false);
    }

    void BuildDamageFlash()
    {
        var flash = new GameObject("Hit Screen Flash", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        flash.transform.SetParent(canvas.transform, false);
        var rect = flash.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        flashImage = flash.GetComponent<Image>();
        flashImage.color = new Color(0.96f, 0.08f, 0.06f, 0f);
        flashImage.raycastTarget = false;
        flash.transform.SetAsLastSibling();
    }

    void UpdateStartHint()
    {
        var hint = GameObject.Find("Controls Hint")?.GetComponent<Text>();
        if (hint != null)
            hint.text = Application.isMobilePlatform
                ? "TILT TO STEER\nTap to start"
                : "A / D or ← / → to steer\nW / S to accelerate / brake";
        var play = GameObject.Find("Play")?.GetComponent<Button>();
        var playLabel = play == null ? null : play.GetComponentInChildren<Text>();
        if (playLabel != null && Application.isMobilePlatform) playLabel.text = "TAP TO START";
    }

    static Button CreateButton(string name, string caption, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
    {
        var buttonObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        var rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = anchorMax;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.04f, 0.20f, 0.29f, 0.97f);
        var button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        if (action != null) button.onClick.AddListener(action);
        var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        labelObject.transform.SetParent(buttonObject.transform, false);
        var labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        var label = labelObject.GetComponent<Text>();
        label.text = caption;
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (label.font == null) label.font = Font.CreateDynamicFontFromOSFont("Arial", 18);
        label.fontSize = 18;
        label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color(0.94f, 0.98f, 0.99f);
        label.raycastTarget = false;
        return button;
    }

    static void CreateCenteredText(string name, string value, Transform parent, Vector2 position, Vector2 size, int fontSize)
    {
        var label = CreateText(name, parent, value, fontSize, TextAnchor.MiddleCenter, Vector2.zero, size);
        var rect = label.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        label.color = Color.white;
        label.fontStyle = FontStyle.Bold;
    }

    static Text CreateText(string name, Transform parent, string value, int size, TextAnchor anchor,
        Vector2 position, Vector2 dimensions)
    {
        var textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(parent, false);
        var rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
        var text = textObject.GetComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (text.font == null) text.font = Font.CreateDynamicFontFromOSFont("Arial", size);
        text.fontSize = size;
        text.fontStyle = FontStyle.Bold;
        text.alignment = anchor;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.color = Color.white;
        return text;
    }

    static RawImage CreateRawImage(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 position, Vector2 size)
    {
        var imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        imageObject.transform.SetParent(parent, false);
        var rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return imageObject.GetComponent<RawImage>();
    }

    static Image CreateImage(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax,
        Vector2 position, Vector2 size, Color color)
    {
        var imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        var rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        var image = imageObject.GetComponent<Image>();
        image.color = color;
        return image;
    }

}
