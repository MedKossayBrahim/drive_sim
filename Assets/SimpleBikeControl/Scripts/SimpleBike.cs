using UnityEngine;

namespace KikiNgao.SimpleBikeControl
{
    public class SimpleBike : MonoBehaviour
    {
        [Tooltip("Control without biker")]
        public bool noBikerCtrl;

        private InputManager inputManager;
        public Transform bikerHolder;

        public WheelCollider frontWheelCollider;
        public WheelCollider rearWheelCollider;
        public GameObject frontWheel;
        public GameObject rearWheel;

        public Transform handlerBar;
        public Transform cranksetTransform;

        [SerializeField] private float legPower = 10;
        [Tooltip("speed multiply max")]
        [SerializeField] private float powerUpMax = 2;
        [Tooltip("define how fast to reach power up max")]
        [SerializeField] private float powerUpSpeed = .5f;
        [SerializeField] private float airResistance = 6;
        [SerializeField] private float turningSmooth = .8f;
        [Tooltip("Rigidbody Drag while standing")]
        [SerializeField] private float restDrag = 2f;
        [Tooltip("Rigidbody AngularDrag while standing")]
        [SerializeField] private float restAngularDrag = .2f;

        [Header("Centre of mass")]
        [Tooltip("Riding centre of mass height above the contact patch. The old value (-0.8 m) sat below the ground, so drive force pitched the bike onto its nose and lifted the driven rear wheel on climbs.")]
        [SerializeField] private float rideCenterOfMassHeight = 0.35f;
        [Tooltip("Centre of mass height used while standing still.")]
        [SerializeField] private float restCenterOfMassHeight;
        [Tooltip("ratio of wheels and crankset rotation ")]
        // Ex:forceRatio = 2f; mean when crankset rotate 1 round the wheels rotate 2 round
        [SerializeField] private float forceRatio = 2f;
        // default: speed [0 -> 50] Angle [1 -> 35]
        [SerializeField] private AnimationCurve frontWheelRestrictCurve = new AnimationCurve(new Keyframe(0f, 35f), new Keyframe(50f, 1f));

        [Header("Speed limits")]
        [Tooltip("Top speed without BOOST, in km/h.")]
        [SerializeField] private float maxSpeedKmh = 62f;
        [Tooltip("Top speed while BOOST is held, in km/h.")]
        [SerializeField] private float boostMaxSpeedKmh = 88f;
        [Tooltip("Motor torque fades out over this many km/h below the active speed limit.")]
        [SerializeField] private float speedLimitSoftness = 4f;

        [Header("Steering response")]
        [Tooltip("How quickly the wheel reaches the requested steer angle. Higher is snappier.")]
        [SerializeField] private float steerResponse = 10f;

        [Header("Uphill assist")]
        [Tooltip("Slope in degrees where the extra climbing torque starts. Flat ground is unaffected.")]
        [SerializeField] private float uphillAssistStartDegrees = 12f;
        [Tooltip("Slope in degrees that receives the full climbing torque multiplier.")]
        [SerializeField] private float uphillAssistFullDegrees = 32f;
        [Tooltip("Peak torque multiplier on steep climbs. The speed limiter still caps top speed.")]
        [SerializeField] private float uphillAssistTorqueMultiplier = 1.4f;
        [Tooltip("How quickly the measured slope follows the terrain.")]
        [SerializeField] private float uphillSlopeSmoothing = 6f;

        public Transform leftHandTarget, rightHandTarget;
        public Transform leftPendalTarget, rightPendalTarget;
        public Transform leftStandTarget, rightStandTarget;

        private Transform centerOfMass;

        private Rigidbody m_Rigidbody;
        public Rigidbody GetRigidbody() => m_Rigidbody;


        [HideInInspector]
        public bool falling;
        private float fallingDrag = 1;
        private float fallingAngurlarDrag = 0.01f;

        private float temporaryFrontWheelAngle;
        private float currentSteerAngle;
        private float smoothedRideSlopeDegrees;

        private float handlerBarYLastAngle;
        private float currentLegPower;
        private float reversePower;
        private EventManager eventManager;

        public float CurrentSpeedKmh => GetBikeSpeedKm();
        public float CurrentSteerAngleDegrees => currentSteerAngle;
        public float CurrentMotorTorque => rearWheelCollider != null ? rearWheelCollider.motorTorque : 0f;
        public float LegPower => legPower;
        public float AirResistance => airResistance;
        public float MaxSpeedKmh => maxSpeedKmh;
        public float BoostMaxSpeedKmh => boostMaxSpeedKmh;
        public float PowerUpMax => powerUpMax;
        public float PowerUpAmount => powerUp;

