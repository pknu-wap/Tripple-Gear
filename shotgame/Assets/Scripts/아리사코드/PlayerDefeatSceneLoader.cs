using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class PlayerDefeatSceneLoader : MonoBehaviour
{
    [SerializeField, KoreanLabel("사망 시 이동할 씬 이름")]
    private string lobbySceneName = "로비";

    private bool isLoadingLobby;

    public void LoadLobby()
    {
        if (isLoadingLobby)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(lobbySceneName))
        {
            Debug.LogError("로비 씬 이름이 설정되지 않았습니다.", this);
            return;
        }

        isLoadingLobby = true;
        SceneManager.LoadScene(lobbySceneName);
    }
}
