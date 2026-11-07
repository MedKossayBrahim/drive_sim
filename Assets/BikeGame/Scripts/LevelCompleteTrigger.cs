using KikiNgao.SimpleBikeControl;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace BikeGame
{
    /// <summary>
    /// Placed on the level-complete ball. When the bike or rider enters the
    /// trigger sphere, gameplay stops, a full-screen "LEVEL COMPLETE!" panel is
    /// shown and a Replay button restarts the current level from the beginning.
    ///
    /// The Replay button is drawn with IMGUI (matching the original completion
    /// message) and also polled through the Input System, so it works on Android
    /// where IMGUI does not always receive touch events.
    /// </summary>
    public class LevelCompleteTrigger : MonoBehaviour
    {
        private bool triggered;
        private bool replaying;
        private Rect replayButtonRect;

        private GUIStyle titleStyle;
        private GUIStyle titleShadowStyle;
        private GUIStyle subtitleStyle;
        private GUIStyle buttonStyle;

        /// <summary>True once the finish ball has been touched.</summary>
        public bool IsComplete => triggered;

        private void OnTriggerEnter(Collider other)
        {
            if (triggered) return;
            if (!IsRelevantCollider(other)) return;

            Complete();
        }

        /// <summary>
        /// Stops the ride and shows the completion screen. Kept public so the
        /// editor verification tool can exercise the same path as a real hit.
        /// </summary>
        public void Complete()
        {
            if (triggered) return;
            triggered = true;

            SimpleBike bike = FindAnyObjectByType<SimpleBike>();
            if (bike != null)
            {
                Rigidbody body = bike.GetRigidbody();
                if (body != null)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }

                // Kinematic also stops the wheelie and stability assists.
                bike.Freeze = true;
            }

            MobileBikeControls controls = FindAnyObjectByType<MobileBikeControls>();
            if (controls != null)
                controls.enabled = false;

            if (GameManager.Instance != null && GameManager.Instance.GetInputManager != null)
                GameManager.Instance.GetInputManager.ClearMobileInput();

            // Freeze the whole ride (animations, physics, particles) until Replay.
            Time.timeScale = 0f;

            Debug.Log("LevelCompleteTrigger: LEVEL COMPLETE");
        }

        private static bool IsRelevantCollider(Collider other)
        {
            if (other == null) return false;

            // Terrain and world-bound boxes are not relevant.
            if (other is TerrainCollider) return false;
            if (other.GetComponentInParent<SimpleBike>() != null) return true;
            if (other.GetComponentInParent<Biker>() != null) return true;
            return false;
        }

        private void Update()
        {
            if (!triggered || replaying) return;

            // Recompute the button rect here as well, so the tap works even if
            // IMGUI never receives an event on this device.
            replayButtonRect = ComputeReplayButton(GetSafeGuiRect());

            if (Keyboard.current != null &&
                (Keyboard.current.enterKey.wasPressedThisFrame ||
                 Keyboard.current.spaceKey.wasPressedThisFrame))
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

            // Touch/mouse positions use a bottom-left origin; IMGUI is top-left.
            Vector2 guiPoint = new Vector2(press.Value.x, Screen.height - press.Value.y);
            if (replayButtonRect.Contains(guiPoint))
                Replay();
        }

        /// <summary>
        /// Restarts the level. Reloading the scene rebuilds the bike at its spawn
        /// point and restores every coin and the coin counter to the initial
        /// serialized state, so no manual reset bookkeeping is needed.
        /// </summary>
        public void Replay()
        {
            if (replaying) return;
            replaying = true;

            Time.timeScale = 1f;
            if (DriveSim.Race.TransportSession.Current != null)
            {
                DriveSim.Race.TransportSession.FinishFromBike(true);
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

        private void OnGUI()
        {
            if (!triggered || replaying) return;

            // Draw above the joystick/coin HUD.
            GUI.depth = -1000;
            EnsureStyles();

            float screenWidth = Screen.width;
            float screenHeight = Screen.height;

            // Dim the whole screen so the message is readable over the forest.
            Color previousColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.72f);
            GUI.DrawTexture(new Rect(0f, 0f, screenWidth, screenHeight), Texture2D.whiteTexture);
            GUI.color = previousColor;

            // Keep the content inside the device safe area (notches, rounded
            // corners).
            Rect safeGui = GetSafeGuiRect();

            float titleSize = Mathf.Clamp(safeGui.height * 0.13f, 32f, 150f);
            int titleFont = Mathf.RoundToInt(titleSize);

            titleStyle.fontSize = titleFont;
            titleShadowStyle.fontSize = titleFont;
            subtitleStyle.fontSize = Mathf.RoundToInt(Mathf.Clamp(titleSize * 0.32f, 16f, 48f));
            buttonStyle.fontSize = Mathf.RoundToInt(Mathf.Clamp(safeGui.height * 0.065f, 24f, 64f));

            float titleHeight = titleSize * 1.35f;
            float titleY = safeGui.y + safeGui.height * 0.18f;

            Rect titleRect = new Rect(safeGui.x, titleY, safeGui.width, titleHeight);
            Rect titleShadowRect = new Rect(
                titleRect.x + titleSize * 0.05f,
                titleRect.y + titleSize * 0.05f,
                titleRect.width,
                titleRect.height);

            GUI.Label(titleShadowRect, "LEVEL COMPLETE!", titleShadowStyle);
            GUI.Label(titleRect, "LEVEL COMPLETE!", titleStyle);

            Rect subtitleRect = new Rect(
                safeGui.x,
                titleY + titleHeight + titleSize * 0.05f,
                safeGui.width,
                titleSize * 0.6f);
            GUI.Label(subtitleRect, BuildSubtitle(), subtitleStyle);

            replayButtonRect = ComputeReplayButton(safeGui);

            Color previousBackground = GUI.backgroundColor;
            GUI.backgroundColor = new Color(0.12f, 0.62f, 0.26f, 1f);
            if (GUI.Button(replayButtonRect, "REPLAY", buttonStyle))
                Replay();
            GUI.backgroundColor = previousBackground;
        }

        /// <summary>
        /// Screen.safeArea is bottom-left based in pixels; IMGUI and the touch
        /// positions are top-left based, so convert it once here.
        /// </summary>
        private static Rect GetSafeGuiRect()
        {
            Rect safe = Screen.safeArea;
            return new Rect(
                safe.x,
                Screen.height - safe.y - safe.height,
                safe.width,
                safe.height);
        }

        /// <summary>
        /// Large, centred button sized from the safe area so it stays readable
        /// on every Android landscape resolution.
        /// </summary>
        private static Rect ComputeReplayButton(Rect safeGui)
        {
            float buttonWidth = Mathf.Clamp(safeGui.width * 0.32f, 240f, 640f);
            float buttonHeight = Mathf.Clamp(safeGui.height * 0.16f, 64f, 160f);
            float buttonX = safeGui.x + (safeGui.width - buttonWidth) * 0.5f;
            float buttonY = safeGui.y + safeGui.height * 0.60f;
            return new Rect(buttonX, buttonY, buttonWidth, buttonHeight);
        }

        private static string BuildSubtitle()
        {
            CoinCounter counter = CoinCounter.Instance;
            if (counter == null)
                return "Tap REPLAY to ride again";

            return string.Format("COINS  {0} / {1}   |   Tap REPLAY to ride again",
                counter.CollectedCoins, counter.TotalCoins);
        }

        private void EnsureStyles()
        {
            if (titleStyle != null) return;

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                wordWrap = false
            };
            titleStyle.normal.textColor = new Color(1f, 0.92f, 0.1f);

            titleShadowStyle = new GUIStyle(titleStyle);
            titleShadowStyle.normal.textColor = new Color(0f, 0f, 0f, 0.8f);

            subtitleStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                wordWrap = false
            };
            subtitleStyle.normal.textColor = new Color(1f, 1f, 1f, 0.9f);

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            buttonStyle.normal.textColor = Color.white;
            buttonStyle.hover.textColor = Color.white;
            buttonStyle.active.textColor = Color.white;
        }
    }
}
