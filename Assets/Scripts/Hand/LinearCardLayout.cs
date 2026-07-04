using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

public class LinearCardLayout
{
    private readonly Transform anchor;
    private readonly float spacing;
    private readonly float margin;
    private readonly bool centerOnSafeArea;
    private readonly bool rightAnchored;

    public float ScrollOffset { get; set; }
    public bool Mirrored { get; set; }
    public bool CanScroll { get; private set; }

    public LinearCardLayout(Transform anchor, float spacing, float margin = 0f, bool centerOnSafeArea = false, bool rightAnchored = false)
    {
        this.anchor = anchor;
        this.spacing = spacing;
        this.margin = margin;
        this.centerOnSafeArea = centerOnSafeArea;
        this.rightAnchored = rightAnchored;
    }

    public void PlaceCards(IReadOnlyList<Card> cards, bool instant = false)
    {
        if (anchor == null)
        {
            Debug.LogError("[LinearCardLayout] anchor is null — wire the anchor field in the Inspector.");
            return;
        }

        int count = cards.Count;
        if (count == 0)
        {
            CanScroll = false;
            return;
        }

        RectTransform anchorRT = anchor as RectTransform;
        if (anchorRT == null)
        {
            Debug.LogError("[LinearCardLayout] anchor must be a RectTransform in Canvas mode.");
            return;
        }

        Canvas rootCanvas = anchorRT.GetComponentInParent<Canvas>();
        if (rootCanvas != null) rootCanvas = rootCanvas.rootCanvas;
        RectTransform canvasRT = rootCanvas != null ? rootCanvas.GetComponent<RectTransform>() : null;
        Camera uiCam = rootCanvas != null ? rootCanvas.worldCamera : null;

        RectTransform cardParentRT = cards[0].GetComponent<RectTransform>().parent as RectTransform;
        float leftBound, rightBound;
        RectTransform refRT = cardParentRT ?? canvasRT ?? anchorRT.parent as RectTransform;
        if (refRT != null)
        {
            Rect sa = Screen.safeArea;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(refRT, new Vector2(sa.xMin, sa.yMin), uiCam, out Vector2 saMin);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(refRT, new Vector2(sa.xMax, sa.yMax), uiCam, out Vector2 saMax);
            leftBound  = saMin.x + margin;
            rightBound = saMax.x - margin;
        }
        else
        {
            leftBound  = anchorRT.anchoredPosition.x - 500f;
            rightBound = anchorRT.anchoredPosition.x + 500f;
        }

        float available  = rightBound - leftBound;
        Vector2 anchorInRef = refRT != null ? (Vector2)refRT.InverseTransformPoint(anchorRT.position) : (Vector2)anchorRT.localPosition;
        float anchorLocalX = anchorInRef.x;
        float anchorLocalY = anchorInRef.y;
        float centerX    = centerOnSafeArea ? (leftBound + rightBound) / 2f : anchorLocalX;
        float totalWidth = (count - 1) * spacing;

        float rightEdge = rightAnchored ? anchorLocalX : (totalWidth <= available ? centerX + totalWidth / 2f : rightBound);
        float baseX = rightEdge - totalWidth;

        float maxScroll = centerOnSafeArea ? Mathf.Max(0f, totalWidth - available) : 0f;
        float scroll = Mathf.Clamp(ScrollOffset, 0f, maxScroll);
        ScrollOffset = scroll;
        CanScroll = maxScroll > 0f;

        for (int i = 0; i < count; i++)
        {
            if (cards[i].IsDragging) continue;

            RectTransform cardRT = cards[i].GetComponent<RectTransform>();
            if (cardRT == null) continue;

            int posIdx = Mirrored ? (count - 1 - i) : i;
            float naturalX = baseX + posIdx * spacing + scroll;
            float cardX = centerOnSafeArea ? Mathf.Clamp(naturalX, leftBound, rightBound) : naturalX;

            Vector2 pos = new Vector2(cardX, anchorLocalY);
            Quaternion rot = anchorRT.localRotation;

            string tweenId = "layout:" + cardRT.GetInstanceID();
            DOTween.Kill(tweenId);
            if (instant)
            {
                cardRT.anchoredPosition = pos;
                cardRT.localRotation = rot;
            }
            else
            {
                cardRT.DOAnchorPos(pos, 0.25f).SetId(tweenId);
                cardRT.DOLocalRotateQuaternion(rot, 0.25f).SetId(tweenId);
            }

            cards[i].SetSortingOrder(i + 1);
            bool isTop = i == count - 1;
            cards[i].SetHorizontal(isTop);
        }
    }
}