        /// <summary>Riding centre of mass height used by the physics step.</summary>
        public float RideCenterOfMassHeight
        {
            get => rideCenterOfMassHeight;
            set => rideCenterOfMassHeight = value;
        }

        public bool IsReverse() => inputManager.vertical < 0;
        public bool IsMovingToward => inputManager.vertical > 0;
        private bool IsRest() => inputManager.vertical == 0 && inputManager.horizontal == 0 || inputManager.vertical == 0 && inputManager.horizontal != 0;
        public bool IsMoving() => inputManager.vertical != 0;
        private bool IsSpeedUp() => inputManager.speedUp;

        private float GetBikeSpeedKm() => GetBikeSpeedMs() * 3.6f;
        private float GetBikeSpeedMs() => m_Rigidbody.linearVelocity.magnitude;
        private float GetBikeAngle() => WrapAngle(transform.eulerAngles.z);

        public bool TiltToRight() => WrapAngle(transform.eulerAngles.z) <= 0;

        public bool Freeze { get => m_Rigidbody.isKinematic; set => m_Rigidbody.isKinematic = value; }
        public bool FreezeCrankset { get; set; }
        public bool ReadyToRide()
        {
            if (noBikerCtrl) return true;
            if (bikerHolder.childCount == 0) return false;
            if (bikerHolder.GetChild(0).CompareTag("Player")) return true;
            return false;
        }

        void Start()
        {
            inputManager = GameManager.Instance.GetInputManager;
            eventManager = GameManager.Instance.GetEventManager;

            CreateCenterOfMass();
            SettingRigidbody();

            currentLegPower = legPower * 10;
            reversePower = legPower * 8;

            Freeze = true;

        }

        //creat cemter of mass and add to the bike
        private void CreateCenterOfMass()
        {
            centerOfMass = new GameObject().transform;
            centerOfMass.name = "CenterOfMass";
            Vector3 center = new Vector3();
            // Local space: the bike can be placed anywhere in the scene, so the
            // wheel positions are converted from world to local space first.
            Vector3 rearPosition = transform.InverseTransformPoint(rearWheelCollider.transform.position);
            Vector3 frontPosition = transform.InverseTransformPoint(frontWheelCollider.transform.position);
            center.x = rearPosition.x;
            center.y = 0;
            center.z = rearPosition.z + (frontPosition.z - rearPosition.z) / 2;

            // The COM helper is attached first so that localPosition really is
            // local to the bike: assigning 'parent' keeps the world position by
            // default, which would place the center of mass far away from the
            // bike (and make the rigidbody fly around as soon as it is driven).
            centerOfMass.transform.SetParent(transform, false);
            centerOfMass.transform.localPosition = center;
            centerOfMass.transform.localRotation = Quaternion.identity;
        }
        private void SettingRigidbody()
        {
            m_Rigidbody = transform.GetComponent<Rigidbody>();
            m_Rigidbody.centerOfMass = centerOfMass.transform.localPosition;
        }
        float powerUp = 1f;
        private void FixedUpdate()
        {
            // falling when exit bike 
            if (falling) { Falling(); return; };

            // <<< no control handle above
            if (!ReadyToRide()) return;
            // under control handle under >>

            // ready to ride and no key press
            if (IsRest()) Rest();
            if (IsMoving()) MovingBike();

            // Steering is updated every physics step, including the return to
            // neutral. The original code only wrote steerAngle while the stick
            // was held, so the wheel stayed turned after the player let go.
            UpdateSteering();

            UpdateLegPower(IsSpeedUp());
            if (!FreezeCrankset) UpdateCranksetRotation();
            UpdateWheelDisplay();
        }

