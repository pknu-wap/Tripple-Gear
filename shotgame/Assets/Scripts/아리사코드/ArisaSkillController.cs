using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[DefaultExecutionOrder(120)]
public sealed class ArisaSkillController : MonoBehaviour
{
    private const float MinDirectionSqrMagnitude = 0.0001f;
#if UNITY_EDITOR
    private const string DefaultSkillPath = "Assets/SO/스킬/대쉬스킬SO 1.asset";
#endif

    [SerializeField, KoreanLabel("스킬 데이터")] private SkillSO 스킬데이터;
    [SerializeField, KoreanLabel("대상 카메라")] private Camera 대상카메라;
    [SerializeField, KoreanLabel("이동 컴포넌트")] private ArisaHorizontalMovement 이동컴포넌트;

    private SkillRuntime runtime;
    private ArisaInvincibility invincibility;
    private SkillSO currentSkillData;
    private int remainingUseCount;
    private float cooldownCompleteTime;
    private bool skillConsumed;

#if ENABLE_INPUT_SYSTEM
    private InputAction skillAction;
    private InputAction pointerPositionAction;
#endif

    public SkillSO SkillData => 스킬데이터;
    public int RemainingUseCount => remainingUseCount;
    public float CooldownRemaining => IsCoolingDown ? Mathf.Max(0f, cooldownCompleteTime - Time.time) : 0f;
    public bool IsCoolingDown => 스킬데이터 != null && Time.time < cooldownCompleteTime;

    private void Reset()
    {
        FindReferences();
        RefreshSkillState();
    }

    private void Awake()
    {
        FindReferences();
        RefreshSkillState();
    }

    private void OnEnable()
    {
        FindReferences();
        RefreshSkillState();

#if ENABLE_INPUT_SYSTEM
        skillAction ??= new InputAction("Skill", InputActionType.Button, "<Mouse>/rightButton");
        pointerPositionAction ??= new InputAction("PointerPosition", InputActionType.Value, "<Pointer>/position");
        skillAction.Enable();
        pointerPositionAction.Enable();
#endif
    }

    private void OnDisable()
    {
        runtime?.StopPlaying();

#if ENABLE_INPUT_SYSTEM
        skillAction?.Disable();
        pointerPositionAction?.Disable();
#endif
    }

    private void Update()
    {
        RefreshSkillState();

        if (!ReadSkillPressed())
        {
            return;
        }

        TryUseSkill();
    }

    private bool TryUseSkill()
    {
        if (스킬데이터 == null || !스킬데이터.HasLogic || runtime == null || runtime.IsPlaying)
        {
            return false;
        }

        RefreshSkillState();
        if (remainingUseCount <= 0)
        {
            RemoveSkillFromArisa();
            return false;
        }

        if (IsCoolingDown)
        {
            return false;
        }

        Camera cameraToUse = GetCamera();
        Vector3 mouseWorldPosition = transform.position;
        Vector2 direction = Vector2.zero;
        Vector2 mouseScreenPosition = default;
        bool hasMouseWorldPosition = cameraToUse != null
            && TryReadMouseScreenPosition(out mouseScreenPosition);

        if (hasMouseWorldPosition)
        {
            mouseWorldPosition = GetMouseWorldPosition(cameraToUse, mouseScreenPosition, transform.position.z);
            direction = mouseWorldPosition - transform.position;
        }

        if (스킬데이터.RequiresAimDirection && (!hasMouseWorldPosition || direction.sqrMagnitude <= MinDirectionSqrMagnitude))
        {
            return false;
        }

        SkillSO skillToUse = 스킬데이터;
        ConsumeSkillUse(skillToUse);

        SkillContext context = new SkillContext(
            transform,
            mouseWorldPosition,
            direction,
            cameraToUse,
            this,
            이동컴포넌트,
            invincibility
        );

        bool shouldMakeInvincible = skillToUse.IsInvincibleWhileActive && invincibility != null;
        if (shouldMakeInvincible)
        {
            invincibility.SetSkillInvincible(true);
        }

        runtime.Play(
            skillToUse.Execute(context),
            () =>
            {
                if (shouldMakeInvincible && invincibility != null)
                {
                    invincibility.SetSkillInvincible(false);
                }
            }
        );

        return true;
    }

