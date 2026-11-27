using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public partial class GameManager
{
    private GameObject pauseOverlay;
    private GameObject pauseActions;
    private GameObject pauseSettings;
    private bool pauseOpen;
    private bool previousMenuCursor;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLock;
    private GameObject previousSelection;
    private Button pauseSettingsButton;
    private Button pauseBackButton;

    internal bool IsPauseMenuOpen => pauseOpen;

    private void Update()
    {
        if (Keyboard.current?.escapeKey.wasPressedThisFrame != true) return;
        if (pauseOpen)
        {
            if (pauseSettings.activeSelf) DisplayPauseSettings(false);
            else ClosePauseMenu();
            return;
        }

        if (gameplayCanvas != null && !isGameOver && !GameplayPause.IsPaused && !GameplayInput.IsBlocked)
            OpenPauseMenu();
    }

    private void OnDisable() => ClosePauseMenu();

    private void OpenPauseMenu()
    {
        if (pauseOverlay == null) CreatePauseMenu();
        pauseOpen = true;
        GameplayPause.Acquire(pauseOverlay);
        GameplayInput.Block(this);
        previousCursorVisible = Cursor.visible;
        previousCursorLock = Cursor.lockState;
        if (mouseReticle != null)
        {
            previousMenuCursor = mouseReticle.ShowMenuCursor;
            mouseReticle.ShowMenuCursor = true;
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        pauseOverlay.SetActive(true);
        DisplayPauseSettings(false);
    }

    private void ClosePauseMenu()
    {
        if (!pauseOpen) return;
        pauseOpen = false;
        if (pauseSettings != null && pauseSettings.activeSelf) PlayerPrefs.Save();
        if (pauseOverlay != null) pauseOverlay.SetActive(false);
        GameplayPause.Release(pauseOverlay);
        GameplayInput.Unblock(this);
        if (mouseReticle != null) mouseReticle.ShowMenuCursor = previousMenuCursor;
        Cursor.lockState = previousCursorLock;
        if (mouseReticle == null || !mouseReticle.isActiveAndEnabled) Cursor.visible = previousCursorVisible;
        EventSystem.current?.SetSelectedGameObject(
            previousSelection != null && previousSelection.activeInHierarchy ? previousSelection : null);
        previousSelection = null;
    }

    private void DisplayPauseSettings(bool show)
    {
        if (!show && pauseSettings.activeSelf) PlayerPrefs.Save();
        pauseActions.SetActive(!show);
        pauseSettings.SetActive(show);
        EventSystem.current?.SetSelectedGameObject((show ? pauseBackButton : pauseSettingsButton).gameObject);
    }

    private void CreatePauseMenu()
    {
        pauseOverlay = new GameObject("일시정지 메뉴", typeof(Canvas), typeof(CanvasScaler),
            typeof(GraphicRaycaster), typeof(Image));
        SceneManager.MoveGameObjectToScene(pauseOverlay, gameObject.scene);
        var canvas = pauseOverlay.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 450;
        var scaler = pauseOverlay.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        pauseOverlay.GetComponent<Image>().color = new Color(0.018f, 0.028f, 0.05f, 0.66f);

        RectTransform actions = CreatePausePanel("일시정지 선택", new Vector2(440f, 204f));
        pauseActions = actions.gameObject;
        pauseSettingsButton = CreatePauseButton(actions, "설정", 42f, () => DisplayPauseSettings(true));
        CreatePauseButton(actions, "게임나가기(로비로)", -42f, GoToLobby);

        RectTransform settings = CreatePausePanel("게임 중 음량 설정", new Vector2(440f, 390f));
        pauseSettings = settings.gameObject;
        var title = ScoreText.Create(settings, "설정 제목", 41f);
        title.text = "음량 설정";
        title.rectTransform.anchoredPosition = new Vector2(0f, 137f);
        title.rectTransform.sizeDelta = new Vector2(350f, 62f);
        AudioSettingsControls.AddTo(settings, ScoreText.Create);
        pauseBackButton = CreatePauseButton(settings, "뒤로", -138f, () => DisplayPauseSettings(false));
        pauseSettings.SetActive(false);
        pauseOverlay.SetActive(false);
    }

    private RectTransform CreatePausePanel(string name, Vector2 size)
    {
        var image = new GameObject(name, typeof(Image)).GetComponent<Image>();
        image.rectTransform.SetParent(pauseOverlay.transform, false);
        image.rectTransform.sizeDelta = size;
        image.color = new Color(0.06f, 0.105f, 0.18f, 0.98f);
        image.raycastTarget = false;
        return image.rectTransform;
    }

    private static Button CreatePauseButton(RectTransform parent, string text, float y, UnityEngine.Events.UnityAction onClick)
    {
        var image = new GameObject(text, typeof(Image), typeof(Button)).GetComponent<Image>();
        RectTransform rect = image.rectTransform;
        rect.SetParent(parent, false);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(346f, 62f);
        image.color = new Color(0.13f, 0.27f, 0.43f);
        var button = image.GetComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(onClick);

        var label = ScoreText.Create(rect, "Text", 31f);
        label.text = text;
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        return button;
    }
}
