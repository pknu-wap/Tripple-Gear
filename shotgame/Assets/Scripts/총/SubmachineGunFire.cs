using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public sealed class SubmachineGunFire : MonoBehaviour
{
    private const float MinDirectionSqrMagnitude = 0.0001f;
    private const float MinAttackSpeed = 0.1f;
    private const int MinBulletCount = 1;
    private const float ReloadAnimationPeakProgress = 0.5f;
#if UNITY_EDITOR
    private const string DefaultGunDataPath = "Assets/SO/총/기관단총SO.asset";
#endif

    [SerializeField, KoreanLabel("총 데이터")] private GunSO gunData;
    [SerializeField, KoreanLabel("총구")] private Transform muzzle;
    [SerializeField, KoreanLabel("조준 그래픽")] private Transform aimGraphic;
    [SerializeField, KoreanLabel("대상 카메라")] private Camera targetCamera;
    [SerializeField, KoreanLabel("왼쪽 발사 총구 위치 보정")] private Vector2 leftMuzzleLocalOffset = new Vector2(0.2f, -0.11f);
    [SerializeField, KoreanLabel("왼쪽 발사 판정 여유"), Min(0f)] private float leftFireDeadZone = 0.05f;
    [SerializeField, KoreanLabel("기본 총구 위치 보정")] private Vector2 fallbackMuzzleLocalOffset = new Vector2(1.4f, 0.28f);
    [SerializeField, KoreanLabel("탄창 애니메이션 부모")] private Transform reloadAnimationParent;
    [SerializeField, KoreanLabel("탄창 애니메이션 위치")] private Vector2 reloadAnimationLocalOffset = Vector2.zero;
    [SerializeField, KoreanLabel("탄창 상승 높이"), Min(0f)] private float reloadAnimationRiseHeight = 1f;
    [SerializeField, KoreanLabel("아리사 장전 렌더러")] private SpriteRenderer arisaReloadSpriteRenderer;
    [SerializeField, KoreanLabel("아리사 장전 위치 보정")] private Vector2 arisaReloadLocalOffset = Vector2.zero;
    [SerializeField, KoreanLabel("아리사 장전 스프라이트")] private Sprite[] arisaReloadSprites;
    [SerializeField, KoreanLabel("아리사 장전 프레임 속도"), Min(0f)] private float arisaReloadFrameRate = 8f;

    private float nextShotTime;
    private int currentBulletCount;
    private bool isReloading;
    private bool isWaitingBetweenSequentialReloads;
    private float reloadStartedTime;
    private float reloadCompleteTime;
    private GameObject reloadAnimationObject;
    private Sprite spriteBeforeArisaReload;
    private Vector3 arisaReloadBaseLocalPosition;
    private Vector3 appliedArisaReloadLocalOffset;
    private bool hasSpriteBeforeArisaReload;
    private bool hasAppliedArisaReloadPositionOffset;
    private ArisaMouseAim arisaAim;
#if ENABLE_INPUT_SYSTEM
    private InputAction fireAction;
    private InputAction pointerPositionAction;
    private InputAction reloadAction;
#endif

    public bool IsReloading => isReloading;

    private void Reset()
    {
        FindReferences();
    }

    private void Awake()
    {
        FindReferences();
        FillBulletCount();
    }

    private void OnEnable()
    {
        FindReferences();
        FillBulletCount();

#if ENABLE_INPUT_SYSTEM
        fireAction ??= new InputAction("Fire", InputActionType.Button, "<Pointer>/press");
        pointerPositionAction ??= new InputAction("PointerPosition", InputActionType.Value, "<Pointer>/position");
        reloadAction ??= new InputAction("Reload", InputActionType.Button, "<Keyboard>/r");
        fireAction.Enable();
        pointerPositionAction.Enable();
        reloadAction.Enable();
#endif
    }

    private void OnDisable()
    {
        nextShotTime = 0f;
        isReloading = false;
        isWaitingBetweenSequentialReloads = false;
        reloadStartedTime = 0f;
        reloadCompleteTime = 0f;
        StopArisaReloadAnimation();
        HideReloadAnimation();

#if ENABLE_INPUT_SYSTEM
        fireAction?.Disable();
        pointerPositionAction?.Disable();
        reloadAction?.Disable();
#endif
    }

    private void LateUpdate()
    {
        UpdateReload();

        if (ReadReloadPressed())
        {
            BeginReload();
        }

        if (!ReadFireHeld())
        {
            if (!isReloading)
            {
                nextShotTime = 0f;
            }

            return;
        }

        if (Time.time < nextShotTime || !CanCreateProjectile())
        {
            return;
        }

        if (isReloading && !CanFireWhileReloading())
        {
            return;
        }

        if (currentBulletCount <= 0)
        {
            BeginReload();
            return;
        }

        Camera cameraToUse = GetCamera();
        if (cameraToUse == null || !TryReadMouseScreenPosition(out Vector2 mouseScreenPosition))
        {
            return;
        }

        Vector3 mouseWorldPosition = GetMouseWorldPosition(cameraToUse, mouseScreenPosition, transform.position.z);
        bool firingLeft = mouseWorldPosition.x < transform.position.x - leftFireDeadZone;
        Vector3 spawnPosition = GetMuzzleWorldPosition(firingLeft);
        mouseWorldPosition = GetMouseWorldPosition(cameraToUse, mouseScreenPosition, spawnPosition.z);
        Vector2 direction = mouseWorldPosition - spawnPosition;
        if (direction.sqrMagnitude < MinDirectionSqrMagnitude)
        {
            return;
        }

        if (isReloading && ShouldInterruptReloadWhenFired())
        {
            CancelReload();
        }

        FireProjectile(spawnPosition, direction.normalized, cameraToUse);
        ConsumeBullet();
        nextShotTime = Time.time + GetShotInterval();
    }

    private void FindReferences()
    {
        if (gunData == null)
        {
            gunData = LoadDefaultGunData();
        }

        if (muzzle == null)
        {
            Transform foundMuzzle = transform.Find("ArmsGun/Muzzle");
            if (foundMuzzle != null)
            {
                muzzle = foundMuzzle;
            }
        }

        if (aimGraphic == null)
        {
            aimGraphic = transform.Find("ArmsGun");
        }

        if (reloadAnimationParent == null)
        {
            reloadAnimationParent = transform;
        }

        if (arisaAim == null)
        {
            arisaAim = GetComponent<ArisaMouseAim>();
        }

        if (arisaReloadSpriteRenderer == null)
        {
            if (aimGraphic != null)
            {
                arisaReloadSpriteRenderer = aimGraphic.GetComponent<SpriteRenderer>();
            }

            if (arisaReloadSpriteRenderer == null)
            {
                Transform armsGun = transform.Find("ArmsGun");
                if (armsGun != null)
                {
                    arisaReloadSpriteRenderer = armsGun.GetComponent<SpriteRenderer>();
                }
            }
        }
    }

    private bool CanCreateProjectile()
    {
        return gunData != null && gunData.CanFire;
    }

    private Camera GetCamera()
    {
        if (targetCamera != null)
        {
            return targetCamera;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            return mainCamera;
        }

        Camera[] cameras = Camera.allCameras;
        return cameras.Length > 0 ? cameras[0] : null;
    }

    private Vector3 GetMuzzleWorldPosition(bool firingLeft)
    {
        if (firingLeft && aimGraphic != null)
        {
            return aimGraphic.TransformPoint(leftMuzzleLocalOffset);
        }

        if (muzzle != null)
        {
            return muzzle.position;
        }

        if (aimGraphic != null)
        {
            return aimGraphic.TransformPoint(fallbackMuzzleLocalOffset);
        }

        return transform.position;
    }

    private void FireProjectile(Vector3 spawnPosition, Vector2 direction, Camera cameraToUse)
    {
        if (gunData == null)
        {
            return;
        }

        gunData.Fire(new GunFireContext(spawnPosition, direction, cameraToUse, this));
        arisaAim?.PlayFireShake(gunData.FireShakeAngle, gunData.FireShakeDuration, gunData.FireShakeCycles);
    }

    private float GetShotInterval()
    {
        return 1f / Mathf.Max(MinAttackSpeed, GetAttackSpeed());
    }

    private float GetAttackSpeed()
    {
        return gunData != null ? gunData.AttackSpeed : MinAttackSpeed;
    }

    private int GetMaxBulletCount()
    {
        return gunData != null ? Mathf.Max(MinBulletCount, gunData.BulletCount) : 0;
    }

    private float GetReloadSpeed()
    {
        return gunData != null ? Mathf.Max(0f, gunData.ReloadSpeed) : 0f;
    }

    private bool ShouldReloadSequentially()
    {
        return gunData != null && gunData.ReloadsSequentially;
    }

    private bool CanFireWhileReloading()
    {
        return gunData != null && gunData.CanFireWhileReloading;
    }

    private bool ShouldInterruptReloadWhenFired()
    {
        return gunData != null && gunData.InterruptsReloadWhenFired;
    }

    private float GetSequentialReloadDelay()
    {
        return gunData != null ? gunData.SequentialReloadDelay : 0f;
    }

    private void ConsumeBullet()
    {
        currentBulletCount = Mathf.Max(0, currentBulletCount - 1);

        if (currentBulletCount <= 0)
        {
            BeginReload();
        }
    }

    private void BeginReload()
    {
        if (isReloading || gunData == null || currentBulletCount >= GetMaxBulletCount())
        {
            return;
        }

        float reloadSpeed = GetReloadSpeed();
        if (reloadSpeed <= 0f)
        {
            FillBulletCount();
            return;
        }

        isReloading = true;
        BeginReloadStep(reloadSpeed);
    }

    private void UpdateReload()
    {
        if (!isReloading)
        {
            return;
        }

        if (Time.time >= reloadCompleteTime)
        {
            if (isWaitingBetweenSequentialReloads)
            {
                BeginReloadStep(GetReloadSpeed());
                return;
            }

            CompleteReloadStep();
            return;
        }

        if (!isWaitingBetweenSequentialReloads)
        {
            UpdateReloadAnimationPosition();
            UpdateArisaReloadAnimation();
        }
    }

    private void FillBulletCount()
    {
        currentBulletCount = GetMaxBulletCount();
        EndReload();
    }

    private void CompleteReloadStep()
    {
        if (!ShouldReloadSequentially())
        {
            FillBulletCount();
            return;
        }

        currentBulletCount = Mathf.Min(GetMaxBulletCount(), currentBulletCount + 1);
        if (currentBulletCount >= GetMaxBulletCount())
        {
            EndReload();
            return;
        }

        BeginReloadWaitOrNextStep();
    }

    private void BeginReloadWaitOrNextStep()
    {
        float reloadDelay = GetSequentialReloadDelay();
        if (reloadDelay <= 0f)
        {
            BeginReloadStep(GetReloadSpeed());
            return;
        }

        isWaitingBetweenSequentialReloads = true;
        ScheduleReloadTimer(reloadDelay);
        StopArisaReloadAnimation();
        HideReloadAnimation();
    }

    private void BeginReloadStep(float reloadSpeed)
    {
        isWaitingBetweenSequentialReloads = false;
        ScheduleReloadTimer(reloadSpeed);
        StartArisaReloadAnimation();
        ShowReloadAnimation();
    }

    private void ScheduleReloadTimer(float duration)
    {
        reloadStartedTime = Time.time;
        reloadCompleteTime = Time.time + Mathf.Max(0f, duration);
    }

    private void CancelReload()
    {
        EndReload();
    }

    private void EndReload()
    {
        StopArisaReloadAnimation();
        isReloading = false;
        isWaitingBetweenSequentialReloads = false;
        reloadStartedTime = 0f;
        reloadCompleteTime = 0f;
        HideReloadAnimation();
    }

    private void StartArisaReloadAnimation()
    {
        if (!CanPlayArisaReloadAnimation())
        {
            return;
        }

        spriteBeforeArisaReload = arisaReloadSpriteRenderer.sprite;
        hasSpriteBeforeArisaReload = true;
        SetArisaReloadAnimationFrame(0);
        ApplyArisaReloadPositionOffset();
    }

    private void UpdateArisaReloadAnimation()
    {
        if (!CanPlayArisaReloadAnimation())
        {
            return;
        }

        float elapsed = Mathf.Max(0f, Time.time - reloadStartedTime);
        int frameIndex = arisaReloadFrameRate <= 0f
            ? 0
            : Mathf.FloorToInt(elapsed * arisaReloadFrameRate);
        SetArisaReloadAnimationFrame(frameIndex);
        ApplyArisaReloadPositionOffset();
    }

    private void StopArisaReloadAnimation()
    {
        if (arisaReloadSpriteRenderer != null && hasSpriteBeforeArisaReload)
        {
            arisaReloadSpriteRenderer.sprite = spriteBeforeArisaReload;
        }

        RestoreArisaReloadPositionOffset();
        spriteBeforeArisaReload = null;
        hasSpriteBeforeArisaReload = false;
    }

    private bool CanPlayArisaReloadAnimation()
    {
        return arisaReloadSpriteRenderer != null
            && arisaReloadSprites != null
            && arisaReloadSprites.Length > 0;
    }

    private void SetArisaReloadAnimationFrame(int frameIndex)
    {
        if (!CanPlayArisaReloadAnimation())
        {
            return;
        }

        Sprite reloadSprite = arisaReloadSprites[Mathf.Abs(frameIndex) % arisaReloadSprites.Length];
        if (reloadSprite != null)
        {
            arisaReloadSpriteRenderer.sprite = reloadSprite;
        }
    }

    private void ApplyArisaReloadPositionOffset()
    {
        if (arisaReloadSpriteRenderer == null)
        {
            return;
        }

        Transform reloadTransform = arisaReloadSpriteRenderer.transform;
        Vector3 currentLocalPosition = reloadTransform.localPosition;
        if (hasAppliedArisaReloadPositionOffset)
        {
            Vector3 expectedOffsetPosition = arisaReloadBaseLocalPosition + appliedArisaReloadLocalOffset;
            if (IsNearlySamePosition(currentLocalPosition, expectedOffsetPosition))
            {
                currentLocalPosition = arisaReloadBaseLocalPosition;
            }
        }

        arisaReloadBaseLocalPosition = currentLocalPosition;
        appliedArisaReloadLocalOffset = GetDirectionalArisaReloadOffset();
        reloadTransform.localPosition = arisaReloadBaseLocalPosition + appliedArisaReloadLocalOffset;
        hasAppliedArisaReloadPositionOffset = true;
    }

    private void RestoreArisaReloadPositionOffset()
    {
        if (arisaReloadSpriteRenderer == null || !hasAppliedArisaReloadPositionOffset)
        {
            hasAppliedArisaReloadPositionOffset = false;
            return;
        }

        Transform reloadTransform = arisaReloadSpriteRenderer.transform;
        Vector3 expectedOffsetPosition = arisaReloadBaseLocalPosition + appliedArisaReloadLocalOffset;
        if (IsNearlySamePosition(reloadTransform.localPosition, expectedOffsetPosition))
        {
            reloadTransform.localPosition = arisaReloadBaseLocalPosition;
        }

        hasAppliedArisaReloadPositionOffset = false;
    }

    private static bool IsNearlySamePosition(Vector3 a, Vector3 b)
    {
        return (a - b).sqrMagnitude <= 0.000001f;
    }

    private Vector3 GetDirectionalArisaReloadOffset()
    {
        float xOffset = IsFacingLeft() ? -arisaReloadLocalOffset.x : arisaReloadLocalOffset.x;
        return new Vector3(xOffset, arisaReloadLocalOffset.y, 0f);
    }

    private void ShowReloadAnimation()
    {
        GameObject reloadAnimationPrefab = gunData != null ? gunData.ReloadAnimationPrefab : null;
        if (reloadAnimationPrefab == null)
        {
            return;
        }

        DestroyReloadAnimationObject();

        Transform parent = reloadAnimationParent != null ? reloadAnimationParent : transform;
        reloadAnimationObject = Instantiate(reloadAnimationPrefab, parent);
        reloadAnimationObject.name = reloadAnimationPrefab.name;

        if (reloadAnimationObject == null)
        {
            return;
        }

        reloadAnimationObject.SetActive(true);
        reloadAnimationObject.transform.localRotation = Quaternion.identity;
        SetReloadAnimationPosition(0f);
    }

    private void UpdateReloadAnimationPosition()
    {
        if (reloadAnimationObject == null || reloadCompleteTime <= reloadStartedTime)
        {
            return;
        }

        float reloadProgress = Mathf.InverseLerp(reloadStartedTime, reloadCompleteTime, Time.time);
        SetReloadAnimationPosition(reloadProgress);
    }

    private void SetReloadAnimationPosition(float reloadProgress)
    {
        if (reloadAnimationObject == null)
        {
            return;
        }

        float risePortion = ReloadAnimationPeakProgress;
        float yOffset;
        if (reloadProgress < risePortion)
        {
            float riseProgress = Mathf.SmoothStep(0f, 1f, reloadProgress / risePortion);
            yOffset = Mathf.Lerp(0f, reloadAnimationRiseHeight, riseProgress);
        }
        else
        {
            float descendProgress = Mathf.SmoothStep(0f, 1f, (reloadProgress - risePortion) / (1f - risePortion));
            yOffset = Mathf.Lerp(reloadAnimationRiseHeight, 0f, descendProgress);
        }

        reloadAnimationObject.transform.localPosition = GetMagazineReloadAnimationLocalPosition(yOffset);
        ApplyMagazineReloadAnimationFacing();
    }

    private Vector3 GetMagazineReloadAnimationLocalPosition(float yOffset)
    {
        float localX = reloadAnimationLocalOffset.x;
        if (IsFacingLeft())
        {
            localX = arisaAim != null ? arisaAim.MirrorLocalX(localX) : -localX;
        }

        return new Vector3(localX, reloadAnimationLocalOffset.y + yOffset, 0f);
    }

    private void ApplyMagazineReloadAnimationFacing()
    {
        if (reloadAnimationObject == null)
        {
            return;
        }

        Vector3 localScale = reloadAnimationObject.transform.localScale;
        localScale.x = Mathf.Abs(localScale.x) * (IsFacingLeft() ? -1f : 1f);
        reloadAnimationObject.transform.localScale = localScale;
    }

    private bool IsFacingLeft()
    {
        if (arisaAim != null)
        {
            return arisaAim.IsFacingLeft;
        }

        return aimGraphic != null && aimGraphic.localScale.x < 0f;
    }

    private void HideReloadAnimation()
    {
        DestroyReloadAnimationObject();
    }

    private void DestroyReloadAnimationObject()
    {
        if (reloadAnimationObject == null)
        {
            return;
        }

        Destroy(reloadAnimationObject);
        reloadAnimationObject = null;
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

    private GunSO LoadDefaultGunData()
    {
#if UNITY_EDITOR
        return AssetDatabase.LoadAssetAtPath<GunSO>(DefaultGunDataPath);
#else
        return null;
#endif
    }

    private bool ReadFireHeld()
    {
#if ENABLE_INPUT_SYSTEM
        if (fireAction != null && fireAction.IsPressed())
        {
            return true;
        }

        if (Pointer.current != null && Pointer.current.press.isPressed)
        {
            return true;
        }

        if (Mouse.current != null)
        {
            return Mouse.current.leftButton.isPressed;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetMouseButton(0);
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

    private bool ReadReloadPressed()
    {
#if ENABLE_INPUT_SYSTEM
        if (reloadAction != null && reloadAction.WasPressedThisFrame())
        {
            return true;
        }

        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
        {
            return true;
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        return Input.GetKeyDown(KeyCode.R);
#else
        return false;
#endif
    }
}