        private void UpdateLegPower(bool speedUp)
        {
            if (speedUp)
            {
                powerUp += powerUpSpeed * Time.deltaTime;
                if (powerUp >= powerUpMax) powerUp = powerUpMax;
                currentLegPower = legPower * 10 * powerUp;

                eventManager?.OnSpeedUp();

                return;
                //Debug.Log(powerUp);
            }
            eventManager?.OnNormalSpeed();
            powerUp = 1f;
            currentLegPower = legPower * 10 * powerUp;
        }
        private void MovingBike()
        {
            Freeze = false;
            m_Rigidbody.linearDamping = GetBikeSpeedMs() / m_Rigidbody.mass * airResistance;
            m_Rigidbody.angularDamping = 5 + GetBikeSpeedMs() / (m_Rigidbody.mass / 10);

            float speedKmh = GetBikeSpeedKm();

            if (IsReverse())
            {
                // Determine whether the bike is still rolling forward.
                // "Forward" means the velocity has a positive component along
                // the bike's local Z axis (the direction it faces).
                float forwardSpeed = Vector3.Dot(m_Rigidbody.linearVelocity, transform.forward);

                if (forwardSpeed > 0.5f)
                {
                    // Still moving forward: brake hard to scrub speed before
                    // applying reverse torque. Scales with joystick deflection.
                    float brakeStrength = Mathf.Abs(inputManager.vertical) * reversePower * 4f;
                    frontWheelCollider.brakeTorque = brakeStrength;
                    rearWheelCollider.brakeTorque  = brakeStrength;
                    rearWheelCollider.motorTorque  = 0f;
                }
                else
                {
                    // Near-stopped or already going backward: apply reverse
                    // motor torque. inputManager.vertical is negative so the
                    // product is negative, which drives the wheel backward.
                    frontWheelCollider.brakeTorque = 0f;
                    rearWheelCollider.brakeTorque  = 0f;
                    rearWheelCollider.motorTorque  = reversePower * inputManager.vertical;
                }
            }
            else
            {
                frontWheelCollider.brakeTorque = 0f;
                rearWheelCollider.brakeTorque  = 0f;
                float limit = IsSpeedUp() ? boostMaxSpeedKmh : maxSpeedKmh;
                float headroom = Mathf.Clamp01((limit - speedKmh) / Mathf.Max(0.1f, speedLimitSoftness));
                rearWheelCollider.motorTorque = currentLegPower * inputManager.vertical * headroom * UphillTorqueMultiplier();
            }

            UpdateCenterOfMass();
        }

