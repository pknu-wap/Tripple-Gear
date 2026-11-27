using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed partial class FinalSkyBattle
{
    private Material effectsMaterial;
    private Transform hullArt;
    private Vector3 hullPosition;
    private Quaternion hullRotation;
    private SpriteRenderer hullRenderer;
    private Color hullNormalColor;
    private Image entryFlash;

    private IEnumerator EnterWithWhiteFlash()
    {
        var overlay = new GameObject("공중전 전환 섬광", typeof(Canvas), typeof(Image));
        SceneManager.MoveGameObjectToScene(overlay, gameObject.scene);
        var canvas = overlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;
        entryFlash = overlay.GetComponent<Image>();
        entryFlash.raycastTarget = false;
        entryFlash.color = WithAlpha(Color.white, 0f);
        GameplaySfx.Play(GameplaySfx.Cue.Flash, .60f);

        yield return AnimateEntryFlash(0f, 1f, 0.12f);
        if (player != null && !player.IsDead) EnterArena();
        yield return new WaitForSecondsRealtime(0.15f);
        yield return AnimateEntryFlash(1f, 0f, 0.60f);
        HideEntryFlash();
    }

    private IEnumerator AnimateEntryFlash(float from, float to, float duration)
    {
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            entryFlash.color = WithAlpha(Color.white, Mathf.Lerp(from, to, Ease(elapsed / duration)));
            yield return null;
        }
        entryFlash.color = WithAlpha(Color.white, to);
    }

    private void HideEntryFlash()
    {
        if (entryFlash == null) return;
        entryFlash.gameObject.SetActive(false);
        Destroy(entryFlash.gameObject);
        entryFlash = null;
    }

    private void CacheHullPose()
    {
        SpriteRenderer hull = boss.GetComponentInChildren<SpriteRenderer>();
        if (hull == null) return;
        hullRenderer = hull;
        hullNormalColor = hull.color;
        hullArt = hull.transform;
        hullPosition = hullArt.localPosition;
        hullRotation = hullArt.localRotation;
    }

    private void RestoreHullColor()
    {
        if (hullRenderer != null) hullRenderer.color = hullNormalColor;
    }

    private void OnBossHpChanged(int hp, int maximum)
    {
        if (damagedPhase || hp <= 0 || (long)hp * 2 > maximum) return;
        damagedPhase = true;
        if (hullRenderer != null) hullRenderer.color = hullNormalColor * new Color(1f, 0.30f, 0.34f);
        if (damageExplosionPrefab != null) StartCoroutine(PlayDamageExplosions());
    }

    private IEnumerator PlayDamageExplosions()
    {
        while (true)
        {
            Bounds hull = hullRenderer != null ? hullRenderer.bounds : bossCollider.bounds;
            Vector3 point = hull.center + new Vector3(
                Random.Range(-0.43f, 0.43f) * hull.size.x, Random.Range(-0.42f, 0.42f) * hull.size.y);
            var burst = Instantiate(damageExplosionPrefab, point, Quaternion.identity, hazards);
            burst.name = "공중 보스 장갑 폭발";
            burst.transform.localScale *= Random.Range(0.48f, 0.90f);
            var renderer = burst.GetComponentInChildren<SpriteRenderer>();
            if (renderer != null) renderer.sortingOrder = 29;
            Destroy(burst, 1.2f);
            yield return new WaitForSeconds(Random.Range(0.30f, 0.58f));
        }
    }

    private void ResetHullPose()
    {
        if (hullArt == null) return;
        hullArt.SetLocalPositionAndRotation(hullPosition, hullRotation);
    }

    private LineRenderer AddJet(string effectName, int sortingOrder, float width)
    {
        var line = new GameObject(effectName).AddComponent<LineRenderer>();
        line.transform.SetParent(hazards, false);
        effectsMaterial ??= new Material(Shader.Find("Sprites/Default"));
        line.sharedMaterial = effectsMaterial;
        line.sortingOrder = sortingOrder;
        line.positionCount = 3;
        line.widthCurve = new AnimationCurve(new Keyframe(0f, 0.65f), new Keyframe(0.16f, 1f), new Keyframe(1f, 0f));
        line.widthMultiplier = width;
        line.numCapVertices = 4;
        return line;
    }

    private void PresentBoost(LineRenderer glow, LineRenderer core, float progress)
    {
        Vector3 nozzle = layout.BoostNozzle.position;
        float power = Mathf.Lerp(0.63f, 1f, Mathf.Sin(progress * Mathf.PI));
        float wobble = Mathf.Sin(progress * 24f) * 0.16f;
        float length = 4.8f * power + wobble;
        SetJet(glow, nozzle, Vector3.right * length + Vector3.up * wobble, new Color(0.14f, 0.72f, 1f, 0.84f));
        SetJet(core, nozzle, Vector3.right * (length * 0.69f), new Color(0.87f, 0.98f, 1f, 0.98f));
        if (hullArt != null) hullArt.localRotation = hullRotation * Quaternion.Euler(0f, 0f, 5.5f * power);
    }

    private IEnumerator PlayPodIgnition(Vector3 mount)
    {
        LineRenderer jet = AddJet("미사일 포드 발사 화염", 31, 0.31f);
        SpriteRenderer flash = AddSprite("미사일 발사 섬광", square, hazards, 32);
        flash.transform.position = mount;
        flash.transform.rotation = Quaternion.Euler(0f, 0f, 45f);
        yield return Animate(0.17f, t =>
        {
            float strength = 1f - t;
            SetJet(jet, mount, Vector3.right * Mathf.Lerp(0.30f, 1.35f, Ease(t)),
                new Color(1f, 0.46f, 0.14f, strength));
            flash.transform.localScale = Vector3.one * Mathf.Lerp(0.14f, 0.49f, Ease(t));
            flash.color = WithAlpha(BeamCore, strength);
            if (hullArt != null)
            {
                float kick = Mathf.Sin(t * Mathf.PI);
                hullArt.localPosition = hullPosition + Vector3.left * (0.17f * kick);
                hullArt.localRotation = hullRotation * Quaternion.Euler(0f, 0f, 4f * kick);
            }
        });
        Destroy(jet.gameObject);
        RemoveEffect(flash);
        ResetHullPose();
    }

    private static void SetJet(LineRenderer jet, Vector3 nozzle, Vector3 plume, Color color)
    {
        jet.startColor = color;
        jet.endColor = WithAlpha(color, 0f);
        jet.SetPosition(0, nozzle);
        jet.SetPosition(1, nozzle + plume * 0.33f);
        jet.SetPosition(2, nozzle + plume);
    }
}
