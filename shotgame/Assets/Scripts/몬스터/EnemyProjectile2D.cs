using System;
using UnityEngine;

// 직접 충돌을 검사하므로 anti 레이어 접촉 피해와 중복되지 않는다.
[DisallowMultipleComponent]
public sealed class EnemyProjectile2D : MonsterProjectile
{
    [SerializeField, KoreanLabel("지형 레이어")] private LayerMask groundLayers;
    [SerializeField, KoreanLabel("충돌 반경"), Min(0.02f)] private float collisionRadius = 0.14f;
    [SerializeField, KoreanLabel("폭발 프리팹")] private GameObject explosionPrefab;
    [SerializeField, KoreanLabel("폭발 반경"), Min(0f)] private float explosionRadius;
    [SerializeField, KoreanLabel("폭발 수명"), Min(0.1f)] private float explosionLifetime = 1f;
    [SerializeField, KoreanLabel("폭발 크기 배율"), Min(0.1f)] private float explosionScale = 1f;

    private readonly RaycastHit2D[] hits = new RaycastHit2D[32];
    private Vector2 velocity;
    private float gravity;
    private float liquidSurface;
    private int damage;
    private float expiresAt;
    private bool launched;
    private bool resolved;
    internal event Action<Vector2> ShotDown;

    public override void Launch(Vector2 direction, float speed, float lifetime, int damage, ArisaHealth player)
    {
        Launch(direction, speed, damage, groundLayers, collisionRadius, lifetime,
            explosionPrefab, explosionRadius, explosionLifetime, explosionScale);
    }

    public void Launch(Vector2 direction, float speed, int damage, LayerMask groundLayers,
        float collisionRadius, float lifetime, GameObject explosionPrefab = null,
        float explosionRadius = 0f, float explosionLifetime = 1f, float explosionScale = 1f)
    {
        velocity = (direction.sqrMagnitude > 0f ? direction.normalized : Vector2.left) * Mathf.Max(0f, speed);
        gravity = 0f;
        liquidSurface = float.NegativeInfinity;
        this.damage = Mathf.Max(0, damage);
        this.groundLayers = groundLayers;
        this.collisionRadius = Mathf.Max(0.02f, collisionRadius);
        this.explosionPrefab = explosionPrefab;
        this.explosionRadius = Mathf.Max(0f, explosionRadius);
        this.explosionLifetime = Mathf.Max(0.1f, explosionLifetime);
        this.explosionScale = Mathf.Max(0.1f, explosionScale);
        expiresAt = Time.time + Mathf.Max(0.1f, lifetime);
        launched = true;
        resolved = false;
        Face(direction.sqrMagnitude > 0f ? direction : Vector2.left);
    }

    internal void LaunchArc(Vector2 initialVelocity, float acceleration, float waterY, int damage, ArisaHealth player)
    {
        Launch(initialVelocity, initialVelocity.magnitude, 8f, damage, player);
        gravity = acceleration;
        liquidSurface = waterY;
    }

    private void Update()
    {
        if (!launched || resolved) return;
        float delta = Time.deltaTime;
        Vector2 falling = Vector2.down * (gravity * delta);
        Vector2 movement = (velocity + falling * 0.5f) * delta;
        velocity += falling;
        float distance = movement.magnitude;
        Vector2 direction = distance > 0f ? movement / distance : (Vector2)transform.right;
        if (gravity > 0f) Face(direction);
        Vector2 start = transform.position;
        ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
        bool enteringWater = movement.y < 0f && start.y + movement.y <= liquidSurface;
        if (enteringWater) distance *= Mathf.Clamp01((liquidSurface - start.y) / movement.y);
        int count = Physics2D.CircleCast(start, collisionRadius, direction, filter, hits, distance);
        RaycastHit2D closest = default;
        ArisaHealth directHit = null;
        for (int i = 0; i < count; i++)
        {
            Collider2D collider = hits[i].collider;
            ArisaHealth player = collider.GetComponentInParent<ArisaHealth>();
            bool passingUp = gravity > 0f && movement.y > 0f && collider.usedByEffector;
            bool isGround = !passingUp && !collider.isTrigger && (groundLayers.value & (1 << collider.gameObject.layer)) != 0;
            if (player == null && !isGround) continue;
            if (closest.collider != null && hits[i].distance >= closest.distance) continue;
            closest = hits[i];
            directHit = player;
        }

        if (closest.collider != null)
        {
            transform.position += (Vector3)(direction * closest.distance);
            Impact(directHit);
            return;
        }
        transform.position += (Vector3)(direction * distance);
        if (enteringWater)
        {
            Impact(null);
            return;
        }
        if (Time.time >= expiresAt) Destroy(gameObject);
    }

    private void Face(Vector2 direction) =>
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

    private void Impact(ArisaHealth directHit)
    {
        resolved = true;
        if (explosionRadius > 0f)
        {
            // 여러 플레이어 콜라이더가 겹쳐도 같은 폭발은 한 번만 적용한다.
            System.Collections.Generic.HashSet<ArisaHealth> damaged = new System.Collections.Generic.HashSet<ArisaHealth>();
            if (directHit != null) damaged.Add(directHit);
            foreach (Collider2D collider in Physics2D.OverlapCircleAll(transform.position, explosionRadius))
            {
                ArisaHealth player = collider.GetComponentInParent<ArisaHealth>();
                if (player != null) damaged.Add(player);
            }
            foreach (ArisaHealth player in damaged)
                if (!player.IsDead) player.TakeDamage(damage);
        }
        else if (directHit != null && !directHit.IsDead)
        {
            directHit.TakeDamage(damage);
        }

        FinishImpact();
    }

    public void BreakApart()
    {
        if (resolved) return;
        resolved = true;
        if (TryGetComponent(out Collider2D hitbox)) hitbox.enabled = false;
        ShotDown?.Invoke(transform.position);
        FinishImpact();
    }

    private void FinishImpact()
    {
        if (explosionPrefab != null)
        {
            GameObject effect = Instantiate(explosionPrefab, transform.position, Quaternion.identity, transform.parent);
            effect.transform.localScale *= explosionScale;
            Destroy(effect, explosionLifetime);
        }
        Destroy(gameObject);
    }
}
