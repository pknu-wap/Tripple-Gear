using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class GunSelectionUI : MonoBehaviour
{
    [SerializeField] private GunSO submachineGun;
    [SerializeField] private GunSO shotgun;
    [SerializeField] private GunSO sniper;
    [SerializeField] private GunSO missile;
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private Material textOutline;
    [SerializeField] private Texture2D gunArtwork;
    [SerializeField] private Texture2D rocketArtwork;
    [SerializeField] private Shader artworkTransparency;

    private const float RevealDuration = 0.36f;
    private const float RevealStagger = 0.12f;
    private static readonly Color32 Accent = new Color32(79, 211, 245, 255);
    private static readonly Color32 SecondaryText = new Color32(207, 221, 232, 255);
    private static readonly Color32 Divider = new Color32(163, 186, 201, 175);
    private Material gunArtworkMaterial;
    private bool canSelect;

    internal bool Show(SubmachineGunFire playerGun, Action<GunSO, int> onSelected)
    {
        GunSO equipped = playerGun.CurrentGun;
        if (equipped == null)
        {
            equipped = submachineGun;
        }

        var alternatives = new List<GunSO>(3);
        foreach (GunSO gun in new[] { shotgun, sniper, missile })
        {
            if (gun != null && gun.CanFire && gun != equipped && !alternatives.Contains(gun))
            {
                alternatives.Add(gun);
            }
        }

        if (equipped == null || !equipped.CanFire || alternatives.Count < 2 || font == null
            || gunArtwork == null || rocketArtwork == null || artworkTransparency == null)
        {
            Debug.LogError("무기 선택 카드에 필요한 총 데이터, 글꼴, 이미지를 확인해 주세요.", this);
            return false;
        }

        int firstIndex = UnityEngine.Random.Range(0, alternatives.Count);
        GunSO first = alternatives[firstIndex];
        alternatives.RemoveAt(firstIndex);
        GunSO second = alternatives[UnityEngine.Random.Range(0, alternatives.Count)];

        gunArtworkMaterial = new Material(artworkTransparency);
        RectTransform layout = CreateRect(transform, "Reward Layout", Vector2.zero, Vector2.one);
        var selection = layout.gameObject.AddComponent<CanvasGroup>();
        selection.interactable = false;
        var aspect = layout.gameObject.AddComponent<AspectRatioFitter>();
        aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        aspect.aspectRatio = 16f / 9f;

        CreateText(layout, "Title", "챕터 클리어 · 무기 선택", new Vector2(0.05f, 0.80f),
            new Vector2(0.95f, 0.85f), Color.white, 45f, true);

        GunSO[] offers = { equipped, first, second };
        var cards = new CanvasGroup[offers.Length];
        for (int slot = 0; slot < offers.Length; slot++)
        {
            GunSO gun = offers[slot];
            int increase = UnityEngine.Random.Range(1, 6);
            cards[slot] = CreateCard(layout, slot, gun, playerGun.GetChapterDamage(gun), increase, onSelected);
        }

        StartCoroutine(RevealCards(selection, cards));
        return true;
    }

    private IEnumerator RevealCards(CanvasGroup selection, CanvasGroup[] cards)
    {
        float startedAt = Time.unscaledTime;
        float duration = RevealDuration + (cards.Length - 1) * RevealStagger;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            for (int i = 0; i < cards.Length; i++)
            {
                float progress = Mathf.Clamp01((elapsed - i * RevealStagger) / RevealDuration);
                float eased = 1f - (1f - progress) * (1f - progress) * (1f - progress);
                SetRevealPose(cards[i], eased);
            }

            yield return null;
            elapsed = Time.unscaledTime - startedAt;
        }

        foreach (CanvasGroup card in cards)
        {
            SetRevealPose(card, 1f);
        }

        canSelect = true;
        selection.interactable = true;
    }

    private static void SetRevealPose(CanvasGroup card, float progress)
    {
        var rect = (RectTransform)card.transform;
        card.alpha = progress;
        rect.localScale = Vector3.one * Mathf.Lerp(0.91f, 1f, progress);
        rect.anchoredPosition = new Vector2(0f, (progress - 1f) * rect.rect.height * 0.10f);
    }

    private CanvasGroup CreateCard(RectTransform layout, int slot, GunSO gun, float currentDamage, int increase,
        Action<GunSO, int> onSelected)
    {
        const float cardWidth = 0.298f;
        float left = 0.029f + slot * 0.322f;
        string title = GetWeaponName(gun);
        RectTransform card = CreateRect(layout, title, new Vector2(left, 0.08f), new Vector2(left + cardWidth, 0.78f));

        var frame = card.gameObject.AddComponent<GunRewardCardFrame>();
        var button = card.gameObject.AddComponent<GunRewardCardButton>();
        button.targetGraphic = frame;
        button.onClick.AddListener(() =>
        {
            if (canSelect)
            {
                onSelected(gun, increase);
            }
        });
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color32(118, 223, 255, 255);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = Accent;
        colors.disabledColor = colors.normalColor;
        button.colors = colors;

        if (slot == 0)
        {
            CreateText(card, "Equipped", "장착 중", new Vector2(0.05f, 0.916f), new Vector2(0.95f, 0.977f),
                Accent, 22f, true);
        }

        CreateText(card, "Weapon Name", title, new Vector2(0.08f, 0.825f), new Vector2(0.92f, 0.917f),
            Color.white, 44f, true);
        CreateLine(card, "Title Divider", 0.81f, 0.12f);

        RectTransform artArea = CreateRect(card, "Artwork Area", new Vector2(0.06f, 0.305f), new Vector2(0.94f, 0.785f));
        RawImage art = CreateRect(artArea, "Weapon Artwork", Vector2.zero, Vector2.one).gameObject.AddComponent<RawImage>();
        art.raycastTarget = false;
        art.texture = gun == missile ? rocketArtwork : gunArtwork;
        if (gun != missile)
        {
            art.material = gunArtworkMaterial;
        }
        art.uvRect = GetArtworkRegion(gun);
        var aspect = art.gameObject.AddComponent<AspectRatioFitter>();
        aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        aspect.aspectRatio = art.texture.width * art.uvRect.width / (art.texture.height * art.uvRect.height);
        button.SetArtwork(art.rectTransform);

        CreateLine(card, "Damage Divider", 0.29f, 0.06f);
        CreateText(card, "Damage Label", "데미지", new Vector2(0.10f, 0.169f), new Vector2(0.90f, 0.248f),
            SecondaryText, 24f, false);
        CreateText(card, "Damage Value", $"{currentDamage:0.##} <color=#4FD3F5>+{increase}</color>",
            new Vector2(0.10f, 0.055f), new Vector2(0.90f, 0.169f), Color.white, 47f, true);
        return card.gameObject.AddComponent<CanvasGroup>();
    }

    private string GetWeaponName(GunSO gun)
    {
        if (gun == shotgun) return "산탄총";
        if (gun == sniper) return "저격총";
        if (gun == missile) return "로켓런처";
        return "기관단총";
    }

    private Rect GetArtworkRegion(GunSO gun)
    {
        if (gun == missile)
        {
            return new Rect(0.025f, 0.28f, 0.9f, 0.44f);
        }

        // Pixel coordinates of the three illustrations in the supplied reference atlas.
        float left = gun == shotgun ? 56f : gun == sniper ? 1140f : 602f;
        return new Rect(left / 1672f, 1f - 586f / 941f, 480f / 1672f, 313f / 941f);
    }

    private void CreateText(Transform parent, string name, string value, Vector2 min, Vector2 max, Color color,
        float maxSize, bool bold)
    {
        var label = CreateRect(parent, name, min, max).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        if (textOutline != null)
        {
            label.fontSharedMaterial = textOutline;
        }
        label.text = value;
        label.color = color;
        label.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true;
        label.fontSizeMin = 1f;
        label.fontSizeMax = maxSize;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;
    }

    private void OnDestroy()
    {
        Destroy(gunArtworkMaterial);
    }

    private static void CreateLine(Transform parent, string name, float height, float inset)
    {
        var line = CreateRect(parent, name, new Vector2(inset, height), new Vector2(1f - inset, height))
            .gameObject.AddComponent<Image>();
        line.rectTransform.sizeDelta = new Vector2(0f, 1f);
        line.color = Divider;
        line.raycastTarget = false;
    }

    private static RectTransform CreateRect(Transform parent, string name, Vector2 min, Vector2 max)
    {
        var child = new GameObject(name, typeof(RectTransform));
        child.layer = parent.gameObject.layer;
        var rect = (RectTransform)child.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }
}
