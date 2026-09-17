using System.Collections;
using UnityEngine;

public enum TeleportGizmoKind
{
    WireBox,
    FilledBox,
    WireSphere,
    FilledSphere,
    Diamond,
}

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class TeleportTrigger : MonoBehaviour
{
    [Header("텔레포트")]
    [SerializeField, KoreanLabel("텔레포트 대상")] private Transform teleportSubject;
    [SerializeField, KoreanLabel("텔레포트 위치")] private Transform teleportPoint;
    [SerializeField, KoreanLabel("직접 텔레포트 좌표")] private Vector3 teleportPosition;
    [SerializeField, KoreanLabel("대상 Z 유지")] private bool keepTargetZ = true;
    [SerializeField, KoreanLabel("아리사만 사용")] private bool requireArisaMovement = true;
    [SerializeField, KoreanLabel("대상 레이어")] private LayerMask targetLayers = ~0;

    [Header("카메라")]
    [SerializeField, KoreanLabel("카메라 매니저")] private CameraManager cameraManager;
    [SerializeField, KoreanLabel("새 카메라 영역 번호")] private int cameraAreaIndex = -1;
    [SerializeField, KoreanLabel("새 카메라 영역 이름")] private string cameraAreaName;
    [SerializeField, KoreanLabel("텔레포트 후 카메라 스냅")] private bool snapCameraAfterTeleport = true;

    [Header("페이드")]
    [SerializeField, KoreanLabel("페이드 컴포넌트")] private TeleportDirectionalFade fade;
    [SerializeField, KoreanLabel("페이드 인 속도"), Min(0.01f)] private float fadeInSpeed = 2f;
    [SerializeField, KoreanLabel("페이드 아웃 속도"), Min(0.01f)] private float fadeOutSpeed = 2f;

    [Header("기즈모")]
    [SerializeField, KoreanLabel("기즈모 표시")] private bool drawGizmos = true;
    [SerializeField, KoreanLabel("트리거 기즈모")] private TeleportGizmoKind triggerGizmo = TeleportGizmoKind.WireBox;
    [SerializeField, KoreanLabel("목적지 기즈모")] private TeleportGizmoKind destinationGizmo = TeleportGizmoKind.WireSphere;
    [SerializeField, KoreanLabel("트리거 기즈모 색상")] private Color triggerGizmoColor = new Color(0.2f, 1f, 0.55f, 0.25f);
    [SerializeField, KoreanLabel("목적지 기즈모 색상")] private Color destinationGizmoColor = new Color(1f, 0.82f, 0.2f, 0.9f);
    [SerializeField, KoreanLabel("목적지 기즈모 크기"), Min(0.05f)] private float destinationGizmoSize = 0.6f;

    private bool isTeleporting;

    private void Reset()
    {
        FindReferences();
        teleportPosition = transform.position;
    }

    private void Awake()
    {
        FindReferences();
    }

    private void OnValidate()
    {
        fadeInSpeed = Mathf.Max(0.01f, fadeInSpeed);
        fadeOutSpeed = Mathf.Max(0.01f, fadeOutSpeed);
        destinationGizmoSize = Mathf.Max(0.05f, destinationGizmoSize);

        if (cameraAreaIndex < -1)
        {
            cameraAreaIndex = -1;
        }

        BoxCollider2D triggerCollider = GetComponent<BoxCollider2D>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isTeleporting || !CanTeleport(other))
        {
            return;
        }

        Transform subject = GetTeleportSubject(other);
        if (subject == null)
        {
            return;
        }

        StartCoroutine(TeleportRoutine(subject));
    }

    private IEnumerator TeleportRoutine(Transform subject)
    {
        isTeleporting = true;

        TeleportDirectionalFade fadeToUse = GetFade();
        if (fadeToUse != null)
        {
            yield return fadeToUse.FadeIn(fadeInSpeed);
        }

        TeleportSubject(subject);
        ApplyCameraArea();

        if (snapCameraAfterTeleport)
        {
            CameraManager manager = GetCameraManager();
            if (manager != null)
            {
                manager.SnapToTarget();
            }
        }

        if (fadeToUse != null)
        {
            yield return fadeToUse.FadeOut(fadeOutSpeed);
        }

        isTeleporting = false;
    }

    private void TeleportSubject(Transform subject)
    {
        Vector3 destination = GetTeleportPosition(subject);
        ArisaHorizontalMovement arisaMovement = subject.GetComponent<ArisaHorizontalMovement>();
        if (arisaMovement == null)
        {
            arisaMovement = subject.GetComponentInParent<ArisaHorizontalMovement>();
        }

        if (arisaMovement != null)
        {
            arisaMovement.TeleportTo(destination);
            return;
        }

        subject.position = destination;
        Physics2D.SyncTransforms();
    }

    private void ApplyCameraArea()
    {
        CameraManager manager = GetCameraManager();
        if (manager == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(cameraAreaName) && manager.SetCameraAreaByName(cameraAreaName))
        {
            return;
        }

        if (cameraAreaIndex >= 0)
        {
            manager.SetCameraAreaByIndex(cameraAreaIndex);
        }
    }

    private bool CanTeleport(Collider2D other)
    {
        if (other == null || !IsInLayerMask(other.gameObject.layer, targetLayers))
        {
            return false;
        }

        return !requireArisaMovement || other.GetComponentInParent<ArisaHorizontalMovement>() != null;
    }

    private Transform GetTeleportSubject(Collider2D other)
    {
        if (teleportSubject != null)
        {
            return teleportSubject;
        }

        ArisaHorizontalMovement arisaMovement = other.GetComponentInParent<ArisaHorizontalMovement>();
        if (arisaMovement != null)
        {
            return arisaMovement.transform;
        }

        return other.attachedRigidbody != null ? other.attachedRigidbody.transform : other.transform;
    }

    private Vector3 GetTeleportPosition(Transform subject)
    {
        Vector3 destination = teleportPoint != null ? teleportPoint.position : teleportPosition;
        if (keepTargetZ && subject != null)
        {
            destination.z = subject.position.z;
        }

        return destination;
    }

    private TeleportDirectionalFade GetFade()
    {
        if (fade == null)
        {
            fade = TeleportDirectionalFade.GetOrCreate();
        }

        return fade;
    }

    private CameraManager GetCameraManager()
    {
        if (cameraManager == null)
        {
            cameraManager = FindCameraManager();
        }

        return cameraManager;
    }

    private void FindReferences()
    {
        BoxCollider2D triggerCollider = GetComponent<BoxCollider2D>();
        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }

        if (cameraManager == null)
        {
            cameraManager = FindCameraManager();
        }

        if (fade == null)
        {
            fade = FindFade();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
        {
            return;
        }

        DrawTriggerGizmo();
        DrawDestinationGizmo();
    }

    private void DrawTriggerGizmo()
    {
        BoxCollider2D triggerCollider = GetComponent<BoxCollider2D>();
        if (triggerCollider == null)
        {
            return;
        }

        Color previousColor = Gizmos.color;
        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.color = triggerGizmoColor;
        Gizmos.matrix = triggerCollider.transform.localToWorldMatrix;
        DrawGizmoShape(triggerGizmo, triggerCollider.offset, new Vector3(triggerCollider.size.x, triggerCollider.size.y, 0f), destinationGizmoSize);
        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }

    private void DrawDestinationGizmo()
    {
        Vector3 destination = teleportPoint != null ? teleportPoint.position : teleportPosition;

        Color previousColor = Gizmos.color;
        Gizmos.color = destinationGizmoColor;
        Gizmos.DrawLine(transform.position, destination);
        DrawGizmoShape(destinationGizmo, destination, Vector3.one * destinationGizmoSize, destinationGizmoSize);
        Gizmos.color = previousColor;
    }

    private static void DrawGizmoShape(TeleportGizmoKind gizmoKind, Vector3 center, Vector3 size, float fallbackSize)
    {
        switch (gizmoKind)
        {
            case TeleportGizmoKind.FilledBox:
                Gizmos.DrawCube(center, size);
                break;
            case TeleportGizmoKind.WireSphere:
                Gizmos.DrawWireSphere(center, Mathf.Max(0.05f, fallbackSize * 0.5f));
                break;
            case TeleportGizmoKind.FilledSphere:
                Gizmos.DrawSphere(center, Mathf.Max(0.05f, fallbackSize * 0.5f));
                break;
            case TeleportGizmoKind.Diamond:
                DrawDiamond(center, Mathf.Max(0.05f, fallbackSize));
                break;
            default:
                Gizmos.DrawWireCube(center, size);
                break;
        }
    }

    private static void DrawDiamond(Vector3 center, float size)
    {
        float halfSize = size * 0.5f;
        Vector3 top = center + Vector3.up * halfSize;
        Vector3 right = center + Vector3.right * halfSize;
        Vector3 bottom = center + Vector3.down * halfSize;
        Vector3 left = center + Vector3.left * halfSize;

        Gizmos.DrawLine(top, right);
        Gizmos.DrawLine(right, bottom);
        Gizmos.DrawLine(bottom, left);
        Gizmos.DrawLine(left, top);
    }

    private static bool IsInLayerMask(int layer, LayerMask layerMask)
    {
        return (layerMask.value & (1 << layer)) != 0;
    }

    private static CameraManager FindCameraManager()
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindFirstObjectByType<CameraManager>();
#else
        return Object.FindObjectOfType<CameraManager>();
#endif
    }

    private static TeleportDirectionalFade FindFade()
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindFirstObjectByType<TeleportDirectionalFade>();
#else
        return Object.FindObjectOfType<TeleportDirectionalFade>();
#endif
    }
}
