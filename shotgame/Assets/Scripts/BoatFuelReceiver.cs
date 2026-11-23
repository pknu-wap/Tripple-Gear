using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public sealed class BoatFuelReceiver : MonoBehaviour
{
    [SerializeField, KoreanLabel("연료 수량 목표")] private MissionObjective objective;
    [SerializeField, KoreanLabel("기름통 도착 지점")] private Transform intake;
    [SerializeField, KoreanLabel("투척 기름통 그림")] private Sprite canisterSprite;
    [SerializeField, KoreanLabel("투척 그림 크기"), Min(0.01f)] private float canisterWidth = 0.55f;
    [SerializeField, KoreanLabel("투척 시간 (초)"), Min(0.01f)] private float flightSeconds = 0.48f;
    [SerializeField, KoreanLabel("투척 포물선 높이"), Min(0f)] private float arcHeight = 1.6f;
    [SerializeField, KoreanLabel("연료 게이지 칸 (아래부터)")] private Image[] gaugeCells;

    private Coroutine transfer;
    private MissionResource carriedFuel;
    private SpriteRenderer flyingCanister;
    private bool fuelInFlight;
    private int shownCount = -1;
    private int shownRequired = -1;

    private void Reset() => GetComponent<BoxCollider2D>().isTrigger = true;
    private void Awake() => GetComponent<BoxCollider2D>().isTrigger = true;

    private void OnTriggerEnter2D(Collider2D other) => TryThrow(other);
    private void OnTriggerStay2D(Collider2D other) => TryThrow(other);

    private void TryThrow(Collider2D other)
    {
        if (!isActiveAndEnabled || transfer != null || objective == null || objective.RemainingCount <= 0
            || intake == null || canisterSprite == null)
        {
            return;
        }

        ArisaHorizontalMovement player = other.GetComponentInParent<ArisaHorizontalMovement>();
        if (player == null || !player.TryGetComponent(out MissionResource resource) || resource.Amount == 0)
        {
            return;
        }

        carriedFuel = resource;
        transfer = StartCoroutine(ThrowFuel(player.transform));
    }

    private IEnumerator ThrowFuel(Transform thrower)
    {
        while (thrower != null && carriedFuel != null && objective != null && intake != null && objective.RemainingCount > 0
            && carriedFuel.Withdraw(1) == 1)
        {
            fuelInFlight = true;
            Vector3 origin = thrower.position + Vector3.up * 0.65f;
            flyingCanister = new GameObject("투척 중인 기름통").AddComponent<SpriteRenderer>();
            flyingCanister.sprite = canisterSprite;
            flyingCanister.sortingOrder = 15;
            flyingCanister.transform.localScale = Vector3.one * (canisterWidth / canisterSprite.bounds.size.x);
            flyingCanister.transform.position = origin;

            float elapsed = 0f;
            while (elapsed < flightSeconds && objective != null && intake != null && objective.IsRunning)
            {
                yield return null;
                if (objective == null || intake == null || !objective.IsRunning) break;
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, flightSeconds));
                Vector3 position = Vector3.Lerp(origin, intake.position, progress);
                position.y += 4f * arcHeight * progress * (1f - progress);
                flyingCanister.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, -360f * progress));
            }

            if (objective == null || intake == null || objective.RemainingCount <= 0)
            {
                RefundFuel();
                break;
            }

            fuelInFlight = false;
            RemoveFlyingCanister();
            objective.AddProgress(1);
        }

        carriedFuel = null;
        transfer = null;
    }

    private void LateUpdate()
    {
        if (objective == null || gaugeCells == null
            || (shownCount == objective.Count && shownRequired == objective.RequiredCount))
        {
            return;
        }

        shownCount = objective.Count;
        shownRequired = objective.RequiredCount;
        float filledCells = (float)((double)shownCount * gaugeCells.Length / shownRequired);
        for (int i = 0; i < gaugeCells.Length; i++)
        {
            if (gaugeCells[i] != null)
            {
                gaugeCells[i].fillAmount = Mathf.Clamp01(filledCells - i);
            }
        }
    }

    private void OnDisable()
    {
        if (transfer != null) StopCoroutine(transfer);
        RefundFuel();
        transfer = null;
        carriedFuel = null;
    }

    private void RefundFuel()
    {
        if (fuelInFlight && carriedFuel != null) carriedFuel.Collect(1);
        fuelInFlight = false;
        RemoveFlyingCanister();
    }

    private void RemoveFlyingCanister()
    {
        if (flyingCanister != null) Destroy(flyingCanister.gameObject);
        flyingCanister = null;
    }
}
