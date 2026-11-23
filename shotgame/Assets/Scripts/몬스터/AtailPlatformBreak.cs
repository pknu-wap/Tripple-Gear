using UnityEngine;

internal sealed class AtailPlatformBreak : MonoBehaviour
{
    private const int PieceCount = 5;
    private static readonly ushort[] Triangles = { 0, 1, 2, 0, 2, 5, 5, 2, 3, 5, 3, 4 };
    private readonly Sprite[] pieces = new Sprite[PieceCount];

    internal static void Play(SpriteRenderer source, Transform parent, AnimationClip clip, int foregroundOrder)
    {
        if (clip == null) return;

        var effect = new GameObject("발판 파괴 파편").AddComponent<AtailPlatformBreak>();
        Transform root = effect.transform;
        root.SetParent(parent, true);
        root.SetPositionAndRotation(source.transform.position, source.transform.rotation);
        var properties = new MaterialPropertyBlock();
        source.GetPropertyBlock(properties);

        int sortingOrder = Mathf.Max(source.sortingOrder + 4, foregroundOrder);
        for (int i = 0; i < PieceCount; i++)
            effect.CreatePiece(i, source, properties, sortingOrder);

        var animation = effect.gameObject.AddComponent<Animation>();
        animation.AddClip(clip, clip.name);
        animation.Play(clip.name);
        Destroy(effect.gameObject, clip.length);
    }

    private void CreatePiece(int index, SpriteRenderer source, MaterialPropertyBlock properties, int sortingOrder)
    {
        Sprite original = source.sprite;
        Rect rect = original.rect;
        Vector2[] vertices =
        {
            Edge(index, 0), Edge(index + 1, 0), Edge(index + 1, 1),
            Edge(index + 1, 2), Edge(index, 2), Edge(index, 1)
        };
        Vector2 pivot = (vertices[2] + vertices[5]) * 0.5f;
        for (int i = 0; i < vertices.Length; i++)
            vertices[i] = Vector2.Scale(vertices[i], rect.size);
        Sprite piece = Sprite.Create(original.texture, rect, pivot, original.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        piece.OverrideGeometry(vertices, Triangles);
        pieces[index] = piece;

        Vector2 offset = (Vector2.Scale(pivot, rect.size) - original.pivot) / original.pixelsPerUnit;
        if (source.flipX) offset.x = -offset.x;
        if (source.flipY) offset.y = -offset.y;
        int animationIndex = source.flipX ? PieceCount - index : index + 1;
        var anchor = new GameObject($"Piece {animationIndex}").transform;
        anchor.SetParent(transform, false);
        anchor.position = source.transform.TransformPoint(offset);

        var renderer = new GameObject("Motion").AddComponent<SpriteRenderer>();
        renderer.transform.SetParent(anchor, false);
        renderer.transform.localScale = source.transform.lossyScale;
        renderer.sprite = piece;
        renderer.sharedMaterial = source.sharedMaterial;
        renderer.flipX = source.flipX;
        renderer.flipY = source.flipY;
        renderer.color = source.color;
        renderer.sortingLayerID = source.sortingLayerID;
        renderer.sortingOrder = sortingOrder;
        renderer.SetPropertyBlock(properties);
    }

    private static Vector2 Edge(int column, int row)
    {
        float x = column / (float)PieceCount;
        if (column > 0 && column < PieceCount)
        {
            float zigzag = row == 1 ? 0.057f : -0.035f;
            x += column % 2 == 0 ? zigzag : -zigzag;
        }
        return new Vector2(x, row * 0.5f);
    }

    private void OnDestroy()
    {
        foreach (Sprite piece in pieces)
            if (piece != null) Destroy(piece);
    }
}
