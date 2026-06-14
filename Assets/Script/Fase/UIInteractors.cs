using UnityEngine;
using UnityEngine.EventSystems;

namespace FaseLucasGame
{
    /// <summary>Drags a target RectTransform when this element is dragged (used for node bodies/headers).</summary>
    public class UIDragHandle : MonoBehaviour, IDragHandler
    {
        public RectTransform target;
        public RectTransform bounds;

        public void OnDrag(PointerEventData e)
        {
            if (target == null) return;
            // The graph can be zoomed, so convert the screen-space pointer delta into the
            // content's local units; otherwise nodes would "run away" from the cursor when zoomed.
            float scale = target.lossyScale.x;
            if (scale <= 0.0001f) scale = 1f;
            target.anchoredPosition += e.delta / scale;
        }
    }

    /// <summary>
    /// Pans and zooms a content RectTransform inside a viewport. Attach to the viewport
    /// (the masked background) and point <see cref="content"/> at the graph root. Dragging
    /// empty background pans; the mouse wheel zooms toward the cursor.
    /// </summary>
    public class GraphViewController : MonoBehaviour, IDragHandler, IScrollHandler
    {
        public RectTransform content;    // the pannable/zoomable node root
        public RectTransform viewport;   // the masked area the content lives in
        public float minZoom = 0.2f;
        public float maxZoom = 2.5f;
        public float zoomSpeed = 0.12f;

        public void OnDrag(PointerEventData e)
        {
            if (content == null) return;
            content.anchoredPosition += e.delta;   // 1:1 with the cursor (canvas scaleFactor = 1)
        }

        public void OnScroll(PointerEventData e)
        {
            if (content == null || viewport == null) return;

            float s = content.localScale.x;
            float target = Mathf.Clamp(s * (1f + e.scrollDelta.y * zoomSpeed), minZoom, maxZoom);
            if (Mathf.Approximately(target, s)) return;

            // Keep the graph point under the cursor anchored while zooming.
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                viewport, e.position, e.pressEventCamera, out Vector2 local);
            Vector2 m = local - viewport.rect.min;           // cursor measured from the viewport's bottom-left
            Vector2 o = content.anchoredPosition;
            content.anchoredPosition = m - (target / s) * (m - o);
            content.localScale = new Vector3(target, target, 1f);
        }
    }

    /// <summary>A clickable connection port on a node.</summary>
    public class UIPort : MonoBehaviour, IPointerClickHandler
    {
        public MagnetProgramUI owner;
        public ProgramNode node;
        public int inputIndex;   // valid when !isOutput
        public bool isOutput;

        public void OnPointerClick(PointerEventData e)
        {
            if (owner != null)
                owner.OnPortClicked(node, inputIndex, isOutput);
        }
    }
}
