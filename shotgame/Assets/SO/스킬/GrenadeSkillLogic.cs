using System;
using System.Collections;
using UnityEngine;

[Serializable]
public sealed class GrenadeSkillLogic : SkillLogic
{
    [SerializeField, KoreanLabel("폭탄 스프라이트")] private Sprite projectileSprite;
    [SerializeField, KoreanLabel("폭발 스프라이트")] private Sprite burstSprite;
    [SerializeField, KoreanLabel("폭발 효과 프리팹")] private GameObject explosionEffect;
    [SerializeField, KoreanLabel("전기 충격탄")] private bool electric;
    [SerializeField, KoreanLabel("피해량"), Min(0)] private int damage = 30;
    [SerializeField, KoreanLabel("효과 반경"), Min(0.1f)] private float radius = 3f;
    [SerializeField, KoreanLabel("정지 시간"), Min(0.1f)] private float stunSeconds = 3f;
    [SerializeField, KoreanLabel("투척 속도"), Min(0.1f)] private float speed = 12f;
    [SerializeField, KoreanLabel("최대 비행 시간"), Min(0.1f)] private float fuseSeconds = 1.6f;
    [SerializeField, KoreanLabel("충돌 레이어")] private LayerMask impactLayers = ~0;

    public override bool RequiresAimDirection => true;
    public override bool CanExecute(SkillContext context) => context.Owner != null && projectileSprite != null;

    public override IEnumerator Execute(SkillContext context)
    {
        Vector2 direction = context.AimDirection.normalized;
        Vector3 origin = context.Owner.position + Vector3.up * 0.55f;
        GameObject grenade = new GameObject(electric ? "전자기 충격탄" : "파쇄탄");
        grenade.transform.position = origin;
        grenade.AddComponent<SkillGrenade>().Launch(context.Owner, projectileSprite, explosionEffect, electric,
            direction * speed + Vector2.up * 4f, fuseSeconds, impactLayers, radius, damage, stunSeconds, burstSprite);
        yield break;
    }
}
