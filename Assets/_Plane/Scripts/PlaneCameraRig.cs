using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Project.Plane
{
    public enum PlaneCameraMode { Chase, Cockpit, Orbit }

    /// <summary>Caméra de vol : lecture des gestes dans Update, suivi du Transform interpolé dans LateUpdate.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    public sealed class PlaneCameraRig : MonoBehaviour
    {
        [Header("Cible — Z avant, Y haut")]
        [SerializeField] private Transform target = null;
        [SerializeField] private Rigidbody targetBody = null;
        [SerializeField] private Transform cockpitView = null;
        [SerializeField] private PlaneCameraMode mode = PlaneCameraMode.Chase;

        [Header("Chase")]
        [SerializeField] private Vector3 chaseOffset = new Vector3(0f, 3f, -14f);
        [SerializeField, Min(0.01f)] private float positionSmoothTime = 0.12f;
        [SerializeField, Min(0.01f), Tooltip("Réactivité de rotation en 1/s ; réduire pour sentir davantage les virages.")]
        private float rotationSharpness = 3f;
        [SerializeField, Min(0f)] private float lookAhead = 3f;
        [SerializeField] private float lookHeight = 0.8f;
        [SerializeField, Tooltip("Stabilise Chase. Cockpit conserve le repère du pilote, Orbit reste vertical.")]
        private bool stabilizeRoll = true;
        [SerializeField, Range(0f, 1f)] private float rollFollow = 0.12f;
        [SerializeField, Range(30f, 100f)] private float minimumFov = 60f;
        [SerializeField, Range(30f, 110f)] private float maximumFov = 75f;
        [SerializeField, Min(1f)] private float fullFovSpeed = 80f;
        [SerializeField, Min(0.01f)] private float fovSharpness = 4f;
        [SerializeField, Min(1f), Tooltip("Recadrage immédiat après un reset ou une téléportation.")]
        private float teleportDistance = 100f;

        [Header("Cockpit")]
        [SerializeField, Range(30f, 110f)] private float cockpitFov = 70f;

        [Header("Orbit — souris droite ou doigt sur une zone libre de la moitié droite")]
        [SerializeField, Min(1f)] private float orbitDistance = 18f;
        [SerializeField, Range(-80f, 80f)] private float initialOrbitPitch = 15f;
        [SerializeField, Min(0f)] private float mouseSensitivity = 0.14f;
        [SerializeField, Min(0f)] private float touchSensitivity = 0.12f;
        [SerializeField] private Vector2 orbitPitchLimits = new Vector2(-20f, 80f);

        [Header("Anti-clipping")]
        [SerializeField] private bool preventClipping = true;
        [SerializeField] private LayerMask collisionMask = ~0;
        [SerializeField, Min(0.3f)] private float collisionPadding = 0.5f;

        [Header("Tremblement — facultatif, hors cockpit")]
        [SerializeField] private bool highSpeedShake = false;
        [SerializeField, Min(0f)] private float shakeStartSpeed = 60f;
        [SerializeField, Min(0f)] private float shakePosition = 0.035f;
        [SerializeField, Min(0f)] private float shakeDegrees = 0.15f;
        [SerializeField, Min(0f)] private float shakeFrequency = 12f;

        private Camera viewCamera;
        private Quaternion followRotation;
        private Vector3 positionVelocity, chaseRelativeOffset, lastTargetPosition, lastStableUp = Vector3.up;
        private PlaneCameraMode appliedMode;
        private bool initialized, mouseDragging;
        private int? orbitTouchId;
        private float orbitYaw, orbitPitch;
        private readonly RaycastHit[] hits = new RaycastHit[64];
        private readonly List<RaycastResult> uiHits = new List<RaycastResult>();
        private PointerEventData pointerData;
        private EventSystem pointerSystem;
        public PlaneCameraMode Mode => mode;
        public Transform Target => target != null ? target : targetBody != null ? targetBody.transform : null;
        public Transform CockpitView => cockpitView;
        public float Speed => targetBody != null ? targetBody.linearVelocity.magnitude : 0f;

        private void OnEnable()
        {
            viewCamera = GetComponent<Camera>();
            ResolveTarget();
            initialized = false;
            SnapToTarget();
        }
        private void Start() => SnapToTarget();
        private void ResolveTarget()
        {
            if (targetBody == null && target != null) targetBody = target.GetComponentInParent<Rigidbody>();
        }
        public void NextMode()
        {
            var next = (PlaneCameraMode)(((int)mode + 1) % 3);
            if (next == PlaneCameraMode.Cockpit && cockpitView == null) next = PlaneCameraMode.Orbit;
            SetMode(next);
        }
        public void SetMode(PlaneCameraMode value)
        {
            if (value == PlaneCameraMode.Cockpit && cockpitView == null)
            { Debug.LogWarning("[Sky Courier] CockpitView absent : retour à Chase.", this); value = PlaneCameraMode.Chase; }
            mode = appliedMode = value;
            initialized = false;
            mouseDragging = false; orbitTouchId = null;
            if (Target != null) orbitYaw = Target.eulerAngles.y;
            orbitPitch = initialOrbitPitch;
            SnapToTarget();
        }

        private void Update()
        {
            if (mode != appliedMode) SetMode(mode);
            if (mode != PlaneCameraMode.Orbit || Time.timeScale <= 0f) return;
            Vector2 delta = Vector2.zero;
            var mouse = Mouse.current;
            if (Application.isEditor && mouse != null)
            {
                if (mouse.rightButton.wasPressedThisFrame)
                    mouseDragging = !IsOverUI(mouse.position.ReadValue());
                if (!mouse.rightButton.isPressed) mouseDragging = false;
                if (mouseDragging) delta += mouse.delta.ReadValue() * mouseSensitivity;
            }
            var screen = Touchscreen.current;
            bool retainedFinger = false;
            if (screen != null)
            {
                foreach (var touch in screen.touches)
                {
                    int id = touch.touchId.ReadValue();
                    if (!orbitTouchId.HasValue && touch.press.wasPressedThisFrame &&
                        touch.position.ReadValue().x >= Screen.width * 0.5f && !IsOverUI(touch.position.ReadValue()))
                        orbitTouchId = id;
                    if (orbitTouchId != id || !touch.press.isPressed) continue;
                    retainedFinger = true;
                    delta += touch.delta.ReadValue() * touchSensitivity * (1920f / Mathf.Max(1, Screen.width));
                    break;
                }
            }
            if (!retainedFinger) orbitTouchId = null;
            orbitYaw = Mathf.Repeat(orbitYaw + delta.x, 360f);
            orbitPitch = Mathf.Clamp(orbitPitch - delta.y, Mathf.Min(orbitPitchLimits.x, orbitPitchLimits.y),
                Mathf.Max(orbitPitchLimits.x, orbitPitchLimits.y));
        }
        private bool IsOverUI(Vector2 position)
        {
            var system = EventSystem.current;
            if (system == null) return false;
            if (pointerData == null || pointerSystem != system)
            { pointerSystem = system; pointerData = new PointerEventData(system); }
            pointerData.Reset(); pointerData.position = position;
            uiHits.Clear(); system.RaycastAll(pointerData, uiHits);
            // Un geste qui commence sur le slider, le joystick ou les boutons reste réservé à l'UI.
            return uiHits.Count > 0;
        }

        private Quaternion FollowBasis()
        {
            if (!stabilizeRoll) return Target.rotation;
            Vector3 forward = Target.forward;
            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, forward);
            if (up.sqrMagnitude < 0.01f) up = Vector3.ProjectOnPlane(lastStableUp, forward);
            if (up.sqrMagnitude < 0.001f) up = Vector3.ProjectOnPlane(Vector3.forward, forward);
            lastStableUp = up.normalized;
            return Quaternion.Slerp(Quaternion.LookRotation(forward, lastStableUp), Target.rotation, Mathf.Clamp01(rollFollow));
        }
        private Vector3 OrbitPosition(Vector3 center) => center +
            Quaternion.Euler(orbitPitch, orbitYaw, 0f) * Vector3.back * Mathf.Max(1f, orbitDistance);
        private float FlightFov() => Mathf.Lerp(minimumFov, maximumFov, Mathf.Clamp01(Speed / Mathf.Max(1f, fullFovSpeed)));

        [ContextMenu("Recadrer immédiatement sur l'avion")]
        public void SnapToTarget()
        {
            if (viewCamera == null) viewCamera = GetComponent<Camera>();
            ResolveTarget();
            if (Target == null || Target == transform) return;
            if (!initialized)
            {
                orbitYaw = Target.eulerAngles.y; orbitPitch = initialOrbitPitch;
            }
            followRotation = FollowBasis(); positionVelocity = Vector3.zero;
            chaseRelativeOffset = followRotation * chaseOffset;
            Vector3 center = Target.position + Vector3.up * lookHeight;
            if (mode == PlaneCameraMode.Cockpit && cockpitView != null)
            {
                transform.SetPositionAndRotation(cockpitView.position, cockpitView.rotation);
                viewCamera.fieldOfView = cockpitFov;
            }
            else
            {
                Vector3 position = mode == PlaneCameraMode.Orbit ? OrbitPosition(center) : Target.position + chaseRelativeOffset;
                position = AvoidClipping(center, position);
                Vector3 look = mode == PlaneCameraMode.Orbit ? center : center + Target.forward * lookAhead;
                transform.SetPositionAndRotation(position, LookRotation(look - position, mode == PlaneCameraMode.Orbit ? Vector3.up : followRotation * Vector3.up));
                viewCamera.fieldOfView = FlightFov();
            }
            lastTargetPosition = Target.position; appliedMode = mode; initialized = true;
        }
        private static Quaternion LookRotation(Vector3 direction, Vector3 up)
        { return direction.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(direction, up) : Quaternion.identity; }

        private void LateUpdate()
        {
            if (Target == null || Target == transform) return;
            if (!initialized || Vector3.Distance(Target.position, lastTargetPosition) > teleportDistance)
            { SnapToTarget(); return; }
            if (mode == PlaneCameraMode.Cockpit && cockpitView != null)
            {
                // Fixation exacte au cockpit, sans retard de position ni d'orientation.
                transform.SetPositionAndRotation(cockpitView.position, cockpitView.rotation);
                viewCamera.fieldOfView = cockpitFov;
                lastTargetPosition = Target.position; return;
            }
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            followRotation = Quaternion.Slerp(followRotation, FollowBasis(), 1f - Mathf.Exp(-rotationSharpness * dt));
            Vector3 center = Target.position + Vector3.up * lookHeight;
            bool orbit = mode == PlaneCameraMode.Orbit;
            float smoothTime = Mathf.Max(0.01f, positionSmoothTime);
            Vector3 position;
            if (orbit)
            {
                position = Vector3.SmoothDamp(transform.position, OrbitPosition(center), ref positionVelocity,
                    smoothTime, Mathf.Infinity, dt);
            }
            else
            {
                // L'avion est déjà interpolé : seul le décalage de caméra prend du retard dans le virage.
                chaseRelativeOffset = Vector3.SmoothDamp(chaseRelativeOffset, followRotation * chaseOffset,
                    ref positionVelocity, smoothTime, Mathf.Infinity, dt);
                position = Target.position + chaseRelativeOffset;
            }
            Vector3 safePosition = AvoidClipping(center, position);
            if (orbit && (safePosition - position).sqrMagnitude > 0.0001f) positionVelocity = Vector3.zero;
            Vector3 look = orbit ? center : center + Target.forward * lookAhead;
            Quaternion orientation = LookRotation(look - safePosition, orbit ? Vector3.up : followRotation * Vector3.up);
            if (orbit)
                orientation = Quaternion.Slerp(transform.rotation, orientation, 1f - Mathf.Exp(-rotationSharpness * dt));
            if (highSpeedShake)
            {
                float intensity = Mathf.InverseLerp(shakeStartSpeed, Mathf.Max(shakeStartSpeed + 1f, fullFovSpeed), Speed);
                float t = Time.time * shakeFrequency;
                Vector2 noise = new Vector2(Mathf.PerlinNoise(t, 1.7f), Mathf.PerlinNoise(8.3f, t)) * 2f - Vector2.one;
                safePosition = AvoidClipping(center, safePosition + orientation * new Vector3(noise.x, noise.y, 0f) * (shakePosition * intensity));
                orientation *= Quaternion.Euler(noise.y * shakeDegrees * intensity, noise.x * shakeDegrees * intensity, 0f);
            }
            transform.SetPositionAndRotation(safePosition, orientation);
            viewCamera.fieldOfView = Mathf.Lerp(viewCamera.fieldOfView, FlightFov(), 1f - Mathf.Exp(-fovSharpness * dt));
            lastTargetPosition = Target.position;
        }
        private Vector3 AvoidClipping(Vector3 origin, Vector3 desired)
        {
            Vector3 direction = desired - origin;
            float distance = direction.magnitude;
            if (!preventClipping || distance < 0.001f) return desired;
            int mask = collisionMask.value;
            int planeLayer = LayerMask.NameToLayer("Plane");
            if (planeLayer >= 0) mask &= ~(1 << planeLayer);
            int count = Physics.RaycastNonAlloc(origin, direction / distance, hits, distance, mask, QueryTriggerInteraction.Ignore);
            float nearest = distance;
            Transform ownRoot = targetBody != null ? targetBody.transform : Target;
            for (int i = 0; i < count; i++)
            {
                var hit = hits[i];
                if (hit.collider.transform.IsChildOf(ownRoot)) continue;
                nearest = Mathf.Min(nearest, Mathf.Max(0.01f, hit.distance - Mathf.Max(viewCamera.nearClipPlane, collisionPadding)));
            }
            return origin + direction / distance * nearest;
        }
#if UNITY_EDITOR
        public void Configure(Transform aircraft, Rigidbody body, Transform cockpit)
        { target = aircraft; targetBody = body; cockpitView = cockpit; SnapToTarget(); }
#endif
    }
}
