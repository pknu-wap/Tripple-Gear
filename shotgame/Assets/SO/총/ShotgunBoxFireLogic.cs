using System;
using UnityEngine;

[Serializable]
public sealed class ShotgunBoxFireLogic : GunFireLogic
{
    private const string DefaultHitboxLayerName = "Bullet";

    [SerializeField, KoreanLabel("판정 크기")] private Vector2 판정크기 = new Vector2(3.5f, 1.2f);
    [SerializeField, KoreanLabel("판정 중심 거리"), Min(0f)] private float 판정중심거리 = 1.8f;
    [SerializeField, KoreanLabel("판정 유지 시간"), Min(0f)] private float 판정유지시간 = 0.05f;
    [SerializeField, KoreanLabel("판정 위치 보정")] private Vector2 판정위치보정 = Vector2.zero;
    [SerializeField, KoreanLabel("한 발씩 장전")] private bool 한발씩장전 = true;
    [SerializeField, KoreanLabel("장전 중 발사 가능")] private bool 장전중발사가능 = true;
    [SerializeField, KoreanLabel("발사 시 장전 중단")] private bool 발사시장전중단 = true;
    [SerializeField, KoreanLabel("장전 사이 대기 시간"), Min(0f)] private float 장전사이대기시간 = 0.15f;

    public Vector2 HitboxSize => new Vector2(Mathf.Max(0f, 판정크기.x), Mathf.Max(0f, 판정크기.y));
    public float HitboxCenterDistance => 판정중심거리;
    public float HitboxLifetime => 판정유지시간;
    public Vector2 HitboxLocalOffset => 판정위치보정;
    public override bool ReloadsSequentially => 한발씩장전;
    public override bool CanFireWhileReloading => 장전중발사가능;
    public override bool InterruptsReloadWhenFired => 발사시장전중단;
    public override float SequentialReloadDelay => 장전사이대기시간;

    public override bool CanFire(GunSO gunData)
    {
        return gunData != null;
    }

    public override void Fire(GunSO gunData, GunFireContext context)
    {
        if (!CanFire(gunData) || context.Direction.sqrMagnitude < MinDirectionSqrMagnitude)
        {
            return;
        }

        Vector2 fireDirection = context.Direction.normalized;
        Quaternion rotation = GetRotation(fireDirection);
        Vector3 hitboxCenter = GetHitboxCenter(context.MuzzlePosition, fireDirection);

        GameObject hitboxObject = new GameObject(gunData.name + " Shotgun Hitbox");
        hitboxObject.transform.SetPositionAndRotation(
            hitboxCenter,
            rotation
        );
        ApplyHitboxLayer(hitboxObject);
        CreateHitboxVisual(gunData, hitboxObject.transform);

        BoxCollider2D hitbox = hitboxObject.AddComponent<BoxCollider2D>();
        hitbox.isTrigger = true;
        hitbox.size = HitboxSize;
        hitbox.includeLayers = gunData.HitTargetLayers;

        Rigidbody2D rigidbody2D = hitboxObject.AddComponent<Rigidbody2D>();
        rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
        rigidbody2D.gravityScale = 0f;

        UnityEngine.Object.Destroy(hitboxObject, HitboxLifetime);
    }

    private GameObject CreateHitboxVisual(GunSO gunData, Transform hitboxRoot)
    {
        if (gunData.BulletPrefab != null)
        {
            GameObject prefabVisualObject = UnityEngine.Object.Instantiate(gunData.BulletPrefab, hitboxRoot);
            prefabVisualObject.transform.localPosition = Vector3.zero;
            prefabVisualObject.transform.localRotation = Quaternion.identity;
            ApplyHitboxLayerRecursively(prefabVisualObject.transform);
            ApplyProjectileSpriteFallback(gunData, prefabVisualObject);
            DisableProjectileMovement(prefabVisualObject);
            return prefabVisualObject;
        }

        if (gunData.BulletSprite == null)
        {
            return null;
        }

        GameObject visualObject = new GameObject("Visual");
        visualObject.transform.SetParent(hitboxRoot, false);
        visualObject.transform.localPosition = gunData.ProjectileVisualLocalOffset;
        visualObject.transform.localScale = Vector3.one * gunData.ProjectileScale;
        ApplyHitboxLayer(visualObject);

        SpriteRenderer spriteRenderer = visualObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = gunData.BulletSprite;
        spriteRenderer.sortingOrder = gunData.ProjectileSortingOrder;

        return visualObject;
    }

    private Vector3 GetHitboxCenter(Vector3 muzzlePosition, Vector2 fireDirection)
    {
        Vector2 sideDirection = new Vector2(-fireDirection.y, fireDirection.x);
        Vector2 offset = fireDirection * (HitboxCenterDistance + HitboxLocalOffset.x)
            + sideDirection * HitboxLocalOffset.y;

        return muzzlePosition + (Vector3)offset;
    }

    private static void ApplyHitboxLayer(GameObject hitboxObject)
    {
        int hitboxLayer = LayerMask.NameToLayer(DefaultHitboxLayerName);
        if (hitboxLayer >= 0)
        {
            hitboxObject.layer = hitboxLayer;
        }
    }

    private static void ApplyHitboxLayerRecursively(Transform target)
    {
        if (target == null)
        {
            return;
        }

        ApplyHitboxLayer(target.gameObject);
        for (int i = 0; i < target.childCount; i++)
        {
            ApplyHitboxLayerRecursively(target.GetChild(i));
        }
    }

    private static void DisableProjectileMovement(GameObject hitboxObject)
    {
        Projectile2D[] projectiles = hitboxObject.GetComponentsInChildren<Projectile2D>(true);
        for (int i = 0; i < projectiles.Length; i++)
        {
            if (projectiles[i] != null)
            {
                projectiles[i].enabled = false;
            }
        }
    }
}
