using UnityEngine;

[CreateAssetMenu(fileName = "몬스터SO", menuName = "Shotgame/몬스터/몬스터")]
public sealed class MonsterSO : ScriptableObject
{
    [Header("체력")]
    [SerializeField, KoreanLabel("최대 HP"), Min(1)] private int maxHp = 5;

    [Header("이동")]
    [SerializeField, KoreanLabel("이동 속도"), Min(0f)] private float moveSpeed = 2f;
    [SerializeField, KoreanLabel("Y축 추적")] private bool followYAxis;

    [Header("공격")]
    [SerializeField, KoreanLabel("공격력"), Min(0)] private int attackDamage = 1;
    [SerializeField, KoreanLabel("첫 공격 지연"), Min(0f)] private float firstAttackDelay = 0.4f;
    [SerializeField, KoreanLabel("공격 간격"), Min(0.1f)] private float attackInterval = 2f;

    public int MaxHp => Mathf.Max(1, maxHp);
    public float MoveSpeed => Mathf.Max(0f, moveSpeed);
    public bool FollowYAxis => followYAxis;
    public int AttackDamage => Mathf.Max(0, attackDamage);
    public float FirstAttackDelay => Mathf.Max(0f, firstAttackDelay);
    public float AttackInterval => Mathf.Max(0.1f, attackInterval);

    private void OnValidate()
    {
        maxHp = Mathf.Max(1, maxHp);
        moveSpeed = Mathf.Max(0f, moveSpeed);
        attackDamage = Mathf.Max(0, attackDamage);
        firstAttackDelay = Mathf.Max(0f, firstAttackDelay);
        attackInterval = Mathf.Max(0.1f, attackInterval);
    }
}
