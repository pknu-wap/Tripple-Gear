
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
            || chapterTeleport == null || !chapterTeleport.CanStartPlayerTeleport)
        {
            Debug.LogError("챕터 클리어에 필요한 UI, 플레이어 무기, 텔레포트를 연결해 주세요.", this);
            return false;
        }

        gunSelection = Instantiate(gunSelectionPrefab, gameplayCanvas.transform, false);
        if (!gunSelection.Show(playerGun, SelectGun))
        {
            gunSelection.gameObject.SetActive(false);
            Destroy(gunSelection.gameObject);
            gunSelection = null;
            return false;
        }

        selectedChapterTeleport = chapterTeleport;
        gameplayTimeScale = Time.timeScale;
        ownsChapterPause = true;
        Time.timeScale = 0f;
        GameplayInput.Block(this);

        if (mouseReticle != null && mouseReticle.transform.parent == gameplayCanvas.transform)
        {
            reticleSiblingIndex = mouseReticle.transform.GetSiblingIndex();
            mouseReticle.transform.SetAsLastSibling();
        }
        return true;
    }

    private void SelectGun(GunSO gun, int damageIncrease)
    {
        if (gunSelection == null || isGameOver || gun == null || !gun.CanFire)
        {
            return;
        }

        Coroutine teleport = selectedChapterTeleport != null ? selectedChapterTeleport.StartPlayerTeleport() : null;
        if (teleport == null)
        {
            Debug.LogWarning("다음 챕터 이동을 시작할 수 없습니다.", this);
            return;
        }

        GameObject selectionPanel = gunSelection.gameObject;
        gunSelection = null;
        selectionPanel.SetActive(false);
        Destroy(selectionPanel);
        playerGun.EquipChapterReward(gun, damageIncrease);
        StartCoroutine(MoveToNextChapter(teleport));
    }

    private IEnumerator MoveToNextChapter(Coroutine teleport)
    {
        yield return teleport;
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
        GameplayInput.Unblock(this);
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
