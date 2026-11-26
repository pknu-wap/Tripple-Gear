using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class HitscanLineFireLogic : GunFireLogic
{
    [SerializeField, KoreanLabel("사거리"), Min(0f)] private float 사거리 = 30f;
    [SerializeField, KoreanLabel("선 유지 시간"), Min(0f)] private float 표시시간 = 0.15f;
    [SerializeField, KoreanLabel("페이드 사용")] private bool 페이드사용 = true;
    [SerializeField, KoreanLabel("페이드 인 시간"), Min(0f)] private float 페이드인시간 = 0.02f;
    [SerializeField, KoreanLabel("페이드 아웃 시간"), Min(0f)] private float 페이드아웃시간 = 0.08f;

    private readonly Plane[] cameraPlanes = new Plane[6];

    public float Range => 사거리;
    public float VisibleDuration => 표시시간;
    public bool UseFade => 페이드사용;
    public float FadeInDuration => 페이드인시간;
    public float FadeOutDuration => 페이드아웃시간;

    public override void Fire(GunSO gunData, GunFireContext context)
    {
        if (!CanFire(gunData) || context.Direction.sqrMagnitude < MinDirectionSqrMagnitude)
        {
            return;
        }

        Vector2 fireDirection = context.Direction.normalized;
        Vector3 shotOrigin = context.MuzzlePosition;
        float shotDistance = Range;
        if (context.Camera != null && !ClipShotToCamera(context.Camera, fireDirection, ref shotOrigin, ref shotDistance))
        {
            return;
        }

        HashSet<MonsterHealth> damaged = new HashSet<MonsterHealth>();
        foreach (RaycastHit2D hit in Physics2D.RaycastAll(shotOrigin, fireDirection, shotDistance, gunData.HitTargetLayers))
        {
            MonsterHealth target = hit.collider.GetComponentInParent<MonsterHealth>();
            if (target == null)
            {
                shotDistance = hit.distance;
                break;
            }

            if (damaged.Add(target))
            {
                target.TakeGunDamage(context.GetDamage(gunData));
                if (!context.DamageOverride.HasValue)
                {
                    // The normal shot also applies base damage to Enemy-layer targets.
                    GunDamage.ApplyToEnemy(hit.collider, gunData.Damage);
                }
            }
        }

        CreateInstantShotVisual(gunData, shotOrigin, fireDirection, shotDistance, context.CoroutineRunner);
    }

    private bool ClipShotToCamera(Camera camera, Vector2 direction, ref Vector3 origin, ref float distance)
    {
        GeometryUtility.CalculateFrustumPlanes(camera, cameraPlanes);
        float entry = 0f;
        float exit = distance;
        foreach (Plane plane in cameraPlanes)
        {
            float originSide = plane.GetDistanceToPoint(origin);
            float travel = Vector3.Dot(plane.normal, direction);
            if (travel > 0f)
            {
                entry = Mathf.Max(entry, -originSide / travel);
            }
            else if (travel < 0f)
            {
                exit = Mathf.Min(exit, -originSide / travel);
            }
            else if (originSide < 0f)
            {
                return false;
            }

            if (entry >= exit) return false;
        }

        origin += (Vector3)direction * entry;
        distance = exit - entry;
        return true;
    }

    private void CreateInstantShotVisual(
        GunSO gunData,
        Vector3 muzzlePosition,
        Vector2 direction,
        float shotDistance,
        MonoBehaviour coroutineRunner
    )
    {
        GameObject shotObject = CreateProjectileObject(gunData, muzzlePosition, GetRotation(direction));
        if (shotObject == null)
        {
            return;
        }

        DisableProjectileMovement(shotObject);
        StretchShotVisual(gunData, shotObject, shotDistance);
        PlayLifetime(coroutineRunner, shotObject);
    }

    private void PlayLifetime(MonoBehaviour coroutineRunner, GameObject shotObject)
    {
        if (!UseFade || coroutineRunner == null)
        {
            UnityEngine.Object.Destroy(shotObject, VisibleDuration);
            return;
        }

        coroutineRunner.StartCoroutine(FadeAndDestroy(shotObject));
    }

    private IEnumerator FadeAndDestroy(GameObject shotObject)
    {
        float lifetime = Mathf.Max(0f, VisibleDuration);
        if (lifetime <= 0f)
        {
            UnityEngine.Object.Destroy(shotObject);
            yield break;
        }

        SpriteRenderer[] spriteRenderers = shotObject.GetComponentsInChildren<SpriteRenderer>(true);
        Color[] baseColors = CaptureBaseColors(spriteRenderers);
        float startedAtTime = Time.time;

        while (shotObject != null)
        {
            float elapsed = Time.time - startedAtTime;
            if (elapsed >= lifetime)
            {
                break;
            }

            ApplyAlpha(spriteRenderers, baseColors, GetAlphaMultiplier(elapsed, lifetime));
            yield return null;
        }

        UnityEngine.Object.Destroy(shotObject);
    }

    private void StretchShotVisual(GunSO gunData, GameObject shotObject, float shotDistance)
    {
        float distanceScale = Mathf.Max(0f, shotDistance);
        if (distanceScale <= 0f)
        {
            return;
        }

        SpriteRenderer[] spriteRenderers = shotObject.GetComponentsInChildren<SpriteRenderer>(true);
        if (spriteRenderers.Length == 0)
        {
            Vector3 rootScale = shotObject.transform.localScale;
            rootScale.x = distanceScale;
            shotObject.transform.localScale = rootScale;
            return;
        }

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] == null)
            {
                continue;
            }

            Transform visualTransform = spriteRenderers[i].transform;
            Vector3 localScale = visualTransform.localScale;
            localScale.x = GetLengthScale(spriteRenderers[i], distanceScale);
            localScale.y = gunData.ProjectileScale;
            visualTransform.localScale = localScale;
            MoveVisualStartToShotOrigin(shotObject.transform, spriteRenderers[i], distanceScale);
        }
    }

    private static float GetLengthScale(SpriteRenderer spriteRenderer, float shotDistance)
    {
        Sprite sprite = spriteRenderer.sprite;
        if (sprite == null || sprite.bounds.size.x <= 0f)
        {
            return shotDistance;
        }

        return shotDistance / sprite.bounds.size.x;
    }

    private static void MoveVisualStartToShotOrigin(
        Transform shotTransform,
        SpriteRenderer spriteRenderer,
        float shotDistance
    )
    {
        float pivotOffset = GetPivotOffsetFromLeftEdge(spriteRenderer.sprite, shotDistance);
        Transform visualTransform = spriteRenderer.transform;

        if (visualTransform == shotTransform)
        {
            visualTransform.position += visualTransform.right * pivotOffset;
            return;
        }

        Vector3 localPosition = visualTransform.localPosition;
        localPosition.x += pivotOffset;
        visualTransform.localPosition = localPosition;
    }

    private static float GetPivotOffsetFromLeftEdge(Sprite sprite, float shotDistance)
    {
        if (sprite == null || sprite.rect.width <= 0f)
        {
            return shotDistance * 0.5f;
        }

        float normalizedPivotX = Mathf.Clamp01(sprite.pivot.x / sprite.rect.width);
        return shotDistance * normalizedPivotX;
    }

    private static Color[] CaptureBaseColors(SpriteRenderer[] spriteRenderers)
    {
        Color[] baseColors = new Color[spriteRenderers.Length];
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            baseColors[i] = spriteRenderers[i] != null ? spriteRenderers[i].color : Color.white;
        }

        return baseColors;
    }

    private float GetAlphaMultiplier(float elapsed, float lifetime)
    {
        float alpha = 1f;

        if (FadeInDuration > 0f && elapsed < FadeInDuration)
        {
            alpha = Mathf.Min(alpha, elapsed / FadeInDuration);
        }

        if (FadeOutDuration > 0f)
        {
            float fadeOutStartedAt = Mathf.Max(0f, lifetime - FadeOutDuration);
            if (elapsed >= fadeOutStartedAt)
            {
                float remaining = Mathf.Max(0f, lifetime - elapsed);
                alpha = Mathf.Min(alpha, remaining / FadeOutDuration);
            }
        }

        return Mathf.Clamp01(alpha);
    }

    private static void ApplyAlpha(
        SpriteRenderer[] spriteRenderers,
        Color[] baseColors,
        float alphaMultiplier
    )
    {
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] == null)
            {
                continue;
            }

            Color color = baseColors[i];
            color.a *= alphaMultiplier;
            spriteRenderers[i].color = color;
        }
    }

    private static void DisableProjectileMovement(GameObject shotObject)
    {
        Projectile2D[] projectiles = shotObject.GetComponentsInChildren<Projectile2D>(true);
        for (int i = 0; i < projectiles.Length; i++)
        {
            if (projectiles[i] != null)
            {
                projectiles[i].enabled = false;
            }
        }
    }
}