    private void FindReferences()
    {
        if (스킬데이터 == null)
        {
            스킬데이터 = skillConsumed ? null : LoadDefaultSkill();
        }

        if (이동컴포넌트 == null)
        {
            이동컴포넌트 = GetComponent<ArisaHorizontalMovement>();
        }

        if (runtime == null)
        {
            runtime = GetComponent<SkillRuntime>();
            if (runtime == null)
            {
                runtime = gameObject.AddComponent<SkillRuntime>();
            }
        }

        if (invincibility == null)
        {
            invincibility = GetComponent<ArisaInvincibility>();
            if (invincibility == null)
            {
                invincibility = gameObject.AddComponent<ArisaInvincibility>();
            }
        }
    }

    private void RefreshSkillState()
    {
        if (스킬데이터 == null)
        {
            currentSkillData = null;
            remainingUseCount = 0;
            cooldownCompleteTime = 0f;
            return;
        }

        if (스킬데이터 != currentSkillData)
        {
            currentSkillData = 스킬데이터;
            remainingUseCount = 스킬데이터.MaxUseCount;
            cooldownCompleteTime = 0f;
            skillConsumed = false;
        }
    }

    private void ConsumeSkillUse(SkillSO skillToUse)
    {
        remainingUseCount = Mathf.Max(0, remainingUseCount - 1);
        if (remainingUseCount <= 0)
        {
            RemoveSkillFromArisa();
            return;
        }

        cooldownCompleteTime = Time.time + skillToUse.Cooldown;
    }

    private void RemoveSkillFromArisa()
    {
        skillConsumed = true;
        스킬데이터 = null;
        currentSkillData = null;
        remainingUseCount = 0;
        cooldownCompleteTime = 0f;
    }

    private Camera GetCamera()
    {
        if (대상카메라 != null)
        {
            return 대상카메라;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            return mainCamera;
        }

        Camera[] cameras = Camera.allCameras;
        return cameras.Length > 0 ? cameras[0] : null;
    }

    private static Vector3 GetMouseWorldPosition(Camera cameraToUse, Vector2 screenPosition, float worldZ)
    {
        Ray mouseRay = cameraToUse.ScreenPointToRay(screenPosition);
        Plane worldPlane = new Plane(Vector3.forward, new Vector3(0f, 0f, worldZ));
        if (worldPlane.Raycast(mouseRay, out float distance))
        {
            return mouseRay.GetPoint(distance);
        }

        float cameraDistance = Mathf.Abs(worldZ - cameraToUse.transform.position.z);
        return cameraToUse.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, cameraDistance));
    }

    private SkillSO LoadDefaultSkill()
    {
#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<SkillSO>(DefaultSkillPath);
#else
        return null;
#endif
    }

    private bool ReadSkillPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (skillAction != null && skillAction.WasPressedThisFrame())
        {
            return true;
        }

        if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame)
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButtonDown(1);
#else
        return false;
#endif
    }

    private bool TryReadMouseScreenPosition(out Vector2 screenPosition)
    {
#if ENABLE_INPUT_SYSTEM
        if (pointerPositionAction != null)
        {
            screenPosition = pointerPositionAction.ReadValue<Vector2>();
            return true;
        }

        if (Pointer.current != null)
        {
            screenPosition = Pointer.current.position.ReadValue();
            return true;
        }

        if (Mouse.current != null)
        {
            screenPosition = Mouse.current.position.ReadValue();
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        screenPosition = Input.mousePosition;
        return true;
#else
        screenPosition = default;
        return false;
#endif
    }
}
