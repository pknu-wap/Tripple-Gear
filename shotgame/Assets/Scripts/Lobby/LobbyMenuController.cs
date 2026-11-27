using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class LobbyMenuController : MonoBehaviour
{
    [SerializeField] private string newGameSceneName = "인게임샘플";
    [SerializeField] private GameObject mainMenu;
    [SerializeField] private GameObject difficultyMenu;
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button easyButton;
    [SerializeField] private Button hardButton;
    [SerializeField] private Button backButton;

    private GameObject scorePanel;
    private GameObject settingsPanel;
    private Button scoreButton;
    private Button settingsButton;
    private TextMeshProUGUI menuLabel;

    private void Start()
    {
        ShowLobbyCursor();
        menuLabel = quitButton.GetComponentInChildren<TextMeshProUGUI>();
        RectTransform exit = (RectTransform)quitButton.transform;
        float rowSpacing = exit.rect.height + 14f;
        scoreButton = InsertMenuButton(exit, "ScoreButton", "점수", 0f);
        settingsButton = InsertMenuButton(exit, "SettingsButton", "설정", rowSpacing);
        exit.anchoredPosition += Vector2.down * (rowSpacing * 2f);

        CreateScorePanel();
        CreateSettingsPanel();
        scoreButton.onClick.AddListener(() =>
        {
            CloseSettings();
            scorePanel.SetActive(!scorePanel.activeSelf);
        });
        settingsButton.onClick.AddListener(() =>
        {
            scorePanel.SetActive(false);
            if (settingsPanel.activeSelf) CloseSettings();
            else settingsPanel.SetActive(true);
        });
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus) ShowLobbyCursor();
    }

    private static void ShowLobbyCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private Button InsertMenuButton(RectTransform exit, string name, string label, float offset)
    {
        Button button = CreateScoreButton(exit.parent, name, label, menuLabel != null ? menuLabel.fontSize : 32f);
        var rect = (RectTransform)button.transform;
        rect.anchorMin = exit.anchorMin;
        rect.anchorMax = exit.anchorMax;
        rect.pivot = exit.pivot;
        rect.anchoredPosition = exit.anchoredPosition + Vector2.down * offset;
        rect.sizeDelta = exit.sizeDelta;
        rect.SetSiblingIndex(exit.GetSiblingIndex());
        return button;
    }

    private void CreateScorePanel()
    {
        var panel = new GameObject("점수판", typeof(Image)).GetComponent<Image>();
        scorePanel = panel.gameObject;
        scorePanel.SetActive(false);
        panel.transform.SetParent(mainMenu.transform, false);
        RectTransform rect = panel.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.055f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(398f, 542f);
        ApplyMenuFrame(panel, 0.52f);
        panel.raycastTarget = false;

        var title = CreateScoreLabel(rect, "점수판 제목", 42f);
        title.text = "점수판";
        title.rectTransform.anchoredPosition = new Vector2(0f, 221f);
        title.rectTransform.sizeDelta = new Vector2(340f, 64f);

        Button close = CreateScoreButton(rect, "CloseScoresButton", "닫기", 26f);
        var closeRect = (RectTransform)close.transform;
        closeRect.anchoredPosition = new Vector2(0f, -236f);
        closeRect.sizeDelta = new Vector2(172f, 48f);
        close.onClick.AddListener(() =>
        {
            scorePanel.SetActive(false);
            EventSystem.current?.SetSelectedGameObject(scoreButton.gameObject);
        });

        var records = ScoreHistory.Read();
        if (records.Count == 0)
        {
            var empty = CreateScoreLabel(rect, "빈 점수판", 28f);
            empty.text = "저장된 점수가 없습니다";
            empty.rectTransform.sizeDelta = new Vector2(340f, 56f);
            return;
        }

        const float rowHeight = 36f;
        RectTransform rows = CreateScoreList(rect, records.Count * rowHeight);
        for (int i = 0; i < records.Count; i++)
        {
            float y = -(i + 0.5f) * rowHeight;
            var rank = CreateScoreLabel(rows, $"순위 {i + 1}", 27f);
            rank.text = $"{i + 1}위";
            rank.alignment = TextAlignmentOptions.MidlineLeft;
            rank.rectTransform.anchorMin = rank.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            rank.rectTransform.anchoredPosition = new Vector2(-110f, y);
            rank.rectTransform.sizeDelta = new Vector2(102f, rowHeight);

            var score = CreateScoreLabel(rows, $"기록 {i + 1}", 27f);
            score.text = records[i].ToString();
            score.alignment = TextAlignmentOptions.MidlineRight;
            score.rectTransform.anchorMin = score.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            score.rectTransform.anchoredPosition = new Vector2(50f, y);
            score.rectTransform.sizeDelta = new Vector2(222f, rowHeight);
            if (i == 0) rank.color = score.color = quitButton.colors.selectedColor;
        }
    }

    private void CreateSettingsPanel()
    {
        var image = new GameObject("설정 패널", typeof(Image)).GetComponent<Image>();
        settingsPanel = image.gameObject;
        settingsPanel.SetActive(false);
        RectTransform rect = image.rectTransform;
        rect.SetParent(mainMenu.transform, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.055f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(398f, 390f);
        ApplyMenuFrame(image, 0.78f);
        image.raycastTarget = false;

        var title = CreateScoreLabel(rect, "설정 제목", 42f);
        title.text = "음량 설정";
        title.rectTransform.anchoredPosition = new Vector2(0f, 137f);
        title.rectTransform.sizeDelta = new Vector2(340f, 64f);
        AudioSettingsControls.AddTo(rect, CreateScoreLabel);

        var close = CreateScoreButton(rect, "CloseSettingsButton", "닫기", 26f);
        var closeRect = (RectTransform)close.transform;
        closeRect.anchoredPosition = new Vector2(0f, -142f);
        closeRect.sizeDelta = new Vector2(172f, 48f);
        close.onClick.AddListener(() =>
        {
            CloseSettings();
            EventSystem.current?.SetSelectedGameObject(settingsButton.gameObject);
        });
    }

    private void CloseSettings()
    {
        if (settingsPanel == null || !settingsPanel.activeSelf) return;
        settingsPanel.SetActive(false);
        PlayerPrefs.Save();
    }

    private Button CreateScoreButton(Transform parent, string name, string text, float fontSize)
    {
        var background = new GameObject(name, typeof(Image), typeof(Button)).GetComponent<Image>();
        background.transform.SetParent(parent, false);
        ApplyMenuFrame(background, 0.55f);
        Button button = background.GetComponent<Button>();
        button.targetGraphic = background;
        button.colors = quitButton.colors;

        var label = CreateScoreLabel(background.transform, "Text", fontSize);
        label.text = text;
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        return button;
    }

    private void ApplyMenuFrame(Image image, float opacity)
    {
        Image template = quitButton.image;
        image.sprite = template.sprite;
        image.type = template.type;
        image.pixelsPerUnitMultiplier = template.pixelsPerUnitMultiplier;
        Color color = template.color;
        color.a = opacity;
        image.color = color;

        if (template.TryGetComponent(out Outline menuOutline))
        {
            var outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = menuOutline.effectColor;
            outline.effectDistance = menuOutline.effectDistance;
            outline.useGraphicAlpha = menuOutline.useGraphicAlpha;
        }
    }

    private TextMeshProUGUI CreateScoreLabel(Transform parent, string name, float fontSize)
    {
        var label = ScoreText.Create(parent, name, fontSize);
        if (menuLabel != null)
        {
            label.font = menuLabel.font;
            label.fontSharedMaterial = menuLabel.fontSharedMaterial;
            label.fontStyle = menuLabel.fontStyle;
            label.color = menuLabel.color;
        }
        return label;
    }

    private static RectTransform CreateScoreList(RectTransform parent, float contentHeight)
    {
        var viewport = new GameObject("점수 목록", typeof(Image), typeof(RectMask2D), typeof(ScrollRect));
        viewport.GetComponent<Image>().color = Color.clear;
        var rect = (RectTransform)viewport.transform;
        rect.SetParent(parent, false);
        rect.sizeDelta = new Vector2(360f, 360f);
        rect.anchoredPosition = new Vector2(0f, -29f);

        var content = new GameObject("저장 기록", typeof(RectTransform)).GetComponent<RectTransform>();
        content.SetParent(rect, false);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, contentHeight);

        var scroll = viewport.GetComponent<ScrollRect>();
        scroll.viewport = rect;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 24f;
        return content;
    }

    private void OnEnable()
    {
        newGameButton.onClick.AddListener(ShowDifficultyMenu);
        quitButton.onClick.AddListener(QuitGame);
        easyButton.onClick.AddListener(StartEasyGame);
        hardButton.onClick.AddListener(StartHardGame);
        backButton.onClick.AddListener(ShowMainMenu);
    }

    private void OnDisable()
    {
        CloseSettings();
        newGameButton.onClick.RemoveListener(ShowDifficultyMenu);
        quitButton.onClick.RemoveListener(QuitGame);
        easyButton.onClick.RemoveListener(StartEasyGame);
        hardButton.onClick.RemoveListener(StartHardGame);
        backButton.onClick.RemoveListener(ShowMainMenu);
    }

    private void ShowDifficultyMenu()
    {
        CloseSettings();
        if (scorePanel != null) scorePanel.SetActive(false);
        mainMenu.SetActive(false);
        difficultyMenu.SetActive(true);
        EventSystem.current?.SetSelectedGameObject(easyButton.gameObject);
    }

    private void ShowMainMenu()
    {
        difficultyMenu.SetActive(false);
        mainMenu.SetActive(true);
        EventSystem.current?.SetSelectedGameObject(newGameButton.gameObject);
    }

    private void StartEasyGame() => StartNewGame(isEasy: true);

    private void StartHardGame() => StartNewGame(isEasy: false);

    private void StartNewGame(bool isEasy)
    {
        GameDifficulty.IsEasy = isEasy;
        SceneTransitionSignalReceiver.LoadSceneWithSignal(newGameSceneName);
    }

    private void QuitGame()
    {
        CloseSettings();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
