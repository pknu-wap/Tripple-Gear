using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public sealed class MonsterHealth : MonoBehaviour
{
    [System.Serializable]
    private sealed class HealthChangedEvent : UnityEvent<int, int>
    {
    }

    [SerializeField, KoreanLabel("몬스터 데이터")] private MonsterSO monsterData;
    [SerializeField, KoreanLabel("HP 변경 시 호출")] private HealthChangedEvent hpChanged = new HealthChangedEvent();
    [SerializeField, KoreanLabel("HP 0 도달 시 호출")] private UnityEvent defeated = new UnityEvent();

    // 같은 SO를 사용하는 몬스터도 현재 HP는 각자 보관한다.
    private int currentHp;

    public MonsterSO Data => monsterData;
    public int MaxHp => monsterData != null ? monsterData.MaxHp : 0;
    public int CurrentHp => currentHp;
    public bool IsDead => currentHp <= 0;
    public UnityEvent<int, int> HpChanged => hpChanged;
    public UnityEvent Defeated => defeated;

    private void Awake()
    {
        if (monsterData == null)
        {
            Debug.LogError("MonsterHealth에 몬스터 SO를 연결해 주세요.", this);
            return;
        }

        currentHp = MaxHp;
        hpChanged?.Invoke(currentHp, MaxHp);
    }

    public void TakeDamage(int amount)
    {
        if (amount <= 0 || IsDead)
        {
            return;
        }

        SetHp(currentHp - amount);
    }

    public void TakeDamage(float amount)
    {
        TakeDamage(Mathf.CeilToInt(amount));
    }

    public void Heal(int amount)
    {
        if (amount <= 0 || IsDead)
        {
            return;
        }

        SetHp(amount >= MaxHp - currentHp ? MaxHp : currentHp + amount);
    }

    private void SetHp(int hp)
    {
        int previousHp = currentHp;
        currentHp = Mathf.Clamp(hp, 0, MaxHp);
        if (currentHp == previousHp)
        {
            return;
        }

        hpChanged?.Invoke(currentHp, MaxHp);
        if (IsDead && previousHp > 0)
        {
            defeated?.Invoke();
        }
    }
}
