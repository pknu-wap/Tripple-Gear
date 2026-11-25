using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[AddComponentMenu("")]
public sealed class GunRewardCardButton : Button
{
    private const float HoverScale = 1.14f;
    private const float ZoomSpeed = 0.9f;

    private RectTransform artwork;
    private bool pointerOver;
    private float artworkScale = 1f;

    internal void SetArtwork(RectTransform imageRect) => artwork = imageRect;

    public override void OnPointerEnter(PointerEventData eventData)
    {
        base.OnPointerEnter(eventData);
        pointerOver = true;
    }

    public override void OnPointerExit(PointerEventData eventData)
    {
        base.OnPointerExit(eventData);
        pointerOver = false;
    }

    private void Update()
    {
        float targetScale = pointerOver && IsInteractable() ? HoverScale : 1f;
        if (artwork == null || artworkScale == targetScale)
        {
            return;
        }

        artworkScale = Mathf.MoveTowards(artworkScale, targetScale, ZoomSpeed * Time.unscaledDeltaTime);
        artwork.localScale = Vector3.one * artworkScale;
    }
}
