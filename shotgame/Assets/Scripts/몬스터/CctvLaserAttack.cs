using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class CctvLaserAttack : MonoBehaviour
{
    [System.Serializable]
    private struct ScanFrame
    {
        public Sprite sprite;
        public Vector2 lensOffset;
        public Vector2 beamDirection;
    }

    private static readonly Color ScanColor = new Color(1f, 0.25f, 0.15f, 0.45f);
    private static readonly Color AttackColor = Color.red;

    [SerializeField, KoreanLabel("광선 머티리얼")] private Material lineMaterial;
    [SerializeField, KoreanLabel("충돌 레이어")] private LayerMask hitLayers;
    [SerializeField, KoreanLabel("렌즈 위치")] private Vector2 lensOffset = new Vector2(-0.9f, -0.03f);
    [SerializeField, KoreanLabel("발사 방향")] private Vector2 beamDirection = new Vector2(-1f, -0.74f);
    [SerializeField, KoreanLabel("카메라 프레임별 광선")] private ScanFrame[] scanFrames = System.Array.Empty<ScanFrame>();
    [SerializeField, KoreanLabel("광선 길이"), Min(0.1f)] private float beamRange = 14f;
    [SerializeField, KoreanLabel("광선 폭"), Min(0.01f)] private float beamWidth = 0.1f;
    [SerializeField, KoreanLabel("피격 간격"), Min(0.01f)] private float damageInterval = 1f;
    [SerializeField, KoreanLabel("피해량"), Min(1)] private int damage = 1;

    private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>();
    private SpriteRenderer cameraSprite;
    private MonsterHealth health;
    private LineRenderer beam;
    private LineRenderer impactDot;
    private float nextDamageTime;

    private void Awake()
    {
        cameraSprite = GetComponent<SpriteRenderer>();
        health = GetComponentInParent<MonsterHealth>();
        beam = CreateLine("CCTV 레이저", beamWidth, 0);
        impactDot = CreateLine("CCTV 레이저 충돌점", beamWidth * 3f, 8);
    }

    private void OnDisable()
    {
        beam.enabled = false;
        impactDot.enabled = false;
    }

    private void LateUpdate()
    {
        if (GameplayPause.IsPaused) return;
        if (health != null && health.IsEntering)
        {
            beam.enabled = false;
            impactDot.enabled = false;
            return;
        }

        GetScanPose(out Vector2 localOrigin, out Vector2 localDirection);
        Vector3 origin = transform.TransformPoint(localOrigin);
        Vector2 direction = ((Vector2)transform.TransformDirection(localDirection)).normalized;
        ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(hitLayers);
        int hitCount = Physics2D.CircleCast(origin, beamWidth * 0.5f, direction, filter, hits, beamRange);

        RaycastHit2D hit = hitCount > 0 ? hits[0] : default;
        ArisaHealth player = hit.collider != null ? hit.collider.GetComponentInParent<ArisaHealth>() : null;
        bool detected = player != null && !player.IsDead;
        Color beamColor = detected ? AttackColor : ScanColor;
        beam.startColor = beam.endColor = beamColor;
        impactDot.startColor = impactDot.endColor = beamColor;

        Vector3 end = origin + (Vector3)(direction * (hit.collider != null ? hit.distance : beamRange));
        beam.SetPosition(0, origin);
        beam.SetPosition(1, end);
        beam.enabled = true;

        impactDot.enabled = hit.collider != null;
        if (impactDot.enabled)
        {
            Vector3 contact = new Vector3(hit.point.x, hit.point.y, origin.z);
            Vector3 dotHalfSegment = Vector3.right * (beamWidth * 0.05f);
            impactDot.SetPosition(0, contact - dotHalfSegment);
            impactDot.SetPosition(1, contact + dotHalfSegment);
        }

        if (detected && Time.time >= nextDamageTime)
        {
            nextDamageTime = Time.time + damageInterval;
            player.TakeDamage(damage, origin);
        }
    }

    private void GetScanPose(out Vector2 localOrigin, out Vector2 localDirection)
    {
        foreach (ScanFrame frame in scanFrames)
        {
            if (frame.sprite == cameraSprite.sprite)
            {
                localOrigin = frame.lensOffset;
                localDirection = frame.beamDirection;
                return;
            }
        }

        localOrigin = lensOffset;
        localDirection = beamDirection;
    }

    private LineRenderer CreateLine(string objectName, float width, int capVertices)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.sharedMaterial = lineMaterial;
        line.sortingLayerID = cameraSprite.sortingLayerID;
        line.sortingOrder = cameraSprite.sortingOrder + 1;
        line.startColor = line.endColor = ScanColor;
        line.widthMultiplier = width;
        line.numCapVertices = capVertices;
        line.positionCount = 2;
        line.enabled = false;
        return line;
    }
}
