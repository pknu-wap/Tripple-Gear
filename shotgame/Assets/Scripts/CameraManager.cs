using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public sealed class CameraManager : MonoBehaviour
{
    [Header("카메라")]
    [SerializeField, KoreanLabel("관리할 카메라")] private Camera managedCamera;
    [SerializeField, KoreanLabel("직교 카메라 크기"), Min(0.1f)] private float orthographicSize = 5f;

    [Header("추적")]
    [SerializeField, KoreanLabel("추적 대상")] private Transform followTarget;
    [SerializeField, KoreanLabel("추적 위치 보정")] private Vector3 followOffset = new Vector3(0f, 0f, -10f);
    [SerializeField, KoreanLabel("부드럽게 따라가는 시간"), Min(0f)] private float smoothTime = 0.1f;

    [Header("영역 제한")]
    [SerializeField, KoreanLabel("카메라 영역 사용")] private bool useCameraArea = true;
    [SerializeField, KoreanLabel("영역 최소 좌표")] private Vector2 areaMin = new Vector2(-10f, -5f);
    [SerializeField, KoreanLabel("영역 최대 좌표")] private Vector2 areaMax = new Vector2(10f, 5f);
    [SerializeField, KoreanLabel("화면 전체를 영역 안에 유지")] private bool keepWholeViewInsideArea = true;

    [Header("기즈모")]
    [SerializeField, KoreanLabel("영역 기즈모 표시")] private bool drawAreaGizmo = true;
    [SerializeField, KoreanLabel("영역 기즈모 색상")] private Color areaGizmoColor = new Color(0.15f, 0.7f, 1f, 0.25f);

    private Vector3 followVelocity;

    public Camera ManagedCamera => GetCamera();
    public Transform FollowTarget => followTarget;
    public float OrthographicSize => orthographicSize;
    public bool UseCameraArea => useCameraArea;
    public Vector2 AreaMin => GetAreaMin();
    public Vector2 AreaMax => GetAreaMax();

    private void Reset()
    {
        FindCamera();
    }

    private void Awake()
    {
        FindCamera();
        ApplyCameraSize();
        NormalizeArea();
    }

    private void OnValidate()
    {
        orthographicSize = Mathf.Max(0.1f, orthographicSize);
        ApplyCameraSize();
        smoothTime = Mathf.Max(0f, smoothTime);
        NormalizeArea();
    }

    private void LateUpdate()
    {
        Camera cameraToUse = GetCamera();
        Transform cameraTransform = GetCameraTransform(cameraToUse);
        Vector3 desiredPosition = GetDesiredPosition();
        Vector3 nextPosition = desiredPosition;

        if (followTarget != null && smoothTime > 0f)
        {
            nextPosition = Vector3.SmoothDamp(
                cameraTransform.position,
                desiredPosition,
                ref followVelocity,
                smoothTime
            );
        }

        cameraTransform.position = ClampToCameraArea(nextPosition, cameraToUse);
        ApplyCameraSize();
    }

    public void SetFollowTarget(Transform target)
    {
        SetFollowTarget(target, false);
    }

    public void SetFollowTarget(Transform target, bool snapImmediately)
    {
        followTarget = target;
        followVelocity = Vector3.zero;

        if (snapImmediately)
        {
            SnapToTarget();
        }
    }

    public void ClearFollowTarget()
    {
        followTarget = null;
        followVelocity = Vector3.zero;
    }

    public void SetOrthographicSize(float size)
    {
        orthographicSize = Mathf.Max(0.1f, size);
        ApplyCameraSize();
        ClampCurrentPosition();
    }

    public void SetCameraArea(Vector2 min, Vector2 max)
    {
        areaMin = min;
        areaMax = max;
        useCameraArea = true;
        NormalizeArea();
        ClampCurrentPosition();
    }

    public void SetCameraArea(Rect area)
    {
        SetCameraArea(area.min, area.max);
    }

    public void SetCameraArea(Bounds area)
    {
        Vector3 min = area.min;
        Vector3 max = area.max;
        SetCameraArea(new Vector2(min.x, min.y), new Vector2(max.x, max.y));
    }

    public void ClearCameraArea()
    {
        useCameraArea = false;
    }

    public void SnapToTarget()
    {
        followVelocity = Vector3.zero;
        Camera cameraToUse = GetCamera();
        GetCameraTransform(cameraToUse).position = ClampToCameraArea(GetDesiredPosition(), cameraToUse);
    }

    public void ClampCurrentPosition()
    {
        Camera cameraToUse = GetCamera();
        Transform cameraTransform = GetCameraTransform(cameraToUse);
        cameraTransform.position = ClampToCameraArea(cameraTransform.position, cameraToUse);
    }

    private void FindCamera()
    {
        if (managedCamera == null)
        {
            managedCamera = GetComponent<Camera>();
        }

        if (managedCamera == null)
        {
            managedCamera = GetComponentInChildren<Camera>();
        }

        if (managedCamera == null)
        {
            managedCamera = Camera.main;
        }
    }

    private Camera GetCamera()
    {
        if (managedCamera == null)
        {
            FindCamera();
        }

        return managedCamera;
    }

    private Transform GetCameraTransform(Camera cameraToUse)
    {
        return cameraToUse != null ? cameraToUse.transform : transform;
    }

    private void ApplyCameraSize()
    {
        Camera cameraToUse = GetCamera();
        if (cameraToUse != null && cameraToUse.orthographic)
        {
            cameraToUse.orthographicSize = orthographicSize;
        }
    }

    private Vector3 GetDesiredPosition()
    {
        if (followTarget == null)
        {
            return GetCameraTransform(GetCamera()).position;
        }

        return followTarget.position + followOffset;
    }

    private Vector3 ClampToCameraArea(Vector3 position, Camera cameraToUse)
    {
        if (!useCameraArea)
        {
            return position;
        }

        Vector2 min = GetAreaMin();
        Vector2 max = GetAreaMax();
        Vector2 halfViewSize = keepWholeViewInsideArea ? GetHalfViewSize(cameraToUse) : Vector2.zero;

        float minX = min.x + halfViewSize.x;
        float maxX = max.x - halfViewSize.x;
        float minY = min.y + halfViewSize.y;
        float maxY = max.y - halfViewSize.y;

        position.x = minX > maxX
            ? (min.x + max.x) * 0.5f
            : Mathf.Clamp(position.x, minX, maxX);
        position.y = minY > maxY
            ? (min.y + max.y) * 0.5f
            : Mathf.Clamp(position.y, minY, maxY);

        return position;
    }

    private Vector2 GetHalfViewSize(Camera cameraToUse)
    {
        if (cameraToUse == null || !cameraToUse.orthographic)
        {
            return Vector2.zero;
        }

        float halfHeight = cameraToUse.orthographicSize;
        return new Vector2(halfHeight * cameraToUse.aspect, halfHeight);
    }

    private void NormalizeArea()
    {
        Vector2 min = GetAreaMin();
        Vector2 max = GetAreaMax();
        areaMin = min;
        areaMax = max;
    }

    private Vector2 GetAreaMin()
    {
        return new Vector2(
            Mathf.Min(areaMin.x, areaMax.x),
            Mathf.Min(areaMin.y, areaMax.y)
        );
    }

    private Vector2 GetAreaMax()
    {
        return new Vector2(
            Mathf.Max(areaMin.x, areaMax.x),
            Mathf.Max(areaMin.y, areaMax.y)
        );
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawAreaGizmo || !useCameraArea)
        {
            return;
        }

        Vector2 min = GetAreaMin();
        Vector2 max = GetAreaMax();
        Vector3 center = new Vector3((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f, 0f);
        Vector3 size = new Vector3(max.x - min.x, max.y - min.y, 0f);

        Color previousColor = Gizmos.color;
        Gizmos.color = areaGizmoColor;
        Gizmos.DrawCube(center, size);
        Gizmos.color = new Color(areaGizmoColor.r, areaGizmoColor.g, areaGizmoColor.b, 1f);
        Gizmos.DrawWireCube(center, size);
        Gizmos.color = previousColor;
    }
}
