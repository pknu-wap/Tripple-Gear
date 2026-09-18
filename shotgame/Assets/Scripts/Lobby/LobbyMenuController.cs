using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class LobbyMenuController : MonoBehaviour
{
    [SerializeField] private string newGameSceneName = "테스트 배틀씬";
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button loadGameButton;
    [SerializeField] private Button optionsButton;
    [SerializeField] private Button quitButton;

    private void Awake()
    {
        FindReferences();
    }

    private void OnValidate()
    {
        FindReferences();
    }

    private void OnEnable()
    {
        FindReferences();

        if (newGameButton != null)
        {
            newGameButton.onClick.AddListener(StartNewGame);
        }

        if (loadGameButton != null)
        {
            loadGameButton.onClick.AddListener(LoadGame);
        }

        if (optionsButton != null)
        {
            optionsButton.onClick.AddListener(OpenOptions);
        }

        if (quitButton != null)
        {
            quitButton.onClick.AddListener(QuitGame);
        }
    }

    private void OnDisable()
    {
        if (newGameButton != null)
        {
            newGameButton.onClick.RemoveListener(StartNewGame);
        }

        if (loadGameButton != null)
        {
            loadGameButton.onClick.RemoveListener(LoadGame);
        }

        if (optionsButton != null)
        {
            optionsButton.onClick.RemoveListener(OpenOptions);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(QuitGame);
        }
    }

    private void FindReferences()
    {
        if (newGameButton == null)
        {
            newGameButton = FindButton("NewGameButton");
        }

        if (loadGameButton == null)
        {
            loadGameButton = FindButton("LoadGameButton");
        }

        if (optionsButton == null)
        {
            optionsButton = FindButton("OptionsButton");
        }

        if (quitButton == null)
        {
            quitButton = FindButton("QuitButton");
        }
    }

    private static Button FindButton(string objectName)
    {
        GameObject buttonObject = GameObject.Find(objectName);
        return buttonObject != null ? buttonObject.GetComponent<Button>() : null;
    }

    public void StartNewGame()
    {
        LoadScene(newGameSceneName);
    }

    public void LoadGame()
    {
        Debug.Log("불러오기 메뉴는 아직 구현되지 않았습니다.", this);
    }

    public void OpenOptions()
    {
        Debug.Log("옵션 메뉴는 아직 구현되지 않았습니다.", this);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void LoadScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning("이동할 씬 이름이 비어 있습니다.", this);
            return;
        }

        string fromSceneName = SceneManager.GetActiveScene().name;
        SceneTransitionSignalReceiver.NotifySceneTransition(fromSceneName, sceneName);
        SceneManager.LoadScene(sceneName);
    }
}
