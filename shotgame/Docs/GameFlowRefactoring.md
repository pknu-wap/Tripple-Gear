# 게임 진행 구조 개편

> 2026-11-05: 시작 방식은 신호 기반으로 변경됐다. 아래 내용은 최초 개편 기록이며, 현재 연결과 사용법은 [GameFlowSignals.md](GameFlowSignals.md)를 따른다. 자동 진행은 기본적으로 꺼져 있고 로비 새게임 또는 인게임 시작 버튼의 BeginGame 신호로 시작한다.

## 선택한 방식

인덱스 기반 순차 진행이다. `chapters[0]`부터 시작하고 현재 챕터의 완료 통보를 받으면 다음 인덱스로 이동한다. 세 챕터를 위해 새 씬을 만들 필요가 없다. `SequenceState`는 시작 중복과 취소 여부를 관리하는 보조 상태이며, 챕터 선택은 switch가 아닌 배열 인덱스로 처리한다.

## 책임과 기존 코드의 이동

| 구성 요소 | 책임 |
| --- | --- |
| LobbyMenuController | 새게임 버튼으로 인게임 씬 로드 |
| InGameSequenceController | 씬에서 자동 시작, 인트로 완료 대기, 챕터 순서, 전체 완료 알림 |
| TimelineCutscenePlayer | 기존 GameFlowController의 타임라인 재생·종료 감지·마지막 프레임 고정 |
| ChapterController | 개별 챕터 시작/완료/취소 이벤트와 중복 완료 방지 |
| WaveManager | 기존 전투 웨이브 실행과 완료 알림 |

`GameFlowController.cs`를 `TimelineCutscenePlayer.cs`로 이름을 바꾸고 기존 meta GUID를 유지했다. `시네마틱샘플` 씬의 타임라인 참조와 종료 옵션은 유지하고, 별도 진행기와 시작 버튼 연결을 추가했다. 진행기에서 로비 출발 신호를 검사하지 않는다. 더 이상 사용하는 곳이 없는 SceneTransitionSignalReceiver와 로비의 수신 오브젝트를 제거했다. 로비 생성 도구도 새 구조와 같은 시작 씬을 사용한다.

## 현재 샘플과 챕터 연결 방법

현재 실제 새게임 목적지는 `시네마틱샘플`이다. 이 씬의 `InGameSequence` 오브젝트는 자동으로 인트로를 재생한다. 챕터 콘텐츠는 아직 등록되지 않았으므로 컷신 완료 후 `Playing`에 머물며 `플레이 시작` 이벤트를 한 번 호출한다. 빈 목록을 전체 클리어로 처리하지 않는다.

1. 인게임 씬에 활성 상태인 관리 오브젝트 세 개를 만들고 각각 ChapterController를 붙인다.
2. InGameSequenceController의 `진행 순서`에 챕터 1, 2, 3을 순서대로 등록한다.
3. 각 챕터의 `챕터 시작` 이벤트에 구역 활성화, 플레이어 배치, 컷신 또는 전투 시작 등을 연결한다.
4. 챕터의 마지막 목표가 달성되면 해당 ChapterController의 `CompleteChapter()`를 호출한다. 마지막 웨이브가 곧 챕터 완료 조건이라면 그 WaveManager의 `웨이브 완료 이벤트`에 연결한다. 여러 목표가 필요하면 목표를 모두 확인한 뒤 호출한다.
5. `챕터 완료` 이벤트에는 이전 구역 정리 등을, 진행기의 `전체 챕터 완료`에는 엔딩을 연결한다.

관리 오브젝트는 계속 활성화하고 콘텐츠 구역을 별도로 켜고 끈다. 챕터 컨트롤러 자체를 아직 비활성인 구역 안에 넣지 않는다. 중복/빈 참조/다른 씬/이미 시작한 챕터는 시작 전에 거부한다. 현재 챕터가 아닌 완료 통보와 중복 완료는 진행시키지 않는다.

