using TMPro;
using UnityEngine;

internal static class ScoreText
{
    internal static TextMeshProUGUI Create(Transform parent, string name, float fontSize)
    {
        var label = new GameObject(name, typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        label.transform.SetParent(parent, false);
        label.font = TMP_Settings.defaultFontAsset;
        label.fontSize = fontSize;
        label.color = Color.white;
        label.raycastTarget = false;
        label.alignment = TextAlignmentOptions.Center;
        return label;
    }
}
