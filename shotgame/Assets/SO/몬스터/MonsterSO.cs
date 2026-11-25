using UnityEngine;

[CreateAssetMenu(fileName = "몬스터SO", menuName = "Shotgame/몬스터/몬스터")]
public sealed class MonsterSO : ScriptableObject
{
    [System.Serializable]
    private struct DropSlot
    {
        [SerializeField, InspectorName("아이템 프리팹")] internal GameObject prefab;
        [SerializeField, InspectorName("확률 (1 = 100%)"), Range(0f, 1f)] internal float chance;

        internal float Weight => prefab != null ? Mathf.Clamp01(chance) : 0f;
    }

    [Header("체력")]
    [SerializeField, KoreanLabel("최대 HP"), Min(1)] private int maxHp = 5;

    [Header("이동")]
    [SerializeField, KoreanLabel("이동 속도"), Min(0f)] private float moveSpeed = 2f;

    [Header("공격")]
    [SerializeField, KoreanLabel("공격력"), Min(0)] private int attackDamage = 1;
    [SerializeField, KoreanLabel("공격 간격"), Min(0.1f)] private float attackInterval = 2f;

    [Header("피격 효과")]
    [SerializeField, KoreanLabel("벽 피격 효과 사용")] private bool useWallHitFlash;

    [Header("아이템 드롭")]
    [SerializeField, KoreanLabel("드롭 슬롯"), Tooltip("최대 하나를 드롭합니다. 확률 합계가 1 미만이면 남은 확률로 아무것도 나오지 않습니다. 1을 넘으면 각 확률의 비율로 추첨합니다.")]
    private DropSlot[] dropSlots;
    [SerializeField, HideInInspector] private GameObject dropPrefab;
    [SerializeField, HideInInspector] private float dropChance;

    public GameObject DropPrefab => HasDropSlots ? dropSlots[0].prefab : dropPrefab;
    public float DropChance => HasDropSlots ? Mathf.Clamp01(dropSlots[0].chance) : Mathf.Clamp01(dropChance);
    public int MaxHp => Mathf.Max(1, maxHp);
    public float MoveSpeed => Mathf.Max(0f, moveSpeed);
    public int AttackDamage => Mathf.Max(0, attackDamage);
    public float AttackInterval => Mathf.Max(0.1f, attackInterval);
    public bool UseWallHitFlash => useWallHitFlash;

    private bool HasDropSlots => dropSlots != null && dropSlots.Length > 0;

    internal GameObject RollDrop()
    {
        if (!HasDropSlots)
        {
            float chance = Mathf.Clamp01(dropChance);
            return dropPrefab != null && chance > 0f && (chance >= 1f || Random.value < chance) ? dropPrefab : null;
        }

        float total = 0f;
        GameObject lastItem = null;
        foreach (DropSlot slot in dropSlots)
        {
            float weight = slot.Weight;
            if (weight <= 0f) continue;
            total += weight;
            lastItem = slot.prefab;
        }
        if (lastItem == null) return null;

        float roll = Random.value * Mathf.Max(1f, total);
        foreach (DropSlot slot in dropSlots)
        {
            float weight = slot.Weight;
            if (weight > 0f && roll < weight) return slot.prefab;
            roll -= weight;
        }
        return total >= 1f ? lastItem : null;
    }

    private void OnValidate()
    {
        maxHp = Mathf.Max(1, maxHp);
        moveSpeed = Mathf.Max(0f, moveSpeed);
        attackDamage = Mathf.Max(0, attackDamage);
        attackInterval = Mathf.Max(0.1f, attackInterval);
    }
}
