using UnityEngine;
using UnityEngine.UI;

internal sealed class BeachLaserImpactDust
{
    private const int PlumeCount = 19;
    private const int GroundCloudCount = 16;
    private const int GritCount = 22;
    private const float Duration = 1.36f;
    private static readonly Color LightDust = new Color(0.82f, 0.69f, 0.52f);
    private static readonly Color DarkDust = new Color(0.47f, 0.40f, 0.34f);
    private static readonly Color HotDust = new Color(1f, 0.60f, 0.32f);

    private sealed class Burst
    {
        internal RectTransform Root;
        internal Vector3 Position;
        internal float Started;
        internal readonly Particle[] Particles = new Particle[PlumeCount + GroundCloudCount + GritCount];
    }

    private struct Particle
    {
        internal Image Image;
        internal Color Color;
        internal Vector2 Size;
        internal Vector2 Velocity;
        internal float Delay;
        internal float Lifetime;
        internal float Angle;
        internal float Spin;
        internal bool IsGrit;
    }

    private readonly RectTransform canvas;
    private readonly RectTransform root;
    private readonly Texture2D texture;
    private readonly Sprite dustSprite;
    private readonly Burst[] bursts = new Burst[2];
    private readonly System.Random random = new System.Random();
    private int nextBurst;

    internal BeachLaserImpactDust(RectTransform canvas, RectTransform beam, Sprite gritSprite)
    {
        this.canvas = canvas;
        texture = CreateDustTexture();
        dustSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one * 0.5f);

        root = NewAnchor("레이저 바닥 먼지", canvas);
        root.anchorMin = Vector2.zero;
        root.anchorMax = Vector2.one;
        root.offsetMin = root.offsetMax = Vector2.zero;
        root.SetSiblingIndex(beam.GetSiblingIndex());

