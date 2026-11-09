using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

#if UNITY_ANDROID
using UnityEditor.Android;
#endif

namespace BikeGame.EditorTools
{
#if UNITY_ANDROID
    /// <summary>
    /// Injects android.permission.VIBRATE into the Gradle project Unity
    /// generates for Android. This keeps the stock manifest intact instead of
    /// replacing it with a hand written one that would need maintenance.
    /// </summary>
    public sealed class BikeGameAndroidSetup : IPostGenerateGradleAndroidProject
    {
        private const string Permission = "android.permission.VIBRATE";

        public int callbackOrder => 100;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");

            if (!File.Exists(manifestPath))
            {
                Debug.LogWarning("BikeGameAndroidSetup: AndroidManifest.xml not found at " + manifestPath);
                return;
            }

            string manifest = File.ReadAllText(manifestPath);

            if (manifest.Contains(Permission))
                return;

            int insertIndex = manifest.IndexOf("<application");

            if (insertIndex < 0)
            {
                Debug.LogWarning("BikeGameAndroidSetup: could not find the <application> tag, " +
                                 "add " + Permission + " manually.");
                return;
            }

            string line = "    <uses-permission android:name=\"" + Permission + "\" />\n";
            File.WriteAllText(manifestPath, manifest.Insert(insertIndex, line));

            Debug.Log("BikeGameAndroidSetup: added " + Permission + " to the generated manifest.");
        }
    }
#endif

    public static class BikeGameAndroidMenu
    {
        [MenuItem("BikeGame/Android/Check Build Setup")]
        public static void CheckBuildSetup()
        {
            NamedBuildTarget android = NamedBuildTarget.Android;

            Debug.LogFormat(
                "BikeGame Android setup\n" +
                "  Active build target : {0}\n" +
                "  Minimum API level   : {1}\n" +
                "  Target API level    : {2} (0 = automatic)\n" +
                "  Package identifier  : {3}\n" +
                "  Scripting backend   : {4}\n" +
                "  Scenes in build     : {5}\n" +
                "  VIBRATE permission  : added automatically by BikeGameAndroidSetup when a Gradle project is generated.",
                EditorUserBuildSettings.activeBuildTarget,
                (int)PlayerSettings.Android.minSdkVersion,
                (int)PlayerSettings.Android.targetSdkVersion,
                PlayerSettings.GetApplicationIdentifier(android),
                PlayerSettings.GetScriptingBackend(android),
                EditorBuildSettings.scenes.Length);
        }
    }
}
