using UnityEngine;
using UnityEngine.EventSystems;

namespace Project.Plane
{
    /// <summary>Manette des gaz verticale persistante ; un doigt peut la régler pendant qu'un autre pilote.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class ThrottleSlider : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform handle = null;
        [SerializeField, Range(0f, 1f)] private float value = 0f;
        private int? pointer;
        public float Value => Mathf.Clamp01(value);
        private void OnEnable() => SetValue(value);
        public void SetValue(float newValue)
        {
            value = Mathf.Clamp01(newValue);
            if (handle == null) return;
            handle.anchorMin = handle.anchorMax = new Vector2(0.5f, value);
            handle.anchoredPosition = Vector2.zero;
        }
        public void OnPointerDown(PointerEventData eventData)
        {
            if (pointer.HasValue) return;
            pointer = eventData.pointerId;
            OnDrag(eventData);
        }
        public void OnDrag(PointerEventData eventData)
        {
            if (pointer != eventData.pointerId) return;
            var track = (RectTransform)transform;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(track, eventData.position, eventData.pressEventCamera, out Vector2 local))
                SetValue(Mathf.InverseLerp(track.rect.yMin, track.rect.yMax, local.y));
        }
        public void OnPointerUp(PointerEventData eventData)
        { if (pointer == eventData.pointerId) pointer = null; }
        private void OnDisable() => pointer = null;
#if UNITY_EDITOR
        public void Configure(RectTransform knob) { handle = knob; SetValue(value); }
#endif
    }
}
