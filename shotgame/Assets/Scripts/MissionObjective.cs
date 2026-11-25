using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
public sealed class MissionObjective : MonoBehaviour
{
    public enum GoalType
    {
        [InspectorName("수량")] Count,
        [InspectorName("시간")] Duration,
        [InspectorName("지정 몬스터 전멸")] DefeatTargets,
    }

    private enum BoatBattleMode
    {
        [InspectorName("항해 중 전투")] Sailing,
        [InspectorName("정박 후 전투")] Docked,
    }

    [SerializeField, KoreanLabel("목표 종류")] private GoalType goalType;
    [SerializeField, KoreanLabel("목표 수량"), Min(1)] private int requiredCount = 1;
    [SerializeField, KoreanLabel("버틸 시간 (초)"), Min(0.01f)] private float requiredSeconds = 10f;
    [SerializeField, KoreanLabel("지정 몬스터 (씬 오브젝트)")] private MonsterHealth[] targetMonsters;
    [SerializeField] private PlacedMonsterEntrance[] targetEntrances;
    [SerializeField, KoreanLabel("미션 시작 전에도 대상 표시")] private bool keepTargetsVisibleBeforeStart;
    [SerializeField, KoreanLabel("항해 전투 보트")] private BoatVoyage missionBoat;
    [SerializeField, KoreanLabel("항해 전투 방식")] private BoatBattleMode boatBattleMode;
    [SerializeField, KoreanLabel("목표 시작 이벤트")] private UnityEvent onStarted = new UnityEvent();
    [SerializeField, KoreanLabel("목표 종료 이벤트")] private UnityEvent onEnded = new UnityEvent();
    [SerializeField, KoreanLabel("진행도 변경 이벤트 (0~1)")] private UnityEvent<float> onProgressChanged = new UnityEvent<float>();

    public bool IsRunning { get; private set; }
    public bool IsCompleted { get; private set; }
    public int Count { get; private set; }
    public int RequiredCount => goalType == GoalType.DefeatTargets ? (targetMonsters?.Length ?? 0) : Mathf.Max(1, requiredCount);
    public float ElapsedSeconds { get; private set; }
    public int RemainingCount => IsRunning && goalType == GoalType.Count ? RequiredCount - Count : 0;
    public float Progress => goalType == GoalType.Duration
        ? ElapsedSeconds / Mathf.Max(0.01f, requiredSeconds)
        : (float)Count / Mathf.Max(1, RequiredCount);

    internal event Action<MissionObjective> Completed;
    internal Trigger Owner { get; set; }
    internal BoatVoyage BattleBoat => goalType == GoalType.DefeatTargets ? missionBoat : null;
    internal bool DockBeforeBattle => boatBattleMode == BoatBattleMode.Docked;
    private readonly HashSet<MonsterHealth> defeatedTargets = new HashSet<MonsterHealth>();
    private readonly Dictionary<MonsterHealth, UnityAction> targetListeners = new Dictionary<MonsterHealth, UnityAction>();
    private (Vector3 position, Quaternion rotation, Vector3 scale)[] targetPoses;

    private void Awake()
    {
        if (goalType != GoalType.DefeatTargets || targetMonsters == null) return;

        targetPoses = new (Vector3, Quaternion, Vector3)[targetMonsters.Length];
        for (int i = 0; i < targetMonsters.Length; i++)
        {
            MonsterHealth target = targetMonsters[i];
            if (target == null) continue;
            Transform targetTransform = target.transform;
            targetPoses[i] = (targetTransform.localPosition, targetTransform.localRotation, targetTransform.localScale);
            if (keepTargetsVisibleBeforeStart)
            {
                target.RequiredDamageObjective = this;
                target.gameObject.SetActive(true);
            }
        }
    }

