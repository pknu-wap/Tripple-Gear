using UnityEngine;
using UnityEngine.UI;

[ExecuteAlways] // 에디터 모드(비실행 상태)에서도 Inspector 변경을 즉시 반영
public class UIBarController : MonoBehaviour
{
    [Header("UI Reference")]
    [SerializeField] private Image barFillImage;
    [SerializeField] private GameObject boss;
    [SerializeField] private GameObject barRoot;

    [Header("Bar Settings")]
    [Range(0f, 100f)]
    [SerializeField] private float currentValue = 100f;
    private const float MaxValue = 100f;
    private MonsterHealth bossHealth;
    private MonsterHealth encounterBoss;
    private Color defaultFillColor;
    private Color previousFillColor;

    internal void ShowEncounter(MonsterHealth target)
    {
        if (target == null || encounterBoss == target) return;
        if (encounterBoss != null) encounterBoss.HpChanged.RemoveListener(UpdateBossHealth);
        else if (barFillImage != null) previousFillColor = barFillImage.color;

        encounterBoss = target;
        if (barFillImage != null) barFillImage.color = defaultFillColor;
        if (!isActiveAndEnabled) return;

        encounterBoss.HpChanged.AddListener(UpdateBossHealth);
        UpdateBossHealth(target.CurrentHp, target.MaxHp);
    }

    internal void HideEncounter(MonsterHealth target)
    {
        if (target == null || encounterBoss != target) return;

        encounterBoss.HpChanged.RemoveListener(UpdateBossHealth);
        encounterBoss = null;
        if (barFillImage != null) barFillImage.color = previousFillColor;
        if (isActiveAndEnabled) RefreshBossHealth();
    }

    private MonsterHealth DisplayedBoss => encounterBoss != null ? encounterBoss : bossHealth;
    private bool HasLivingBoss => DisplayedBoss != null && DisplayedBoss.gameObject.activeInHierarchy && !DisplayedBoss.IsDead;

    private void RefreshBossHealth()
    {
        MonsterHealth target = DisplayedBoss;
        UpdateBossHealth(target != null ? target.CurrentHp : 0, target != null ? target.MaxHp : 0);
    }

    private void Awake()
    {
        if (!Application.isPlaying) return;

        defaultFillColor = barFillImage != null ? barFillImage.color : Color.white;
        if (boss == null) return;
        bossHealth = boss.GetComponent<MonsterHealth>();
        SetBossBarVisible(false);
    }

    private void OnEnable()
    {
        if (!Application.isPlaying) return;

        if (bossHealth != null) bossHealth.HpChanged.AddListener(OnDefaultBossHealthChanged);
        if (encounterBoss != null) encounterBoss.HpChanged.AddListener(UpdateBossHealth);
        if (bossHealth != null || encounterBoss != null) RefreshBossHealth();
    }

    private void Update()
    {
        if (Application.isPlaying && (bossHealth != null || encounterBoss != null))
        {
            SetBossBarVisible(HasLivingBoss);
        }
    }

    private void OnDisable()
    {
        if (!Application.isPlaying) return;

        if (bossHealth != null) bossHealth.HpChanged.RemoveListener(OnDefaultBossHealthChanged);
        if (encounterBoss != null) encounterBoss.HpChanged.RemoveListener(UpdateBossHealth);
        if (bossHealth != null || encounterBoss != null) SetBossBarVisible(false);
    }

    private void OnDefaultBossHealthChanged(int currentHp, int maxHp)
    {
        if (encounterBoss == null) UpdateBossHealth(currentHp, maxHp);
    }

    private void UpdateBossHealth(int currentHp, int maxHp)
    {
        SetValue(maxHp > 0 ? currentHp * MaxValue / maxHp : 0f);
        SetBossBarVisible(HasLivingBoss);
    }

    private void SetBossBarVisible(bool visible)
    {
        if (barRoot != null && barRoot.activeSelf != visible)
        {
            barRoot.SetActive(visible);
        }
    }

    private void OnValidate()
    {
        UpdateBar();
    }

    private void UpdateBar()
    {
        if (barFillImage != null)
        {
            // 0~100 사이 값으로 제한 후 0.0~1.0 비율로 변환
            currentValue = Mathf.Clamp(currentValue, 0f, MaxValue);
            barFillImage.fillAmount = currentValue / MaxValue;
        }
    }

    // 외부 스크립트에서 값을 변경할 때 사용할 수 있는 함수
    public void SetValue(float newValue)
    {
        currentValue = newValue;
        UpdateBar();
    }
}
