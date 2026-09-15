using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[Serializable]
public sealed class DashSkillLogic : SkillLogic
{
    [SerializeField, KoreanLabel("대쉬 거리"), Min(0f)] private float 대쉬거리 = 5f;
    [SerializeField, KoreanLabel("대쉬 속도"), Min(0.01f)] private float 대쉬속도 = 18f;
    [FormerlySerializedAs("초록화면색")]
    [SerializeField, KoreanLabel("배경 변경 색")] private Color 배경변경색 = Color.green;
    [SerializeField, KoreanLabel("배경 레이어 이름")] private string 배경레이어이름 = "background";
    [SerializeField, KoreanLabel("주변 애니메이션 속도 배율"), Range(0f, 1f)] private float 주변애니메이션속도배율 = 0.25f;
    [SerializeField, KoreanLabel("주변 애니메이션 느리게")] private bool 주변애니메이션느리게 = true;
    [SerializeField, KoreanLabel("대쉬 중 이동 입력 끄기")] private bool 대쉬중이동입력끄기 = true;
    [SerializeField, KoreanLabel("잔상 남기기")] private bool 잔상남기기 = true;
    [SerializeField, KoreanLabel("잔상 생성 간격"), Min(0.01f)] private float 잔상생성간격 = 0.04f;
    [SerializeField, KoreanLabel("잔상 색")] private Color 잔상색 = new Color(0f, 1f, 0.45f, 0.35f);
    [SerializeField, KoreanLabel("잔상 정렬 순서 보정")] private int 잔상정렬순서보정 = -1;

    public override IEnumerator Execute(SkillContext context)
    {
        if (context.Owner == null || context.AimDirection.sqrMagnitude <= 0.0001f)
        {
            yield break;
        }

        List<AnimatorSpeedSnapshot> animatorSnapshots = 주변애니메이션느리게
            ? SlowOtherAnimators(context.Owner)
            : new List<AnimatorSpeedSnapshot>();
        List<SpriteRendererColorSnapshot> backgroundSnapshots = ChangeBackgroundColors();
        List<GameObject> afterimageObjects = new List<GameObject>();

        bool restoreMovement = 대쉬중이동입력끄기
            && context.Movement != null
            && context.Movement.enabled;

        if (restoreMovement)
        {
            context.Movement.enabled = false;
        }

        try
        {
            Vector2 direction = context.AimDirection.normalized;
            float remainingDistance = Mathf.Max(0f, 대쉬거리);
            float speed = Mathf.Max(0.01f, 대쉬속도);
            float nextAfterimageTime = 0f;

            while (context.Owner != null && remainingDistance > 0f)
            {
                if (잔상남기기 && Time.time >= nextAfterimageTime)
                {
                    CreateAfterimage(context.Owner, afterimageObjects);
                    nextAfterimageTime = Time.time + Mathf.Max(0.01f, 잔상생성간격);
                }

                float moveDistance = Mathf.Min(remainingDistance, speed * Time.deltaTime);
                context.Owner.position += new Vector3(direction.x, direction.y, 0f) * moveDistance;
                remainingDistance -= moveDistance;
                yield return null;
            }
        }
        finally
        {
            RestoreBackgroundColors(backgroundSnapshots);
            RestoreAnimatorSpeeds(animatorSnapshots);
            DestroyAfterimages(afterimageObjects);

            if (restoreMovement && context.Movement != null)
            {
                context.Movement.enabled = true;
            }
        }
    }

    public override void OnValidate()
    {
        대쉬거리 = Mathf.Max(0f, 대쉬거리);
        대쉬속도 = Mathf.Max(0.01f, 대쉬속도);
        주변애니메이션속도배율 = Mathf.Clamp01(주변애니메이션속도배율);
        잔상생성간격 = Mathf.Max(0.01f, 잔상생성간격);
    }

    private List<AnimatorSpeedSnapshot> SlowOtherAnimators(Transform owner)
    {
        List<AnimatorSpeedSnapshot> snapshots = new List<AnimatorSpeedSnapshot>();
        float speedMultiplier = Mathf.Clamp01(주변애니메이션속도배율);

#if UNITY_2023_1_OR_NEWER || UNITY_6000_0_OR_NEWER
        Animator[] animators = UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
        Animator[] animators = UnityEngine.Object.FindObjectsOfType<Animator>();
#endif

        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (animator == null || ShouldIgnoreAnimator(animator, owner))
            {
                continue;
            }

            snapshots.Add(new AnimatorSpeedSnapshot(animator, animator.speed));
            animator.speed *= speedMultiplier;
        }

