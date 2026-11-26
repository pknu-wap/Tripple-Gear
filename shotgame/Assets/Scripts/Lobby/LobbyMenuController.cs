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
        newGameButton.onClick.RemoveListener(ShowDifficultyMenu);
        quitButton.onClick.RemoveListener(QuitGame);
        easyButton.onClick.RemoveListener(StartEasyGame);
        hardButton.onClick.RemoveListener(StartHardGame);
        backButton.onClick.RemoveListener(ShowMainMenu);
    }

    private void ShowDifficultyMenu()
    {
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
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
