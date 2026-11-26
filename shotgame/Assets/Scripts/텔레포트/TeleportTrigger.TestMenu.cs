using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

public sealed partial class TeleportTrigger
{
    [Serializable]
    private sealed class TestDestination
    {
        [SerializeField, InspectorName("표시 이름")] internal string label;
        [SerializeField, InspectorName("텔레포트")] internal TeleportTrigger route;
        [SerializeField, InspectorName("보트 항해")] internal BoatVoyage voyage;
        [SerializeField, InspectorName("아테일 등장 연출")] internal AtailEntrance atailEntrance;
        [SerializeField, InspectorName("이동 시 다시 시작할 보스")] internal BossTrigger resetBoss;
    }

    [Header("테스트 이동")]
    [SerializeField, KoreanLabel("임시 테스트용 F1 메뉴")] private bool temporaryF1Shortcut;
    [SerializeField, KoreanLabel("F1 이동 목적지")] private TestDestination[] temporaryDestinations;

    private bool testMenuOpen;
    private GameObject testMenuCanvas;
    private RectTransform testMenuPanel;
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
        if (testMenuOpen)
        {
            testMenuPanel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,
                Mathf.Max(0f, Mathf.Min(360f, Screen.width - 32f)));
        }
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
            }
        }
    }

    private void CreateTestMenu()
    {
        const float rowHeight = 36f;
        float height = 108f + rowHeight * (temporaryDestinations.Length + 1);
        testMenuCanvas = new GameObject("F1 Test Menu", typeof(Canvas), typeof(GraphicRaycaster), typeof(Image));
        var canvas = testMenuCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        testMenuCanvas.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.65f);

        if (EventSystem.current == null)
        {
            var events = new GameObject("F1 Event System", typeof(EventSystem));
            events.transform.SetParent(canvas.transform, false);
#if ENABLE_INPUT_SYSTEM
            events.AddComponent<InputSystemUIInputModule>();
#elif ENABLE_LEGACY_INPUT_MANAGER
            events.AddComponent<StandaloneInputModule>();
#endif
        }

        var panel = new GameObject("Panel", typeof(Image)).GetComponent<Image>();
        panel.color = new Color(0.16f, 0.16f, 0.16f);
        testMenuPanel = panel.rectTransform;
        testMenuPanel.SetParent(canvas.transform, false);
        testMenuPanel.sizeDelta = new Vector2(360f, height);
        CreateTestMenuLabel(CreateTestMenuRow(testMenuPanel, "Title", 0f, 30f), "F1 테스트 메뉴");

        for (int i = 0; i < temporaryDestinations.Length; i++)
        {
            TestDestination destination = temporaryDestinations[i];
            if (destination != null && (destination.route != null || destination.voyage != null
                || destination.atailEntrance != null))
            {
                int index = i;
                CreateTestMenuButton(32f + i * rowHeight, $"{i + 1}. {destination.label}",
                    _ => SelectTestDestination(index));
            }
        }

        if (teleportSubject != null && teleportSubject.TryGetComponent(out SubmachineGunFire playerGun))
        {
            string DamageLabel() => playerGun.TestDamageEnabled
                ? "총 데미지 100 해제 (적용 중)" : "총 데미지 100 적용";
            CreateTestMenuButton(height - 43f - rowHeight, DamageLabel(), label =>
            {
                if (playerGun == null) return;
                playerGun.TestDamageEnabled = !playerGun.TestDamageEnabled;
                label.text = DamageLabel();
            });
        }

        CreateTestMenuButton(height - 43f, "닫기  (F1 / Esc)", _ => SetTestMenuOpen(false));
    }

    private void CreateTestMenuButton(float top, string text, Action<TMP_Text> onClick)
    {
        RectTransform row = CreateTestMenuRow(testMenuPanel, "Button", top, 30f);
        var background = row.gameObject.AddComponent<Image>();
        background.color = new Color(0.32f, 0.32f, 0.32f);
        var button = row.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        TMP_Text label = CreateTestMenuLabel(row, text);
        button.onClick.AddListener(() => onClick(label));
    }

    private static RectTransform CreateTestMenuRow(RectTransform parent, string name, float top, float height)
    {
        var row = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        row.SetParent(parent, false);
        row.anchorMin = new Vector2(0f, 1f);
        row.anchorMax = Vector2.one;
        row.offsetMin = new Vector2(16f, -top - height);
        row.offsetMax = new Vector2(-16f, -top);
        return row;
    }

    private static TMP_Text CreateTestMenuLabel(RectTransform parent, string text)
    {
        var label = new GameObject("Label", typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        label.rectTransform.SetParent(parent, false);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = Vector2.zero;
        label.rectTransform.offsetMax = Vector2.zero;
        label.font = TMP_Settings.defaultFontAsset;
        label.text = text;
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 14f;
        label.raycastTarget = false;
        return label;
    }

    private void SelectTestDestination(int index)
    {
        TestDestination destination = temporaryDestinations[index];
        TeleportTrigger route = destination?.route;
        BoatVoyage voyage = destination?.voyage;
        AtailEntrance atailEntrance = destination?.atailEntrance;
        bool canTravel = atailEntrance != null ? atailEntrance.CanPlayFromTestMenu
            : voyage != null ? voyage.isActiveAndEnabled
            : route != null && route.isActiveAndEnabled && route.teleportSubject != null && !route.isTeleporting;
        if (!canTravel || GetFade().IsFading)
        {
            return;
        }

        SetTestMenuOpen(false);
        var targetScene = atailEntrance != null ? atailEntrance.gameObject.scene
            : voyage != null ? voyage.gameObject.scene : route.gameObject.scene;
        foreach (Trigger trigger in FindObjectsByType<Trigger>(FindObjectsSortMode.None))
        {
            if (trigger.gameObject.scene == targetScene)
            {
                trigger.CancelWave();
                trigger.CancelMission();
            }
        }

        foreach (BossTrigger trigger in FindObjectsByType<BossTrigger>(FindObjectsSortMode.None))
        {
            if (trigger.gameObject.scene == targetScene)
            {
                trigger.CancelEncounter();
            }
        }

        foreach (AtailEntrance entrance in FindObjectsByType<AtailEntrance>(FindObjectsSortMode.None))
        {
            if (entrance.gameObject.scene == targetScene) entrance.CancelEntrance();
        }

        foreach (AtailBossController battle in FindObjectsByType<AtailBossController>(FindObjectsSortMode.None))
        {
            if (battle.gameObject.scene == targetScene) battle.CancelDefeatSequence();
        }

        foreach (SummitAirshipBoss boss in FindObjectsByType<SummitAirshipBoss>(FindObjectsSortMode.None))
        {
            if (boss.gameObject.scene == targetScene) boss.CancelBoardingSequence();
        }

        if (destination.resetBoss != null) destination.resetBoss.ResetEncounter();

        if (atailEntrance != null)
        {
            atailEntrance.PlayFromTestMenu();
        }
        else if (voyage != null)
        {
            voyage.RestartForTest();
        }
        else
        {
            route.StartCoroutine(route.TeleportRoutine(route.teleportSubject, restorePlayerFollow: true));
        }
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
            GameplayInput.Block(this);
            cursorWasVisible = Cursor.visible;
            cursorLockBeforeTestMenu = Cursor.lockState;
            GameplayPause.Acquire(this);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            CreateTestMenu();
        }
        else
        {
            if (testMenuCanvas != null)
            {
                testMenuCanvas.SetActive(false);
                Destroy(testMenuCanvas);
            }
            GameplayInput.Unblock(this);
            Cursor.lockState = cursorLockBeforeTestMenu;
            Cursor.visible = cursorWasVisible;
            GameplayPause.Release(this);
        }
    }

    private void OnDisable() => SetTestMenuOpen(false);
}
