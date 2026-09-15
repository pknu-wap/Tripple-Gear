using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class ArisaStanceCollider : MonoBehaviour
{
    [SerializeField, KoreanLabel("이동 컴포넌트")] private ArisaHorizontalMovement movement;
    [SerializeField, KoreanLabel("조준 컴포넌트")] private ArisaMouseAim aim;
    [SerializeField, KoreanLabel("대상 충돌체")] private BoxCollider2D targetCollider;
    [SerializeField, KoreanLabel("서기 충돌체 크기")] private Vector2 standingSize = new Vector2(0.9f, 2.65f);
    [SerializeField, KoreanLabel("서기 충돌체 위치")] private Vector2 standingOffset = new Vector2(-0.15f, -1.05f);
    [SerializeField, KoreanLabel("앉기 1프레임 충돌체 크기")] private Vector2 crouchFrame1Size = new Vector2(1.15f, 1.55f);
    [SerializeField, KoreanLabel("앉기 1프레임 충돌체 위치")] private Vector2 crouchFrame1Offset = new Vector2(-0.05f, -1.35f);
    [SerializeField, KoreanLabel("왼쪽 앉기 1프레임 위치 보정")] private Vector2 leftCrouchFrame1OffsetAdjustment;
    [SerializeField, KoreanLabel("앉기 2프레임 충돌체 크기")] private Vector2 crouchFrame2Size = new Vector2(1.35f, 1.15f);
    [SerializeField, KoreanLabel("앉기 2프레임 충돌체 위치")] private Vector2 crouchFrame2Offset = new Vector2(0.05f, -1.5f);
    [SerializeField, KoreanLabel("왼쪽 앉기 2프레임 위치 보정")] private Vector2 leftCrouchFrame2OffsetAdjustment;

    private void Reset()
    {
        FindReferences();
        ApplyCurrentStance();
    }

    private void Awake()
    {
        FindReferences();
        ApplyCurrentStance();
    }

    private void LateUpdate()
    {
        ApplyCurrentStance();
    }

    private void OnValidate()
    {
        FindReferences();
        ApplyCurrentStance();
    }

    private void FindReferences()
    {
        if (movement == null)
        {
            movement = GetComponent<ArisaHorizontalMovement>();
        }

        if (targetCollider == null)
        {
            targetCollider = GetComponent<BoxCollider2D>();
        }

        if (aim == null)
        {
            aim = GetComponent<ArisaMouseAim>();
        }
    }

    public void ApplyCurrentStance()
    {
        if (targetCollider == null)
        {
            return;
        }

        if (movement == null || !movement.IsCrouching)
        {
            ApplyCollider(standingSize, standingOffset);
            return;
        }

        if (movement.CrouchFrameIndex <= 0)
        {
            ApplyCollider(crouchFrame1Size, GetCrouchOffset(crouchFrame1Offset, leftCrouchFrame1OffsetAdjustment));
            return;
        }

        ApplyCollider(crouchFrame2Size, GetCrouchOffset(crouchFrame2Offset, leftCrouchFrame2OffsetAdjustment));
    }

    private void ApplyCollider(Vector2 size, Vector2 offset)
    {
        targetCollider.size = size;
        targetCollider.offset = offset;
    }

    private Vector2 GetCrouchOffset(Vector2 rightOffset, Vector2 leftAdjustment)
    {
        if (aim == null || !aim.IsFacingLeft)
        {
            return rightOffset;
        }

        return new Vector2(aim.MirrorLocalX(rightOffset.x), rightOffset.y) + leftAdjustment;
    }
}
