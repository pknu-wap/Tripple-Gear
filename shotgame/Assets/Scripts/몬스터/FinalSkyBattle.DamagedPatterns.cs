using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class FinalSkyBattle
{
    private const float ElectricWarningSeconds = 3f;
    private const float ElectricHitRadius = 0.18f;
    private static readonly Color ElectricWarning = new Color(0.70f, 0.45f, 1f);
    private static readonly Color ElectricStrike = new Color(0.76f, 0.94f, 1f);
    private Sprite energyBallSprite;
    private Texture2D energyBallTexture;

    private IEnumerator ElectricField()
    {
        GameplaySfx.Play(GameplaySfx.Cue.Warning, .53f);
        var branches = new List<LineRenderer>();
        Rect arena = Arena;
        Vector3 source = layout.LaserMuzzle.position;
        for (int i = 0; i < 4; i++)
        {
            float lane = (i + Random.Range(0.22f, 0.78f)) / 4f;
            var main = AddElectricBranch(source, At(Random.Range(0.01f, 0.19f), lane), 7);
            branches.Add(main);
            Vector3 forkStart = main.GetPosition(Random.Range(2, 5));
            Vector3 forkEnd = forkStart + new Vector3(
                -arena.width * Random.Range(0.08f, 0.17f), arena.height * Random.Range(0.10f, 0.19f) * (i % 2 == 0 ? 1f : -1f));
            forkEnd.y = Mathf.Clamp(forkEnd.y, arena.yMin + 0.1f, arena.yMax - 0.1f);
            branches.Add(AddElectricBranch(forkStart, forkEnd, 4));
        }

        yield return Animate(ElectricWarningSeconds, t =>
        {
            Color warning = WithAlpha(ElectricWarning, Mathf.Lerp(0.30f, 0.78f, Mathf.PingPong(t * 8f, 1f)));
            foreach (LineRenderer branch in branches) branch.startColor = branch.endColor = warning;
        });

        if (playerCollider != null && !player.IsDead && TouchesElectricField(branches, playerCollider.bounds))
            player.TakeDamage(1, source);
        GameplaySfx.Play(GameplaySfx.Cue.Electric, .63f);

        yield return Animate(0.32f, t =>
        {
            foreach (LineRenderer branch in branches)
            {
                branch.startColor = branch.endColor = WithAlpha(ElectricStrike, 1f - t);
                branch.widthMultiplier = Mathf.Lerp(ElectricHitRadius * 2f, 0.07f, t);
            }
        });
        foreach (LineRenderer branch in branches) Destroy(branch.gameObject);
    }

    private LineRenderer AddElectricBranch(Vector3 start, Vector3 end, int points)
    {
        LineRenderer branch = AddJet("전기장 갈래 예고", 30, 0.075f);
        branch.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
        branch.positionCount = points;
        branch.numCornerVertices = 2;
        branch.SetPosition(0, start);
        for (int i = 1; i < points - 1; i++)
        {
            Vector3 point = Vector3.Lerp(start, end, (float)i / (points - 1));
            point.y += Random.Range(-0.52f, 0.52f);
            branch.SetPosition(i, point);
        }
        branch.SetPosition(points - 1, end);
        return branch;
    }

    private static bool TouchesElectricField(List<LineRenderer> branches, Bounds playerBounds)
    {
        playerBounds.Expand(ElectricHitRadius * 2f);
        foreach (LineRenderer branch in branches)
        {
            for (int i = 1; i < branch.positionCount; i++)
            {
                Vector3 start = branch.GetPosition(i - 1);
                Vector3 delta = branch.GetPosition(i) - start;
                start.z = playerBounds.center.z;
                delta.z = 0f;
                if (playerBounds.IntersectRay(new Ray(start, delta.normalized), out float distance)
                    && distance <= delta.magnitude)
                    return true;
            }
        }
        return false;
    }

    private IEnumerator EnergyBallVolley()
    {
        int throws = Random.Range(3, 6);
        Sprite ballArt = GetEnergyBallSprite();
        for (int i = 0; i < throws; i++)
        {
            SpriteRenderer ball = AddSprite("에너지 볼 충전", ballArt, hazards, 32);
            Vector3 origin = layout.LaserMuzzle.position + Vector3.left * 0.45f;
            ball.transform.position = origin;
            yield return Animate(0.43f, t =>
            {
                ball.transform.localScale = Vector3.one * Mathf.Lerp(0.18f, 0.95f, Ease(t));
                ball.color = WithAlpha(Color.white, Mathf.Lerp(0.36f, 1f, t));
            });

            Vector2 target = playerCollider != null ? playerCollider.bounds.center : player.transform.position;
            ball.name = "에너지 볼 투사체";
            GameplaySfx.Play(GameplaySfx.Cue.Energy, .48f);
            ball.gameObject.AddComponent<EnemyProjectile2D>().Launch(
                target - (Vector2)origin, 12f, 1, 0, 0.31f, 4f);
            yield return new WaitForSeconds(0.22f);
        }
        yield return new WaitForSeconds(0.50f);
    }

    private Sprite GetEnergyBallSprite()
    {
        if (energyBallSprite != null) return energyBallSprite;
        const int size = 48;
        energyBallTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float radius = new Vector2((x + 0.5f) * 2f / size - 1f, (y + 0.5f) * 2f / size - 1f).magnitude;
                Color color = Color.Lerp(new Color(0.95f, 0.99f, 1f), new Color(0.20f, 0.47f, 1f), radius);
                color.a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.42f, 1f, radius));
                pixels[y * size + x] = color;
            }
        }
        energyBallTexture.SetPixels(pixels);
        energyBallTexture.Apply(false, true);
        energyBallSprite = Sprite.Create(energyBallTexture, new Rect(0f, 0f, size, size), Vector2.one * 0.5f, size);
        return energyBallSprite;
    }
}
