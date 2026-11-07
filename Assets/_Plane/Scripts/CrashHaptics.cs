using System;
using UnityEngine;

namespace Project.Plane
{
    /// <summary>Alertes haptiques Android ; les autres plateformes conservent seulement le réglage et le diagnostic.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(PlaneSensors), typeof(PlaneHealth), typeof(PlaneInput))]
    public sealed class CrashHaptics : MonoBehaviour
    {
        public const string PreferenceKey = "SkyCourier.Haptics";
        [SerializeField] private PlaneSensors sensors;
        [SerializeField] private PlaneHealth health;
        [SerializeField] private FallCrashDetector fallDetector;
        [SerializeField] private PlaneInput input;
        [SerializeField, Min(.1f)] private float minimumPulseInterval = .22f;
        [SerializeField, Min(.1f)] private float maximumPulseInterval = 1.1f;
        [SerializeField, Range(10, 200)] private int minimumPulseMilliseconds = 35;
        [SerializeField, Range(20, 300)] private int maximumPulseMilliseconds = 90;
        [SerializeField, Range(100, 1000)] private int crashPulseMilliseconds = 400;
        private bool terrain, falling, focused = true, applicationPaused;
        private float nextPulseAt, finalPulseUntil;
        private bool outputActive;
        public bool HapticsEnabled { get; private set; } = true;
        public float DangerLevel { get; private set; }
#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaObject vibrator;
        private AndroidJavaClass vibrationEffect;
        private int sdk;
        private bool triedAndroid;
#endif
        private void Awake()
        {
            if (sensors == null) sensors = GetComponent<PlaneSensors>();
            if (health == null) health = GetComponent<PlaneHealth>();
            if (fallDetector == null) fallDetector = GetComponent<FallCrashDetector>();
            if (input == null) input = GetComponent<PlaneInput>();
            // Réactive une fois la vibration après l'intégration (ancienne préférence OFF).
            const string migrated = PreferenceKey + ".ResetOn";
            if (!PlayerPrefs.HasKey(migrated))
            {
                PlayerPrefs.SetInt(PreferenceKey, 1);
                PlayerPrefs.SetInt(migrated, 1);
                PlayerPrefs.Save();
            }
            HapticsEnabled = PlayerPrefs.GetInt(PreferenceKey, 1) != 0;
        }
        private void OnEnable()
        {
            if (sensors != null) { sensors.OnTerrainWarning += TerrainChanged; sensors.OnStall += StallStarted; terrain = sensors.TerrainWarning; }
            if (fallDetector != null) { fallDetector.OnFalling += FallingChanged; falling = fallDetector.IsFalling; }
            if (health != null) { health.OnCrashed += Crashed; health.OnRespawned += Respawned; }
        }
        private void OnDisable()
        {
            if (sensors != null) { sensors.OnTerrainWarning -= TerrainChanged; sensors.OnStall -= StallStarted; }
            if (fallDetector != null) fallDetector.OnFalling -= FallingChanged;
            if (health != null) { health.OnCrashed -= Crashed; health.OnRespawned -= Respawned; }
            StopOutput();
        }
        public void SetHapticsEnabled(bool value)
        {
            HapticsEnabled = value;
            PlayerPrefs.SetInt(PreferenceKey, value ? 1 : 0);
            PlayerPrefs.Save();
            if (!value) StopOutput();
        }
        private void TerrainChanged(bool value) { terrain = value; if (value) nextPulseAt = Mathf.Min(nextPulseAt, Time.unscaledTime); }
        private void FallingChanged(bool value) { falling = value; if (value) nextPulseAt = Mathf.Min(nextPulseAt, Time.unscaledTime); }
        private void StallStarted() { nextPulseAt = Mathf.Min(nextPulseAt, Time.unscaledTime); }
        private void Respawned() { falling = terrain = false; finalPulseUntil = 0f; StopOutput(); }
        private bool CanOutput => HapticsEnabled && focused && !applicationPaused && input != null &&
            input.IsMissionStarted && !input.IsPaused && Time.timeScale > 0f;
        private void Update()
        {
            if (!CanOutput || sensors == null || health == null) { StopOutput(); return; }
            if (health.IsCrashed)
            {
                DangerLevel = 0f;
                if (Time.unscaledTime >= finalPulseUntil) StopOutput();
                return;
            }
            bool airborne = sensors.HasGroundAltitude ? sensors.AltitudeAGL > 4f : sensors.RawSpeedMetersPerSecond > 5f;
            DangerLevel = falling ? .95f : 0f;
            if (terrain) DangerLevel = Mathf.Max(DangerLevel, Mathf.Lerp(.3f, 1f, 1f - Mathf.Clamp01(sensors.TimeToImpact / 5f)));
            if (airborne && sensors.IsStalling) DangerLevel = Mathf.Max(DangerLevel, .35f);
            if (DangerLevel <= 0f) { StopOutput(); return; }
            if (Time.unscaledTime < nextPulseAt) return;
            int duration = Mathf.RoundToInt(Mathf.Lerp(minimumPulseMilliseconds, maximumPulseMilliseconds, DangerLevel));
            Pulse(duration, Mathf.RoundToInt(Mathf.Lerp(70, 220, DangerLevel)));
            nextPulseAt = Time.unscaledTime + Mathf.Max(.1f, Mathf.Lerp(maximumPulseInterval, minimumPulseInterval, DangerLevel));
        }
        private void Crashed()
        {
            if (!CanOutput) return;
            finalPulseUntil = Time.unscaledTime + crashPulseMilliseconds / 1000f;
            Pulse(crashPulseMilliseconds, 255);
        }
        private void Pulse(int milliseconds, int amplitude)
        {
#if UNITY_EDITOR
            // Pas de moteur haptique dans l'éditeur : on enregistre seulement l'état actif.
            outputActive = true;
            return;
#elif UNITY_ANDROID
            try
            {
                PrepareAndroid();
                if (vibrator != null)
                {
                    if (sdk >= 26 && vibrationEffect != null)
                    {
                        using (var effect = vibrationEffect.CallStatic<AndroidJavaObject>("createOneShot", (long)milliseconds, amplitude))
                            vibrator.Call("vibrate", effect);
                    }
                    else vibrator.Call("vibrate", (long)milliseconds);
                }
                else Handheld.Vibrate();
                outputActive = true;
            }
            catch (Exception error)
            {
                if (!warned) { warned = true; Debug.LogWarning("[Sky Courier] Vibration Android indisponible : " + error.Message, this); }
                Handheld.Vibrate(); outputActive = true;
            }
#else
            // iOS et autres plateformes : vibration système courte.
            Handheld.Vibrate();
            outputActive = true;
#endif
        }
        private void StopOutput()
        {
            DangerLevel = 0f;
            if (!outputActive) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            try { vibrator?.Call("cancel"); } catch (Exception) { /* Téléphone ou activité déjà arrêtés. */ }
#endif
            outputActive = false;
        }
#if UNITY_ANDROID && !UNITY_EDITOR
        private bool warned;
        private void PrepareAndroid()
        {
            if (triedAndroid) return;
            triedAndroid = true;
            using (var version = new AndroidJavaClass("android.os.Build$VERSION")) sdk = version.GetStatic<int>("SDK_INT");
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var context = activity.Call<AndroidJavaObject>("getApplicationContext"))
                vibrator = context.Call<AndroidJavaObject>("getSystemService", "vibrator");
            if (vibrator != null && !vibrator.Call<bool>("hasVibrator")) { vibrator.Dispose(); vibrator = null; }
            if (sdk >= 26) vibrationEffect = new AndroidJavaClass("android.os.VibrationEffect");
        }
#endif
        private void OnApplicationFocus(bool value) { focused = value; if (!value) StopOutput(); }
        private void OnApplicationPause(bool value) { applicationPaused = value; if (value) StopOutput(); }
        private void OnDestroy()
        {
            StopOutput();
#if UNITY_ANDROID && !UNITY_EDITOR
            vibrator?.Dispose(); vibrationEffect?.Dispose();
#endif
        }
    }
}
