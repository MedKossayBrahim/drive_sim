#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace Project.Plane
{
    /// <summary>Conserve les layers existants et les collisions des autres véhicules ; isole seulement Checkpoint.</summary>
    public static class LayerSetup
    {
        public static void Configure()
        {
            var assets=AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            var serialized=new SerializedObject(assets[0]);var layers=serialized.FindProperty("layers");
            foreach(string name in new[]{"Plane","Ground","Checkpoint","Cargo"})
            {
                bool found=false;
                for(int i=0;i<layers.arraySize;i++) if(layers.GetArrayElementAtIndex(i).stringValue==name){found=true;break;}
                if(found)continue;
                int free=-1;
                for(int i=8;i<layers.arraySize;i++) if(string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)){free=i;break;}
                if(free<0)throw new InvalidOperationException("Aucun layer utilisateur libre pour "+name);
                layers.GetArrayElementAtIndex(free).stringValue=name;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.SaveAssets();
            int plane=LayerMask.NameToLayer("Plane"),ground=LayerMask.NameToLayer("Ground"),checkpoint=LayerMask.NameToLayer("Checkpoint"),cargo=LayerMask.NameToLayer("Cargo");
            if(plane<0||ground<0||checkpoint<0||cargo<0)throw new InvalidOperationException("Création des layers impossible.");
            for(int i=0;i<32;i++)Physics.IgnoreLayerCollision(checkpoint,i,i!=plane);
            Physics.IgnoreLayerCollision(plane,ground,false);
            Physics.IgnoreLayerCollision(plane,cargo,false);
            Physics.IgnoreLayerCollision(cargo,ground,false);
            // Persistance explicite de la matrice dans les réglages Physics, sans toucher aux autres valeurs.
            var physics=AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/DynamicsManager.asset");
            if(physics.Length==0)throw new InvalidOperationException("Réglages Physics introuvables.");
            foreach(var settings in physics)EditorUtility.SetDirty(settings);
            InternalEditorUtility.SaveToSerializedFileAndForget(physics,"ProjectSettings/DynamicsManager.asset",true);
            Debug.Log($"[Sky Courier] Layers Plane={plane}, Ground={ground}, Checkpoint={checkpoint}, Cargo={cargo}. Checkpoint ne rencontre que Plane ; autres paires conservées, Plane/Ground/Cargo autorisées.");
        }
        public static void ConfigureCheckpoint(Checkpoint checkpoint)
        {
            checkpoint.gameObject.layer=LayerMask.NameToLayer("Checkpoint");
            var serialized=new SerializedObject(checkpoint);
            serialized.FindProperty("acceptedLayers").intValue=LayerMask.GetMask("Plane");serialized.ApplyModifiedPropertiesWithoutUndo();
            foreach(var collider in checkpoint.GetComponents<Collider>())
            { collider.isTrigger=true; EditorUtility.SetDirty(collider); }
            EditorUtility.SetDirty(checkpoint.gameObject);EditorUtility.SetDirty(checkpoint);
        }
    }
}
#endif
