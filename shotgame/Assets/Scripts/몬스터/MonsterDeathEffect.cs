using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MonsterHealth))]
public sealed class MonsterDeathEffect : MonoBehaviour
{
    [SerializeField, KoreanLabel("사망 효과 프리팹")] private GameObject deathEffectPrefab;

    private MonsterHealth health;
    private bool hasHandledDeath;

    private void Awake()
    {
        health = GetComponent<MonsterHealth>();
    }

    private void OnEnable()
    {
        if (hasHandledDeath)
        {
            gameObject.SetActive(false);
            return;
        }

        if (health == null)
        {
            health = GetComponent<MonsterHealth>();
        }

        health.Defeated.AddListener(HandleDefeated);
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.Defeated.RemoveListener(HandleDefeated);
        }
    }

    private void HandleDefeated()
    {
        if (hasHandledDeath)
        {
            return;
        }

        hasHandledDeath = true;
        if (deathEffectPrefab != null)
        {
            Instantiate(deathEffectPrefab, transform.position, Quaternion.identity);
        }

        gameObject.SetActive(false);
    }
}
