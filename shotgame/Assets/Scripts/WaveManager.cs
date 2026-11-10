using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[DefaultExecutionOrder(1000)]
public sealed class WaveManager : MonoBehaviour
{
    private const string CameraBoundaryLayerName = "WaveBoundary";
    private const float CameraBoundaryThickness = 1f;

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
    private readonly BoxCollider2D[] cameraBoundaries = new BoxCollider2D[4];

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
                Debug.LogWarning("WaveManager: 직교 카메라, WaveBoundary 레이어, 씬의 몬스터를 확인하고 몬스터의 부모를 활성화해 주세요.", this);
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
        CreateCameraBoundaries();

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
        if (cameraManager == null || cameraManager.ManagedCamera == null
            || !cameraManager.ManagedCamera.orthographic
            || LayerMask.NameToLayer(CameraBoundaryLayerName) < 0 || monsters == null)
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
        UpdateCameraBoundaries();
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
        RemoveCameraBoundaries();
        if (lockedManager != null)
        {
            lockedManager.enabled = cameraWasEnabled;
        }
        lockedManager = null;
        lockedCamera = null;
        waveMonsters.Clear();
    }

    private void CreateCameraBoundaries()
    {
        int boundaryLayer = LayerMask.NameToLayer(CameraBoundaryLayerName);
        string[] names = { "Left", "Right", "Bottom", "Top" };

        for (int i = 0; i < cameraBoundaries.Length; i++)
        {
            GameObject boundary = new GameObject("Wave Camera Boundary " + names[i]);
            boundary.layer = boundaryLayer;
            cameraBoundaries[i] = boundary.AddComponent<BoxCollider2D>();
        }

        UpdateCameraBoundaries();
    }

    private void UpdateCameraBoundaries()
    {
        if (lockedCamera == null)
        {
            return;
        }

        Vector3 center = lockedCamera.transform.position;
        float halfHeight = lockedCamera.orthographicSize;
        float halfWidth = halfHeight * lockedCamera.aspect;
        float thickness = CameraBoundaryThickness;
        float halfThickness = thickness * 0.5f;

        SetCameraBoundary(0, center.x - halfWidth - halfThickness, center.y, thickness, halfHeight * 2f + thickness * 2f);
        SetCameraBoundary(1, center.x + halfWidth + halfThickness, center.y, thickness, halfHeight * 2f + thickness * 2f);
        SetCameraBoundary(2, center.x, center.y - halfHeight - halfThickness, halfWidth * 2f + thickness * 2f, thickness);
        SetCameraBoundary(3, center.x, center.y + halfHeight + halfThickness, halfWidth * 2f + thickness * 2f, thickness);
        Physics2D.SyncTransforms();
    }

    private void SetCameraBoundary(int index, float x, float y, float width, float height)
    {
        BoxCollider2D boundary = cameraBoundaries[index];
        boundary.transform.position = new Vector3(x, y, 0f);
        boundary.size = new Vector2(width, height);
    }

    private void RemoveCameraBoundaries()
    {
        for (int i = 0; i < cameraBoundaries.Length; i++)
        {
            BoxCollider2D boundary = cameraBoundaries[i];
            if (boundary == null)
            {
                continue;
            }

            boundary.gameObject.SetActive(false);
            Destroy(boundary.gameObject);
            cameraBoundaries[i] = null;
        }
    }

    private void OnDisable()
    {
        CancelWave();
    }
}
