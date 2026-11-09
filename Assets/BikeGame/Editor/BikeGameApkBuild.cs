using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BikeGame.EditorTools
{
    /// <summary>
    /// Builds the Android APK used for on-device testing.
    /// Run with:
    ///   Unity.exe -batchmode -nographics -buildTarget Android -projectPath <project> \
    ///       -executeMethod BikeGame.EditorTools.BikeGameApkBuild.Build -logFile <log>
    /// </summary>
    public static class BikeGameApkBuild
    {
        public static void Build()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string outputDirectory = Path.Combine(projectRoot, "Builds", "Android");
            string outputPath = Path.Combine(outputDirectory, "ForestBikeRide.apk");

            Directory.CreateDirectory(outputDirectory);

            EditorUserBuildSettings.buildAppBundle = false;

            string[] scenes = System.Array.ConvertAll(
                System.Array.FindAll(EditorBuildSettings.scenes, scene => scene.enabled),
                scene => scene.path);

            if (scenes.Length == 0)
            {
                Debug.LogError("BIKEBUILD: no enabled scenes in Build Settings.");
                EditorApplication.Exit(1);
                return;
            }

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            Debug.LogFormat("BIKEBUILD: building {0} scene(s) to {1}", scenes.Length, outputPath);

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            Debug.LogFormat(
                "BIKEBUILD: result={0} errors={1} warnings={2} size={3} bytes output={4} duration={5}",
                summary.result,
                summary.totalErrors,
                summary.totalWarnings,
                summary.totalSize,
                summary.outputPath,
                summary.totalTime);

            EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
