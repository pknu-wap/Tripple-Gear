using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

[RequireComponent(typeof(VideoPlayer))]
public sealed class LobbyVideoBackground : MonoBehaviour
{
    [SerializeField] private Canvas lobbyCanvas;

    private VideoPlayer player;
    private RawImage background;

    private void Start()
    {
        if (lobbyCanvas == null) return;

        background = new GameObject("Lobby Video", typeof(RawImage)).GetComponent<RawImage>();
        RectTransform rect = background.rectTransform;
        rect.SetParent(lobbyCanvas.transform, false);
        rect.SetAsFirstSibling();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        background.raycastTarget = false;
        background.enabled = false;

        player = GetComponent<VideoPlayer>();
        player.renderMode = VideoRenderMode.APIOnly;
        player.audioOutputMode = VideoAudioOutputMode.None;
        player.isLooping = true;
        player.url = Application.streamingAssetsPath.TrimEnd('/', '\\') + "/ingame.mp4";
        player.Play();
    }

    private void Update()
    {
        if (player == null || background.enabled || player.texture == null) return;

        Texture frame = player.texture;
        background.texture = frame;
        var fit = background.gameObject.AddComponent<AspectRatioFitter>();
        fit.aspectRatio = (float)frame.width / frame.height;
        fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
        background.enabled = true;
    }
}
