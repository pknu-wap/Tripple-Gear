using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform), typeof(RawImage))]
public sealed class MouseReticleController : MonoBehaviour
{
    [SerializeField, KoreanLabel("플레이어")] private Transform player;
    [SerializeField, KoreanLabel("발사 시 조준경 색")] private Color shotColor = Color.white;
    [SerializeField, KoreanLabel("발사 색 유지 시간"), Min(0.01f)] private float shotColorDuration = 0.12f;
    [SerializeField] private Shader shotColorShader;

    private RectTransform canvasRect;
    private RectTransform reticleRect;
    private RawImage reticleImage;
    private SubmachineGunFire gun;
    private Material normalMaterial;
    private Material shotMaterial;
    private float restoreColorAt;
    private bool colorFlashing;
    private bool previousCursorVisible;
    private bool cursorHidden;

    private void Awake()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        canvasRect = canvas != null ? canvas.GetComponent<RectTransform>() : null;
        reticleRect = GetComponent<RectTransform>();
        reticleImage = GetComponent<RawImage>();
        gun = player != null ? player.GetComponent<SubmachineGunFire>() : null;
        normalMaterial = reticleImage.material;
        if (shotColorShader != null)
        {
            shotMaterial = new Material(shotColorShader) { hideFlags = HideFlags.DontSave };
        }
    }

    private void OnEnable()
    {
        if (canvasRect == null || reticleImage == null)
        {
            return;
        }

        previousCursorVisible = Cursor.visible;
        Cursor.visible = false;
        cursorHidden = true;
        if (gun != null)
        {
            gun.ShotFired += ShowShotColor;
        }
    }

    private void LateUpdate()
    {
        if (colorFlashing && Time.unscaledTime >= restoreColorAt)
        {
            reticleImage.material = normalMaterial;
            colorFlashing = false;
        }

        if (canvasRect == null || reticleImage == null || Mouse.current == null)
        {
            if (reticleImage != null)
            {
                reticleImage.enabled = false;
            }
            return;
        }

        Vector2 mousePosition = Mouse.current.position.ReadValue();
        bool insideScreen = Application.isFocused && mousePosition.x >= 0f && mousePosition.x <= Screen.width
            && mousePosition.y >= 0f && mousePosition.y <= Screen.height;
        reticleImage.enabled = insideScreen;
        if (insideScreen && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, mousePosition, null, out Vector2 localPosition))
        {
            reticleRect.anchoredPosition = localPosition;
        }
    }

    private void OnDisable()
    {
        if (gun != null)
        {
            gun.ShotFired -= ShowShotColor;
        }

        if (reticleImage != null)
        {
            reticleImage.enabled = false;
            reticleImage.material = normalMaterial;
        }
        colorFlashing = false;

        if (cursorHidden)
        {
            Cursor.visible = previousCursorVisible;
            cursorHidden = false;
        }
    }

    private void ShowShotColor()
    {
        if (shotMaterial == null)
        {
            return;
        }

        shotMaterial.SetColor("_FlashColor", shotColor);
        reticleImage.material = shotMaterial;
        restoreColorAt = Time.unscaledTime + shotColorDuration;
        colorFlashing = true;
    }

    private void OnDestroy()
    {
        if (shotMaterial != null)
        {
            Destroy(shotMaterial);
        }
    }
}
