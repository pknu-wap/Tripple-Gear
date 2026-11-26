using System;
using System.Collections;
using UnityEngine;

[Serializable]
public sealed class TeleportSkillLogic : SkillLogic
{
    [SerializeField, Min(0.5f)] private float distance = 5f;

    private readonly RaycastHit2D[] hits = new RaycastHit2D[16];

    public override bool RequiresAimDirection => true;

    public override bool CanExecute(SkillContext context) => TravelDistance(context) > 0.05f;

    public override IEnumerator Execute(SkillContext context)
    {
        float travel = TravelDistance(context);
        if (travel > 0.05f)
        {
            Vector3 destination = context.Owner.position + (Vector3)(context.AimDirection.normalized * travel);
            context.Movement.TeleportTo(destination, useFallbackGround: false);
        }
        yield break;
    }

    private float TravelDistance(SkillContext context)
    {
        if (context.Movement == null || context.Owner == null || context.AimDirection.sqrMagnitude < 0.001f
            || !context.Owner.TryGetComponent(out Collider2D body))
        {
            return 0f;
        }

        Vector2 direction = context.AimDirection.normalized;
        ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(LayerMask.GetMask("Box", "Stair", "WaveBoundary"));
        int count = body.Cast(direction, filter, hits, distance);
        float travel = distance;
        for (int i = 0; i < count; i++)
        {
            if (Vector2.Dot(direction, hits[i].normal) >= -0.01f) continue;
            travel = Mathf.Min(travel, Mathf.Max(0f, hits[i].distance - 0.045f));
        }
        return travel;
    }
}
