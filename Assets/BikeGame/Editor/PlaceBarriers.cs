using System;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BikeGame.EditorTools
{
    /// <summary>
    /// Temporary one-shot script: places 4 road-barrier obstacles along the
    /// riding route using actual terrain heights, then saves the scene.
    /// </summary>
    public static class PlaceBarriers
    {
        private const string ScenePath = "Assets/BikeGame/Scenes/ForestBikeRide.unity";

        private static readonly Vector3 Spawn =
            new Vector3(31.567383f, 2.4903865f, 52.807617f);
        private const float SpawnYaw = 67.5f;

        // Barrier placements: distance along route, lateral offset, size.
        private static readonly float[] BarrierDistances = { 4f, 10f, 18f, 21f };
        private static readonly float[] BarrierLateral   = { 0.8f, -0.5f, 0.5f, -0.8f };

        private static readonly Vector3 BarrierSize = new Vector3(0.8f, 0.6f, 0.3f);
        private static readonly Color BarrierColor = new Color(0.95f, 0.45f, 0.08f, 1f);

        private static readonly StringBuilder Log = new StringBuilder();

        public static void Run()
        {
            try
            {
                Place();
                EditorApplication.Exit(0);
            }
            catch (Exception e)
            {
                Debug.LogError("PlaceBarriers FAILED: " + e);
                EditorApplication.Exit(1);
            }
        }

        private static void Place()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Vector3 forward = new Vector3(
                Mathf.Sin(SpawnYaw * Mathf.Deg2Rad), 0f,
                Mathf.Cos(SpawnYaw * Mathf.Deg2Rad));
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;

            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null) throw new InvalidOperationException("No active terrain.");

            // Remove old barriers if re-running.
            for (int i = 1; i <= 4; i++)
            {
                GameObject old = GameObject.Find("Road Barrier " + i);
                if (old != null)
                    UnityEngine.Object.DestroyImmediate(old);
            }

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/BikeGame/Barriers/Source/Red_barricade.fbx");
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/BikeGame/Barriers/Materials/RedBarricade.mat");
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.color = BarrierColor;
                mat.SetFloat("_Smoothness", 0.3f);
            }

            LogLine(prefab != null
                ? "=== BARRIERS (Red_barricade from read road bar) ==="
                : "=== BARRIERS (cube fallback) ===");

            for (int i = 0; i < BarrierDistances.Length; i++)
            {
                float d = BarrierDistances[i];
                float lat = BarrierLateral[i];

                Vector3 center = Spawn + forward * d;
                Vector3 pos = center + right * lat;

                float ground = terrain.transform.position.y + terrain.SampleHeight(pos);

                GameObject barrier;
                if (prefab != null)
                {
                    barrier = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                    barrier.name = "Road Barrier " + (i + 1);
                    barrier.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, SpawnYaw, 0f));
                    Bounds bounds = GetBounds(barrier);
                    float width = Mathf.Max(bounds.size.x, bounds.size.z);
                    if (width > 0.05f)
                        barrier.transform.localScale *= 1.15f / width;
                    bounds = GetBounds(barrier);
                    barrier.transform.position += new Vector3(
                        pos.x - bounds.center.x,
                        ground - bounds.min.y,
                        pos.z - bounds.center.z);
                }
                else
                {
                    pos.y = ground + BarrierSize.y * 0.5f;
                    barrier = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    barrier.name = "Road Barrier " + (i + 1);
                    barrier.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, SpawnYaw, 0f));
                    barrier.transform.localScale = BarrierSize;
                    SceneManager.MoveGameObjectToScene(barrier, scene);
                }

                foreach (var rend in barrier.GetComponentsInChildren<Renderer>())
                    if (mat != null) rend.sharedMaterial = mat;

                int ignoreRaycast = LayerMask.NameToLayer("Ignore Raycast");
                if (ignoreRaycast >= 0)
                {
                    foreach (var t in barrier.GetComponentsInChildren<Transform>(true))
                        t.gameObject.layer = ignoreRaycast;
                }

                foreach (var col in barrier.GetComponentsInChildren<Collider>())
                    col.isTrigger = false;
                if (barrier.GetComponentInChildren<Collider>() == null)
                    barrier.AddComponent<BoxCollider>();

                if (barrier.GetComponent<BarrierCollision>() == null)
                    barrier.AddComponent<BarrierCollision>();

                LogLine(string.Format("  Road Barrier {0}: pos=({1:F3}, {2:F3}, {3:F3}) d={4:F1} L={5:F1} ground={6:F3}",
                    i + 1, barrier.transform.position.x, barrier.transform.position.y, barrier.transform.position.z, d, lat, ground));
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            LogLine("=== SCENE SAVED ===");
            Debug.Log(Log.ToString());
        }

        private static void LogLine(string s)
        {
            Log.AppendLine(s);
            Debug.Log("PlaceBarriers: " + s);
        }

        private static Bounds GetBounds(GameObject target)
        {
            var renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return new Bounds(target.transform.position, Vector3.one);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }
    }
}
