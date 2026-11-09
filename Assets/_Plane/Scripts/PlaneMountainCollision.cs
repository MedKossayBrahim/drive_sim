using UnityEngine;

namespace Project.Plane
{
    /// <summary>Le contact avec le relief met fin à cette mission ; la piste conserve les seuils de dégâts normaux.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(PlaneHealth))]
    public sealed class PlaneMountainCollision : MonoBehaviour
    {
        [SerializeField] private LandingMission mission;
        private PlaneHealth health;
        private void Awake()=>health=GetComponent<PlaneHealth>();
        private void OnCollisionEnter(Collision collision)
        {
            if(mission==null||mission.IsTerminal||health.IsCrashed||!mission.Input.IsMissionStarted)return;
            if(collision.collider.GetComponentInParent<MountainHazard>()!=null)
                health.ApplyDamage(health.MaximumHealth);
        }
#if UNITY_EDITOR
        public void Configure(LandingMission owner)=>mission=owner;
#endif
    }
}