인트로를 사용하지 않는 씬은 `인트로`를 비워 두면 바로 플레이 단계로 진입한다. 자동 시작을 끄면 `BeginSequence()`로 명시적으로 시작한다. 타임라인 재생기가 연결돼 있지만 Director/에셋이 빠졌다면 오류를 표시하고 Ready로 돌아간다. 중간에 컷신을 정지하면 다시 시작할 수 있다. 진행기 비활성화나 `CancelSequence()`는 현재 실행을 취소하며, 취소/전체 완료 후 재시작은 씬을 다시 로드해 새 실행으로 시작한다.

컷신 재생기는 자신의 자식 Director만 자동 탐색한다. 다른 오브젝트의 Director는 Inspector에서 명시적으로 연결한다. 여러 컷신이 있는 씬에서 임의의 첫 Director를 선택하지 않는다. 단발 컷신이므로 재생 시 Hold 모드를 사용해 프레임 지연이나 Timeline의 종료 시간 초기화 때문에 완료를 놓치지 않게 한다.

## 검증 재실행

Unity 6000.3.21f1에서 프로젝트를 닫은 상태로 실행한다. 검증은 샘플 씬을 저장하지 않는다. `-quit`은 넣지 않는다. 테스트가 직접 종료 코드를 반환한다.

```powershell
$p = Start-Process -FilePath 'C:/Program Files/Unity 6000.3.21f1/Editor/Unity.exe' -ArgumentList @('-batchmode', '-nographics', '-projectPath', '"C:/Users/HP/Desktop/Tripple-Gear/shotgame"', '-executeMethod', 'GameFlowValidation.Run', '-logFile', '"C:/Users/HP/Desktop/Tripple-Gear/tmp/gameflow-validation.log"') -WindowStyle Hidden -PassThru
$p.WaitForExit()
$p.ExitCode
```

검증 코드는 3챕터 순서, 미래/중복 완료, 중복 시작, 즉시 완료 콜백, 취소, 빈 목록, 잘못된 설정 거부, 기존 씬의 참조 및 누락 스크립트를 확인한다. 이어 실제 Play Mode에서 로비 없이 샘플 씬 실행, 인트로 취소와 재시도, 자연 재생 완료 후 플레이 단계 진입 및 캐릭터 활성 상태 3초 유지, 로비를 로드하고 새게임으로 다시 진입하는 경로를 확인한다. 컷신의 전체 영상을 시각적으로 검사하는 테스트는 아니다.


## 실행 결과 (2026-11-04)

Unity 6000.3.21f1 배치 검증 종료 코드 0. 편집 모드 검증 및 실제 Play Mode 검증 모두 통과했다. 기존 인게임 씬의 누락 스크립트가 없고, 기존 Director 및 버튼 연결도 확인했다. 직접 인게임 씬 실행과 로비 새게임 경로 모두 인트로를 시작한다. 인트로 취소 후 재시도 및 컷신 종료 후 Playing 진입을 확인했다.

세 챕터 순차 진행 검증은 테스트가 만든 ChapterController 세 개로 수행했다. 실제 세 챕터의 콘텐츠/목표/엔딩은 아직 프로젝트에 없으므로 씬에 임의로 제작하지 않았다. 실제 콘텐츠 연결 이후 전체 플레이와 컷신의 화면 연출은 별도 확인해야 한다. 잘못된 챕터 설정 두 건의 오류 로그는 거부 동작을 확인하기 위해 의도적으로 발생시킨 것이다.


## 캐릭터 비활성화 후속 수정

플레이어 Activation Track의 Post-playback State를 LeaveAsIs(3)에서 Active(0)로 변경했다. 활성화 클립의 끝에서 비활성화되더라도 Director 정지 시 플레이어가 활성 상태로 남는다. 기존 통과 검증은 캐릭터 활성 상태를 확인하지 않았으므로, 이번에 자연 재생과 종료 후 3초 활성 유지 검사를 추가했다. 이번 변경 후에는 사용자 Unity 편집기가 프로젝트를 사용 중이어서 배치 Play Mode 검증을 재실행하지 않았다.
