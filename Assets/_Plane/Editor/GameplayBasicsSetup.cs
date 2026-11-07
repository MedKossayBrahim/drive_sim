#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Project.Plane
{
    /// <summary>Installe les composants sur Plane et une seule station de ravitaillement sur l'apron.</summary>
    public static class GameplayBasicsSetup
    {
        private const string ScenePath="Assets/_Plane/Scenes/Plane_Scene.unity";
        private const string PlanePath="Assets/_Plane/Prefabs/Plane.prefab";
        private const string RingPath="Assets/_Plane/Prefabs/Checkpoint_Ring.prefab";
        [MenuItem("Sky Courier/10 - Setup Gameplay Basics",false,100)]
        public static void Setup()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode){Debug.LogWarning("[Sky Courier] Quitter Play avant Setup Gameplay Basics.");return;}
            try
            {
                Scene scene=SceneManager.GetSceneByPath(ScenePath);
                if(!scene.IsValid()||!scene.isLoaded)scene=EditorSceneManager.OpenScene(ScenePath,
                    SceneManager.sceneCount==1&&!SceneManager.GetActiveScene().isDirty?OpenSceneMode.Single:OpenSceneMode.Additive);
                var apron=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true))
                    .FirstOrDefault(t=>t.name=="Apron"&&t.GetComponent<Renderer>()!=null);
                if(apron==null)throw new InvalidOperationException("Apron avec Renderer introuvable ; aucun objet de décor ne sera déplacé.");
                if(AssetDatabase.LoadAssetAtPath<GameObject>(PlanePath)==null)throw new InvalidOperationException("Plane.prefab absent.");
                foreach(string folder in new[]{"Materials","VFX"})
                    if(!AssetDatabase.IsValidFolder("Assets/_Plane/"+folder))AssetDatabase.CreateFolder("Assets/_Plane",folder);
                LayerSetup.Configure();
                var explosion=ExplosionMaterial();
                EditPrefab(PlanePath,root=>
                {
                    if(root.GetComponent<PlaneInput>()==null||root.GetComponent<PlaneSensors>()==null||root.GetComponent<Rigidbody>()==null)
                        throw new InvalidOperationException("PlaneInput, PlaneSensors et Rigidbody requis sur Plane.");
                    root.layer=LayerMask.NameToLayer("Plane");
                    foreach(var collider in root.GetComponentsInChildren<Collider>(true))collider.gameObject.layer=root.layer;
                    var health=root.GetComponent<PlaneHealth>()??root.AddComponent<PlaneHealth>();
                    var serialized=new SerializedObject(health);var property=serialized.FindProperty("explosionMaterial");
                    if(property.objectReferenceValue==null){property.objectReferenceValue=explosion;serialized.ApplyModifiedPropertiesWithoutUndo();}
                    BindReset(root.GetComponent<PlaneInput>(),health);
                });
                if(AssetDatabase.LoadAssetAtPath<GameObject>(RingPath)!=null)
                    EditPrefab(RingPath,root=>{foreach(var checkpoint in root.GetComponentsInChildren<Checkpoint>(true))LayerSetup.ConfigureCheckpoint(checkpoint);});
                foreach(var checkpoint in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Checkpoint>(true)))
                {
                    LayerSetup.ConfigureCheckpoint(checkpoint);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(checkpoint);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(checkpoint.gameObject);
                }
                foreach(var input in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<PlaneInput>(true)))
                {
                    if(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(input.gameObject)!=PlanePath)continue;
                    var health=input.GetComponent<PlaneHealth>();
                    if(health==null)throw new InvalidOperationException("PlaneHealth non propagé à l'instance de Plane.");
                    BindReset(input,health);PrefabUtility.RecordPrefabInstancePropertyModifications(input);
                }
                var gameplay=scene.GetRootGameObjects().FirstOrDefault(r=>r.name=="Gameplay");
                if(gameplay==null){gameplay=new GameObject("Gameplay");SceneManager.MoveGameObjectToScene(gameplay,scene);Undo.RegisterCreatedObjectUndo(gameplay,"Créer Gameplay");}
                var zones=gameplay.GetComponentsInChildren<RefuelZone>(true);
                if(zones.Length>1)throw new InvalidOperationException("Plusieurs RefuelZone présentes : conserver celle souhaitée.");
                if(zones.Length==0)
                {
                    if(gameplay.transform.Find("RefuelZone")!=null)throw new InvalidOperationException("RefuelZone existe sans composant : renommer pour préserver cet objet.");
                    var zone=new GameObject("RefuelZone",typeof(BoxCollider),typeof(RefuelZone));zone.transform.SetParent(gameplay.transform,false);
                    zone.layer=LayerMask.NameToLayer("Ground");Undo.RegisterCreatedObjectUndo(zone,"Créer RefuelZone");
                    Bounds bounds=apron.GetComponent<Renderer>().bounds;
                    float width=Mathf.Clamp(bounds.size.x*.5f,12f,30f),length=Mathf.Clamp(bounds.size.z*.5f,12f,30f);
                    zone.transform.position=new Vector3(bounds.center.x,bounds.max.y+2.5f,bounds.center.z);
                    var box=zone.GetComponent<BoxCollider>();box.size=new Vector3(width,5f,length);box.isTrigger=true;
                    var serialized=new SerializedObject(zone.GetComponent<RefuelZone>());serialized.FindProperty("groundMask").intValue=LayerMask.GetMask("Ground");serialized.ApplyModifiedPropertiesWithoutUndo();
                    var marker=MarkerMaterial();
                    Marker(zone.transform,"Left",new Vector3(-width/2,-2.47f,0),new Vector3(.25f,.03f,length),marker);
                    Marker(zone.transform,"Right",new Vector3(width/2,-2.47f,0),new Vector3(.25f,.03f,length),marker);
                    Marker(zone.transform,"Front",new Vector3(0,-2.47f,length/2),new Vector3(width,.03f,.25f),marker);
                    Marker(zone.transform,"Back",new Vector3(0,-2.47f,-length/2),new Vector3(width,.03f,.25f),marker);
                }
                AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene))throw new InvalidOperationException("Sauvegarde de Plane_Scene impossible.");
                Selection.activeGameObject=gameplay.GetComponentInChildren<RefuelZone>(true).gameObject;
                Debug.Log("[Sky Courier] Gameplay prêt : santé 100, dégâts dès 5 m/s de choc normal, crash dès 20 m/s, respawn 3 s via PlaneInput.ResetPlane (carburant conservé). RefuelZone : 10 L/s, vitesse <= 2 m/s, contact proche du sol. Paramètres et position existants de la zone conservés aux relances.");
            }
            catch(Exception exception){Debug.LogException(exception);}
        }
        private static void EditPrefab(string path,Action<GameObject> edit)
        {var root=PrefabUtility.LoadPrefabContents(path);try{edit(root);if(PrefabUtility.SaveAsPrefabAsset(root,path)==null)throw new InvalidOperationException("Sauvegarde impossible : "+path);}finally{PrefabUtility.UnloadPrefabContents(root);}}
        private static void BindReset(PlaneInput input,PlaneHealth health)
        {
            var field=typeof(PlaneInput).GetField("onReset",BindingFlags.Instance|BindingFlags.NonPublic);
            var reset=field?.GetValue(input) as UnityEvent;if(reset==null)throw new InvalidOperationException("onReset absent de PlaneInput.");
            bool found=false;
            for(int i=reset.GetPersistentEventCount()-1;i>=0;i--)
            {
                if(reset.GetPersistentTarget(i)!=health||reset.GetPersistentMethodName(i)!=nameof(PlaneHealth.HandleResetCompleted))continue;
                if(found)UnityEventTools.RemovePersistentListener(reset,i);else{found=true;reset.SetPersistentListenerState(i,UnityEventCallState.RuntimeOnly);}
            }
            if(!found)UnityEventTools.AddPersistentListener(reset,health.HandleResetCompleted);EditorUtility.SetDirty(input);
        }
        private static Material ExplosionMaterial()
        {
            const string path="Assets/_Plane/Materials/PlaneCrashParticles.mat";
            var existing=AssetDatabase.LoadAssetAtPath<Material>(path);if(existing!=null)return existing;
            var shader=Shader.Find("Universal Render Pipeline/Particles/Unlit");if(shader==null)throw new InvalidOperationException("Shader URP Particles/Unlit absent.");
            var texture=new Texture2D(32,32,TextureFormat.RGBA32,false){name="CrashGlow",wrapMode=TextureWrapMode.Clamp};
            for(int y=0;y<32;y++)for(int x=0;x<32;x++)
            {float radius=new Vector2((x-15.5f)/15.5f,(y-15.5f)/15.5f).magnitude;texture.SetPixel(x,y,new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-radius),1.5f)));}
            texture.Apply();System.IO.Directory.CreateDirectory("Assets/_Plane/VFX");
            const string texturePath="Assets/_Plane/VFX/CrashGlow.asset";
            var saved=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if(saved==null){AssetDatabase.CreateAsset(texture,texturePath);saved=texture;}else UnityEngine.Object.DestroyImmediate(texture);
            var material=new Material(shader){name="PlaneCrashParticles",renderQueue=(int)RenderQueue.Transparent};
            material.SetTexture("_BaseMap",saved);material.SetFloat("_Surface",1);material.SetFloat("_Blend",0);material.SetFloat("_ZWrite",0);
            material.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);material.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.SetOverrideTag("RenderType","Transparent");
            AssetDatabase.CreateAsset(material,path);return material;
        }
        private static Material MarkerMaterial()
        {
            const string path="Assets/_Plane/Materials/RefuelMarker.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(material!=null)return material;
            var shader=Shader.Find("Universal Render Pipeline/Unlit");if(shader==null)throw new InvalidOperationException("Shader URP Unlit absent.");
            material=new Material(shader){name="RefuelMarker"};material.SetColor("_BaseColor",new Color(.05f,.8f,.85f));AssetDatabase.CreateAsset(material,path);return material;
        }
        private static void Marker(Transform parent,string name,Vector3 position,Vector3 scale,Material material)
        {
            var marker=GameObject.CreatePrimitive(PrimitiveType.Cube);marker.name=name;marker.transform.SetParent(parent,false);
            marker.transform.localPosition=position;marker.transform.localScale=scale;marker.layer=2;marker.isStatic=true;
            UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());marker.GetComponent<Renderer>().sharedMaterial=material;
        }
    }
}
#endif
