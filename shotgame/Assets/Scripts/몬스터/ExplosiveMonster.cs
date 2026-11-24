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
        Patrol,
        Fuse,
    }

    private const float FuseDuration = 3f;
    private static readonly Color WarningColor = new Color(1f, 0.24f, 0.1f);
    private static readonly ContactFilter2D BodyFilter = new ContactFilter2D { useTriggers = false };

    [SerializeField, KoreanLabel("방향 전환 간격 (초)"), Min(0.1f)] private float turnInterval = 2f;
    [SerializeField, KoreanLabel("폭발 반경"), Min(0.1f)] private float blastRadius = 1.5f;
    [SerializeField, KoreanLabel("폭발 효과 프리팹")] private GameObject explosionEffectPrefab;

    private readonly Plane[] cameraPlanes = new Plane[6];
    private readonly List<Collider2D> overlaps = new List<Collider2D>();
    private MonsterHealth health;
    private SpriteRenderer spriteRenderer;
    private Rigidbody2D body;
    private BoxCollider2D contactCollider;
    private MonsterGroundMotion groundMotion;
    private float patrolOriginX;
    private Color patrolColor;
    private Phase phase;
    private float patrolElapsed;
    private double fuseEndsAt;

    private void Awake()
    {
        health = GetComponent<MonsterHealth>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        body = GetComponent<Rigidbody2D>();
        contactCollider = GetComponent<BoxCollider2D>();
        groundMotion = new MonsterGroundMotion(contactCollider);
        patrolOriginX = body.position.x;
        patrolColor = spriteRenderer.color;
    }

    private void OnEnable() => groundMotion.Reset();

    private void LateUpdate()
    {
        if (phase != Phase.Idle || health.IsDead || health.IsStunned || health.IsEntering)
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
            phase = Phase.Patrol;
        }
    }

    private void FixedUpdate()
    {
        if (health.IsDead || health.IsEntering)
        {
            return;
        }

        if (health.IsStunned)
        {
            if (phase == Phase.Fuse) fuseEndsAt += Time.fixedDeltaTime;
            return;
        }

        groundMotion.Step();
        double now = Time.fixedTimeAsDouble;
        switch (phase)
        {
            case Phase.Patrol:
                Patrol(now);
                break;
            case Phase.Fuse:
                if (now >= fuseEndsAt)
                {
                    Explode();
                }
                else
                {
                    float elapsed = FuseDuration - (float)(fuseEndsAt - now);
                    spriteRenderer.color = Color.Lerp(WarningColor, patrolColor,
                        Mathf.PingPong(elapsed * 4f, 1f));
                }
                break;
        }
    }

    private void Patrol(double now)
    {
        contactCollider.Overlap(BodyFilter, overlaps);
        if (FindLivingPlayer() != null)
        {
            phase = Phase.Fuse;
            fuseEndsAt = now + FuseDuration;
            spriteRenderer.color = WarningColor;
            return;
        }

        float interval = Mathf.Max(0.1f, turnInterval);
        patrolElapsed = Mathf.Repeat(patrolElapsed + Time.fixedDeltaTime, 2f * interval);
        float distance = Mathf.PingPong(patrolElapsed, interval) * health.Data.MoveSpeed;
        Vector2 position = body.position;
        body.MovePosition(new Vector2(patrolOriginX - distance, position.y));
    }

    private void Explode()
    {
        if (explosionEffectPrefab != null)
        {
            Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
        }

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
