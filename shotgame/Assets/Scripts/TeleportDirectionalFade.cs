using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TeleportDirectionalFade : MonoBehaviour
{
    [Header("페이드")]
    [SerializeField, KoreanLabel("페이드 색상")] private Color fadeColor = Color.black;
    [SerializeField, KoreanLabel("정렬 순서")] private int sortingOrder = 5000;
    [SerializeField, KoreanLabel("일시정지 중에도 재생")] private bool useUnscaledTime = true;

    private Canvas canvas;
    private Image fadeImage;

    public bool IsFading { get; private set; }

    public static TeleportDirectionalFade GetOrCreate()
    {
        TeleportDirectionalFade fade = FindExistingFade();
        if (fade != null)
        {
            return fade;
        }

        GameObject fadeObject = new GameObject("TeleportDirectionalFade");
        return fadeObject.AddComponent<TeleportDirectionalFade>();
    }

    private void Awake()
    {
        EnsureReferences();
        SetFadeAmount(0f);
        SetVisible(false);
    }

    private void OnValidate()
    {
        if (canvas != null)
        {
            canvas.sortingOrder = sortingOrder;
        }

        if (fadeImage != null)
        {
            fadeImage.color = fadeColor;
        }
    }

    public IEnumerator Play(System.Action onCovered, float fadeInSpeed, float fadeOutSpeed)
    {
        IsFading = true;

        yield return FadeIn(fadeInSpeed);
        onCovered?.Invoke();
        yield return FadeOut(fadeOutSpeed);

        IsFading = false;
    }

    public IEnumerator FadeIn(float speed)
    {
        IsFading = true;
        EnsureReferences();
        SetVisible(true);
        fadeImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        yield return AnimateFill(0f, 1f, speed);
    }

    public IEnumerator FadeOut(float speed)
    {
        EnsureReferences();
        SetVisible(true);
        fadeImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        yield return AnimateFill(1f, 0f, speed);
        SetVisible(false);
        IsFading = false;
    }

    private IEnumerator AnimateFill(float startAmount, float endAmount, float speed)
    {
        float amount = Mathf.Clamp01(startAmount);
        speed = Mathf.Max(0.01f, speed);
        SetFadeAmount(amount);

        while (!Mathf.Approximately(amount, endAmount))
        {
            float deltaTime = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            amount = Mathf.MoveTowards(amount, endAmount, speed * deltaTime);
            SetFadeAmount(amount);
            yield return null;
        }

        SetFadeAmount(endAmount);
    }

    private void EnsureReferences()
    {
        if (canvas == null)
        {
            canvas = GetComponentInChildren<Canvas>(true);
        }

        if (fadeImage == null)
        {
            fadeImage = GetComponentInChildren<Image>(true);
        }

        if (canvas != null && fadeImage != null)
        {
            canvas.sortingOrder = sortingOrder;
            fadeImage.color = fadeColor;
            fadeImage.type = Image.Type.Filled;
            fadeImage.fillMethod = Image.FillMethod.Horizontal;
            fadeImage.fillOrigin = (int)Image.OriginHorizontal.Left;
            return;
        }

        CreateFadeCanvas();
    }

    private void CreateFadeCanvas()
    {
        GameObject canvasObject = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler canvasScaler = canvasObject.GetComponent<CanvasScaler>();
        canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
        canvasScaler.matchWidthOrHeight = 0.5f;

        GameObject imageObject = new GameObject("BlackWipe", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(canvasObject.transform, false);

        RectTransform imageTransform = imageObject.GetComponent<RectTransform>();
        imageTransform.anchorMin = Vector2.zero;
        imageTransform.anchorMax = Vector2.one;
        imageTransform.offsetMin = Vector2.zero;
        imageTransform.offsetMax = Vector2.zero;

        fadeImage = imageObject.GetComponent<Image>();
        fadeImage.color = fadeColor;
        fadeImage.raycastTarget = false;
        fadeImage.type = Image.Type.Filled;
        fadeImage.fillMethod = Image.FillMethod.Horizontal;
        fadeImage.fillOrigin = (int)Image.OriginHorizontal.Left;
    }

    private void SetFadeAmount(float amount)
    {
        EnsureReferences();
        fadeImage.fillAmount = Mathf.Clamp01(amount);
    }

    private void SetVisible(bool visible)
    {
        EnsureReferences();
        canvas.enabled = visible;
    }

    private static TeleportDirectionalFade FindExistingFade()
    {
#if UNITY_2023_1_OR_NEWER
        return Object.FindFirstObjectByType<TeleportDirectionalFade>();
#else
        return Object.FindObjectOfType<TeleportDirectionalFade>();
#endif
    }
}
