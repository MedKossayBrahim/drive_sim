#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Project.Plane
{
    /// <summary>APK de test autonome ; conserve la demande à travers le rechargement Android de l'éditeur.</summary>
    [InitializeOnLoad]
    public static class AndroidApkBuilder
    {
        const string PendingKey="SkyCourier.AndroidApk.Pending";
        const string StageKey="SkyCourier.AndroidApk.Stage";
        const string ScenePath="Assets/_Plane/Scenes/Plane_Scene.unity";
        public static string OutputPath=>Path.GetFullPath("Builds/Android/SkyCourier-test.apk");
        static AndroidApkBuilder(){EditorApplication.update+=ContinueBuild;}
        [MenuItem("Sky Courier/Build Android Test APK",false,300)]
        public static void Build()
        {
            if(SessionState.GetBool(PendingKey,false)){Debug.LogWarning("[Sky Courier] Construction Android déjà demandée.");return;}
            Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
            File.WriteAllText(Path.ChangeExtension(OutputPath,"status.txt"),"Préparation de l'APK Android.");
            SessionState.SetInt(StageKey,0);SessionState.SetBool(PendingKey,true);
            if(EditorApplication.isPlaying)EditorApplication.isPlaying=false;
            Debug.Log("[Sky Courier] APK Android demandé : "+OutputPath);
        }
        static void ContinueBuild()
        {
            if(!SessionState.GetBool(PendingKey,false)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||BuildPipeline.isBuildingPlayer)return;
            try
            {
                int stage=SessionState.GetInt(StageKey,0);
                if(stage==0)
                {
                    Configure();SessionState.SetInt(StageKey,1);
                    if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.Android)
                    {
                        Status("Import des assets pour Android et compilation IL2CPP ARMv7/ARM64.");
                        if(!EditorUserBuildSettings.SwitchActiveBuildTargetAsync(BuildTargetGroup.Android,BuildTarget.Android))throw new InvalidOperationException("Passage à Android impossible.");
                        return;
                    }
                }
                if(EditorUserBuildSettings.activeBuildTarget!=BuildTarget.Android)return;
                // Retire la demande avant BuildPlayer pour éviter une reconstruction automatique après un échec.
                SessionState.SetBool(PendingKey,false);Status("Construction de l'APK Android en cours.");
                var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes=new[]{ScenePath},locationPathName=OutputPath,target=BuildTarget.Android,
                    options=BuildOptions.Development|BuildOptions.CompressWithLz4
                });
                var summary=report.summary;
                var text=new StringBuilder();
                text.AppendLine("Sky Courier — APK de test Android");text.AppendLine("Résultat : "+summary.result);
                text.AppendLine("Scène : "+ScenePath);text.AppendLine("APK : "+OutputPath);
                text.AppendLine("Durée : "+summary.totalTime);text.AppendLine("Octets : "+summary.totalSize);
                text.AppendLine("Erreurs : "+summary.totalErrors+" ; avertissements : "+summary.totalWarnings);
                foreach(var step in report.steps)foreach(var message in step.messages)
                    if(message.type==LogType.Error||message.type==LogType.Exception)text.AppendLine(message.content);
                File.WriteAllText(Path.Combine(Path.GetDirectoryName(OutputPath),"build-report.txt"),text.ToString());
                if(summary.result!=BuildResult.Succeeded||!File.Exists(OutputPath))throw new InvalidOperationException("Échec de construction : "+summary.result+". Consulter Builds/Android/build-report.txt et Logs/Editor.log.");
                Status("SUCCEEDED — "+OutputPath+" — "+new FileInfo(OutputPath).Length+" octets");
                Debug.Log("[Sky Courier] APK prêt : "+OutputPath);
                if(Application.isBatchMode)EditorApplication.Exit(0);
            }
            catch(Exception e){SessionState.SetBool(PendingKey,false);Status("FAILED — "+e);Debug.LogException(e);if(Application.isBatchMode)EditorApplication.Exit(1);}
        }
        static void Configure()
        {
            if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android,BuildTarget.Android))throw new InvalidOperationException("Installer Android Build Support dans Unity Hub.");
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath)==null)throw new FileNotFoundException("Plane_Scene absente",ScenePath);
            // Sauvegarde uniquement la scène avion si elle est ouverte et modifiée.
            var scene=SceneManager.GetSceneByPath(ScenePath);
            if(scene.IsValid()&&scene.isLoaded&&scene.isDirty&&!EditorSceneManager.SaveScene(scene))throw new IOException("Sauvegarde de Plane_Scene impossible.");
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
            PlayerSettings.productName="Sky Courier";
            PlayerSettings.bundleVersion="0.1.1";
            PlayerSettings.Android.bundleVersionCode=2;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,"com.interactivedevelopment.skycourier");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android,ManagedStrippingLevel.Low);
            PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.Android,Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARMv7|AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion=(AndroidSdkVersions)26;
            PlayerSettings.Android.targetSdkVersion=(AndroidSdkVersions)36;
            PlayerSettings.Android.useCustomKeystore=false;
            PlayerSettings.defaultInterfaceOrientation=UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait=false;PlayerSettings.allowedAutorotateToPortraitUpsideDown=false;
            PlayerSettings.allowedAutorotateToLandscapeLeft=true;PlayerSettings.allowedAutorotateToLandscapeRight=true;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android,false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,new[]{GraphicsDeviceType.OpenGLES3});
            EditorUserBuildSettings.buildAppBundle=false;EditorUserBuildSettings.exportAsGoogleAndroidProject=false;
            AssetDatabase.SaveAssets();
        }
        static void Status(string text)
        {Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));File.WriteAllText(Path.ChangeExtension(OutputPath,"status.txt"),text);}
    }
}
#endif
