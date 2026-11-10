using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator), typeof(SpriteRenderer))]
public sealed class CctvLaserAttack : MonoBehaviour
{
    [SerializeField, KoreanLabel("발사 스프라이트")] private Sprite firingSprite;
    [SerializeField, KoreanLabel("광선 머티리얼")] private Material lineMaterial;
    [SerializeField, KoreanLabel("충돌 레이어")] private LayerMask hitLayers;
    [SerializeField, KoreanLabel("렌즈 위치")] private Vector2 lensOffset = new Vector2(-0.9f, -0.03f);
    [SerializeField, KoreanLabel("발사 방향")] private Vector2 beamDirection = new Vector2(-1f, -0.74f);
    [SerializeField, KoreanLabel("광선 길이"), Min(0.1f)] private float beamRange = 14f;
    [SerializeField, KoreanLabel("광선 폭"), Min(0.01f)] private float beamWidth = 0.1f;
    [SerializeField, KoreanLabel("대기 시간"), Min(0.01f)] private float idleDuration = 3f;
    [SerializeField, KoreanLabel("발사 시간"), Min(0.01f)] private float firingDuration = 5f;
    [SerializeField, KoreanLabel("피격 간격"), Min(0.01f)] private float damageInterval = 1f;
    [SerializeField, KoreanLabel("피해량"), Min(1)] private int damage = 1;

    private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>();
    private Animator animator;
    private SpriteRenderer cameraSprite;
    private Sprite idleSprite;
    private LineRenderer beam;
    private LineRenderer impactDot;
    private float idleAnimationSpeed;
    private float phaseEndsAt;
    private float nextDamageTime;
    private bool isFiring;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        cameraSprite = GetComponent<SpriteRenderer>();
        beam = CreateLine("CCTV 레이저", beamWidth, 0);
        impactDot = CreateLine("CCTV 레이저 충돌점", beamWidth * 3f, 8);
    }

    private void OnEnable()
    {
        phaseEndsAt = Time.time + idleDuration;
    }

    private void OnDisable()
    {
        if (isFiring)
        {
            EndFiring();
        }
    }

    private void LateUpdate()
    {
        if (Time.time >= phaseEndsAt)
        {
            if (isFiring)
            {
                EndFiring();
                return;
            }

            idleSprite = cameraSprite.sprite;
            idleAnimationSpeed = animator.speed;
            animator.speed = 0f;
            isFiring = true;
            phaseEndsAt = Time.time + firingDuration;
            nextDamageTime = Time.time;
            beam.enabled = true;
        }

        if (isFiring)
        {
            cameraSprite.sprite = firingSprite;
            UpdateBeam();
        }
    }

    private void EndFiring()
    {
        isFiring = false;
        animator.speed = idleAnimationSpeed;
        cameraSprite.sprite = idleSprite;
        beam.enabled = false;
        impactDot.enabled = false;
        phaseEndsAt = Time.time + idleDuration;
    }

    private void UpdateBeam()
    {
        Vector3 origin = transform.TransformPoint(lensOffset);
        Vector2 direction = ((Vector2)transform.TransformDirection(beamDirection)).normalized;
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(hitLayers);
        int hitCount = Physics2D.CircleCast(origin, beamWidth * 0.5f, direction, filter, hits, beamRange);

        RaycastHit2D hit = hitCount > 0 ? hits[0] : default;
        Vector3 end = origin + (Vector3)(direction * (hit.collider != null ? hit.distance : beamRange));
        beam.SetPosition(0, origin);
        beam.SetPosition(1, end);

        impactDot.enabled = hit.collider != null;
        if (!impactDot.enabled)
        {
            return;
        }

        Vector3 contact = new Vector3(hit.point.x, hit.point.y, origin.z);
        Vector3 dotHalfSegment = Vector3.right * (beamWidth * 0.05f);
        impactDot.SetPosition(0, contact - dotHalfSegment);
        impactDot.SetPosition(1, contact + dotHalfSegment);

        if (Time.time < nextDamageTime)
        {
            return;
        }

        ArisaHealth player = hit.collider.GetComponentInParent<ArisaHealth>();
        if (player != null && !player.IsDead)
        {
            nextDamageTime = Time.time + damageInterval;
            player.TakeDamage(damage, origin);
        }
    }

    private LineRenderer CreateLine(string objectName, float width, int capVertices)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(transform, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.sharedMaterial = lineMaterial;
        line.sortingLayerID = cameraSprite.sortingLayerID;
        line.sortingOrder = cameraSprite.sortingOrder + 1;
        line.startColor = line.endColor = Color.red;
        line.widthMultiplier = width;
        line.numCapVertices = capVertices;
        line.positionCount = 2;
        line.enabled = false;
        return line;
    }
}
