using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class InGameHudController : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private TextMeshProUGUI skillText;
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
        if (skill != null) skill.RemainingUseCountChanged += UpdateSkill;
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
        if (skill != null) skill.RemainingUseCountChanged -= UpdateSkill;
        if (gun != null) gun.AmmoChanged -= UpdateAmmo;
    }

    private void RefreshAll()
    {
        if (health != null) UpdateHp(health.CurrentHp, health.MaxHp);
        if (skill != null) UpdateSkill(skill.RemainingUseCount);
        if (gun != null) UpdateAmmo(gun.CurrentBulletCount, gun.MaxBulletCount);
    }

    private void UpdateHp(int current, int maximum)
    {
        if (hpText != null) hpText.text = $": {current}";
    }

    private void UpdateSkill(int remaining)
    {
        if (skillText != null) skillText.text = $"스킬 수 : {remaining}";
    }

    private void UpdateAmmo(int current, int maximum)
    {
        if (ammoText != null) ammoText.text = $": {maximum}/{current}";
    }
}
