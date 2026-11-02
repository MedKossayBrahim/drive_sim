#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace Project.Plane
{
    /// <summary>Branche la caméra existante sur l'événement C déjà lu par PlaneInput.</summary>
    public static class PlaneCameraSetup
    {
        private const string ScenePath = "Assets/_Plane/Scenes/Plane_Scene.unity";
        private const string PrefabPath = "Assets/_Plane/Prefabs/Plane.prefab";
        [MenuItem("Sky Courier/7 - Setup Camera", false, 70)]
        public static void Setup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { Debug.LogWarning("[Sky Courier] Quitter Play avant de configurer la caméra."); return; }
            try
            {
                Scene scene = SceneManager.GetSceneByPath(ScenePath);
                if (!scene.IsValid() || !scene.isLoaded)
                    scene = EditorSceneManager.OpenScene(ScenePath,
                        SceneManager.sceneCount == 1 && !SceneManager.GetActiveScene().isDirty ? OpenSceneMode.Single : OpenSceneMode.Additive);
                var cameras = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Camera>(true))
                    .Where(c => c.CompareTag("MainCamera")).ToArray();
                if (cameras.Length != 1) throw new InvalidOperationException("Une seule caméra portant le tag MainCamera est requise dans Plane_Scene.");
                var planes = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<PlaneController>(true))
                    .Where(p => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(p.gameObject) == PrefabPath).ToArray();
                if (planes.Length != 1) throw new InvalidOperationException("Une seule instance de Plane.prefab est requise dans Plane_Scene.");
                var plane = planes[0];
                var input = plane.GetComponent<PlaneInput>();
                var body = plane.GetComponent<Rigidbody>();
                if (input == null || body == null) throw new InvalidOperationException("PlaneInput et Rigidbody sont requis sur Plane.");
                // Réflexion uniquement dans l'éditeur : garde PlaneInput.cs intact et conserve les autres abonnés.
                var field = typeof(PlaneInput).GetField("onCameraSwitch", BindingFlags.Instance | BindingFlags.NonPublic);
                var cameraEvent = field?.GetValue(input) as UnityEvent;
                if (cameraEvent == null) throw new InvalidOperationException("Événement onCameraSwitch introuvable sur PlaneInput.");
                var root = PrefabUtility.LoadPrefabContents(PrefabPath);
                try
                {
                    var anchors = root.transform.Cast<Transform>().Where(t => t.name == "CockpitView").ToArray();
                    if (anchors.Length > 1) throw new InvalidOperationException("CockpitView dupliqué dans le prefab.");
                    if (anchors.Length == 0)
                    {
                        var cockpit = new GameObject("CockpitView").transform;
                        cockpit.SetParent(root.transform, false);
                        cockpit.gameObject.layer = root.layer;
                        var com = root.transform.Find("CenterOfMass");
                        Vector3 center = com != null ? com.localPosition : root.GetComponent<Rigidbody>().centerOfMass;
                        cockpit.localPosition = center + new Vector3(0f, 1.4f, 1.2f);
                        cockpit.localRotation = Quaternion.identity;
                        if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null) throw new InvalidOperationException("Sauvegarde de CockpitView impossible.");
                    }
                    // En cas de relance, la position affinée par l'utilisateur est conservée.
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
                Transform view = plane.transform.Find("CockpitView");
                if (view == null) throw new InvalidOperationException("CockpitView non propagé à l'instance du prefab.");
                var camera = cameras[0];
                var rigs = camera.GetComponents<PlaneCameraRig>();
                if (rigs.Length > 1) throw new InvalidOperationException("Plusieurs rigs sont présents sur la Main Camera.");
                var rig = rigs.Length == 1 ? rigs[0] : Undo.AddComponent<PlaneCameraRig>(camera.gameObject);
                Undo.RecordObjects(new UnityEngine.Object[] { camera, camera.transform, rig, input }, "Configurer la caméra avion");
                camera.orthographic = false;
                camera.nearClipPlane = 0.3f; camera.farClipPlane = 8000f;
                rig.Configure(plane.transform, body, view);
                bool registered = false;
                for (int i = cameraEvent.GetPersistentEventCount() - 1; i >= 0; i--)
                {
                    if (cameraEvent.GetPersistentTarget(i) != rig || cameraEvent.GetPersistentMethodName(i) != nameof(PlaneCameraRig.NextMode)) continue;
                    if (registered) UnityEventTools.RemovePersistentListener(cameraEvent, i);
                    else { registered = true; cameraEvent.SetPersistentListenerState(i, UnityEventCallState.RuntimeOnly); }
                }
                if (!registered) UnityEventTools.AddPersistentListener(cameraEvent, rig.NextMode);
                foreach (var obj in new UnityEngine.Object[] { camera, camera.transform, rig, input }) EditorUtility.SetDirty(obj);
                PrefabUtility.RecordPrefabInstancePropertyModifications(input);
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Sauvegarde de Plane_Scene impossible.");
                AssetDatabase.SaveAssets(); Selection.activeGameObject = camera.gameObject;
                Debug.Log("[Sky Courier] Caméra prête : Chase derrière l'avion dès Play, C/bouton Caméra → Cockpit → Orbit. " +
                    "Cible et CockpitView assignés, near 0,3 / far 8000, un seul abonnement. " +
                    "Ajuster chaseOffset et CockpitView si nécessaire ; les autres objets et scripts sont conservés.", rig);
            }
            catch (Exception exception) { Debug.LogException(exception); }
        }
    }
}
#endif
