using UnityEngine;
using UnityEngine.InputSystem;

namespace KikiNgao.SimpleBikeControl
{
    /// <summary>
    /// Reads keyboard/gamepad through the new Input System and accepts axes from
    /// MobileBikeControls. Legacy UnityEngine.Input is deliberately not used
    /// because this project has activeInputHandler set to "Input System Package".
    /// </summary>
    public class InputManager : MonoBehaviour
    {
        public float horizontal, vertical;

        [Header("Keyboard Bindings")]
        public KeyCode enterExitKey = KeyCode.F;
        public KeyCode speedUpKey = KeyCode.LeftShift;

        [Header("Rider")]
        [Tooltip("Mount/exit requests are disabled in this game so the rider stays on the bike.")]
        public bool allowEnterExit;

        [HideInInspector]
        public bool enterExitVehicle;
        [HideInInspector]
        public bool speedUp;

        /// <summary>Final steering axis used by SimpleBike this frame.</summary>
        public float Horizontal => horizontal;
        /// <summary>Final throttle axis used by SimpleBike this frame.</summary>
        public float Vertical => vertical;
        public bool SpeedUpActive => speedUp;
        public float MobileHorizontal => mobileHorizontal;
        public float MobileVertical => mobileVertical;

        private float mobileHorizontal;
        private float mobileVertical;
        private bool mobileSpeedUp;
        private bool mobileEnterExitRequested;

        private const float MobileDeadZone = 0.08f;

        /// <summary>Called every frame by the on-screen mobile controls.</summary>
        public void SetMobileInput(float horizontalAxis, float verticalAxis, bool boost)
        {
            mobileHorizontal = Mathf.Clamp(horizontalAxis, -1f, 1f);
            mobileVertical = Mathf.Clamp(verticalAxis, -1f, 1f);
            mobileSpeedUp = boost;
        }

        /// <summary>Clears touch input when the controls are disabled or released.</summary>
        public void ClearMobileInput()
        {
            mobileHorizontal = 0f;
            mobileVertical = 0f;
            mobileSpeedUp = false;
        }

        /// <summary>Queues a mount/exit request from the touch UI.</summary>
        public void RequestEnterExitVehicle()
        {
            if (!allowEnterExit) return;
            mobileEnterExitRequested = true;
        }

        /// <summary>
        /// Returns the pending mount/exit request exactly once. Biker uses this
        /// so one press cannot be handled twice or missed by FixedUpdate.
        /// </summary>
        public bool ConsumeEnterExitVehicle()
        {
            if (!enterExitVehicle) return false;
            enterExitVehicle = false;
            return true;
        }

        private void Update()
        {
            float keyboardHorizontal = 0f;
            float keyboardVertical = 0f;
            bool keyboardSpeedUp = false;
            bool keyboardEnterExit = false;

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) keyboardHorizontal -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) keyboardHorizontal += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) keyboardVertical -= 1f;
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) keyboardVertical += 1f;

                keyboardSpeedUp = IsPressed(keyboard, speedUpKey);
                keyboardEnterExit = WasPressedThisFrame(keyboard, enterExitKey);
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                Vector2 stick = gamepad.leftStick.ReadValue();
                if (Mathf.Abs(stick.x) > Mathf.Abs(keyboardHorizontal)) keyboardHorizontal = stick.x;
                if (Mathf.Abs(stick.y) > Mathf.Abs(keyboardVertical)) keyboardVertical = stick.y;

                keyboardSpeedUp |= gamepad.rightTrigger.ReadValue() > 0.35f ||
                                   gamepad.rightShoulder.isPressed;
                keyboardEnterExit |= gamepad.buttonSouth.wasPressedThisFrame;
            }

            bool mobileSteering = Mathf.Abs(mobileHorizontal) > MobileDeadZone;
            bool mobileThrottle = Mathf.Abs(mobileVertical) > MobileDeadZone;

            horizontal = Mathf.Clamp(mobileSteering ? mobileHorizontal : keyboardHorizontal, -1f, 1f);
            vertical = Mathf.Clamp(mobileThrottle ? mobileVertical : keyboardVertical, -1f, 1f);

            speedUp = mobileSpeedUp || keyboardSpeedUp;

            enterExitVehicle = allowEnterExit && (mobileEnterExitRequested || keyboardEnterExit);
            mobileEnterExitRequested = false;
        }

        private static bool IsPressed(Keyboard keyboard, KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.F: return keyboard.fKey.isPressed;
                case KeyCode.E: return keyboard.eKey.isPressed;
                case KeyCode.R: return keyboard.rKey.isPressed;
                case KeyCode.Return: return keyboard.enterKey.isPressed;
                case KeyCode.Space: return keyboard.spaceKey.isPressed;
                case KeyCode.Escape: return keyboard.escapeKey.isPressed;
                case KeyCode.LeftShift: return keyboard.leftShiftKey.isPressed;
                case KeyCode.RightShift: return keyboard.rightShiftKey.isPressed;
                case KeyCode.LeftControl: return keyboard.leftCtrlKey.isPressed;
                case KeyCode.RightControl: return keyboard.rightCtrlKey.isPressed;
                case KeyCode.LeftAlt: return keyboard.leftAltKey.isPressed;
                case KeyCode.RightAlt: return keyboard.rightAltKey.isPressed;
                default: return false;
            }
        }

        private static bool WasPressedThisFrame(Keyboard keyboard, KeyCode keyCode)
        {
            switch (keyCode)
            {
                case KeyCode.F: return keyboard.fKey.wasPressedThisFrame;
                case KeyCode.E: return keyboard.eKey.wasPressedThisFrame;
                case KeyCode.R: return keyboard.rKey.wasPressedThisFrame;
                case KeyCode.Return: return keyboard.enterKey.wasPressedThisFrame;
                case KeyCode.Space: return keyboard.spaceKey.wasPressedThisFrame;
                case KeyCode.Escape: return keyboard.escapeKey.wasPressedThisFrame;
                default: return false;
            }
        }
    }
}
