using TMPro;
using UnityEngine;
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
    }

    private void OnDisable()
    {
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
        if (ammoText != null) ammoText.text = gun != null && gun.HasUnlimitedAmmo ? ": 무제한" : $": {maximum}/{current}";
    }
}
