using UnityEngine;

namespace Project.Plane
{
    /// <summary>Coupe seulement les gaz, avant le contrôleur. Ne désactive ni portance, ni traînée, ni pilotage.</summary>
    [DefaultExecutionOrder(-100), DisallowMultipleComponent, RequireComponent(typeof(PlaneSensors), typeof(PlaneController))]
    public sealed class EngineCutoff : MonoBehaviour
    {
        private PlaneSensors sensors;
        private PlaneController controller;
        private void Awake() { sensors = GetComponent<PlaneSensors>(); controller = GetComponent<PlaneController>(); }
        private void FixedUpdate() => EnforceCutoff();
        // PlaneInput peut réécrire Throttle dans Update : protège la physique puis l'affichage et l'hélice.
        private void LateUpdate() => EnforceCutoff();
        private void EnforceCutoff()
        { if (sensors != null && controller != null && !sensors.IsEngineActive()) controller.Throttle = 0f; }
    }
}
