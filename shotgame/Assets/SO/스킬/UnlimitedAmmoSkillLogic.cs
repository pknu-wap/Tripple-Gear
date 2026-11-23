using System;
using System.Collections;
using UnityEngine;

[Serializable]
public sealed class UnlimitedAmmoSkillLogic : SkillLogic
{
    [SerializeField, KoreanLabel("지속 시간"), Min(0.1f)] private float duration = 5f;

    public override bool CanExecute(SkillContext context) =>
        context.Owner != null && context.Owner.TryGetComponent<SubmachineGunFire>(out _);

    public override IEnumerator Execute(SkillContext context)
    {
        GameObject effect = SkillEffectVisual.CreateRisingSparks(context.Owner, duration, "무한 탄창",
            new Color(0.07f, 0.32f, 1f), new Color(0.38f, 0.9f, 1f));
        context.Owner.GetComponent<SubmachineGunFire>().StartUnlimitedAmmo(duration, effect);
        yield break;
    }
}
