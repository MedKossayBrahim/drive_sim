#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DriveSim.Editor
{
    // Build APK complet (toutes les scènes du Build Settings) pour la validation.
    public static class DriveSimAndroidBuild
    {
        public static string OutputPath => Path.GetFullPath("Builds/Android/MultiRide.apk");

        [MenuItem("MultiRide/Build Android APK", false, 100)]
        public static void BuildFromMenu()
        {
            MultiRideBranding.Apply();
            var code = BuildApk(false);
            if (code != 0)
                throw new InvalidOperationException("Échec du build Android. Voir Builds/Android/build-report.txt");
        }

        // Unity -batchmode -executeMethod DriveSim.Editor.DriveSimAndroidBuild.BuildFromCommandLine
        public static void BuildFromCommandLine()
        {
            MultiRideBranding.Apply();
            EditorApplication.Exit(BuildApk(true));
        }

        static int BuildApk(bool batch)
        {
            try
            {
                if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                    throw new InvalidOperationException("Installer Android Build Support dans Unity Hub.");

                var scenes = EditorBuildSettings.scenes
                    .Where(scene => scene.enabled && !string.IsNullOrEmpty(scene.path))
                    .Select(scene => scene.path)
                    .ToArray();
                if (scenes.Length == 0)
                    throw new InvalidOperationException("Aucune scène activée dans File > Build Settings.");

                Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
                if (File.Exists(OutputPath))
                    File.Delete(OutputPath);

                EditorUserBuildSettings.buildAppBundle = false;
                PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.esprit.drivesim");
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

                if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                {
                    if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                        throw new InvalidOperationException("Impossible de basculer vers Android.");
                }

                var options = new BuildPlayerOptions
                {
                    scenes = scenes,
                    locationPathName = OutputPath,
                    target = BuildTarget.Android,
                    options = BuildOptions.CompressWithLz4
                };

                Debug.Log("[MultiRide] Build Android → " + OutputPath);
                var report = BuildPipeline.BuildPlayer(options);
                WriteReport(report, scenes);

                if (report.summary.result != BuildResult.Succeeded || !File.Exists(OutputPath))
                    throw new InvalidOperationException("Build échoué : " + report.summary.result);

                Debug.Log("[MultiRide] APK prêt : " + OutputPath + " (" + new FileInfo(OutputPath).Length + " octets)");
                return 0;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(OutputPath));
                    File.WriteAllText(
                        Path.Combine(Path.GetDirectoryName(OutputPath), "build-report.txt"),
                        "FAILED\n" + e);
                }
                catch
                {
                    // ignore IO errors while reporting failure
                }
                return 1;
            }
        }

        static void WriteReport(BuildReport report, string[] scenes)
        {
            var text = new StringBuilder();
            text.AppendLine("MultiRide — APK Android");
            text.AppendLine("Résultat : " + report.summary.result);
            text.AppendLine("APK : " + OutputPath);
            text.AppendLine("Durée : " + report.summary.totalTime);
            text.AppendLine("Octets : " + report.summary.totalSize);
            text.AppendLine("Erreurs : " + report.summary.totalErrors);
            text.AppendLine("Avertissements : " + report.summary.totalWarnings);
            text.AppendLine("Scènes :");
            foreach (var scene in scenes)
                text.AppendLine("  - " + scene);
            foreach (var step in report.steps)
            {
                foreach (var message in step.messages)
                {
                    if (message.type == LogType.Error || message.type == LogType.Exception)
                        text.AppendLine(message.content);
                }
            }
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(OutputPath), "build-report.txt"), text.ToString());
        }
    }
}
#endif
