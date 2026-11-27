using TMPro;
using UnityEngine;
using UnityEngine.UI;

internal static class AudioSettingsControls
{
    internal delegate TextMeshProUGUI LabelFactory(Transform parent, string name, float fontSize);

    internal static void AddTo(RectTransform panel, LabelFactory makeLabel)
    {
        CreateVolumeSlider(panel, "배경음", 55f, GameAudioSettings.Music, level => GameAudioSettings.Music = level, makeLabel);
        CreateVolumeSlider(panel, "효과음", -48f, GameAudioSettings.Effects, level => GameAudioSettings.Effects = level, makeLabel);
    }

    private static void CreateVolumeSlider(RectTransform parent, string name, float y, float initial,
        System.Action<float> save, LabelFactory makeLabel)
    {
        var caption = makeLabel(parent, name + " 음량 표시", 29f);
        caption.rectTransform.anchoredPosition = new Vector2(0f, y + 26f);
        caption.rectTransform.sizeDelta = new Vector2(338f, 35f);

        var slider = new GameObject(name + " Slider", typeof(RectTransform), typeof(Slider)).GetComponent<Slider>();
        var rect = (RectTransform)slider.transform;
        rect.SetParent(parent, false);
        rect.anchoredPosition = new Vector2(0f, y - 9f);
        rect.sizeDelta = new Vector2(310f, 34f);

        Image track = CreateSliderGraphic(rect, "Track", new Color(0.04f, 0.08f, 0.14f, 0.95f));
        track.rectTransform.offsetMin = new Vector2(0f, 10f);
        track.rectTransform.offsetMax = new Vector2(0f, -10f);
        var handle = CreateSliderGraphic(rect, "Handle", new Color(0.73f, 0.91f, 1f));
        handle.rectTransform.anchorMin = new Vector2(0f, 0f);
        handle.rectTransform.anchorMax = new Vector2(0f, 1f);
        handle.rectTransform.sizeDelta = new Vector2(20f, 0f);

        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.SetValueWithoutNotify(initial);
        caption.text = $"{name}   {Mathf.RoundToInt(initial * 100f)}%";
        slider.onValueChanged.AddListener(value =>
        {
            save(value);
            caption.text = $"{name}   {Mathf.RoundToInt(value * 100f)}%";
        });
    }

    private static Image CreateSliderGraphic(RectTransform parent, string name, Color color)
    {
        var image = new GameObject(name, typeof(Image)).GetComponent<Image>();
        image.transform.SetParent(parent, false);
        image.rectTransform.anchorMin = Vector2.zero;
        image.rectTransform.anchorMax = Vector2.one;
        image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;
        image.color = color;
        return image;
    }
}
