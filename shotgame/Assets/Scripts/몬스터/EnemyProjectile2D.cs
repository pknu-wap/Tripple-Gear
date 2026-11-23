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
    private Vector2 direction;
    private float speed;
    private int damage;
    private float expiresAt;
    private bool launched;
    private bool resolved;

    public override void Launch(Vector2 direction, float speed, float lifetime, int damage, ArisaHealth player)
    {
        Launch(direction, speed, damage, groundLayers, collisionRadius, lifetime,
            explosionPrefab, explosionRadius, explosionLifetime, explosionScale);
    }

    public void Launch(Vector2 direction, float speed, int damage, LayerMask groundLayers,
        float collisionRadius, float lifetime, GameObject explosionPrefab = null,
        float explosionRadius = 0f, float explosionLifetime = 1f, float explosionScale = 1f)
    {
        this.direction = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.left;
        this.speed = Mathf.Max(0f, speed);
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
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(this.direction.y, this.direction.x) * Mathf.Rad2Deg);
    }

    private void Update()
    {
        if (!launched || resolved) return;
        float distance = speed * Time.deltaTime;
        ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
        int count = Physics2D.CircleCast(transform.position, collisionRadius, direction, filter, hits, distance);
        RaycastHit2D closest = default;
        ArisaHealth directHit = null;
        for (int i = 0; i < count; i++)
        {
            Collider2D collider = hits[i].collider;
            ArisaHealth player = collider.GetComponentInParent<ArisaHealth>();
            bool isGround = !collider.isTrigger && (groundLayers.value & (1 << collider.gameObject.layer)) != 0;
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
        if (Time.time >= expiresAt) Destroy(gameObject);
    }

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

        if (explosionPrefab != null)
        {
            GameObject effect = Instantiate(explosionPrefab, transform.position, Quaternion.identity, transform.parent);
            effect.transform.localScale *= explosionScale;
            Destroy(effect, explosionLifetime);
        }
        Destroy(gameObject);
    }
}
