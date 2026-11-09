#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Callbacks;
using UnityEngine;

namespace DriveSim.Editor
{
    // Applique le nom MultiRide et l'icône d'application.
    public static class MultiRideBranding
    {
        const string LogoPath = "Assets/Resources/Menu/MultiRideLogo.jpg";

        [InitializeOnLoadMethod]
        static void ApplyOnLoad()
        {
            Apply();
        }

        [DidReloadScripts]
        static void ApplyAfterReload()
        {
            Apply();
        }

        [MenuItem("MultiRide/Appliquer branding", false, 10)]
        public static void ApplyFromMenu()
        {
            Apply();
            Debug.Log("[MultiRide] Nom et icône appliqués.");
        }

        public static void Apply()
        {
            PlayerSettings.productName = "MultiRide";
            PlayerSettings.companyName = "Esprit";
            PlayerSettings.locationUsageDescription =
                "MultiRide utilise la position GPS pour la course en voiture. La position reste sur le telephone.";
            PlayerSettings.microphoneUsageDescription =
                "MultiRide utilise le micro pour le bruit ambiant dans la mission sous-marin. L'audio reste sur le telephone.";

            var logo = AssetDatabase.LoadAssetAtPath<Texture2D>(LogoPath);
            if (logo == null)
                return;

            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { logo }, IconKind.Application);
            PlayerSettings.SetIcons(NamedBuildTarget.Android, new[] { logo }, IconKind.Application);
            PlayerSettings.SetIcons(NamedBuildTarget.iOS, new[] { logo }, IconKind.Application);
        }
    }
}
#endif
