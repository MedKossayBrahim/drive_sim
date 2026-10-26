using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

// Au moment du build iPhone, ajoute la phrase Face ID et autorise le serveur local en HTTP.
public static class IosAuthBuild
{
    [PostProcessBuild(50)]
    public static void OnPostProcessBuild(BuildTarget target, string path)
    {
#if UNITY_IOS
        if (target != BuildTarget.iOS)
            return;

        var plistPath = Path.Combine(path, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);
        var root = plist.root;
        root.SetString(
            "NSFaceIDUsageDescription",
            "Drive Sim utilise Face ID pour confirmer que c'est bien vous. Votre visage ne quitte pas le telephone.");
        root.SetString(
            "NSLocalNetworkUsageDescription",
            "Drive Sim se connecte au serveur de comptes du projet, sur le Wi-Fi de la salle.");
        var ats = root.CreateDict("NSAppTransportSecurity");
        ats.SetBoolean("NSAllowsArbitraryLoads", true);
        plist.WriteToFile(plistPath);

        var projectPath = PBXProject.GetPBXProjectPath(path);
        var project = new PBXProject();
        project.ReadFromFile(projectPath);
        var frameworkTarget = project.GetUnityFrameworkTargetGuid();
        project.AddFrameworkToProject(frameworkTarget, "LocalAuthentication.framework", false);
        project.AddFrameworkToProject(frameworkTarget, "Security.framework", false);
        project.WriteToFile(projectPath);
#endif
    }
}
