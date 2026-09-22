using System;
using System.Collections;
using UnityEngine;

[Serializable]
public sealed class InvincibleSkillLogic : SkillLogic
{
    private const float UiPrefabWorldScale = 0.025f;
    private const int EffectSortingOrderOffset = 20;

    [SerializeField, KoreanLabel("지속 시간"), Min(0.01f)] private float 지속시간 = 3f;
    [SerializeField, KoreanLabel("효과 프리팹")] private GameObject 효과프리팹;
    [SerializeField, KoreanLabel("프리팹 크기"), Min(0.01f)] private float 프리팹크기 = 1f;

    public override IEnumerator Execute(SkillContext context)
    {
        if (context.Owner == null)
        {
            yield break;
        }

        GameObject effectInstance = CreateEffect(context.Owner);
        bool restoreInvincible = context.Invincibility != null;

        if (restoreInvincible)
        {
            context.Invincibility.SetSkillInvincible(true);
        }

        try
        {
            float endTime = Time.time + Mathf.Max(0.01f, 지속시간);
            while (context.Owner != null && Time.time < endTime)
            {
                yield return null;
            }
        }
        finally
        {
            if (effectInstance != null)
            {
                UnityEngine.Object.Destroy(effectInstance);
            }

            if (restoreInvincible && context.Invincibility != null)
            {
                context.Invincibility.SetSkillInvincible(false);
            }
        }
    }

    public override void OnValidate()
    {
        지속시간 = Mathf.Max(0.01f, 지속시간);
        프리팹크기 = Mathf.Max(0.01f, 프리팹크기);
    }

    private GameObject CreateEffect(Transform owner)
    {
        if (효과프리팹 == null || owner == null)
        {
            return null;
        }

        Vector3 prefabScale = 효과프리팹.transform.localScale;
        Vector3 center = GetOwnerCenter(owner);
        GameObject instance = UnityEngine.Object.Instantiate(효과프리팹, center, Quaternion.identity, owner);
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = IsUiPrefab(instance)
            ? Vector3.one * UiPrefabWorldScale * 프리팹크기
            : prefabScale * 프리팹크기;
        ConfigureWorldCanvas(instance, owner);
        return instance;
    }

    private static bool IsUiPrefab(GameObject instance)
    {
        return instance != null
            && instance.GetComponentInChildren<RectTransform>(true) != null
            && instance.GetComponentInChildren<SpriteRenderer>(true) == null;
    }

    private static void ConfigureWorldCanvas(GameObject instance, Transform owner)
    {
        if (!IsUiPrefab(instance))
        {
            BringSpriteRenderersForward(instance, owner);
            return;
        }

        Canvas canvas = instance.GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = instance.AddComponent<Canvas>();
        }

        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = GetHighestSortingOrder(owner) + EffectSortingOrderOffset;
    }

    private static void BringSpriteRenderersForward(GameObject instance, Transform owner)
    {
        if (instance == null)
        {
            return;
        }

        int sortingOrder = GetHighestSortingOrder(owner) + EffectSortingOrderOffset;
        SpriteRenderer[] spriteRenderers = instance.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
            {
                spriteRenderers[i].sortingOrder = Mathf.Max(spriteRenderers[i].sortingOrder, sortingOrder);
            }
        }
    }

    private static int GetHighestSortingOrder(Transform owner)
    {
        int sortingOrder = 0;
        if (owner == null)
        {
            return sortingOrder;
        }

        SpriteRenderer[] spriteRenderers = owner.GetComponentsInChildren<SpriteRenderer>(false);
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
            {
                sortingOrder = Mathf.Max(sortingOrder, spriteRenderers[i].sortingOrder);
            }
        }

        return sortingOrder;
    }

    private static Vector3 GetOwnerCenter(Transform owner)
    {
        Bounds bounds = default;
        bool hasBounds = false;

        SpriteRenderer[] spriteRenderers = owner.GetComponentsInChildren<SpriteRenderer>(false);
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];
            if (spriteRenderer == null || !spriteRenderer.enabled || spriteRenderer.sprite == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = spriteRenderer.bounds;
                hasBounds = true;
                continue;
            }

            bounds.Encapsulate(spriteRenderer.bounds);
        }

        if (hasBounds)
        {
            return bounds.center;
        }

        Collider2D collider = owner.GetComponentInChildren<Collider2D>();
        return collider != null ? collider.bounds.center : owner.position;
    }
}
