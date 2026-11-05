using System;
using System.Collections;
using UnityEngine;

[Serializable]
public sealed class HealSkillLogic : SkillLogic
{
    [SerializeField, KoreanLabel("회복량"), Min(1)] private int 회복량 = 1;

    public override bool ActivatesOnPickup => true;

    public override IEnumerator Execute(SkillContext context)
    {
        yield break;
    }

    public override bool TryApplyOnPickup(ArisaHealth health)
    {
        if (health == null || health.IsDead)
        {
            return false;
        }

        health.Heal(회복량);
        return true;
    }

    public override void OnValidate()
    {
        회복량 = Mathf.Max(1, 회복량);
    }
}
