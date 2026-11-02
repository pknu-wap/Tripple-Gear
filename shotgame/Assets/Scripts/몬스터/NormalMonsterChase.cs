using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(MonsterHealth))]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class NormalMonsterChase : MonoBehaviour
{
    private const float CollisionSkinWidth = 0.02f;
    private readonly RaycastHit2D[] obstacleHits = new RaycastHit2D[16];

    [Header("감지")]
    [SerializeField, KoreanLabel("대상 카메라")] private Camera targetCamera;
    [SerializeField, KoreanLabel("카메라 진입 로그")] private bool logWhenEnteredCamera = true;
    [SerializeField, KoreanLabel("카메라 진입 메시지")] private string cameraEnteredLogMessage = "일반몹이 Main Camera 영역에 들어왔습니다.";

    [Header("추적")]
    [SerializeField, KoreanLabel("플레이어")] private Transform followTarget;

    [Header("장애물 점프")]
    [SerializeField, KoreanLabel("장애물 점프 사용")] private bool jumpOverObstacles = true;
    [SerializeField, KoreanLabel("점프 높이"), Min(0.1f)] private float jumpHeight = 3.5f;
    [SerializeField, KoreanLabel("점프 시간"), Min(0.1f)] private float jumpDuration = 2.2f;
    [SerializeField, KoreanLabel("점프 중 이동 속도 배율"), Min(0.1f)] private float jumpMoveSpeedMultiplier = 2.2f;

    [Header("그래픽")]
    [SerializeField, KoreanLabel("애니메이터")] private Animator animator;
    [SerializeField, KoreanLabel("이동 컨트롤러")] private RuntimeAnimatorController moveController;

    private bool isChasing;
    private bool isMovementPaused;
    private SpriteRenderer spriteRenderer;
    private Vector3 baseLocalScale;
    private bool baseSpriteFlipX;
    private bool baseFacesRight;
    private MonsterHealth health;
    private BoxCollider2D movementCollider;
    private float startingY;
    private float jumpStartY;
    private float jumpStartTime;
    private float nextJumpTime;
    private bool isJumping;

    private MonsterSO MonsterData => health != null ? health.Data : null;

    public bool IsChasing => isChasing;
    public bool IsMovementPaused => isMovementPaused;
    public bool IsJumping => isJumping;
    public float MovementSpeedMultiplier => isJumping ? jumpMoveSpeedMultiplier : 1f;

    private void Reset()
    {
        FindReferences();
    }

    private void Awake()
    {
        FindReferences();
        baseLocalScale = transform.localScale;
        baseSpriteFlipX = spriteRenderer != null && spriteRenderer.flipX;
        baseFacesRight = IsFacingRight();
        startingY = transform.position.y;
    }

    private void OnValidate()
    {
        jumpHeight = Mathf.Max(0.1f, jumpHeight);
        jumpDuration = Mathf.Max(0.1f, jumpDuration);
        jumpMoveSpeedMultiplier = Mathf.Max(0.1f, jumpMoveSpeedMultiplier);
        if (!Application.isPlaying)
        {
            FindReferences();
        }
    }

    private void Update()
    {
        if (MonsterData == null || health.IsDead)
        {
            return;
        }

        UpdateJumpMotion();

        if (!isChasing)
        {
            if (IsInsideCameraView())
            {
                StartChasing();
            }

            return;
        }

        if (isMovementPaused)
        {
            return;
        }

        ChaseTarget();
    }

    public void SetFollowTarget(Transform target)
    {
        if (target != null)
        {
            followTarget = target;
        }
    }

    public void SetMovementPaused(bool paused)
    {
        if (isMovementPaused == paused)
        {
            return;
        }

        isMovementPaused = paused;
        if (!isMovementPaused)
        {
            SetAnimationController(moveController);
        }
    }

    public void FaceTarget(Transform target)
    {
        if (target == null)
        {
            return;
        }

        ApplyFacing(target.position.x - transform.position.x);
    }

    public float MoveHorizontally(float movement)
    {
        if (Mathf.Approximately(movement, 0f) || movementCollider == null || !movementCollider.enabled)
        {
            return 0f;
        }

        Physics2D.SyncTransforms();
        Vector2 direction = movement > 0f ? Vector2.right : Vector2.left;
        float requestedDistance = Mathf.Abs(movement);
        LayerMask obstacleLayers = LayerMask.GetMask("Box", "Stair");
        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = obstacleLayers,
            useTriggers = false,
        };
        Bounds ownBounds = movementCollider.bounds;
        int hitCount = Physics2D.BoxCast(
            ownBounds.center,
            ownBounds.size,
            transform.eulerAngles.z,
            direction,
            filter,
            obstacleHits,
            requestedDistance + CollisionSkinWidth
        );

        float allowedDistance = requestedDistance;
        RaycastHit2D nearestObstacle = default;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = obstacleHits[i];
            if (!IsBlockingObstacle(hit, direction, ownBounds))
            {
                continue;
            }

            float hitDistance = Mathf.Max(0f, hit.distance - CollisionSkinWidth);
            if (hitDistance < allowedDistance)
            {
                allowedDistance = hitDistance;
                nearestObstacle = hit;
            }
        }

        float actualMovement = allowedDistance * direction.x;
        transform.position += new Vector3(actualMovement, 0f, 0f);
        if (nearestObstacle.collider != null)
        {
            TryBeginJump(nearestObstacle.collider);
        }
        return actualMovement;
    }

    private void TryBeginJump(Collider2D obstacle)
    {
        if (!jumpOverObstacles || isJumping || Time.time < nextJumpTime || obstacle == null)
        {
            return;
        }

        float requiredRise = obstacle.bounds.max.y - movementCollider.bounds.min.y + CollisionSkinWidth;
        if (requiredRise <= 0f || requiredRise > jumpHeight)
        {
            return;
        }

        jumpStartY = transform.position.y;
        jumpStartTime = Time.time;
        nextJumpTime = Time.time + jumpDuration + 0.3f;
        isJumping = true;
    }

    private void UpdateJumpMotion()
    {
        if (isJumping)
        {
            float progress = Mathf.Clamp01((Time.time - jumpStartTime) / jumpDuration);
            float desiredY = jumpStartY + 4f * jumpHeight * progress * (1f - progress);
            float verticalMovement = desiredY - transform.position.y;
            bool blocked = MoveVertically(verticalMovement);
            if (blocked || progress >= 1f)
            {
                isJumping = false;
            }
        }
        else if (transform.position.y > startingY + CollisionSkinWidth)
        {
            float fallSpeed = 2f * jumpHeight / jumpDuration;
            float verticalMovement = Mathf.Max(startingY - transform.position.y, -fallSpeed * Time.deltaTime);
            MoveVertically(verticalMovement);
        }
    }

    private bool MoveVertically(float movement)
    {
        if (Mathf.Abs(movement) <= 0.0001f || movementCollider == null || !movementCollider.enabled)
        {
            return false;
        }

        Physics2D.SyncTransforms();
        Vector2 direction = movement > 0f ? Vector2.up : Vector2.down;
        float requestedDistance = Mathf.Abs(movement);
        LayerMask obstacleLayers = LayerMask.GetMask("Box", "Stair");
        ContactFilter2D filter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = obstacleLayers,
            useTriggers = false,
        };
        Bounds ownBounds = movementCollider.bounds;
        int hitCount = Physics2D.BoxCast(
            ownBounds.center,
            ownBounds.size,
            transform.eulerAngles.z,
            direction,
            filter,
            obstacleHits,
            requestedDistance + CollisionSkinWidth
        );

        float allowedDistance = requestedDistance;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = obstacleHits[i];
            if (!IsBlockingVerticalObstacle(hit, direction, ownBounds))
            {
                continue;
            }

            allowedDistance = Mathf.Min(allowedDistance, Mathf.Max(0f, hit.distance - CollisionSkinWidth));
        }

        transform.position += new Vector3(0f, allowedDistance * direction.y, 0f);
        return allowedDistance < requestedDistance - 0.0001f;
    }

    private static bool IsBlockingVerticalObstacle(RaycastHit2D hit, Vector2 direction, Bounds ownBounds)
    {
        if (hit.collider == null || hit.collider.isTrigger
            || (direction.y > 0f && hit.collider.gameObject.layer == LayerMask.NameToLayer("Stair")))
        {
            return false;
        }

        Bounds obstacleBounds = hit.collider.bounds;
        bool horizontallyOverlapping = obstacleBounds.max.x > ownBounds.min.x + CollisionSkinWidth
            && obstacleBounds.min.x < ownBounds.max.x - CollisionSkinWidth;
        if (!horizontallyOverlapping)
        {
            return false;
        }

        if (hit.distance <= 0f || hit.normal.sqrMagnitude < 0.0001f)
        {
            return direction.y > 0f
                ? obstacleBounds.min.y >= ownBounds.center.y
                : obstacleBounds.max.y <= ownBounds.center.y;
        }

        return Vector2.Dot(hit.normal, direction) < -0.01f;
    }

    private static bool IsBlockingObstacle(RaycastHit2D hit, Vector2 direction, Bounds ownBounds)
    {
        if (hit.collider == null || hit.collider.isTrigger)
        {
            return false;
        }

        Bounds obstacleBounds = hit.collider.bounds;
        bool verticallyOverlapping = obstacleBounds.max.y > ownBounds.min.y + CollisionSkinWidth
            && obstacleBounds.min.y < ownBounds.max.y - CollisionSkinWidth;
        if (!verticallyOverlapping)
        {
            return false;
        }

        if (hit.distance <= 0f || hit.normal.sqrMagnitude < 0.0001f)
        {
            return direction.x > 0f
                ? obstacleBounds.min.x >= ownBounds.center.x
                : obstacleBounds.max.x <= ownBounds.center.x;
        }

        return Vector2.Dot(hit.normal, direction) < -0.01f;
    }

    private void StartChasing()
    {
        isChasing = true;

        if (logWhenEnteredCamera)
        {
            Debug.Log(cameraEnteredLogMessage, this);
        }

        TryAssignFollowTarget();
        if (!isMovementPaused)
        {
            SetAnimationController(moveController);
            ChaseTarget();
        }
    }

    private void ChaseTarget()
    {
        if (!TryAssignFollowTarget())
        {
            return;
        }

        Vector3 currentPosition = transform.position;
        float horizontalDelta = followTarget.position.x - currentPosition.x;
        ApplyFacing(horizontalDelta);
        float maxMovement = MonsterData.MoveSpeed * MovementSpeedMultiplier * Time.deltaTime;
        MoveHorizontally(Mathf.Clamp(horizontalDelta, -maxMovement, maxMovement));
    }

    private void FindReferences()
    {
        if (health == null)
        {
            health = GetComponent<MonsterHealth>();
        }

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (movementCollider == null)
        {
            movementCollider = GetComponent<BoxCollider2D>();
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    private bool IsInsideCameraView()
    {
        Camera cameraToUse = GetCamera();
        if (cameraToUse == null)
        {
            return false;
        }

        Vector3 viewportPosition = cameraToUse.WorldToViewportPoint(transform.position);
        return viewportPosition.z >= 0f
            && viewportPosition.x >= 0f
            && viewportPosition.x <= 1f
            && viewportPosition.y >= 0f
            && viewportPosition.y <= 1f;
    }

    private bool TryAssignFollowTarget()
    {
        if (followTarget != null)
        {
            return true;
        }

        ArisaHorizontalMovement player = FindPlayer();
        if (player == null)
        {
            return false;
        }

        followTarget = player.transform;
        return true;
    }

    private static ArisaHorizontalMovement FindPlayer()
    {
#if UNITY_2023_1_OR_NEWER || UNITY_6000_0_OR_NEWER
        return Object.FindFirstObjectByType<ArisaHorizontalMovement>();
#else
        return Object.FindObjectOfType<ArisaHorizontalMovement>();
#endif
    }

    private Camera GetCamera()
    {
        if (targetCamera != null)
        {
            return targetCamera;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            targetCamera = mainCamera;
            return targetCamera;
        }

        Camera[] cameras = Camera.allCameras;
        if (cameras.Length == 0)
        {
            return null;
        }

        targetCamera = cameras[0];
        return targetCamera;
    }

    private void SetAnimationController(RuntimeAnimatorController controller)
    {
        if (animator == null || controller == null || animator.runtimeAnimatorController == controller)
        {
            return;
        }

        animator.runtimeAnimatorController = controller;
    }

    private void ApplyFacing(float horizontalDelta)
    {
        if (Mathf.Abs(horizontalDelta) <= 0.01f)
        {
            return;
        }

        bool shouldFaceRight = horizontalDelta > 0f;
        bool shouldFlip = shouldFaceRight != baseFacesRight;

        if (spriteRenderer != null)
        {
            spriteRenderer.flipX = shouldFlip ? !baseSpriteFlipX : baseSpriteFlipX;
            transform.localScale = baseLocalScale;
            return;
        }

        Vector3 targetScale = baseLocalScale;
        if (shouldFlip)
        {
            targetScale.x = -targetScale.x;
        }
        transform.localScale = targetScale;
    }

    private bool IsFacingRight()
    {
        float scaleSign = baseLocalScale.x >= 0f ? 1f : -1f;
        float flipSign = baseSpriteFlipX ? -1f : 1f;
        return transform.right.x * scaleSign * flipSign >= 0f;
    }
}
