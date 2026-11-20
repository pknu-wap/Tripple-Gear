#if UNITY_EDITOR
using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public sealed partial class TeleportTrigger
{
    [Serializable]
    private sealed class TestDestination
    {
        [SerializeField, InspectorName("표시 이름")] internal string label;
        [SerializeField, InspectorName("텔레포트")] internal TeleportTrigger route;
    }

    [Header("에디터 테스트")]
    [SerializeField, KoreanLabel("임시 테스트용 F1 메뉴")] private bool temporaryF1Shortcut;
    [SerializeField, KoreanLabel("F1 이동 목적지")] private TestDestination[] temporaryDestinations;
    [SerializeField, KoreanLabel("F1 메뉴 글꼴")] private Font temporaryMenuFont;

    private bool testMenuOpen;
    private float timeScaleBeforeTestMenu;
    private bool cursorWasVisible;
    private CursorLockMode cursorLockBeforeTestMenu;

    private void Update()
    {
        if (!temporaryF1Shortcut || temporaryDestinations == null || temporaryDestinations.Length == 0)
        {
            SetTestMenuOpen(false);
            return;
        }

#if ENABLE_INPUT_SYSTEM
        bool togglePressed = Keyboard.current?.f1Key.wasPressedThisFrame == true;
#elif ENABLE_LEGACY_INPUT_MANAGER
        bool togglePressed = Input.GetKeyDown(KeyCode.F1);
#else
        bool togglePressed = false;
#endif
        if (togglePressed) SetTestMenuOpen(!testMenuOpen);
    }

    private void OnGUI()
    {
        if (!testMenuOpen)
        {
            return;
        }

        Event input = Event.current;
        if (input.type == EventType.KeyDown)
        {
            if (input.keyCode == KeyCode.Escape)
            {
                SetTestMenuOpen(false);
                input.Use();
                return;
            }

            int index = input.keyCode >= KeyCode.Keypad1 && input.keyCode <= KeyCode.Keypad9
                ? input.keyCode - KeyCode.Keypad1
                : input.keyCode - KeyCode.Alpha1;
            if (index >= 0 && index < Mathf.Min(9, temporaryDestinations.Length))
            {
                SelectTestDestination(index);
                input.Use();
                return;
            }
        }

        Color previousColor = GUI.color;
        Font previousFont = GUI.skin.font;
        GUI.color = new Color(0f, 0f, 0f, 0.65f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = previousColor;
        if (temporaryMenuFont != null) GUI.skin.font = temporaryMenuFont;

        const float rowHeight = 36f;
        float width = Mathf.Min(360f, Screen.width - 32f);
        float height = 108f + rowHeight * temporaryDestinations.Length;
        float left = (Screen.width - width) * 0.5f;
        float top = (Screen.height - height) * 0.5f;
        GUI.Box(new Rect(left, top, width, height), "F1 테스트 이동");
        for (int i = 0; i < temporaryDestinations.Length; i++)
        {
            TestDestination destination = temporaryDestinations[i];
            if (destination != null && destination.route != null && GUI.Button(
                new Rect(left + 16f, top + 32f + i * rowHeight, width - 32f, 30f), $"{i + 1}. {destination.label}"))
            {
                SelectTestDestination(i);
            }
        }
        if (GUI.Button(new Rect(left + 16f, top + height - 43f, width - 32f, 30f), "닫기  (F1 / Esc)"))
        {
            SetTestMenuOpen(false);
        }
        GUI.skin.font = previousFont;
    }

    private void SelectTestDestination(int index)
    {
        TeleportTrigger route = temporaryDestinations[index]?.route;
        if (route == null || !route.isActiveAndEnabled || route.teleportSubject == null || route.isTeleporting)
        {
            return;
        }

        SetTestMenuOpen(false);
        foreach (Trigger trigger in FindObjectsByType<Trigger>(FindObjectsSortMode.None))
        {
            if (trigger.gameObject.scene == route.gameObject.scene)
            {
                trigger.CancelWave();
                trigger.CancelMission();
            }
        }
        route.TeleportPlayer();
    }

    private void SetTestMenuOpen(bool open)
    {
        if (testMenuOpen == open)
        {
            return;
        }

        testMenuOpen = open;
        if (open)
        {
            timeScaleBeforeTestMenu = Time.timeScale;
            cursorWasVisible = Cursor.visible;
            cursorLockBeforeTestMenu = Cursor.lockState;
            Time.timeScale = 0f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = cursorLockBeforeTestMenu;
            Cursor.visible = cursorWasVisible;
            // A scene change may already have restored its own time scale.
            if (Time.timeScale == 0f) Time.timeScale = timeScaleBeforeTestMenu;
        }
    }

    private void OnDisable() => SetTestMenuOpen(false);
}
#endif
