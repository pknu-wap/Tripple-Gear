using UnityEngine;

internal sealed class EncounterCameraLock
{
    private const string BoundaryLayerName = "WaveBoundary";
    private const float CameraBoundaryThickness = 1f;

    private readonly BoxCollider2D[] cameraBoundaries = new BoxCollider2D[4];
    private CameraManager lockedManager;
    private bool cameraWasEnabled;

    public Camera Camera { get; private set; }
    public Vector3 Position { get; set; }
    public bool IsLocked => Camera != null && lockedManager != null;

    public static bool CanLock(CameraManager manager)
    {
        return manager != null && manager.ManagedCamera != null && manager.ManagedCamera.orthographic
            && LayerMask.NameToLayer(BoundaryLayerName) >= 0;
    }

    public void Lock(CameraManager manager)
    {
        lockedManager = manager;
        Camera = manager.ManagedCamera;
        Position = Camera.transform.position;
        cameraWasEnabled = manager.enabled;
        manager.enabled = false;
        CreateCameraBoundaries();
    }

    public void Update()
    {
        Camera.transform.position = Position;
        UpdateCameraBoundaries();
    }

    public void Release()
    {
        RemoveCameraBoundaries();
        if (lockedManager != null)
        {
            if (cameraWasEnabled)
            {
                lockedManager.ResumeFollowAfterWave();
            }
            lockedManager.enabled = cameraWasEnabled;
        }
        lockedManager = null;
        Camera = null;
    }

    private void CreateCameraBoundaries()
    {
        int boundaryLayer = LayerMask.NameToLayer(BoundaryLayerName);
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
        if (Camera == null)
        {
            return;
        }

        Vector3 center = Camera.transform.position;
        float halfHeight = Camera.orthographicSize;
        float halfWidth = halfHeight * Camera.aspect;
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

    public void RemoveCameraBoundaries()
    {
        for (int i = 0; i < cameraBoundaries.Length; i++)
        {
            BoxCollider2D boundary = cameraBoundaries[i];
            if (boundary == null)
            {
                continue;
            }

            boundary.gameObject.SetActive(false);
            Object.Destroy(boundary.gameObject);
            cameraBoundaries[i] = null;
        }
    }
}
