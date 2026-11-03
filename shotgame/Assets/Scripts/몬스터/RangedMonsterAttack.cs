using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MonsterHealth))]
public sealed class RangedMonsterAttack : MonoBehaviour
{
    [SerializeField, KoreanLabel("탄환 스프라이트")] private Sprite bulletSprite;
    [SerializeField, KoreanLabel("탄환 머티리얼")] private Material bulletMaterial;
    [SerializeField, KoreanLabel("탄환 속도"), Min(0.1f)] private float bulletSpeed = 4.5f;
    [SerializeField, KoreanLabel("지형 레이어")] private LayerMask groundLayers;
    private MonsterHealth health;
    private ArisaHealth target;
    private Transform projectileParent;
    private Vector3 hoverOrigin;
    private float nextShotAt;
    private float hoverPhase;

    private void Awake()
    {
        health = GetComponent<MonsterHealth>();
        health.Defeated.AddListener(HandleDefeated);
    }

    public void Initialize(ArisaHealth player, Transform projectileParent)
    {
        target = player;
        this.projectileParent = projectileParent;
        hoverOrigin = transform.position;
        hoverPhase = Random.Range(0f, Mathf.PI * 2f);
        nextShotAt = Time.time + (health.Data != null ? health.Data.FirstAttackDelay : 1f);
    }

    private void Update()
    {
        if (health.IsDead || health.Data == null || target == null || target.IsDead) return;
        transform.position = hoverOrigin + Vector3.up * (Mathf.Sin(Time.time * 2f + hoverPhase) * 0.12f);
        if (Time.time < nextShotAt) return;
        nextShotAt = Time.time + health.Data.AttackInterval;
        Collider2D playerCollider = target.GetComponent<Collider2D>();
        Vector2 targetPosition = playerCollider != null ? (Vector2)playerCollider.bounds.center : (Vector2)target.transform.position;
        Vector2 direction = (targetPosition - (Vector2)transform.position).normalized;
        GameObject bullet = new GameObject("원거리 몹 탄환");
        bullet.transform.SetParent(projectileParent, false);
        bullet.transform.position = transform.position;
        SpriteRenderer renderer = bullet.AddComponent<SpriteRenderer>();
        renderer.sprite = bulletSprite;
        if (bulletMaterial != null) renderer.sharedMaterial = bulletMaterial;
        renderer.color = new Color(1f, 0.35f, 0.15f);
        renderer.sortingOrder = 25;
        if (bulletSprite != null)
            bullet.transform.localScale = Vector3.one * (0.3f / Mathf.Max(0.01f, bulletSprite.bounds.size.x));
        bullet.AddComponent<EnemyProjectile2D>().Launch(direction, bulletSpeed, health.Data.AttackDamage,
            groundLayers.value != 0 ? groundLayers : LayerMask.GetMask("Box", "Stair"), 0.09f, 8f);
    }

    private void HandleDefeated()
    {
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (health != null) health.Defeated.RemoveListener(HandleDefeated);
    }
}