        return snapshots;
    }

    private List<SpriteRendererColorSnapshot> ChangeBackgroundColors()
    {
        List<SpriteRendererColorSnapshot> snapshots = new List<SpriteRendererColorSnapshot>();
        int backgroundLayer = GetBackgroundLayer();
        if (backgroundLayer < 0)
        {
            return snapshots;
        }

#if UNITY_2023_1_OR_NEWER || UNITY_6000_0_OR_NEWER
        SpriteRenderer[] spriteRenderers = UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#else
        SpriteRenderer[] spriteRenderers = UnityEngine.Object.FindObjectsOfType<SpriteRenderer>();
#endif

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = spriteRenderers[i];
            if (spriteRenderer == null || spriteRenderer.gameObject.layer != backgroundLayer)
            {
                continue;
            }

            snapshots.Add(new SpriteRendererColorSnapshot(spriteRenderer, spriteRenderer.color));
            spriteRenderer.color = 배경변경색;
        }

        return snapshots;
    }

    private int GetBackgroundLayer()
    {
        if (string.IsNullOrWhiteSpace(배경레이어이름))
        {
            return LayerMask.NameToLayer("background");
        }

        int layer = LayerMask.NameToLayer(배경레이어이름);
        if (layer >= 0)
        {
            return layer;
        }

        return LayerMask.NameToLayer("Background");
    }

    private static void RestoreBackgroundColors(List<SpriteRendererColorSnapshot> snapshots)
    {
        if (snapshots == null)
        {
            return;
        }

        for (int i = 0; i < snapshots.Count; i++)
        {
            snapshots[i].Restore();
        }
    }

    private void CreateAfterimage(Transform owner, List<GameObject> afterimageObjects)
    {
        if (owner == null || afterimageObjects == null)
        {
            return;
        }

        SpriteRenderer[] sourceRenderers = owner.GetComponentsInChildren<SpriteRenderer>(false);
        GameObject root = new GameObject(owner.name + " Dash Afterimage");
        bool hasVisibleSprite = false;

        for (int i = 0; i < sourceRenderers.Length; i++)
        {
            SpriteRenderer source = sourceRenderers[i];
            if (source == null || !source.enabled || source.sprite == null)
            {
                continue;
            }

            GameObject copyObject = new GameObject(source.gameObject.name);
            copyObject.transform.SetParent(root.transform, false);
            copyObject.transform.position = source.transform.position;
            copyObject.transform.rotation = source.transform.rotation;
            copyObject.transform.localScale = source.transform.lossyScale;

            SpriteRenderer copy = copyObject.AddComponent<SpriteRenderer>();
            copy.sprite = source.sprite;
            copy.color = GetAfterimageColor(source.color);
            copy.flipX = source.flipX;
            copy.flipY = source.flipY;
            copy.drawMode = source.drawMode;
            copy.size = source.size;
            copy.sortingLayerID = source.sortingLayerID;
            copy.sortingOrder = source.sortingOrder + 잔상정렬순서보정;
            copy.maskInteraction = source.maskInteraction;
            hasVisibleSprite = true;
        }

        if (!hasVisibleSprite)
        {
            UnityEngine.Object.Destroy(root);
            return;
        }

        afterimageObjects.Add(root);
    }

    private Color GetAfterimageColor(Color sourceColor)
    {
        Color color = 잔상색;
        color.a *= sourceColor.a;
        return color;
    }

    private static void DestroyAfterimages(List<GameObject> afterimageObjects)
    {
        if (afterimageObjects == null)
        {
            return;
        }

        for (int i = 0; i < afterimageObjects.Count; i++)
        {
            if (afterimageObjects[i] != null)
            {
                UnityEngine.Object.Destroy(afterimageObjects[i]);
            }
        }

        afterimageObjects.Clear();
    }

    private static bool ShouldIgnoreAnimator(Animator animator, Transform owner)
    {
        return (owner != null && animator.transform.IsChildOf(owner))
            || animator.GetComponentInParent<ArisaHorizontalMovement>() != null;
    }

    private static void RestoreAnimatorSpeeds(List<AnimatorSpeedSnapshot> snapshots)
    {
        if (snapshots == null)
        {
            return;
        }

        for (int i = 0; i < snapshots.Count; i++)
        {
            snapshots[i].Restore();
        }
    }

    private readonly struct AnimatorSpeedSnapshot
    {
        public AnimatorSpeedSnapshot(Animator animator, float speed)
        {
            Animator = animator;
            Speed = speed;
        }

        private Animator Animator { get; }
        private float Speed { get; }

        public void Restore()
        {
            if (Animator != null)
            {
                Animator.speed = Speed;
            }
        }
    }

    private readonly struct SpriteRendererColorSnapshot
    {
        public SpriteRendererColorSnapshot(SpriteRenderer spriteRenderer, Color color)
        {
            SpriteRenderer = spriteRenderer;
            Color = color;
        }

        private SpriteRenderer SpriteRenderer { get; }
        private Color Color { get; }

        public void Restore()
        {
            if (SpriteRenderer != null)
            {
                SpriteRenderer.color = Color;
            }
        }
    }
}
