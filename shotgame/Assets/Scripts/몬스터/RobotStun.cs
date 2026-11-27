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
    private SpriteRenderer stunSprite;
    private Material arcMaterial;
    private readonly LineRenderer[] arcs = new LineRenderer[3];

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
        CreateElectricArcs();
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

    private void CreateElectricArcs()
    {
        float largest = 0f;
        foreach (SpriteRenderer sprite in GetComponentsInChildren<SpriteRenderer>())
        {
            Vector3 size = sprite.bounds.size;
            float area = size.x * size.y;
            if (!sprite.enabled || sprite.sprite == null || area <= largest) continue;
            stunSprite = sprite;
            largest = area;
        }
        if (stunSprite == null) return;

        arcMaterial = new Material(Shader.Find("Sprites/Default"));
        for (int i = 0; i < arcs.Length; i++)
        {
            var arc = new GameObject("EMP 감전 스파크").AddComponent<LineRenderer>();
            arc.transform.SetParent(transform, false);
            arc.sharedMaterial = arcMaterial;
            arc.sortingLayerID = stunSprite.sortingLayerID;
            arc.sortingOrder = stunSprite.sortingOrder + 6;
            arc.positionCount = 6;
            arc.numCapVertices = 2;
            arc.startColor = new Color(0.68f, 0.98f, 1f, 0.96f);
            arc.endColor = new Color(0.19f, 0.52f, 1f, 0.76f);
            arcs[i] = arc;
        }
        LateUpdate();
    }

    private void LateUpdate()
    {
        if (stunSprite == null) return;
        Bounds bounds = stunSprite.bounds;
        float frame = Mathf.Floor(Time.time * 24f);
        for (int i = 0; i < arcs.Length; i++)
        {
            LineRenderer arc = arcs[i];
            arc.widthMultiplier = Mathf.Clamp(bounds.size.y * 0.055f, 0.042f, 0.13f);
            float angle = (i * 2.094f) + frame * 0.18f;
            for (int point = 0; point < arc.positionCount; point++)
            {
                float t = (float)point / (arc.positionCount - 1);
                float bend = angle + t * 1.65f;
                float radius = 0.73f + Mathf.Sin(frame * 2.63f + i + point * 11.7f) * 0.38f;
                Vector3 offset = new Vector3(Mathf.Cos(bend) * bounds.extents.x,
                    Mathf.Sin(bend) * bounds.extents.y, 0f);
                arc.SetPosition(point, bounds.center + offset * radius);
            }
        }
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
        stunSprite = null;
        foreach (LineRenderer arc in arcs)
        {
            if (arc == null) continue;
            arc.enabled = false;
            Destroy(arc.gameObject);
        }
        if (arcMaterial != null) Destroy(arcMaterial);
        arcMaterial = null;
    }
}
