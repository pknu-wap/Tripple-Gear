using UnityEngine;

public sealed partial class ArisaHorizontalMovement
{
    private const float FlightSpeed = 9f;
    private const float FlightVerticalPadding = 0.22f;
    private Camera flightCamera;
    private ArisaFlightArms flightArms;
    private Vector2 flightColliderSize;
    private Vector2 flightColliderOffset;
    private Vector2 flightArtVerticalOffsets;
    private SpriteRenderer[] groundRenderers;
    private bool[] groundRendererOverrides;

    internal ArisaFlightArms FlightArms => flightArms;

    internal void BeginFlight(Camera camera, Vector3 position, SpriteRenderer flightArt, ArisaFlightArms arms)
    {
        TeleportTo(position, useFallbackGround: false);
        Bounds artBounds = flightArt.bounds;
        flightColliderOffset = transform.InverseTransformPoint(artBounds.center);
        Vector3 scale = transform.lossyScale;
        flightColliderSize = new Vector2(artBounds.size.x / Mathf.Abs(scale.x), artBounds.size.y / Mathf.Abs(scale.y));
        flightArtVerticalOffsets = new Vector2(artBounds.min.y - position.y, artBounds.max.y - position.y);
        flightCamera = camera;
        flightArms = arms;
        ApplyStanceCollider();
        groundRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        groundRendererOverrides = new bool[groundRenderers.Length];
        for (int i = 0; i < groundRenderers.Length; i++)
        {
            groundRendererOverrides[i] = groundRenderers[i].forceRenderingOff;
            groundRenderers[i].forceRenderingOff = true;
        }
    }

    internal void EndFlight(Vector3 returnPosition)
    {
        if (groundRenderers == null) return;
        flightCamera = null;
        flightArms = null;
        for (int i = 0; i < groundRenderers.Length; i++)
            if (groundRenderers[i] != null) groundRenderers[i].forceRenderingOff = groundRendererOverrides[i];
        groundRenderers = null;
        groundRendererOverrides = null;
        TeleportTo(returnPosition, useFallbackGround: false);
    }

    private void MoveInFlight()
    {
        Vector2 input = new Vector2(horizontalInput, (jumpHeld ? 1f : 0f) - (crouchHeld ? 1f : 0f));
        Vector2 next = movementBody.position + Vector2.ClampMagnitude(input, 1f) * (FlightSpeed * Time.fixedDeltaTime);
        Vector2 center = flightCamera.transform.position;
        float halfHeight = flightCamera.orthographicSize;
        float halfWidth = halfHeight * flightCamera.aspect;
        float sideMargin = Mathf.Min(1.45f, halfWidth * 0.3f);
        next.x = Mathf.Clamp(next.x, center.x - halfWidth + sideMargin, center.x + halfWidth * 0.44f);
        float bottom = center.y - halfHeight + FlightVerticalPadding - flightArtVerticalOffsets.x;
        float top = center.y + halfHeight - FlightVerticalPadding - flightArtVerticalOffsets.y;
        next.y = Mathf.Clamp(next.y, bottom, top);
        movementBody.linearVelocity = Vector2.zero;
        movementBody.MovePosition(next);
    }

    internal bool TryGetFlightCollider(out Vector2 size, out Vector2 offset)
    {
        size = flightColliderSize;
        offset = flightColliderOffset;
        return flightCamera != null;
    }

    internal bool TryGetFlightMuzzle(bool facingLeft, Vector3 aimTarget, out Vector3 position)
    {
        position = default;
        if (flightArms == null) return false;
        flightArms.AimAt(aimTarget, facingLeft);
        position = flightArms.MuzzlePosition;
        return true;
    }
}
