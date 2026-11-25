using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("")]
public sealed class GunRewardCardFrame : Image
{
    protected override void OnPopulateMesh(VertexHelper vertices)
    {
        vertices.Clear();

        Rect rect = GetPixelAdjustedRect();
        float corner = rect.width * 0.038f;
        float edge = Mathf.Max(1f, rect.width * 0.006f);
        AddOutline(vertices, rect, corner, edge, new Color32(185, 197, 206, 255));
        AddOutline(vertices, Inset(rect, edge * 3f), corner, edge * 0.5f, new Color32(133, 158, 175, 155));

        float center = rect.center.x;
        float tabY = rect.yMin + edge * 3.5f;
        int first = vertices.currentVertCount;
        var accent = new Color32(47, 150, 227, 255);
        vertices.AddVert(new Vector2(center - corner * 0.8f, tabY), accent, Vector2.zero);
        vertices.AddVert(new Vector2(center + corner * 0.8f, tabY), accent, Vector2.zero);
        vertices.AddVert(new Vector2(center, tabY - edge * 2.2f), accent, Vector2.zero);
        vertices.AddTriangle(first, first + 1, first + 2);
    }

    private static Rect Inset(Rect rect, float amount)
    {
        return Rect.MinMaxRect(rect.xMin + amount, rect.yMin + amount, rect.xMax - amount, rect.yMax - amount);
    }

    private static void AddOutline(VertexHelper vertices, Rect rect, float corner, float width, Color32 color)
    {
        int first = vertices.currentVertCount;
        AddOctagonVertices(vertices, rect, corner, color);
        AddOctagonVertices(vertices, Inset(rect, width), corner - width * 0.586f, color);

        for (int i = 0; i < 8; i++)
        {
            int outer = first + i;
            int next = first + (i + 1) % 8;
            vertices.AddTriangle(outer, next, next + 8);
            vertices.AddTriangle(outer, next + 8, outer + 8);
        }
    }

    private static void AddOctagonVertices(VertexHelper vertices, Rect rect, float corner, Color32 color)
    {
        vertices.AddVert(new Vector2(rect.xMin + corner, rect.yMax), color, Vector2.zero);
        vertices.AddVert(new Vector2(rect.xMax - corner, rect.yMax), color, Vector2.zero);
        vertices.AddVert(new Vector2(rect.xMax, rect.yMax - corner), color, Vector2.zero);
        vertices.AddVert(new Vector2(rect.xMax, rect.yMin + corner), color, Vector2.zero);
        vertices.AddVert(new Vector2(rect.xMax - corner, rect.yMin), color, Vector2.zero);
        vertices.AddVert(new Vector2(rect.xMin + corner, rect.yMin), color, Vector2.zero);
        vertices.AddVert(new Vector2(rect.xMin, rect.yMin + corner), color, Vector2.zero);
        vertices.AddVert(new Vector2(rect.xMin, rect.yMax - corner), color, Vector2.zero);
    }
}
