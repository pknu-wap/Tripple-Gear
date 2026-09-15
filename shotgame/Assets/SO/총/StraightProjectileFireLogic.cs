using System;
using UnityEngine;

[Serializable]
public sealed class StraightProjectileFireLogic : GunFireLogic
{
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

        projectile.ConfigureHitSettings(gunData);
        projectile.Launch(
            fireDirection,
            gunData.BulletFireSpeed,
            context.Camera,
            gunData.DestroyViewportPadding,
            gunData.MaxProjectileLifetime
        );
    }
}
