using System;
using UnityEngine;

public enum MonsterEntranceType
{
    [InspectorName("0 · 일반")] Normal,
    [InspectorName("1 · 하늘에서 낙하")] SkyDrop,
    [InspectorName("2 · 땅에서 솟아오르기")] GroundArc,
    [InspectorName("3 · 바다에서 솟아오르기")] SeaArc,
}

[Serializable]
internal struct MonsterEntranceSettings
{
    [SerializeField, InspectorName("등장 연출")] internal MonsterEntranceType type;
    [SerializeField, InspectorName("연출 시간 (초)"), Min(0f)] internal float duration;
    [SerializeField, InspectorName("포물선 높이"), Min(0f)] internal float arcHeight;
    [SerializeField, InspectorName("등장 효과 프리팹")] internal GameObject effectPrefab;
}

[Serializable]
internal sealed class PlacedMonsterEntrance
{
    [SerializeField] internal GameObject monster;
    [SerializeField] internal Transform start;
    [SerializeField] internal MonsterEntranceSettings entrance;

    internal static PlacedMonsterEntrance Find(PlacedMonsterEntrance[] entries, GameObject target) =>
        entries == null ? null : Array.Find(entries, entry => entry != null && entry.monster == target);

    internal bool HasValidStart(GameObject target) =>
        entrance.type == MonsterEntranceType.Normal || start == null || start.gameObject.scene == target.scene;

    internal void Play(MonsterHealth health, Vector3 destination, Camera view)
    {
        if (entrance.type == MonsterEntranceType.Normal) return;

        Vector3 origin = start != null ? start.position : GetDefaultStart(destination, view);
        MonsterEntranceMotion.Play(health, origin, destination, entrance);
    }

    private Vector3 GetDefaultStart(Vector3 destination, Camera view)
    {
        if (entrance.type != MonsterEntranceType.SkyDrop)
        {
            return destination + new Vector3(-2.5f, -1.9f);
        }

        destination.y += 6f;
        if (view != null && view.orthographic)
        {
            destination.y = Mathf.Max(destination.y, view.transform.position.y + view.orthographicSize + 1.5f);
        }
        return destination;
    }
}

[DisallowMultipleComponent]
internal sealed class MonsterEntranceMotion : MonoBehaviour
{
    private MonsterHealth health;
    private Rigidbody2D body;
    private RigidbodyType2D originalBodyType;
    private MonsterEntranceSettings settings;
    private Vector3 from;
    private Vector3 to;
    private float elapsed;
    private bool flying;

    internal static void Play(MonsterHealth monster, Vector3 start, Vector3 destination, MonsterEntranceSettings entrance)
    {
        if (entrance.type == MonsterEntranceType.Normal || monster == null ||
            !monster.gameObject.activeInHierarchy || monster.IsDead)
        {
            return;
        }

        if (!monster.TryGetComponent(out MonsterEntranceMotion motion))
        {
            motion = monster.gameObject.AddComponent<MonsterEntranceMotion>();
        }
        else if (motion.health != null)
        {
            motion.enabled = false;
        }

        motion.Begin(monster, start, destination, entrance);
    }

    private void Begin(MonsterHealth monster, Vector3 start, Vector3 destination, MonsterEntranceSettings entrance)
    {
        health = monster;
        health.IsEntering = true;
        from = start;
        to = destination;
        settings = entrance;
        elapsed = 0f;
        flying = TryGetComponent<FlyingMonsterAttack>(out _);
        if (TryGetComponent(out body))
        {
            originalBodyType = body.bodyType;
            body.bodyType = RigidbodyType2D.Kinematic;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
        }
        SetPosition(from);
        enabled = true;
        if (settings.type != MonsterEntranceType.SkyDrop) PlayEffect(from);
    }

    private void Update()
    {
        if (health == null || health.IsDead)
        {
            enabled = false;
            return;
        }

        elapsed += Time.deltaTime;
        float duration = settings.duration > 0f ? settings.duration : 0.75f;
        float t = Mathf.Clamp01(elapsed / duration);
        if (settings.type == MonsterEntranceType.SkyDrop)
        {
            float distance = flying ? 1f - (1f - t) * (1f - t) : t * t;
            SetPosition(Vector3.LerpUnclamped(from, to, distance));
        }
        else
        {
            float height = settings.arcHeight > 0f ? settings.arcHeight : 3.5f;
            Vector3 position = Vector3.LerpUnclamped(from, to, t);
            position.y += 4f * height * t * (1f - t);
            SetPosition(position);
        }

        if (t >= 1f)
        {
            if (settings.type == MonsterEntranceType.SkyDrop) PlayEffect(to);
            enabled = false;
        }
    }

    private void SetPosition(Vector3 position)
    {
        if (body != null) body.position = position;
        transform.position = position;
        Physics2D.SyncTransforms();
    }

    private void PlayEffect(Vector3 position)
    {
        if (settings.effectPrefab != null)
        {
            Instantiate(settings.effectPrefab, position, Quaternion.identity);
        }
    }

    internal void FinishWithoutEffect()
    {
        if (isActiveAndEnabled && health != null && !health.IsDead)
        {
            SetPosition(to);
            enabled = false;
        }
    }

    private void OnDisable()
    {
        if (body != null) body.bodyType = originalBodyType;
        body = null;
        if (health != null) health.IsEntering = false;
        health = null;
    }
}
