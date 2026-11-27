using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

internal sealed class MonsterDeathBurst : MonoBehaviour
{
    private const float BurstDuration = 0.7f;
    private const float Lifetime = 1.65f;
    private const int Columns = 4;
    private const int Rows = 3;
    private const int MaxFragmentSources = 3;
    private static readonly Color FragmentFlash = new Color(1f, 0.78f, 0.46f);
    private static GameObject defaultBurst;

    private struct Fragment
    {
        public SpriteRenderer Renderer;
        public Sprite Sprite;
        public Vector3 Start;
        public Vector2 Velocity;
        public Vector3 Scale;
        public Quaternion Rotation;
        public Color Color;
        public float Spin;
        public float Gravity;
        public float Duration;
    }

    private readonly List<Fragment> fragments = new List<Fragment>();
    private SpriteRenderer explosion;
    private Vector3 explosionScale;
    private Color explosionColor;
    private float age;

    internal static void Play(GameObject source, GameObject prefab = null, bool fromGun = false,
        bool useWallHitFlash = false)
    {
        if (defaultBurst == null)
        {
            defaultBurst = Resources.Load<GameObject>("ExplosiveMonsterBurst");
        }

        SpriteRenderer[] renderers = source.GetComponentsInChildren<SpriteRenderer>();
        Array.Sort(renderers, (left, right) => VisibleArea(right).CompareTo(VisibleArea(left)));
        SpriteRenderer primary = renderers.Length > 0 && VisibleArea(renderers[0]) > 0f ? renderers[0] : null;
        Vector3 center = primary != null ? primary.bounds.center : source.transform.position;
        float size = primary != null ? Mathf.Max(primary.bounds.size.x, primary.bounds.size.y) : 1f;

        var effect = new GameObject("Monster Death Burst");
        GameplaySfx.Play(GameplaySfx.Cue.Explosion, .42f);
        SceneManager.MoveGameObjectToScene(effect, source.scene);
        effect.transform.position = center;
        var animation = effect.AddComponent<MonsterDeathBurst>();
        animation.CreateExplosion(prefab != null ? prefab : defaultBurst, primary, source.layer, size);
        if (prefab != null && prefab != defaultBurst && fromGun && animation.explosion != null)
        {
            MonsterHitFlash.Play(animation.explosion.gameObject, useWallHitFlash);
        }

        if (primary == null) return;

        float minimumArea = VisibleArea(primary) * 0.15f;
        for (int i = 0; i < Mathf.Min(renderers.Length, MaxFragmentSources); i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (VisibleArea(renderer) < minimumArea) break;
            animation.SliceSprite(renderer, center, size);
        }
    }

    private static float VisibleArea(SpriteRenderer renderer)
    {
        if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.sprite == null
            || renderer.color.a <= 0.05f)
        {
            return 0f;
        }