        /// <summary>
        /// Extra torque on climbs only. Flat ground returns exactly 1 so the
        /// existing acceleration, top speed and BOOST tuning stay untouched.
        /// Without it a 200 kg bike on the stock 26 Nm/10 motor stalls around
        /// 35 degrees because the drive force equals the weight component.
        /// </summary>
        private float UphillTorqueMultiplier()
        {
            float pitch = Mathf.Asin(Mathf.Clamp(transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
            float blend = 1f - Mathf.Exp(-Mathf.Max(0.1f, uphillSlopeSmoothing) * Time.fixedDeltaTime);
            smoothedRideSlopeDegrees = Mathf.Lerp(smoothedRideSlopeDegrees, pitch, blend);

            return UphillMultiplierForSlope(smoothedRideSlopeDegrees);
        }

        private float UphillMultiplierForSlope(float slopeDegrees)
        {
            float full = Mathf.Max(uphillAssistStartDegrees + 1f, uphillAssistFullDegrees);
            float uphill = Mathf.InverseLerp(uphillAssistStartDegrees, full, slopeDegrees);
            return Mathf.Lerp(1f, Mathf.Max(1f, uphillAssistTorqueMultiplier), uphill);
        }

        /// <summary>Smoothed riding slope in degrees, positive uphill.</summary>
        public float RideSlopeDegrees => smoothedRideSlopeDegrees;

        /// <summary>Climbing torque multiplier currently applied (1 on flat).</summary>
        public float CurrentUphillTorqueMultiplier => UphillMultiplierForSlope(smoothedRideSlopeDegrees);

        private void UpdateSteering()
        {
            if (frontWheelCollider == null) return;

            float speedKmh = GetBikeSpeedKm();
            temporaryFrontWheelAngle = Mathf.Max(0.5f, frontWheelRestrictCurve.Evaluate(speedKmh));

            float targetAngle = temporaryFrontWheelAngle * Mathf.Clamp(inputManager.horizontal, -1f, 1f);

            // Keep the steering responsive at low speed but avoid instant full
            // lock at high speed, which would make the bike snap sideways.
            float speedFactor = Mathf.InverseLerp(10f, 70f, speedKmh);
            float response = Mathf.Max(1f, steerResponse) * Mathf.Lerp(1f, 0.6f, speedFactor);
            currentSteerAngle = Mathf.Lerp(currentSteerAngle, targetAngle,
                1f - Mathf.Exp(-response * Time.fixedDeltaTime));

            if (Mathf.Abs(currentSteerAngle - targetAngle) < 0.02f)
                currentSteerAngle = targetAngle;

            frontWheelCollider.steerAngle = currentSteerAngle;

            // Rotate the handlebar visually by the same delta as the wheel.
            if (handlerBar != null)
            {
                float delta = currentSteerAngle - handlerBarYLastAngle;
                if (Mathf.Abs(delta) > 0.0001f)
                {
                    handlerBar.rotation = Quaternion.Lerp(handlerBar.rotation,
                        handlerBar.rotation * Quaternion.Euler(0f, delta, 0f), turningSmooth);
                }

                handlerBarYLastAngle = currentSteerAngle;
            }
        }

        // the bike keep moving slowly if we forgot reset motor force
        private void ResetWheelsCollider()
        {
            frontWheelCollider.steerAngle = 0f;
            frontWheelCollider.motorTorque = 0;
            rearWheelCollider.motorTorque = 0;
            rearWheelCollider.brakeTorque = 0;
            frontWheelCollider.brakeTorque = 0;
        }

        /// <summary>
        /// Clears transient ride state after a respawn without rebuilding the
        /// bike or the rider. Keeps the existing mounting/animator setup.
        /// </summary>
        public void ResetRideState()
        {
            falling = false;
            powerUp = 1f;
            currentLegPower = legPower * 10f;
            currentSteerAngle = 0f;
            handlerBarYLastAngle = 0f;
            smoothedRideSlopeDegrees = 0f;
            ResetWheelsCollider();

            if (m_Rigidbody != null)
            {
                m_Rigidbody.linearVelocity = Vector3.zero;
                m_Rigidbody.angularVelocity = Vector3.zero;
                m_Rigidbody.linearDamping = restDrag;
                m_Rigidbody.angularDamping = restAngularDrag;
            }

            Freeze = false;
        }

        // when had biker and no key press
        private void Rest()
        {
            // the bike auto stop due to high drag
            m_Rigidbody.linearDamping = restDrag;
            m_Rigidbody.angularDamping = restAngularDrag;
            ResetWheelsCollider();
            UpdateCenterOfMass();


        }
        //When biker exit bike it's start falling 
        public void Falling()
        {
            falling = true;
            // the bike auto stop due to high drag
            m_Rigidbody.linearDamping = fallingDrag;
            m_Rigidbody.angularDamping = fallingAngurlarDrag;

            currentSteerAngle = 0f;
            handlerBarYLastAngle = 0f;
            UpdateCenterOfMass();
            UpdateWheelDisplay();
            ResetWheelsCollider();

            //Debug.Log(gameObject.name + "  Falling ");
            float angle = GetBikeAngle();
            if (angle < -75 || angle > 75) { Freeze = true; falling = false; }
        }
        private void UpdateCranksetRotation()
        {
            cranksetTransform.rotation *= Quaternion.Euler(GetBikeSpeedKm() / forceRatio, 0, 0);
            Quaternion ro = Quaternion.Euler(-GetBikeSpeedKm() / forceRatio, 0, 0);
            cranksetTransform.GetChild(0).rotation *= ro;
            cranksetTransform.GetChild(1).rotation *= ro;
        }

        private void UpdateWheelDisplay()
        {
            Vector3 temporaryVector;
            Quaternion temporaryQuaternion;

            rearWheelCollider.GetWorldPose(out temporaryVector, out temporaryQuaternion);
            rearWheel.transform.position = temporaryVector;

            Quaternion rearWheelRot = rearWheel.transform.rotation;

            rearWheel.transform.rotation = IsReverse() ? rearWheelRot * Quaternion.Euler(-GetBikeSpeedKm(), 0, 0)
                : rearWheelRot * Quaternion.Euler(GetBikeSpeedKm(), 0, 0);

            frontWheel.transform.localRotation = rearWheel.transform.localRotation;
        }

        private void UpdateCenterOfMass()
        {
            var centerLocal = centerOfMass.localPosition;

            if (!falling)
            {
                // Keep the rider's weight above the wheels while the bike is
                // actually rolling, even if the throttle is released. Dropping
                // the mass to the contact patch and off to one side while
                // coasting makes the bike tip over during turns.
                bool riding = !IsRest() || GetBikeSpeedMs() > 0.5f;

                if (riding)
                {
                    centerLocal.y = rideCenterOfMassHeight;
                    // The rest pose keeps a small sideways offset; leaving it
                    // in place at speed creates a constant roll moment.
                    centerLocal.x = 0f;
                }
                else
                {
                    centerLocal.y = restCenterOfMassHeight;
                    centerLocal.x = 0f;
                }
            }
            else //falling
            {
                centerLocal.y = 0;
                centerLocal.x = TiltToRight() ? .2f : -0.2f;
            }

            // when bike falling
            m_Rigidbody.centerOfMass = centerLocal;
        }

        private bool OnGround(WheelCollider wheelCollider)
        {
            if (Physics.Raycast(wheelCollider.transform.position, -transform.up, out RaycastHit hit, wheelCollider.radius + 0.1f))
            {
                return true;
            }
            return false;
        }
        // convert runtime Euler Angle to angle that showing in Unity Editor 
        private static float WrapAngle(float angle)
        {
            angle %= 360;
            if (angle > 180)
                return angle - 360;

            return angle;
        }

    }
}
