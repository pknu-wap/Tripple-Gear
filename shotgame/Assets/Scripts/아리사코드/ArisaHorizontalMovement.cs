using System.Collections.Generic;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(CapsuleCollider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class ArisaHorizontalMovement : MonoBehaviour
{
    private static readonly int IsMovingParameter = Animator.StringToHash("IsMoving");
    private const string WaveBoundaryLayerName = "WaveBoundary";
    private const string EnemyLayerName = "Enemy";
    private const float StopThreshold = 0.03f;
    private const float MinimumLandingAirborneDuration = 0.08f;

    [SerializeField, KoreanLabel("이동 속도")] private float moveSpeed = 3f;
    [SerializeField, KoreanLabel("달리는 맵 화면 왼쪽 여유"), Min(0f)] private float runningMapLeftPadding = 0.2f;
    [SerializeField, KoreanLabel("점프 속도")] private float jumpSpeed = 6f;
    [SerializeField, KoreanLabel("중력")] private float gravity = DefaultGravity;
    [SerializeField, KoreanLabel("하강 중력 배율"), Min(1f)] private float fallGravityMultiplier = DefaultFallGravityMultiplier;
    [SerializeField, KoreanLabel("애니메이터")] private Animator animator;
    [SerializeField, KoreanLabel("대기 스프라이트 렌더러")] private SpriteRenderer idleSpriteRenderer;
    [SerializeField, KoreanLabel("몸 스프라이트 렌더러")] private SpriteRenderer bodySpriteRenderer;
    [SerializeField, KoreanLabel("앉기 스프라이트")] private Sprite[] crouchSprites;
    [SerializeField, KoreanLabel("앉기 프레임 속도")] private float crouchFrameRate = 12f;
    [SerializeField, KoreanLabel("관성 스프라이트 렌더러")] private SpriteRenderer inertiaSpriteRenderer;
    [SerializeField, KoreanLabel("관성 먼지 스프라이트 렌더러")] private SpriteRenderer inertiaDustSpriteRenderer;
    [SerializeField, KoreanLabel("관성 오버레이 렌더러")] private SpriteRenderer[] inertiaOverlayRenderers;
    [SerializeField, KoreanLabel("이동 중 렌더러")] private SpriteRenderer[] movingSpriteRenderers;
    [SerializeField, KoreanLabel("이동 충돌체")] private CapsuleCollider2D movementCollider;
    [SerializeField, KoreanLabel("시작 시 콜라이더로만 지면 판정")] private bool useColliderGroundAtStart;
    private Rigidbody2D movementBody;
    private float windCenterX;
    private float windPullSpeed;

    internal void SetWindPull(float centerX, float speed)
    {
        windCenterX = centerX;
        windPullSpeed = speed;
    }
    private CameraManager cameraManager;
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

    [SerializeField, KoreanLabel("제자리 점프 스프라이트")] private Sprite[] standingJumpSprites;
    [SerializeField, KoreanLabel("달리기 점프 스프라이트")] private Sprite[] runningJumpSprites;
    [SerializeField, KoreanLabel("착지 스프라이트")] private Sprite[] landingSprites;
    [SerializeField, KoreanLabel("착지 프레임 속도"), Min(1f)] private float landingFrameRate = 16f;
    [SerializeField, KoreanLabel("착지 상체 위치 보정")] private Vector2[] landingUpperBodyOffsets;

    private float landingStartedAt = float.NegativeInfinity;
    private float airborneDuration;
    private Sprite[] activeJumpSprites;
    private Sprite bodySpriteBeforePose;
    private Sprite appliedBodyPose;

    private bool IsPlayingJumpPose => !isGrounded && activeJumpSprites != null && activeJumpSprites.Length > 0;

    public bool IsLanding => enabled && isGrounded && !isCrouching
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
    private float hitReactionEndTime;
    private float hitKnockbackSpeed;
    private float verticalSpeed;
    private bool isLaunchedUpward;
    private float groundY;
    private bool useFallbackGround;
    private BoxCollider2D boatDeck;
    private float boatSpeed;
    private float airborneBoatSpeed;
    private bool isGrounded = true;
    private bool isCrouching;
    private float crouchStartedAt;
    private readonly RaycastHit2D[] movementCastHits = new RaycastHit2D[12];
    private readonly List<Vector2> platformOutline = new List<Vector2>();

    public bool IsCrouching => isCrouching;
    public bool IsInBoatSection => boatDeck != null;

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


    public bool TryGetMouseScreenPosition(out Vector2 screenPosition)
    {
        screenPosition = mouseScreenPosition;
        return hasMouseScreenPosition;
    }

    public void TeleportTo(Vector3 position) => TeleportTo(position, useFallbackGround: true);

    internal SpriteRenderer IdleSpriteRenderer => idleSpriteRenderer;

    internal void ShowIdlePresentation()
    {
        SetMovingAnimation(false);
        SetRendererVisibility(false);
    }

    internal void TeleportTo(Vector3 position, bool useFallbackGround)
    {
        boatDeck = null;
        boatSpeed = 0f;
        airborneBoatSpeed = 0f;

        if (movementBody != null)
        {
            movementBody.position = position;
            movementBody.linearVelocity = Vector2.zero;
        }

        transform.position = position;
        ClearAirPoses();
        airborneDuration = 0f;
        groundY = position.y;
        this.useFallbackGround = useFallbackGround;
        hitReactionEndTime = 0f;
        hitKnockbackSpeed = 0f;
        verticalSpeed = 0f;
        isLaunchedUpward = false;
        jumpPressed = false;
        isGrounded = true;
        isCrouching = false;
        ApplyStanceCollider();
        Physics2D.SyncTransforms();
    }

    public void BoardBoat(BoxCollider2D deck)
    {
        LandOnPlatform(deck);
        boatDeck = deck;
    }

    internal void LandOnPlatform(Collider2D platform)
    {
        TeleportTo(transform.position, useFallbackGround: false);
        Bounds feet = movementCollider.bounds;
        Bounds platformBounds = platform.bounds;
        Vector3 position = transform.position;
        position.x += platformBounds.center.x - feet.center.x;
        float surface = GetPlatformSurface(platform, platformBounds.center.x);
        position.y += (float.IsNegativeInfinity(surface) ? platformBounds.max.y : surface) - feet.min.y;
        movementBody.position = position;
        transform.position = position;
        Physics2D.SyncTransforms();
    }

    public void MoveWithBoat(BoxCollider2D deck, float step)
    {
        if (boatDeck != deck || !isActiveAndEnabled)
        {
            return;
        }

        boatSpeed = step / Time.fixedDeltaTime;
        if (step > 0f && isGrounded && IsAtBoatDeck())
        {
            movementBody.position += Vector2.right * step;
        }
    }

    public void StartHitReaction(float horizontalDirection, float duration, float distance, float upwardSpeed)
    {
        if (duration <= 0f)
        {
            return;
        }

        ClearAirPoses();
        hitReactionEndTime = Time.time + duration;
        hitKnockbackSpeed = Mathf.Sign(horizontalDirection) * Mathf.Max(0f, distance) / duration;
        if (upwardSpeed > 0f)
        {
            isLaunchedUpward = true;
            verticalSpeed = Mathf.Max(verticalSpeed, upwardSpeed);
            if (movementBody != null)
            {
                Vector2 velocity = movementBody.linearVelocity;
                movementBody.linearVelocity = new Vector2(velocity.x, Mathf.Max(velocity.y, upwardSpeed));
            }
            isGrounded = false;
        }
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
        useFallbackGround = !useColliderGroundAtStart;
        isGrounded = true;
        ShowIdlePresentation();
    }

    private void FindReferences()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (movementCollider == null)
        {
            movementCollider = GetComponent<CapsuleCollider2D>();
        }

        if (movementBody == null)
        {
            movementBody = GetComponent<Rigidbody2D>();
        }

        if (cameraManager == null)
        {
#if UNITY_2023_1_OR_NEWER
            cameraManager = Object.FindFirstObjectByType<CameraManager>();
#else
            cameraManager = Object.FindObjectOfType<CameraManager>();
#endif
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
        bool landedAfterAirborne = !wasGrounded && isGrounded
            && airborneDuration >= MinimumLandingAirborneDuration;
        if (!isGrounded)
        {
            airborneDuration += Time.fixedDeltaTime;
            CancelLanding();
        }
        else
        {
            activeJumpSprites = null;
            if (landedAfterAirborne && !isCrouching && !isHitReacting)
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
        float inputSpeed = hasMovementInput ? horizontalInput * moveSpeed : 0f;
        float horizontalSpeed = isHitReacting && !landedAfterAirborne
            ? hitKnockbackSpeed * Mathf.Clamp01((hitReactionEndTime - Time.time) / Time.fixedDeltaTime)
            : inputSpeed;
        if (windPullSpeed > 0f)
        {
            float gap = windCenterX - movementBody.position.x;
            if (horizontalInput * gap >= 0f)
                horizontalSpeed += Mathf.Clamp(gap / Time.fixedDeltaTime, -windPullSpeed, windPullSpeed);
        }
        horizontalSpeed = KeepInsideRunningMapScreen(horizontalSpeed);
        if (boatDeck != null)
        {
            horizontalSpeed = ApplyBoatMovement(horizontalSpeed, wasGrounded);
        }
        ClampToFallbackGround();
        movementBody.linearVelocity = new Vector2(horizontalSpeed, verticalSpeed);

        bool isGunReloading = gunFire != null && gunFire.IsReloading;
        bool isAirborneAiming = !isGrounded && !isCrouching && (fireHeld || isGunReloading);
        bool isIdleAiming = isGrounded && !hasMovementInput && (fireHeld || isGunReloading);
        SetMovingAnimation(hasMovementInput);
        SetRendererVisibility(IsPlayingJumpPose || IsLanding || hasMovementInput
            || isAirborneAiming || isIdleAiming || isCrouching || isGunReloading);
    }

    private void LateUpdate()
    {
        ApplyCrouchAnimationFrame();
        ApplyBodyPose(GetAirPoseSprite());
    }

    private float ApplyBoatMovement(float horizontalSpeed, bool wasGrounded)
    {
        if (!isGrounded)
        {
            if (wasGrounded)
            {
                airborneBoatSpeed = boatSpeed;
            }

            return horizontalSpeed + airborneBoatSpeed;
        }

        airborneBoatSpeed = 0f;
        if (!IsAtBoatDeck())
        {
            return horizontalSpeed;
        }

        Bounds bodyBounds = movementCollider.bounds;
        Bounds deckBounds = boatDeck.bounds;
        float minStep = deckBounds.min.x - bodyBounds.min.x;
        float maxStep = deckBounds.max.x - bodyBounds.max.x;
        float step = minStep > maxStep ? (minStep + maxStep) * 0.5f
            : Mathf.Clamp(horizontalSpeed * Time.fixedDeltaTime, minStep, maxStep);
        return step / Time.fixedDeltaTime;
    }

    private bool IsAtBoatDeck()
    {
        Bounds bodyBounds = movementCollider.bounds;
        Bounds deckBounds = boatDeck.bounds;
        float tolerance = GetCurrentGroundedCheckDistance() + GetSkinWidth();
        return Mathf.Abs(bodyBounds.min.y - deckBounds.max.y) <= tolerance
            && bodyBounds.max.x > deckBounds.min.x && bodyBounds.min.x < deckBounds.max.x;
    }

    private float KeepInsideRunningMapScreen(float horizontalSpeed)
    {
        if (cameraManager == null || cameraManager.FollowMode != CameraFollowMode.RunningMap
            || cameraManager.FollowTarget != transform || movementCollider == null)
        {
            return horizontalSpeed;
        }

        Camera cameraToUse = cameraManager.ManagedCamera;
        if (cameraToUse == null || !cameraToUse.orthographic)
        {
            return horizontalSpeed;
        }

        float leftEdge = cameraToUse.transform.position.x
            - cameraToUse.orthographicSize * cameraToUse.aspect + runningMapLeftPadding;
        float distanceToEdge = movementCollider.bounds.min.x - leftEdge;
        if (distanceToEdge < 0f)
        {
            Vector2 position = movementBody.position;
            position.x -= distanceToEdge;
            movementBody.position = position;
            Physics2D.SyncTransforms();
            distanceToEdge = 0f;
        }

        if (horizontalSpeed < 0f)
        {
            return Mathf.Max(horizontalSpeed, -distanceToEdge / Time.fixedDeltaTime);
        }

        return horizontalSpeed;
    }

    private Sprite GetAirPoseSprite()
    {
        if (IsPlayingJumpPose)
        {
            float launchSpeed = Mathf.Max(0.01f, jumpSpeed);
            float progress = Mathf.Clamp01(0.5f - 0.5f * verticalSpeed / launchSpeed);
            int frameIndex = Mathf.Min(Mathf.FloorToInt(progress * activeJumpSprites.Length), activeJumpSprites.Length - 1);
            return activeJumpSprites[frameIndex];
        }

        return IsLanding ? landingSprites[LandingFrameIndex] : null;
    }

    private void ApplyBodyPose(Sprite frame)
    {
        if (bodySpriteRenderer == null)
        {
            return;
        }

        Sprite current = bodySpriteRenderer.sprite;
        if (frame == null)
        {
            if (appliedBodyPose != null && current == appliedBodyPose)
            {
                bodySpriteRenderer.sprite = bodySpriteBeforePose;
            }
            appliedBodyPose = null;
            bodySpriteBeforePose = null;
            return;
        }

        // Preserve fresh Animator frames while overriding the body sprite in the air.
        if (appliedBodyPose == null || current != appliedBodyPose)
        {
            bodySpriteBeforePose = current;
        }
        bodySpriteRenderer.sprite = frame;
        appliedBodyPose = frame;
    }

    private void CancelLanding()
    {
        landingStartedAt = float.NegativeInfinity;
    }

    private void ClearAirPoses()
    {
        activeJumpSprites = null;
        CancelLanding();
        ApplyBodyPose(null);
    }

    private void OnDisable()
    {
        ClearAirPoses();
        airborneDuration = 0f;
        if (movementBody != null)
        {
            movementBody.linearVelocity = Vector2.zero;
        }

        verticalSpeed = 0f;
        isLaunchedUpward = false;
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
        // Uphill contacts can add upward velocity without launching the player.
        isLaunchedUpward = isLaunchedUpward && verticalSpeed > 0f;
        if (isLaunchedUpward)
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
        if (jumpPressed && isGrounded && !isCrouching && Time.time >= hitReactionEndTime)
        {
            CancelLanding();
            // Choose once from actual takeoff speed. Air steering keeps the selected pose.
            activeJumpSprites = Mathf.Abs(movementBody.linearVelocity.x) > StopThreshold
                ? runningJumpSprites : standingJumpSprites;
            verticalSpeed = jumpSpeed;
            isLaunchedUpward = true;
            isGrounded = false;
        }

        if (!isGrounded)
        {
            verticalSpeed = ApplyGravity(verticalSpeed, this);
        }
    }

    private const float DefaultGravity = 18f;
    private const float DefaultFallGravityMultiplier = 1.7f;

    internal static float GetGravity(ArisaHorizontalMovement source) => source != null ? source.gravity : DefaultGravity;

    internal static float ApplyGravity(float speed, ArisaHorizontalMovement source)
    {
        float acceleration = GetGravity(source);
        if (speed <= 0f)
        {
            acceleration *= source != null ? Mathf.Max(1f, source.fallGravityMultiplier) : DefaultFallGravityMultiplier;
        }
        return speed - acceleration * Time.fixedDeltaTime;
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

            float top = GetOneWaySurface(hit, feetY);
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
        LayerMask groundCandidates = GetGroundLayers() | LayerMask.GetMask(EnemyLayerName);
        int hitCount = CastMovementCollider(Vector2.down,
            Mathf.Max(0f, distance) + GetSkinWidth(), groundCandidates);
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

        return movementCollider.Cast(direction, CreateContactFilter(layerMask), movementCastHits, distance);
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
        if (IsInLayerMask(hitLayer, oneWayPlatformLayers)
            && GetOneWaySurface(hit, movementCollider.bounds.min.y) > movementCollider.bounds.min.y + oneWayPlatformTopTolerance)
        {
            return false;
        }

        bool isSolidSpawner = !hit.collider.isTrigger
            && hitLayer == LayerMask.NameToLayer(EnemyLayerName)
            && hit.collider.TryGetComponent<MonsterSpawner>(out _);
        if (!isSolidSpawner && !IsInLayerMask(hitLayer, GetGroundLayers()))
        {
            return false;
        }

        if (hit.normal.sqrMagnitude > 0.0001f)
        {
            return hit.normal.y > 0.01f;
        }

        return IsColliderBelowFeet(hit.collider, checkDistance);
    }

    private float GetOneWaySurface(RaycastHit2D hit, float feetY)
    {
        if (hit.collider is PolygonCollider2D && hit.distance > 0.0001f)
        {
            return feetY - hit.distance;
        }

        float surface = GetPlatformSurface(hit.collider, movementCollider.bounds.center.x);
        return float.IsNegativeInfinity(surface) ? float.PositiveInfinity : surface;
    }

    private float GetPlatformSurface(Collider2D platform, float x)
    {
        if (platform is not PolygonCollider2D polygon)
        {
            return platform.bounds.max.y;
        }

        float top = float.NegativeInfinity;
        for (int path = 0; path < polygon.pathCount; path++)
        {
            polygon.GetPath(path, platformOutline);
            if (platformOutline.Count == 0) continue;

            Vector2 previous = polygon.transform.TransformPoint(platformOutline[platformOutline.Count - 1] + polygon.offset);
            foreach (Vector2 point in platformOutline)
            {
                Vector2 next = polygon.transform.TransformPoint(point + polygon.offset);
                if (x >= Mathf.Min(previous.x, next.x) && x <= Mathf.Max(previous.x, next.x))
                {
                    float surface = Mathf.Approximately(previous.x, next.x) ? Mathf.Max(previous.y, next.y)
                        : Mathf.Lerp(previous.y, next.y, Mathf.InverseLerp(previous.x, next.x, x));
                    top = Mathf.Max(top, surface);
                }
                previous = next;
            }
        }
        return top;
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
        return useFallbackGround && transform.position.y <= groundY + GetSkinWidth();
    }

    private float GetSkinWidth()
    {
        return Mathf.Max(0f, collisionSkinWidth);
    }

    private void SetRendererVisibility(bool showLayeredRenderers)
    {
        if (idleSpriteRenderer != null)
        {
            idleSpriteRenderer.enabled = !showLayeredRenderers;
        }

        if (inertiaSpriteRenderer != null)
        {
            inertiaSpriteRenderer.enabled = false;
        }

        if (inertiaDustSpriteRenderer != null)
        {
            inertiaDustSpriteRenderer.enabled = false;
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
                inertiaOverlayRenderers[i].enabled = showLayeredRenderers;
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
