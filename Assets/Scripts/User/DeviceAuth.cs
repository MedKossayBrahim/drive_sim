using UnityEngine;
using UnityEngine.Scripting;

namespace DriveSim.User
{
    // Pont vers Face ID (iPhone), la biométrie Android, ou une simulation dans l'éditeur.
    public static class DeviceAuth
    {
        const string TokenKey = "drive_sim_refresh_token";

        public static bool IsBiometricAvailable()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return Biometric_IsAvailable();
#elif UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var plugin = new AndroidJavaClass("com.drivesim.auth.BiometricPlugin"))
                using (var activity = CurrentActivity())
                {
                    return plugin.CallStatic<bool>("isAvailable", activity);
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("Biométrie Android : " + exception.Message);
                return false;
            }
#else
            return true;
#endif
        }

        public static bool HasSavedToken()
        {
#if UNITY_IOS && !UNITY_EDITOR
            return Biometric_HasToken();
#elif UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var plugin = new AndroidJavaClass("com.drivesim.auth.BiometricPlugin"))
                using (var activity = CurrentActivity())
                {
                    return plugin.CallStatic<bool>("hasToken", activity);
                }
            }
            catch (System.Exception)
            {
                return false;
            }
#else
            return PlayerPrefs.HasKey(TokenKey) && !string.IsNullOrEmpty(PlayerPrefs.GetString(TokenKey, ""));
#endif
        }

        public static void SaveToken(string token)
        {
#if UNITY_IOS && !UNITY_EDITOR
            Biometric_SaveToken(token);
#elif UNITY_ANDROID && !UNITY_EDITOR
            using (var plugin = new AndroidJavaClass("com.drivesim.auth.BiometricPlugin"))
            using (var activity = CurrentActivity())
            {
                plugin.CallStatic("saveToken", activity, token, Callback);
            }
#else
            PlayerPrefs.SetString(TokenKey, token);
            PlayerPrefs.Save();
            AuthFlow.EnqueueNative("save:ok");
#endif
        }

        public static void LoadToken()
        {
#if UNITY_IOS && !UNITY_EDITOR
            Biometric_LoadToken("Deverrouillez Drive Sim pour jouer");
#elif UNITY_ANDROID && !UNITY_EDITOR
            using (var plugin = new AndroidJavaClass("com.drivesim.auth.BiometricPlugin"))
            using (var activity = CurrentActivity())
            {
                plugin.CallStatic("loadToken", activity, Callback);
            }
#else
            AuthFlow.EnqueueNative("load:ok:" + PlayerPrefs.GetString(TokenKey, ""));
#endif
        }

        public static void ClearToken()
        {
#if UNITY_IOS && !UNITY_EDITOR
            Biometric_ClearToken();
#elif UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var plugin = new AndroidJavaClass("com.drivesim.auth.BiometricPlugin"))
                using (var activity = CurrentActivity())
                {
                    plugin.CallStatic("clearToken", activity);
                }
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning(exception.Message);
            }
#else
            PlayerPrefs.DeleteKey(TokenKey);
            PlayerPrefs.Save();
#endif
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidAuthCallback _callback;
        static AndroidAuthCallback Callback
        {
            get
            {
                if (_callback == null) _callback = new AndroidAuthCallback();
                return _callback;
            }
        }

        static AndroidJavaObject CurrentActivity()
        {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            {
                return player.GetStatic<AndroidJavaObject>("currentActivity");
            }
        }
#endif

#if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern bool Biometric_IsAvailable();

        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern bool Biometric_HasToken();

        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern void Biometric_SaveToken(string token);

        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern void Biometric_LoadToken(string reason);

        [System.Runtime.InteropServices.DllImport("__Internal")]
        static extern void Biometric_ClearToken();
#endif
    }

#if UNITY_ANDROID && !UNITY_EDITOR
    [Preserve]
    public sealed class AndroidAuthCallback : AndroidJavaProxy
    {
        public AndroidAuthCallback() : base("com.drivesim.auth.ResultCallback")
        {
        }

        [Preserve]
        public void onResult(string message)
        {
            AuthFlow.EnqueueNative(message);
        }
    }
#endif
}
