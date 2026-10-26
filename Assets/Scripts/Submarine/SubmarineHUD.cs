using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DriveSim.Submarine
{
    /// <summary>
    /// Diegetic submarine HUD.
    /// Radial gauges, animated sonar, warning pulses, sweep and ping rings.
    /// </summary>
    public class SubmarineHUD : MonoBehaviour
    {
        [Header("References")]
        public SubmarineController controller;
        public HullSystem hull;
        public OxygenSystem oxygen;
        public BatterySystem battery;
        public SonarSystem sonar;
        public GameManager gameManager;
        public LeviathanAI creature;
        public DepthZoneManager depthZone;

        // Canvas
        Canvas _canvas;

        // Gauges
        Image _hullFill, _oxygenFill, _batteryFill;
        Text _hullValue, _oxygenValue, _batteryValue;
        float _hullShown = 1f, _oxygenShown = 1f, _batteryShown = 1f;

        // Top
        Text _depthText, _zoneText, _speedText, _scoreText, _statusText;
        Text _beaconText;
        Text _beaconDepthHint;
        RectTransform _beaconArrow;
        GameObject _beaconChip;
        float _beaconPulse;
        float _scoreShown;

        // Sonar
        RectTransform _sonarPanel;
        RectTransform _sonarCenter;
        Image _sonarSweep;
        Image _noiseFill;
        Image _noiseTrack;
        Text _noiseLabel;
        readonly List<Image> _blipPool = new List<Image>();
        readonly List<RectTransform> _blipRects = new List<RectTransform>();
        Image _pingRing;
        float _pingAnim = -1f;

        // Warning
        Image _alarmVignette;
        float _alarmPulse;
        GameObject _silentBadge;
        GameObject _lightBadge;

        // Extraction
        Image _extractFill;
        Text _extractText;
        GameObject _extractGroup;
        float _extractShown;

        // End screen
        GameObject _endOverlay;
        CanvasGroup _endGroup;
        Text _endTitle, _endBody;
        float _endShownAt;

        // Status toast
        GameObject _statusToast;

        // Briefing
        GameObject _briefingOverlay;
        CanvasGroup _briefingGroup;
        bool _briefingVisible;

        // Styles
        static readonly Color Cyan = new Color(0.25f, 0.85f, 1f);
        static readonly Color Mint = new Color(0.3f, 0.95f, 0.75f);
        static readonly Color Amber = new Color(1f, 0.75f, 0.3f);
        static readonly Color Red = new Color(1f, 0.32f, 0.32f);
        static readonly Color Ink = new Color(0.85f, 0.93f, 1f);
        static readonly Color PanelColor = new Color(0.03f, 0.09f, 0.18f, 0.72f);
        static readonly Color Edge = new Color(0.35f, 0.8f, 1f, 0.25f);

        static Font _font;
        static Sprite _circle, _ring, _rounded, _vignette;

        void Awake()
        {
            Build();
        }

        void Start()
        {
            Resolve();
            Subscribe();
            StartCoroutine(ShowBriefingDelayed());
        }

        IEnumerator ShowBriefingDelayed()
        {
            yield return null;
            if (Application.isBatchMode) yield break;
            ShowBriefing();
        }

        void Resolve()
        {
            if (controller == null) controller = FindFirstObjectByType<SubmarineController>();
            if (hull == null) hull = FindFirstObjectByType<HullSystem>();
            if (oxygen == null) oxygen = FindFirstObjectByType<OxygenSystem>();
            if (battery == null) battery = FindFirstObjectByType<BatterySystem>();
            if (sonar == null) sonar = FindFirstObjectByType<SonarSystem>();
            if (gameManager == null) gameManager = FindFirstObjectByType<GameManager>();
            if (creature == null) creature = FindFirstObjectByType<LeviathanAI>();
            if (depthZone == null) depthZone = FindFirstObjectByType<DepthZoneManager>();
        }

        void Subscribe()
        {
            if (hull != null)
            {
                hull.OnHullChanged += _ => FlashAlarm(Red);
                hull.OnCritical += () => ShowStatus("COQUE CRITIQUE", Red);
                hull.OnLeakStarted += () => ShowStatus("VOIE D'EAU — INONDATION", Red);
            }
            if (oxygen != null)
            {
                oxygen.OnOxygenLow += () => ShowStatus("OXYGÈNE BAS", Amber);
                oxygen.OnOxygenCritical += () => ShowStatus("OXYGÈNE CRITIQUE", Red);
            }
            if (battery != null)
                battery.OnBatteryCritical += () => ShowStatus("ÉNERGIE CRITIQUE", Amber);
            if (sonar != null)
                sonar.OnPing += (_, __) => StartPingRing();
            if (gameManager != null)
            {
                gameManager.OnStateChanged += HandleState;
                gameManager.OnGameOverReason += reason => ShowEnd(false, "MISSION ÉCHOUÉE", reason);
                gameManager.OnBlackBoxCollected += () => ShowStatus("BOÎTE NOIRE RÉCUPÉRÉE", Mint);
            }
        }

        void Update()
        {
            if (controller == null) return;

            UpdateGauges();
            UpdateTop();
            UpdateSonar();
            UpdateBeacon();
            UpdateWarnings();
            UpdateExtraction();
            UpdateEndInput();

            if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame && !_briefingVisible)
                ShowBriefing();

            if (_pingAnim >= 0f)
                AnimatePing();
        }

        // ------------------------------------------------------------------
        // Gauges
        // ------------------------------------------------------------------

        void UpdateGauges()
        {
            _hullShown = Mathf.Lerp(_hullShown, hull != null ? hull.HullPercent : 1f, Time.deltaTime * 6f);
            _oxygenShown = Mathf.Lerp(_oxygenShown, oxygen != null ? oxygen.OxygenPercent : 1f, Time.deltaTime * 6f);
            _batteryShown = Mathf.Lerp(_batteryShown, battery != null ? battery.BatteryPercent : 1f, Time.deltaTime * 6f);

            ApplyGauge(_hullFill, _hullValue, _hullShown, ColorFor(_hullShown));
            ApplyGauge(_oxygenFill, _oxygenValue, _oxygenShown, Color.Lerp(Red, Cyan, Mathf.InverseLerp(0.1f, 0.4f, _oxygenShown)));
            ApplyGauge(_batteryFill, _batteryValue, _batteryShown, Color.Lerp(Red, Amber, Mathf.InverseLerp(0.15f, 0.45f, _batteryShown)));
        }

        void ApplyGauge(Image fill, Text value, float amount, Color color)
        {
            if (fill != null)
            {
                fill.fillAmount = amount;
                fill.color = color;
            }
            if (value != null)
                value.text = Mathf.RoundToInt(amount * 100f) + "%";
        }

        static Color ColorFor(float v)
        {
            if (v > 0.6f) return Mint;
            if (v > 0.3f) return Amber;
            return Red;
        }

        void UpdateTop()
        {
            if (_depthText != null)
                _depthText.text = Mathf.Max(0f, controller.Depth).ToString("F0") + " m";
            if (_speedText != null)
                _speedText.text = controller.Speed.ToString("F1") + " kn";
            if (_zoneText != null && depthZone != null)
                _zoneText.text = depthZone.CurrentZoneName.ToUpperInvariant();
            if (_scoreText != null && gameManager != null)
            {
                _scoreShown = Mathf.Lerp(_scoreShown, gameManager.Score, Time.deltaTime * 5f);
                _scoreText.text = "SCORE  " + Mathf.RoundToInt(_scoreShown).ToString("N0");
            }
        }

        // Balise acoustique : la distance n'apparaît qu'à portée d'écoute.
        void UpdateBeacon()
        {
            if (_beaconChip == null || _beaconText == null) return;
            if (gameManager == null || gameManager.blackBoxLocation == null || controller == null)
                return;

            if (gameManager.BlackBoxCollected)
            {
                if (_beaconChip.activeSelf) _beaconChip.SetActive(false);
                return;
            }

            Vector3 subPos = controller.transform.position;
            Vector3 boxPos = gameManager.blackBoxLocation.position;
            float distance = Vector3.Distance(subPos, boxPos);

            if (!_beaconChip.activeSelf) _beaconChip.SetActive(true);

            _beaconPulse += Time.deltaTime;
            float pulse = 0.7f + 0.3f * Mathf.Sin(_beaconPulse * 3f);
            _beaconText.text = "OBJECTIF · BOÎTE NOIRE   " + Mathf.RoundToInt(distance) + " m";
            _beaconText.color = Color.Lerp(Amber, Ink, pulse);

            // Depth hint
            if (_beaconDepthHint != null)
            {
                float depthDiff = (-boxPos.y) - (-subPos.y); // positive = box is deeper
                // Direction in horizontal plane
                Vector3 toBox = boxPos - subPos;
                Vector3 forward = controller.transform.forward;
                Vector3 right = controller.transform.right;
                float fDot = Vector3.Dot(toBox.normalized, new Vector3(forward.x, 0, forward.z).normalized);
                float rDot = Vector3.Dot(toBox.normalized, new Vector3(right.x, 0, right.z).normalized);

                string dirHint = "";
                float horizDist = new Vector2(toBox.x, toBox.z).magnitude;
                if (horizDist > 15f)
                {
                    if (Mathf.Abs(fDot) > Mathf.Abs(rDot))
                        dirHint = fDot > 0 ? " · DEVANT" : " · DERRIÈRE";
                    else
                        dirHint = rDot > 0 ? " · DROITE" : " · GAUCHE";
                }

                if (Mathf.Abs(depthDiff) > 5f)
                {
                    string depthDir = depthDiff > 0 ? "▼ DESCENDRE " : "▲ REMONTER ";
                    _beaconDepthHint.text = depthDir + Mathf.RoundToInt(Mathf.Abs(depthDiff)) + " m" + dirHint;
                    _beaconDepthHint.color = depthDiff > 0 ? Cyan : Mint;
                }
                else
                {
                    _beaconDepthHint.text = "● MÊME PROFONDEUR" + dirHint;
                    _beaconDepthHint.color = Mint;
                }
            }

            // Arrow pointing toward the black box
            if (_beaconArrow != null)
            {
                Vector3 toBox = boxPos - subPos;
                // Project to submarine's local horizontal plane
                float x = Vector3.Dot(toBox, controller.transform.right);
                float z = Vector3.Dot(toBox, controller.transform.forward);
                float angle = Mathf.Atan2(x, z) * Mathf.Rad2Deg;
                _beaconArrow.localRotation = Quaternion.Euler(0f, 0f, -angle);

                // Pulse the arrow alpha based on distance (brighter when closer)
                float a = distance < 50f ? 1f : Mathf.Lerp(0.5f, 0.9f, pulse);
                var arrowImg = _beaconArrow.GetComponent<Image>();
                if (arrowImg != null)
                    arrowImg.color = new Color(Amber.r, Amber.g, Amber.b, a);
            }
        }

        // ------------------------------------------------------------------
        // Sonar
        // ------------------------------------------------------------------

        void UpdateSonar()
        {
            if (sonar == null) return;

            if (_sonarSweep != null)
                _sonarSweep.rectTransform.Rotate(0f, 0f, -140f * Time.deltaTime);

            float noise = sonar.GetDetectionPercent();
            if (_noiseFill != null)
            {
                _noiseFill.fillAmount = Mathf.Lerp(_noiseFill.fillAmount, noise, Time.deltaTime * 5f);
                _noiseFill.color = Color.Lerp(Mint, Red, Mathf.InverseLerp(0.35f, 0.85f, noise));
            }
            if (_noiseLabel != null)
            {
                bool tracking = sonar.IsCreatureTracking();
                _noiseLabel.text = tracking ? "REPÉRÉ" : "BRUIT";
                _noiseLabel.color = tracking ? Red : Ink;
            }

            UpdateBlips();
        }

        void UpdateBlips()
        {
            if (_sonarCenter == null || controller == null) return;

            int used = 0;
            var contacts = sonar.Contacts;
            for (int i = 0; i < contacts.Count && used < _blipPool.Count; i++)
            {
                var contact = contacts[i];
                Vector3 world = contact.position - controller.transform.position;
                if (world.sqrMagnitude < 0.01f) continue;

                float x = Vector3.Dot(world, controller.transform.right);
                float z = Vector3.Dot(world, controller.transform.forward);
                Vector2 dir = new Vector2(x, z).normalized;
                float radius = Mathf.Clamp01(contact.distance / 200f) * 92f;

                var rect = _blipRects[used];
                rect.anchoredPosition = dir * radius;
                rect.sizeDelta = contact.type == SonarTargetType.Creature ? new Vector2(12f, 12f) : new Vector2(7f, 7f);

                var img = _blipPool[used];
                img.color = BlipColor(contact.type);
                img.enabled = true;
                used++;
            }

            for (int i = used; i < _blipPool.Count; i++)
                _blipPool[i].enabled = false;
        }

        static Color BlipColor(SonarTargetType type)
        {
            switch (type)
            {
                case SonarTargetType.Creature: return new Color(1f, 0.25f, 0.25f, 0.95f);
                case SonarTargetType.Objective: return new Color(1f, 0.9f, 0.3f, 0.95f);
                case SonarTargetType.Mine: return new Color(1f, 0.55f, 0.1f, 0.95f);
                case SonarTargetType.Debris: return new Color(0.7f, 0.6f, 0.5f, 0.8f);
                default: return new Color(0.3f, 0.9f, 1f, 0.6f);
            }
        }

        void StartPingRing()
        {
            _pingAnim = 0f;
            if (_pingRing != null)
            {
                _pingRing.gameObject.SetActive(true);
                _pingRing.rectTransform.localScale = Vector3.one * 0.2f;
            }
        }

        void AnimatePing()
        {
            _pingAnim += Time.deltaTime * 0.9f;
            if (_pingAnim >= 1f)
            {
                _pingAnim = -1f;
                if (_pingRing != null) _pingRing.gameObject.SetActive(false);
                if (_sonarPanel != null) _sonarPanel.localScale = Vector3.one;
                return;
            }
            if (_pingRing == null) return;
            _pingRing.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.2f, 2.2f, _pingAnim);
            var c = _pingRing.color;
            c.a = Mathf.Lerp(0.8f, 0f, _pingAnim);
            _pingRing.color = c;
            if (_sonarPanel != null)
                _sonarPanel.localScale = Vector3.one * (1f + 0.05f * (1f - _pingAnim));
        }

        // ------------------------------------------------------------------
        // Warnings / badges
        // ------------------------------------------------------------------

        void UpdateWarnings()
        {
            bool danger = (sonar != null && sonar.IsCreatureTracking()) ||
                          (hull != null && hull.HullPercent < 0.3f);
            _alarmPulse = Mathf.Lerp(_alarmPulse, danger ? 1f : 0f, Time.deltaTime * 4f);

            if (_alarmVignette != null)
            {
                float a = (0.12f + Mathf.Sin(Time.time * 6f) * 0.06f) * _alarmPulse;
                _alarmVignette.color = new Color(1f, 0.15f, 0.15f, a);
                _alarmVignette.enabled = _alarmPulse > 0.02f;
            }

            if (_silentBadge != null && controller != null)
                _silentBadge.SetActive(controller.IsSilentRunning);
            if (_lightBadge != null && battery != null)
                _lightBadge.SetActive(battery.LightsOn);

            if (_statusText != null && _statusText.gameObject.activeSelf)
            {
                var c = _statusText.color;
                c.a = 0.7f + Mathf.Sin(Time.time * 5f) * 0.3f;
                _statusText.color = c;
            }
        }

        void FlashAlarm(Color color)
        {
            _alarmPulse = 1f;
        }

        // ------------------------------------------------------------------
        // Extraction
        // ------------------------------------------------------------------

        void UpdateExtraction()
        {
            bool extracting = gameManager != null && gameManager.IsExtracting;
            if (_extractGroup != null)
                _extractGroup.SetActive(extracting);
            if (!extracting) return;

            _extractShown = Mathf.Lerp(_extractShown, gameManager.ExtractionProgress, Time.deltaTime * 6f);
            if (_extractFill != null) _extractFill.fillAmount = _extractShown;
            if (_extractText != null)
                _extractText.text = "EXTRACTION  " + Mathf.RoundToInt(_extractShown * 100f) + "%";
        }

        // ------------------------------------------------------------------
        // End screen
        // ------------------------------------------------------------------

        void HandleState(GameManager.GameState state)
        {
            if (state == GameManager.GameState.Won)
            {
                if (gameManager != null)
                    ShowEnd(true, "MISSION ACCOMPLIE",
                        "Score  " + Mathf.RoundToInt(gameManager.Score).ToString("N0") +
                        "\nProfondeur  " + Mathf.RoundToInt(gameManager.MaxDepthReached) + " m" +
                        "\nTemps  " + Mathf.RoundToInt(gameManager.PlayTime) + " s");
            }
        }

        void ShowEnd(bool won, string title, string body)
        {
            if (_endOverlay == null) return;
            _endShownAt = Time.unscaledTime;
            _endOverlay.SetActive(true);
            if (_endTitle != null)
            {
                _endTitle.text = title;
                _endTitle.color = won ? Mint : Red;
            }
            if (_endBody != null)
                _endBody.text = body + "\n\nAppuyez pour recommencer";
            StartCoroutine(FadeEnd());
        }

        IEnumerator FadeEnd()
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / 0.5f;
                if (_endGroup != null) _endGroup.alpha = Mathf.Clamp01(t);
                yield return null;
            }
        }

        void UpdateEndInput()
        {
            if (gameManager == null) return;
            if (gameManager.State != GameManager.GameState.Lost &&
                gameManager.State != GameManager.GameState.Won) return;

            // Petite temporisation : évite qu'un appui fatal relance la partie.
            if (Time.unscaledTime < _endShownAt + 0.9f) return;

            bool pressed = false;
            if (Keyboard.current != null && (Keyboard.current.rKey.wasPressedThisFrame || Keyboard.current.spaceKey.wasPressedThisFrame))
                pressed = true;
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                pressed = true;
            if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
                pressed = true;

            if (pressed) gameManager.Restart();
        }

        // ------------------------------------------------------------------
        // Status toast
        // ------------------------------------------------------------------

        void ShowStatus(string message, Color color)
        {
            Announce(message, color, 3f);
        }

        // Message d'objectif / d'alerte affiché en haut de l'écran.
        public void Announce(string message, Color color, float seconds = 4f)
        {
            if (_statusText == null) return;
            _statusText.text = message;
            _statusText.color = color;
            if (_statusToast != null) _statusToast.SetActive(true);
            _statusText.gameObject.SetActive(true);
            CancelInvoke(nameof(HideStatus));
            Invoke(nameof(HideStatus), seconds);
        }

        void HideStatus()
        {
            if (_statusText != null) _statusText.gameObject.SetActive(false);
            if (_statusToast != null) _statusToast.SetActive(false);
        }

        // ------------------------------------------------------------------
        // Build
        // ------------------------------------------------------------------

        void Build()
        {
            var canvasObj = new GameObject("SubmarineHUD");
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 100;
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();

            var root = canvasObj.transform;

            BuildTopBar(root);
            BuildGauges(root);
            BuildBeacon(root);
            BuildSonar(root);
            BuildBadges(root);
            BuildExtraction(root);
            BuildAlarmVignette(root);
            BuildEndOverlay(root);
            BuildBriefing(root);
            BuildHelpButton(root);
        }

        void BuildTopBar(Transform root)
        {
            var panel = Panel(root, "TopBar", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -24f), new Vector2(620f, 108f));

            var depthLabel = Label(panel.transform, "PROFONDEUR", 13, Cyan, TextAnchor.UpperLeft);
            depthLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            depthLabel.rectTransform.anchorMax = new Vector2(0f, 1f);
            depthLabel.rectTransform.pivot = new Vector2(0f, 1f);
            depthLabel.rectTransform.anchoredPosition = new Vector2(26f, -16f);
            depthLabel.rectTransform.sizeDelta = new Vector2(120f, 20f);

            _depthText = Label(panel.transform, "0 m", 38, Ink, TextAnchor.MiddleLeft);
            _depthText.fontStyle = FontStyle.Bold;
            _depthText.rectTransform.anchorMin = new Vector2(0f, 0f);
            _depthText.rectTransform.anchorMax = new Vector2(0f, 0f);
            _depthText.rectTransform.pivot = new Vector2(0f, 0f);
            _depthText.rectTransform.anchoredPosition = new Vector2(26f, 16f);
            _depthText.rectTransform.sizeDelta = new Vector2(160f, 48f);

            _zoneText = Label(panel.transform, "SURFACE", 14, Amber, TextAnchor.MiddleCenter);
            _zoneText.rectTransform.anchoredPosition = new Vector2(0f, -18f);
            _zoneText.rectTransform.sizeDelta = new Vector2(220f, 24f);

            _speedText = Label(panel.transform, "0.0 kn", 26, Cyan, TextAnchor.MiddleRight);
            _speedText.rectTransform.anchorMin = new Vector2(1f, 0.5f);
            _speedText.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            _speedText.rectTransform.pivot = new Vector2(1f, 0.5f);
            _speedText.rectTransform.anchoredPosition = new Vector2(-26f, 0f);
            _speedText.rectTransform.sizeDelta = new Vector2(140f, 40f);

            _scoreText = Label(panel.transform, "SCORE  0", 18, Ink, TextAnchor.MiddleCenter);
            _scoreText.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            _scoreText.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            _scoreText.rectTransform.pivot = new Vector2(0.5f, 0f);
            _scoreText.rectTransform.anchoredPosition = new Vector2(0f, 8f);
            _scoreText.rectTransform.sizeDelta = new Vector2(220f, 22f);

            _statusToast = Panel(root, "StatusToast", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 96f), new Vector2(760f, 76f));
            _statusToast.SetActive(false);

            _statusText = Label(_statusToast.transform, "", 22, Ink, TextAnchor.MiddleCenter);
            _statusText.rectTransform.anchorMin = Vector2.zero;
            _statusText.rectTransform.anchorMax = Vector2.one;
            _statusText.rectTransform.offsetMin = new Vector2(18f, 8f);
            _statusText.rectTransform.offsetMax = new Vector2(-18f, -8f);
            _statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _statusText.verticalOverflow = VerticalWrapMode.Overflow;
            _statusText.gameObject.SetActive(false);
        }

        void BuildGauges(Transform root)
        {
            var panel = Panel(root, "Gauges", new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(230f, -110f), new Vector2(400f, 170f));

            CreateGauge(panel.transform, "COQUE", new Vector2(-120f, 0f), Mint, out _hullFill, out _hullValue);
            CreateGauge(panel.transform, "O2", Vector2.zero, Cyan, out _oxygenFill, out _oxygenValue);
            CreateGauge(panel.transform, "ÉNERGIE", new Vector2(120f, 0f), Amber, out _batteryFill, out _batteryValue);
        }

        void BuildBeacon(Transform root)
        {
            _beaconChip = Panel(root, "ObjectiveChip", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -118f), new Vector2(380f, 72f));

            _beaconText = Label(_beaconChip.transform, "OBJECTIF · BOÎTE NOIRE   390 m", 15, Amber, TextAnchor.MiddleCenter);
            _beaconText.fontStyle = FontStyle.Bold;
            _beaconText.rectTransform.anchorMin = new Vector2(0.1f, 0.5f);
            _beaconText.rectTransform.anchorMax = new Vector2(0.9f, 1f);
            _beaconText.rectTransform.offsetMin = Vector2.zero;
            _beaconText.rectTransform.offsetMax = Vector2.zero;

            // Depth hint text below distance
            _beaconDepthHint = Label(_beaconChip.transform, "▼ DESCENDRE 180 m · DEVANT", 13, Cyan, TextAnchor.MiddleCenter);
            _beaconDepthHint.rectTransform.anchorMin = new Vector2(0.1f, 0f);
            _beaconDepthHint.rectTransform.anchorMax = new Vector2(0.9f, 0.5f);
            _beaconDepthHint.rectTransform.offsetMin = Vector2.zero;
            _beaconDepthHint.rectTransform.offsetMax = Vector2.zero;

            // Directional arrow on the left of the chip
            var arrowObj = new GameObject("BeaconArrow");
            arrowObj.transform.SetParent(_beaconChip.transform, false);
            _beaconArrow = arrowObj.AddComponent<RectTransform>();
            _beaconArrow.anchorMin = _beaconArrow.anchorMax = new Vector2(0.06f, 0.5f);
            _beaconArrow.pivot = new Vector2(0.5f, 0.5f);
            _beaconArrow.sizeDelta = new Vector2(28f, 28f);
            var arrowImg = arrowObj.AddComponent<Image>();
            // Create a simple triangle arrow texture
            arrowImg.sprite = CreateArrowSprite();
            arrowImg.color = Amber;
            arrowImg.raycastTarget = false;
        }

        static Sprite CreateArrowSprite()
        {
            int s = 64;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false);
            var pixels = new Color32[s * s];
            // Draw a triangle pointing UP (will be rotated by code)
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float ny = (float)y / s;
                    float nx = (float)x / s - 0.5f;
                    float halfWidth = (1f - ny) * 0.45f;
                    bool inside = Mathf.Abs(nx) < halfWidth && ny > 0.15f;
                    // Add a stem
                    bool stem = Mathf.Abs(nx) < 0.08f && ny <= 0.15f && ny > 0.0f;
                    pixels[y * s + x] = (inside || stem) ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                }
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f));
        }

        void CreateGauge(Transform parent, string label, Vector2 pos, Color color, out Image fill, out Text value)
        {
            var gauge = new GameObject(label + "Gauge");
            gauge.transform.SetParent(parent, false);
            var rect = gauge.AddComponent<RectTransform>();
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(110f, 110f);

            var track = gauge.AddComponent<Image>();
            track.sprite = Ring;
            track.color = new Color(1f, 1f, 1f, 0.12f);
            track.raycastTarget = false;

            var fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(gauge.transform, false);
            var fRect = fillObj.AddComponent<RectTransform>();
            fRect.anchorMin = Vector2.zero;
            fRect.anchorMax = Vector2.one;
            fRect.offsetMin = new Vector2(6f, 6f);
            fRect.offsetMax = new Vector2(-6f, -6f);
            fill = fillObj.AddComponent<Image>();
            fill.sprite = Ring;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;
            fill.fillClockwise = true;
            fill.fillAmount = 1f;
            fill.color = color;
            fill.raycastTarget = false;

            value = Label(gauge.transform, "100%", 20, Ink, TextAnchor.MiddleCenter);
            value.fontStyle = FontStyle.Bold;
            value.rectTransform.sizeDelta = new Vector2(90f, 30f);

            var tag = Label(gauge.transform, label, 11, new Color(0.7f, 0.82f, 0.95f, 0.85f), TextAnchor.MiddleCenter);
            tag.rectTransform.anchoredPosition = new Vector2(0f, -66f);
            tag.rectTransform.sizeDelta = new Vector2(110f, 18f);
        }

        void BuildSonar(Transform root)
        {
            var panel = Panel(root, "Sonar", new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-170f, -190f), new Vector2(250f, 250f));
            _sonarPanel = panel.GetComponent<RectTransform>();

            var circle = new GameObject("Scope");
            circle.transform.SetParent(panel.transform, false);
            var cRect = circle.AddComponent<RectTransform>();
            cRect.sizeDelta = new Vector2(200f, 200f);
            cRect.anchoredPosition = Vector2.zero;
            var cImg = circle.AddComponent<Image>();
            cImg.sprite = Circle;
            cImg.color = new Color(0.02f, 0.12f, 0.18f, 0.85f);
            cImg.raycastTarget = false;

            // concentric rings
            for (int i = 1; i <= 2; i++)
            {
                var ringObj = new GameObject("ScopeRing" + i);
                ringObj.transform.SetParent(circle.transform, false);
                var rRect = ringObj.AddComponent<RectTransform>();
                rRect.sizeDelta = new Vector2(200f * (i / 2.5f), 200f * (i / 2.5f));
                var rImg = ringObj.AddComponent<Image>();
                rImg.sprite = Ring;
                rImg.color = new Color(0.3f, 0.9f, 1f, 0.12f);
                rImg.raycastTarget = false;
            }

            _sonarCenter = new GameObject("Center").AddComponent<RectTransform>();
            _sonarCenter.SetParent(circle.transform, false);
            _sonarCenter.sizeDelta = new Vector2(200f, 200f);

            _sonarSweep = new GameObject("Sweep").AddComponent<Image>();
            _sonarSweep.transform.SetParent(circle.transform, false);
            var sweepRect = _sonarSweep.rectTransform;
            sweepRect.anchorMin = sweepRect.anchorMax = new Vector2(0.5f, 0.5f);
            sweepRect.pivot = new Vector2(0.5f, 0.5f);
            sweepRect.sizeDelta = new Vector2(190f, 190f);
            _sonarSweep.sprite = Sweep;
            _sonarSweep.color = new Color(0.3f, 1f, 0.8f, 0.35f);
            _sonarSweep.raycastTarget = false;

            // ping ring
            _pingRing = new GameObject("PingRing").AddComponent<Image>();
            _pingRing.transform.SetParent(circle.transform, false);
            var pRect = _pingRing.rectTransform;
            pRect.anchorMin = pRect.anchorMax = new Vector2(0.5f, 0.5f);
            pRect.sizeDelta = new Vector2(100f, 100f);
            _pingRing.sprite = Ring;
            _pingRing.color = new Color(0.3f, 1f, 0.7f, 0.8f);
            _pingRing.raycastTarget = false;

            // blip pool
            for (int i = 0; i < 24; i++)
            {
                var blip = new GameObject("Blip" + i).AddComponent<Image>();
                blip.transform.SetParent(_sonarCenter, false);
                var br = blip.rectTransform;
                br.anchorMin = br.anchorMax = new Vector2(0.5f, 0.5f);
                br.pivot = new Vector2(0.5f, 0.5f);
                br.sizeDelta = new Vector2(7f, 7f);
                blip.sprite = Circle;
                blip.color = Cyan;
                blip.enabled = false;
                blip.raycastTarget = false;
                _blipPool.Add(blip);
                _blipRects.Add(br);
            }

            // noise meter at the bottom of the sonar
            var noiseBg = new GameObject("NoiseTrack");
            noiseBg.transform.SetParent(panel.transform, false);
            var nRect = noiseBg.AddComponent<RectTransform>();
            nRect.anchorMin = new Vector2(0f, 0f);
            nRect.anchorMax = new Vector2(1f, 0f);
            nRect.pivot = new Vector2(0.5f, 0f);
            nRect.anchoredPosition = new Vector2(0f, 2f);
            nRect.sizeDelta = new Vector2(-16f, 8f);
            _noiseTrack = noiseBg.AddComponent<Image>();
            _noiseTrack.color = new Color(1f, 1f, 1f, 0.12f);
            _noiseTrack.raycastTarget = false;

            var noiseFillObj = new GameObject("NoiseFill");
            noiseFillObj.transform.SetParent(noiseBg.transform, false);
            var nfRect = noiseFillObj.AddComponent<RectTransform>();
            nfRect.anchorMin = Vector2.zero;
            nfRect.anchorMax = Vector2.one;
            nfRect.offsetMin = new Vector2(1f, 1f);
            nfRect.offsetMax = new Vector2(-1f, -1f);
            _noiseFill = noiseFillObj.AddComponent<Image>();
            _noiseFill.color = Mint;
            _noiseFill.type = Image.Type.Filled;
            _noiseFill.fillMethod = Image.FillMethod.Horizontal;
            _noiseFill.fillAmount = 0f;
            _noiseFill.raycastTarget = false;

            _noiseLabel = Label(panel.transform, "BRUIT", 11, Ink, TextAnchor.MiddleCenter);
            _noiseLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
            _noiseLabel.rectTransform.anchorMax = new Vector2(1f, 0f);
            _noiseLabel.rectTransform.pivot = new Vector2(0.5f, 0f);
            _noiseLabel.rectTransform.anchoredPosition = new Vector2(0f, 12f);
            _noiseLabel.rectTransform.sizeDelta = new Vector2(0f, 16f);
        }

        void BuildBadges(Transform root)
        {
            _silentBadge = Badge(root, "SILENCIEUX", new Vector2(0.5f, 0f), new Vector2(-70f, 26f), Mint);
            _lightBadge = Badge(root, "PHARES", new Vector2(0.5f, 0f), new Vector2(70f, 26f), Amber);
        }

        GameObject Badge(Transform root, string text, Vector2 anchor, Vector2 pos, Color color)
        {
            var obj = Panel(root, text + "Badge", anchor, anchor, pos, new Vector2(120f, 32f));
            var label = Label(obj.transform, text, 13, color, TextAnchor.MiddleCenter);
            label.fontStyle = FontStyle.Bold;
            label.rectTransform.sizeDelta = new Vector2(120f, 32f);
            obj.SetActive(false);
            return obj;
        }

        void BuildExtraction(Transform root)
        {
            _extractGroup = new GameObject("Extraction");
            _extractGroup.transform.SetParent(root, false);
            var rect = _extractGroup.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -180f);
            rect.sizeDelta = new Vector2(420f, 90f);

            var back = _extractGroup.AddComponent<Image>();
            back.sprite = Rounded;
            back.type = Image.Type.Sliced;
            back.color = PanelColor;
            back.raycastTarget = false;

            var ringObj = new GameObject("Ring");
            ringObj.transform.SetParent(_extractGroup.transform, false);
            var rRect = ringObj.AddComponent<RectTransform>();
            rRect.anchorMin = rRect.anchorMax = new Vector2(0f, 0.5f);
            rRect.pivot = new Vector2(0f, 0.5f);
            rRect.anchoredPosition = new Vector2(18f, 0f);
            rRect.sizeDelta = new Vector2(58f, 58f);
            var ringImg = ringObj.AddComponent<Image>();
            ringImg.sprite = Ring;
            ringImg.color = new Color(1f, 0.9f, 0.3f, 0.25f);
            ringImg.raycastTarget = false;

            var fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(ringObj.transform, false);
            var fRect = fillObj.AddComponent<RectTransform>();
            fRect.anchorMin = Vector2.zero;
            fRect.anchorMax = Vector2.one;
            fRect.offsetMin = new Vector2(4f, 4f);
            fRect.offsetMax = new Vector2(-4f, -4f);
            _extractFill = fillObj.AddComponent<Image>();
            _extractFill.sprite = Ring;
            _extractFill.type = Image.Type.Filled;
            _extractFill.fillMethod = Image.FillMethod.Radial360;
            _extractFill.fillOrigin = (int)Image.Origin360.Top;
            _extractFill.color = new Color(1f, 0.9f, 0.3f);
            _extractFill.fillAmount = 0f;
            _extractFill.raycastTarget = false;

            _extractText = Label(_extractGroup.transform, "EXTRACTION 0%", 20, Ink, TextAnchor.MiddleLeft);
            _extractText.fontStyle = FontStyle.Bold;
            _extractText.rectTransform.anchorMin = new Vector2(0f, 0f);
            _extractText.rectTransform.anchorMax = new Vector2(1f, 1f);
            _extractText.rectTransform.offsetMin = new Vector2(92f, 0f);
            _extractText.rectTransform.offsetMax = new Vector2(-18f, 0f);

            _extractGroup.SetActive(false);
        }

        void BuildBriefing(Transform root)
        {
            _briefingOverlay = new GameObject("BriefingOverlay");
            _briefingOverlay.transform.SetParent(root, false);
            var rect = _briefingOverlay.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var dim = _briefingOverlay.AddComponent<Image>();
            dim.color = new Color(0.01f, 0.03f, 0.08f, 0.86f);
            dim.raycastTarget = true;
            _briefingGroup = _briefingOverlay.AddComponent<CanvasGroup>();
            _briefingGroup.alpha = 0f;

            var card = Panel(_briefingOverlay.transform, "BriefingCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(820f, 620f));

            var title = Label(card.transform, "LA DESCENTE SILENCIEUSE", 34, Cyan, TextAnchor.MiddleCenter);
            title.fontStyle = FontStyle.Bold;
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -24f);
            title.rectTransform.sizeDelta = new Vector2(-40f, 46f);

            var sub = Label(card.transform, "Récupération du boîtier noir — Fosse des Ombres", 16, Amber, TextAnchor.MiddleCenter);
            sub.rectTransform.anchorMin = new Vector2(0f, 1f);
            sub.rectTransform.anchorMax = new Vector2(1f, 1f);
            sub.rectTransform.pivot = new Vector2(0.5f, 1f);
            sub.rectTransform.anchoredPosition = new Vector2(0f, -72f);
            sub.rectTransform.sizeDelta = new Vector2(-40f, 24f);

            string[] lines = new string[]
            {
                "OBJECTIF : descendre à 390 m, approcher le boîtier noir (< 15 m) et le maintenir en extraction 30 s.",
                "Le Léviathan est aveugle : il chasse le BRUIT. REPÉRÉ = il vous traque.",
                "",
                "COMMANDES",
                "• Manche (BARRE) : diriger (gauche/droite = cap, haut/bas = inclinaison).",
                "• Rail (MOTEUR) : poussée avant / arrière.",
                "• SONAR : révèle 200 m pendant 5 s — mais vous trahit (+bruit, batterie).",
                "• SILENCE : bruit moteur à 15% (économie batterie / oxygène).",
                "• PHARES : éclaire devant vous (consomme la batterie).",
                "• RÉPARER : +25% coque (2 usages).",
                "",
                "CONSEILS",
                "• Détection : silence, moteur coupé, changez de cap. Il cherche 10 s puis se rendort.",
                "• Batterie et oxygène limités (~2–3 min). Allez droit au but.",
                "• Appuyez sur H ou AIDE pour rouvrir cet écran.",
            };

            var body = Label(card.transform, string.Join("\n", lines), 18, Ink, TextAnchor.UpperLeft);
            body.supportRichText = true;
            body.rectTransform.anchorMin = new Vector2(0f, 0f);
            body.rectTransform.anchorMax = new Vector2(1f, 1f);
            body.rectTransform.offsetMin = new Vector2(34f, 96f);
            body.rectTransform.offsetMax = new Vector2(-34f, -108f);
            body.horizontalOverflow = HorizontalWrapMode.Wrap;
            body.verticalOverflow = VerticalWrapMode.Overflow;

            var btnObj = new GameObject("StartButton");
            btnObj.transform.SetParent(card.transform, false);
            var bRect = btnObj.AddComponent<RectTransform>();
            bRect.anchorMin = new Vector2(0.5f, 0f);
            bRect.anchorMax = new Vector2(0.5f, 0f);
            bRect.pivot = new Vector2(0.5f, 0f);
            bRect.anchoredPosition = new Vector2(0f, 22f);
            bRect.sizeDelta = new Vector2(360f, 56f);
            var bImg = btnObj.AddComponent<Image>();
            bImg.sprite = Rounded;
            bImg.type = Image.Type.Sliced;
            bImg.color = new Color(Cyan.r, Cyan.g, Cyan.b, 0.22f);
            var bOutline = btnObj.AddComponent<Outline>();
            bOutline.effectColor = Cyan;
            bOutline.effectDistance = new Vector2(1.5f, -1.5f);
            var bBtn = btnObj.AddComponent<Button>();
            bBtn.targetGraphic = bImg;
            bBtn.onClick.AddListener(HideBriefing);

            var bLabel = Label(btnObj.transform, "COMMENCER LA PLONGÉE", 20, Cyan, TextAnchor.MiddleCenter);
            bLabel.fontStyle = FontStyle.Bold;
            bLabel.rectTransform.anchorMin = Vector2.zero;
            bLabel.rectTransform.anchorMax = Vector2.one;
            bLabel.rectTransform.offsetMin = Vector2.zero;
            bLabel.rectTransform.offsetMax = Vector2.zero;
        }

        void BuildHelpButton(Transform root)
        {
            var obj = new GameObject("HelpButton");
            obj.transform.SetParent(root, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 24f);
            rect.sizeDelta = new Vector2(110f, 34f);
            var img = obj.AddComponent<Image>();
            img.sprite = Rounded;
            img.type = Image.Type.Sliced;
            img.color = new Color(0.03f, 0.1f, 0.2f, 0.7f);
            img.raycastTarget = true;
            var outline = obj.AddComponent<Outline>();
            outline.effectColor = Edge;
            outline.effectDistance = new Vector2(1f, -1f);
            var btn = obj.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.onClick.AddListener(ShowBriefing);
            var label = Label(obj.transform, "AIDE  (H)", 14, Ink, TextAnchor.MiddleCenter);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = Vector2.zero;
            label.rectTransform.offsetMax = Vector2.zero;
        }

        void ShowBriefing()
        {
            if (_briefingOverlay == null) return;
            _briefingVisible = true;
            _briefingOverlay.SetActive(true);
            StartCoroutine(FadeBriefing(1f));
            Time.timeScale = 0f;
        }

        void HideBriefing()
        {
            if (_briefingOverlay == null) return;
            _briefingVisible = false;
            StartCoroutine(FadeBriefingOut());
            Time.timeScale = 1f;
        }

        IEnumerator FadeBriefing(float target)
        {
            float t = _briefingGroup != null ? _briefingGroup.alpha : 0f;
            while (t < target)
            {
                t += Time.unscaledDeltaTime / 0.25f;
                if (_briefingGroup != null) _briefingGroup.alpha = Mathf.Clamp01(t);
                yield return null;
            }
            if (_briefingGroup != null) _briefingGroup.alpha = target;
        }

        IEnumerator FadeBriefingOut()
        {
            float t = _briefingGroup != null ? _briefingGroup.alpha : 1f;
            while (t > 0f)
            {
                t -= Time.unscaledDeltaTime / 0.2f;
                if (_briefingGroup != null) _briefingGroup.alpha = Mathf.Clamp01(t);
                yield return null;
            }
            if (_briefingOverlay != null) _briefingOverlay.SetActive(false);
        }

        void BuildAlarmVignette(Transform root)
        {
            var obj = new GameObject("AlarmVignette");
            obj.transform.SetParent(root, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _alarmVignette = obj.AddComponent<Image>();
            _alarmVignette.sprite = Vignette;
            _alarmVignette.color = new Color(1f, 0.15f, 0.15f, 0f);
            _alarmVignette.raycastTarget = false;
            _alarmVignette.enabled = false;
        }

        void BuildEndOverlay(Transform root)
        {
            _endOverlay = new GameObject("EndOverlay");
            _endOverlay.transform.SetParent(root, false);
            var rect = _endOverlay.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var dim = _endOverlay.AddComponent<Image>();
            dim.color = new Color(0.01f, 0.03f, 0.08f, 0.78f);
            _endGroup = _endOverlay.AddComponent<CanvasGroup>();
            _endGroup.alpha = 0f;

            var card = Panel(_endOverlay.transform, "EndCard", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(720f, 380f));

            _endTitle = Label(card.transform, "MISSION ÉCHOUÉE", 42, Red, TextAnchor.MiddleCenter);
            _endTitle.fontStyle = FontStyle.Bold;
            _endTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            _endTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            _endTitle.rectTransform.pivot = new Vector2(0.5f, 1f);
            _endTitle.rectTransform.anchoredPosition = new Vector2(0f, -34f);
            _endTitle.rectTransform.sizeDelta = new Vector2(-48f, 60f);

            _endBody = Label(card.transform, "", 22, Ink, TextAnchor.UpperCenter);
            _endBody.rectTransform.anchorMin = new Vector2(0f, 0f);
            _endBody.rectTransform.anchorMax = new Vector2(1f, 1f);
            _endBody.rectTransform.offsetMin = new Vector2(40f, 30f);
            _endBody.rectTransform.offsetMax = new Vector2(-40f, -100f);
            _endBody.supportRichText = true;

            _endOverlay.SetActive(false);
        }

        // ------------------------------------------------------------------
        // Build helpers
        // ------------------------------------------------------------------

        GameObject Panel(Transform root, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(root, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            var img = obj.AddComponent<Image>();
            img.sprite = Rounded;
            img.type = Image.Type.Sliced;
            img.color = PanelColor;
            img.raycastTarget = false;

            var outline = obj.AddComponent<Outline>();
            outline.effectColor = Edge;
            outline.effectDistance = new Vector2(1f, -1f);
            return obj;
        }

        Text Label(Transform parent, string value, int size, Color color, TextAnchor anchor)
        {
            var obj = new GameObject("Text");
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(200f, 30f);
            var text = obj.AddComponent<Text>();
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.font = Font;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        // ------------------------------------------------------------------
        // Generated sprites
        // ------------------------------------------------------------------

        static Font Font
        {
            get
            {
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                return _font;
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
                    float a = Mathf.Clamp01(r - d + 1f);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
                tex.SetPixels(px); tex.Apply();
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
                    float a = 1f - Mathf.Clamp01(Mathf.Abs(d - radius) / thickness);
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
                tex.SetPixels(px); tex.Apply();
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
                tex.SetPixels(px); tex.Apply();
                var border = new Vector4(radius, radius, radius, radius);
                _rounded = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
                return _rounded;
            }
        }

        static Sprite Sweep
        {
            get
            {
                const int size = 128;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp;
                var px = new Color[size * size];
                Vector2 c = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    Vector2 d = new Vector2(x, y) - c;
                    float dist = d.magnitude / (size * 0.5f);
                    float ang = Mathf.Atan2(d.y, d.x);
                    float a = Mathf.Repeat(ang + Mathf.PI * 2.2f, Mathf.PI * 2f) / (Mathf.PI * 2f);
                    float alpha = Mathf.Clamp01(1f - a) * Mathf.Clamp01(1f - dist) * 0.9f;
                    px[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
                tex.SetPixels(px); tex.Apply();
                var sprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
                return sprite;
            }
        }

        static Sprite _sweep;

        static Sprite Vignette
        {
            get
            {
                if (_vignette != null) return _vignette;
                const int size = 128;
                var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
                tex.wrapMode = TextureWrapMode.Clamp;
                var px = new Color[size * size];
                Vector2 c = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
                float max = size * 0.5f;
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), c) / max;
                    float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 1f, d));
                    px[y * size + x] = new Color(1f, 1f, 1f, a);
                }
                tex.SetPixels(px); tex.Apply();
                _vignette = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
                return _vignette;
            }
        }
    }
}
