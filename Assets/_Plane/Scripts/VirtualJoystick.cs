using UnityEngine;
using UnityEngine.EventSystems;

namespace Project.Plane
{
    /// <summary>Un seul doigt propriétaire par joystick ; les autres contrôles gardent leurs propres doigts.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(RectTransform))]
    public sealed class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform baseRect = null;
        [SerializeField] private RectTransform handle = null;
        [SerializeField, Min(1f), Tooltip("Rayon en unités du Canvas, indépendant de sa résolution écran.")]
        private float radius = 85f;
        [SerializeField, Range(0f, 0.95f)] private float deadZone = 0.06f;
        [SerializeField, Min(0f)] private float returnSpeed = 14f;
        [SerializeField] private bool floating = false;
        private RectTransform area;
        private int? pointer;
        public Vector2 Value { get; private set; }
        private void Awake() => area = (RectTransform)transform;
        public void OnPointerDown(PointerEventData eventData)
        {
            if (pointer.HasValue || baseRect == null || handle == null) return;
            if (area == null) area = (RectTransform)transform;
            pointer = eventData.pointerId;
            if (floating && RectTransformUtility.ScreenPointToLocalPointInRectangle(area, eventData.position, eventData.pressEventCamera, out Vector2 point))
            {
                Rect rect = area.rect;
                float x = Mathf.Min(radius, rect.width * 0.5f), y = Mathf.Min(radius, rect.height * 0.5f);
                baseRect.localPosition = new Vector3(Mathf.Clamp(point.x, rect.xMin + x, rect.xMax - x),
                    Mathf.Clamp(point.y, rect.yMin + y, rect.yMax - y), 0f);
            }
            OnDrag(eventData);
        }
        public void OnDrag(PointerEventData eventData)
        {
            if (pointer != eventData.pointerId || baseRect == null || handle == null) return;
            // La conversion locale prend en compte Canvas Scaler et la caméra de l'événement.
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(baseRect, eventData.position, eventData.pressEventCamera, out Vector2 point)) return;
            Vector2 raw = Vector2.ClampMagnitude(point / Mathf.Max(1f, radius), 1f);
            float magnitude = raw.magnitude;
            Value = magnitude <= deadZone ? Vector2.zero : raw.normalized * ((magnitude - deadZone) / (1f - deadZone));
            handle.anchoredPosition = raw * radius;
        }
        public void OnPointerUp(PointerEventData eventData)
        { if (pointer == eventData.pointerId) Release(); }
        public void Release() { pointer = null; Value = Vector2.zero; }
        private void Update()
        {
            if (pointer.HasValue || handle == null) return;
            float t = returnSpeed <= 0f ? 1f : 1f - Mathf.Exp(-returnSpeed * Time.unscaledDeltaTime);
            handle.anchoredPosition = Vector2.Lerp(handle.anchoredPosition, Vector2.zero, t);
        }
        private void OnDisable()
        { Release(); if (handle != null) handle.anchoredPosition = Vector2.zero; }
#if UNITY_EDITOR
        public void Configure(RectTransform background, RectTransform knob) { baseRect = background; handle = knob; }
#endif
    }
}
