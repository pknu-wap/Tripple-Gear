using System;
using System.Collections;
using UnityEngine;

[Serializable]
public sealed class GrenadeSkillLogic : SkillLogic
{
    [SerializeField, KoreanLabel("폭탄 스프라이트")] private Sprite projectileSprite;
    [SerializeField, KoreanLabel("비행 중 스프라이트 크기 (유닛)"), Min(0.01f)] private float projectileSize = 0.7f;
    [SerializeField, KoreanLabel("폭발 스프라이트")] private Sprite burstSprite;
    [SerializeField, KoreanLabel("폭발 스프라이트 크기 배율"), Min(0.01f)] private float burstScale = 1.5f;
    [SerializeField, KoreanLabel("폭발 효과 프리팹")] private GameObject explosionEffect;
    [SerializeField, KoreanLabel("전기 충격탄")] private bool electric;
    [SerializeField, KoreanLabel("피해량"), Min(0)] private int damage = 30;
    [SerializeField, KoreanLabel("효과 반경"), Min(0.1f)] private float radius = 3f;
    [SerializeField, KoreanLabel("정지 시간"), Min(0.1f)] private float stunSeconds = 3f;
    [SerializeField, KoreanLabel("투척 속도"), Min(0.1f)] private float speed = 12f;
    [SerializeField, KoreanLabel("초기 상승 속도"), Tooltip("조준 방향 속도에 더하는 수직 속도입니다. 음수를 입력하면 위로 향하는 속도가 줄어듭니다.")]
    private float upwardSpeed = 4f;
    [SerializeField, KoreanLabel("비행 중 중력 가속도"), Min(0f)] private float gravity = 17f;
    [SerializeField, KoreanLabel("최대 비행 거리 (유닛)"), Min(0.1f)] private float maxFlightDistance = 3f;
    [SerializeField, KoreanLabel("최대 비행 시간"), Min(0.1f)] private float fuseSeconds = 0.55f;
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
            direction * speed + Vector2.up * upwardSpeed, gravity, maxFlightDistance, fuseSeconds, impactLayers,
            radius, damage, stunSeconds, burstSprite, projectileSize, burstScale);
        yield break;
    }
}
