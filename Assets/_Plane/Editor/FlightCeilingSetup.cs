#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Project.Plane
{
    /// <summary>Installe le plafond sur le prefab et prolonge uniquement le sol d'environnement de Plane_Scene.</summary>
    public static class FlightCeilingSetup
    {
        const string PrefabPath="Assets/_Plane/Prefabs/Plane.prefab";
        const string ScenePath="Assets/_Plane/Scenes/Plane_Scene.unity";
        [MenuItem("Sky Courier/17 - Add Flight Ceiling",false,171)]
        public static void Setup()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){Debug.LogWarning("[Sky Courier] Quitter Play avant Add Flight Ceiling.");return;}
            try
            {
                var root=PrefabUtility.LoadPrefabContents(PrefabPath);
                float radius;Vector3 center;
                try
                {
                    var ceiling=root.GetComponent<FlightCeiling>();if(ceiling==null)ceiling=root.AddComponent<FlightCeiling>();
                    radius=ceiling.HorizontalRadius;center=ceiling.MapCenter;
                    var sensors=root.GetComponent<PlaneSensors>();
                    if(sensors!=null&&Mathf.Abs(new SerializedObject(sensors).FindProperty("seaLevelWorldY").floatValue)>.001f)
                        Debug.LogWarning("[Sky Courier] FlightCeiling utilise Y monde : régler le zéro MSL de PlaneSensors à 0 pour un HUD cohérent.");
                    if(PrefabUtility.SaveAsPrefabAsset(root,PrefabPath)==null)throw new InvalidOperationException("Sauvegarde Plane.prefab impossible.");
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
                var scene=SceneManager.GetSceneByPath(ScenePath);
                if(!scene.IsValid()||!scene.isLoaded)scene=EditorSceneManager.OpenScene(ScenePath,
                    SceneManager.sceneCount==1&&!SceneManager.GetActiveScene().isDirty?OpenSceneMode.Single:OpenSceneMode.Additive);
                ExtendGround(scene,radius,center);
                EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
                if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Sauvegarde Plane_Scene impossible.");
                Debug.Log("[Sky Courier] Plafond installé : alerte 600 m MSL, limite 800 m MSL (Y monde), rayon horizontal configurable et prolongement du sol. Contrôleur, santé et checkpoints inchangés.");
            }
            catch(Exception exception){Debug.LogException(exception);}
        }
        static void ExtendGround(Scene scene,float radius,Vector3 center)
        {
            var roots=scene.GetRootGameObjects();
            var grass=roots.SelectMany(r=>r.GetComponentsInChildren<MeshRenderer>(true)).FirstOrDefault(r=>r.name=="GrassGround");
            if(grass==null){Debug.LogWarning("[Sky Courier] GrassGround absent : prolongement du sol ignoré.");return;}
            var environment=roots.FirstOrDefault(r=>r.name=="Environment");
            if(environment==null){environment=new GameObject("Environment");SceneManager.MoveGameObjectToScene(environment,scene);Undo.RegisterCreatedObjectUndo(environment,"Créer Environment");}
            var existing=environment.transform.Find("FlightBoundaryGround");
            GameObject ground;
            if(existing!=null)ground=existing.gameObject;
            else
            {
                ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.name="FlightBoundaryGround";
                ground.transform.SetParent(environment.transform,false);Undo.RegisterCreatedObjectUndo(ground,"Prolonger le sol");
            }
            float far=roots.SelectMany(r=>r.GetComponentsInChildren<Camera>(true)).Select(c=>c.farClipPlane).DefaultIfEmpty(8000).Max();
            float side=2f*(radius+far+4000f);
            Undo.RecordObject(ground.transform,"Ajuster le prolongement du sol");
            ground.transform.SetPositionAndRotation(new Vector3(center.x,grass.transform.position.y-.02f,center.z),Quaternion.identity);
            ground.transform.localScale=new Vector3(side/10f,1f,side/10f);
            var meshCollider=ground.GetComponent<MeshCollider>();if(meshCollider!=null)Undo.DestroyObjectImmediate(meshCollider);
            var collider=ground.GetComponent<BoxCollider>();if(collider==null)collider=Undo.AddComponent<BoxCollider>(ground);
            collider.size=new Vector3(10,.1f,10);collider.center=new Vector3(0,-.05f,0);
            ground.layer=grass.gameObject.layer;ground.isStatic=true;
            var renderer=ground.GetComponent<MeshRenderer>();renderer.sharedMaterial=grass.sharedMaterial;
            renderer.shadowCastingMode=ShadowCastingMode.Off;
            EditorUtility.SetDirty(ground);EditorUtility.SetDirty(renderer);EditorUtility.SetDirty(collider);
            Debug.Log($"[Sky Courier] Sol prolongé à {side:0} × {side:0} m, sous GrassGround ; même matériau uni. Piste conservée à son altitude actuelle, zéro MSL = Y 0.");
        }
    }
}
#endif
