using UnityEngine;

namespace BikeGame
{
    /// <summary>
    /// Thin, fail-safe wrapper around the Android vibrator.
    /// Every platform/API failure is swallowed so gameplay never breaks.
    /// </summary>
    public static class BikeVibration
    {
        /// <summary>Global switch, also exposed to the on-screen UI.</summary>
        public static bool Enabled = true;

        /// <summary>Number of Vibrate() calls, including rejected ones. Used by tests.</summary>
        public static int CallCount { get; private set; }

        /// <summary>Duration of the last accepted request, in milliseconds.</summary>
        public static int LastDurationMs { get; private set; }

        /// <summary>True when the last call actually reached the device.</summary>
        public static bool LastCallSucceeded { get; private set; }

        private static bool initialized;
        private static bool supported;

#if UNITY_ANDROID
        private static AndroidJavaObject vibrator;
        private static int sdkInt = 26;
#endif

        public static bool IsSupported
        {
            get
            {
                Initialize();
                return supported;
            }
        }

        /// <summary>
        /// Requests a short vibration. Returns true only if the device vibrated.
        /// </summary>
        /// <param name="milliseconds">Duration in milliseconds (1..1000).</param>
        /// <param name="amplitude">1..255, or -1 for the device default.</param>
        public static bool Vibrate(int milliseconds, int amplitude = -1)
        {
            if (milliseconds <= 0)
                return false;

            milliseconds = Mathf.Clamp(milliseconds, 1, 1000);

            CallCount++;
            LastDurationMs = milliseconds;
            LastCallSucceeded = false;

            if (!Enabled)
                return false;

            Initialize();
            if (!supported)
                return false;

#if UNITY_ANDROID
            LastCallSucceeded = VibrateAndroid(milliseconds, amplitude);
#else
            if (Application.isEditor)
                Debug.LogFormat("BikeVibration: simulated {0} ms vibration (no mobile device).", milliseconds);
#endif
            return LastCallSucceeded;
        }

        private static void Initialize()
        {
            if (initialized) return;
            initialized = true;

#if UNITY_ANDROID
            if (Application.isEditor)
            {
                // AndroidJavaObject calls only work inside the player. The editor
                // reports the request in the console instead.
                supported = false;
                return;
            }

            try
            {
                using (AndroidJavaClass version = new AndroidJavaClass("android.os.Build$VERSION"))
                    sdkInt = version.GetStatic<int>("SDK_INT");

                using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (AndroidJavaObject activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                }

                if (vibrator != null && sdkInt >= 11)
                    supported = vibrator.Call<bool>("hasVibrator");
                else
                    supported = vibrator != null;
            }
            catch (System.Exception exception)
            {
                supported = false;
                vibrator = null;
                Debug.LogWarning("BikeVibration: Android vibrator unavailable (" + exception.Message + ").");
            }
#else
            supported = false;
#endif
        }

#if UNITY_ANDROID
        private static bool VibrateAndroid(int milliseconds, int amplitude)
        {
            try
            {
                if (sdkInt >= 26)
                {
                    using (AndroidJavaClass effectClass = new AndroidJavaClass("android.os.VibrationEffect"))
                    using (AndroidJavaObject effect = effectClass.CallStatic<AndroidJavaObject>(
                               "createOneShot", (long)milliseconds, amplitude))
                    {
                        vibrator.Call("vibrate", effect);
                    }
                }
                else
                {
                    vibrator.Call("vibrate", (long)milliseconds);
                }

                return true;
            }
            catch (System.Exception exception)
            {
                // Most common cause: android.permission.VIBRATE missing.
                Debug.LogWarning("BikeVibration: vibration failed (" + exception.Message + ").");
                return VibrateFallback();
            }
        }

        private static bool VibrateFallback()
        {
            try
            {
                Handheld.Vibrate();
                return true;
            }
            catch (System.Exception)
            {
                return false;
            }
        }
#endif
    }
}
