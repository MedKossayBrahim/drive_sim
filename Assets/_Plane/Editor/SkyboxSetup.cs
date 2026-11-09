#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Project.Plane
{
    /// <summary>Prépare les ciels et leurs presets sans ouvrir ni modifier de scène.</summary>
    public static class SkyboxSetup
    {
        private const string Skyboxes = "Assets/_Plane/Skyboxes";
        private const string Materials = "Assets/_Plane/Materials";
        private const string Presets = Skyboxes + "/Presets";
        private static readonly string[] Names = { "Noon", "Sunset", "Night", "Storm" };

        [MenuItem("Sky Courier/14 - Setup Skyboxes", false, 140)]
        public static void SetupSkyboxes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Sky Courier] Quitter Play avant de réimporter les skyboxes.");
                return;
            }
            var shader = Shader.Find("Skybox/Panoramic");
            if (shader == null) throw new InvalidOperationException("Shader Skybox/Panoramic introuvable.");
            // Vérifie toutes les entrées avant d'effectuer la première modification.
            foreach (string name in Names)
            {
                string path = Skyboxes + "/sky_" + name.ToLowerInvariant() + ".exr";
                if (!File.Exists(path) || !(AssetImporter.GetAtPath(path) is TextureImporter))
                    throw new InvalidOperationException("EXR ou TextureImporter absent : " + path);
            }
            EnsureFolder(Materials);
            EnsureFolder(Presets);
            try
            {
                for (int i = 0; i < Names.Length; i++)
                {
                    string name = Names[i];
                    EditorUtility.DisplayProgressBar("Sky Courier — ciels", name, (float)i / Names.Length);
                    string texturePath = Skyboxes + "/sky_" + name.ToLowerInvariant() + ".exr";
                    ConfigureTexture(texturePath);
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                    if (texture == null) throw new InvalidOperationException("Texture non importée : " + texturePath);
                    var material = CreateMaterial(name, shader, texture);
                    CreatePreset(name, material);
                }
                AssetDatabase.SaveAssets();
                Debug.Log("[Sky Courier] 4 EXR configurés, 4 matériaux Sky_* et 4 presets dans " + Presets +
                    ". Aucune scène modifiée. Les réglages personnalisés des presets existants sont conservés.");
            }
            finally { EditorUtility.ClearProgressBar(); }
        }

        private static void ConfigureTexture(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = false;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.isReadable = false;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.compressionQuality = (int)TextureCompressionQuality.Normal;
            importer.crunchedCompression = false;
            // Automatic laisse Unity choisir un format adapté au HDR sur chaque plateforme.
            // Ne pas imposer une compression ASTC LDR aux fichiers EXR.
            var defaults = importer.GetDefaultPlatformTextureSettings();
            defaults.maxTextureSize = 2048;
            defaults.format = TextureImporterFormat.Automatic;
            defaults.textureCompression = TextureImporterCompression.Compressed;
            defaults.compressionQuality = (int)TextureCompressionQuality.Normal;
            defaults.crunchedCompression = false;
            importer.SetPlatformTextureSettings(defaults);
            ConfigurePlatform(importer, "Standalone", 2048);
            ConfigurePlatform(importer, "Android", 1024);
            importer.SaveAndReimport();
        }

        private static void ConfigurePlatform(TextureImporter importer, string name, int maxSize)
        {
            var platform = importer.GetPlatformTextureSettings(name);
            platform.name = name;
            platform.overridden = true;
            platform.maxTextureSize = maxSize;
            platform.format = TextureImporterFormat.Automatic;
            platform.textureCompression = TextureImporterCompression.Compressed;
            platform.compressionQuality = (int)TextureCompressionQuality.Normal;
            platform.crunchedCompression = false;
            importer.SetPlatformTextureSettings(platform);
        }

        private static Material CreateMaterial(string name, Shader shader, Texture2D texture)
        {
            string path = Materials + "/Sky_" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                if (File.Exists(path)) throw new IOException("Le chemin contient un autre type d'asset : " + path);
                material = new Material(shader) { name = "Sky_" + name };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            if (!material.HasProperty("_MainTex"))
                throw new InvalidOperationException("Le shader panoramique n'expose pas _MainTex.");
            material.SetTexture("_MainTex", texture);
            material.SetFloat("_Mapping", 1f); // Latitude Longitude Layout.
            material.DisableKeyword("_MAPPING_6_FRAMES_LAYOUT");
            // Nettoie aussi l'ancien mot-clé invalide conservé par certains matériaux importés.
            material.DisableKeyword("_MAPPING_LATITUDE_LONGITUDE_LAYOUT");
            material.SetFloat("_ImageType", 0f); // 360 degrés, et non 180 degrés.
            material.SetFloat("_Layout", 0f); // Panorama monoscopique.
            material.SetFloat("_MirrorOnBack", 0f);
            material.SetFloat("_Exposure", 1f);
            material.SetFloat("_Rotation", 0f);
            material.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f, 1f));
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void CreatePreset(string name, Material material)
        {
            string path = Presets + "/" + name + ".asset";
            var preset = AssetDatabase.LoadAssetAtPath<SkyPreset>(path);
            if (preset != null)
            {
                preset.SetSkyMaterial(material);
                EditorUtility.SetDirty(preset);
                return;
            }
            if (File.Exists(path)) throw new IOException("Le chemin contient un autre type d'asset : " + path);
            preset = ScriptableObject.CreateInstance<SkyPreset>();
            preset.name = name;
            switch (name)
            {
                case "Noon":
                    preset.InitializeDefaults(material, new Color(1f, 0.97f, 0.90f), 1.1f,
                        new Vector3(60f, -30f, 0f), new Color(0.66f, 0.76f, 0.86f), 0.0015f);
                    break;
                case "Sunset":
                    preset.InitializeDefaults(material, new Color(1f, 0.52f, 0.25f), 0.6f,
                        new Vector3(12f, -70f, 0f), new Color(0.58f, 0.37f, 0.31f), 0.003f);
                    break;
                case "Night":
                    preset.InitializeDefaults(material, new Color(0.38f, 0.52f, 0.85f), 0.015f,
                        new Vector3(25f, 130f, 0f), new Color(0.035f, 0.05f, 0.09f), 0.004f);
                    break;
                case "Storm":
                    preset.InitializeDefaults(material, new Color(0.62f, 0.66f, 0.70f), 0.25f,
                        new Vector3(35f, -20f, 0f), new Color(0.31f, 0.35f, 0.39f), 0.02f, true, 45f);
                    break;
                default:
                    UnityEngine.Object.DestroyImmediate(preset);
                    throw new ArgumentOutOfRangeException(nameof(name));
            }
            AssetDatabase.CreateAsset(preset, path);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, Path.GetFileName(path))))
                throw new IOException("Impossible de créer " + path);
        }
    }
}
#endif
