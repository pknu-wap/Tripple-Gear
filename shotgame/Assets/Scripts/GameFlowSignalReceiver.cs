using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public sealed class GameFlowSignalReceiver : MonoBehaviour
{
    [SerializeField, KoreanLabel("받을 신호 이름")] private string signalName = GameFlowSignals.BeginGame;
    [SerializeField, KoreanLabel("한 번만 실행")] private bool executeOnce = true;
    [SerializeField, KoreanLabel("신호를 받으면 실행")] private UnityEvent onReceived = new UnityEvent();
    private bool started;
    private bool pending;
    private bool executed;
    private string registeredSignal;

    private void OnEnable()
    {
        registeredSignal = signalName;
        GameFlowSignals.Register(gameObject.scene, registeredSignal, Receive);
    }

    private void Start()
    {
        started = true;
        if (pending)
        {
            pending = false;
            Receive();
        }
    }

    private void Receive()
    {
        if (!isActiveAndEnabled || (executeOnce && executed)) return;
        // sceneLoaded happens before Start; wait until the scene's Awake/OnEnable have finished.
        if (!started)
        {
            pending = true;
            return;
        }
        executed = true;
        onReceived.Invoke();
    }

    private void OnDisable()
    {
        GameFlowSignals.Unregister(gameObject.scene, registeredSignal, Receive);
        pending = false;
    }
}
