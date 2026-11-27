using System.Collections;
using UnityEngine;

public sealed partial class GongOilBoss
{
    [SerializeField, KoreanLabel("기계실 폭발 효과")] private GameObject defeatExplosionPrefab;

    private Coroutine defeatSequence;
    private Transform defeatEffects;

    private IEnumerator PlayDefeatSequence(Vector3 defeatPosition)
    {
        defeatEffects = new GameObject("비행선 내부 연쇄 폭발").transform;
        defeatEffects.SetParent(transform, false);
        Rect visibleRoom = GetVisibleDefeatArea();
        const int burstCount = 14;

        for (int i = 0; i < burstCount; i++)
        {
            float progress = i / (float)(burstCount - 1);
            Vector3 position = i == 0 ? defeatPosition : new Vector3(
                Random.Range(visibleRoom.xMin, visibleRoom.xMax),
                Random.Range(visibleRoom.yMin, visibleRoom.yMax),
                defeatPosition.z);
            if (defeatExplosionPrefab != null)
            {
                GameObject burst = Instantiate(defeatExplosionPrefab, position, Quaternion.identity, defeatEffects);
                burst.transform.localScale *= Mathf.Lerp(1.1f, 2.2f, progress) * Random.Range(0.85f, 1.2f);
                Destroy(burst, 1.2f);
            }
            yield return new WaitForSeconds(Mathf.Lerp(0.30f, 0.10f, progress));
        }

        finalSkyBattle.Begin(player, bossBar, missileSprite, useWhiteFlash: true);
        Destroy(defeatEffects.gameObject, 0.25f);
        defeatSequence = null;
        GameplayInput.Unblock(this);
    }

    private Rect GetVisibleDefeatArea()
    {
        Bounds floor = arenaFloor.bounds;
        Bounds room = arenaBackground.bounds;
        var bounds = Rect.MinMaxRect(floor.min.x, floor.max.y + 0.6f, floor.max.x, room.max.y - 0.6f);
        Camera view = Camera.main;
        if (view == null) return bounds;

        Vector3 cameraPosition = view.transform.position;
        float halfHeight = view.orthographicSize * 0.86f;
        float halfWidth = view.orthographicSize * view.aspect * 0.90f;
        Rect onscreen = Rect.MinMaxRect(Mathf.Max(bounds.xMin, cameraPosition.x - halfWidth),
            Mathf.Max(bounds.yMin, cameraPosition.y - halfHeight),
            Mathf.Min(bounds.xMax, cameraPosition.x + halfWidth),
            Mathf.Min(bounds.yMax, cameraPosition.y + halfHeight));
        return onscreen.width > 0f && onscreen.height > 0f ? onscreen : bounds;
    }

    internal void CancelDefeatSequence()
    {
        if (defeatSequence != null)
        {
            StopCoroutine(defeatSequence);
            defeatSequence = null;
            GameplayInput.Unblock(this);
        }
        if (defeatEffects != null)
        {
            defeatEffects.gameObject.SetActive(false);
            Destroy(defeatEffects.gameObject);
            defeatEffects = null;
        }
    }
}
