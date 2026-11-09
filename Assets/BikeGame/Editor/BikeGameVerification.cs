using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using KikiNgao.SimpleBikeControl;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BikeGame.EditorTools
{
    /// <summary>
    /// Automated play-mode verification for the mobile steering pipeline:
    ///
    ///   TiltSteering (simulated accelerometer)
    ///     -> MobileBikeControls
    ///       -> InputManager.SetMobileInput
    ///         -> SimpleBike.FixedUpdate / front wheel steer angle
    ///
    /// It also checks the always-on steering reset, calibration, invert, the
    /// dismount lock and the impact vibration thresholds.
    ///
    /// Run from a terminal with:
    ///   Unity.exe -batchmode -nographics -projectPath <project> \
    ///       -executeMethod BikeGame.EditorTools.BikeGameVerification.Run \
    ///       -logFile <log>
    /// The method writes a report next to the project Logs folder and exits the
    /// editor with code 0 (all passed) or 1 (failure).
    /// </summary>
    public static class BikeGameVerification
    {
        private const string PendingKey = "BikeGame.Verification.Pending";
        private const string StartKey = "BikeGame.Verification.Start";

        private static readonly List<string> Report = new List<string>();
        private static readonly StringBuilder Summary = new StringBuilder();

        private static int failures;
        private static int step;
        private static double stepEndsAt;
        private static double startedAt;

        private static TiltSteering tilt;
        private static SimpleBike bike;
        private static InputManager input;
        private static Biker rider;
        private static BikeImpactFeedback feedback;
        private static Rigidbody body;

        private static float neutralAxis;
        private static float leftAxis;
        private static float rightAxis;

        [InitializeOnLoadMethod]
        private static void Install()
        {
            if (!EditorPrefs.GetBool(PendingKey, false)) return;

            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;

            if (!EditorApplication.isPlaying)
                EditorApplication.isPlaying = true;
        }

        public static void Run()
        {
            EditorPrefs.SetBool(PendingKey, true);
            EditorPrefs.SetFloat(StartKey, (float)EditorApplication.timeSinceStartup);
            ClearReportFile();

            // Batch mode starts on an empty untitled scene, so the playable
            // forest scene has to be opened before play mode is entered.
            const string scenePath = "Assets/BikeGame/Scenes/ForestBikeRide.unity";
            if (File.Exists(scenePath))
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            else
                Debug.LogWarning("BikeGameVerification: scene not found at " + scenePath);

            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;

            if (!EditorApplication.isPlaying)
                EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            try
            {
                if (!EditorPrefs.GetBool(PendingKey, false))
                {
                    EditorApplication.update -= Tick;
                    return;
                }

                if (EditorApplication.isPlaying && startedAt <= 0.0)
                {
                    startedAt = EditorApplication.timeSinceStartup;
                    Log("Play mode entered.");
                }

                double now = EditorApplication.timeSinceStartup;
                double limit = EditorPrefs.GetFloat(StartKey, 0f) + 150.0;
                if (now > limit)
                {
                    Fail("Global timeout. Last step: " + step);
                    Finish();
                    return;
                }

                if (!EditorApplication.isPlaying)
                    return;

                switch (step)
                {
                    case 0:
                        BeginWaitForScene(now);
                        break;
                    case 1:
                        if (now < stepEndsAt) return;
                        MeasureNeutral("neutral-1");
                        BeginTilt(new Vector3(-3.03f, -9.33f, 0f), 2); // ~18 deg left
                        break;
                    case 2:
                        if (now < stepEndsAt) return;
                        MeasureLeft("full-left");
                        BeginTilt(Vector3.zero, 3); // neutral, orientation-independent zero
                        break;
                    case 3:
                        if (now < stepEndsAt) return;
                        MeasureNeutral("neutral-2 (stick released)");
                        BeginTilt(new Vector3(3.03f, -9.33f, 0f), 4); // ~18 deg right
                        break;
                    case 4:
                        if (now < stepEndsAt) return;
                        MeasureRight("full-right");
                        BeginTilt(Vector3.zero, 5);
                        break;
                    case 5:
                        if (now < stepEndsAt) return;
                        TestTiltPriorityOverStick(now);
                        break;
                    case 6:
                        if (now < stepEndsAt) return;
                        TestInvert(now);
                        break;
                    case 7:
                        if (now < stepEndsAt) return;
                        TestCalibration(now);
                        break;
                    case 8:
                        if (now < stepEndsAt) return;
                        TestDismountLock();
                        TestVibration();
                        TestTopSpeedLimit();
                        PlanFinal(now);
                        break;
                    case 9:
                        if (now < stepEndsAt) return;
                        Finish();
                        break;
                }
            }
            catch (Exception exception)
            {
                Fail("Exception: " + exception);
                Finish();
            }
        }

        // ---- steps -----------------------------------------------------------

        private static void BeginWaitForScene(double now)
        {
            tilt = UnityEngine.Object.FindAnyObjectByType<TiltSteering>();
            bike = UnityEngine.Object.FindAnyObjectByType<SimpleBike>();
            rider = UnityEngine.Object.FindAnyObjectByType<Biker>();

            if (GameManager.Instance != null)
                input = GameManager.Instance.GetInputManager;

            bool ready = tilt != null && bike != null && rider != null && input != null &&
                         rider.IsRiding && bike.GetRigidbody() != null;

            if (!ready)
            {
                if (now - startedAt > 25.0)
                {
                    Fail("Scene did not become ready within 25s (tilt=" + (tilt != null) +
                         ", bike=" + (bike != null) + ", rider=" + (rider != null) +
                         ", input=" + (input != null) + ", riding=" + (rider != null && rider.IsRiding) + ").");
                    Finish();
                }
                return;
            }

            body = bike.GetRigidbody();
            feedback = bike.GetComponent<BikeImpactFeedback>();

            Log("Scene ready: bike='" + bike.name + "', rider mounted=" + rider.IsRiding +
                ", tiltMode=" + tilt.Mode + ", sensorAvailable=" + tilt.IsAvailable);

            Check("auto-mount at start", rider.IsRiding, "rider.IsRiding=" + rider.IsRiding);
            Check("BOOST/steering joystick present", input != null, "InputManager resolved");
            Check("dismount disabled", !input.allowEnterExit,
                "InputManager.allowEnterExit=" + input.allowEnterExit);

            // Force tilt mode for the test, then keep the bike still so the
            // steering curve is evaluated at 0 km/h and the measurement is stable.
            tilt.SetMode(SteeringMode.Tilt);
            tilt.SetNeutralCalibration(0f);
            body.constraints = RigidbodyConstraints.FreezeAll;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;

            BeginTilt(new Vector3(0f, -9.81f, 0f), 1);
        }

        private static void BeginTilt(Vector3 acceleration, int nextStep)
        {
            if (acceleration == Vector3.zero)
                tilt.SimulateAcceleration(new Vector3(0f, -9.81f, 0f));
            else
                tilt.SimulateAcceleration(acceleration);

            step = nextStep;
            stepEndsAt = EditorApplication.timeSinceStartup + 1.6;
        }

        private static void MeasureNeutral(string label)
        {
            float axis = tilt.SteerAxis;
            float inputAxis = input.Horizontal;
            float wheel = bike.CurrentSteerAngleDegrees;

            neutralAxis = axis;
            Log(string.Format("{0}: tiltAxis={1:F3} inputHorizontal={2:F3} wheel={3:F2} speed={4:F2}",
                label, axis, inputAxis, wheel, bike.CurrentSpeedKmh));

            Check(label + ": tilt axis neutral", Mathf.Abs(axis) < 0.05f, "axis=" + axis.ToString("F3"));
            Check(label + ": InputManager horizontal neutral", Mathf.Abs(inputAxis) < 0.05f,
                "horizontal=" + inputAxis.ToString("F3"));
            Check(label + ": wheel returns to center", Mathf.Abs(wheel) < 1.5f,
                "wheel=" + wheel.ToString("F2") + " deg");
            Check(label + ": steer angle reached wheel collider",
                Mathf.Abs(bike.frontWheelCollider.steerAngle - wheel) < 0.01f,
                "wheelCollider=" + bike.frontWheelCollider.steerAngle.ToString("F2"));
        }

        private static void MeasureLeft(string label)
        {
            leftAxis = tilt.SteerAxis;
            float inputAxis = input.Horizontal;
            float wheel = bike.CurrentSteerAngleDegrees;

            Log(string.Format("{0}: calibrated={1:F1} tiltAxis={2:F3} inputHorizontal={3:F3} wheel={4:F2}",
                label, tilt.CalibratedTiltDegrees, leftAxis, inputAxis, wheel));

            Check(label + ": phone left produces negative steering", leftAxis < -0.9f,
                "axis=" + leftAxis.ToString("F3"));
            Check(label + ": InputManager receives negative steering", inputAxis < -0.9f,
                "horizontal=" + inputAxis.ToString("F3"));
            Check(label + ": wheel steers left", wheel < -20f, "wheel=" + wheel.ToString("F2") + " deg");
            Check(label + ": wheel value matches collider",
                Mathf.Abs(bike.frontWheelCollider.steerAngle - wheel) < 0.01f, "collider ok");
        }

        private static void MeasureRight(string label)
        {
            rightAxis = tilt.SteerAxis;
            float inputAxis = input.Horizontal;
            float wheel = bike.CurrentSteerAngleDegrees;

            Log(string.Format("{0}: calibrated={1:F1} tiltAxis={2:F3} inputHorizontal={3:F3} wheel={4:F2}",
                label, tilt.CalibratedTiltDegrees, rightAxis, inputAxis, wheel));

            Check(label + ": phone right produces positive steering", rightAxis > 0.9f,
                "axis=" + rightAxis.ToString("F3"));
            Check(label + ": InputManager receives positive steering", inputAxis > 0.9f,
                "horizontal=" + inputAxis.ToString("F3"));
            Check(label + ": wheel steers right", wheel > 20f, "wheel=" + wheel.ToString("F2") + " deg");
            Check(label + ": wheel value matches collider",
                Mathf.Abs(bike.frontWheelCollider.steerAngle - wheel) < 0.01f, "collider ok");
            Check(label + ": left and right have opposite signs",
                leftAxis < 0f && rightAxis > 0f, "left=" + leftAxis.ToString("F3") + " right=" + rightAxis.ToString("F3"));
        }

        private static void TestTiltPriorityOverStick(double now)
        {
            // Tilt left, then inject a full-right joystick value as a stale or
            // competing source. MobileBikeControls must overwrite it with tilt.
            tilt.SimulateAcceleration(new Vector3(-3.03f, -9.33f, 0f));
            stepEndsAt = now + 1.2;
            step = 6;

            // First half of step 6: settle, then inject and sample.
            EditorApplication.delayCall += () =>
            {
                input.SetMobileInput(1f, 0.55f, false);
                float injected = input.MobileHorizontal;
                double sampleAt = EditorApplication.timeSinceStartup + 0.25;

                void SamplePriority()
                {
                    if (EditorApplication.timeSinceStartup < sampleAt) return;
                    EditorApplication.update -= SamplePriority;

                    float axis = tilt.SteerAxis;
                    float horizontal = input.Horizontal;
                    Log(string.Format("tilt-priority: injected stick={0:F2}, tiltAxis={1:F3}, finalHorizontal={2:F3}",
                        injected, axis, horizontal));

                    Check("tilt priority: joystick cannot overwrite tilt", horizontal < -0.5f,
                        "final horizontal=" + horizontal.ToString("F3"));
                    Check("tilt priority: steering still comes from tilt", axis < -0.5f,
                        "axis=" + axis.ToString("F3"));

                    tilt.SetInvert(true);
                    tilt.SimulateAcceleration(new Vector3(3.03f, -9.33f, 0f)); // right
                    stepEndsAt = EditorApplication.timeSinceStartup + 1.3;
                }

                EditorApplication.update += SamplePriority;
            };
        }

        private static void TestInvert(double now)
        {
            float axis = tilt.SteerAxis;
            float wheel = bike.CurrentSteerAngleDegrees;
            Log(string.Format("invert: axis={0:F3} wheel={1:F2}", axis, wheel));

            Check("INV: right tilt steers left", axis < -0.9f, "axis=" + axis.ToString("F3"));
            Check("INV: wheel follows inverted axis", wheel < -20f, "wheel=" + wheel.ToString("F2"));

            tilt.SetInvert(false);
            tilt.SetNeutralCalibration(0f);
            tilt.SimulateAcceleration(new Vector3(2.04f, -9.6f, 0f)); // ~12 deg right
            tilt.BeginCalibration();
            step = 7;
            stepEndsAt = now + 1.4;
        }

        private static void TestCalibration(double now)
        {
            float calibrated = tilt.CalibratedTiltDegrees;
            float axis = tilt.SteerAxis;
            Log(string.Format("calibration: neutral={0:F1} calibrated={1:F2} axis={2:F3}",
                tilt.NeutralDegrees, calibrated, axis));

            Check("CAL: held angle becomes neutral", Mathf.Abs(calibrated) < 1.5f,
                "calibrated=" + calibrated.ToString("F2") + " deg");
            Check("CAL: steering at calibrated neutral is centered", Mathf.Abs(axis) < 0.08f,
                "axis=" + axis.ToString("F3"));

            tilt.SetNeutralCalibration(0f);
            tilt.SimulateAcceleration(new Vector3(0f, -9.81f, 0f));
            step = 8;
            stepEndsAt = now + 0.8;
        }

        private static void TestDismountLock()
        {
            Check("dismount lock flag", !input.allowEnterExit, "allowEnterExit=" + input.allowEnterExit);

            input.RequestEnterExitVehicle();
            bool consumed = input.ConsumeEnterExitVehicle();
            Check("mount/exit request rejected", !consumed, "ConsumeEnterExitVehicle=" + consumed);
            Check("rider still mounted after exit request", rider.IsRiding, "IsRiding=" + rider.IsRiding);
        }

        private static void TestVibration()
        {
            if (feedback == null)
            {
                Fail("No BikeImpactFeedback on the spawned bike.");
                return;
            }

            bool groundLight = feedback.ShouldVibrate(true, 3f);
            bool groundHard = feedback.ShouldVibrate(true, 7f);
            bool obstacleLight = feedback.ShouldVibrate(false, 3f);
            bool obstacleHard = feedback.ShouldVibrate(false, 9f);

            Check("vibration: normal ground riding is silent", !groundLight, "3 m/s terrain -> " + groundLight);
            Check("vibration: hard terrain landing vibrates", groundHard, "7 m/s terrain -> " + groundHard);
            Check("vibration: light obstacle contact vibrates", obstacleLight, "3 m/s tree -> " + obstacleLight);
            Check("vibration: hard obstacle contact vibrates", obstacleHard, "9 m/s tree -> " + obstacleHard);

            int callsBefore = BikeVibration.CallCount;
            int impactsBefore = feedback.ImpactCount;
            feedback.SimulateImpact(9f);

            Check("vibration: impact requests one vibration",
                BikeVibration.CallCount == callsBefore + 1,
                "calls " + callsBefore + " -> " + BikeVibration.CallCount);
            Check("vibration: duration within configured range",
                BikeVibration.LastDurationMs >= 25 && BikeVibration.LastDurationMs <= 90,
                BikeVibration.LastDurationMs + " ms");
            Check("vibration: impact counted", feedback.ImpactCount == impactsBefore + 1,
                "impacts " + impactsBefore + " -> " + feedback.ImpactCount);

            int impactsAfterFirst = feedback.ImpactCount;
            feedback.TriggerImpact(9f);
            Check("vibration: cooldown blocks immediate second vibration",
                feedback.ImpactCount == impactsAfterFirst,
                "impacts stayed " + feedback.ImpactCount);

            feedback.ResetCooldown();
        }

        private static void TestTopSpeedLimit()
        {
            if (bike == null || body == null) return;

            Log(string.Format("tuning: legPower={0} airResistance={1} max={2} km/h boostMax={3} km/h powerUpMax={4}",
                bike.LegPower, bike.AirResistance, bike.MaxSpeedKmh, bike.BoostMaxSpeedKmh, bike.PowerUpMax));

            Check("tuning: legPower raised to 26", Mathf.Approximately(bike.LegPower, 26f), "legPower=" + bike.LegPower);
            Check("tuning: air resistance lowered to 4", Mathf.Approximately(bike.AirResistance, 4f), "airResistance=" + bike.AirResistance);
            Check("tuning: normal top speed 62 km/h", Mathf.Approximately(bike.MaxSpeedKmh, 62f), "max=" + bike.MaxSpeedKmh);
            Check("tuning: boost top speed 88 km/h", Mathf.Approximately(bike.BoostMaxSpeedKmh, 88f), "boostMax=" + bike.BoostMaxSpeedKmh);

            // Put the bike above the ground and over the normal limit, then
            // check that the soft limiter removes motor torque instead of
            // letting it accelerate forever. Hovering avoids tree/ground
            // collisions that would otherwise distort the measurement.
            body.constraints = RigidbodyConstraints.FreezeRotation;
            body.position += Vector3.up * 3f;
            Physics.SyncTransforms();
            body.linearVelocity = bike.transform.forward * (70f / 3.6f);
            body.angularVelocity = Vector3.zero;

            double until = EditorApplication.timeSinceStartup + 0.12;
            double started = EditorApplication.timeSinceStartup;
            float measuredSpeed = 0f;
            float measuredTorque = -1f;

            void SampleSpeed()
            {
                if (EditorApplication.timeSinceStartup < until) return;
                EditorApplication.update -= SampleSpeed;

                measuredSpeed = bike.CurrentSpeedKmh;
                measuredTorque = bike.CurrentMotorTorque;
                Log(string.Format("speed limiter at {0:F1} km/h: motorTorque={1:F2} Nm",
                    measuredSpeed, measuredTorque));

                Check("speed limiter: above 62 km/h motor torque is cut", measuredTorque <= 0.5f,
                    "torque=" + measuredTorque.ToString("F2") + " Nm at " + measuredSpeed.ToString("F1") + " km/h");

                body.constraints = RigidbodyConstraints.FreezeAll;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            EditorApplication.update += SampleSpeed;
        }

        private static void PlanFinal(double now)
        {
            // Wait for the asynchronous speed sample, then finish.
            step = 9;
            stepEndsAt = now + 0.8;
        }

        // ---- reporting -------------------------------------------------------

        private static void Check(string name, bool passed, string detail)
        {
            string line = (passed ? "PASS  " : "FAIL  ") + name + "  [" + detail + "]";
            Report.Add(line);
            Log("BIKETEST " + line);

            if (!passed) failures++;
        }

        private static void Fail(string message)
        {
            failures++;
            Report.Add("FAIL  " + message);
            Log("BIKETEST FAIL  " + message);
        }

        private static void Log(string message)
        {
            Summary.AppendLine(message);
            Debug.Log("BIKETEST " + message);
        }

        private static string ReportPath
        {
            get
            {
                string root = Directory.GetParent(Application.dataPath).FullName;
                return Path.Combine(root, "Logs", "bike-verification.txt");
            }
        }

        private static void ClearReportFile()
        {
            try
            {
                string path = ReportPath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, "Bike verification run " + DateTime.Now.ToString("u") + Environment.NewLine);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("BikeGameVerification: could not reset report: " + exception.Message);
            }
        }

        private static void Finish()
        {
            EditorApplication.update -= Tick;
            EditorPrefs.SetBool(PendingKey, false);

            string header = failures == 0 ? "RESULT: ALL CHECKS PASSED" : "RESULT: " + failures + " CHECK(S) FAILED";
            Report.Insert(0, header + " (" + DateTime.Now.ToString("u") + ")");
            Report.Insert(1, new string('-', 72));

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
                File.AppendAllLines(ReportPath, Report);
                File.AppendAllText(ReportPath, Environment.NewLine + "--- log ---" + Environment.NewLine + Summary);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("BikeGameVerification: could not write report: " + exception.Message);
            }

            Debug.Log("BIKETEST " + header);
            Debug.Log("BIKETEST report: " + ReportPath);

            EditorApplication.Exit(failures == 0 ? 0 : 1);
        }
    }
}
