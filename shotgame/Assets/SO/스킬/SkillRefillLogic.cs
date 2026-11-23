using System;
using System.Collections;
using UnityEngine;

[Serializable]
public sealed class SkillRefillLogic : SkillLogic
{
    [SerializeField, KoreanLabel("추가 사용 횟수"), Min(1)] private int extraUses = 2;

    public override bool ActivatesOnPickup => true;

    public override bool TryApplyOnPickup(ArisaSkillController controller) =>
        controller != null && controller.TryAddSkillUses(extraUses);

    public override IEnumerator Execute(SkillContext context)
    {
        yield break;
    }
}
