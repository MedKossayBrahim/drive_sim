#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.Plane
{
    /// <summary>Applique les corrections de vol et du soleil sans modifier les contrôleurs protégés.</summary>
    public static class FlightBehaviorSetup
    {
        [MenuItem("Sky Courier/18 - Fix Flight And Sun",false,180)]
        public static void Setup()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){Debug.LogWarning("[Sky Courier] Quitter Play avant Fix Flight And Sun.");return;}
            try
            {
                var root=PrefabUtility.LoadPrefabContents("Assets/_Plane/Prefabs/Plane.prefab");
                try
                {
                    if(root.GetComponent<PlaneFlightEnvelope>()==null)root.AddComponent<PlaneFlightEnvelope>();
                    PrefabUtility.SaveAsPrefabAsset(root,"Assets/_Plane/Prefabs/Plane.prefab");
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
                const string path="Assets/_Plane/Scenes/Plane_Scene.unity";
                var scene=SceneManager.GetSceneByPath(path);
                if(!scene.IsValid()||!scene.isLoaded)scene=EditorSceneManager.OpenScene(path,
                    SceneManager.sceneCount==1&&!SceneManager.GetActiveScene().isDirty?OpenSceneMode.Single:OpenSceneMode.Additive);
                var sky=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<SkyboxSwitcher>(true)).Single();
                var settings=new SerializedObject(sky);settings.FindProperty("minimumSunElevation").floatValue=45;
                settings.FindProperty("sunAngularRadius").floatValue=.65f;settings.FindProperty("originalSunMaskRadius").floatValue=7;
                settings.FindProperty("sunBrightness").floatValue=24;
                settings.FindProperty("noonSourceSunDirection").vector3Value=new Vector3(.523252f,.761202f,-.383116f);
                settings.FindProperty("sunsetSourceSunDirection").vector3Value=new Vector3(.802462f,.104122f,-.587549f);
                settings.FindProperty("nightSourceSunDirection").vector3Value=new Vector3(.711419f,.006136f,-.702742f);
                settings.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(sky);
                AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
                Debug.Log("[Sky Courier] Vol corrigé pour Tilt et Touch : piqué réel, décrochage non annulé par l'assistance, crash par PlaneHealth à l'impact. Soleil/lune à au moins 45°, disque HDRI bas masqué, ciel toujours à l'infini.");
            }
            catch(Exception e){Debug.LogException(e);}
        }
    }
}
#endif
