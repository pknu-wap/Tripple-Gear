using System;
using System.Collections;
using UnityEngine;

[Serializable]
public sealed class DamageBoostSkillLogic : SkillLogic
{
    [SerializeField, KoreanLabel("공격력 증가량"), Min(0)] private int damageBonus = 5;
    [SerializeField, KoreanLabel("지속 시간"), Min(0.1f)] private float duration = 5f;

    public override bool CanExecute(SkillContext context) =>
        context.Owner != null && context.Owner.TryGetComponent<SubmachineGunFire>(out _);

    public override IEnumerator Execute(SkillContext context)
    {
        SubmachineGunFire gun = context.Owner.GetComponent<SubmachineGunFire>();
        GameObject effect = SkillEffectVisual.CreateRisingSparks(context.Owner, duration, "홍염 과부하",
            new Color(1f, 0.06f, 0.08f), new Color(1f, 0.38f, 0.17f));
        gun.StartDamageBoost(damageBonus, duration, effect);
        yield break;
    }
}
