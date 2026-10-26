using DriveSim.Submarine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DriveSim.EditorTools
{
    /// <summary>
    /// Reconstruit SubmarineScene en scène propre : caméra, lumière, point d'entrée.
    /// Le monde (fosse, submersible, créature, HUD) est monté au lancement par SubmarineBoot.
    /// Menu : Drive Sim > Rebuild Submarine Scene.
    /// </summary>
    public static class SubmarineSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/SubmarineScene.unity";

        [MenuItem("Drive Sim/Rebuild Submarine Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.015f, 0.06f, 0.12f);
            camera.fieldOfView = 62f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 520f;
            cameraObject.transform.position = new Vector3(0f, -35f, -13f);
            cameraObject.AddComponent<AudioListener>();

            var lightObject = new GameObject("Directional Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1f;
            light.color = new Color(0.72f, 0.86f, 1f);
            light.shadows = LightShadows.Soft;
            lightObject.transform.rotation = Quaternion.Euler(52f, -32f, 0f);

            var boot = new GameObject("SubmarineBoot");
            boot.AddComponent<SubmarineBoot>();

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Debug.LogError("SUBMARINE SCENE SAVE FAILED: " + ScenePath);
                return;
            }

            AssetDatabase.SaveAssets();
            Debug.Log("SUBMARINE SCENE OK: " + ScenePath);
        }
    }
}
