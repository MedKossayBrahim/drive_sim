using System;
using System.IO;
using BikeGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace BikeGame.EditorTools
{
    /// <summary>
    /// Builds the coin HUD: a top-right uGUI panel with a coin icon and the
    /// collected amount. Also generates the two small sprites it needs (a coin
    /// icon cropped from the coin albedo and a rounded panel background) so no
    /// external UI package or art dependency is required.
    ///
    /// Run from the menu or with:
    ///   Unity.exe -batchmode -projectPath <project> \
    ///       -executeMethod BikeGame.EditorTools.CoinHudSetup.Run \
    ///       -logFile <log>
    /// </summary>
    public static class CoinHudSetup
    {
        private const string ScenePath = "Assets/BikeGame/Scenes/ForestBikeRide.unity";
        private const string UiFolder = "Assets/BikeGame/UI";
        private const string IconPath = UiFolder + "/CoinIcon.png";
        private const string PanelPath = UiFolder + "/CoinHudPanel.png";
        private const string CoinAlbedoPath = "Assets/BikeGame/Coins/Textures/Coin_LP1_Gold_BaseColor.png";

        [MenuItem("Tools/Bike Game/Setup Coin HUD")]
        public static void SetupFromMenu()
        {
            Setup();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Selection.activeGameObject = GameObject.Find("Coin Counter");
        }

        public static void Run()
        {
            try
            {
                Setup();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void Setup()
        {
            EnsureUiFolder();
            WriteCoinIcon();
            WritePanelSprite();

            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
            ConfigureSprite(IconPath, Vector4.zero, 256);
            ConfigureSprite(PanelPath, new Vector4(24f, 24f, 24f, 24f), 64);

            Sprite iconSprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
            Sprite panelSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PanelPath);
            Font font = LoadBuiltinFont();

            if (iconSprite == null) throw new InvalidOperationException("Could not import " + IconPath);
            if (panelSprite == null) throw new InvalidOperationException("Could not import " + PanelPath);
            if (font == null) throw new InvalidOperationException("No built-in UI font was found.");

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            CoinCounter counter = UnityEngine.Object.FindAnyObjectByType<CoinCounter>();
            if (counter == null)
                throw new InvalidOperationException("Coin Counter was not found in " + ScenePath);

            Transform oldHud = counter.transform.Find("Coin HUD");
            if (oldHud != null)
                UnityEngine.Object.DestroyImmediate(oldHud.gameObject);

            BuildHud(counter, iconSprite, panelSprite, font);

            SerializedObject serialized = new SerializedObject(counter);
            serialized.FindProperty("collectedCoins").intValue = 0;
            serialized.FindProperty("totalCoins").intValue =
                UnityEngine.Object.FindObjectsByType<CoinCollectible>(FindObjectsSortMode.None).Length;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            Debug.LogFormat("CoinHud: built HUD for '{0}', icon={1}, panel={2}, scene={3}",
                counter.name, IconPath, PanelPath, ScenePath);
        }

        private static void EnsureUiFolder()
        {
            if (!AssetDatabase.IsValidFolder(UiFolder))
                AssetDatabase.CreateFolder("Assets/BikeGame", "UI");
        }

        private static void BuildHud(CoinCounter counter, Sprite iconSprite, Sprite panelSprite, Font font)
        {
            GameObject canvasObject = new GameObject("Coin HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(counter.transform, false);

            RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasRect.localPosition = Vector3.zero;
            canvasRect.localRotation = Quaternion.identity;
            canvasRect.localScale = Vector3.one;

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            // Reference resolution matches a 1080p landscape phone. Matching the
            // short edge keeps the HUD proportionally sized on tablets and small
            // phones alike.
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;

            GameObject safeObject = new GameObject("Safe Area", typeof(RectTransform));
            safeObject.transform.SetParent(canvasObject.transform, false);
            RectTransform safeRect = safeObject.GetComponent<RectTransform>();
            safeRect.anchorMin = Vector2.zero;
            safeRect.anchorMax = Vector2.one;
            safeRect.offsetMin = Vector2.zero;
            safeRect.offsetMax = Vector2.zero;
            safeRect.pivot = new Vector2(0.5f, 0.5f);

            GameObject panelObject = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(safeRect, false);
            RectTransform panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1f, 1f);
            panelRect.anchorMax = new Vector2(1f, 1f);
            panelRect.pivot = new Vector2(1f, 1f);
            panelRect.sizeDelta = new Vector2(268f, 104f);
            panelRect.anchoredPosition = new Vector2(-28f, -28f);

            Image panel = panelObject.GetComponent<Image>();
            panel.sprite = panelSprite;
            panel.type = Image.Type.Sliced;
            panel.color = new Color(0.03f, 0.035f, 0.03f, 0.82f);
            panel.raycastTarget = false;

            GameObject iconObject = new GameObject("Coin Icon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(panelRect, false);
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.sizeDelta = new Vector2(68f, 68f);
            iconRect.anchoredPosition = new Vector2(26f, 0f);

            Image icon = iconObject.GetComponent<Image>();
            icon.sprite = iconSprite;
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            GameObject labelObject = new GameObject("Count", typeof(RectTransform), typeof(Text), typeof(Shadow));
            labelObject.transform.SetParent(panelRect, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(112f, 6f);
            labelRect.offsetMax = new Vector2(-22f, -6f);

            Text label = labelObject.GetComponent<Text>();
            label.font = font;
            label.text = "0";
            label.fontSize = 58;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleLeft;
            label.color = new Color(1f, 0.95f, 0.78f, 1f);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            label.supportRichText = false;

            Shadow shadow = labelObject.GetComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
            shadow.effectDistance = new Vector2(2f, -2f);
            shadow.useGraphicAlpha = true;

            SerializedObject serialized = new SerializedObject(counter);
            serialized.FindProperty("safeArea").objectReferenceValue = safeRect;
            serialized.FindProperty("countLabel").objectReferenceValue = label;
            serialized.FindProperty("icon").objectReferenceValue = iconRect;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Debug.LogFormat("CoinHud: hierarchy {0} > {1} > {2} (icon + count), font={3}",
                canvasObject.name, safeObject.name, panelObject.name, font.name);
        }

        private static Font LoadBuiltinFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return font;
        }

        /// <summary>
        /// Crops the engraved coin face out of the supplied albedo, masks it to a
        /// circle and brightens it slightly so it reads on the dark HUD panel.
        /// </summary>
        private static void WriteCoinIcon()
        {
            if (!File.Exists(CoinAlbedoPath))
                throw new FileNotFoundException("Coin albedo not found: " + CoinAlbedoPath);

            Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            if (!ImageConversion.LoadImage(source, File.ReadAllBytes(CoinAlbedoPath)))
                throw new InvalidOperationException("Could not read " + CoinAlbedoPath);

            const int size = 384;
            // Face circle measured on the 1024px albedo, in Unity texture space
            // (origin bottom-left).
            const float centerX = 242f;
            const float centerY = 786f;
            const float radius = 189f;

            Texture2D icon = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float sourceX = centerX - size * 0.5f + x + 0.5f;
                    float sourceY = centerY - size * 0.5f + y + 0.5f;

                    Color color = source.GetPixel(
                        Mathf.Clamp(Mathf.RoundToInt(sourceX), 0, source.width - 1),
                        Mathf.Clamp(Mathf.RoundToInt(sourceY), 0, source.height - 1));

                    float dx = x + 0.5f - size * 0.5f;
                    float dy = y + 0.5f - size * 0.5f;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Clamp01((radius + 1.5f - distance) / 3f);

                    color.r = Mathf.Clamp01(color.r * 1.28f);
                    color.g = Mathf.Clamp01(color.g * 1.22f);
                    color.b = Mathf.Clamp01(color.b * 1.1f);
                    color.a = alpha;

                    pixels[y * size + x] = color;
                }
            }

            icon.SetPixels(pixels);
            icon.Apply();
            File.WriteAllBytes(IconPath, icon.EncodeToPNG());

            UnityEngine.Object.DestroyImmediate(icon);
            UnityEngine.Object.DestroyImmediate(source);
        }

        /// <summary>Rounded 9-slice panel background, tinted by the Image color.</summary>
        private static void WritePanelSprite()
        {
            const int size = 64;
            const float radius = 18f;
            float half = size * 0.5f;

            Texture2D panel = new Texture2D(size, size, TextureFormat.RGBA32, false, false);
            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f - half;
                    float py = y + 0.5f - half;

                    float qx = Mathf.Abs(px) - (half - radius);
                    float qy = Mathf.Abs(py) - (half - radius);
                    float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) +
                                               Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f)) +
                                    Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                    float alpha = Mathf.Clamp01(0.5f - outside);

                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            panel.SetPixels(pixels);
            panel.Apply();
            File.WriteAllBytes(PanelPath, panel.EncodeToPNG());

            UnityEngine.Object.DestroyImmediate(panel);
        }

        private static void ConfigureSprite(string path, Vector4 border, int maxSize)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                throw new InvalidOperationException("Could not load sprite importer: " + path);

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = border;
            importer.spritePixelsPerUnit = 100f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = maxSize;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.sRGBTexture = true;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }
    }
}
