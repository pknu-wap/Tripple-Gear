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

    private void Awake()
    {
        if (!Application.isPlaying || boss == null)
        {
            return;
        }

        bossHealth = boss.GetComponent<MonsterHealth>();
        SetBossBarVisible(false);
    }

    private void OnEnable()
    {
        if (!Application.isPlaying || boss == null)
        {
            return;
        }

        if (bossHealth == null)
        {
            bossHealth = boss.GetComponent<MonsterHealth>();
        }

        if (bossHealth != null)
        {
            bossHealth.HpChanged.AddListener(UpdateBossHealth);
            UpdateBossHealth(bossHealth.CurrentHp, bossHealth.MaxHp);
        }
    }

    private void Update()
    {
        if (Application.isPlaying && boss != null)
        {
            SetBossBarVisible(boss.activeInHierarchy && bossHealth != null && !bossHealth.IsDead);
        }
    }

    private void OnDisable()
    {
        if (!Application.isPlaying || boss == null)
        {
            return;
        }

        if (bossHealth != null)
        {
            bossHealth.HpChanged.RemoveListener(UpdateBossHealth);
        }

        SetBossBarVisible(false);
    }

    private void UpdateBossHealth(int currentHp, int maxHp)
    {
        SetValue(maxHp > 0 ? currentHp * MaxValue / maxHp : 0f);
        SetBossBarVisible(boss != null && boss.activeInHierarchy && currentHp > 0);
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
