using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CircleCollider2D), typeof(Rigidbody2D))]
public sealed class FlyingMonsterProjectile : MonoBehaviour
{
    [SerializeField, KoreanLabel("막히는 레이어")] private LayerMask blockingLayers;

    private CircleCollider2D projectileCollider;
    private ArisaHealth target;
    private Vector2 direction;
    private float speed;
    private float expiresAt;
    private int damage;
    private bool launched;
    private bool hasHit;

    public void Launch(Vector2 launchDirection, float launchSpeed, float lifetime, int attackDamage, ArisaHealth player)
    {
        direction = launchDirection.sqrMagnitude > 0.0001f ? launchDirection.normalized : Vector2.down;
        speed = Mathf.Max(0f, launchSpeed);
        expiresAt = Time.time + Mathf.Max(0.1f, lifetime);
        damage = Mathf.Max(0, attackDamage);
        target = player;
        launched = true;
        hasHit = false;
    }

    private void Awake()
    {
        projectileCollider = GetComponent<CircleCollider2D>();
    }

    private void Reset()
    {
        blockingLayers = LayerMask.GetMask("Box");
        GetComponent<CircleCollider2D>().isTrigger = true;
        Rigidbody2D body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
    }

    private void Update()
    {
        if (!launched || hasHit)
        {
            return;
        }

        if (Time.time >= expiresAt)
        {
            Destroy(gameObject);
            return;
        }

        float distance = speed * Time.deltaTime;
        if (distance <= 0f)
        {
            return;
        }

        int hitMask = blockingLayers.value;
        if (target != null)
        {
            hitMask |= 1 << target.gameObject.layer;
        }

        float radius = projectileCollider.radius * Mathf.Max(
            Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.y)
        );
        Vector3 startPosition = transform.position;
        RaycastHit2D[] hits = Physics2D.CircleCastAll(startPosition, radius, direction, distance, hitMask);
        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null)
            {
                continue;
            }

            bool hitsPlayer = target != null && hit.collider.GetComponentInParent<ArisaHealth>() == target;
            bool hitsWall = (blockingLayers.value & (1 << hit.collider.gameObject.layer)) != 0;
            if (hitsPlayer || hitsWall)
            {
                transform.position = hit.centroid;
                TryResolveHit(hit.collider);
                return;
            }
        }

        transform.position = startPosition + (Vector3)(direction * distance);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (launched && !hasHit)
        {
            TryResolveHit(other);
        }
    }

    private bool TryResolveHit(Collider2D other)
    {
        if (other == null)
        {
            return false;
        }

        ArisaHealth player = other.GetComponentInParent<ArisaHealth>();
        if (target != null && player == target)
        {
            hasHit = true;
            target.TakeDamage(damage, transform.position);
            Destroy(gameObject);
            return true;
        }

        if ((blockingLayers.value & (1 << other.gameObject.layer)) == 0)
        {
            return false;
        }

        hasHit = true;
        Destroy(gameObject);
        return true;
    }
}
