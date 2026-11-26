using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MonsterHealth), typeof(SpriteRenderer), typeof(MonsterDeathEffect))]
[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public sealed class ExplosiveMonster : MonoBehaviour
{
    private enum Phase
    {
        Idle,
        Chase,
        Fuse,
    }

    private const float FuseDuration = 3f;
    private static readonly int IsMoving = Animator.StringToHash("IsMoving");
    private static readonly Color WarningColor = new Color(1f, 0.24f, 0.1f);
    private static readonly ContactFilter2D BodyFilter = new ContactFilter2D { useTriggers = false };

    [SerializeField, KoreanLabel("폭발 반경"), Min(0.1f)] private float blastRadius = 1.5f;
    [SerializeField, KoreanLabel("폭발 효과 프리팹")] private GameObject explosionEffectPrefab;

    internal GameObject ExplosionEffectPrefab => explosionEffectPrefab;
    internal bool IsIdle => phase == Phase.Idle;

    private readonly Plane[] cameraPlanes = new Plane[6];
    private readonly List<Collider2D> overlaps = new List<Collider2D>();
    private readonly RaycastHit2D[] wallHits = new RaycastHit2D[8];
    private MonsterHealth health;
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D body;
    private BoxCollider2D contactCollider;
    private MonsterGroundMotion groundMotion;
    private ContactFilter2D wallFilter;
    private Animator animator;
    private ArisaHealth targetPlayer;
    private Collider2D targetCollider;
    private Color idleColor;
    private bool idleFlipX;
    private Phase phase;
    private double fuseEndsAt;

    private void Awake()
    {
        health = GetComponent<MonsterHealth>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        body = GetComponent<Rigidbody2D>();
        contactCollider = GetComponent<BoxCollider2D>();
        groundMotion = new MonsterGroundMotion(contactCollider);
        animator = GetComponent<Animator>();
        wallFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = LayerMask.GetMask("Box", "WaveBoundary"),
            useTriggers = false,
        };
        idleColor = spriteRenderer.color;
        idleFlipX = spriteRenderer.flipX;
    }

    private void OnEnable()
    {
        groundMotion.Reset();
        phase = Phase.Idle;
        SetTarget(null);
        spriteRenderer.color = idleColor;
        spriteRenderer.flipX = idleFlipX;
        SetMoving(false);
    }

    private void LateUpdate()
    {
        if (GameplayPause.IsPaused || phase != Phase.Idle || health.IsDead || health.IsStunned || health.IsEntering)
        {
            return;
        }

        Camera battleCamera = Camera.main;
        if (battleCamera == null || (battleCamera.cullingMask & (1 << gameObject.layer)) == 0)
        {
            return;
        }

        GeometryUtility.CalculateFrustumPlanes(battleCamera, cameraPlanes);
        if (GeometryUtility.TestPlanesAABB(cameraPlanes, spriteRenderer.bounds))
        {
            phase = Phase.Chase;
        }
    }

    private void FixedUpdate()
    {
        if (health.IsDead || health.IsEntering)
        {
            SetMoving(false);
            return;
        }

        if (health.IsStunned)
        {
            if (phase == Phase.Fuse) fuseEndsAt += Time.fixedDeltaTime;
            SetMoving(false);
            return;
        }

        groundMotion.Step();
        double now = Time.fixedTimeAsDouble;
        switch (phase)
        {
            case Phase.Chase:
                Chase(now);
                break;
            case Phase.Fuse:
                UpdateFuse(now);
                break;
        }
    }

    private void Chase(double now)
    {
        contactCollider.Overlap(BodyFilter, overlaps);
        ArisaHealth player = FindLivingPlayer();
        if (player != null)
        {
            phase = Phase.Fuse;
            SetTarget(player);
            fuseEndsAt = now + FuseDuration;
            UpdateFuse(now);
            return;
        }

        if (targetPlayer == null)
        {
            SetTarget(FindFirstObjectByType<ArisaHealth>());
        }
        FollowTarget();
    }

    private void UpdateFuse(double now)
    {
        if (now >= fuseEndsAt)
        {
            Explode();
            return;
        }

        float elapsed = FuseDuration - (float)(fuseEndsAt - now);
        spriteRenderer.color = Color.Lerp(WarningColor, idleColor,
            Mathf.PingPong(elapsed * (4f + elapsed), 1f));

        FollowTarget();
    }

    private void SetTarget(ArisaHealth player)
    {
        targetPlayer = player;
        targetCollider = player != null ? player.GetComponent<Collider2D>() : null;
    }

    private void FollowTarget()
    {
        if (targetPlayer == null || targetPlayer.IsDead || !targetPlayer.isActiveAndEnabled)
        {
            SetMoving(false);
            return;
        }

        float targetX = targetCollider != null ? targetCollider.bounds.center.x : targetPlayer.transform.position.x;
        MoveTowards(targetX);
    }

    private void MoveTowards(float targetX)
    {
        Vector2 position = body.position;
        float movement = Mathf.MoveTowards(position.x, targetX, health.Data.MoveSpeed * Time.fixedDeltaTime) - position.x;
        if (Mathf.Abs(movement) <= 0.0001f)
        {
            SetMoving(false);
            return;
        }

        float sign = Mathf.Sign(movement);
        spriteRenderer.flipX = sign < 0f;
        Vector2 direction = new Vector2(sign, 0f);
        Bounds bounds = contactCollider.bounds;
        float skin = MonsterGroundMotion.SkinWidth;
        float distance = Mathf.Abs(movement);
        Vector2 castSize = new Vector2(bounds.size.x, Mathf.Max(skin, bounds.size.y - 2f * skin));
        int count = Physics2D.BoxCast(bounds.center, castSize, 0f, direction, wallFilter, wallHits, distance + skin);
        for (int i = 0; i < count; i++)
        {
            RaycastHit2D hit = wallHits[i];
            if (hit.collider != null && Vector2.Dot(hit.normal, direction) < -0.01f)
            {
                distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - skin));
            }
        }

        SetMoving(distance > 0.0001f);
        if (distance > 0.0001f)
        {
            body.MovePosition(position + direction * distance);
        }
    }

    private void SetMoving(bool moving)
    {
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.SetBool(IsMoving, moving);
        }
    }

    private void Explode()
    {
        Physics2D.OverlapCircle(body.position, blastRadius, BodyFilter, overlaps);
        ArisaHealth player = FindLivingPlayer();
        if (player != null)
        {
            player.TakeDamage(health.Data.AttackDamage, body.position);
        }

        health.TakeDamage(health.CurrentHp);
    }

    private ArisaHealth FindLivingPlayer()
    {
        foreach (Collider2D overlap in overlaps)
        {
            ArisaHealth player = overlap.GetComponentInParent<ArisaHealth>();
            if (player != null && !player.IsDead)
            {
                return player;
            }
        }

        return null;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = WarningColor;
        Gizmos.DrawWireSphere(transform.position, blastRadius);
    }
}
