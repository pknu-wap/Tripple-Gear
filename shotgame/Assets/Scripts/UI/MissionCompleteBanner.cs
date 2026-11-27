using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasRenderer))]
public sealed class MissionCompleteBanner : MaskableGraphic
{
    private const string ArtworkPath = "UI/MissionComplete";
    private const float LetterInterval = 0.075f;
    private const float HoldDuration = 0.9f;
    private const float FadeDuration = 0.3f;
    private const float ArtWidth = 2172f;
    private const float ArtHeight = 724f;

    // Each cut gives the x coordinate at mid-height and the slope of a gap
    // between two italic glyphs in the source artwork.
    private static readonly Vector2[] MissionCuts =
    {
        new Vector2(710.2f, 0.30f), new Vector2(821.2f, 0.31f),
        new Vector2(995f, 0.33f), new Vector2(1175.3f, 0.30f),
        new Vector2(1284.2f, 0.27f), new Vector2(1479.9f, 0.29f),
    };
    private static readonly Vector2[] CompleteCuts =
    {
        new Vector2(564f, 0.33f), new Vector2(768.3f, 0.33f),
        new Vector2(1015.2f, 0.25f), new Vector2(1204.5f, 0.27f),
        new Vector2(1384.8f, 0.28f), new Vector2(1559.3f, 0.22f),
        new Vector2(1744.1f, 0.33f),
    };

    private Texture2D artwork;
    private int visibleLetters;

    public override Texture mainTexture => artwork != null ? artwork : base.mainTexture;

    internal static MissionCompleteBanner Show(Scene scene)
    {
        Texture2D texture = Resources.Load<Texture2D>(ArtworkPath);
        if (texture == null) return null;

        GameObject overlay = new GameObject("Mission Complete Overlay", typeof(Canvas), typeof(CanvasGroup));
        SceneManager.MoveGameObjectToScene(overlay, scene);
        Canvas canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        CanvasGroup group = overlay.GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        GameObject logo = new GameObject("Mission Complete", typeof(RectTransform), typeof(MissionCompleteBanner));
        logo.transform.SetParent(overlay.transform, false);
        MissionCompleteBanner banner = logo.GetComponent<MissionCompleteBanner>();
        banner.artwork = texture;
        banner.raycastTarget = false;
        RectTransform rect = banner.rectTransform;
        rect.anchorMin = new Vector2(0.18f, 0.70f);
        rect.anchorMax = new Vector2(0.82f, 0.70f);
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;
        AspectRatioFitter fitter = logo.AddComponent<AspectRatioFitter>();
        fitter.aspectRatio = ArtWidth / ArtHeight;
        fitter.aspectMode = AspectRatioFitter.AspectMode.WidthControlsHeight;
        banner.SetMaterialDirty();
        GameplaySfx.Play(GameplaySfx.Cue.Clear);
        banner.StartCoroutine(banner.Reveal(group));
        return banner;
    }

    private IEnumerator Reveal(CanvasGroup group)
    {
        int letterCount = MissionCuts.Length + CompleteCuts.Length + 2;
        for (visibleLetters = 1; visibleLetters <= letterCount; visibleLetters++)
        {
            SetVerticesDirty();
            if (visibleLetters < letterCount) yield return new WaitForSecondsRealtime(LetterInterval);
        }

        yield return new WaitForSecondsRealtime(HoldDuration);
        float fadeStart = Time.unscaledTime;
        while (Time.unscaledTime - fadeStart < FadeDuration)
        {
            group.alpha = 1f - (Time.unscaledTime - fadeStart) / FadeDuration;
            yield return null;
        }
        Hide();
    }

    internal void Hide()
    {
        GameObject overlay = transform.parent.gameObject;
        overlay.SetActive(false);
        Destroy(overlay);
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (artwork == null) return;

        int missionLength = MissionCuts.Length + 1;
        Rect rect = GetPixelAdjustedRect();
        AddRow(mesh, rect, MissionCuts, Mathf.Min(visibleLetters, missionLength), true);
        AddRow(mesh, rect, CompleteCuts,
            Mathf.Clamp(visibleLetters - missionLength, 0, CompleteCuts.Length + 1), false);
    }

    private void AddRow(VertexHelper mesh, Rect rect, Vector2[] cuts, int count, bool topRow)
    {
        float outsideY = topRow ? 0f : ArtHeight;
        for (int i = 0; i < count; i++)
        {
            Vector2 left = i == 0 ? Vector2.zero : cuts[i - 1];
            Vector2 right = i == cuts.Length ? new Vector2(ArtWidth, 0f) : cuts[i];
            Vector2 a = new Vector2(left.x - left.y * (outsideY - 360f), outsideY);
            Vector2 b = AtRowDivider(left);
            Vector2 c = AtRowDivider(right);
            Vector2 d = new Vector2(right.x - right.y * (outsideY - 360f), outsideY);
            int vertex = mesh.currentVertCount;
            AddVertex(mesh, rect, topRow ? a : b);
            AddVertex(mesh, rect, topRow ? b : a);
            AddVertex(mesh, rect, topRow ? c : d);
            AddVertex(mesh, rect, topRow ? d : c);
            mesh.AddTriangle(vertex, vertex + 1, vertex + 2);
            mesh.AddTriangle(vertex + 2, vertex + 3, vertex);
        }
    }

    private static Vector2 AtRowDivider(Vector2 cut)
    {
        // The gap between the two lines rises eight pixels per hundred pixels across.
        const float dividerIntercept = 346f + 0.08f * (ArtWidth * 0.5f);
        float x = (cut.x + cut.y * (360f - dividerIntercept)) / (1f - cut.y * 0.08f);
        return new Vector2(x, dividerIntercept - 0.08f * x);
    }

    private void AddVertex(VertexHelper mesh, Rect rect, Vector2 pixel)
    {
        Vector2 uv = new Vector2(pixel.x / ArtWidth, 1f - pixel.y / ArtHeight);
        Vector2 position = rect.min + Vector2.Scale(rect.size, uv);
        mesh.AddVert(position, color, uv);
    }
}
