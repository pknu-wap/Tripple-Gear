using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
internal sealed class RobotStun : MonoBehaviour
{
    private readonly Dictionary<Animator, float> animatorSpeeds = new Dictionary<Animator, float>();
    private MonsterHealth health;
    private Rigidbody2D body;
    private RigidbodyConstraints2D originalConstraints;
    private float endsAt;

    internal static void Apply(MonsterHealth target, float duration)
    {
        if (duration <= 0f || (!target.TryGetComponent<NormalMonsterChase>(out _)
            && !target.TryGetComponent<FlyingMonsterAttack>(out _)
            && !target.TryGetComponent<NormalMonsterAttack>(out _)
            && !target.TryGetComponent<ExplosiveMonster>(out _)))
        {
            return;
        }

        RobotStun stun = target.GetComponent<RobotStun>();
        if (stun == null)
        {
            stun = target.gameObject.AddComponent<RobotStun>();
            stun.health = target;
            stun.enabled = false;
        }

        stun.endsAt = Mathf.Max(stun.endsAt, Time.time + duration);
        stun.enabled = true;
    }

    private void OnEnable()
    {
        if (health == null) return;

        health.SetStunned(true);
        health.Defeated.AddListener(End);
        body = GetComponent<Rigidbody2D>();
        if (body != null && body.bodyType != RigidbodyType2D.Static)
        {
            originalConstraints = body.constraints;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeAll;
        }
        else
        {
            body = null;
        }

        foreach (Animator animator in GetComponentsInChildren<Animator>())
        {
            animatorSpeeds[animator] = animator.speed;
            animator.speed = 0f;
        }
    }

    internal static float GetAnimationSpeed(Animator animator)
    {
        RobotStun stun = animator.GetComponentInParent<RobotStun>();
        return stun != null && stun.animatorSpeeds.TryGetValue(animator, out float speed) ? speed : animator.speed;
    }

    internal static void SetAnimationSpeed(Animator animator, float speed)
    {
        RobotStun stun = animator.GetComponentInParent<RobotStun>();
        if (stun != null && stun.animatorSpeeds.ContainsKey(animator))
        {
            stun.animatorSpeeds[animator] = speed;
        }
        else
        {
            animator.speed = speed;
        }
    }

    private void Update()
    {
        if (Time.time >= endsAt) End();
    }

    internal void End()
    {
        enabled = false;
    }

    private void OnDisable()
    {
        endsAt = 0f;
        if (health != null)
        {
            health.SetStunned(false);
            health.Defeated.RemoveListener(End);
        }

        if (body != null && body.constraints == RigidbodyConstraints2D.FreezeAll)
        {
            body.constraints = originalConstraints;
        }
        body = null;

        foreach (KeyValuePair<Animator, float> animation in animatorSpeeds)
        {
            if (animation.Key != null) animation.Key.speed = animation.Value;
        }
        animatorSpeeds.Clear();
    }
}
