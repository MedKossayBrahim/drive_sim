using UnityEngine;

namespace Project.Plane
{
    /// <summary>Réserve le centre au vol et dégage les zones du joystick et des gaz sur petit écran.</summary>
    [ExecuteAlways, DefaultExecutionOrder(50), DisallowMultipleComponent]
    public sealed class PlaneHUDLayout : MonoBehaviour
    {
        [SerializeField] private RectTransform safeArea = null, flight = null, mission = null, compass = null;
        [SerializeField] private RectTransform dock = null, navigation = null, warnings = null, message = null;
        [SerializeField] private PlaneInput input = null;
        [SerializeField, Min(0f)] private float margin = 22f;
        [SerializeField, Min(0f)] private float touchToolbarSpace = 90f;
        [SerializeField] private bool mobileToolbarInstalled = false;
        private void LateUpdate() => RefreshLayout();
        public void RefreshLayout()
        {
            if (safeArea == null || flight == null) return;
            float width = safeArea.rect.width;
            if (width <= 0f) return;
            bool touch = mobileToolbarInstalled || (Application.isPlaying && input != null && (input.Mode != ControlMode.Keyboard || Application.isMobilePlatform));
            float top = margin + (touch ? touchToolbarSpace : 0f);
            bool compact = width < 1650f;
            float card = Mathf.Min(290f, width * 0.28f);
            Position(flight, new Vector2(0,1), new Vector2(0,1), new Vector2(margin,-top), new Vector2(card,145));
            Position(mission, Vector2.one, Vector2.one, new Vector2(-margin,-top), new Vector2(card,145));
            float compassWidth = Mathf.Min(400, width - 2 * card - 4 * margin);
            Position(compass,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-top),new Vector2(Mathf.Max(160,compassWidth),72));
            Position(warnings,new Vector2(.5f,1),new Vector2(.5f,1),new Vector2(0,-top-82),new Vector2(Mathf.Min(400,compassWidth),36));
            // Sur écran étroit, le bloc secondaire monte sous les cartes pour libérer les pouces.
            Position(dock,new Vector2(.5f,compact?1:0),new Vector2(.5f,compact?1:0),
                new Vector2(0,compact?-top-160:margin),new Vector2(Mathf.Min(520,width-2*margin),150));
            Position(navigation,Vector2.one,Vector2.one,new Vector2(-margin,-top-(compact?325:160)),new Vector2(Mathf.Min(250,width*.28f),108));
            Position(message,new Vector2(.5f,.23f),new Vector2(.5f,.5f),Vector2.zero,new Vector2(Mathf.Min(720,width-2*margin),48));
        }
        private static void Position(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            if(rect==null)return;
            rect.anchorMin=rect.anchorMax=anchor;rect.pivot=pivot;rect.anchoredPosition=position;rect.sizeDelta=size;
        }
    }
}
