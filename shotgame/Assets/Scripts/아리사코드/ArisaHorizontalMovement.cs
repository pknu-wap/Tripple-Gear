using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class ArisaHorizontalMovement : MonoBehaviour
{
    private static readonly int IsMovingParameter = Animator.StringToHash("IsMoving");
    private const string InertiaStateName = "Inertia";
    private const string DustStateName = "Dust";
    private const string WaveBoundaryLayerName = "WaveBoundary";
    private const float StopThreshold = 0.03f;

    [SerializeField, KoreanLabel("이동 속도")] private float moveSpeed = 3f;
    [SerializeField, KoreanLabel("착지 시 남길 최대 수평 속도"), Min(0f)] private float landingHorizontalSpeed = 0f;
    [SerializeField, KoreanLabel("가속도")] private float acceleration = 18f;
    [SerializeField, KoreanLabel("감속도")] private float deceleration = 12f;
    [SerializeField, KoreanLabel("점프 속도")] private float jumpSpeed = 6f;
    [SerializeField, KoreanLabel("중력")] private float gravity = 18f;
    [SerializeField, KoreanLabel("하강 중력 배율"), Min(1f)] private float fallGravityMultiplier = 1.7f;
    [SerializeField, KoreanLabel("관성 프레임 속도")] private float inertiaFrameRate = 12f;
    [SerializeField, KoreanLabel("관성 프레임 수")] private int inertiaFrameCount = 4;
    [SerializeField, KoreanLabel("애니메이터")] private Animator animator;
    [SerializeField, KoreanLabel("대기 스프라이트 렌더러")] private SpriteRenderer idleSpriteRenderer;
    [SerializeField, KoreanLabel("몸 스프라이트 렌더러")] private SpriteRenderer bodySpriteRenderer;
    [SerializeField, KoreanLabel("앉기 스프라이트")] private Sprite[] crouchSprites;
    [SerializeField, KoreanLabel("앉기 프레임 속도")] private float crouchFrameRate = 12f;
    [SerializeField, KoreanLabel("관성 스프라이트 렌더러")] private SpriteRenderer inertiaSpriteRenderer;
    [SerializeField, KoreanLabel("관성 애니메이터")] private Animator inertiaAnimator;
    [SerializeField, KoreanLabel("관성 먼지 스프라이트 렌더러")] private SpriteRenderer inertiaDustSpriteRenderer;
    [SerializeField, KoreanLabel("관성 먼지 애니메이터")] private Animator inertiaDustAnimator;
    [SerializeField, KoreanLabel("관성 오버레이 렌더러")] private SpriteRenderer[] inertiaOverlayRenderers;
    [SerializeField, KoreanLabel("이동 중 렌더러")] private SpriteRenderer[] movingSpriteRenderers;
    [SerializeField, KoreanLabel("이동 충돌체")] private BoxCollider2D movementCollider;
    private Rigidbody2D movementBody;
    private PhysicsMaterial2D frictionlessMaterial;
    [SerializeField, KoreanLabel("자세 충돌체")] private ArisaStanceCollider stanceCollider;
    [SerializeField, KoreanLabel("총 발사")] private SubmachineGunFire gunFire;
    [SerializeField, KoreanLabel("고정 발판 레이어")] private LayerMask solidPlatformLayers;
    [SerializeField, KoreanLabel("일방향 발판 레이어")] private LayerMask oneWayPlatformLayers;
    [SerializeField, KoreanLabel("트리거 충돌 포함")] private bool collisionIncludesTriggers;
    [SerializeField, KoreanLabel("충돌 여유 폭")] private float collisionSkinWidth = 0.02f;
    [SerializeField, KoreanLabel("지면 확인 거리")] private float groundedCheckDistance = 0.06f;
    [SerializeField, KoreanLabel("앉기 지면 확인 거리")] private float crouchGroundedCheckDistance = 0.3f;
    [SerializeField, KoreanLabel("앉기 지면 유지 시간")] private float crouchGroundedGraceTime = 0.08f;
    [SerializeField, KoreanLabel("일방향 발판 상단 허용 오차")] private float oneWayPlatformTopTolerance = 0.05f;

    [SerializeField, KoreanLabel("착지 스프라이트")] private Sprite[] landingSprites;
    [SerializeField, KoreanLabel("착지 프레임 속도"), Min(1f)] private float landingFrameRate = 16f;
    [SerializeField, KoreanLabel("착지 상체 위치 보정")] private Vector2[] landingUpperBodyOffsets;

    [SerializeField, KoreanLabel("점프 준비 시간"), Min(0f)] private float jumpPreparationDuration = 0.14f;
    [SerializeField, KoreanLabel("점프 준비 스프라이트")] private Sprite[] jumpPreparationSprites = System.Array.Empty<Sprite>();
    [SerializeField, KoreanLabel("점프 준비 상체 위치 보정")] private Vector2[] jumpPreparationUpperBodyOffsets = System.Array.Empty<Vector2>();
    private bool jumpPreparing;
    private float jumpPreparationStartedAt;

    public bool IsPreparingJump => enabled && jumpPreparing && isGrounded && !isCrouching;

    private int JumpPreparationFrameIndex => Mathf.Clamp(
        Mathf.FloorToInt((Time.time - jumpPreparationStartedAt)
            / Mathf.Max(0.001f, jumpPreparationDuration) * jumpPreparationSprites.Length),
        0, jumpPreparationSprites.Length - 1);

    public Vector3 JumpPreparationUpperBodyOffset => IsPreparingJump
        && jumpPreparationUpperBodyOffsets != null
        && JumpPreparationFrameIndex < jumpPreparationUpperBodyOffsets.Length
        ? (Vector3)jumpPreparationUpperBodyOffsets[JumpPreparationFrameIndex] : Vector3.zero;

    private float landingStartedAt = float.NegativeInfinity;
    private float airborneDuration;
    private Sprite spriteBeforeLanding;
    private bool landingSpriteApplied;

    public bool IsLanding => enabled && !jumpPreparing && isGrounded && !isCrouching
        && landingSprites != null && landingSprites.Length > 0
        && Time.time - landingStartedAt < landingSprites.Length / Mathf.Max(1f, landingFrameRate);

    private int LandingFrameIndex => Mathf.Clamp(
        Mathf.FloorToInt((Time.time - landingStartedAt) * Mathf.Max(1f, landingFrameRate)),
        0, landingSprites.Length - 1);

    public Vector3 LandingUpperBodyOffset => IsLanding && landingUpperBodyOffsets != null
        && LandingFrameIndex < landingUpperBodyOffsets.Length
        ? (Vector3)landingUpperBodyOffsets[LandingFrameIndex] : Vector3.zero;

    private float horizontalInput;
    private bool fireHeld;
    private bool jumpHeld;
    private bool jumpPressed;
    private bool crouchHeld;
    private bool hasMouseScreenPosition;
    private Vector2 mouseScreenPosition;
    private float currentHorizontalSpeed;
    private float hitReactionEndTime;
    private float hitKnockbackSpeed;
    private float verticalSpeed;
    private float groundY;
    private bool isGrounded = true;
    private bool isCrouching;
    private bool isCoasting;
    private float crouchStartedAt;
    private float inertiaStartedAt;
    private bool wasCoasting;
    private readonly RaycastHit2D[] movementCastHits = new RaycastHit2D[12];

    public bool IsCrouching => isCrouching;
    public bool IsCoasting => isCoasting;

    public int CrouchFrameIndex
    {
        get
        {
            if (!isCrouching || crouchSprites == null || crouchSprites.Length == 0)
            {
                return 0;
            }

            if (crouchFrameRate <= 0f)
            {
                return crouchSprites.Length - 1;
            }

            float elapsed = Mathf.Max(0f, Time.time - crouchStartedAt);
            int frameIndex = Mathf.FloorToInt(elapsed * crouchFrameRate);
            return Mathf.Clamp(frameIndex, 0, crouchSprites.Length - 1);
        }
    }

    public int InertiaFrameIndex
    {
        get
        {
            if (!isCoasting || inertiaFrameCount <= 0)
            {
                return 0;
            }

            float elapsed = Mathf.Max(0f, Time.time - inertiaStartedAt);
            return Mathf.FloorToInt(elapsed * inertiaFrameRate) % inertiaFrameCount;
        }
    }

    public bool TryGetMouseScreenPosition(out Vector2 screenPosition)
    {
        screenPosition = mouseScreenPosition;
        return hasMouseScreenPosition;
    }

    public void TeleportTo(Vector3 position)
    {
        if (movementBody != null)
        {
            movementBody.position = position;
            movementBody.linearVelocity = Vector2.zero;
        }

        transform.position = position;
        CancelJumpPreparation();
        CancelLanding();
        airborneDuration = 0f;
        groundY = position.y;
        currentHorizontalSpeed = 0f;
        hitReactionEndTime = 0f;
        hitKnockbackSpeed = 0f;
        verticalSpeed = 0f;
        jumpPressed = false;
        isGrounded = true;
        isCrouching = false;
        isCoasting = false;
        wasCoasting = false;
        ApplyStanceCollider();
        Physics2D.SyncTransforms();
    }

    public void StartHitReaction(float horizontalDirection, float duration, float distance, float upwardSpeed)
    {
        if (duration <= 0f)
        {
            return;
        }

        CancelJumpPreparation();
        CancelLanding();
        hitReactionEndTime = Time.time + duration;
        hitKnockbackSpeed = Mathf.Sign(horizontalDirection) * Mathf.Max(0f, distance) / duration;
        currentHorizontalSpeed = 0f;
        if (upwardSpeed > 0f)
        {
            verticalSpeed = Mathf.Max(verticalSpeed, upwardSpeed);
            if (movementBody != null)
            {
                Vector2 velocity = movementBody.linearVelocity;
                movementBody.linearVelocity = new Vector2(velocity.x, Mathf.Max(velocity.y, upwardSpeed));
            }
            isGrounded = false;
        }

        isCoasting = false;
        wasCoasting = false;
    }

    private void Reset()
    {
        FindReferences();
    }

    private void Awake()
    {
        FindReferences();
        if (movementBody != null)
        {
            movementBody.gravityScale = 0f;
        }

        if (movementCollider != null)
        {
            frictionlessMaterial = new PhysicsMaterial2D("Arisa Frictionless")
            {
                friction = 0f,
                bounciness = 0f,
            };
            movementCollider.sharedMaterial = frictionlessMaterial;
        }

        groundY = transform.position.y;
        isGrounded = true;
        SetMovingAnimation(false);
        SetRendererVisibility(false, false);
    }

    private void FindReferences()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (movementCollider == null)
        {
            movementCollider = GetComponent<BoxCollider2D>();
        }

        if (movementBody == null)
        {
            movementBody = GetComponent<Rigidbody2D>();
        }

        if (stanceCollider == null)
        {
            stanceCollider = GetComponent<ArisaStanceCollider>();
        }

        if (gunFire == null)
        {
            gunFire = GetComponent<SubmachineGunFire>();
        }

        AssignDefaultCollisionLayers();

        if (idleSpriteRenderer == null)
        {
            Transform idleSprite = transform.Find("IdleSprite");
            if (idleSprite != null)
            {
                idleSpriteRenderer = idleSprite.GetComponent<SpriteRenderer>();
            }
        }

        if (idleSpriteRenderer == null)
        {
            idleSpriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (idleSpriteRenderer == null)
        {
            idleSpriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }

        if (bodySpriteRenderer == null)
        {
            Transform bodySprite = transform.Find("BodyLegs");
            if (bodySprite != null)
            {
                bodySpriteRenderer = bodySprite.GetComponent<SpriteRenderer>();
            }
        }

        if (inertiaSpriteRenderer == null)
        {
            Transform inertiaSprite = transform.Find("InertiaSprite");
            if (inertiaSprite != null)
            {
                inertiaSpriteRenderer = inertiaSprite.GetComponent<SpriteRenderer>();
            }
        }

        if (inertiaAnimator == null && inertiaSpriteRenderer != null)
        {
            inertiaAnimator = inertiaSpriteRenderer.GetComponent<Animator>();
        }

        if (inertiaDustSpriteRenderer == null)
        {
            Transform dustSprite = transform.Find("InertiaDust/DustSprite");
            if (dustSprite == null)
            {
                dustSprite = transform.Find("InertiaDust");
            }

            if (dustSprite != null)
            {
                inertiaDustSpriteRenderer = dustSprite.GetComponentInChildren<SpriteRenderer>(true);
            }
        }

        if (inertiaDustAnimator == null && inertiaDustSpriteRenderer != null)
        {
            inertiaDustAnimator = inertiaDustSpriteRenderer.GetComponent<Animator>();
        }

        if (inertiaOverlayRenderers == null || inertiaOverlayRenderers.Length == 0)
        {
            inertiaOverlayRenderers = FindSpriteRenderers("Head", "ArmsGun");
        }

        if (movingSpriteRenderers == null || movingSpriteRenderers.Length == 0)
        {
            SpriteRenderer[] spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
            int movingRendererCount = 0;
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                if (IsLayeredRenderer(spriteRenderers[i]))
                {
                    movingRendererCount++;
                }
            }

            movingSpriteRenderers = new SpriteRenderer[movingRendererCount];
            int movingRendererIndex = 0;
            for (int i = 0; i < spriteRenderers.Length; i++)
            {
                if (IsLayeredRenderer(spriteRenderers[i]))
                {
                    movingSpriteRenderers[movingRendererIndex] = spriteRenderers[i];
                    movingRendererIndex++;
                }
            }
        }
    }

    private void Update()
    {
        SampleInput();
    }

    private void FixedUpdate()
    {
        if (movementBody == null)
        {
            return;
        }

        verticalSpeed = movementBody.linearVelocity.y;
        bool isHitReacting = Time.time < hitReactionEndTime;
        if (isHitReacting)
        {
            horizontalInput = 0f;
            jumpPressed = false;
        }

        bool wasGrounded = isGrounded;
        UpdateCrouchState();
        ApplyStanceCollider();
        RefreshGroundedState();
        UpdateJump();
        SnapToOneWayPlatform();
        bool justLanded = !wasGrounded && isGrounded;
        if (!isGrounded)
        {
            airborneDuration += Time.fixedDeltaTime;
            CancelLanding();
        }
        else
        {
            if (!wasGrounded && airborneDuration >= 0.08f && !IsPreparingJump && !isCrouching && !isHitReacting)
            {
                landingStartedAt = Time.time;
            }
            airborneDuration = 0f;
        }
        if (isCrouching || isHitReacting)
        {
            CancelLanding();
        }
        jumpPressed = false;

        bool hasMovementInput = !isCrouching && !Mathf.Approximately(horizontalInput, 0f);
        float targetHorizontalSpeed = horizontalInput * moveSpeed;
        if (isHitReacting || isCrouching)
        {
            currentHorizontalSpeed = 0f;
        }
        else if (justLanded)
        {
            // Preserve direction without speeding up a slower landing.
            float speedLimit = Mathf.Max(0f, landingHorizontalSpeed);
            currentHorizontalSpeed = Mathf.Clamp(currentHorizontalSpeed, -speedLimit, speedLimit);
        }
        else
        {
            float speedChange = hasMovementInput ? acceleration : GetCurrentDeceleration();
            currentHorizontalSpeed = Mathf.MoveTowards(
                currentHorizontalSpeed,
                targetHorizontalSpeed,
                speedChange * Time.fixedDeltaTime
            );
        }

        if (Mathf.Abs(currentHorizontalSpeed) < StopThreshold)
        {
            currentHorizontalSpeed = 0f;
        }

        bool hasMovementSpeed = Mathf.Abs(currentHorizontalSpeed) > 0f;
        float horizontalSpeed = justLanded ? currentHorizontalSpeed : isHitReacting
            ? hitKnockbackSpeed * Mathf.Clamp01((hitReactionEndTime - Time.time) / Time.fixedDeltaTime)
            : currentHorizontalSpeed;
        ClampToFallbackGround();
        movementBody.linearVelocity = new Vector2(horizontalSpeed, verticalSpeed);

        bool isGunReloading = gunFire != null && gunFire.IsReloading;
        bool shouldCoast = isGrounded && !IsLanding && !IsPreparingJump && !isCrouching && !isGunReloading && !hasMovementInput && hasMovementSpeed;
        if (shouldCoast && !isCoasting)
        {
            inertiaStartedAt = Time.time;
        }

        isCoasting = shouldCoast;

        bool showAirborneDriftPose = !isGrounded && !hasMovementInput && hasMovementSpeed;
        bool isAirborneAiming = !isGrounded && !isCrouching && (fireHeld || isGunReloading);
        bool isIdleAiming = isGrounded && !hasMovementInput && !isCoasting && (fireHeld || isGunReloading);
        SetMovingAnimation(hasMovementInput);
        UpdateInertiaAnimation(isCoasting);
        SetRendererVisibility(IsPreparingJump || IsLanding || hasMovementInput || showAirborneDriftPose || isAirborneAiming || isIdleAiming || isCrouching || isGunReloading, isCoasting);
    }

    private void LateUpdate()
    {
        ApplyCrouchAnimationFrame();
        ApplyLandingAnimationFrame();
    }

    private void ApplyLandingAnimationFrame()
    {
        if ((!IsLanding && !IsPreparingJump) || bodySpriteRenderer == null)
        {
            RestoreLandingSprite();
            return;
        }

        Sprite[] frames = IsPreparingJump ? jumpPreparationSprites : landingSprites;
        int frameIndex = IsPreparingJump ? JumpPreparationFrameIndex : LandingFrameIndex;
        Sprite current = bodySpriteRenderer.sprite;
        // The Animator can supply a fresh running pose each frame. Keep it for restoration.
        if (!IsJumpOrLandingSprite(current))
        {
            spriteBeforeLanding = current;
        }
        Sprite frame = frames[frameIndex];
        if (frame != null)
        {
            bodySpriteRenderer.sprite = frame;
            landingSpriteApplied = true;
        }
    }

    private void RestoreLandingSprite()
    {
        if (landingSpriteApplied && bodySpriteRenderer != null
            && IsJumpOrLandingSprite(bodySpriteRenderer.sprite))
        {
            bodySpriteRenderer.sprite = spriteBeforeLanding;
        }
        landingSpriteApplied = false;
        spriteBeforeLanding = null;
    }

    private bool IsJumpOrLandingSprite(Sprite sprite)
    {
        return (landingSprites != null && System.Array.IndexOf(landingSprites, sprite) >= 0)
            || (jumpPreparationSprites != null && System.Array.IndexOf(jumpPreparationSprites, sprite) >= 0);
    }

    private void CancelJumpPreparation()
    {
        jumpPreparing = false;
        RestoreLandingSprite();
    }

    private void CancelLanding()
    {
        landingStartedAt = float.NegativeInfinity;
        RestoreLandingSprite();
    }

    private void OnDisable()
    {
        CancelJumpPreparation();
        CancelLanding();
        airborneDuration = 0f;
        if (movementBody != null)
        {
            movementBody.linearVelocity = Vector2.zero;
        }

        currentHorizontalSpeed = 0f;
        verticalSpeed = 0f;
        horizontalInput = 0f;
        jumpHeld = false;
        crouchHeld = false;
        fireHeld = false;
        jumpPressed = false;
    }

    private void OnDestroy()
    {
        if (frictionlessMaterial != null)
        {
            Destroy(frictionlessMaterial);
        }
    }

    private void AssignDefaultCollisionLayers()
    {
        if (solidPlatformLayers.value == 0)
        {
            solidPlatformLayers = LayerMask.GetMask("Box");
        }

        if (oneWayPlatformLayers.value == 0)
        {
            oneWayPlatformLayers = LayerMask.GetMask("Stair");
        }
    }

    private bool IsLayeredRenderer(SpriteRenderer spriteRenderer)
    {
        return spriteRenderer != null
            && spriteRenderer != idleSpriteRenderer
            && spriteRenderer != inertiaSpriteRenderer
            && spriteRenderer != inertiaDustSpriteRenderer;
    }

    private SpriteRenderer[] FindSpriteRenderers(params string[] childNames)
    {
        int rendererCount = 0;
        for (int i = 0; i < childNames.Length; i++)
        {
            if (FindSpriteRenderer(childNames[i]) != null)
            {
                rendererCount++;
            }
        }

        SpriteRenderer[] spriteRenderers = new SpriteRenderer[rendererCount];
        int rendererIndex = 0;
        for (int i = 0; i < childNames.Length; i++)
        {
            SpriteRenderer spriteRenderer = FindSpriteRenderer(childNames[i]);
            if (spriteRenderer != null)
            {
                spriteRenderers[rendererIndex] = spriteRenderer;
                rendererIndex++;
            }
        }

        return spriteRenderers;
    }

    private SpriteRenderer FindSpriteRenderer(string childName)
    {
        Transform child = transform.Find(childName);
        return child != null ? child.GetComponent<SpriteRenderer>() : null;
    }

    private void SetMovingAnimation(bool isMoving)
    {
        if (animator == null)
        {
            return;
        }

        animator.SetBool(IsMovingParameter, isMoving);
    }

    private void UpdateInertiaAnimation(bool isCoasting)
    {
        if (isCoasting && !wasCoasting && inertiaAnimator != null)
        {
            inertiaAnimator.Play(InertiaStateName, 0, 0f);
        }

        if (isCoasting && !wasCoasting && inertiaDustAnimator != null)
        {
            inertiaDustAnimator.Play(DustStateName, 0, 0f);
        }

        wasCoasting = isCoasting;
    }

    private float GetCurrentDeceleration()
    {
        return isGrounded ? deceleration : 0f;
    }

    private void UpdateCrouchState()
    {
        bool shouldCrouch = isGrounded && crouchHeld;
        if (shouldCrouch && !isCrouching)
        {
            crouchStartedAt = Time.time;
        }

        isCrouching = shouldCrouch;
    }

    private void ApplyStanceCollider()
    {
        if (stanceCollider != null)
        {
            stanceCollider.ApplyCurrentStance();
        }
    }

    private void RefreshGroundedState()
    {
        if (verticalSpeed > 0f)
        {
            isGrounded = false;
            return;
        }

        if (IsOnFallbackGround())
        {
            isGrounded = true;
            verticalSpeed = 0f;
            return;
        }

        isGrounded = HasGroundBelow(GetCurrentGroundedCheckDistance());
        if (isGrounded || ShouldKeepCrouchGrounded())
        {
            isGrounded = true;
            verticalSpeed = 0f;
        }
    }

    private void UpdateJump()
    {
        bool canJump = isGrounded && !isCrouching && Time.time >= hitReactionEndTime;
        if (jumpPreparing && !canJump)
        {
            CancelJumpPreparation();
        }

        if (jumpPressed && canJump && !jumpPreparing)
        {
            CancelLanding();
            if (jumpPreparationDuration > 0f && jumpPreparationSprites != null
                && jumpPreparationSprites.Length > 0)
            {
                jumpPreparing = true;
                jumpPreparationStartedAt = Time.time;
            }
            else
            {
                verticalSpeed = jumpSpeed;
                isGrounded = false;
            }
        }

        // A short tap still completes the wind-up. Holding W cannot restart it.
        if (jumpPreparing && Time.time - jumpPreparationStartedAt >= jumpPreparationDuration)
        {
            CancelJumpPreparation();
            verticalSpeed = jumpSpeed;
            isGrounded = false;
        }

        if (!isGrounded)
        {
            float currentGravity = verticalSpeed <= 0f
                ? gravity * Mathf.Max(1f, fallGravityMultiplier)
                : gravity;
            verticalSpeed -= currentGravity * Time.fixedDeltaTime;
        }
    }

    private void ApplyCrouchAnimationFrame()
    {
        if (!isCrouching || bodySpriteRenderer == null || crouchSprites == null || crouchSprites.Length == 0)
        {
            return;
        }

        Sprite crouchSprite = crouchSprites[CrouchFrameIndex];
        if (crouchSprite != null)
        {
            bodySpriteRenderer.sprite = crouchSprite;
        }
    }

    private void SnapToOneWayPlatform()
    {
        if (verticalSpeed > 0f || movementCollider == null || !movementCollider.enabled
            || oneWayPlatformLayers.value == 0)
        {
            return;
        }

        float distance = Mathf.Max(groundedCheckDistance,
            -verticalSpeed * Time.fixedDeltaTime) + GetSkinWidth();
        int hitCount = CastMovementCollider(Vector2.down, distance, oneWayPlatformLayers);
        float feetY = movementCollider.bounds.min.y;
        float highestTop = float.NegativeInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = movementCastHits[i];
            if (!IsGroundHit(hit, distance))
            {
                continue;
            }

            float top = hit.collider.bounds.max.y;
            if (top <= feetY + oneWayPlatformTopTolerance
                && top >= feetY - distance - GetSkinWidth())
            {
                highestTop = Mathf.Max(highestTop, top);
            }
        }

        if (float.IsNegativeInfinity(highestTop))
        {
            return;
        }

        movementBody.position += new Vector2(0f, highestTop - feetY);
        verticalSpeed = 0f;
        isGrounded = true;
        Physics2D.SyncTransforms();
    }

    private bool HasGroundBelow(float distance)
    {
        if (movementCollider == null || !movementCollider.enabled)
        {
            return IsOnFallbackGround();
        }

        Physics2D.SyncTransforms();
        int hitCount = CastMovementCollider(Vector2.down, Mathf.Max(0f, distance) + GetSkinWidth(), GetGroundLayers());
        for (int i = 0; i < hitCount; i++)
        {
            if (IsGroundHit(movementCastHits[i], distance))
            {
                return true;
            }
        }

        return false;
    }

    private int CastMovementCollider(Vector2 direction, float distance, LayerMask layerMask)
    {
        if (layerMask.value == 0)
        {
            return 0;
        }

        return Physics2D.BoxCast(
            GetMovementColliderWorldCenter(),
            GetMovementColliderWorldSize(),
            GetMovementColliderWorldAngle(),
            direction,
            CreateContactFilter(layerMask),
            movementCastHits,
            distance
        );
    }

    private Vector2 GetMovementColliderWorldCenter()
    {
        return movementCollider.transform.TransformPoint(movementCollider.offset);
    }

    private Vector2 GetMovementColliderWorldSize()
    {
        Vector3 scale = movementCollider.transform.lossyScale;
        return new Vector2(
            Mathf.Abs(movementCollider.size.x * scale.x),
            Mathf.Abs(movementCollider.size.y * scale.y)
        );
    }

    private float GetMovementColliderWorldAngle()
    {
        return movementCollider.transform.eulerAngles.z;
    }

    private ContactFilter2D CreateContactFilter(LayerMask layerMask)
    {
        return new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = layerMask,
            useTriggers = collisionIncludesTriggers,
        };
    }

    private LayerMask GetGroundLayers()
    {
        return GetSolidPlatformLayers() | oneWayPlatformLayers;
    }

    private LayerMask GetSolidPlatformLayers()
    {
        return solidPlatformLayers | LayerMask.GetMask(WaveBoundaryLayerName);
    }

    private bool IsGroundHit(RaycastHit2D hit, float checkDistance)
    {
        if (ShouldIgnoreCollisionHit(hit))
        {
            return false;
        }

        int hitLayer = hit.collider.gameObject.layer;
        if (IsInLayerMask(hitLayer, oneWayPlatformLayers) && !CanLandOnOneWayPlatform(hit.collider))
        {
            return false;
        }

        if (!IsInLayerMask(hitLayer, GetGroundLayers()))
        {
            return false;
        }

        if (hit.normal.sqrMagnitude > 0.0001f)
        {
            return hit.normal.y > 0.01f;
        }

        return IsColliderBelowFeet(hit.collider, checkDistance);
    }

    private bool CanLandOnOneWayPlatform(Collider2D platformCollider)
    {
        return movementCollider.bounds.min.y >= platformCollider.bounds.max.y - oneWayPlatformTopTolerance;
    }

    private bool IsColliderBelowFeet(Collider2D otherCollider, float checkDistance)
    {
        Bounds ownBounds = movementCollider.bounds;
        Bounds otherBounds = otherCollider.bounds;
        return otherBounds.max.y <= ownBounds.min.y + oneWayPlatformTopTolerance
            && otherBounds.max.y >= ownBounds.min.y - Mathf.Max(0f, checkDistance) - GetSkinWidth();
    }

    private float GetCurrentGroundedCheckDistance()
    {
        if (!isCrouching)
        {
            return groundedCheckDistance;
        }

        return Mathf.Max(groundedCheckDistance, crouchGroundedCheckDistance);
    }

    private bool ShouldKeepCrouchGrounded()
    {
        if (!isCrouching)
        {
            return false;
        }

        return Time.time - crouchStartedAt <= Mathf.Max(0f, crouchGroundedGraceTime);
    }

    private bool ShouldIgnoreCollisionHit(RaycastHit2D hit)
    {
        return hit.collider == null
            || hit.collider == movementCollider
            || hit.collider.transform.IsChildOf(transform)
            || (!collisionIncludesTriggers && hit.collider.isTrigger);
    }

    private bool IsInLayerMask(int layer, LayerMask layerMask)
    {
        return (layerMask.value & (1 << layer)) != 0;
    }

    private bool ClampToFallbackGround()
    {
        if (!IsOnFallbackGround() || verticalSpeed > 0f)
        {
            return false;
        }

        if (movementBody != null)
        {
            movementBody.position = new Vector2(movementBody.position.x, groundY);
        }
        else
        {
            transform.position = new Vector3(transform.position.x, groundY, transform.position.z);
        }
        verticalSpeed = 0f;
        isGrounded = true;
        return true;
    }

    private bool IsOnFallbackGround()
    {
        return transform.position.y <= groundY + GetSkinWidth();
    }

    private float GetSkinWidth()
    {
        return Mathf.Max(0f, collisionSkinWidth);
    }

    private void SetRendererVisibility(bool showLayeredRenderers, bool showInertiaRenderer)
    {
        if (idleSpriteRenderer != null)
        {
            idleSpriteRenderer.enabled = !showLayeredRenderers && !showInertiaRenderer;
        }

        if (inertiaSpriteRenderer != null)
        {
            inertiaSpriteRenderer.enabled = showInertiaRenderer;
        }

        if (inertiaDustSpriteRenderer != null)
        {
            inertiaDustSpriteRenderer.enabled = showInertiaRenderer;
        }

        if (movingSpriteRenderers == null)
        {
            return;
        }

        for (int i = 0; i < movingSpriteRenderers.Length; i++)
        {
            if (movingSpriteRenderers[i] != null)
            {
                movingSpriteRenderers[i].enabled = showLayeredRenderers;
            }
        }

        if (inertiaOverlayRenderers == null)
        {
            return;
        }

        for (int i = 0; i < inertiaOverlayRenderers.Length; i++)
        {
            if (inertiaOverlayRenderers[i] != null)
            {
                inertiaOverlayRenderers[i].enabled = showLayeredRenderers || showInertiaRenderer;
            }
        }
    }

    private void SampleInput()
    {
        bool previousJumpHeld = jumpHeld;
        horizontalInput = ReadHorizontalInput();
        fireHeld = ReadFireHeld();
        jumpHeld = ReadJumpHeld();
        crouchHeld = ReadCrouchHeld();
        jumpPressed |= jumpHeld && !previousJumpHeld;
        hasMouseScreenPosition = ReadMouseScreenPosition(out mouseScreenPosition);
    }

    private static bool ReadFireHeld()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current != null)
        {
            return Mouse.current.leftButton.isPressed;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButton(0);
#else
        return false;
#endif
    }

    private static float ReadHorizontalInput()
    {
        float horizontalInput = 0f;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed)
            {
                horizontalInput -= 1f;
            }

            if (Keyboard.current.dKey.isPressed)
            {
                horizontalInput += 1f;
            }

            return horizontalInput;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKey(KeyCode.A))
        {
            horizontalInput -= 1f;
        }

        if (Input.GetKey(KeyCode.D))
        {
            horizontalInput += 1f;
        }
#endif

        return horizontalInput;
    }

    private static bool ReadJumpHeld()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            return Keyboard.current.wKey.isPressed;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKey(KeyCode.W);
#else
        return false;
#endif
    }

    private static bool ReadCrouchHeld()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            return Keyboard.current.sKey.isPressed;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKey(KeyCode.S);
#else
        return false;
#endif
    }

    private static bool ReadMouseScreenPosition(out Vector2 screenPosition)
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
