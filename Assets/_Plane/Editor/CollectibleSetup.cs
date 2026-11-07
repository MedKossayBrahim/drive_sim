#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Plane
{
    /// <summary>Trajectoires de pièces déterministes, limitées à Gameplay/Collectibles.</summary>
    public static class CollectibleSetup
    {
        [MenuItem("Sky Courier/Gameplay/Build Collectibles")]
        public static void Build()
        {
            var scene = ModuleSceneUtility.Open();
            var score = ModuleSceneUtility.Only<ScoreManager>(scene);
            var course = ModuleSceneUtility.Only<CheckpointManager>(scene);
            var input = ModuleSceneUtility.Only<PlaneInput>(scene);
            if (LayerMask.NameToLayer("Plane") < 0) throw new InvalidOperationException("Layer Plane absent : génération annulée sans changer TagManager.");
            var settings = ModuleSceneUtility.Settings();
            var prefab = CreatePrefab(settings);
            var points = new List<Vector3>(); var rings = course.Checkpoints;
            var random = new System.Random(settings.Seed);
            for (int i = 0; i < rings.Count; i++)
            {
                if (rings[i] == null) throw new InvalidOperationException("Anneau absent du parcours.");
                Transform ring = rings[i].transform;
                for (int j = 0; j < settings.BonusCoinsPerRing; j++)
                    points.Add(ring.position - ring.forward * 18f + ring.right * ((j - (settings.BonusCoinsPerRing-1)*.5f) * 5.5f));
                if (i + 1 >= rings.Count) continue;
                Vector3 a = ring.position, b = rings[i+1].transform.position;
                Vector3 side = Vector3.Cross(Vector3.up, (b-a).normalized).normalized;
                float arc = (float)(random.NextDouble()*20f-10f);
                int steps = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(a,b)/settings.CoinSpacing));
                for (int j = 1; j < steps; j++)
                {
                    float t = j/(float)steps;
                    points.Add(Vector3.Lerp(a,b,t) + side * (Mathf.Sin(t*Mathf.PI)*arc));
                }
            }
            var gameplay = ModuleSceneUtility.Root(scene, "Gameplay");
            var old = gameplay.Find("Collectibles");
            if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var root = new GameObject("Collectibles"); root.transform.SetParent(gameplay, false);
            Undo.RegisterCreatedObjectUndo(root, "Créer les pièces");
            var instances = new Collectible[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject, root.transform);
                instance.name = $"Coin_{i+1:000}"; instance.transform.position = points[i];
                instances[i] = instance.GetComponent<Collectible>();
                instances[i].Configure(instance.transform.Find("Visual"), settings.CoinValue, i*2.39996f);
                instances[i].Bind(score,input,input.GetComponent<PlaneHealth>());
                PrefabUtility.RecordPrefabInstancePropertyModifications(instances[i]);
                points[i] = root.transform.InverseTransformPoint(points[i]);
            }
            root.AddComponent<CollectibleSpawner>().Configure(prefab,points.ToArray(),instances,score,input,input.GetComponent<PlaneHealth>());
            ModuleSceneUtility.Save(scene);
            Debug.Log($"[Sky Courier] {points.Count} pièces générées. Collider overrides : Plane uniquement, aucune matrice globale modifiée.");
        }
        private static Collectible CreatePrefab(WorldModuleSettings settings)
        {
            ModuleSceneUtility.Folder("Assets/_Plane/Models/Procedural");
            ModuleSceneUtility.Folder("Assets/_Plane/Prefabs/Gameplay");
            var mesh = ModuleSceneUtility.SaveMesh("Assets/_Plane/Models/Procedural/Coin.asset", CoinMesh(settings.CoinVisualRadius));
            var material = ModuleSceneUtility.Material("Assets/_Plane/Materials/Coin_Gold.mat", "Universal Render Pipeline/Lit", new Color(1f,.67f,.055f));
            var root = new GameObject("Collectible_Coin");
            try
            {
                var sphere=root.AddComponent<SphereCollider>();sphere.radius=Mathf.Max(settings.PickupRadius,settings.CoinVisualRadius+1f);Collectible.RestrictToPlane(sphere);
                var visual=new GameObject("Visual",typeof(MeshFilter),typeof(MeshRenderer));visual.transform.SetParent(root.transform,false);
                visual.GetComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=visual.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
                renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
                root.AddComponent<Collectible>().Configure(visual.transform,settings.CoinValue,0f);
                return PrefabUtility.SaveAsPrefabAsset(root,"Assets/_Plane/Prefabs/Gameplay/Collectible_Coin.prefab").GetComponent<Collectible>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private static Mesh CoinMesh(float radius)
        {
            const int n=12;var vertices=new Vector3[2+n*2];var triangles=new List<int>();
            vertices[0]=Vector3.forward*.2f;vertices[1]=Vector3.back*.2f;
            for(int i=0;i<n;i++)
            {
                float angle=i*Mathf.PI*2/n;var point=new Vector3(Mathf.Cos(angle)*radius,Mathf.Sin(angle)*radius,0f);
                vertices[2+i]=point+Vector3.forward*.2f;vertices[2+n+i]=point+Vector3.back*.2f;
            }
            for(int i=0;i<n;i++)
            {
                int a=2+i,b=2+(i+1)%n,c=a+n,d=b+n;
                triangles.AddRange(new[]{0,a,b,1,d,c,a,c,b,b,c,d});
            }
            var mesh=new Mesh{name="Coin_48Triangles"};mesh.vertices=vertices;mesh.triangles=triangles.ToArray();mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
        }
    }
}
#endif
