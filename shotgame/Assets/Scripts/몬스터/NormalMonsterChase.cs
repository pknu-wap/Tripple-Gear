using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class NormalMonsterChase : MonoBehaviour
{
    [Header("감지")]
    [SerializeField, KoreanLabel("대상 카메라")] private Camera targetCamera;
    [SerializeField, KoreanLabel("카메라 진입 로그")] private bool logWhenEnteredCamera = true;
    [SerializeField, KoreanLabel("카메라 진입 메시지")] private string cameraEnteredLogMessage = "일반몹이 Main Camera 영역에 들어왔습니다.";

    [Header("추적")]
    [SerializeField, KoreanLabel("플레이어")] private Transform followTarget;
    [SerializeField, KoreanLabel("이동 속도"), Min(0f)] private float moveSpeed = 2f;
    [SerializeField, KoreanLabel("Y축 추적")] private bool followYAxis;

    [Header("그래픽")]
    [SerializeField, KoreanLabel("애니메이터")] private Animator animator;
    [SerializeField, KoreanLabel("이동 컨트롤러")] private RuntimeAnimatorController moveController;

    private bool isChasing;
    private SpriteRenderer spriteRenderer;
    private Vector3 baseLocalScale;
    private bool baseSpriteFlipX;
    private bool baseFacesRight;

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
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(0f, moveSpeed);

        if (!Application.isPlaying)
        {
            FindReferences();
        }
    }

    private void Update()
    {
        if (!isChasing)
        {
            if (IsInsideCameraView())
            {
                StartChasing();
            }

            return;
        }

        ChaseTarget();
    }

    private void StartChasing()
    {
        isChasing = true;

        if (logWhenEnteredCamera)
        {
            Debug.Log(cameraEnteredLogMessage, this);
        }

        SetAnimationController(moveController);
        TryAssignFollowTarget();
        ChaseTarget();
    }

    private void ChaseTarget()
    {
        if (!TryAssignFollowTarget())
        {
            return;
        }

        Vector3 currentPosition = transform.position;
        Vector3 targetPosition = followTarget.position;
        if (!followYAxis)
        {
            targetPosition.y = currentPosition.y;
        }

        targetPosition.z = currentPosition.z;
        float horizontalDelta = targetPosition.x - currentPosition.x;
        ApplyFacing(horizontalDelta);

        transform.position = Vector3.MoveTowards(
            currentPosition,
            targetPosition,
            moveSpeed * Time.deltaTime
        );
    }

    private void FindReferences()
    {
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
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
