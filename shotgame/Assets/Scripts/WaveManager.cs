using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]
public sealed class WaveManager : MonoBehaviour
{
    public enum WaveState { Ready, Running, Completed, Cancelled }

    [SerializeField, KoreanLabel("카메라 매니저")] private CameraManager cameraManager;
    [SerializeField, KoreanLabel("활성화할 몬스터")] private GameObject[] monsters;
    [SerializeField, KoreanLabel("웨이브 시작 이벤트")] private UnityEvent waveStarted = new UnityEvent();
    [SerializeField, KoreanLabel("웨이브 완료 이벤트")] private UnityEvent waveCompleted = new UnityEvent();

    public WaveState State { get; private set; }
    public bool HasStarted => State != WaveState.Ready;
    public bool IsCompleted => State == WaveState.Completed;
    public UnityEvent WaveStarted => waveStarted;
    public UnityEvent WaveCompleted => waveCompleted;

    private readonly List<GameObject> waveMonsters = new List<GameObject>();
    private CameraManager lockedManager;
    private Camera lockedCamera;
    private Vector3 lockedPosition;
    private bool cameraWasEnabled;
    private bool reportedInvalidSetup;

    public bool TryStartWave()
    {
        if (!isActiveAndEnabled || HasStarted)
        {
            return false;
        }

        if (!PrepareWave())
        {
            if (!reportedInvalidSetup)
            {
                Debug.LogWarning("WaveManager: 카메라와 씬의 몬스터를 등록하고 몬스터의 부모를 활성화해 주세요.", this);
                reportedInvalidSetup = true;
            }
            return false;
        }

        // A disabled camera manager may already belong to another running wave.
        if (!cameraManager.isActiveAndEnabled)
        {
            return false;
        }

        lockedManager = cameraManager;
        lockedCamera = cameraManager.ManagedCamera;
        lockedPosition = lockedCamera.transform.position;
        cameraWasEnabled = lockedManager.enabled;
        State = WaveState.Running;
        lockedManager.enabled = false;

        for (int i = 0; i < waveMonsters.Count; i++)
        {
            if (State != WaveState.Running)
            {
                return true;
            }
            GameObject monster = waveMonsters[i];
            if (monster != null)
            {
                monster.SetActive(true);
            }
        }

        if (State == WaveState.Running)
        {
            waveStarted.Invoke();
        }
        return true;
    }

    private bool PrepareWave()
    {
        waveMonsters.Clear();
        if (cameraManager == null || cameraManager.ManagedCamera == null || monsters == null)
        {
            return false;
        }

        foreach (GameObject monster in monsters)
        {
            if (monster == null || waveMonsters.Contains(monster))
            {
                continue;
            }
            if (!monster.scene.IsValid() || monster.scene != gameObject.scene
                || transform.IsChildOf(monster.transform)
                || cameraManager.transform.IsChildOf(monster.transform)
                || (monster.transform.parent != null && !monster.transform.parent.gameObject.activeInHierarchy))
            {
                return false;
            }
            waveMonsters.Add(monster);
        }
        return waveMonsters.Count > 0;
    }

    private void LateUpdate()
    {
        if (State != WaveState.Running)
        {
            return;
        }
        if (lockedCamera == null || lockedManager == null)
        {
            CancelWave();
            return;
        }

        lockedCamera.transform.position = lockedPosition;
        foreach (GameObject monster in waveMonsters)
        {
            if (monster != null && monster.activeInHierarchy)
            {
                return;
            }
        }

        State = WaveState.Completed;
        RestoreCamera();
        waveCompleted.Invoke();
    }

    public void CancelWave()
    {
        if (State != WaveState.Running)
        {
            return;
        }
        State = WaveState.Cancelled;
        RestoreCamera();
    }

    private void RestoreCamera()
    {
        if (lockedManager != null)
        {
            lockedManager.enabled = cameraWasEnabled;
        }
        lockedManager = null;
        lockedCamera = null;
        waveMonsters.Clear();
    }

    private void OnDisable()
    {
        CancelWave();
    }
}
