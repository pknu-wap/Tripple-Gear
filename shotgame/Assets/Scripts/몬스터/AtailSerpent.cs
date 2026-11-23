using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class AtailSerpent : MonoBehaviour
{
    [SerializeField] private SpriteRenderer drill;
    [SerializeField] private SpriteRenderer[] bodyParts;
    [SerializeField, Min(0f)] private float headOverlap = 3.09f;
    [SerializeField, Min(0f)] private float jointOverlap = 0.87f;
    [SerializeField] private Shader waterContactShader;

    private static readonly int WaterlineY = Shader.PropertyToID("_WaterlineY");
    private const float VisibleWaterDepth = 0.85f;

    private struct TrailPoint
    {
        public Vector2 Position;
        public float Distance;
    }

    private readonly List<TrailPoint> trail = new List<TrailPoint>();
    private Collider2D drillHitbox;
    private Collider2D[] bodyHitboxes;
    private Vector2[] jointSockets;
    private float[] jointLengths;
    private Material waterContactMaterial;
    private Material drillWaterContactMaterial;
    private float headLength;
    private float waterY;
    private float travelled;
    private bool surfaced;
    private bool attackHitboxesEnabled = true;
    private SpriteRenderer hatchSegment;
    private SpriteRenderer hatchDoor;
    private SpriteRenderer hatchOpening;
    private Vector3 hatchCenter;

    public float Span { get; private set; }
    public Vector2 HatchExit => hatchOpening.transform.position;
    public int HatchSortingOrder => hatchDoor.sortingOrder;
    internal Vector2 NosePosition => drill.transform.position;
    internal bool IsSurfaced => surfaced;

    internal void SetAttackHitboxes(bool enabled)
    {
        attackHitboxesEnabled = enabled;
        if (enabled) return;
        if (drillHitbox != null) drillHitbox.enabled = false;
        foreach (Collider2D hitbox in bodyHitboxes)
            if (hitbox != null) hitbox.enabled = false;
    }

    private void Awake()
    {
        drillHitbox = drill.GetComponent<Collider2D>();
        headLength = drill.sprite.bounds.max.x * Mathf.Abs(drill.transform.lossyScale.x);
        waterContactMaterial = new Material(waterContactShader);
        drillWaterContactMaterial = new Material(waterContactMaterial) { mainTexture = drill.sprite.texture };
        UseWaterContact(drill, drillWaterContactMaterial);
        bodyHitboxes = new Collider2D[bodyParts.Length];
        jointSockets = new Vector2[bodyParts.Length];
        jointLengths = new float[bodyParts.Length];
        Span = headLength;
        for (int i = 0; i < bodyParts.Length; i++)
        {
            SpriteRenderer part = bodyParts[i];
            float scale = Mathf.Abs(part.transform.lossyScale.x);
            Bounds bounds = part.sprite.bounds;
            float inset = Mathf.Min(jointOverlap * 0.5f / scale, bounds.extents.x * 0.8f);
            jointSockets[i] = new Vector2(bounds.min.x + inset, bounds.max.x - inset);
            jointLengths[i] = (jointSockets[i].y - jointSockets[i].x) * scale;
            bodyHitboxes[i] = part.GetComponent<Collider2D>();
            Span += jointLengths[i];
            UseWaterContact(part, waterContactMaterial);
            if (hatchDoor == null && part.sprite.name == "BodyArmor") CreateHatch(part);
        }
        Hide();
    }

    private static void UseWaterContact(SpriteRenderer part, Material material)
    {
        Color tint = part.color;
        tint.a = 1f;
        part.color = tint;
        part.sharedMaterial = material;
    }

    private void CreateHatch(SpriteRenderer armor)
    {
        Sprite source = armor.sprite;
        hatchSegment = armor;
        // Use the X-braced door and the dark grille painted in the BodyArmor sprite.
        Rect door = ArmorRegion(source, new Rect(301f, 53f, 110f, 96f));
        Rect opening = ArmorRegion(source, new Rect(273f, 225f, 126f, 38f));
        hatchCenter = (door.center - source.rect.position - source.pivot) / source.pixelsPerUnit;
        hatchOpening = CreateHatchRenderer("아테일 발사구", armor, opening);
        hatchOpening.transform.localScale = new Vector3(door.width / opening.width, door.height / opening.height, 1f);
        hatchOpening.transform.localPosition = hatchCenter;
        hatchOpening.sortingOrder = armor.sortingOrder + 1;
        hatchOpening.color = new Color(1f, 0.75f, 0.48f);
        hatchDoor = CreateHatchRenderer("아테일 발사구 덮개", armor, door);
        hatchDoor.sortingOrder = armor.sortingOrder + 2;
    }

    private static Rect ArmorRegion(Sprite armor, Rect pixels)
    {
        Vector2 scale = new Vector2(armor.rect.width / 685f, armor.rect.height / 294f);
        return new Rect(armor.rect.position + Vector2.Scale(pixels.position, scale), Vector2.Scale(pixels.size, scale));
    }

    private static SpriteRenderer CreateHatchRenderer(string objectName, SpriteRenderer armor, Rect region)
    {
        var renderer = new GameObject(objectName).AddComponent<SpriteRenderer>();
        renderer.transform.SetParent(armor.transform, false);
        renderer.sprite = Sprite.Create(armor.sprite.texture, region, new Vector2(0.5f, 0.5f),
            armor.sprite.pixelsPerUnit, 0, SpriteMeshType.FullRect);
        renderer.sharedMaterial = armor.sharedMaterial;
        renderer.sortingLayerID = armor.sortingLayerID;
        return renderer;
    }

    private void OnDestroy()
    {
        if (hatchDoor != null) Destroy(hatchDoor.sprite);
        if (hatchOpening != null) Destroy(hatchOpening.sprite);
        if (waterContactMaterial != null) Destroy(waterContactMaterial);
        if (drillWaterContactMaterial != null) Destroy(drillWaterContactMaterial);
    }

    public void SetWaterline(float value)
    {
        waterY = value;
        waterContactMaterial.SetFloat(WaterlineY, value);
        drillWaterContactMaterial.SetFloat(WaterlineY, value);
    }

    public void BeginPath(Vector2 position, Vector2 direction)
    {
        trail.Clear();
        trail.Add(new TrailPoint { Position = position - direction.normalized * (Span + 2f), Distance = -Span - 2f });
        trail.Add(new TrailPoint { Position = position, Distance = 0f });
        travelled = 0f;
        surfaced = true;
        PlaceParts(position, direction.normalized);
    }

    public void MoveNose(Vector2 position) => MoveAlongTrail(position, Vector2.zero, 0f);

    internal void MoveAlongTrail(Vector2 position, Vector2 straightDirection, float straighten)
    {
        if (!surfaced) return;
        TrailPoint last = trail[trail.Count - 1];
        float distance = Vector2.Distance(last.Position, position);
        if (distance < 0.0001f) return;
        travelled += distance;
        trail.Add(new TrailPoint { Position = position, Distance = travelled });

        int remove = 0;
        while (remove + 2 < trail.Count && trail[remove + 1].Distance < travelled - Span - 3f) remove++;
        if (remove > 0) trail.RemoveRange(0, remove);
        Vector2 direction = (position - Sample(travelled - 2.1f)).normalized;
        PlaceParts(position, direction, straightDirection, straighten);
    }

    public void SetHatchOpening(float amount)
    {
        float open = Mathf.SmoothStep(0f, 1f, amount);
        hatchOpening.enabled = open > 0.01f && hatchSegment.enabled;
        hatchDoor.enabled = hatchOpening.enabled;
        float doorHeight = hatchDoor.sprite.bounds.size.y;
        hatchDoor.transform.SetLocalPositionAndRotation(hatchCenter + Vector3.up * (doorHeight * 0.95f * open),
            Quaternion.Euler(0f, 0f, 21f * open));
    }

    public void CloseHatch()
    {
        hatchOpening.enabled = false;
        hatchDoor.enabled = false;
    }

    internal bool TryGetDamageBurstPosition(out Vector2 position)
    {
        position = default;
        if (!surfaced) return false;

        int partCount = bodyParts.Length + 1;
        int startingPart = Random.Range(0, partCount);
        for (int i = 0; i < partCount; i++)
        {
            int index = (startingPart + i) % partCount;
            SpriteRenderer part = index == bodyParts.Length ? drill : bodyParts[index];
            if (!part.enabled) continue;
            Bounds bounds = part.bounds;
            float low = Mathf.Max(bounds.center.y - bounds.extents.y * 0.4f, waterY + 0.25f);
            float high = bounds.center.y + bounds.extents.y * 0.45f;
            if (low >= high) continue;

            position = new Vector2(bounds.center.x + Random.Range(-0.46f, 0.46f) * bounds.extents.x,
                Random.Range(low, high));
            return true;
        }
        return false;
    }

    public void Hide()
    {
        surfaced = false;
        CloseHatch();
        drill.enabled = false;
        if (drillHitbox != null) drillHitbox.enabled = false;
        for (int i = 0; i < bodyParts.Length; i++)
        {
            bodyParts[i].enabled = false;
            if (bodyHitboxes[i] != null) bodyHitboxes[i].enabled = false;
        }
    }

    internal void PlaceStraight(Vector2 nose, Vector2 direction) => PlaceParts(nose, direction, direction, 1f);

    private void PlaceParts(Vector2 nose, Vector2 direction, Vector2 straightDirection = default, float straighten = 0f)
    {
        direction = Vector2.Lerp(direction, straightDirection, straighten).normalized;
        SetPart(drill, drillHitbox, nose, direction);
        float offset = Mathf.Max(0.05f, headLength - headOverlap);
        Vector2 joint = nose - direction * offset;
        for (int i = 0; i < bodyParts.Length; i++)
        {
            SpriteRenderer part = bodyParts[i];
            offset += jointLengths[i];
            Vector2 toFront = joint - Sample(travelled - offset);
            if (toFront.sqrMagnitude > 0.0001f)
                direction = Vector2.Lerp(toFront.normalized, straightDirection, straighten).normalized;
            float frontSocket = jointSockets[i].x * Mathf.Abs(part.transform.lossyScale.x);
            SetPart(part, bodyHitboxes[i], joint + direction * frontSocket, direction);
            // The next segment starts at this sprite's rear socket, even on tight turns.
            joint -= direction * jointLengths[i];
        }
    }

    private void SetPart(SpriteRenderer part, Collider2D hitbox, Vector2 position, Vector2 direction)
    {
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 180f;
        part.transform.SetPositionAndRotation(new Vector3(position.x, position.y, transform.position.z),
            Quaternion.Euler(0f, 0f, angle));
        part.enabled = part.bounds.max.y > waterY - VisibleWaterDepth;
        if (hitbox != null) hitbox.enabled = attackHitboxesEnabled && part.bounds.max.y > waterY + 0.15f;
    }

    private Vector2 Sample(float distance)
    {
        int low = 0;
        int high = trail.Count - 1;
        while (high - low > 1)
        {
            int mid = (low + high) / 2;
            if (trail[mid].Distance <= distance) low = mid;
            else high = mid;
        }
        TrailPoint a = trail[low];
        TrailPoint b = trail[high];
        return Vector2.Lerp(a.Position, b.Position, Mathf.InverseLerp(a.Distance, b.Distance, distance));
    }
}
