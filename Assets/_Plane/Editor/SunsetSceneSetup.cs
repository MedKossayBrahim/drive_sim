#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Project.Plane
{
    /// <summary>Configure le coucher de soleil dans Plane_Scene, sans recréer les ciels ni les effets existants.</summary>
    public static class SunsetSceneSetup
    {
        private const string ScenePath = "Assets/_Plane/Scenes/Plane_Scene.unity";
        private const string PresetFolder = "Assets/_Plane/Skyboxes/Presets/";
        [MenuItem("Sky Courier/15 - Apply Sunset To Scene", false, 150)]
        public static void ApplySunset()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            { Debug.LogWarning("[Sky Courier] Quitter Play avant d'appliquer Sunset à la scène."); return; }
            Scene previousScene = SceneManager.GetActiveScene();
            try
            {
                var presets = new[] { "Noon", "Sunset", "Night", "Storm" }
                    .Select(name => AssetDatabase.LoadAssetAtPath<SkyPreset>(PresetFolder + name + ".asset")).ToArray();
                if (presets.Any(p => p == null || p.SkyMaterial == null))
                    throw new InvalidOperationException("Les quatre presets existants et leurs matériaux doivent être assignés.");
                Scene scene = SceneManager.GetSceneByPath(ScenePath);
                if (!scene.IsValid() || !scene.isLoaded)
                    scene = EditorSceneManager.OpenScene(ScenePath,
                        SceneManager.sceneCount == 1 && !previousScene.isDirty ? OpenSceneMode.Single : OpenSceneMode.Additive);
                var roots = scene.GetRootGameObjects();
                var lights = roots.SelectMany(r => r.GetComponentsInChildren<Light>(true))
                    .Where(l => l.type == LightType.Directional && l.isActiveAndEnabled).ToArray();
                if (lights.Length != 1) throw new InvalidOperationException("Une seule Directional Light active est requise dans Plane_Scene.");
                var globals = roots.SelectMany(r => r.GetComponentsInChildren<Volume>(true)).Where(v => v.isGlobal).ToArray();
                if (globals.Length != 1 || globals[0].sharedProfile == null)
                    throw new InvalidOperationException("Un Global Volume existant avec profil est requis.");
                var volume = globals[0]; var profile = volume.sharedProfile;
                if (!AssetDatabase.GetAssetPath(profile).StartsWith("Assets/_Plane/", StringComparison.Ordinal))
                    throw new InvalidOperationException("Le profil du Global Volume doit appartenir à Assets/_Plane pour préserver les autres scènes.");
                var environments = roots.Where(r => r.name == "Environment").ToArray();
                if (environments.Length > 1) throw new InvalidOperationException("Plusieurs objets racines Environment : résoudre cette ambiguïté.");
                var environment = environments.SingleOrDefault();
                var skyChildren = environment != null ? environment.transform.Cast<Transform>().Where(t => t.name == "Sky").ToArray() : Array.Empty<Transform>();
                if (skyChildren.Length > 1) throw new InvalidOperationException("Environment contient plusieurs enfants Sky.");
                var skyTransform = skyChildren.SingleOrDefault();
                var otherSwitchers = roots.SelectMany(r => r.GetComponentsInChildren<SkyboxSwitcher>(true))
                    .Where(s => s.transform != skyTransform && s.isActiveAndEnabled).ToArray();
                if (otherSwitchers.Length > 0) throw new InvalidOperationException("Un autre SkyboxSwitcher actif existe : éviter deux contrôleurs de ciel.");

                // RenderSettings appartient à la scène active ; les autres scènes chargées restent intactes.
                SceneManager.SetActiveScene(scene);
                Undo.IncrementCurrentGroup(); Undo.SetCurrentGroupName("Appliquer Sunset à Plane_Scene");
                if (environment == null)
                {
                    environment = new GameObject("Environment"); SceneManager.MoveGameObjectToScene(environment, scene);
                    Undo.RegisterCreatedObjectUndo(environment, "Créer Environment");
                }
                if (skyTransform == null)
                {
                    var sky = new GameObject("Sky"); sky.transform.SetParent(environment.transform, false); skyTransform = sky.transform;
                    Undo.RegisterCreatedObjectUndo(sky, "Créer Environment/Sky");
                }
                var switcher = skyTransform.GetComponent<SkyboxSwitcher>() ?? Undo.AddComponent<SkyboxSwitcher>(skyTransform.gameObject);
                var serialized = new SerializedObject(switcher);
                Undo.RecordObject(switcher, "Configurer SkyboxSwitcher");
                string[] fields = { "noon", "sunset", "night", "storm" };
                for (int i = 0; i < fields.Length; i++) serialized.FindProperty(fields[i]).objectReferenceValue = presets[i];
                serialized.FindProperty("directionalLight").objectReferenceValue = lights[0];
                serialized.FindProperty("selectedPreset").enumValueIndex = (int)SkyPresetId.Sunset;
                serialized.ApplyModifiedProperties();
                // Le composant n'édite jamais RenderSettings hors Play ; l'aperçu persistant relève de ce menu.
                SkyPreset sunset = presets[1];
                RenderSettings.skybox = sunset.SkyMaterial;
                RenderSettings.ambientMode = AmbientMode.Skybox;
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
                RenderSettings.sun = lights[0];
                RenderSettings.fog = sunset.FogEnabled;
                RenderSettings.fogMode = FogMode.ExponentialSquared;
                RenderSettings.fogColor = sunset.FogColor;
                RenderSettings.fogDensity = sunset.FogDensity;
                Undo.RecordObjects(new UnityEngine.Object[] { lights[0], lights[0].transform }, "Appliquer la lumière Sunset");
                lights[0].color = sunset.DirectionalLightColor; lights[0].intensity = sunset.DirectionalLightIntensity;
                lights[0].transform.rotation = Quaternion.Euler(sunset.SunEuler);

                Undo.RecordObject(volume, "Activer le Global Volume");
                volume.enabled = true; volume.weight = 1f;
                var tone = Effect<Tonemapping>(profile); Undo.RecordObject(tone, "Configurer ACES");
                tone.active = true; tone.mode.Override(TonemappingMode.ACES);
                var bloom = Effect<Bloom>(profile); Undo.RecordObject(bloom, "Configurer Bloom");
                bloom.active = true; bloom.threshold.Override(0.9f); bloom.intensity.Override(0.5f);
                var vignette = Effect<Vignette>(profile); Undo.RecordObject(vignette, "Configurer Vignette");
                vignette.active = true; vignette.intensity.Override(0.15f); vignette.smoothness.Override(0.4f);
                foreach (var item in new UnityEngine.Object[] { switcher, lights[0], lights[0].transform, volume, profile, tone, bloom, vignette })
                    EditorUtility.SetDirty(item);
                if (PrefabUtility.IsPartOfPrefabInstance(switcher)) PrefabUtility.RecordPrefabInstancePropertyModifications(switcher);
                foreach (var item in new UnityEngine.Object[] { lights[0], lights[0].transform, volume })
                    if (PrefabUtility.IsPartOfPrefabInstance(item)) PrefabUtility.RecordPrefabInstancePropertyModifications(item);
                DynamicGI.UpdateEnvironment();
                AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Sauvegarde de Plane_Scene impossible.");
                Selection.activeGameObject = skyTransform.gameObject;
                var grass = roots.SelectMany(r => r.GetComponentsInChildren<Renderer>(true)).FirstOrDefault(r => r.name == "GrassGround");
                Debug.Log("[Sky Courier] Sunset appliqué : Environment/Sky, quatre presets, Sun Source et éclairage/réflexions Skybox. " +
                    "ACES, Bloom seuil 0,9 / intensité 0,5, Vignette 0,15. Touches 1/2/3/4 ; F3 préservée. " +
                    (grass != null ? $"Herbe : {grass.bounds.size.x:F0} × {grass.bounds.size.z:F0} m ; son bord peut révéler la carrière en altitude." : "Grand sol d'herbe non trouvé."), switcher);
            }
            catch (Exception exception) { Debug.LogException(exception); }
            finally
            {
                if (previousScene.IsValid() && previousScene.isLoaded && previousScene != SceneManager.GetActiveScene())
                    SceneManager.SetActiveScene(previousScene);
            }
        }
        private static T Effect<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet<T>(out var effect)) return effect;
            Undo.RecordObject(profile, "Ajouter un effet manquant");
            effect = profile.Add<T>(false); AssetDatabase.AddObjectToAsset(effect, profile);
            Undo.RegisterCreatedObjectUndo(effect, "Ajouter " + typeof(T).Name); return effect;
        }
    }
}
#endif
