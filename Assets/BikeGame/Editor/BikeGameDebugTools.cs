using UnityEditor;
using UnityEngine;

namespace BikeGame.EditorTools
{
    /// <summary>
    /// Play-mode helpers. Useful on a desktop where no physical collision is
    /// convenient, and to check the steering sign without touching the phone.
    /// </summary>
    public static class BikeGameDebugTools
    {
        [MenuItem("BikeGame/Debug/Simulate Bike Impact (Play Mode)")]
        public static void SimulateBikeImpact()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("BikeGame: enter Play Mode first.");
                return;
            }

            BikeImpactFeedback feedback = Object.FindAnyObjectByType<BikeImpactFeedback>();

            if (feedback == null)
            {
                Debug.LogError("BikeGame: no BikeImpactFeedback in the scene. " +
                               "The bike is spawned by BikeGameBootstrap at runtime.");
                return;
            }

            feedback.SimulateImpact(9f);
            Debug.LogFormat("BikeGame: simulated impact. vibrations requested: {0}, last duration: {1} ms, supported: {2}.",
                BikeVibration.CallCount, BikeVibration.LastDurationMs, BikeVibration.IsSupported);
        }

        [MenuItem("BikeGame/Debug/Recalibrate Tilt Neutral (Play Mode)")]
        public static void RecalibrateTilt()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("BikeGame: enter Play Mode first.");
                return;
            }

            TiltSteering tilt = Object.FindAnyObjectByType<TiltSteering>();

            if (tilt == null)
            {
                Debug.LogError("BikeGame: no TiltSteering in the scene.");
                return;
            }

            tilt.BeginCalibration();
            Debug.Log("BikeGame: tilt neutral calibration started. Hold the phone in the neutral position.");
        }

        [MenuItem("BikeGame/Debug/Toggle Tilt Steering (Play Mode)")]
        public static void ToggleTiltSteering()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning("BikeGame: enter Play Mode first.");
                return;
            }

            TiltSteering tilt = Object.FindAnyObjectByType<TiltSteering>();

            if (tilt == null)
            {
                Debug.LogError("BikeGame: no TiltSteering in the scene.");
                return;
            }

            tilt.ToggleMode();
            Debug.LogFormat("BikeGame: steering mode is now {0} (sensor available: {1}).",
                tilt.Mode, tilt.IsAvailable);
        }
    }
}
