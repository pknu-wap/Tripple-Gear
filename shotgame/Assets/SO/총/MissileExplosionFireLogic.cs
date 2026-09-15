using System;
using System.Collections;
using UnityEngine;

[Serializable]
public sealed class MissileExplosionFireLogic : GunFireLogic
{
    [SerializeField, KoreanLabel("폭발 지연 시간"), Min(0f)] private float 폭발지연시간 = 1f;
    [SerializeField, KoreanLabel("폭발 반경"), Min(0f)] private float 폭발반경 = 2f;
    [SerializeField, KoreanLabel("폭발 대상 레이어")] private LayerMask 폭발대상레이어 = ~0;
    [SerializeField, KoreanLabel("한 발씩 장전")] private bool 한발씩장전 = true;
    [SerializeField, KoreanLabel("장전 사이 대기 시간"), Min(0f)] private float 장전사이대기시간 = 0.15f;

    public float ExplosionDelay => 폭발지연시간;
    public float ExplosionRadius => 폭발반경;
    public LayerMask ExplosionTargetLayers => 폭발대상레이어;
    public override bool ReloadsSequentially => 한발씩장전;
    public override bool CanFireWhileReloading => false;
    public override bool InterruptsReloadWhenFired => false;
    public override float SequentialReloadDelay => 장전사이대기시간;

    public override void Fire(GunSO gunData, GunFireContext context)
    {
        if (!CanFire(gunData) || context.Direction.sqrMagnitude < MinDirectionSqrMagnitude)
        {
            return;
        }

        Vector2 fireDirection = context.Direction.normalized;
        GameObject projectileObject = CreateProjectileObject(
            gunData,
            context.MuzzlePosition,
            GetRotation(fireDirection)
        );

        if (projectileObject == null)
        {
            return;
        }

        ApplyProjectileSpriteFallback(gunData, projectileObject);

        Projectile2D projectile = projectileObject.GetComponent<Projectile2D>();
        if (projectile == null)
        {
            projectile = projectileObject.AddComponent<Projectile2D>();
        }

        projectile.ConfigureHitSettings(gunData, hitPosition => Explode(gunData, hitPosition));
        projectile.Launch(
            fireDirection,
            gunData.BulletFireSpeed,
            context.Camera,
            gunData.DestroyViewportPadding,
            gunData.MaxProjectileLifetime
        );

        if (context.CoroutineRunner == null)
        {
            return;
        }

        context.CoroutineRunner.StartCoroutine(ExplodeAfterDelay(projectileObject, projectile));
    }

    private IEnumerator ExplodeAfterDelay(GameObject projectileObject, Projectile2D projectile)
    {
        float elapsed = 0f;
        float explosionDelay = Mathf.Max(0f, ExplosionDelay);

        while (projectileObject != null && projectile != null && !projectile.HasHit && elapsed < explosionDelay)
        {
            float deltaTime = Time.deltaTime;
            elapsed += deltaTime;
            yield return null;
        }

        if (projectileObject == null || projectile == null || projectile.HasHit)
        {
            yield break;
        }

        projectile.ResolveHit(projectileObject.transform.position, true);
    }

    private void Explode(GunSO gunData, Vector3 explosionPosition)
    {
        ApplyExplosionDamage(gunData, explosionPosition);
    }

    private void ApplyExplosionDamage(GunSO gunData, Vector3 explosionPosition)
    {
        if (ExplosionRadius <= 0f || ExplosionTargetLayers.value == 0)
        {
            return;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(
            explosionPosition,
            ExplosionRadius,
            ExplosionTargetLayers
        );

        for (int i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null)
            {
                continue;
            }

            GameObject hitObject = hits[i].attachedRigidbody != null
                ? hits[i].attachedRigidbody.gameObject
                : hits[i].gameObject;
            hitObject.SendMessage("TakeDamage", gunData.Damage, SendMessageOptions.DontRequireReceiver);
        }
    }
}
