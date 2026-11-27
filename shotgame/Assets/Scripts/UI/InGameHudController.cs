using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class InGameHudController : MonoBehaviour
{
    [System.Serializable]
    private sealed class SkillSlotView
    {
        [SerializeField] private Image icon;
        [SerializeField] private Image countBadge;
        [SerializeField] private TextMeshProUGUI count;

        internal RectTransform Rect => icon != null ? icon.rectTransform.parent as RectTransform : null;

        internal void Show(SkillSO data, int uses)
        {
            Sprite sprite = data != null ? data.HudIcon : null;
            if (icon != null)
            {
                icon.sprite = sprite;
                icon.enabled = sprite != null;
            }

            if (countBadge != null) countBadge.enabled = data != null;
            if (count != null)
            {
                count.text = data != null ? uses.ToString() : string.Empty;
            }
        }
    }

    [SerializeField] private Transform player;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private SkillSlotView activeSkill;
    [SerializeField] private SkillSlotView storedSkill;
    [SerializeField] private TextMeshProUGUI ammoText;

    private ArisaHealth health;
    private ArisaSkillController skill;
    private SubmachineGunFire gun;
    private GameManager scoreSource;
    private TextMeshProUGUI scoreText;
    private RectTransform skillPanel;
    private RectTransform skillTooltip;
    private TextMeshProUGUI skillTooltipTitle;
    private TextMeshProUGUI skillTooltipDescription;
    private SkillSO hoverSkill;
    private Canvas hudCanvas;

    private void Awake()
    {
        if (player != null)
        {
            health = player.GetComponent<ArisaHealth>();
            skill = player.GetComponent<ArisaSkillController>();
            gun = player.GetComponent<SubmachineGunFire>();
        }
    }

    private void OnEnable()
    {
        if (health != null) health.HpChanged.AddListener(UpdateHp);
        if (skill != null) skill.SkillSlotsChanged += UpdateSkill;
        if (gun != null) gun.AmmoChanged += UpdateAmmo;
        RefreshAll();
    }

    private void Start()
    {
        // Other components can initialize after this HUD's OnEnable.
        RefreshAll();
        scoreSource = GameManager.Instance;
        if (scoreSource != null && hpText != null) CreateScorePanel((RectTransform)hpText.transform.parent);
        CreateSkillTooltip();
    }

    private void CreateSkillTooltip()
    {
        RectTransform slot = activeSkill?.Rect ?? storedSkill?.Rect;
        if (slot == null || slot.parent is not RectTransform panel || panel.parent is not RectTransform uiRoot)
            return;

        skillPanel = panel;
        hudCanvas = GetComponentInParent<Canvas>();
        var frame = new GameObject("스킬 설명", typeof(Image)).GetComponent<Image>();
        skillTooltip = frame.rectTransform;
        skillTooltip.SetParent(uiRoot, false);
        skillTooltip.SetSiblingIndex(panel.GetSiblingIndex() + 1);
        skillTooltip.pivot = Vector2.zero;
        skillTooltip.sizeDelta = new Vector2(416f, 124f);
        frame.raycastTarget = false;
        frame.color = new Color(0.055f, 0.075f, 0.11f, 0.92f);
        if (panel.TryGetComponent(out Image skillFrame) && skillFrame.sprite != null)
        {
            var border = new GameObject("스킬 설명 테두리", typeof(Image)).GetComponent<Image>();
            border.rectTransform.SetParent(skillTooltip, false);
            border.rectTransform.anchorMin = Vector2.zero;
            border.rectTransform.anchorMax = Vector2.one;
            border.rectTransform.offsetMin = border.rectTransform.offsetMax = Vector2.zero;
            border.sprite = skillFrame.sprite;
            border.raycastTarget = false;
        }

        skillTooltipTitle = CreateTooltipLabel("스킬 이름", 30f, 13f, 36f);
        skillTooltipDescription = CreateTooltipLabel("스킬 효과", 23f, 53f, 60f);
        frame.gameObject.SetActive(false);
    }

    private TextMeshProUGUI CreateTooltipLabel(string labelName, float size, float top, float height)
    {
        var label = ScoreText.Create(skillTooltip, labelName, size);
        if (hpText != null)
        {
            label.font = hpText.font;
            label.fontSharedMaterial = hpText.fontSharedMaterial;
        }
        label.alignment = TextAlignmentOptions.TopLeft;
        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(18f, -top - height);
        rect.offsetMax = new Vector2(-18f, -top);
        return label;
    }

    private void LateUpdate()
    {
        if (skillTooltip == null) return;
        if (skill == null || !skillPanel.gameObject.activeInHierarchy || GameplayInput.IsBlocked ||
            GameplayPause.IsPaused || Mouse.current == null)
        {
            ShowSkillTooltip(null);
            return;
        }

        Vector2 mouse = Mouse.current.position.ReadValue();
        Camera uiCamera = hudCanvas != null && hudCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? hudCanvas.worldCamera : null;
        SkillSO hovered = IsOverSlot(activeSkill?.Rect, mouse, uiCamera) ? skill.SkillData
            : IsOverSlot(storedSkill?.Rect, mouse, uiCamera) ? skill.StoredSkillData : null;
        ShowSkillTooltip(hovered);

        if (hovered != null)
        {
            Vector2 corner = new Vector2(skillPanel.rect.xMax, skillPanel.rect.yMin);
            Vector3 position = skillTooltip.parent.InverseTransformPoint(skillPanel.TransformPoint(corner));
            skillTooltip.localPosition = position + Vector3.right * 12f;
        }
    }

    private static bool IsOverSlot(RectTransform slot, Vector2 mouse, Camera uiCamera) =>
        slot != null && slot.gameObject.activeInHierarchy &&
        RectTransformUtility.RectangleContainsScreenPoint(slot, mouse, uiCamera);

    private void ShowSkillTooltip(SkillSO hovered)
    {
        if (skillTooltip == null || hoverSkill == hovered) return;
        hoverSkill = hovered;
        if (hovered != null)
        {
            skillTooltipTitle.text = hovered.DisplayName;
            skillTooltipDescription.text = hovered.Description;
        }
        skillTooltip.gameObject.SetActive(hovered != null);
    }

    private void CreateScorePanel(RectTransform hpPanel)
    {
        var panel = new GameObject("점수 HUD", typeof(Image)).GetComponent<Image>();
        RectTransform rect = panel.rectTransform;
        rect.SetParent(hpPanel.parent, false);
        rect.SetSiblingIndex(hpPanel.GetSiblingIndex() + 1);
        rect.anchorMin = rect.anchorMax = hpPanel.anchorMin;
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = hpPanel.anchoredPosition + Vector2.down * (hpPanel.rect.height + 6f);
        rect.sizeDelta = new Vector2(410f, 78f);
        if (hpPanel.TryGetComponent(out Image hpFrame))
        {
            panel.sprite = hpFrame.sprite;
            panel.color = hpFrame.color;
        }
        panel.raycastTarget = false;

        scoreText = ScoreText.Create(rect, "현재 점수", 36f);
        scoreText.font = hpText.font;
        scoreText.fontSharedMaterial = hpText.fontSharedMaterial;
        scoreText.alignment = TextAlignmentOptions.MidlineLeft;
        scoreText.rectTransform.anchorMin = Vector2.zero;
        scoreText.rectTransform.anchorMax = Vector2.one;
        scoreText.rectTransform.offsetMin = new Vector2(18f, 0f);
        scoreText.rectTransform.offsetMax = new Vector2(-18f, 0f);
        scoreSource.ScoreChanged += UpdateScore;
        UpdateScore(scoreSource.Score);
    }

    private void OnDestroy()
    {
        if (scoreSource != null) scoreSource.ScoreChanged -= UpdateScore;
    }

    private void UpdateScore(int score) => scoreText.text = $"점수 : {score:N0}";

    private void OnDisable()
    {
        ShowSkillTooltip(null);
        if (health != null) health.HpChanged.RemoveListener(UpdateHp);
        if (skill != null) skill.SkillSlotsChanged -= UpdateSkill;
        if (gun != null) gun.AmmoChanged -= UpdateAmmo;
    }

    private void RefreshAll()
    {
        if (health != null) UpdateHp(health.CurrentHp, health.MaxHp);
        if (skill != null) UpdateSkill();
        if (gun != null) UpdateAmmo(gun.CurrentBulletCount, gun.MaxBulletCount);
    }

    private void UpdateHp(int current, int maximum)
    {
        if (hpText != null) hpText.text = $": {current}";
    }

    private void UpdateSkill()
    {
        activeSkill?.Show(skill.SkillData, skill.RemainingUseCount);
        storedSkill?.Show(skill.StoredSkillData, skill.StoredUseCount);
    }

    private void UpdateAmmo(int current, int maximum)
    {
        if (ammoText != null) ammoText.text = gun != null && gun.HasUnlimitedAmmo ? ": 무제한" : $": {current}/{maximum}";
    }
}
