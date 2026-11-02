#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Xml.Linq;
using UnityEditor.Android;

namespace Project.Plane
{
    /// <summary>Déclare la permission dans le projet Gradle généré, sans manifeste global ni changement de PlayerSettings.</summary>
    public sealed class HapticsAndroidManifest : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 100;
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifest = Path.Combine(path, "src/main/AndroidManifest.xml");
            EnsurePermission(manifest);
        }
        public static void EnsurePermission(string path)
        {
            var document = XDocument.Load(path);
            XNamespace android = "http://schemas.android.com/apk/res/android";
            if (!document.Root.Elements("uses-permission").Any(e => (string)e.Attribute(android + "name") == "android.permission.VIBRATE"))
            {
                document.Root.Add(new XElement("uses-permission", new XAttribute(android + "name", "android.permission.VIBRATE")));
                document.Save(path);
            }
        }
    }
}
#endif
