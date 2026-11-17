using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [SerializeField]
    private GameObject gameOverUI;

    private bool isGameOver = false;

    private void Awake()
    {
        Instance = this;

        if (gameOverUI != null)
        {
            gameOverUI.SetActive(false);
        }
    }

    public void GameOver()
    {
        if (isGameOver) return;

        isGameOver = true;

        // 게임오버 UI 표시
        gameOverUI.SetActive(true);

        // 게임 정지
        Time.timeScale = 0f;
    }
}
