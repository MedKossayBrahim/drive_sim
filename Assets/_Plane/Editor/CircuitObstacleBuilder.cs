#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Project.Plane
{
    /// <summary>Installe un piton sur le raccourci entre les anneaux 2 et 3 et dévie les pièces par la droite.</summary>
    public static class CircuitObstacleBuilder
    {
        internal const int SegmentIndex = 1;
        private const string ObstacleName = "CircuitMountain";
        private const float Radius = 82f;
        private const float Height = 380f;
        private const float MaximumDetour = 90f;

        internal static float DetourOffset(float progress) => 4f * MaximumDetour * progress * (1f - progress);

        internal static bool IsInstalled(Scene scene) => scene.GetRootGameObjects()
            .Any(root => root.name == "Environment" && root.transform.Find(ObstacleName) != null);

        [MenuItem("Sky Courier/Scenario/Build Circuit Obstacle")]
        public static void Build()
        {
            var scene = ModuleSceneUtility.Open();
            var course = ModuleSceneUtility.Only<CheckpointManager>(scene);
            var spawner = ModuleSceneUtility.Only<CollectibleSpawner>(scene);
            var settings = ModuleSceneUtility.Settings();
            int ground = LayerMask.NameToLayer("Ground");
            if (ground < 0 || course.Checkpoints.Count <= SegmentIndex + 1)
                throw new InvalidOperationException("Le layer Ground et les anneaux 2 et 3 sont requis.");

            var positions = CollectibleSetup.RoutePoints(course, settings, true);
            var straight = CollectibleSetup.RoutePoints(course, settings, false);
            var serialized = new SerializedObject(spawner);
            var instances = serialized.FindProperty("instances");
            var localPositions = serialized.FindProperty("localPositions");
            if (instances.arraySize != positions.Count || localPositions.arraySize != positions.Count)
                throw new InvalidOperationException("Reconstruire les pièces avant d’installer le massif.");

            var coins = new Transform[positions.Count];
            for (int i = 0; i < coins.Length; i++)
            {
                var item = instances.GetArrayElementAtIndex(i).objectReferenceValue as Collectible;
                if (item == null) throw new InvalidOperationException("Pièce absente de la scène.");
                coins[i] = item.transform;
            }

            var environment = ModuleSceneUtility.Root(scene, "Environment");
            var mountain = environment.Find(ObstacleName)?.gameObject;
            if (mountain == null)
            {
                mountain = new GameObject(ObstacleName, typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider), typeof(MountainHazard));
                mountain.transform.SetParent(environment, false);
                Undo.RegisterCreatedObjectUndo(mountain, "Créer le massif du circuit");
            }
            Vector3 position = Vector3.Lerp(course.Checkpoints[SegmentIndex].transform.position,
                course.Checkpoints[SegmentIndex + 1].transform.position, .5f);
            position.y = -.5f;
            mountain.transform.position = position;
            mountain.layer = ground;
            mountain.isStatic = true;

            const string meshFolder = "Assets/_Plane/Models/Procedural/Mountains";
            ModuleSceneUtility.Folder(meshFolder);
            var mesh = ModuleSceneUtility.SaveMesh(meshFolder + "/CircuitMountain.asset", MountainBuilder.CreateMesh(Radius, Height, settings.Seed));
            mountain.GetComponent<MeshFilter>().sharedMesh = mesh;
            mountain.GetComponent<MeshCollider>().sharedMesh = mesh;
            var renderer = mountain.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = ModuleSceneUtility.Material("Assets/_Plane/Materials/Mountain_Altitude.mat",
                "Sky Courier/Mountain Vertex Color", Color.white);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            // Met à jour les seules pièces du détour et leur position de réapparition.
            for (int i = 0; i < coins.Length; i++)
            {
                if ((positions[i] - straight[i]).sqrMagnitude < .001f) continue;
                Undo.RecordObject(coins[i], "Dévier la ligne de pièces");
                coins[i].position = positions[i];
                PrefabUtility.RecordPrefabInstancePropertyModifications(coins[i]);
                localPositions.GetArrayElementAtIndex(i).vector3Value = spawner.transform.InverseTransformPoint(positions[i]);
            }
            serialized.ApplyModifiedProperties();
            ModuleSceneUtility.Save(scene);
            Debug.Log("[Sky Courier] Massif entre les anneaux 2 et 3. Suivez les pièces par la droite.");
        }
    }
}
#endif
