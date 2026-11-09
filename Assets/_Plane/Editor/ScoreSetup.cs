#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Project.Plane
{
    public static class ScoreSetup
    {
        [MenuItem("Sky Courier/Gameplay/Setup Score And Haptics")]
        public static void Build()
        {
            var scene = ModuleSceneUtility.Open();
            const string planePath = "Assets/_Plane/Prefabs/Plane.prefab";
            var prefab = PrefabUtility.LoadPrefabContents(planePath);
            try
            {
                if (prefab.GetComponent<CrashHaptics>() == null)
                {
                    prefab.AddComponent<CrashHaptics>();
                    PrefabUtility.SaveAsPrefabAsset(prefab, planePath);
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(prefab); }
            var course = ModuleSceneUtility.Only<CheckpointManager>(scene);
            var input = ModuleSceneUtility.Only<PlaneInput>(scene);
            var gameplay = ModuleSceneUtility.Root(scene, "Gameplay");
            var root = gameplay.Find("Score");
            if (root == null) { root = new GameObject("Score").transform; root.SetParent(gameplay, false); Undo.RegisterCreatedObjectUndo(root.gameObject, "Créer Score"); }
            var score = root.GetComponent<ScoreManager>() ?? Undo.AddComponent<ScoreManager>(root.gameObject);
            score.Configure(course, input.GetComponent<PlaneHealth>()); EditorUtility.SetDirty(score);
            var old = root.Find("ScoreCanvas"); if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
            var canvas = Rect("ScoreCanvas", root);
            canvas.gameObject.AddComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.GetComponent<Canvas>().sortingOrder = 60;
            var scaler = canvas.gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            var safe = Rect("SafeArea", canvas); Stretch(safe); safe.gameObject.AddComponent<PlaneHUDSafeArea>();
            var content = Rect("ScorePanel", safe); content.anchorMin = content.anchorMax = content.pivot = new Vector2(0, 1);
            content.anchoredPosition = new Vector2(22, -16); content.sizeDelta = new Vector2(720, 156);
            var image = content.gameObject.AddComponent<Image>(); image.color = new Color(.025f, .06f, .08f, .8f); image.raycastTarget = false;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.padding = new RectOffset(12,12,6,6); layout.spacing = 1;
            layout.childControlHeight = layout.childControlWidth = true; layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Plane/UI/TextMeshPro/Resources/Fonts & Materials/LiberationSans SDF.asset");
            var total = Label(content, font, "Total", "SCORE 0  ·  RECORD 0", 40, 54);
            var coins = Label(content, font, "Coins", "PIÈCES 0", 34, 44);
            var feedback = Label(content, font, "Points", "", 28, 34); feedback.color = new Color(1f,.79f,.08f);
            var view = canvas.gameObject.AddComponent<ScoreHUD>(); view.Configure(score, input, total, coins, feedback, content.gameObject);
            var hud = ModuleSceneUtility.Only<PlaneHUD>(scene);
            var serialized = new SerializedObject(hud); serialized.FindProperty("globalScore").objectReferenceValue = score; serialized.ApplyModifiedPropertiesWithoutUndo();
            // Un panneau séparé survit au menu Build HUD ; le score y reste l'unique source du total.
            ModuleSceneUtility.Save(scene);
            Debug.Log("[Sky Courier] Score global, compteur de pièces et haptique prêts. Aucun réglage global modifié.");
        }
        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false); return rect;
        }
        private static void Stretch(RectTransform rect) { rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero; }
        private static TMP_Text Label(Transform parent, TMP_FontAsset font, string name, string value, int size, float height)
        {
            var rect=Rect(name,parent);var text=rect.gameObject.AddComponent<TextMeshProUGUI>();text.font=font;text.text=value;
            text.fontSize=size;text.enableAutoSizing=true;text.fontSizeMax=size;text.fontSizeMin=24;text.alignment=TextAlignmentOptions.Center;
            text.color=new Color(.18f,.9f,.85f);text.raycastTarget=false;
            var element=rect.gameObject.AddComponent<LayoutElement>();element.preferredHeight=height;element.minHeight=height;return text;
        }
    }
}
#endif
