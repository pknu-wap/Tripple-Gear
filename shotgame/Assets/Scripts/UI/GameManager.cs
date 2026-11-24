
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [SerializeField]
    private GameObject gameOverUI;

    [SerializeField]
    private string lobbySceneName = "로비";

    [Header("Chapter Clear")]
    [SerializeField] private GunSelectionUI gunSelectionPrefab;
    [SerializeField] private Canvas gameplayCanvas;
    [SerializeField] private MouseReticleController mouseReticle;
    [SerializeField] private SubmachineGunFire playerGun;
    [SerializeField] private TeleportTrigger nextChapterTeleport;

    private GunSelectionUI gunSelection;
    private float gameplayTimeScale;
    private bool ownsChapterPause;
    private int reticleSiblingIndex = -1;
    private bool previousGunInputSuppression;
    private TeleportTrigger selectedChapterTeleport;
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

    private void OnDestroy()
    {
        RestoreGameplay();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void OnBossCleared() => TryShowGunSelection(nextChapterTeleport);

    internal bool TryShowGunSelection(TeleportTrigger chapterTeleport)
    {
        if (ownsChapterPause || isGameOver)
        {
            return true;
        }

        if (gunSelectionPrefab == null || gameplayCanvas == null || playerGun == null
            || chapterTeleport == null || !chapterTeleport.isActiveAndEnabled)
        {
            Debug.LogError("챕터 클리어에 필요한 UI, 플레이어 무기, 텔레포트를 연결해 주세요.", this);
            return false;
        }

        selectedChapterTeleport = chapterTeleport;
        gunSelection = Instantiate(gunSelectionPrefab, gameplayCanvas.transform, false);
        gameplayTimeScale = Time.timeScale;
        ownsChapterPause = true;
        Time.timeScale = 0f;
        previousGunInputSuppression = playerGun.InputSuppressed;
        playerGun.InputSuppressed = true;

        if (mouseReticle != null && mouseReticle.transform.parent == gameplayCanvas.transform)
        {
            reticleSiblingIndex = mouseReticle.transform.GetSiblingIndex();
            mouseReticle.transform.SetAsLastSibling();
        }
        return true;
    }

    public void SelectGun(GunSO gun)
    {
        if (gunSelection == null || isGameOver || gun == null || !gun.CanFire)
        {
            return;
        }

        GameObject selectionPanel = gunSelection.gameObject;
        gunSelection = null;
        selectionPanel.SetActive(false);
        Destroy(selectionPanel);
        playerGun.EquipGun(gun);
        StartCoroutine(MoveToNextChapter(selectedChapterTeleport));
    }

    private IEnumerator MoveToNextChapter(TeleportTrigger chapterTeleport)
    {
        Coroutine teleport = chapterTeleport != null ? chapterTeleport.StartPlayerTeleport() : null;
        if (teleport != null)
        {
            yield return teleport;
        }
        RestoreGameplay();
    }

    private void RestoreGameplay()
    {
        if (!ownsChapterPause)
        {
            return;
        }

        ownsChapterPause = false;
        Time.timeScale = gameplayTimeScale;
        selectedChapterTeleport = null;
        if (playerGun != null)
        {
            playerGun.InputSuppressed = previousGunInputSuppression;
        }
        if (reticleSiblingIndex >= 0 && mouseReticle != null)
        {
            mouseReticle.transform.SetSiblingIndex(reticleSiblingIndex);
        }
        reticleSiblingIndex = -1;
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
