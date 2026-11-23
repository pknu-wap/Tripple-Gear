using UnityEngine;

[DisallowMultipleComponent]
public sealed class MonsterHitFlash : MonoBehaviour
{
    private const float Duration = 0.1f;
    private static readonly Color HitOrange = new Color(2.0f, 1.5f, 0.8f);
    private static readonly Color WallHitOrange = new Color(1f, 0.72f, 0.44f);
    private const float WallFlashStrength = 0.25f;
    private static readonly int HitFlashColor = Shader.PropertyToID("_HitFlashColor");

    private struct SpriteTint
    {
        public SpriteRenderer Renderer;
        public Color Original;
        public Color Applied;
        public bool UsesShaderFlash;
        public bool IsApplied;
    }

    private SpriteTint[] tints;
    private MaterialPropertyBlock shaderProperties;
    private Color flashColor;
    private float endTime;

    internal static void PlayFinalSprite(SpriteRenderer source, bool useWallHitFlash)
    {
        if (!source.enabled || source.sprite == null)
        {
            return;
        }

        // 피격 판정이 끝난 몬스터 대신 마지막 스프라이트만 잠깐 표시한다.
        GameObject visual = new GameObject("Monster Final Hit");
        visual.layer = source.gameObject.layer;
        visual.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        visual.transform.localScale = source.transform.lossyScale;

        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = source.sprite;
        renderer.sharedMaterial = source.sharedMaterial;
        renderer.sortingLayerID = source.sortingLayerID;
        renderer.sortingOrder = source.sortingOrder;
        renderer.maskInteraction = source.maskInteraction;
        renderer.flipX = source.flipX;
        renderer.flipY = source.flipY;
        renderer.color = source.color;
        Play(visual, useWallHitFlash);
        Destroy(visual, Duration);
    }

    internal static void Play(GameObject target, bool useWallHitFlash)
    {
        if (!target.activeInHierarchy)
        {
            return;
        }

        if (!target.TryGetComponent(out MonsterHitFlash flash))
        {
            flash = target.AddComponent<MonsterHitFlash>();
        }

        if (flash.tints == null)
        {
            SpriteRenderer[] renderers = target.GetComponentsInChildren<SpriteRenderer>(true);
            flash.tints = new SpriteTint[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                flash.tints[i].Renderer = renderers[i];
                Material material = renderers[i].sharedMaterial;
                flash.tints[i].UsesShaderFlash = material != null && material.HasProperty(HitFlashColor);
            }
        }

        flash.flashColor = useWallHitFlash ? WallHitOrange : HitOrange;
        flash.flashColor.a = useWallHitFlash ? WallFlashStrength : 1f;
        flash.endTime = Time.time + Duration;
        flash.enabled = true;
    }

    private void LateUpdate()
    {
        if (Time.time >= endTime)
        {
            enabled = false;
            return;
        }

        for (int i = 0; i < tints.Length; i++)
        {
            ref SpriteTint tint = ref tints[i];
            if (tint.Renderer == null)
            {
                continue;
            }

            if (tint.UsesShaderFlash)
            {
                if (!tint.IsApplied)
                {
                    shaderProperties ??= new MaterialPropertyBlock();
                    tint.Renderer.GetPropertyBlock(shaderProperties);
                    tint.Original = shaderProperties.HasColor(HitFlashColor)
                        ? shaderProperties.GetColor(HitFlashColor)
                        : tint.Renderer.sharedMaterial.GetColor(HitFlashColor);
                    shaderProperties.SetColor(HitFlashColor, flashColor);
                    tint.Renderer.SetPropertyBlock(shaderProperties);
                    tint.IsApplied = true;
                }

                continue;
            }

            Color color = tint.Renderer.color;
            // 애니메이션이나 몬스터 동작이 갱신한 색을 복원할 수 있게 보관한다.
            if (!tint.IsApplied || !color.Equals(tint.Applied))
            {
                tint.Original = color;
            }

            Color blendedColor = Color.Lerp(tint.Original, flashColor, flashColor.a);
            blendedColor.a = tint.Original.a;
            tint.Renderer.color = blendedColor;
            tint.Applied = tint.Renderer.color;
            tint.IsApplied = true;
        }
    }

    private void OnDisable()
    {
        if (tints == null)
        {
            return;
        }

        for (int i = 0; i < tints.Length; i++)
        {
            ref SpriteTint tint = ref tints[i];
            if (!tint.IsApplied || tint.Renderer == null)
            {
                continue;
            }

            if (tint.UsesShaderFlash)
            {
                tint.Renderer.GetPropertyBlock(shaderProperties);
                shaderProperties.SetColor(HitFlashColor, tint.Original);
                tint.Renderer.SetPropertyBlock(shaderProperties);
            }
            else if (tint.Renderer.color.Equals(tint.Applied))
            {
                tint.Renderer.color = tint.Original;
            }
        }

        tints = null;
        enabled = false;
    }
}
