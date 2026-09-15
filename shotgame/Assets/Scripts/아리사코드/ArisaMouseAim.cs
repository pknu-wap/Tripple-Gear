using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public sealed class ArisaMouseAim : MonoBehaviour
{
    [SerializeField, KoreanLabel("대기 그래픽")] private Transform idleGraphic;
    [SerializeField, KoreanLabel("대기 그래픽 기본 왼쪽 방향")] private bool idleGraphicFacesLeftByDefault = true;
    [SerializeField, KoreanLabel("관성 그래픽")] private Transform inertiaGraphic;
    [SerializeField, KoreanLabel("관성 그래픽 기본 왼쪽 방향")] private bool inertiaGraphicFacesLeftByDefault;
    [SerializeField, KoreanLabel("왼쪽 관성 그래픽 위치 보정")] private Vector2 leftInertiaGraphicPositionOffset;
    [SerializeField, KoreanLabel("관성 먼지")] private Transform inertiaDust;
    [SerializeField, KoreanLabel("왼쪽 관성 먼지 위치 보정")] private Vector2 leftInertiaDustPositionOffset;
    [SerializeField, KoreanLabel("관성 머리 위치 보정")] private Vector2[] inertiaHeadOffsets = new Vector2[4];
    [SerializeField, KoreanLabel("관성 조준 위치 보정")] private Vector2[] inertiaAimOffsets = new Vector2[4];
    [SerializeField, KoreanLabel("앉기 몸 위치 보정")] private Vector2[] crouchBodyOffsets =
    {
        new Vector2(0f, -0.02f),
        new Vector2(0f, -0.05f),
    };
    [SerializeField, KoreanLabel("앉기 몸 크기 배율")] private float[] crouchBodyScaleMultipliers =
    {
        0.92f,
        0.84f,
    };
    [SerializeField, KoreanLabel("앉기 머리 위치 보정")] private Vector2[] crouchHeadOffsets =
    {
        new Vector2(0.03f, -0.22f),
        new Vector2(0.08f, -0.42f),
    };
    [SerializeField, KoreanLabel("앉기 조준 위치 보정")] private Vector2[] crouchAimOffsets =
    {
        new Vector2(0.05f, -0.18f),
        new Vector2(0.13f, -0.36f),
    };
    [SerializeField, KoreanLabel("입력 소스")] private ArisaHorizontalMovement inputSource;
    [SerializeField, KoreanLabel("몸 그래픽")] private Transform bodyGraphic;
    [SerializeField, KoreanLabel("반대 팔 회전축")] private Transform oppositeArmPivot;
    [SerializeField, KoreanLabel("반대 팔 그래픽")] private Transform oppositeArmGraphic;
    [SerializeField, KoreanLabel("머리 회전축")] private Transform headPivot;
    [SerializeField, KoreanLabel("머리 그래픽")] private Transform headGraphic;
    [SerializeField, KoreanLabel("조준 회전축")] private Transform aimPivot;
    [SerializeField, KoreanLabel("조준 그래픽")] private Transform aimGraphic;
    [SerializeField, KoreanLabel("대상 카메라")] private Camera targetCamera;
    [SerializeField, KoreanLabel("머리 각도 보정")] private float headAngleOffset;
    [SerializeField, KoreanLabel("조준 각도 보정")] private float aimAngleOffset = 30f;
    [SerializeField, KoreanLabel("반대 팔 각도 보정")] private float oppositeArmAngleOffset;
    [SerializeField, KoreanLabel("좌우 반전 판정 여유")] private float flipDeadZone = 0.05f;

    private const float MinDirectionSqrMagnitude = 0.0001f;
    private const float MinFireShakeCycles = 0.5f;

    private Vector3 bodyBasePosition;
    private Vector3 inertiaGraphicBasePosition;
    private Vector3 inertiaDustBasePosition;
    private Vector3 oppositeArmPivotBasePosition;
    private Vector3 oppositeArmBasePosition;
    private Vector3 headPivotBasePosition;
    private Vector3 headGraphicBasePosition;
    private Vector3 aimPivotBasePosition;
    private Vector3 aimGraphicBasePosition;

    private Quaternion bodyBaseRotation;
    private Quaternion oppositeArmPivotBaseRotation;
    private Quaternion oppositeArmBaseRotation;
    private Quaternion headPivotBaseRotation;
    private Quaternion headGraphicBaseRotation;
    private Quaternion aimPivotBaseRotation;
    private Quaternion aimGraphicBaseRotation;

    private Vector3 bodyBaseScale;
    private Vector3 idleGraphicBaseScale;
    private Vector3 inertiaGraphicBaseScale;
    private Vector3 inertiaDustBaseScale;
    private Vector3 oppositeArmBaseScale;
    private Vector3 headGraphicBaseScale;
    private Vector3 aimGraphicBaseScale;

    private bool facingLeft;
    private float fireShakeStartedAt = float.NegativeInfinity;
    private float fireShakeAngle;
    private float fireShakeDuration;
    private float fireShakeCycles = MinFireShakeCycles;

    public bool IsFacingLeft => facingLeft;

    public void PlayFireShake(float angle, float duration, float cycles)
    {
        fireShakeAngle = Mathf.Max(0f, angle);
        fireShakeDuration = Mathf.Max(0f, duration);
        fireShakeCycles = Mathf.Max(MinFireShakeCycles, cycles);
        fireShakeStartedAt = Time.time;
    }

    public float MirrorLocalX(float localX)
    {
        float flipCenterX = GetFlipCenterX();
        return (flipCenterX * 2f) - localX;
    }

    private void Reset()
    {
        FindParts();
        CacheBasePose();
    }

    private void Awake()
    {
        FindParts();
        CacheBasePose();
    }

    private void LateUpdate()
    {
        Camera cameraToUse = targetCamera != null ? targetCamera : Camera.main;
        if (cameraToUse == null || !TryGetCurrentMouseScreenPosition(out Vector2 mouseScreenPosition))
        {
            return;
        }

        float targetDepth = aimPivot != null ? aimPivot.position.z : transform.position.z;
        float cameraDistance = Mathf.Abs(targetDepth - cameraToUse.transform.position.z);
        Vector3 mouseWorldPosition = cameraToUse.ScreenToWorldPoint(
            new Vector3(mouseScreenPosition.x, mouseScreenPosition.y, cameraDistance)
        );
        Vector3 mouseLocalPosition = transform.InverseTransformPoint(mouseWorldPosition);

        UpdateFacing(mouseLocalPosition);
        ApplyIdleFacing();
        ApplyInertiaFacing();
        ApplyInertiaDustFacing();
        ApplyBodyFacing(GetCrouchPoseOffset(crouchBodyOffsets), bodyBaseScale * GetCrouchBodyScaleMultiplier());
        Vector3 headPoseOffset = GetInertiaPoseOffset(inertiaHeadOffsets) + GetCrouchPoseOffset(crouchHeadOffsets);
        Vector3 aimPoseOffset = GetInertiaPoseOffset(inertiaAimOffsets) + GetCrouchPoseOffset(crouchAimOffsets);
        float fireShakeOffset = GetFireShakeAngleOffset();
        RotateGraphicToward(
            headPivot,
            headGraphic,
            headPivotBasePosition + headPoseOffset,
            headGraphicBasePosition + headPoseOffset,
            headPivotBaseRotation,
            headGraphicBaseRotation,
            headGraphicBaseScale,
            mouseLocalPosition,
            headAngleOffset
        );
        RotateGraphicToward(
            oppositeArmPivot,
            oppositeArmGraphic,
            oppositeArmPivotBasePosition + aimPoseOffset,
            oppositeArmBasePosition + aimPoseOffset,
            oppositeArmPivotBaseRotation,
            oppositeArmBaseRotation,
            oppositeArmBaseScale,
            mouseLocalPosition,
            oppositeArmAngleOffset + fireShakeOffset
        );
        RotateGraphicToward(
            aimPivot,
            aimGraphic,
            aimPivotBasePosition + aimPoseOffset,
            aimGraphicBasePosition + aimPoseOffset,
            aimPivotBaseRotation,
            aimGraphicBaseRotation,
            aimGraphicBaseScale,
            mouseLocalPosition,
            aimAngleOffset + fireShakeOffset
        );
    }

    private void FindParts()
    {
        if (inputSource == null)
        {
            inputSource = GetComponent<ArisaHorizontalMovement>();
        }

        if (idleGraphic == null)
        {
            idleGraphic = transform.Find("IdleSprite");
        }

        if (inertiaGraphic == null)
        {
            inertiaGraphic = transform.Find("InertiaSprite");
        }

        if (inertiaDust == null)
        {
            inertiaDust = transform.Find("InertiaDust");
        }

        if (bodyGraphic == null)
        {
            bodyGraphic = transform.Find("BodyLegs");
        }

        if (oppositeArmGraphic == null)
        {
            oppositeArmGraphic = transform.Find("OppositeArm");
        }

        if (oppositeArmPivot == null)
        {
            oppositeArmPivot = transform.Find("OppositeArmPIVOT");
        }

        if (headPivot == null)
        {
            headPivot = transform.Find("HeadPivot");
        }

        if (headGraphic == null)
        {
            headGraphic = transform.Find("Head");
        }

        if (aimPivot == null)
        {
            aimPivot = transform.Find("AimPivot");
        }

        if (aimGraphic == null)
        {
            aimGraphic = transform.Find("ArmsGun");
        }
    }

    private void CacheBasePose()
    {
        bodyBasePosition = GetLocalPosition(bodyGraphic);
        inertiaGraphicBasePosition = GetLocalPosition(inertiaGraphic);
        inertiaDustBasePosition = GetLocalPosition(inertiaDust);
        oppositeArmPivotBasePosition = GetLocalPosition(oppositeArmPivot);
        oppositeArmBasePosition = GetLocalPosition(oppositeArmGraphic);
        headPivotBasePosition = GetLocalPosition(headPivot);
        headGraphicBasePosition = GetLocalPosition(headGraphic);
        aimPivotBasePosition = GetLocalPosition(aimPivot);
        aimGraphicBasePosition = GetLocalPosition(aimGraphic);

        bodyBaseRotation = GetLocalRotation(bodyGraphic);
        oppositeArmPivotBaseRotation = GetLocalRotation(oppositeArmPivot);
        oppositeArmBaseRotation = GetLocalRotation(oppositeArmGraphic);
        headPivotBaseRotation = GetLocalRotation(headPivot);
        headGraphicBaseRotation = GetLocalRotation(headGraphic);
        aimPivotBaseRotation = GetLocalRotation(aimPivot);
        aimGraphicBaseRotation = GetLocalRotation(aimGraphic);

        bodyBaseScale = GetLocalScale(bodyGraphic);
        idleGraphicBaseScale = GetLocalScale(idleGraphic);
        inertiaGraphicBaseScale = GetLocalScale(inertiaGraphic);
        inertiaDustBaseScale = GetLocalScale(inertiaDust);
        oppositeArmBaseScale = GetLocalScale(oppositeArmGraphic);
        headGraphicBaseScale = GetLocalScale(headGraphic);
        aimGraphicBaseScale = GetLocalScale(aimGraphic);
    }

    private void UpdateFacing(Vector3 targetLocalPosition)
    {
        float targetX = targetLocalPosition.x - GetFlipCenterX();
        if (targetX > flipDeadZone)
        {
            facingLeft = false;
        }
        else if (targetX < -flipDeadZone)
        {
            facingLeft = true;
        }
    }

    private void ApplyBodyFacing(Vector3 bodyPoseOffset, Vector3 bodyPoseScale)
    {
        ApplyMirroredPose(bodyGraphic, bodyBasePosition + bodyPoseOffset, bodyBaseRotation, bodyPoseScale);
    }

    private void ApplyIdleFacing()
    {
        if (idleGraphic == null)
        {
            return;
        }

        bool flipIdleScale = idleGraphicFacesLeftByDefault ? !facingLeft : facingLeft;
        idleGraphic.localScale = MirrorScale(idleGraphicBaseScale, flipIdleScale);
    }

    private void ApplyInertiaFacing()
    {
        if (inertiaGraphic == null)
        {
            return;
        }

        bool flipInertiaScale = inertiaGraphicFacesLeftByDefault ? !facingLeft : facingLeft;
        inertiaGraphic.localPosition = GetInertiaGraphicPosition();
        inertiaGraphic.localScale = MirrorScale(inertiaGraphicBaseScale, flipInertiaScale);
    }

    private void ApplyInertiaDustFacing()
    {
        if (inertiaDust == null)
        {
            return;
        }

        inertiaDust.localPosition = GetInertiaDustPosition();
        inertiaDust.localScale = MirrorScale(inertiaDustBaseScale);
    }

    private Vector3 GetInertiaGraphicPosition()
    {
        if (!facingLeft)
        {
            return inertiaGraphicBasePosition;
        }

        Vector3 leftPosition = MirrorPosition(inertiaGraphicBasePosition);
        leftPosition += new Vector3(leftInertiaGraphicPositionOffset.x, leftInertiaGraphicPositionOffset.y, 0f);
        return leftPosition;
    }

    private Vector3 GetInertiaDustPosition()
    {
        if (!facingLeft)
        {
            return inertiaDustBasePosition;
        }

        Vector3 leftPosition = MirrorPosition(inertiaDustBasePosition);
        leftPosition += new Vector3(leftInertiaDustPositionOffset.x, leftInertiaDustPositionOffset.y, 0f);
        return leftPosition;
    }

    private Vector3 GetInertiaPoseOffset(Vector2[] offsets)
    {
        if (inputSource == null || !inputSource.IsCoasting || offsets == null || offsets.Length == 0)
        {
            return Vector3.zero;
        }

        int frameIndex = Mathf.Clamp(inputSource.InertiaFrameIndex, 0, offsets.Length - 1);
        Vector2 offset = offsets[frameIndex];
        return new Vector3(offset.x, offset.y, 0f);
    }

    private Vector3 GetCrouchPoseOffset(Vector2[] offsets)
    {
        if (inputSource == null || !inputSource.IsCrouching || offsets == null || offsets.Length == 0)
        {
            return Vector3.zero;
        }

        int frameIndex = Mathf.Clamp(inputSource.CrouchFrameIndex, 0, offsets.Length - 1);
        Vector2 offset = offsets[frameIndex];
        return new Vector3(offset.x, offset.y, 0f);
    }

    private float GetCrouchBodyScaleMultiplier()
    {
        if (inputSource == null || !inputSource.IsCrouching || crouchBodyScaleMultipliers == null || crouchBodyScaleMultipliers.Length == 0)
        {
            return 1f;
        }

        int frameIndex = Mathf.Clamp(inputSource.CrouchFrameIndex, 0, crouchBodyScaleMultipliers.Length - 1);
        return Mathf.Max(0f, crouchBodyScaleMultipliers[frameIndex]);
    }

    private float GetFireShakeAngleOffset()
    {
        if (fireShakeDuration <= 0f || fireShakeAngle <= 0f)
        {
            return 0f;
        }

        float elapsed = Time.time - fireShakeStartedAt;
        if (elapsed < 0f || elapsed > fireShakeDuration)
        {
            return 0f;
        }

        float progress = Mathf.Clamp01(elapsed / fireShakeDuration);
        float decay = 1f - progress;
        float wave = Mathf.Cos(progress * Mathf.PI * 2f * fireShakeCycles);
        return fireShakeAngle * decay * wave;
    }

    private static Vector3 GetLocalPosition(Transform target)
    {
        return target != null ? target.localPosition : Vector3.zero;
    }

    private static Quaternion GetLocalRotation(Transform target)
    {
        return target != null ? target.localRotation : Quaternion.identity;
    }

    private static Vector3 GetLocalScale(Transform target)
    {
        return target != null ? target.localScale : Vector3.one;
    }

    private void ApplyMirroredPose(
        Transform graphic,
        Vector3 basePosition,
        Quaternion baseRotation,
        Vector3 baseScale
    )
    {
        if (graphic == null)
        {
            return;
        }

        graphic.localPosition = MirrorPosition(basePosition);
        graphic.localRotation = baseRotation;
        graphic.localScale = MirrorScale(baseScale);
    }

    private void RotateGraphicToward(
        Transform pivot,
        Transform graphic,
        Vector3 pivotBasePosition,
        Vector3 graphicBasePosition,
        Quaternion pivotBaseRotation,
        Quaternion graphicBaseRotation,
        Vector3 graphicBaseScale,
        Vector3 targetLocalPosition,
        float angleOffset
    )
    {
        if (pivot == null)
        {
            return;
        }

        Vector3 pivotPosition = MirrorPosition(pivotBasePosition);
        Vector2 direction = targetLocalPosition - pivotPosition;
        if (direction.sqrMagnitude < MinDirectionSqrMagnitude)
        {
            return;
        }

        if (facingLeft)
        {
            direction = -direction;
        }

        float signedAngleOffset = facingLeft ? -angleOffset : angleOffset;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg + signedAngleOffset;
        Quaternion targetRotation = Quaternion.Euler(0f, 0f, angle);
        pivot.localPosition = pivotPosition;
        pivot.localRotation = targetRotation * pivotBaseRotation;

        if (graphic == null)
        {
            return;
        }

        Vector3 graphicOffset = MirrorVector(graphicBasePosition - pivotBasePosition);
        graphic.localPosition = pivotPosition + targetRotation * graphicOffset;
        graphic.localRotation = targetRotation * graphicBaseRotation;
        graphic.localScale = MirrorScale(graphicBaseScale);
    }

    private Vector3 MirrorPosition(Vector3 position)
    {
        if (!facingLeft)
        {
            return position;
        }

        float flipCenterX = GetFlipCenterX();
        return new Vector3(MirrorLocalX(position.x), position.y, position.z);
    }

    private Vector3 MirrorVector(Vector3 vector)
    {
        return facingLeft ? new Vector3(-vector.x, vector.y, vector.z) : vector;
    }

    private Vector3 MirrorScale(Vector3 scale)
    {
        return MirrorScale(scale, facingLeft);
    }

    private static Vector3 MirrorScale(Vector3 scale, bool flipScaleX)
    {
        scale.x = Mathf.Abs(scale.x) * (flipScaleX ? -1f : 1f);
        return scale;
    }

    private float GetFlipCenterX()
    {
        return bodyGraphic != null ? bodyBasePosition.x : 0f;
    }

    private bool TryGetCurrentMouseScreenPosition(out Vector2 screenPosition)
    {
        if (inputSource != null && inputSource.TryGetMouseScreenPosition(out screenPosition))
        {
            return true;
        }

        return TryReadMouseScreenPosition(out screenPosition);
    }

    private static bool TryReadMouseScreenPosition(out Vector2 screenPosition)
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            screenPosition = Mouse.current.position.ReadValue();
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        screenPosition = Input.mousePosition;
        return true;
#else
        screenPosition = default;
        return false;
#endif
    }
}
