using UnityEngine;

internal sealed class MonsterGroundMotion
{
    internal const float SkinWidth = 0.02f;
    private const float GroundRecoveryDistance = 0.4f;

    private readonly BoxCollider2D body;
    private readonly ContactFilter2D terrainFilter;
    private readonly int oneWayLayer = LayerMask.NameToLayer("Stair");
    private readonly RaycastHit2D[] hits = new RaycastHit2D[16];
    private ArisaHorizontalMovement playerMovement;
    private float verticalSpeed;

    internal bool IsGrounded { get; private set; }
    internal bool IsJumping { get; private set; }

    internal MonsterGroundMotion(BoxCollider2D body)
    {
        this.body = body;
        terrainFilter = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = LayerMask.GetMask("Box", "Stair"),
            useTriggers = false,
        };
        playerMovement = Object.FindFirstObjectByType<ArisaHorizontalMovement>();
    }

    internal void Reset()
    {
        verticalSpeed = 0f;
        IsGrounded = false;
        IsJumping = false;
    }

    internal void Jump(float height)
    {
        float gravity = ArisaHorizontalMovement.GetGravity(playerMovement);
        // Account for the first gravity step before the monster moves.
        verticalSpeed = Mathf.Sqrt(2f * gravity * height) + gravity * Time.fixedDeltaTime * 0.5f;
        IsGrounded = false;
        IsJumping = true;
    }

    internal void Step()
    {
        if (!body.enabled) return;

        if (playerMovement == null)
        {
            playerMovement = Object.FindFirstObjectByType<ArisaHorizontalMovement>();
        }

        verticalSpeed = ArisaHorizontalMovement.ApplyGravity(verticalSpeed, playerMovement);
        float movement = verticalSpeed * Time.fixedDeltaTime;
        bool rising = movement > 0f;

        Physics2D.SyncTransforms();
        Bounds bounds = body.bounds;
        // Probe slightly above the feet so small spawn overlaps can settle onto a surface.
        float inset = rising ? 0f : Mathf.Min(GroundRecoveryDistance, bounds.size.y * 0.4f);
        Vector2 center = (Vector2)bounds.center + Vector2.up * (inset * 0.5f);
        Vector2 size = new Vector2(bounds.size.x, bounds.size.y - inset);
        Vector2 direction = rising ? Vector2.up : Vector2.down;
        float allowedDistance = Mathf.Abs(movement);
        int count = Physics2D.BoxCast(center, size, 0f, direction, terrainFilter,
            hits, allowedDistance + inset + SkinWidth);

        bool blocked = false;
        for (int i = 0; i < count; i++)
        {
            RaycastHit2D hit = hits[i];
            if (hit.collider == null || hit.collider == body || hit.collider.transform.IsChildOf(body.transform)
                || hit.distance <= 0f || Vector2.Dot(hit.normal, direction) >= -0.01f)
            {
                continue;
            }

            bool oneWay = hit.collider.gameObject.layer == oneWayLayer;
            if (oneWay && (rising || hit.distance < inset - SkinWidth))
            {
                continue;
            }

            allowedDistance = Mathf.Min(allowedDistance, hit.distance - inset - SkinWidth);
            blocked = true;
        }

        if (Mathf.Abs(allowedDistance) > 0.0001f)
        {
            body.transform.position += (Vector3)(direction * allowedDistance);
            Physics2D.SyncTransforms();
        }
        IsGrounded = !rising && blocked;
        if (blocked) verticalSpeed = 0f;
        if (IsGrounded) IsJumping = false;
    }
}
