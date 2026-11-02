#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Project.Plane
{
    /// <summary>Scénario collecte/atterrissage ; construit ses seules racines et dégage le relief existant de l'arrivée.</summary>
    public static class FinalFlightMissionBuilder
    {
        [MenuItem("Sky Courier/Scenario/Build Final Flight Mission")]
        public static void Build()
        {
            var scene=ModuleSceneUtility.Open();SceneManager.SetActiveScene(scene);
            var input=ModuleSceneUtility.Only<PlaneInput>(scene);var ceiling=input.GetComponent<FlightCeiling>();
            var score=ModuleSceneUtility.Only<ScoreManager>(scene);var course=ModuleSceneUtility.Only<CheckpointManager>(scene);
            var coins=ModuleSceneUtility.Only<CollectibleSpawner>(scene);var sky=ModuleSceneUtility.Only<SkyboxSwitcher>(scene);
            var config=Settings();int ground=LayerMask.NameToLayer("Ground");
            if(ground<0)throw new InvalidOperationException("Ground absent ; aucun réglage global modifié.");
            if(config.RequiredCoins>coins.PlannedCount)throw new InvalidOperationException("L'objectif dépasse le nombre de pièces disponibles.");
            if(config.BoundaryRidgeRadius<=config.BoundaryFootRadius||config.BoundaryOuterRadius<=config.BoundaryRidgeRadius+150f)
                throw new InvalidOperationException("Les rayons du massif doivent augmenter de l'intérieur vers l'extérieur.");
            if(config.BoundaryFootRadius<=config.ArrivalCenter.z+config.RunwayLength*.5f+80f)
                throw new InvalidOperationException("La montagne doit commencer après la piste, avec une marge de sécurité.");
            var gameplay=ModuleSceneUtility.Root(scene,"Gameplay");var environment=ModuleSceneUtility.Root(scene,"Environment");
            Delete(gameplay,"FinalMission");Delete(environment,"ArrivalAirport");Delete(environment,"MountainBoundary");
            var root=new GameObject("FinalMission");root.transform.SetParent(gameplay,false);Undo.RegisterCreatedObjectUndo(root,"Créer scénario final");
            var mission=root.AddComponent<LandingMission>();
            if(input.GetComponent<PlaneLandingContact>()==null)Undo.AddComponent<PlaneLandingContact>(input.gameObject);
            var airport=CreateAirport(environment,config,ground);
            var runway=airport.transform.Find("Runway_Arrival").GetComponent<BoxCollider>();
            var zone=airport.AddComponent<LandingZone>();zone.Configure(config,runway,mission,input);
            mission.Configure(config,score,input,course,coins,zone);
            var mountainContact=input.GetComponent<PlaneMountainCollision>()??Undo.AddComponent<PlaneMountainCollision>(input.gameObject);
            mountainContact.Configure(mission);EditorUtility.SetDirty(mountainContact);
            PrefabUtility.RecordPrefabInstancePropertyModifications(zone);
            CreateBoundary(environment,config,ceiling,ground);
            MountainBuilder.Build(); // Préserve maintenant la piste et son approche grâce à LandingZone.
            CreateHUD(root.transform,mission);
            var hud=ModuleSceneUtility.Only<PlaneHUD>(scene);var serialized=new SerializedObject(hud);
            serialized.FindProperty("landingMission").objectReferenceValue=mission;serialized.ApplyModifiedPropertiesWithoutUndo();
            serialized=new SerializedObject(sky);serialized.FindProperty("fogDensityMultiplier").floatValue=.1f;
            serialized.FindProperty("maximumFogDensity").floatValue=.0006f;
            string presetName=new[]{"noon","sunset","night","storm"}[serialized.FindProperty("selectedPreset").enumValueIndex];
            var preset=serialized.FindProperty(presetName).objectReferenceValue as SkyPreset;serialized.ApplyModifiedPropertiesWithoutUndo();
            if(preset!=null){RenderSettings.fogDensity=Mathf.Min(.0006f,preset.FogDensity*.1f);RenderSettings.fogColor=preset.FogColor;RenderSettings.fogMode=FogMode.ExponentialSquared;}
            // Repartir sur la vraie piste de départ ; ne modifie ni le prefab de l'avion ni SampleScene.
            var start=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<BoxCollider>(true)).First(c=>c.name=="Runway_A");
            Undo.RecordObject(input.transform,"Départ sur la piste");
            input.transform.SetPositionAndRotation(new Vector3(start.bounds.center.x,start.bounds.max.y+1.8f,start.bounds.min.z+70f),Quaternion.LookRotation(start.transform.forward));
            PrefabUtility.RecordPrefabInstancePropertyModifications(input.transform);
            ModuleSceneUtility.Save(scene);
            Debug.Log($"[Sky Courier] Mission prête : {config.RequiredCoins} pièces, piste finale {config.RunwayLength:0} m, arrêt 2 s, bonus {config.LandingBonus}. Ceinture solide au-dessus de {ceiling.CeilingAltitude:0} m ; impact = échec terminal. Aucun réglage global modifié.");
        }
        public static FlightMissionSettings Settings()
        {
            const string path="Assets/_Plane/Prefabs/FlightMissionSettings.asset";
            var asset=AssetDatabase.LoadAssetAtPath<FlightMissionSettings>(path);
            if(asset==null){asset=ScriptableObject.CreateInstance<FlightMissionSettings>();AssetDatabase.CreateAsset(asset,path);}return asset;
        }
        private static void Delete(Transform parent,string name)
        {var old=parent.Find(name);if(old!=null)Undo.DestroyObjectImmediate(old.gameObject);}
        private static GameObject CreateAirport(Transform parent,FlightMissionSettings config,int ground)
        {
            var asphalt=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Plane/Materials/Airport/Asphalt.mat");
            var white=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Plane/Materials/Airport/Markings_White.mat");
            var lights=ModuleSceneUtility.Material("Assets/_Plane/Materials/Arrival_Lights.mat","Universal Render Pipeline/Lit",new Color(.1f,.5f,.4f));
            lights.EnableKeyword("_EMISSION");lights.SetColor("_EmissionColor",new Color(.15f,1f,.65f)*2f);EditorUtility.SetDirty(lights);
            var root=new GameObject("ArrivalAirport");
            try
            {
                float length=config.RunwayLength,width=config.RunwayWidth;
                Cube("Runway_Arrival",root.transform,new Vector3(0,-.25f,0),new Vector3(width,.5f,length),asphalt,ground,true);
                Cube("Apron_Arrival",root.transform,new Vector3(-100,-.25f,140),new Vector3(140,.5f,170),asphalt,ground,true);
                Cube("Taxiway_Arrival",root.transform,new Vector3(-width*.5f-25f,-.25f,140),new Vector3(50,.5f,20),asphalt,ground,true);
                for(float z=-length*.5f+55;z<length*.5f-40;z+=45)
                    Cube("CenterDash_"+Mathf.RoundToInt(z),root.transform,new Vector3(0,.015f,z),new Vector3(1.2f,.02f,20),white,ground,false);
                foreach(int side in new[]{-1,1})
                {
                    Cube("EdgeMark_"+side,root.transform,new Vector3(side*(width*.5f-2),.015f,0),new Vector3(.5f,.02f,length-10),white,ground,false);
                    Cube("EdgeLights_"+side,root.transform,new Vector3(side*(width*.5f+1),.1f,0),new Vector3(.35f,.2f,length),lights,ground,false);
                    for(int stripe=0;stripe<4;stripe++)
                        Cube("Threshold_"+side+"_"+stripe,root.transform,new Vector3((stripe-1.5f)*8f,.015f,side*(length*.5f-30)),new Vector3(3,.02f,22),white,ground,false);
                }
                Building(root.transform,"hangar",new Vector3(-175,0,150),25f,false,ground);
                Building(root.transform,"control_tower",new Vector3(90,0,220),20f,true,ground);
                var prefab=PrefabUtility.SaveAsPrefabAsset(root,"Assets/_Plane/Prefabs/Airport/ArrivalAirport.prefab");
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);instance.name="ArrivalAirport";instance.transform.position=config.ArrivalCenter;
                return instance;
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
        }
        private static GameObject Cube(string name,Transform parent,Vector3 position,Vector3 scale,Material material,int layer,bool solid)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;obj.transform.SetParent(parent,false);
            obj.transform.localPosition=position;obj.transform.localScale=scale;obj.layer=layer;obj.isStatic=true;
            obj.GetComponent<Renderer>().sharedMaterial=material;
            if(!solid)UnityEngine.Object.DestroyImmediate(obj.GetComponent<BoxCollider>());
            return obj;
        }
        private static void Building(Transform parent,string name,Vector3 position,float size,bool height,int layer)
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Plane/Prefabs/Imported/Airport/"+name+".prefab");if(source==null)return;
            var model=(GameObject)PrefabUtility.InstantiatePrefab(source,parent);model.name=name;
            var renderers=model.GetComponentsInChildren<Renderer>(true);if(renderers.Length==0)return;
            var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            model.transform.localScale*=size/Mathf.Max(.01f,height?bounds.size.y:bounds.size.x);
            model.transform.localPosition=position;bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            model.transform.position+=Vector3.up*(parent.position.y-bounds.min.y);
            bounds=renderers[0].bounds;foreach(var r in renderers){r.gameObject.isStatic=true;bounds.Encapsulate(r.bounds);}
            var proxy=new GameObject(name+"_Collider");proxy.transform.SetParent(parent,false);proxy.transform.position=bounds.center;
            proxy.AddComponent<BoxCollider>().size=bounds.size;proxy.layer=layer;proxy.isStatic=true;
        }
        private static void CreateBoundary(Transform parent,FlightMissionSettings config,FlightCeiling ceiling,int layer)
        {
            const int n=48;var rows=new Vector3[4,n];var random=new System.Random(config.MountainSeed);
            float minimum=Mathf.Max(config.MinimumRidgeHeight,ceiling.CeilingAltitude+200f);
            for(int i=0;i<n;i++)
            {
                float angle=i*Mathf.PI*2/n;var radial=new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle));
                float height=minimum+(float)random.NextDouble()*config.PeakVariation;
                rows[0,i]=radial*config.BoundaryFootRadius+Vector3.down*.5f;
                rows[1,i]=radial*config.BoundaryRidgeRadius+Vector3.up*height;
                rows[2,i]=radial*(config.BoundaryRidgeRadius+150f)+Vector3.up*(height+30f);
                rows[3,i]=radial*config.BoundaryOuterRadius+Vector3.down*.5f;
            }
            var vertices=new List<Vector3>();var colors=new List<Color>();
            Action<Vector3,Vector3,Vector3> face=(a,b,c)=>
            {
                float altitude=(a.y+b.y+c.y)/3f;
                Color color=Color.Lerp(new Color(.25f,.32f,.22f),new Color(.8f,.85f,.88f),Mathf.InverseLerp(100f,minimum*.85f,altitude));
                // Faces tournées vers l'intérieur sur la pente interne : visibles et solides depuis la vallée.
                vertices.Add(a);vertices.Add(c);vertices.Add(b);colors.Add(color);colors.Add(color);colors.Add(color);
            };
            for(int i=0;i<n;i++)
            {
                int j=(i+1)%n;
                for(int band=0;band<3;band++)
                {
                    face(rows[band,i],rows[band,j],rows[band+1,i]);
                    face(rows[band,j],rows[band+1,j],rows[band+1,i]);
                }
                face(rows[0,i],rows[3,i],rows[0,j]);face(rows[0,j],rows[3,i],rows[3,j]);
            }
            var mesh=new Mesh{name="MountainBoundary_384Triangles"};mesh.SetVertices(vertices);mesh.SetColors(colors);
            mesh.SetTriangles(Enumerable.Range(0,vertices.Count).ToArray(),0);mesh.RecalculateNormals();mesh.RecalculateBounds();
            ModuleSceneUtility.Folder("Assets/_Plane/Models/Procedural/FinalMission");
            mesh=ModuleSceneUtility.SaveMesh("Assets/_Plane/Models/Procedural/FinalMission/MountainBoundary.asset",mesh);
            var collisionMesh=ModuleSceneUtility.SaveMesh("Assets/_Plane/Models/Procedural/FinalMission/MountainBoundary_Collision.asset",CreateCollisionMesh(mesh));
            var root=new GameObject("MountainBoundary",typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));
            root.transform.SetParent(parent,false);root.transform.position=new Vector3(ceiling.MapCenter.x,0f,ceiling.MapCenter.z);root.layer=layer;root.isStatic=true;
            root.GetComponent<MeshFilter>().sharedMesh=mesh;root.GetComponent<MeshCollider>().sharedMesh=collisionMesh;
            root.AddComponent<MountainHazard>();
            var renderer=root.GetComponent<MeshRenderer>();renderer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Plane/Materials/Mountain_Altitude.mat");
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            GameObjectUtility.SetStaticEditorFlags(root,StaticEditorFlags.BatchingStatic|StaticEditorFlags.OccluderStatic|StaticEditorFlags.OccludeeStatic);
        }
        private static Mesh CreateCollisionMesh(Mesh visual)
        {
            var positions=visual.vertices;var indices=visual.triangles;float longest=0f;
            for(int i=0;i<indices.Length;i+=3)
            {
                Vector3 a=positions[indices[i]],b=positions[indices[i+1]],c=positions[indices[i+2]];
                longest=Mathf.Max(longest,Vector3.Distance(a,b),Vector3.Distance(b,c),Vector3.Distance(c,a));
            }
            int levels=0;
            while(longest>400f){levels++;longest*=.5f;if(levels>4)throw new InvalidOperationException("Relief trop grand pour le budget de collision.");}
            var vertices=new List<Vector3>();
            for(int i=0;i<indices.Length;i+=3)Subdivide(positions[indices[i]],positions[indices[i+1]],positions[indices[i+2]],levels,vertices);
            var mesh=new Mesh{name="MountainBoundary_Collision"};mesh.indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16;mesh.SetVertices(vertices);
            mesh.SetTriangles(Enumerable.Range(0,vertices.Count).ToArray(),0);mesh.RecalculateBounds();
            return mesh;
        }
        private static void Subdivide(Vector3 a,Vector3 b,Vector3 c,int levels,List<Vector3> output)
        {
            if(levels==0){output.Add(a);output.Add(b);output.Add(c);return;}
            // Même subdivision sur toutes les faces : conserve une surface continue sans grands triangles PhysX.
            Vector3 ab=(a+b)*.5f,bc=(b+c)*.5f,ca=(c+a)*.5f;
            Subdivide(a,ab,ca,levels-1,output);Subdivide(ab,b,bc,levels-1,output);
            Subdivide(ca,bc,c,levels-1,output);Subdivide(ab,bc,ca,levels-1,output);
        }
        private static void CreateHUD(Transform parent,LandingMission mission)
        {
            var canvas=Rect("MissionCanvas",parent);canvas.gameObject.AddComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;canvas.GetComponent<Canvas>().sortingOrder=70;
            var scaler=canvas.gameObject.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
            var view=canvas.gameObject.AddComponent<LandingMissionHUD>();
            var safe=Rect("SafeArea",canvas);Stretch(safe);safe.gameObject.AddComponent<PlaneHUDSafeArea>();
            var objective=Rect("Objective",safe);objective.anchorMin=objective.anchorMax=new Vector2(0,1);objective.pivot=new Vector2(.5f,1);
            var background=objective.gameObject.AddComponent<Image>();background.color=new Color(.02f,.06f,.08f,.85f);background.raycastTarget=false;
            var label=Label(objective,"ObjectiveText","COLLECTEZ PUIS ATTERRISSEZ",25);Stretch(label.rectTransform,8);
            var result=Rect("Result",canvas);Stretch(result);
            var resultCanvas=result.gameObject.AddComponent<Canvas>();resultCanvas.overrideSorting=true;resultCanvas.sortingOrder=500;result.gameObject.AddComponent<GraphicRaycaster>();
            var veil=result.gameObject.AddComponent<Image>();veil.color=new Color(.01f,.025f,.04f,.94f);veil.raycastTarget=true;
            var panel=Rect("Panel",result);panel.anchorMin=new Vector2(.18f,.18f);panel.anchorMax=new Vector2(.82f,.82f);panel.offsetMin=panel.offsetMax=Vector2.zero;
            var layout=panel.gameObject.AddComponent<VerticalLayoutGroup>();layout.spacing=24;layout.childControlWidth=layout.childControlHeight=true;layout.childForceExpandWidth=true;layout.childForceExpandHeight=false;
            var title=Label(panel,"Title","MISSION",48);title.GetComponent<LayoutElement>().preferredHeight=100;
            var details=Label(panel,"Details","",30);details.GetComponent<LayoutElement>().preferredHeight=160;
            Button(panel,"Restart","RECOMMENCER",view.Restart);Button(panel,"Quit","QUITTER",view.Quit);
            view.Configure(mission,safe,objective,label,result.gameObject,title,details);result.gameObject.SetActive(false);
        }
        private static RectTransform Rect(string name,Transform parent)
        {var rect=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rect.SetParent(parent,false);return rect;}
        private static void Stretch(RectTransform rect,float padding=0f)
        {rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=Vector2.one*padding;rect.offsetMax=-Vector2.one*padding;}
        private static TMP_Text Label(Transform parent,string name,string value,int size)
        {
            var rect=Rect(name,parent);var label=rect.gameObject.AddComponent<TextMeshProUGUI>();label.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Plane/UI/TextMeshPro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            label.text=value;label.fontSize=size;label.fontSizeMax=size;label.fontSizeMin=18;label.enableAutoSizing=true;label.alignment=TextAlignmentOptions.Center;label.color=new Color(.18f,.9f,.85f);label.raycastTarget=false;
            var element=rect.gameObject.AddComponent<LayoutElement>();element.minHeight=48;element.preferredHeight=64;return label;
        }
        private static void Button(Transform parent,string name,string value,UnityAction action)
        {
            var rect=Rect(name,parent);var image=rect.gameObject.AddComponent<Image>();image.color=new Color(.06f,.22f,.25f);
            var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=image;UnityEventTools.AddPersistentListener(button.onClick,action);
            rect.gameObject.AddComponent<LayoutElement>().preferredHeight=96;var text=Label(rect,"Label",value,32);Stretch(text.rectTransform);
        }
    }
}
#endif
