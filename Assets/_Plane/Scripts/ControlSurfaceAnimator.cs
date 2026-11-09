using System;
using UnityEngine;

namespace Project.Plane
{
    /// <summary>Anime uniquement les pivots visuels ; ne jamais assigner le Transform du Rigidbody.</summary>
    [DisallowMultipleComponent]
    public sealed class ControlSurfaceAnimator : MonoBehaviour
    {
        [Serializable]
        private sealed class Surface
        {
            [SerializeField] private Transform pivot = null;
            [SerializeField, Tooltip("Axe dans le repère LOCAL du pivot avant animation.")]
            private Vector3 localAxis = Vector3.right;
            [SerializeField, Range(-60f, 60f), Tooltip("Angle signé pour une entrée de +1.")]
            private float maximumAngle = 20f;
            private Quaternion restRotation;
            private float angle;
            private bool ready;

            public Surface(Vector3 axis, float degrees) { localAxis = axis; maximumAngle = degrees; }
            public void Initialize(Transform bodyRoot)
            {
                ready = pivot != null && pivot != bodyRoot &&
                    (bodyRoot == null || !bodyRoot.IsChildOf(pivot));
                if (!ready) return;
                restRotation = pivot.localRotation;
                angle = 0f;
            }
            public void Animate(float input, float degreesPerSecond)
            {
                if (!ready || pivot == null || localAxis.sqrMagnitude < 0.0001f) return;
                angle = Mathf.MoveTowards(angle, Mathf.Clamp(input, -1f, 1f) * maximumAngle,
                    degreesPerSecond * Time.deltaTime);
                pivot.localRotation = restRotation * Quaternion.AngleAxis(angle, localAxis.normalized);
            }
            public void Restore()
            {
                if (ready && pivot != null) pivot.localRotation = restRotation;
                ready = false;
            }
        }

        [SerializeField] private PlaneController controller = null;
        [Header("Pivots aux charnières ; inverser l'angle si le modèle est orienté autrement")]
        [SerializeField] private Surface leftAileron = new Surface(Vector3.right, 20f);
        [SerializeField] private Surface rightAileron = new Surface(Vector3.right, -20f);
        [SerializeField] private Surface elevator = new Surface(Vector3.right, -20f);
        [SerializeField] private Surface rudder = new Surface(Vector3.up, 25f);
        [SerializeField, Min(0f)] private float surfaceSpeed = 120f;

        [Header("Hélice")]
        [SerializeField] private Transform propeller = null;
        [SerializeField] private Vector3 propellerLocalAxis = Vector3.forward;
        [SerializeField, Min(0f)] private float maximumPropellerRpm = 1800f;
        [SerializeField, Min(0f)] private float propellerAccelerationRpm = 1200f;
        private Quaternion propellerRestRotation;
        private float propellerAngle, propellerRpm;
        private bool propellerReady;

        private void OnEnable()
        {
            if (controller == null) controller = GetComponentInParent<PlaneController>();
            Transform root = controller != null ? controller.transform : null;
            leftAileron.Initialize(root);
            rightAileron.Initialize(root);
            elevator.Initialize(root);
            rudder.Initialize(root);
            propellerReady = propeller != null && propeller != root &&
                (root == null || !root.IsChildOf(propeller));
            if (propellerReady) propellerRestRotation = propeller.localRotation;
            propellerAngle = propellerRpm = 0f;
        }

        private void LateUpdate()
        {
            if (controller == null) return;
            leftAileron.Animate(controller.Roll, Mathf.Max(0f, surfaceSpeed));
            rightAileron.Animate(controller.Roll, Mathf.Max(0f, surfaceSpeed));
            elevator.Animate(controller.Pitch, Mathf.Max(0f, surfaceSpeed));
            rudder.Animate(controller.Yaw, Mathf.Max(0f, surfaceSpeed));
            if (!propellerReady || propeller == null || propellerLocalAxis.sqrMagnitude < 0.0001f) return;
            propellerRpm = Mathf.MoveTowards(propellerRpm, controller.EngineThrottle * Mathf.Max(0f, maximumPropellerRpm),
                Mathf.Max(0f, propellerAccelerationRpm) * Time.deltaTime);
            // Un tour = 360°, une minute = 60 s : RPM × 6 = degrés/seconde.
            propellerAngle = (propellerAngle + propellerRpm * 6f * Time.deltaTime) % 360f;
            propeller.localRotation = propellerRestRotation *
                Quaternion.AngleAxis(propellerAngle, propellerLocalAxis.normalized);
        }

        private void OnDisable()
        {
            leftAileron.Restore(); rightAileron.Restore(); elevator.Restore(); rudder.Restore();
            if (propellerReady && propeller != null) propeller.localRotation = propellerRestRotation;
        }
    }
}