    internal bool HasValidTargets()
    {
        if (goalType != GoalType.DefeatTargets) return true;
        if (targetMonsters == null || targetMonsters.Length == 0) return false;

        var unique = new HashSet<MonsterHealth>();
        foreach (MonsterHealth target in targetMonsters)
        {
            if (target == null || target.gameObject.scene != gameObject.scene || target.Data == null
                || target.gameObject == gameObject || !unique.Add(target)
                || (target.transform.parent != null && !target.transform.parent.gameObject.activeInHierarchy)
                || PlacedMonsterEntrance.Find(targetEntrances, target.gameObject)?.HasValidStart(target.gameObject) == false)
            {
                return false;
            }
        }

        return true;
    }

    internal void ResetProgress()
    {
        IsCompleted = false;
        Count = 0;
        ElapsedSeconds = 0f;
        defeatedTargets.Clear();
        if (targetPoses == null) return;

        for (int i = 0; i < targetMonsters.Length; i++)
        {
            MonsterHealth target = targetMonsters[i];
            if (target == null) continue;
            if (!keepTargetsVisibleBeforeStart) target.gameObject.SetActive(false);
            var pose = targetPoses[i];
            target.transform.localPosition = pose.position;
            target.transform.localRotation = pose.rotation;
            target.transform.localScale = pose.scale;
            target.ResetForMission();
            if (keepTargetsVisibleBeforeStart) target.gameObject.SetActive(true);
        }
    }

    internal void Begin()
    {
        IsRunning = true;
        if (goalType == GoalType.DefeatTargets)
        {
            Camera view = Camera.main;
            foreach (MonsterHealth target in targetMonsters)
            {
                UnityAction listener = () => RecordTargetDefeat(target);
                targetListeners.Add(target, listener);
                target.Defeated.AddListener(listener);
                if (target.TryGetComponent(out NormalMonsterChase chase))
                {
                    chase.BeginEncounterApproach(null, view);
                }
                Vector3 destination = target.transform.position;
                target.gameObject.SetActive(true);
                if (!IsRunning) return;
                PlacedMonsterEntrance.Find(targetEntrances, target.gameObject)?.Play(target, destination, view);
            }
        }
        onStarted.Invoke();
    }

    internal void Stop()
    {
        if (!IsRunning)
        {
            return;
        }

        IsRunning = false;
        StopTrackingTargets(hideTargets: !IsCompleted && !keepTargetsVisibleBeforeStart);
        onEnded.Invoke();
    }

    private void OnDestroy() => StopTrackingTargets(hideTargets: false);

    private void StopTrackingTargets(bool hideTargets)
    {
        foreach (var entry in targetListeners)
        {
            if (entry.Key == null) continue;
            entry.Key.Defeated.RemoveListener(entry.Value);
            if (entry.Key.TryGetComponent(out NormalMonsterChase chase))
            {
                chase.EndEncounterApproach();
            }
            if (hideTargets) entry.Key.gameObject.SetActive(false);
            else if (entry.Key.TryGetComponent(out MonsterEntranceMotion motion)) motion.FinishWithoutEffect();
        }
        targetListeners.Clear();
    }

    private void RecordTargetDefeat(MonsterHealth target)
    {
        if (!IsRunning || !defeatedTargets.Add(target)) return;
        Count = defeatedTargets.Count;
        PublishProgress();
    }

    public void RecordSuccess() => AddProgress(1);

    public void AddProgress(int amount)
    {
        int added = Mathf.Min(amount, RemainingCount);
        if (added > 0)
        {
            Count += added;
            PublishProgress();
        }
    }

    private void Update()
    {
        if (IsRunning && goalType == GoalType.Duration && Time.deltaTime > 0f)
        {
            ElapsedSeconds = Mathf.Min(ElapsedSeconds + Time.deltaTime, Mathf.Max(0.01f, requiredSeconds));
            PublishProgress();
        }
    }

    private void PublishProgress()
    {
        bool reachedTarget = goalType == GoalType.Duration
            ? ElapsedSeconds >= Mathf.Max(0.01f, requiredSeconds)
            : Count >= RequiredCount;
        if (!reachedTarget)
        {
            onProgressChanged.Invoke(Progress);
            return;
        }

        IsCompleted = true;
        Stop();
        onProgressChanged.Invoke(Progress);
        Completed?.Invoke(this);
    }
}
