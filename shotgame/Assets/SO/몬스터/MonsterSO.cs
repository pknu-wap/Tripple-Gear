using UnityEngine;

[CreateAssetMenu(fileName = "몬스터SO", menuName = "Shotgame/몬스터/몬스터")]
public sealed class MonsterSO : ScriptableObject
{
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
    [SerializeField, KoreanLabel("드롭 아이템 프리팹")] private GameObject dropPrefab;
    [SerializeField, KoreanLabel("드롭 확률 (1 = 100%)"), Range(0f, 1f)] private float dropChance;

    public GameObject DropPrefab => dropPrefab;
    public float DropChance => Mathf.Clamp01(dropChance);
    public int MaxHp => Mathf.Max(1, maxHp);
    public float MoveSpeed => Mathf.Max(0f, moveSpeed);
    public int AttackDamage => Mathf.Max(0, attackDamage);
    public float AttackInterval => Mathf.Max(0.1f, attackInterval);
    public bool UseWallHitFlash => useWallHitFlash;

    private void OnValidate()
    {
        maxHp = Mathf.Max(1, maxHp);
        moveSpeed = Mathf.Max(0f, moveSpeed);
        attackDamage = Mathf.Max(0, attackDamage);
        attackInterval = Mathf.Max(0.1f, attackInterval);
    }
}
