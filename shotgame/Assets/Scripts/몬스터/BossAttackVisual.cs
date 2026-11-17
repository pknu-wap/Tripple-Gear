using UnityEngine;

[DisallowMultipleComponent]
public sealed class BossAttackVisual : MonoBehaviour
{
    [SerializeField] private SpriteRenderer body;
    [SerializeField] private Animator idleAnimator;
    [SerializeField] private SpriteRenderer dockCharge;
    [SerializeField] private Sprite[] rainFrames;
    [SerializeField] private Sprite[] horizontalFrames;
    [SerializeField] private Sprite[] summonFrames;

    private Sprite idleSprite;
    private bool showingPose;

    public Vector3 RainMuzzle => BodyPoint(new Vector2(0.18f, 0.90f));
    public Vector3 ChestMuzzle => BodyPoint(new Vector2(0.56f, -0.50f));
    public Vector3 HangarExit => BodyPoint(new Vector2(0.31f, 0.12f));

    public void ShowInFrontOfBody(SpriteRenderer renderer)
    {
        if (body == null || renderer == null) return;
        bool sameLayer = renderer.sortingLayerID == body.sortingLayerID;
        renderer.sortingLayerID = body.sortingLayerID;
        renderer.sortingOrder = sameLayer
            ? Mathf.Max(renderer.sortingOrder, body.sortingOrder + 1)
            : body.sortingOrder + 1;
    }

    public void ShowPose(BossAttackPattern pattern, float opening)
    {
        Sprite[] frames = pattern switch
        {
            BossAttackPattern.MissileRain => rainFrames,
            BossAttackPattern.HorizontalMissiles => horizontalFrames,
            BossAttackPattern.Summon => summonFrames,
            _ => null,
        };
        if (body == null || frames == null || frames.Length == 0) return;
        if (!showingPose)
        {
            idleSprite = body.sprite;
            showingPose = true;
            if (idleAnimator != null) idleAnimator.enabled = false;
        }

        int index = Mathf.CeilToInt(Mathf.Clamp01(opening) * frames.Length) - 1;
        body.sprite = index < 0 ? idleSprite : frames[index];
    }

    public void SetLaserCharge(float intensity)
    {
        if (dockCharge == null) return;
        dockCharge.enabled = intensity > 0f;
        Color color = dockCharge.color;
        color.a = Mathf.Clamp01(intensity);
        dockCharge.color = color;
    }

    public void ResetPose()
    {
        if (showingPose)
        {
            body.sprite = idleSprite;
            if (idleAnimator != null) idleAnimator.enabled = true;
            showingPose = false;
        }
        SetLaserCharge(0f);
    }

    private Vector3 BodyPoint(Vector2 point)
    {
        if (body == null) return transform.position;
        // Coordinates are relative to the body renderer pivot in sprite units.
        if (body.flipX) point.x = -point.x;
        return body.transform.TransformPoint(point);
    }
}
