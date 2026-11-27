using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class EndingCredits : MonoBehaviour
{
    private RectTransform textGroup;
    private Image background;

    internal static GameObject Show(Scene scene, int score, GameManager manager)
    {
        var root = new GameObject("엔딩 크레딧", typeof(Canvas), typeof(CanvasScaler),
            typeof(GraphicRaycaster), typeof(Image), typeof(EndingCredits));
        SceneManager.MoveGameObjectToScene(root, scene);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 400;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        var background = root.GetComponent<Image>();
        background.color = new Color(0.015f, 0.025f, 0.05f, 0f);
        background.raycastTarget = false;

        var group = new GameObject("크레딧 문구", typeof(RectTransform)).GetComponent<RectTransform>();
        group.SetParent(root.transform, false);
        group.anchorMin = Vector2.zero;
        group.anchorMax = Vector2.one;
        group.sizeDelta = Vector2.zero;
        AddLine(group, "감사합니다", 78f, 70f, Color.white);
        AddLine(group, $"최종 점수 : {score:N0}", 42f, -36f, new Color(0.83f, 0.91f, 1f));
        if (manager != null) AddLobbyButton(group, manager);
        var credits = root.GetComponent<EndingCredits>();
        credits.textGroup = group;
        credits.background = background;
        credits.StartCoroutine(credits.ScrollIn());
        return root;
    }

    private static void AddLine(RectTransform parent, string text, float size, float y, Color color)
    {
        var label = ScoreText.Create(parent, text, size);
        label.rectTransform.anchorMin = new Vector2(0.05f, 0.5f);
        label.rectTransform.anchorMax = new Vector2(0.95f, 0.5f);
        label.rectTransform.sizeDelta = new Vector2(0f, size * 1.4f);
        label.rectTransform.anchoredPosition = new Vector2(0f, y);
        label.text = text;
        label.color = color;
    }

    private static void AddLobbyButton(RectTransform parent, GameManager manager)
    {
        var image = new GameObject("로비로", typeof(Image), typeof(Button)).GetComponent<Image>();
        image.rectTransform.SetParent(parent, false);
        image.rectTransform.anchoredPosition = new Vector2(0f, -135f);
        image.rectTransform.sizeDelta = new Vector2(240f, 66f);
        image.color = new Color(0.04f, 0.10f, 0.19f, 0.86f);
        var button = image.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(manager.GoToLobby);
        AddLine(image.rectTransform, "로비로", 31f, 0f, Color.white);
    }

    private IEnumerator ScrollIn()
    {
        const float duration = 3.2f;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            textGroup.anchoredPosition = Vector2.up * Mathf.Lerp(-680f, 0f, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            background.color = new Color(0.015f, 0.025f, 0.05f, 0.42f * Mathf.Clamp01(elapsed / 0.8f));
            yield return null;
        }
        textGroup.anchoredPosition = Vector2.zero;
        background.color = new Color(0.015f, 0.025f, 0.05f, 0.42f);
    }
}
