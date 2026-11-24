using System.Collections;
using System.Collections.Generic;
using UnityEngine;

internal sealed class SkillGrenade : MonoBehaviour
{
    private const float CollisionRadius = 0.12f;
    private const float ShockwaveDuration = 0.28f;
    private const int RingPoints = 32;

    private static readonly Color BlastColor = new Color(1f, 0.36f, 0.1f);
    private static readonly Color ElectricColor = new Color(0.25f, 0.85f, 1f);

    internal void Launch(Transform owner, Sprite sprite, GameObject explosionEffect, bool electric, Vector2 velocity,
        float gravity, float maxFlightDistance, float fuseSeconds, LayerMask impactLayers, float radius, int damage, float stunSeconds,
        Sprite burstSprite, float projectileSize, float burstScale)
    {
        SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 45;
        renderer.color = electric ? ElectricColor : Color.white;
        transform.localScale = Vector3.one * (projectileSize / Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y));

        Vector2 acceleration = Vector2.down * gravity;
        StartCoroutine(Fly(owner, renderer, explosionEffect, electric, velocity, acceleration,
            maxFlightDistance, fuseSeconds, impactLayers, radius, damage, stunSeconds, burstSprite, burstScale));
    }

    private IEnumerator Fly(Transform owner, SpriteRenderer renderer, GameObject explosionEffect, bool electric,
        Vector2 velocity, Vector2 acceleration, float maxFlightDistance, float fuseSeconds, LayerMask impactLayers, float radius, int damage, float stunSeconds,
        Sprite burstSprite, float burstScale)
    {
        WaitForFixedUpdate waitForPhysics = new WaitForFixedUpdate();
        ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
        filter.SetLayerMask(impactLayers);
        List<RaycastHit2D> hits = new List<RaycastHit2D>();
        float elapsed = 0f;
        Vector2 launchPosition = transform.position;
        float maxDistanceSquared = maxFlightDistance * maxFlightDistance;

        while (elapsed < fuseSeconds)
        {
            yield return waitForPhysics;
            float step = Mathf.Min(Time.fixedDeltaTime, fuseSeconds - elapsed);
            Vector2 displacement = velocity * step + acceleration * (0.5f * step * step);
            float distance = displacement.magnitude;
            Vector2 direction = displacement.normalized;
            Vector2 origin = transform.position;
            Vector2 fromLaunch = origin - launchPosition;
            bool reachedRange = (fromLaunch + displacement).sqrMagnitude >= maxDistanceSquared;
            if (reachedRange)
            {
                // Clip this physics step to the boundary before testing for an impact.
                float alongRay = Vector2.Dot(fromLaunch, direction);
                float discriminant = alongRay * alongRay + maxDistanceSquared - fromLaunch.sqrMagnitude;
                distance = Mathf.Clamp(Mathf.Sqrt(Mathf.Max(0f, discriminant)) - alongRay, 0f, distance);
            }

            Physics2D.CircleCast(origin, CollisionRadius, direction, filter, hits, distance);
            float impactDistance = float.PositiveInfinity;
            foreach (RaycastHit2D hit in hits)
            {
                Collider2D collider = hit.collider;
                if (collider == null || collider.GetComponentInParent<ArisaHealth>() != null
                    || collider.GetComponentInParent<SkillPickup>() != null
                    || (owner != null && collider.transform.IsChildOf(owner)))
                {
                    continue;
                }

                bool solid = !collider.isTrigger && !(displacement.y > 0f && collider.usedByEffector);
                if (solid || GunDamage.GetEnemyHealth(collider) != null)
                {
                    impactDistance = Mathf.Min(impactDistance, hit.distance);
                }
            }

            if (impactDistance <= distance)
            {
                Move(direction * impactDistance);
                break;
            }

            Move(direction * distance);
            if (reachedRange)
            {
                break;
            }

            transform.Rotate(0f, 0f, step * -540f);
            velocity += acceleration * step;
            elapsed += step;
        }

        renderer.enabled = false;
        if (burstSprite == null && explosionEffect != null)
        {
            GameObject effect = Instantiate(explosionEffect, transform.position, Quaternion.identity);
            effect.transform.localScale *= burstScale;
            Destroy(effect, 1.5f);
        }

        ApplyBurst(electric, radius, damage, stunSeconds);
        yield return DrawBurst(burstSprite, electric, radius, burstScale);
        Destroy(gameObject);
    }

    private void Move(Vector2 displacement)
    {
        transform.position += (Vector3)displacement;
    }

    private void ApplyBurst(bool electric, float radius, int damage, float stunSeconds)
    {
        HashSet<MonsterHealth> affected = new HashSet<MonsterHealth>();
        foreach (Collider2D collider in Physics2D.OverlapCircleAll(transform.position, radius, LayerMask.GetMask("Enemy")))
        {
            MonsterHealth target = GunDamage.GetEnemyHealth(collider);
            if (target == null || target.IsDead || !affected.Add(target))
            {
                continue;
            }

            if (electric)
            {
                RobotStun.Apply(target, stunSeconds);
            }
            else
            {
                target.TakeDamage(damage);
            }
        }
    }

    private IEnumerator DrawBurst(Sprite sprite, bool electric, float radius, float burstScale)
    {
        float duration = sprite == null ? ShockwaveDuration : electric ? 0.42f : 0.52f;
        SpriteRenderer burstRenderer = null;
        float blastSize = 0f;
        if (sprite != null)
        {
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            GameObject burst = new GameObject("폭발 스프라이트");
            burst.transform.SetParent(transform, false);
            burstRenderer = burst.AddComponent<SpriteRenderer>();
            burstRenderer.sprite = sprite;
            burstRenderer.sortingOrder = 47;
            // Keep the burst texture per renderer when different bursts render in the same frame.
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            properties.SetTexture("_MainTex", sprite.texture);
            burstRenderer.SetPropertyBlock(properties);
            float maxSize = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
            blastSize = radius * (electric ? 1.65f : 1.2f) * burstScale / maxSize;
        }

        Color color = electric ? ElectricColor : BlastColor;
        LineRenderer ring = gameObject.AddComponent<LineRenderer>();
        ring.sharedMaterial = SkillEffectVisual.Material;
        ring.sortingOrder = 46;
        ring.loop = true;
        ring.useWorldSpace = true;
        ring.positionCount = RingPoints;

        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            if (burstRenderer != null)
            {
                float burstProgress = elapsed / duration;
                float expansion = 1f - Mathf.Pow(1f - burstProgress, 3f);
                burstRenderer.transform.localScale = Vector3.one * Mathf.Lerp(blastSize * 0.18f, blastSize, expansion);
                float opacity = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.24f, 1f, burstProgress));
                burstRenderer.color = new Color(1f, 1f, 1f, opacity);
            }

            float progress = Mathf.Clamp01(elapsed / ShockwaveDuration);
            ring.enabled = progress < 1f;
            float ringRadius = Mathf.Lerp(radius * 0.2f, radius, progress);
            ring.widthMultiplier = Mathf.Lerp(0.22f, 0.03f, progress);
            color.a = 1f - progress;
            ring.startColor = ring.endColor = color;
            for (int i = 0; i < RingPoints; i++)
            {
                float angle = i * (2f * Mathf.PI / RingPoints);
                ring.SetPosition(i, transform.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * ringRadius);
            }
            yield return null;
        }
    }
}