        Vector3 size = renderer.bounds.size;
        return size.x * size.y;
    }

    private void CreateExplosion(GameObject prefab, SpriteRenderer primary, int layer, float size)
    {
        if (prefab == null) return;

        GameObject burst = Instantiate(prefab, transform.position, Quaternion.identity, transform);
        Destroy(burst, BurstDuration);
        explosion = burst.GetComponentInChildren<SpriteRenderer>();
        if (explosion == null) return;

        explosion.gameObject.layer = primary != null ? primary.gameObject.layer : layer;
        if (primary != null)
        {
            explosion.sortingLayerID = primary.sortingLayerID;
            explosion.sortingOrder = primary.sortingOrder + 5;
        }

        Sprite frame = explosion.sprite;
        Vector2 frameSize = frame != null
            ? Vector2.Scale(frame.rect.size / frame.pixelsPerUnit, explosion.transform.lossyScale)
            : (Vector2)explosion.bounds.size;
        float frameWidth = Mathf.Max(Mathf.Abs(frameSize.x), Mathf.Abs(frameSize.y), 0.01f);
        explosionScale = explosion.transform.localScale * (Mathf.Clamp(size * 1.6f, 1.75f, 11f) / frameWidth);
        explosionColor = explosion.color;
        explosion.transform.localScale = explosionScale * 0.58f;
    }

    private void SliceSprite(SpriteRenderer source, Vector3 center, float size)
    {
        Sprite sprite = source.sprite;
        // The monster textures are individual sprites or slices of an unpacked sheet.
        if (sprite.packed) return;

        Rect rect = sprite.rect;
        float bottom = rect.yMin;
        for (int row = 0; row < Rows; row++)
        {
            float top = row == Rows - 1
                ? rect.yMax
                : rect.yMin + (row + 1 + Random.Range(-0.2f, 0.2f)) * rect.height / Rows;
            float left = rect.xMin;
            for (int column = 0; column < Columns; column++)
            {
                float right = column == Columns - 1
                    ? rect.xMax
                    : rect.xMin + (column + 1 + Random.Range(-0.35f, 0.35f)) * rect.width / Columns;
                if ((column + row) % 3 == 0)
                {
                    float split = Mathf.Lerp(left, right, Random.Range(0.32f, 0.56f));
                    CreateFragment(source, center, size, Rect.MinMaxRect(left, bottom, split, top));
                    left = split;
                }
                CreateFragment(source, center, size, Rect.MinMaxRect(left, bottom, right, top));
                left = right;
            }
            bottom = top;
        }
    }

    private void CreateFragment(SpriteRenderer source, Vector3 center, float size, Rect region)
    {
        Sprite original = source.sprite;
        Sprite piece = Sprite.Create(original.texture, region, Vector2.one * 0.5f, original.pixelsPerUnit,
            0, SpriteMeshType.FullRect, Vector4.zero, false);

        Vector2 offset = (region.center - original.rect.position - original.pivot) / original.pixelsPerUnit;
        if (source.flipX) offset.x = -offset.x;
        if (source.flipY) offset.y = -offset.y;
        Vector3 position = source.transform.TransformPoint(offset);

        var fragment = new GameObject("Monster Fragment").AddComponent<SpriteRenderer>();
        fragment.gameObject.layer = source.gameObject.layer;
        fragment.transform.SetParent(transform, false);
        fragment.transform.SetPositionAndRotation(position, source.transform.rotation);
        fragment.transform.localScale = source.transform.lossyScale;
        fragment.sprite = piece;
        fragment.flipX = source.flipX;
        fragment.flipY = source.flipY;
        fragment.color = source.color;
        fragment.sortingLayerID = source.sortingLayerID;
        fragment.sortingOrder = source.sortingOrder + 3;
        fragment.maskInteraction = source.maskInteraction;

        Vector2 direction = ((Vector2)(position - center)).normalized + Random.insideUnitCircle * 0.45f;
        float speed = Mathf.Clamp(size * 2.1f, 2.7f, 13f);
        fragments.Add(new Fragment
        {
            Renderer = fragment,
            Sprite = piece,
            Start = position,
            Velocity = direction * (speed * Random.Range(0.65f, 1.35f)) + Vector2.up * (speed * 0.65f),
            Scale = fragment.transform.localScale,
            Rotation = fragment.transform.rotation,
            Color = source.color,
            Spin = Random.Range(200f, 550f) * (Random.value < 0.5f ? -1f : 1f),
            Gravity = Mathf.Clamp(speed * 2.25f, 8f, 24f),
            Duration = Random.Range(0.95f, Lifetime),
        });
    }

    private void LateUpdate()
    {
        age += Time.deltaTime;
        if (age >= Lifetime)
        {
            Destroy(gameObject);
            return;
        }

        if (explosion != null)
        {
            float expansion = Mathf.SmoothStep(0.58f, 1.15f, age / 0.33f);
            explosion.transform.localScale = explosionScale * expansion;
            Color color = explosionColor;
            color.a *= 1f - Mathf.InverseLerp(0.37f, BurstDuration, age);
            explosion.color = color;
        }

        float travelTime = 0.75f * (1f - Mathf.Exp(-age / 0.75f));
        foreach (Fragment fragment in fragments)
        {
            Vector2 distance = fragment.Velocity * travelTime;
            distance.y -= 0.5f * fragment.Gravity * age * age;
            Transform piece = fragment.Renderer.transform;
            piece.SetPositionAndRotation(fragment.Start + (Vector3)distance,
                fragment.Rotation * Quaternion.Euler(0f, 0f, fragment.Spin * age));

            float fade = Mathf.InverseLerp(fragment.Duration * 0.5f, fragment.Duration, age);
            piece.localScale = fragment.Scale * Mathf.Lerp(1f, 0.72f, fade);
            Color color = Color.Lerp(FragmentFlash, fragment.Color, Mathf.Clamp01(age / 0.15f));
            color.a = fragment.Color.a * (1f - fade);
            fragment.Renderer.color = color;
        }
    }

    private void OnDestroy()
    {
        foreach (Fragment fragment in fragments)
        {
            Destroy(fragment.Sprite);
        }
    }
}
