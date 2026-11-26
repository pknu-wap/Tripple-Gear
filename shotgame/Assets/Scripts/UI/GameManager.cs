
using System.Collections;
using System.Collections.Generic;
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

    [Header("챕터 재시작")]
    [SerializeField, KoreanLabel("카메라 매니저")] private CameraManager retryCamera;
    [SerializeField, KoreanLabel("카메라 영역별 챕터 시작 이동")] private TeleportTrigger[] retryRoutesByCameraArea;
    [SerializeField, KoreanLabel("항해 중 재시작 이동")] private TeleportTrigger voyageRetryRoute;

    private GunSelectionUI gunSelection;
    private readonly object gameOverPause = new object();
    private bool ownsChapterPause;
    private int reticleSiblingIndex = -1;
    private TeleportTrigger selectedChapterTeleport;
    private bool isGameOver = false;

    private bool retryInProgress;
    private int defeatedCameraArea = -1;
    private bool defeatedDuringVoyage;

    private void Awake()
    {
        Instance = this;

        if (gameOverUI != null)
        {
            gameOverUI.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        RestoreGameplay();
        GameplayPause.Release(gameOverPause);
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
        ownsChapterPause = true;
        GameplayPause.Acquire(this);
        GameplayInput.Block(this);

        if (mouseReticle != null && mouseReticle.transform.parent == gameplayCanvas.transform)
        {
            reticleSiblingIndex = mouseReticle.transform.GetSiblingIndex();
            mouseReticle.transform.SetAsLastSibling();
        }
        return true;
    }

    private void SelectGun(GunSO gun, IReadOnlyDictionary<GunSO, int> damageIncreases)
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
        playerGun.EquipChapterReward(gun, damageIncreases);
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
        GameplayPause.Release(this);
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
        TryShowGameOver();
    }

    internal bool TryShowGameOver()
    {
        if (gameOverUI == null) return false;
        if (isGameOver) return true;

        defeatedCameraArea = retryCamera != null ? retryCamera.ActiveCameraAreaIndex : -1;
        defeatedDuringVoyage = playerGun != null && playerGun.TryGetComponent(out ArisaHorizontalMovement player)
            && player.IsInBoatSection;
        isGameOver = true;

        gameOverUI.SetActive(true);
        GameplayPause.Acquire(gameOverPause);
        return true;
    }

    public void Retry()
    {
        if (retryInProgress) return;

        Scene currentScene = gameObject.scene;
        string scenePath = currentScene.path;
        int cameraArea = defeatedCameraArea;
        bool wasVoyaging = defeatedDuringVoyage;
        GunSO equipped = playerGun != null ? playerGun.CurrentGun : null;
        var damageBonuses = playerGun != null ? playerGun.CopyChapterDamageBonuses() : null;

        retryInProgress = true;
        SceneManager.sceneLoaded += ResumeAtChapterStart;
        SceneManager.LoadScene(currentScene.name);

        void ResumeAtChapterStart(Scene scene, LoadSceneMode mode)
        {
            if (mode != LoadSceneMode.Single) return;
            SceneManager.sceneLoaded -= ResumeAtChapterStart;

            GameManager manager = Instance;
            if (scene.path != scenePath || manager == null || manager.gameObject.scene != scene) return;

            if (equipped != null && manager.playerGun != null)
            {
                manager.playerGun.EquipChapterReward(equipped, damageBonuses);
            }
            TeleportTrigger route = manager.GetRetryRoute(cameraArea, wasVoyaging);
            if (route != null) route.PlacePlayerAtDestination();
        }
    }

    private TeleportTrigger GetRetryRoute(int cameraArea, bool wasVoyaging)
    {
        if (wasVoyaging && voyageRetryRoute != null) return voyageRetryRoute;

        return retryRoutesByCameraArea != null && cameraArea >= 0 && cameraArea < retryRoutesByCameraArea.Length
            ? retryRoutesByCameraArea[cameraArea] : null;
    }

    // 로비로 이동
    public void GoToLobby()
    {
        SceneManager.LoadScene(lobbySceneName);
    }
}
