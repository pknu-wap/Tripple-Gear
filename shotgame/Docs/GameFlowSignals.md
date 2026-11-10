# 신호로 컷신과 이벤트 시작하기

## 현재 적용된 동작

로비 새게임은 `GameFlowSignals.LoadSceneAndSend("시네마틱샘플", "BeginGame")`을 호출한다. 전달 코드는 대상 씬의 빌드 인덱스와 신호 이름을 보관한 후 씬을 로드한다. 도착한 씬의 수신기에 요청을 한 번 전달하고 보관한 요청을 지운다.

시네마틱샘플의 `InGameSequence`에는 `GameFlowSignalReceiver`가 붙어 있다. 받을 신호는 `BeginGame`, 연결된 이벤트는 `InGameSequenceController.BeginSequence()`다. 수신기는 OnEnable에서 등록하고, Start 이전에 도착한 요청은 Start까지 보관한다. 이 덕분에 다른 오브젝트의 Awake가 실행되기 전에 컷신을 재생하지 않는다.

같은 씬의 `GameStartButton`에는 `GameFlowSignalSender`가 붙어 있다. 이동할 씬은 비어 있고 신호는 `BeginGame`이다. 클릭하면 같은 씬의 수신기에 즉시 전달한다. 기존 버튼의 직접 BeginSequence 호출은 제거해서 수신기를 우회하지 않는다.

InGameSequenceController의 시작 버튼 참조와 숨기기 옵션은 제거했다. 진행기는 버튼 표시나 클릭 가능 여부를 변경하지 않는다. 버튼은 신호 발신기에 연결된 상태로 유지되며, 진행 중 들어오는 중복 시작 요청은 진행기의 상태 검사로 무시한다. 버튼을 숨기는 UI가 필요하면 별도 UI 코드에서 처리한다.

인게임 진행기의 자동 진행은 기본값과 샘플 씬 모두 껐다. 씬을 직접 실행하면 요청을 기다리고, 로비 없이도 인게임 시작 버튼으로 진행할 수 있다. 테스트용 자동 진행을 명시적으로 켜는 것도 가능하다.

```text
로비 새게임 → 씬 전환 + BeginGame 보관 ─┐
                                      ↓
인게임 버튼 → BeginGame 전달 → 수신기 → BeginSequence → 컷신 → 챕터
```

## 각 코드의 책임

| 코드 | 책임 |
| --- | --- |
| GameFlowSignals | 씬과 신호 이름으로 전달 대상 식별, 씬 전환 요청 보관과 소비 |
| GameFlowSignalSender | 버튼 또는 다른 코드의 요청을 신호로 보내기 |
| GameFlowSignalReceiver | 신호 수신과 Inspector에 등록한 이벤트 실행 |
| InGameSequenceController | 인트로와 챕터 진행 순서 |
| TimelineCutscenePlayer | 실제 컷신 재생과 완료 알림 |

## 다른 버튼 연결하기

1. 버튼 오브젝트에 GameFlowSignalSender를 추가한다. 같은 오브젝트의 Button은 자동 연결된다.
2. `보낼 신호 이름`을 지정한다. 예: `OpenGate`, `BossIntro`.
3. 같은 씬에서 실행하면 `이동할 씬`은 비운다. 다른 씬으로 이동하면서 실행하면 해당 씬 이름 또는 `Assets/Scenes/...unity` 경로를 넣는다. 빌드 목록에 활성화된 씬이어야 한다.
4. 목적지 씬의 활성 오브젝트에 GameFlowSignalReceiver를 추가한다.
5. `받을 신호 이름`을 정확히 일치시킨다. 대소문자를 구분한다.
6. `신호를 받으면 실행`에 대상 오브젝트와 public 메서드를 연결한다.

버튼의 On Click에 Send를 별도로 추가할 필요는 없다. 발신기가 클릭 이벤트를 등록한다. 이미 있는 별도 UnityEvent나 트리거에서 사용하려면 발신기의 `Send()`를 호출하면 된다.

