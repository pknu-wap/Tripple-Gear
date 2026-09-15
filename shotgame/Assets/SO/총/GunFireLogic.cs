using System;
using UnityEngine;

[Serializable]
public abstract class GunFireLogic
{
    protected const float MinDirectionSqrMagnitude = 0.0001f;
    private const string DefaultProjectileLayerName = "Bullet";

    public virtual bool CanFire(GunSO gunData)
    {
        return gunData != null && (gunData.BulletPrefab != null || gunData.BulletSprite != null);
    }

    public virtual bool ReloadsSequentially => false;
    public virtual bool CanFireWhileReloading => false;
    public virtual bool InterruptsReloadWhenFired => false;
    public virtual float SequentialReloadDelay => 0f;

    public abstract void Fire(GunSO gunData, GunFireContext context);

    protected GameObject CreateProjectileObject(GunSO gunData, Vector3 spawnPosition, Quaternion rotation)
    {
        if (gunData == null)
        {
            return null;
        }

        GameObject bulletPrefab = gunData.BulletPrefab;
        if (bulletPrefab != null)
        {
            GameObject prefabProjectileObject = UnityEngine.Object.Instantiate(bulletPrefab, spawnPosition, rotation);
            ApplyDefaultProjectileLayer(prefabProjectileObject);
            return prefabProjectileObject;
        }

        Sprite bulletSprite = gunData.BulletSprite;
        if (bulletSprite == null)
        {
            return null;
        }

        GameObject projectileObject = new GameObject(gunData.name + " Shot");
        projectileObject.transform.SetPositionAndRotation(spawnPosition, rotation);
        ApplyDefaultProjectileLayer(projectileObject);

        GameObject visualObject = new GameObject("Visual");
        visualObject.transform.SetParent(projectileObject.transform, false);
        visualObject.transform.localPosition = gunData.ProjectileVisualLocalOffset;
        visualObject.transform.localScale = Vector3.one * gunData.ProjectileScale;
        ApplyDefaultProjectileLayer(visualObject);

        SpriteRenderer spriteRenderer = visualObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = bulletSprite;
        spriteRenderer.sortingOrder = gunData.ProjectileSortingOrder;

        return projectileObject;
    }

    protected void ApplyProjectileSpriteFallback(GunSO gunData, GameObject projectileObject)
    {
        if (gunData == null || projectileObject == null || gunData.BulletSprite == null)
        {
            return;
        }

        SpriteRenderer[] spriteRenderers = projectileObject.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null && spriteRenderers[i].sprite == null)
            {
                spriteRenderers[i].sprite = gunData.BulletSprite;
            }
        }
    }

    protected static Quaternion GetRotation(Vector2 direction)
    {
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        return Quaternion.Euler(0f, 0f, angle);
    }

    private static void ApplyDefaultProjectileLayer(GameObject projectileObject)
    {
        if (projectileObject == null)
        {
            return;
        }

        int bulletLayer = LayerMask.NameToLayer(DefaultProjectileLayerName);
        if (bulletLayer < 0)
        {
            return;
        }

        ApplyDefaultProjectileLayer(projectileObject.transform, bulletLayer);
    }

    private static void ApplyDefaultProjectileLayer(Transform target, int bulletLayer)
    {
        if (target == null)
        {
            return;
        }

        if (ShouldUseDefaultProjectileLayer(target.gameObject))
        {
            target.gameObject.layer = bulletLayer;
        }

        for (int i = 0; i < target.childCount; i++)
        {
            ApplyDefaultProjectileLayer(target.GetChild(i), bulletLayer);
        }
    }

    private static bool ShouldUseDefaultProjectileLayer(GameObject projectileObject)
    {
        return projectileObject.layer == 0 || string.IsNullOrEmpty(LayerMask.LayerToName(projectileObject.layer));
    }
}
