using System.Collections.Generic;
using UnityEngine;

namespace Project.Plane
{
    /// <summary>Contacts physiques réels ; évite de confondre un survol très bas avec un posé sur la piste.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(Rigidbody))]
    public sealed class PlaneLandingContact : MonoBehaviour
    {
        private readonly Dictionary<Collider,float> contacts=new Dictionary<Collider,float>();
        private Rigidbody body;
        private PlaneInput input;
        private void Awake(){body=GetComponent<Rigidbody>();input=GetComponent<PlaneInput>();}
        private void OnEnable(){if(input==null)input=GetComponent<PlaneInput>();if(input!=null)input.OnResetCompleted+=Clear;}
        private void OnDisable(){if(input!=null)input.OnResetCompleted-=Clear;Clear();}
        private void OnCollisionEnter(Collision collision)=>Remember(collision);
        private void OnCollisionStay(Collision collision)=>Remember(collision);
        private void OnCollisionExit(Collision collision)=>contacts.Remove(collision.collider);
        private void Remember(Collision collision)
        {
            for(int i=0;i<collision.contactCount;i++)
                if(Vector3.Dot(collision.GetContact(i).normal,Vector3.up)>.9f)
                {contacts[collision.collider]=Time.fixedTime;return;}
            contacts.Remove(collision.collider);
        }
        public bool HasContact(Collider surface)=>surface!=null&&contacts.TryGetValue(surface,out float time)&&
            (body!=null&&body.IsSleeping()||Time.fixedTime-time<=Time.fixedDeltaTime*2.1f);
        private void Clear()=>contacts.Clear();
    }
}
