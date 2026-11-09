#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Plane
{
    /// <summary>Montagnes statiques, semées et à faces plates ; ne remplace que Environment/Mountains.</summary>
    public static class MountainBuilder
    {
        [MenuItem("Sky Courier/Environment/Build Mountains")]
        public static void Build()
        {
            var scene=ModuleSceneUtility.Open();var settings=ModuleSceneUtility.Settings();
            var course=ModuleSceneUtility.Only<CheckpointManager>(scene);var ceiling=ModuleSceneUtility.Only<FlightCeiling>(scene);
            int ground=LayerMask.NameToLayer("Ground");if(ground<0)throw new InvalidOperationException("Ground absent : aucun layer créé automatiquement.");
            var rings=course.Checkpoints.Select(c=>c.transform.position).ToArray();
            var runway=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t=>t.name=="Runway_A");
            Vector3 airport=runway!=null?runway.position:Vector3.zero;
            var refuels=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<RefuelZone>(true)).Select(r=>r.transform.position).ToArray();
            var arrivals=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<LandingZone>(true)).Where(z=>z.Runway!=null).ToArray();
            var random=new System.Random(settings.Seed);var placements=new List<Vector3>();var radii=new List<float>();var heights=new List<float>();
            float peakLimit=Mathf.Min(settings.MaximumPeak,ceiling.WarningAltitude-40f);
            if(peakLimit<80f)throw new InvalidOperationException("Plafond trop bas pour des montagnes sûres.");
            // Prépare et vérifie les positions avant de remplacer notre propre racine.
            for(int i=0;i<settings.MountainCount;i++)
            {
                bool found=false;
                for(int attempt=0;attempt<160;attempt++)
                {
                    float angle=(i/(float)settings.MountainCount*Mathf.PI*2f)+(float)(random.NextDouble()-.5)*.45f;
                    float distance=i%4==0?Range(random,1100,2400):Range(random,settings.InnerRadius,settings.OuterRadius);
                    float radius=Range(random,settings.MinimumBaseRadius,settings.MaximumBaseRadius);
                    var position=ceiling.MapCenter+new Vector3(Mathf.Sin(angle)*distance,0,Mathf.Cos(angle)*distance);position.y=-.5f;
                    float clearance=radius*1.2f+settings.FlightPathClearance;
                    if(distance+radius*1.2f>Mathf.Min(3900f,ceiling.HorizontalRadius-60f))continue;
                    if(FlatDistance(position,airport)<radius*1.2f+850f)continue;
                    if(refuels.Any(p=>FlatDistance(position,p)<radius*1.2f+200f))continue;
                    // Préserve aussi la piste finale et sa trajectoire d'approche.
                    if(arrivals.Any(z=>ArrivalBlocked(position,radius,z,rings.Last())))continue;
                    if(rings.Any(p=>FlatDistance(position,p)<clearance))continue;
                    bool pathBlocked=false;
                    for(int k=1;k<rings.Length;k++)if(SegmentDistance(position,rings[k-1],rings[k])<clearance){pathBlocked=true;break;}
                    if(pathBlocked)continue;
                    if(placements.Where((p,j)=>FlatDistance(position,p)<(radius+radii[j])*.75f).Any())continue;
                    placements.Add(position);radii.Add(radius);heights.Add(Range(random,Mathf.Min(settings.MinimumPeak,peakLimit),peakLimit));found=true;break;
                }
                if(!found)Debug.LogWarning("[Sky Courier] Emplacement montagne ignoré pour préserver la piste et le parcours : "+i);
            }
            ModuleSceneUtility.Folder("Assets/_Plane/Models/Procedural/Mountains");
            var material=ModuleSceneUtility.Material("Assets/_Plane/Materials/Mountain_Altitude.mat","Sky Courier/Mountain Vertex Color",Color.white);
            var environment=ModuleSceneUtility.Root(scene,"Environment");var old=environment.Find("Mountains");
            if(old!=null)Undo.DestroyObjectImmediate(old.gameObject);
            var root=new GameObject("Mountains");root.transform.SetParent(environment,false);Undo.RegisterCreatedObjectUndo(root,"Créer montagnes");
            if(scene.GetRootGameObjects().Any(r=>r.GetComponentInChildren<LandingMission>(true)!=null))root.AddComponent<MountainHazard>();
            for(int i=0;i<placements.Count;i++)
            {
                var mesh=ModuleSceneUtility.SaveMesh($"Assets/_Plane/Models/Procedural/Mountains/Mountain_{i+1:00}.asset",CreateMesh(radii[i],heights[i],settings.Seed+i*7919));
                var mountain=new GameObject($"Mountain_{i+1:00}",typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));
                mountain.transform.SetParent(root.transform,false);mountain.transform.position=placements[i];mountain.layer=ground;mountain.isStatic=true;
                mountain.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=mountain.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                mountain.GetComponent<MeshCollider>().sharedMesh=mesh;
                GameObjectUtility.SetStaticEditorFlags(mountain,StaticEditorFlags.BatchingStatic|StaticEditorFlags.OccluderStatic|StaticEditorFlags.OccludeeStatic);
            }
            ModuleSceneUtility.Save(scene);
            Debug.Log($"[Sky Courier] {placements.Count} montagnes × 168 triangles, 1 matériau, collisions statiques basse résolution ; sommets ≤ {peakLimit:0} m, parcours dégagé.");
        }
        private static float Range(System.Random random,float min,float max)=>(float)(min+random.NextDouble()*(max-min));
        private static float FlatDistance(Vector3 a,Vector3 b)=>new Vector2(a.x-b.x,a.z-b.z).magnitude;
        private static bool ArrivalBlocked(Vector3 position,float radius,LandingZone zone,Vector3 lastRing)
        {
            var bounds=zone.Runway.bounds;var flat=new Vector3(position.x,bounds.center.y,position.z);
            float gap=Vector3.Distance(flat,bounds.ClosestPoint(flat));
            return gap<radius*1.2f+180f||SegmentDistance(position,lastRing,bounds.center)<radius*1.2f+250f;
        }
        public static float SegmentDistance(Vector3 p,Vector3 a,Vector3 b)
        {
            p.y=a.y=b.y=0f;var delta=b-a;
            float t=delta.sqrMagnitude>.001f?Mathf.Clamp01(Vector3.Dot(p-a,delta)/delta.sqrMagnitude):0f;
            return Vector3.Distance(p,a+delta*t);
        }
        internal static Mesh CreateMesh(float radius,float height,int seed)
        {
            const int sides=12,levels=7;var random=new System.Random(seed);var rings=new Vector3[levels,sides];
            var vertices=new List<Vector3>();var colors=new List<Color>();
            for(int level=0;level<levels;level++)
            {
                float t=level/(float)levels;
                for(int side=0;side<sides;side++)
                {
                    float angle=side*Mathf.PI*2/sides;
                    float r=radius*Mathf.Pow(1f-t,1.25f)*Range(random,.85f,1.12f);
                    rings[level,side]=new Vector3(Mathf.Cos(angle)*r,height*t,Mathf.Sin(angle)*r);
                }
            }
            Action<Vector3,Vector3,Vector3> face=(a,b,c)=>
            {
                // Sommets dupliqués : normales indépendantes et couleurs constantes par face.
                float altitude=(a.y+b.y+c.y)/(3f*height);
                Color color=altitude<.45f?Color.Lerp(new Color(.22f,.31f,.2f),new Color(.34f,.33f,.29f),altitude/.45f):
                    Color.Lerp(new Color(.34f,.33f,.29f),new Color(.82f,.87f,.89f),Mathf.InverseLerp(.65f,.94f,altitude));
                vertices.Add(a);vertices.Add(b);vertices.Add(c);colors.Add(color);colors.Add(color);colors.Add(color);
            };
            for(int side=0;side<sides;side++)
            {
                int next=(side+1)%sides;
                for(int level=0;level<levels-1;level++)
                {
                    face(rings[level,side],rings[level+1,side],rings[level,next]);
                    face(rings[level,next],rings[level+1,side],rings[level+1,next]);
                }
                face(rings[levels-1,side],Vector3.up*height,rings[levels-1,next]);
                face(Vector3.zero,rings[0,side],rings[0,next]);
            }
            var mesh=new Mesh{name="Mountain_168Triangles"};mesh.SetVertices(vertices);mesh.SetColors(colors);
            mesh.SetTriangles(Enumerable.Range(0,vertices.Count).ToArray(),0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}
#endif
