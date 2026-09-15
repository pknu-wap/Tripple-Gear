using System.Collections;
using UnityEngine;

public readonly struct SkillContext
{
    public SkillContext(
        Transform owner,
        Vector3 mouseWorldPosition,
        Vector2 aimDirection,
        Camera camera,
        MonoBehaviour coroutineRunner,
        ArisaHorizontalMovement movement,
        ArisaInvincibility invincibility
    )
    {
        Owner = owner;
        MouseWorldPosition = mouseWorldPosition;
        AimDirection = aimDirection;
        Camera = camera;
        CoroutineRunner = coroutineRunner;
        Movement = movement;
        Invincibility = invincibility;
    }

    public Transform Owner { get; }
    public Vector3 MouseWorldPosition { get; }
    public Vector2 AimDirection { get; }
    public Camera Camera { get; }
    public MonoBehaviour CoroutineRunner { get; }
    public ArisaHorizontalMovement Movement { get; }
    public ArisaInvincibility Invincibility { get; }
}

[CreateAssetMenu(fileName = "스킬SO", menuName = "Shotgame/스킬/스킬")]
public sealed class SkillSO : ScriptableObject
{
    [SerializeField, KoreanLabel("사용 가능 횟수"), Min(1)] private int 사용가능횟수 = 1;
    [SerializeField, KoreanLabel("쿨타임"), Min(0f)] private float 쿨타임 = 1f;
    [SerializeField, KoreanLabel("스킬 중 무적")] private bool 스킬중무적;
    [SerializeReference, KoreanLabel("스킬 로직")] private SkillLogic 스킬로직 = new DashSkillLogic();

    public int MaxUseCount => Mathf.Max(1, 사용가능횟수);
    public float Cooldown => Mathf.Max(0f, 쿨타임);
    public bool IsInvincibleWhileActive => 스킬중무적;
    public bool HasLogic => 스킬로직 != null;
    public SkillLogic Logic => 스킬로직;

    public IEnumerator Execute(SkillContext context)
    {
        return 스킬로직 != null ? 스킬로직.Execute(context) : null;
    }

    private void OnValidate()
    {
        사용가능횟수 = Mathf.Max(1, 사용가능횟수);
        쿨타임 = Mathf.Max(0f, 쿨타임);
        스킬로직?.OnValidate();
    }
}
