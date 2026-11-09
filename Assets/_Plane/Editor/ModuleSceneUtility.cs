#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Project.Plane
{
    /// <summary>Outils locaux aux nouveaux modules : aucun réglage global n'est écrit.</summary>
    public static class ModuleSceneUtility
    {
        public const string ScenePath = "Assets/_Plane/Scenes/Plane_Scene.unity";
        public static Scene Open()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Quitter Play avant la génération.");
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            return scene;
        }
        public static T Only<T>(Scene scene) where T : Component
        {
            var found = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
            if (found.Length != 1) throw new InvalidOperationException("Un seul " + typeof(T).Name + " est requis dans " + ScenePath);
            return found[0];
        }
        public static Transform Root(Scene scene, string name)
        {
            var matches = scene.GetRootGameObjects().Where(r => r.name == name).ToArray();
            if (matches.Length > 1) throw new InvalidOperationException("Racine dupliquée : " + name);
            if (matches.Length == 1) return matches[0].transform;
            var root = new GameObject(name); SceneManager.MoveGameObjectToScene(root, scene);
            Undo.RegisterCreatedObjectUndo(root, "Créer " + name); return root.transform;
        }
        public static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/'); Folder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
        public static WorldModuleSettings Settings()
        {
            const string path = "Assets/_Plane/Prefabs/WorldModuleSettings.asset";
            var settings = AssetDatabase.LoadAssetAtPath<WorldModuleSettings>(path);
            if (settings == null) { settings = ScriptableObject.CreateInstance<WorldModuleSettings>(); AssetDatabase.CreateAsset(settings, path); }
            return settings;
        }
        public static Material Material(string path, string shaderName, Color color)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find(shaderName);
                if (shader == null) throw new InvalidOperationException("Shader absent : " + shaderName);
                material = new Material(shader); AssetDatabase.CreateAsset(material, path);
                material.color = color;
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .2f);
            }
            material.enableInstancing = true; EditorUtility.SetDirty(material); return material;
        }
        public static Mesh SaveMesh(string path, Mesh mesh)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(mesh, path); return mesh; }
            EditorUtility.CopySerialized(mesh, existing); UnityEngine.Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing); return existing;
        }
        public static void Save(Scene scene)
        {
            AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Sauvegarde impossible : " + ScenePath);
        }
    }
}
#endif
