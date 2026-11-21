using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class BossDebrisWaterline : MonoBehaviour
{
    [System.Serializable]
    private struct Platform
    {
        public SpriteRenderer debris;
        public Transform wave;
        public Color paintColor;
    }

    [SerializeField] private Platform[] platforms = System.Array.Empty<Platform>();
    [SerializeField] private float waveCrestOffset = 0.19f;

    private static readonly int Waterline = Shader.PropertyToID("_Waterline");
    private static readonly int SpriteUVRect = Shader.PropertyToID("_SpriteUVRect");
    private static readonly int PaintColor = Shader.PropertyToID("_PaintColor");
    private MaterialPropertyBlock properties;

    private void OnEnable()
    {
        properties ??= new MaterialPropertyBlock();
        LateUpdate();
    }

    private void LateUpdate()
    {
        foreach (Platform platform in platforms)
        {
            if (!platform.debris || !platform.debris.sprite || !platform.wave)
                continue;

            Sprite sprite = platform.debris.sprite;
            Bounds bounds = sprite.bounds;
            Vector3 crest = platform.wave.position
                + platform.wave.parent.TransformVector(Vector3.up * waveCrestOffset);
            float crestY = platform.debris.transform.InverseTransformPoint(crest).y;
            float height = Mathf.Clamp(Mathf.InverseLerp(bounds.min.y, bounds.max.y, crestY), 0.08f, 0.36f);
            Rect rect = sprite.rect;
            Texture2D texture = sprite.texture;

            platform.debris.GetPropertyBlock(properties);
            properties.SetFloat(Waterline, height);
            properties.SetVector(SpriteUVRect, new Vector4(rect.x / texture.width, rect.y / texture.height,
                rect.width / texture.width, rect.height / texture.height));
            properties.SetColor(PaintColor, platform.paintColor);
            platform.debris.SetPropertyBlock(properties);
        }
    }
}