        for (int i = 0; i < bursts.Length; i++)
        {
            var burst = new Burst { Root = NewAnchor("충돌 먼지", root) };
            bursts[i] = burst;
            for (int j = 0; j < burst.Particles.Length; j++)
            {
                bool grit = j >= PlumeCount + GroundCloudCount;
                var image = new GameObject(grit ? "모래 알갱이" : "먼지 구름", typeof(Image)).GetComponent<Image>();
                image.rectTransform.SetParent(burst.Root, false);
                image.sprite = grit ? gritSprite : dustSprite;
                image.raycastTarget = false;
                burst.Particles[j].Image = image;
                burst.Particles[j].IsGrit = grit;
            }
        }
        Clear();
    }

    internal void Play(Vector3 position)
    {
        Burst burst = bursts[nextBurst];
        nextBurst = (nextBurst + 1) % bursts.Length;
        burst.Position = position;
        burst.Started = Time.time;
        burst.Root.SetAsLastSibling();
        burst.Root.gameObject.SetActive(true);

        for (int i = 0; i < burst.Particles.Length; i++)
        {
            ref Particle particle = ref burst.Particles[i];
            bool column = i < PlumeCount;
            float side = i % 2 == 0 ? -1f : 1f;
            particle.Angle = Range(-180f, 180f);
            particle.Spin = Range(-95f, 95f);
            particle.Delay = i < 5 ? 0f : Range(0f, column ? 0.18f : 0.12f);

            if (particle.IsGrit)
            {
                float size = Range(0.006f, 0.018f);
                particle.Size = new Vector2(size * 0.45f, size);
                particle.Velocity = new Vector2(side * Range(0.14f, 0.60f), Range(0.22f, 0.68f));
                particle.Lifetime = Range(0.32f, 0.72f);
                particle.Color = Color.Lerp(DarkDust, HotDust, Range(0.40f, 1f));
            }
            else
            {
                float size = Range(0.10f, 0.21f);
                particle.Size = new Vector2(size * (column ? 1f : 1.38f), size);
                particle.Velocity = column
                    ? new Vector2(side * Range(0.06f, 0.27f), Range(0.38f, 0.86f))
                    : new Vector2(side * Range(0.49f, 1.03f), Range(0.03f, 0.15f));
                particle.Lifetime = Range(0.82f, Duration - particle.Delay);
                particle.Color = Color.Lerp(DarkDust, LightDust, Range(0.24f, 0.95f));
            }
        }
    }

    internal void Update(Camera camera, Vector2 shakeOffset)
    {
        if (camera == null)
        {
            Clear();
            return;
        }

        float height = canvas.rect.height;
        foreach (Burst burst in bursts)
        {
            if (!burst.Root.gameObject.activeSelf) continue;
            float age = Time.time - burst.Started;
            if (age >= Duration)
            {
                burst.Root.gameObject.SetActive(false);
                continue;
            }

            Vector3 viewport = camera.WorldToViewportPoint(burst.Position);
            burst.Root.anchorMin = burst.Root.anchorMax = new Vector2(viewport.x, viewport.y) + shakeOffset;
            foreach (Particle particle in burst.Particles) Draw(particle, age - particle.Delay, height);
        }
    }

    private static void Draw(Particle particle, float age, float height)
    {
        bool visible = age >= 0f && age < particle.Lifetime;
        particle.Image.enabled = visible;
        if (!visible) return;

        float progress = age / particle.Lifetime;
        float travel = 0.50f * (1f - Mathf.Exp(-age * 2f));
        Vector2 offset = particle.Velocity * (particle.IsGrit ? age : travel);
        if (particle.IsGrit) offset.y -= 0.88f * age * age;
        float expansion = particle.IsGrit ? 1f : Mathf.Lerp(0.45f, 1.9f, Mathf.Sqrt(progress));
        Vector2 size = particle.Size * expansion;
        offset.y = Mathf.Max(particle.IsGrit ? 0f : size.y * 0.23f, offset.y);

        RectTransform rect = particle.Image.rectTransform;
        rect.anchoredPosition = offset * height;
        rect.sizeDelta = size * height;
        rect.localRotation = Quaternion.Euler(0f, 0f, particle.Angle + particle.Spin * age);

        float heat = (1f - Mathf.Clamp01(age / 0.18f)) * (particle.IsGrit ? 0.9f : 0.44f);
        Color color = Color.Lerp(particle.Color, HotDust, heat);
        float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.19f, 1f, progress));
        color.a = Mathf.Lerp(0.66f, 1f, Mathf.Clamp01(age / 0.07f)) * fade;
        particle.Image.color = color;
    }

    internal void Clear()
    {
        if (root == null) return;
        foreach (Burst burst in bursts) burst.Root.gameObject.SetActive(false);
        nextBurst = 0;
    }

    internal void Dispose()
    {
        if (root != null) Object.Destroy(root.gameObject);
        Object.Destroy(dustSprite);
        Object.Destroy(texture);
    }

    private float Range(float min, float max) => Mathf.Lerp(min, max, (float)random.NextDouble());

    private static RectTransform NewAnchor(string objectName, RectTransform parent)
    {
        var rect = new GameObject(objectName, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = Vector2.zero;
        return rect;
    }

    private static Texture2D CreateDustTexture()
    {
        const int size = 80;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "레이저 충돌 먼지",
            wrapMode = TextureWrapMode.Clamp,
        };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            Vector2 uv = new Vector2((x + 0.5f) / size, (y + 0.5f) / size);
            Vector2 point = uv * 2f - Vector2.one;
            float shape = Mathf.Max(
                0.74f - Vector2.Distance(point, new Vector2(-0.11f, -0.04f)),
                0.54f - Vector2.Distance(point, new Vector2(0.30f, 0.13f)),
                0.43f - Vector2.Distance(point, new Vector2(-0.33f, 0.26f)));
            float noise = Mathf.PerlinNoise(uv.x * 8f + 9f, uv.y * 8f + 17f);
            float edge = Mathf.Clamp01((0.98f - point.magnitude) * 6f);
            float density = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(shape * 2.2f + (noise - 0.54f) * 0.48f));
            byte tint = (byte)Mathf.Lerp(178f, 255f, noise);
            pixels[y * size + x] = new Color32(tint, tint, tint, (byte)(245f * density * edge));
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        return texture;
    }
}
