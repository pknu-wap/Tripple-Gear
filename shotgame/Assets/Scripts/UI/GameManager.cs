
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [SerializeField]
    private GameObject gameOverUI;

    [SerializeField]
    private string lobbySceneName = "로비";

    private bool isGameOver = false;

    internal bool IsGameOver => isGameOver;

    private void Awake()
    {
        Instance = this;

        Time.timeScale = 1f;

        if (gameOverUI != null)
        {
            gameOverUI.SetActive(false);
        }
    }

    public void GameOver()
    {
        if (isGameOver) return;

        isGameOver = true;

        gameOverUI.SetActive(true);
        Time.timeScale = 0f;
    }

    // 현재 게임 다시 시작
    public void Retry()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // 로비로 이동
    public void GoToLobby()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(lobbySceneName);
    }
}
