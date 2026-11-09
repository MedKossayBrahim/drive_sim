using KikiNgao.SimpleBikeControl;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace BikeGame
{
    /// <summary>
    /// Three-heart life counter plus the ramp-jump penalty. The hearts are drawn
    /// with IMGUI, so they work on Android landscape without adding another
    /// canvas. A failed ramp jump removes exactly one heart; a clean jump and
    /// landing removes none. When the last heart is lost a GAME OVER overlay is
    /// shown and Replay reloads the level, which restores the bike, hearts, coins
    /// and every collectible to their initial scene state.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    [DisallowMultipleComponent]
    public class HeartSystem : MonoBehaviour
    {
        [Header("Hearts")]
        [SerializeField] private int maxHearts = 3;
        [SerializeField] private Color fullHeartColor = new Color(0.95f, 0.16f, 0.22f, 1f);
        [SerializeField] private Color emptyHeartColor = new Color(0.16f, 0.06f, 0.08f, 0.78f);

        [Header("Ramp jump")]
        [Tooltip("The ramp transform. Set by the scene setup; found by name if left empty.")]
        [SerializeField] private Transform ramp;
        [SerializeField] private string rampObjectName = "Jump Ramp";
        [Tooltip("Minimum speed (km/h) for entering the ramp to count as a jump attempt.")]
        [SerializeField] private float minimumAttemptSpeedKmh = 8f;
        [Tooltip("How long both wheels must be off the ground before the bike counts as airborne.")]
        [SerializeField] private float airborneThreshold = 0.15f;
        [Tooltip("Landing tilt (bike up vs. world up) that counts as a bad landing.")]
        [SerializeField] private float badLandingTilt = 55f;
        [Tooltip("Fall speed at touchdown that counts as a bad landing (m/s).")]
        [SerializeField] private float badLandingFallSpeed = 8.5f;
        [Tooltip("Bike tilt after touchdown that counts as falling over.")]
        [SerializeField] private float fallenTilt = 68f;
        [Tooltip("How long after touchdown a fall-over still counts as this jump failing.")]
        [SerializeField] private float postLandingWatch = 1.1f;
        [Tooltip("Impact speed while jumping that counts as a crash (m/s).")]
        [SerializeField] private float crashImpactSpeed = 7.5f;
        [Tooltip("Delay before another ramp attempt can start, preventing repeated hits on one failure.")]
        [SerializeField] private float rearmDelay = 1.6f;

        [Header("Barrier collisions")]
        [Tooltip("Minimum seconds between heart losses from road-barrier hits. " +
                 "Prevents sustained contact from draining multiple hearts at once.")]
        [SerializeField] private float barrierDamageCooldown = 1.5f;

        public static HeartSystem Instance { get; private set; }

        private SimpleBike bike;
        private Rigidbody bikeBody;
        private BikeImpactFeedback impactFeedback;
        private LevelCompleteTrigger levelComplete;

        private Bounds rampBounds;
        private bool hasRampBounds;

        private bool attemptActive;
        private bool attemptArmed = true;
        private bool wasAirborne;
        private bool penaltyGiven;
        private float lastGroundTime;
        private float airborneStartTime = -1f;
        private float nextAttemptAllowedTime;
        private float postLandingWatchUntil;
        private Vector3 lastAirVelocity;

        private bool gameOver;
        private bool replaying;
        private Rect replayButtonRect;
        private float nextBarrierDamageTime;

        private Texture2D heartTexture;
        private GUIStyle titleStyle;
        private GUIStyle titleShadowStyle;
        private GUIStyle messageStyle;
        private GUIStyle buttonStyle;

        private float LayoutScale
        {
            get { return Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 1080f, 0.55f, 1.6f); }
        }

        public int Hearts => hearts;
        public int MaxHearts => maxHearts;
        public bool IsGameOver => gameOver;

        private int hearts;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            hearts = Mathf.Max(1, maxHearts);
            maxHearts = hearts;
            EnsureHeartTexture();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            if (impactFeedback != null)
                impactFeedback.Impacted -= OnImpact;

            if (heartTexture != null)
                Destroy(heartTexture);
        }

        private void Start()
        {
            ResolveBike();
            ResolveRamp();
        }

        private void ResolveBike()
        {
            if (bike == null)
                bike = FindAnyObjectByType<SimpleBike>();

            if (bike == null) return;

            if (bikeBody == null)
                bikeBody = bike.GetRigidbody();

            if (impactFeedback == null)
            {
                impactFeedback = bike.GetComponent<BikeImpactFeedback>();
                if (impactFeedback != null)
                    impactFeedback.Impacted += OnImpact;
            }
        }

        private void ResolveRamp()
        {
            if (ramp == null)
            {
                GameObject found = GameObject.Find(rampObjectName);
                if (found != null) ramp = found.transform;
            }

            if (ramp == null) return;

            Renderer[] renderers = ramp.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                Collider[] colliders = ramp.GetComponentsInChildren<Collider>(true);
                if (colliders.Length == 0) return;

                rampBounds = colliders[0].bounds;
                for (int i = 1; i < colliders.Length; i++)
                {
                    if (!colliders[i].isTrigger)
                        rampBounds.Encapsulate(colliders[i].bounds);
                }

                hasRampBounds = true;
                return;
            }

            rampBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                rampBounds.Encapsulate(renderers[i].bounds);

            hasRampBounds = true;
        }

        private void Update()
        {
            if (gameOver || replaying) return;

            if (levelComplete == null)
                levelComplete = FindAnyObjectByType<LevelCompleteTrigger>();

            if (levelComplete != null && levelComplete.IsComplete)
                return;

            ResolveBike();
            if (bike == null) return;

            if (!hasRampBounds)
                ResolveRamp();

            if (!hasRampBounds || bikeBody == null || bikeBody.isKinematic)
                return;

            UpdateJumpState();
        }

        private void UpdateJumpState()
        {
            Vector3 position = bike.transform.position;

            // A failed jump must be evaluated before the bike can leave the
            // ramp's trigger volume, so airborne tracking continues even after
            // the bike has flown past the ramp.
            if (position.y < rampBounds.min.y - 8f)
            {
                if (attemptActive)
                    FailAttempt("fell below the ramp");

                return;
            }

            bool grounded = IsGrounded();
            if (grounded)
                lastGroundTime = Time.time;

            if (!attemptActive)
            {
                if (!attemptArmed || Time.time < nextAttemptAllowedTime)
                {
                    if (!IsNearRamp(position, 2.5f))
                        attemptArmed = true;
                    return;
                }

                if (IsInsideRampArea(position) &&
                    bike.CurrentSpeedKmh >= Mathf.Max(1f, minimumAttemptSpeedKmh) &&
                    grounded)
                {
                    BeginAttempt();
                }

                return;
            }

            // Continue the attempt while the bike is on or near the ramp, in the
            // air, or within the short post-landing watch window.
            if (Time.time > postLandingWatchUntil && !wasAirborne && !IsNearRamp(position, 3f))
            {
                EndAttempt();
                return;
            }

            bool airborneNow = !grounded && Time.time - lastGroundTime >= airborneThreshold;

            if (airborneNow)
            {
                if (!wasAirborne)
                {
                    wasAirborne = true;
                    airborneStartTime = Time.time;
                }

                lastAirVelocity = bikeBody.linearVelocity;
            }
            else if (grounded && wasAirborne)
            {
                wasAirborne = false;
                EvaluateLanding();
                return;
            }

            if (wasAirborne)
            {
                float tilt = Vector3.Angle(bike.transform.up, Vector3.up);
                if (tilt >= fallenTilt)
                    FailAttempt("flipped in the air");
                return;
            }

            if (Time.time < postLandingWatchUntil)
            {
                float tilt = Vector3.Angle(bike.transform.up, Vector3.up);
                if (tilt >= fallenTilt)
                    FailAttempt("fell after landing");
                return;
            }

            // Slow roll-over of the ramp (no real air time): award nothing and
            // quietly finish the attempt once the bike has left the ramp area.
            if (!IsNearRamp(position, 3f) && Time.time - lastGroundTime > 0.4f)
                EndAttempt();
        }

        private void BeginAttempt()
        {
            attemptActive = true;
            attemptArmed = false;
            wasAirborne = false;
            penaltyGiven = false;
            lastGroundTime = Time.time;
            airborneStartTime = -1f;
            postLandingWatchUntil = Time.time + postLandingWatch;
            lastAirVelocity = bikeBody != null ? bikeBody.linearVelocity : Vector3.zero;
        }

        private void EvaluateLanding()
        {
            float tilt = Vector3.Angle(bike.transform.up, Vector3.up);
            float fallSpeed = Mathf.Max(0f, -lastAirVelocity.y);
            float airTime = airborneStartTime >= 0f ? Time.time - airborneStartTime : 0f;

            // Tiny hops are not a jump. They are treated as a safe roll-over.
            bool wasRealJump = airTime >= Mathf.Max(airborneThreshold, 0.12f);

            if (wasRealJump && (tilt >= badLandingTilt || fallSpeed >= badLandingFallSpeed))
            {
                FailAttempt(string.Format("bad landing (tilt {0:0} deg, fall {1:0.0} m/s)", tilt, fallSpeed));
                return;
            }

            postLandingWatchUntil = Time.time + Mathf.Max(0f, postLandingWatch);
        }

        private void OnImpact(float impactSpeed)
        {
            if (gameOver || replaying || penaltyGiven) return;
            if (!attemptActive) return;
            if (impactSpeed < crashImpactSpeed) return;

            FailAttempt(string.Format("crash at {0:0.0} m/s", impactSpeed));
        }

        private void FailAttempt(string reason)
        {
            if (!attemptActive || penaltyGiven)
                return;

            penaltyGiven = true;
            attemptActive = false;
            attemptArmed = false;
            wasAirborne = false;
            airborneStartTime = -1f;
            nextAttemptAllowedTime = Time.time + Mathf.Max(0.2f, rearmDelay);

            Debug.LogFormat("HeartSystem: ramp jump failed ({0}).", reason);
            LoseHeart();
        }

        private void EndAttempt()
        {
            attemptActive = false;
            attemptArmed = false;
            wasAirborne = false;
            airborneStartTime = -1f;
            nextAttemptAllowedTime = Time.time + Mathf.Max(0.2f, rearmDelay);
        }

        private void LoseHeart()
        {
            if (gameOver) return;

            hearts = Mathf.Max(0, hearts - 1);
            Debug.LogFormat("HeartSystem: hearts left {0}/{1}", hearts, maxHearts);

            if (hearts <= 0)
                TriggerGameOver();
        }

        /// <summary>
        /// Called by road-barrier obstacles when the bike crashes into them.
        /// Removes exactly one heart and enforces a cooldown so sustained
        /// contact or a multi-collider crash cannot drain more than one heart.
        /// Ordinary terrain contact and successful ramp jumps never call this.
        /// </summary>
        public void TakeDamage()
        {
            if (gameOver || replaying) return;
            if (Time.time < nextBarrierDamageTime) return;

            nextBarrierDamageTime = Time.time + Mathf.Max(0.1f, barrierDamageCooldown);

            Debug.Log("HeartSystem: barrier collision, losing a heart.");
            LoseHeart();
        }

        private void TriggerGameOver()
        {
            if (gameOver) return;

            gameOver = true;

            if (bike != null)
            {
                Rigidbody body = bike.GetRigidbody();
                if (body != null)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }

                bike.Freeze = true;
            }

            MobileBikeControls controls = FindAnyObjectByType<MobileBikeControls>();
            if (controls != null)
                controls.enabled = false;

            if (GameManager.Instance != null && GameManager.Instance.GetInputManager != null)
                GameManager.Instance.GetInputManager.ClearMobileInput();

            Time.timeScale = 0f;
            Debug.Log("HeartSystem: GAME OVER");
        }

        public void Replay()
        {
            if (replaying) return;
            replaying = true;

            Time.timeScale = 1f;
            // Depuis le hub Drive Sim : conserver les pièces et revenir au menu.
            if (DriveSim.Race.TransportSession.Current != null)
            {
                DriveSim.Race.TransportSession.FinishFromBike(false);
                return;
            }

            if (GameManager.Instance != null && GameManager.Instance.GetInputManager != null)
                GameManager.Instance.GetInputManager.ClearMobileInput();

            Scene active = SceneManager.GetActiveScene();
            if (active.buildIndex >= 0)
                SceneManager.LoadScene(active.buildIndex);
            else
                SceneManager.LoadScene(active.name);
        }

        private bool IsGrounded()
        {
            if (bike == null) return false;

            WheelCollider front = bike.frontWheelCollider;
            WheelCollider rear = bike.rearWheelCollider;

            if (front != null && front.GetGroundHit(out _)) return true;
            if (rear != null && rear.GetGroundHit(out _)) return true;

            return false;
        }

        private bool IsInsideRampArea(Vector3 position)
        {
            Bounds area = rampBounds;
            Vector3 size = area.size;
            area.Expand(new Vector3(1.2f, Mathf.Max(1.5f, size.y * 0.5f), 1.2f));
            return area.Contains(position);
        }

        private bool IsNearRamp(Vector3 position, float margin)
        {
            Bounds area = rampBounds;
            Vector3 size = area.size;
            area.Expand(new Vector3(margin * 2f, Mathf.Max(1.5f, size.y), margin * 2f));
            return area.Contains(position);
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;

            EnsureGuiStyles();

            float scale = LayoutScale;
            Rect safe = GetSafeGuiRect();

            if (!gameOver)
            {
                DrawHearts(safe, scale);
                return;
            }

            DrawGameOver(safe, scale);
        }

        private void DrawHearts(Rect safe, float scale)
        {
            float heartSize = 48f * scale;
            float gap = 8f * scale;
            float margin = 28f * scale;
            float width = maxHearts * heartSize + (maxHearts - 1) * gap;

            // The coin HUD sits in the top-right corner. Place the hearts just
            // below it so neither HUD can cover the other in landscape.
            float x = safe.xMax - margin - width;
            float y = safe.y + 28f * scale + 104f * scale + 12f * scale;

            Rect panel = new Rect(x - 10f * scale, y - 7f * scale,
                width + 20f * scale, heartSize + 14f * scale);

            Color previous = GUI.color;
            GUI.color = new Color(0.03f, 0.035f, 0.03f, 0.72f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = previous;

            for (int i = 0; i < maxHearts; i++)
            {
                Rect rect = new Rect(x + i * (heartSize + gap), y, heartSize, heartSize);
                GUI.color = i < hearts ? fullHeartColor : emptyHeartColor;
                GUI.DrawTexture(rect, heartTexture, ScaleMode.ScaleToFit, true);
            }

            GUI.color = previous;
        }

        private void DrawGameOver(Rect safe, float scale)
        {
            Color previous = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.78f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;

            float titleSize = Mathf.Clamp(safe.height * 0.14f, 36f, 156f);
            int titleFont = Mathf.RoundToInt(titleSize);
            titleStyle.fontSize = titleFont;
            titleShadowStyle.fontSize = titleFont;
            messageStyle.fontSize = Mathf.RoundToInt(Mathf.Clamp(titleSize * 0.28f, 16f, 44f));
            buttonStyle.fontSize = Mathf.RoundToInt(Mathf.Clamp(safe.height * 0.07f, 24f, 64f));

            float titleHeight = titleSize * 1.35f;
            float titleY = safe.y + safe.height * 0.20f;
            Rect titleRect = new Rect(safe.x, titleY, safe.width, titleHeight);
            Rect shadowRect = new Rect(titleRect.x + titleSize * 0.05f,
                titleRect.y + titleSize * 0.05f, titleRect.width, titleRect.height);

            GUI.Label(shadowRect, "GAME OVER", titleShadowStyle);
            GUI.Label(titleRect, "GAME OVER", titleStyle);

            Rect messageRect = new Rect(safe.x, titleY + titleHeight + titleSize * 0.05f,
                safe.width, titleSize * 0.55f);
            GUI.Label(messageRect, "You ran out of hearts", messageStyle);

            replayButtonRect = ComputeReplayButton(safe);

            Color previousBackground = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.30f, 0.65f, 0.28f, 1f);
            if (GUI.Button(replayButtonRect, "REPLAY", buttonStyle))
                Replay();
            GUI.backgroundColor = previousBackground;
        }

        private void UpdateReplayPolling()
        {
            // Replay is also polled directly through the Input System because
            // IMGUI mouse/touch events are not reliable on every Android device.
            if (!gameOver || replaying) return;

            if (Keyboard.current != null &&
                (Keyboard.current.enterKey.wasPressedThisFrame ||
                 Keyboard.current.spaceKey.wasPressedThisFrame ||
                 Keyboard.current.rKey.wasPressedThisFrame))
            {
                Replay();
                return;
            }

            Vector2? press = null;

            if (Touchscreen.current != null &&
                Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                press = Touchscreen.current.primaryTouch.position.ReadValue();
            }
            else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                press = Mouse.current.position.ReadValue();
            }

            if (!press.HasValue) return;

            Vector2 guiPoint = new Vector2(press.Value.x, Screen.height - press.Value.y);
            if (replayButtonRect.Contains(guiPoint))
                Replay();
        }

        private void LateUpdate()
        {
            UpdateReplayPolling();
        }

        private static Rect ComputeReplayButton(Rect safe)
        {
            float width = Mathf.Clamp(safe.width * 0.30f, 240f, 620f);
            float height = Mathf.Clamp(safe.height * 0.16f, 64f, 158f);
            float x = safe.x + (safe.width - width) * 0.5f;
            float y = safe.y + safe.height * 0.60f;
            return new Rect(x, y, width, height);
        }

        private static Rect GetSafeGuiRect()
        {
            Rect safe = Screen.safeArea;
            return new Rect(safe.x, Screen.height - safe.y - safe.height,
                safe.width, safe.height);
        }

        private void EnsureGuiStyles()
        {
            if (titleStyle != null) return;

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                wordWrap = false
            };
            titleStyle.normal.textColor = new Color(1f, 0.18f, 0.20f);

            titleShadowStyle = new GUIStyle(titleStyle);
            titleShadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.82f);

            messageStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                wordWrap = false
            };
            messageStyle.normal.textColor = new Color(1f, 1f, 1f, 0.92f);

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            buttonStyle.normal.textColor = Color.white;
            buttonStyle.hover.textColor = Color.white;
            buttonStyle.active.textColor = Color.white;
        }

        private void EnsureHeartTexture()
        {
            if (heartTexture != null) return;

            const int size = 96;
            heartTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            heartTexture.name = "BikeGame Heart";
            heartTexture.wrapMode = TextureWrapMode.Clamp;
            heartTexture.filterMode = FilterMode.Bilinear;
            heartTexture.hideFlags = HideFlags.DontSave;

            Color32[] pixels = new Color32[size * size];

            for (int py = 0; py < size; py++)
            {
                for (int px = 0; px < size; px++)
                {
                    // Heart implicit curve: (x^2 + y^2 - 1)^3 - x^2 y^3 <= 0.
                    float x = (((px + 0.5f) / size) * 2f - 1f) * 1.25f;
                    float y = ((1f - (py + 0.5f) / size) * 2f - 1f) * 1.25f + 0.18f;

                    float a = x * x + y * y - 1f;
                    float value = a * a * a - x * x * y * y * y;

                    // A little smooth edge so the small Android icon does not
                    // look jagged when the screen is scaled.
                    float alpha = value <= 0f ? 1f : Mathf.Clamp01(1f - value * 5f);
                    pixels[py * size + px] = new Color32(255, 255, 255,
                        (byte)Mathf.Clamp(Mathf.RoundToInt(alpha * 255f), 0, 255));
                }
            }

            heartTexture.SetPixels32(pixels);
            heartTexture.Apply();
        }
    }
}
