using System;
using System.Collections;
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
    /// Diagnostic harness for two reported Android issues:
    ///  1. which way the bike actually turns for a negative/positive steer axis
    ///     and for a simulated left/right phone tilt;
    ///  2. where the bike stalls on reproducible ramps (slope, throttle,
    ///     grounded state, forward slip, torque and speed).
    ///
    /// It creates a large test platform high above the forest terrain, so the
    /// forest scene, bike prefab, rider and controller are the real ones but
    /// trees cannot interfere with the measurements.
    /// </summary>
    public static class BikeGameHillDiagnostics
    {
        private const string PendingKey = "BikeGame.Diagnostics.Pending";

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

            const string scenePath = "Assets/BikeGame/Scenes/ForestBikeRide.unity";
            if (File.Exists(scenePath))
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;

            if (!EditorApplication.isPlaying)
                EditorApplication.isPlaying = true;
        }

        private static void Tick()
        {
            if (!EditorPrefs.GetBool(PendingKey, false))
            {
                EditorApplication.update -= Tick;
                return;
            }

            if (!EditorApplication.isPlaying) return;

            if (UnityEngine.Object.FindAnyObjectByType<BikeGameHillTestRunner>() == null)
            {
                GameObject runner = new GameObject("Bike Game Hill Test Runner");
                runner.AddComponent<BikeGameHillTestRunner>();
            }

            EditorApplication.update -= Tick;
        }

        internal static void WriteReport(IEnumerable<string> lines, bool passed)
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            string path = Path.Combine(root, "Logs", "bike-hill-diagnostics.txt");

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllLines(path, lines);
                Debug.Log("BIKEDIAG report: " + path);
            }
            catch (Exception exception)
            {
                Debug.LogError("BIKEDIAG could not write report: " + exception);
            }

            EditorPrefs.SetBool(PendingKey, false);
            EditorApplication.Exit(passed ? 0 : 1);
        }
    }

    /// <summary>Runs in play mode and performs the measurements.</summary>
    public class BikeGameHillTestRunner : MonoBehaviour
    {
        private readonly List<string> report = new List<string>();

        private SimpleBike bike;
        private Biker rider;
        private InputManager input;
        private TiltSteering tilt;
        private MobileBikeControls mobile;
        private BikeGameBootstrap bootstrap;
        private Rigidbody body;
        private float platformTop;
        private Vector3 platformCentre;
        private float settledHeight;

        private int failures;

        private IEnumerator Start()
        {
            yield return WaitForScene();

            if (bike == null)
            {
                Fail("Scene never became ready.");
                Finish();
                yield break;
            }

            SurveyTerrainSlopes();
            BuildPlatform();

            yield return TestSteerDirection(-1f, "stick-left");
            yield return TestSteerDirection(1f, "stick-right");
            yield return TestTiltDirection(-1f, "tilt-left");
            yield return TestTiltDirection(1f, "tilt-right");

            // Centre-of-mass sweep on the same 20 degree ramp. The old riding
            // value was -0.8 m (below the contact patch); these runs show which
            // height keeps the driven wheel loaded while climbing. They are
            // baselines, so they are reported but do not fail the run.
            yield return TestClimb(20, 1.0f, "BASELINE COM -0.80 m, full throttle", false, false, false, false, -0.8f, false);
            yield return TestClimb(20, 1.0f, "BASELINE COM 0.00 m, full throttle", false, false, false, false, 0f, false);
            yield return TestClimb(20, 1.0f, "COM 0.35 m, full throttle", false, false, false, false, 0.35f, true);
            yield return TestClimb(20, 1.0f, "COM 0.50 m, full throttle", false, false, false, false, 0.5f, true);
            yield return TestClimb(20, 0.55f, "BASELINE COM 0.00 m, auto-pedal 55%", false, false, false, false, 0f, false);
            yield return TestClimb(20, 0.55f, "BASELINE fixed 55%, COM 0.35 m", false, false, false, false, 0.35f, false);

            int[] angles = { 15, 20, 25, 30, 35, 40 };
            foreach (int angle in angles)
            {
                yield return TestClimb(angle, 1.0f, "full throttle, COM 0.35", false, false, false, false, 0.35f, angle <= 35);
                if (angle <= 30)
                    yield return TestClimb(angle, 0.55f, "BASELINE fixed 55%, COM 0.35", false, false, false, false, 0.35f, false);
            }

            TestAutoPedalCurve();

            // End to end: real MobileBikeControls tilt mode with the phone held
            // neutral on the same ramps, so the adaptive uphill auto pedal is
            // exercised through the shipping input path.
            int[] autoAngles = { 15, 20, 25, 30, 35 };
            foreach (int angle in autoAngles)
                yield return TestTiltAutoClimb(angle);

            yield return TestFlatSpeed();

            Finish();
        }

        /// <summary>
        /// Regression guard for the flat tuning: the uphill torque assist must be
        /// exactly 1 on level ground and normal acceleration must be unchanged.
        /// </summary>
        private IEnumerator TestFlatSpeed()
        {
            if (mobile != null) mobile.enabled = false;
            if (tilt != null) tilt.enabled = false;

            PlaceOnFlat();
            body.constraints = RigidbodyConstraints.FreezeRotationZ;
            input.SetMobileInput(0f, 1f, false);

            float end = Time.time + 6f;
            while (Time.time < end)
                yield return new WaitForFixedUpdate();

            float speed = bike.CurrentSpeedKmh;
            float torque = bike.CurrentMotorTorque;
            float multiplier = bike.CurrentUphillTorqueMultiplier;
            bool ok = multiplier <= 1.001f && speed > 30f;

            Log(string.Format(
                "FLAT full throttle after 6 s: speed={0:F1} km/h torque={1:F0} Nm uphillMultiplier={2:F3} slope={3:F1} deg -> {4}",
                speed, torque, multiplier, bike.RideSlopeDegrees,
                ok ? "flat tuning unchanged" : "CHECK"));

            if (!ok) failures++;

            input.SetMobileInput(0f, 0f, false);
            body.constraints = RigidbodyConstraints.None;
            yield return new WaitForSeconds(0.2f);
        }

        /// <summary>
        /// Samples the forest terrain around the spawn point to report which
        /// slopes the playable hills actually have. The ramp matrix above is
        /// meaningful only if it covers the terrain the player can ride on.
        /// </summary>
        private void SurveyTerrainSlopes()
        {
            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null || terrain.terrainData == null)
                return;

            Vector3 centre = bike.transform.position;
            const float halfExtent = 40f;
            const float sampleStep = 2f;
            const float normalStep = 1.5f;

            float maxSlope = 0f;
            float above12 = 0f;
            float above20 = 0f;
            float above30 = 0f;
            int count = 0;

            for (float dx = -halfExtent; dx <= halfExtent; dx += sampleStep)
            {
                for (float dz = -halfExtent; dz <= halfExtent; dz += sampleStep)
                {
                    Vector3 p = centre + new Vector3(dx, 0f, dz);
                    float h = terrain.SampleHeight(p) + terrain.transform.position.y;
                    float hx = terrain.SampleHeight(p + new Vector3(normalStep, 0f, 0f))
                               + terrain.transform.position.y;
                    float hz = terrain.SampleHeight(p + new Vector3(0f, 0f, normalStep))
                               + terrain.transform.position.y;

                    Vector3 alongX = new Vector3(normalStep, hx - h, 0f);
                    Vector3 alongZ = new Vector3(0f, hz - h, normalStep);
                    float slope = Vector3.Angle(Vector3.Cross(alongZ, alongX), Vector3.up);

                    count++;
                    maxSlope = Mathf.Max(maxSlope, slope);
                    if (slope > 12f) above12++;
                    if (slope > 20f) above20++;
                    if (slope > 30f) above30++;
                }
            }

            if (count == 0) return;

            Log(string.Format(
                "TERRAIN within {0} m of spawn: max slope {1:F1} deg, {2:F0}% above 12 deg, {3:F0}% above 20 deg, {4:F1}% above 30 deg",
                halfExtent, maxSlope, 100f * above12 / count, 100f * above20 / count, 100f * above30 / count));

            float centreHeight = terrain.SampleHeight(centre) + terrain.transform.position.y;
            float centreX = terrain.SampleHeight(centre + new Vector3(normalStep, 0f, 0f))
                            + terrain.transform.position.y;
            float centreZ = terrain.SampleHeight(centre + new Vector3(0f, 0f, normalStep))
                            + terrain.transform.position.y;
            Vector3 centreNormal = Vector3.Cross(
                new Vector3(0f, centreZ - centreHeight, normalStep),
                new Vector3(normalStep, centreX - centreHeight, 0f));
            Log(string.Format(
                "TERRAIN at spawn point: slope {0:F1} deg, bike pitch {1:F1} deg",
                Vector3.Angle(centreNormal, Vector3.up),
                Mathf.Asin(Mathf.Clamp(bike.transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg));
        }

        private IEnumerator WaitForScene()
        {
            float deadline = Time.realtimeSinceStartup + 25f;

            while (Time.realtimeSinceStartup < deadline)
            {
                bike = UnityEngine.Object.FindAnyObjectByType<SimpleBike>();
                rider = UnityEngine.Object.FindAnyObjectByType<Biker>();
                tilt = UnityEngine.Object.FindAnyObjectByType<TiltSteering>();
                mobile = UnityEngine.Object.FindAnyObjectByType<MobileBikeControls>();
                bootstrap = UnityEngine.Object.FindAnyObjectByType<BikeGameBootstrap>();

                if (GameManager.Instance != null)
                    input = GameManager.Instance.GetInputManager;

                if (bike != null && rider != null && input != null && rider.IsRiding)
                    break;

                yield return null;
            }

            body = bike != null ? bike.GetRigidbody() : null;

            if (bootstrap != null)
                bootstrap.enabled = false;

            if (body == null)
                yield break;

            Log("Scene ready. Bike mass=" + body.mass +
                " wheel radius=" + bike.rearWheelCollider.radius +
                " legPower=" + bike.LegPower +
                " maxSpeed=" + bike.MaxSpeedKmh +
                " boostMax=" + bike.BoostMaxSpeedKmh);

            Log("Rear friction: extremumSlip=" + bike.rearWheelCollider.forwardFriction.extremumSlip +
                " extremumValue=" + bike.rearWheelCollider.forwardFriction.extremumValue +
                " stiffness=" + bike.rearWheelCollider.forwardFriction.stiffness +
                " | Front extremumSlip=" + bike.frontWheelCollider.forwardFriction.extremumSlip);

            WheelCollider rear = bike.rearWheelCollider;
            Log("Rear suspension: distance=" + rear.suspensionDistance +
                " spring=" + rear.suspensionSpring.spring +
                " damper=" + rear.suspensionSpring.damper +
                " target=" + rear.suspensionSpring.targetPosition +
                " wheelMass=" + rear.mass +
                " forceApp=" + rear.forceAppPointDistance +
                " | rigidbody mass=" + body.mass);
        }

        private void BuildPlatform()
        {
            Terrain terrain = Terrain.activeTerrain;
            Vector3 centre = terrain != null
                ? terrain.transform.position + terrain.terrainData.size * 0.5f
                : Vector3.zero;

            platformCentre = new Vector3(centre.x, 120f, centre.z);

            GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = "Bike Test Platform";
            platform.transform.position = platformCentre;
            platform.transform.localScale = new Vector3(500f, 1f, 500f);
            platformTop = platformCentre.y + 0.5f;

            Log("Test platform top Y=" + platformTop.ToString("F1") + " at X=" + platformCentre.x.ToString("F1") +
                " Z=" + platformCentre.z.ToString("F1"));
        }

        // ---- steering direction -------------------------------------------------

        private IEnumerator TestSteerDirection(float steer, string label)
        {
            if (mobile != null) mobile.enabled = false;
            if (tilt != null) tilt.enabled = false;

            PlaceOnFlat();
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            input.SetMobileInput(steer, 0.8f, false);
            yield return new WaitForSeconds(0.5f);

            float yaw0 = bike.transform.eulerAngles.y;
            float speed0 = bike.CurrentSpeedKmh;
            yield return new WaitForSeconds(1.5f);
            float yaw1 = bike.transform.eulerAngles.y;
            float speed1 = bike.CurrentSpeedKmh;

            float delta = Mathf.DeltaAngle(yaw0, yaw1);
            bool ok = steer < 0f ? delta < -5f : delta > 5f;

            Log(string.Format("STEER {0}: input={1:+0.0;-0.0} yawDelta={2:+0.0;-0.0} deg speed={3:F1}->{4:F1} km/h wheel={5:F1} deg -> {6}",
                label, steer, delta, speed0, speed1, bike.CurrentSteerAngleDegrees, ok ? "left/right as expected" : "WRONG DIRECTION"));

            if (!ok) failures++;
            input.SetMobileInput(0f, 0f, false);
            yield return new WaitForSeconds(0.3f);
        }

        private IEnumerator TestTiltDirection(float expectedSign, string label)
        {
            PlaceOnFlat();
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            if (tilt != null)
            {
                tilt.enabled = true;
                tilt.SetMode(SteeringMode.Tilt);
                tilt.SetNeutralCalibration(0f);
                tilt.SimulateAcceleration(new Vector3(3.03f * expectedSign, -9.33f, 0f));
            }

            if (mobile != null) mobile.enabled = true;

            yield return new WaitForSeconds(1.2f);

            float axis = tilt != null ? tilt.SteerAxis : 0f;
            float wheel = bike.CurrentSteerAngleDegrees;
            float yaw0 = bike.transform.eulerAngles.y;
            float speed0 = bike.CurrentSpeedKmh;

            yield return new WaitForSeconds(1.5f);

            float delta = Mathf.DeltaAngle(yaw0, bike.transform.eulerAngles.y);
            float speed1 = bike.CurrentSpeedKmh;
            bool ok = expectedSign < 0f ? delta < -5f : delta > 5f;

            Log(string.Format("TILT {0}: tiltAxis={1:+0.00;-0.00} input={2:+0.00;-0.00} wheel={3:F1} yawDelta={4:+0.0;-0.0} speed={5:F1}->{6:F1} -> {7}",
                label, axis, input.Horizontal, wheel, delta, speed0, speed1, ok ? "expected turn" : "WRONG TURN"));

            if (!ok) failures++;
            input.SetMobileInput(0f, 0f, false);
            if (mobile != null) mobile.enabled = false;
            yield return new WaitForSeconds(0.3f);
        }

        // ---- climbing -----------------------------------------------------------

        private IEnumerator TestClimb(int angleDegrees, float throttle, string throttleLabel,
            bool frontTypeGrip, bool lockRotation, bool longSuspension, bool disableRiderColliders,
            float comHeight, bool countFailure)
        {
            if (mobile != null) mobile.enabled = false;
            if (tilt != null) tilt.enabled = false;

            Collider[] riderColliders = rider != null
                ? rider.GetComponentsInChildren<Collider>(true)
                : new Collider[0];
            bool[] riderColliderStates = new bool[riderColliders.Length];
            for (int i = 0; i < riderColliders.Length; i++)
            {
                riderColliderStates[i] = riderColliders[i].enabled;
                if (disableRiderColliders)
                    riderColliders[i].enabled = false;
            }

            WheelFrictionCurve stockRearGrip = bike.rearWheelCollider.forwardFriction;
            if (frontTypeGrip)
            {
                WheelFrictionCurve front = bike.frontWheelCollider.forwardFriction;
                WheelFrictionCurve rear = stockRearGrip;
                rear.extremumSlip = front.extremumSlip;
                rear.extremumValue = front.extremumValue;
                rear.asymptoteSlip = front.asymptoteSlip;
                rear.asymptoteValue = front.asymptoteValue;
                rear.stiffness = front.stiffness;
                bike.rearWheelCollider.forwardFriction = rear;
            }

            WheelCollider rearWheel = bike.rearWheelCollider;
            WheelCollider frontWheel = bike.frontWheelCollider;
            float stockRearDistance = rearWheel.suspensionDistance;
            float stockFrontDistance = frontWheel.suspensionDistance;
            JointSpring stockRearSpring = rearWheel.suspensionSpring;
            JointSpring stockFrontSpring = frontWheel.suspensionSpring;
            float stockComHeight = bike.RideCenterOfMassHeight;
            bike.RideCenterOfMassHeight = comHeight;

            if (longSuspension)
            {
                rearWheel.suspensionDistance = 0.15f;
                frontWheel.suspensionDistance = 0.15f;
                rearWheel.suspensionSpring = new JointSpring
                {
                    spring = 30000f, damper = 3000f, targetPosition = 0.5f
                };
                frontWheel.suspensionSpring = new JointSpring
                {
                    spring = 30000f, damper = 3000f, targetPosition = 0.5f
                };
            }

            // Let the suspension settle under the bike's real weight on the flat
            // platform, then reproduce that compression on the ramp. Spawning at
            // a guessed height either dropped or pre-loaded the springs.
            yield return SettleOnFlat();

            GameObject ramp = BuildRamp(angleDegrees);
            yield return null;

            PlaceOnRamp(angleDegrees, 6f);
            body.constraints = lockRotation
                ? RigidbodyConstraints.FreezeRotation
                : RigidbodyConstraints.FreezeRotationZ;
            input.SetMobileInput(0f, throttle, false);
            float radians = angleDegrees * Mathf.Deg2Rad;
            float rampBottomZ = platformCentre.z - Mathf.Cos(radians) * 40f;
            float surfaceAtSpawn = platformTop + (bike.transform.position.z - rampBottomZ) * Mathf.Tan(radians);

            bike.rearWheelCollider.GetWorldPose(out Vector3 rearPose, out Quaternion rearPoseRot);
            bike.frontWheelCollider.GetWorldPose(out Vector3 frontPose, out Quaternion frontPoseRot);
            float rearSurface = platformTop + (rearPose.z - rampBottomZ) * Mathf.Tan(radians);
            float frontSurface = platformTop + (frontPose.z - rampBottomZ) * Mathf.Tan(radians);

            Log(string.Format(
                "SPAWN angle={0} root={1} settled={2:F3} rearPose={3} rearBottomGap={4:F3} frontBottomGap={5:F3} ground={6}",
                angleDegrees,
                bike.transform.position.ToString("F2"),
                settledHeight,
                rearPose.ToString("F2"),
                rearPose.y - bike.rearWheelCollider.radius - rearSurface,
                frontPose.y - bike.frontWheelCollider.radius - frontSurface,
                bike.rearWheelCollider.isGrounded));

            float startY = bike.transform.position.y;
            float startZ = bike.transform.position.z;
            float startSpeed = bike.CurrentSpeedKmh;
            float minSpeed = 999f;
            float maxSpeed = 0f;
            float maxSlip = 0f;
            float torqueSum = 0f;
            float rearForceSum = 0f;
            float frontForceSum = 0f;
            float groundedFrames = 0;
            int samples = 0;
            bool stalled = false;
            float stalledAt = -1f;
            float lastSpeed = 0f;

            float duration = 8f;
            float end = Time.time + duration;

            while (Time.time < end)
            {
                yield return new WaitForFixedUpdate();
                input.SetMobileInput(0f, throttle, false);

                samples++;
                float speed = bike.CurrentSpeedKmh;
                float torque = bike.CurrentMotorTorque;
                bool grounded = bike.rearWheelCollider.isGrounded;
                float slip = 0f;
                float normalY = 0f;
                float elapsed = Time.time - (end - duration);
                float rearHitForce = 0f;
                float frontHitForce = 0f;

                if (grounded && bike.rearWheelCollider.GetGroundHit(out WheelHit hit))
                {
                    slip = Mathf.Abs(hit.forwardSlip);
                    normalY = hit.normal.y;
                    rearHitForce = hit.force;
                    groundedFrames++;

                    if (angleDegrees >= 30 && samples % 25 == 0)
                    {
                        Log(string.Format(
                            "HIT t={0:F2} force={1:F0} N normalY={2:F2} fwdSlip={3:F2} sideSlip={4:F2} rpm={5:F0} susp={6:F2}",
                            elapsed, hit.force, normalY, hit.forwardSlip, hit.sidewaysSlip,
                            bike.rearWheelCollider.rpm, bike.rearWheelCollider.suspensionDistance));
                    }
                }

                if (bike.frontWheelCollider.isGrounded &&
                    bike.frontWheelCollider.GetGroundHit(out WheelHit frontHit))
                    frontHitForce = frontHit.force;

                minSpeed = Mathf.Min(minSpeed, speed);
                maxSpeed = Mathf.Max(maxSpeed, speed);
                maxSlip = Mathf.Max(maxSlip, slip);
                torqueSum += torque;
                rearForceSum += rearHitForce;
                frontForceSum += frontHitForce;
                lastSpeed = speed;

                if (angleDegrees == 20 && countFailure && samples % 100 == 0 && elapsed < 3f)
                {
                    Vector3 rayOrigin = bike.transform.position + Vector3.up * 2f;
                    string below = "none";
                    if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit belowHit, 3f,
                            ~0, QueryTriggerInteraction.Ignore))
                    {
                        below = belowHit.collider.name + " y=" + belowHit.point.y.ToString("F2") +
                                " n=" + belowHit.normal.ToString("F2");
                    }

                    Log(string.Format(
                        "TRACE angle={0} t={1:F2} root={2} rear={3} grounded={4} vel={5:F2} m/s frozen={6} below={7}\n" +
                        "      inputV={8:F2} motor={9:F0} brake={10:F0} rpm={11:F0} frontGround={12} " +
                        "rearForce={13:F0} frontForce={14:F0}",
                        angleDegrees, elapsed, bike.transform.position.ToString("F2"),
                        bike.rearWheelCollider.transform.position.ToString("F2"), grounded,
                        body.linearVelocity.magnitude, body.isKinematic, below,
                        input.Vertical, bike.CurrentMotorTorque, bike.rearWheelCollider.brakeTorque,
                        bike.rearWheelCollider.rpm, bike.frontWheelCollider.isGrounded,
                        rearHitForce, frontHitForce));
                }

                if (!stalled && elapsed > 2.5f && speed < 0.8f)
                {
                    stalled = true;
                    stalledAt = Time.time - (end - duration);
                    float slope = Vector3.Angle(bike.transform.forward,
                        Vector3.ProjectOnPlane(bike.transform.forward, Vector3.up));
                    Log(string.Format(
                        "STALL angle={0} deg throttle={1:F2} ({2}) at t={3:F2}s speed={4:F1} km/h torque={5:F0} Nm grounded={6} slip={7:F3} slope={8:F1} deg",
                        angleDegrees, throttle, throttleLabel, stalledAt, speed, torque, grounded, slip, slope));
                }
            }

            input.SetMobileInput(0f, 0f, false);

            float endY = bike.transform.position.y;
            float endZ = bike.transform.position.z;
            float climbed = endY - startY;
            float travelled = Mathf.Sqrt((endZ - startZ) * (endZ - startZ) + climbed * climbed);

            bool success = lastSpeed > 2f && climbed > 1.5f;
            if (!success && countFailure) failures++;

            float avgTorque = samples > 0 ? torqueSum / samples : 0f;
            float groundedPercent = samples > 0 ? 100f * groundedFrames / samples : 0f;
            float avgRearForce = samples > 0 ? rearForceSum / samples : 0f;
            float avgFrontForce = samples > 0 ? frontForceSum / samples : 0f;

            Log(string.Format(
                "CLIMB angle={0} throttle={1:F2} ({2}): {3} | speed {4:F1}->{5:F1} (min {6:F1}, max {7:F1}) km/h, avgTorque={8:F0} Nm, grounded={9:F0}%, maxSlip={10:F3}, climbed={11:F1} m, rearForce={12:F0} N, frontForce={13:F0} N, COM={14:F2} m",
                angleDegrees, throttle, throttleLabel, success ? "CLIMBED" : "STALLED",
                startSpeed, lastSpeed, minSpeed, maxSpeed, avgTorque, groundedPercent, maxSlip, climbed,
                avgRearForce, avgFrontForce, comHeight));

            bike.rearWheelCollider.forwardFriction = stockRearGrip;
            rearWheel.suspensionDistance = stockRearDistance;
            frontWheel.suspensionDistance = stockFrontDistance;
            rearWheel.suspensionSpring = stockRearSpring;
            frontWheel.suspensionSpring = stockFrontSpring;
            bike.RideCenterOfMassHeight = stockComHeight;

            for (int i = 0; i < riderColliders.Length; i++)
                riderColliders[i].enabled = riderColliderStates[i];

            body.constraints = RigidbodyConstraints.None;
            UnityEngine.Object.Destroy(ramp);
            yield return new WaitForSeconds(0.2f);
        }

        private void TestAutoPedalCurve()
        {
            if (mobile == null)
            {
                Fail("MobileBikeControls missing, cannot test auto pedal.");
                return;
            }

            float flat = mobile.AutoThrottleForSlope(0f);
            float mid = mobile.AutoThrottleForSlope(8f);
            float steep = mobile.AutoThrottleForSlope(15f);
            bool ok = Mathf.Abs(flat - 0.55f) < 0.01f &&
                      mid > flat + 0.05f &&
                      mid < 0.999f &&
                      steep > 0.99f;

            Log(string.Format(
                "AUTOPEDAL flat(0deg)={0:F2} at 8deg={1:F2} at 15deg={2:F2} -> {3}",
                flat, mid, steep, ok ? "adaptive uphill power ok" : "WRONG"));

            if (!ok) failures++;
        }

        private IEnumerator TestTiltAutoClimb(int angleDegrees)
        {
            if (mobile == null || tilt == null)
            {
                Fail("Tilt components missing, cannot run end-to-end tilt climb.");
                yield break;
            }

            mobile.enabled = false;
            tilt.enabled = false;

            yield return SettleOnFlat();

            GameObject ramp = BuildRamp(angleDegrees);
            yield return null;

            PlaceOnRamp(angleDegrees, 6f);
            body.constraints = RigidbodyConstraints.FreezeRotationZ;

            tilt.enabled = true;
            tilt.SetMode(SteeringMode.Tilt);
            tilt.SetNeutralCalibration(0f);
            // Upright landscape: gravity points down the screen, roll axis 0.
            tilt.SimulateAcceleration(new Vector3(0f, -9.81f, 0f));
            mobile.enabled = true;

            yield return new WaitForFixedUpdate();

            float startY = bike.transform.position.y;
            float minSpeed = 999f;
            float maxSpeed = 0f;
            float maxSlip = 0f;
            float torqueSum = 0f;
            float groundedFrames = 0;
            int samples = 0;
            float lastSpeed = 0f;
            float lastThrottle = 0f;
            float lastSlope = 0f;
            float peakSlope = 0f;

            float duration = 8f;
            float end = Time.time + duration;

            while (Time.time < end)
            {
                yield return new WaitForFixedUpdate();

                samples++;
                float speed = bike.CurrentSpeedKmh;
                torqueSum += bike.CurrentMotorTorque;
                minSpeed = Mathf.Min(minSpeed, speed);
                maxSpeed = Mathf.Max(maxSpeed, speed);
                lastSpeed = speed;
                lastThrottle = input.Vertical;
                lastSlope = mobile.CurrentSlopeDegrees;
                peakSlope = Mathf.Max(peakSlope, lastSlope);

                if (bike.rearWheelCollider.isGrounded &&
                    bike.rearWheelCollider.GetGroundHit(out WheelHit hit))
                {
                    groundedFrames++;
                    maxSlip = Mathf.Max(maxSlip, Mathf.Abs(hit.forwardSlip));
                }
            }

            input.SetMobileInput(0f, 0f, false);
            mobile.enabled = false;
            tilt.enabled = false;
            tilt.ClearSimulatedAcceleration();

            float climbed = bike.transform.position.y - startY;
            bool success = lastSpeed > 2f && climbed > 1.5f;
            if (!success) failures++;

            Log(string.Format(
                "AUTOCLIMB angle={0} tilt-mode: {1} | throttle {2:F2} (auto), slope {3:F1} deg (peak {4:F1}), " +
                "speed {5:F1} km/h (min {6:F1}, max {7:F1}), avgTorque={8:F0} Nm, grounded={9:F0}%, " +
                "maxSlip={10:F3}, climbed={11:F1} m",
                angleDegrees, success ? "CLIMBED" : "STALLED", lastThrottle, lastSlope, peakSlope,
                lastSpeed, minSpeed, maxSpeed,
                samples > 0 ? torqueSum / samples : 0f,
                samples > 0 ? 100f * groundedFrames / samples : 0f,
                maxSlip, climbed));

            body.constraints = RigidbodyConstraints.None;
            UnityEngine.Object.Destroy(ramp);
            yield return new WaitForSeconds(0.2f);
        }

        private IEnumerator SettleOnFlat()        {
            input.SetMobileInput(0f, 0f, false);
            PlaceBike(new Vector3(platformCentre.x, platformTop + 0.5f, platformCentre.z),
                Quaternion.identity);

            yield return new WaitForSeconds(1.5f);

            settledHeight = bike.transform.position.y - platformTop;
            Log(string.Format(
                "SETTLE height={0:F3} m rearGround={1} frontGround={2} rearSusp={3:F3}",
                settledHeight, bike.rearWheelCollider.isGrounded,
                bike.frontWheelCollider.isGrounded,
                bike.rearWheelCollider.suspensionDistance));
        }

        private GameObject BuildRamp(int angleDegrees)
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            const float length = 80f;
            const float width = 16f;

            GameObject ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.name = "Bike Test Ramp " + angleDegrees;
            ramp.transform.localScale = new Vector3(width, 1f, length);
            ramp.transform.rotation = Quaternion.Euler(-angleDegrees, 0f, 0f);
            ramp.transform.position = new Vector3(
                platformCentre.x,
                platformTop + Mathf.Sin(radians) * length * 0.5f - Mathf.Cos(radians) * 0.5f,
                platformCentre.z);

            Physics.SyncTransforms();
            return ramp;
        }

        private void PlaceOnFlat()
        {
            Vector3 position = new Vector3(platformCentre.x, platformTop, platformCentre.z);
            PlaceBike(position, Quaternion.identity);
        }

        private void PlaceOnRamp(int angleDegrees, float distanceFromBottom)
        {
            float radians = angleDegrees * Mathf.Deg2Rad;
            float bottomZ = platformCentre.z - Mathf.Cos(radians) * 40f;
            float z = bottomZ + Mathf.Cos(radians) * distanceFromBottom;
            float y = platformTop + Mathf.Sin(radians) * distanceFromBottom;

            Vector3 up = new Vector3(0f, Mathf.Cos(radians), -Mathf.Sin(radians));
            Vector3 forward = new Vector3(0f, Mathf.Sin(radians), Mathf.Cos(radians));

            Quaternion rotation = Quaternion.LookRotation(forward, up);
            // Reproduce the compression measured on the flat platform instead of
            // dropping the bike from above the short suspension travel.
            PlaceBike(new Vector3(platformCentre.x, y, z) + up * settledHeight, rotation);
        }

        private void PlaceBike(Vector3 position, Quaternion rotation)
        {
            body.constraints = RigidbodyConstraints.None;
            body.position = position;
            body.rotation = rotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            bike.transform.SetPositionAndRotation(position, rotation);
            Physics.SyncTransforms();
            bike.ResetRideState();
        }

        // ---- reporting ----------------------------------------------------------

        private void Log(string message)
        {
            report.Add(message);
            Debug.Log("BIKEDIAG " + message);
        }

        private void Fail(string message)
        {
            failures++;
            Log("FAIL " + message);
        }

        private void Finish()
        {
            report.Insert(0, failures == 0
                ? "RESULT: all direction/climb checks passed"
                : "RESULT: " + failures + " check(s) failed");

            BikeGameHillDiagnostics.WriteReport(report, failures == 0);
        }
    }
}