## 어떤 함수를 연결할까?

| 원하는 작업 | 수신기에 연결할 함수 |
| --- | --- |
| 인트로부터 게임 전체 진행 시작 | InGameSequenceController.BeginSequence |
| 독립적인 컷신 하나 재생 | TimelineCutscenePlayer.Play |
| 문/구역 오브젝트 활성화 | GameObject.SetActive (true) |
| 현재 챕터 완료 통보 | 해당 ChapterController.CompleteChapter |

독립 컷신의 완료 후 작업은 재생기의 `완료 후 호출`에 연결한다. 게임 진행의 인트로는 BeginSequence를 통해 시작해야 진행기가 완료 이벤트를 구독하고 다음 단계로 넘어간다.

한 수신기에 여러 이벤트를 연결할 수 있다. 같은 씬과 신호 이름에 수신기를 여러 개 만들면 오류로 거부한다. 한 오브젝트에는 수신기 하나를 붙일 수 있으므로 여러 신호를 받으려면 관리용 자식 오브젝트별로 수신기를 배치한다.

`한 번만 실행`은 기본적으로 켜져 있다. 문 열기처럼 단발 이벤트에 적합하다. 반복 가능한 이벤트는 끈다. 샘플의 BeginGame 수신기는 이 옵션을 껐고, 진행기가 중복 시작을 차단한다. 그래서 컷신 취소 후에는 같은 신호로 재시도할 수 있다.

## 코드에서 직접 보내기

```csharp
// 같은 씬에 있는 이름이 일치하는 수신기에 보낸다.
GameFlowSignals.Send(gameObject.scene, "OpenGate");

// 다른 씬을 로드한 뒤 그 씬의 수신기에 보낸다.
GameFlowSignals.LoadSceneAndSend("시네마틱샘플", GameFlowSignals.BeginGame);
```

같은 씬의 수신기가 없거나 비활성 상태면 경고하고 버린다. 다음에 우연히 실행되지 않도록 미수신 요청을 계속 보관하지 않는다. 다른 씬 요청은 한 번에 하나만 허용하며 Single 로드로 이동한다. 다른 씬이 먼저 로드되면 보관한 요청을 취소한다. 이미 로드된 다른 Additive 씬에 요청만 보내려면 그 Scene을 명시해 Send를 호출해야 한다.

초기화 중 Start 이전에 같은 수신기로 들어온 요청은 한 번으로 합쳐서 처리한다. 이벤트 콜백을 실행하기 전에 수신 완료 상태를 기록하므로 단발 이벤트가 자신에게 다시 신호를 보내도 반복 실행되지 않는다. OnDisable에서 등록을 해제하고, Play 진입 시 정적 상태를 초기화한다.

## 검증

GameFlowValidation은 직접 씬 실행 시 대기, 같은 씬 버튼 신호, 일반 UnityEvent 실행, 씬별 신호 분리, 없는 신호/잘못된 씬 거부, 로비 새게임 요청, 재사용 발신기의 씬 전환, 소비한 요청이 씬 재진입 때 반복되지 않는지를 확인한다. 기존 챕터 순서와 컷신 자연 재생 및 종료 후 캐릭터 활성 상태 3초 유지도 확인한다.


2026-11-05 실행 결과: Unity 6000.3.21f1의 별도 프로젝트 복사본에서 컴파일과 편집/Play Mode 기능 검증 통과, 종료 코드 0. 원본과 검증한 스크립트 및 시네마틱샘플 씬의 일치를 확인했다. 시작 버튼의 비활성 부모 캔버스도 활성화했다. 검증 로그에는 UnityEditor.Search 검색 인덱서 자체의 예외가 있었으나 게임 코드의 컴파일 오류 없이 기능 검증은 완료됐다. 원본에서 열려 있는 Unity 편집기는 종료하거나 씬을 전환하지 않았다.
