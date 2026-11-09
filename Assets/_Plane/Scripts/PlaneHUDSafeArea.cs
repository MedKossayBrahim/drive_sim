using UnityEngine;

namespace Project.Plane
{
    /// <summary>Applique la zone sûre après rotation ou changement de résolution, même pendant la pause.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class PlaneHUDSafeArea : MonoBehaviour
    {
        private Rect previous;
        private Vector2Int resolution;
        private void OnEnable() => Refresh();
        private void Update() => Refresh();
        private void Refresh()
        {
            if (Screen.width <= 0 || Screen.height <= 0) return;
            Rect area = Screen.safeArea;
            var size = new Vector2Int(Screen.width, Screen.height);
            if (previous == area && resolution == size) return;
            previous = area; resolution = size;
            var rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            rect.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
