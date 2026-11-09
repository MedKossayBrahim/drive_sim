using UnityEngine;

namespace BikeGame
{
    /// <summary>
    /// Loads the finish-line soccer ball prefab built from the supplied glTF
    /// model and parents it to this GameObject as a visual. If the prefab cannot
    /// be loaded a coloured sphere primitive is used as a fallback so the
    /// level-complete trigger always has something visible.
    /// </summary>
    [DefaultExecutionOrder(500)]
    public class BallVisualSetup : MonoBehaviour
    {
        [Tooltip("Scale applied to the loaded model. Matches the trigger collider radius.")]
        [SerializeField] private float modelScale = 1.5f;

        private void Start()
        {
            GameObject model = Resources.Load<GameObject>("SoccerBall");

            if (model != null)
            {
                GameObject visual = Instantiate(model, transform);
                visual.name = "SoccerBall_Visual";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                visual.transform.localScale = Vector3.one * modelScale;

                // The model has no colliders, but keep this guard so only the
                // trigger SphereCollider on the parent can fire the level end.
                foreach (Collider col in visual.GetComponentsInChildren<Collider>())
                    col.enabled = false;

                Debug.Log("BallVisualSetup: SoccerBall prefab loaded successfully.");
            }
            else
            {
                // Fallback: bright sphere so the goal is always visible.
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = "SoccerBall_Fallback";
                sphere.transform.SetParent(transform);
                sphere.transform.localPosition = Vector3.zero;
                sphere.transform.localScale = Vector3.one * modelScale;

                Renderer rend = sphere.GetComponent<Renderer>();
                if (rend != null)
                {
                    rend.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    rend.material.color = new Color(1f, 0.85f, 0.1f);
                }

                // Remove the primitive's built-in collider; the parent trigger handles it.
                Destroy(sphere.GetComponent<Collider>());

                Debug.LogWarning("BallVisualSetup: SoccerBall prefab not found in Resources, using fallback sphere.");
            }
        }
    }
}
