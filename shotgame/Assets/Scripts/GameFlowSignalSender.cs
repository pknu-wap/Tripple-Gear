using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class GameFlowSignalSender : MonoBehaviour
{
    [SerializeField, KoreanLabel("보낼 신호 이름")] private string signalName = GameFlowSignals.BeginGame;
    [SerializeField, KoreanLabel("이동할 씬 (비우면 현재 씬)")] private string targetSceneName;
    [SerializeField, KoreanLabel("연결할 버튼 (선택)")] private Button button;

    private void OnEnable()
    {
        if (button == null) button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(Send);
    }

    // Can also be called by a trigger, a UnityEvent, or another script.
    public void Send()
    {
        if (string.IsNullOrWhiteSpace(targetSceneName)) GameFlowSignals.Send(gameObject.scene, signalName);
        else GameFlowSignals.LoadSceneAndSend(targetSceneName, signalName);
    }

    private void OnDisable()
    {
        if (button != null) button.onClick.RemoveListener(Send);
    }
}
