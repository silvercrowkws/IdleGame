using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Idle 상태가 한 바퀴 끝날 때마다 다음 행동을 확률로 결정한다.
///  - idleRepeatChance(기본 50%) : 그냥 Idle 한 번 더
///  - 나머지(50%)               : 등록된 액션 Trigger 중 하나를 균등 확률로 발사
///                                (기본 4개 → 각각 12.5%: EyeBlink, LookAround, Surprise, Stomp)
///
/// Animator 설정:
///  - Idle → 각 액션 : Condition = 해당 Trigger, Has Exit Time = OFF, Transition Duration = 0
///  - 각 액션 → Idle : Has Exit Time = ON (Exit Time 1), Transition Duration = 0 (조건 없음)
///  - Idle 클립은 Loop Time = ON
///  - Idle 상태의 Tag 에 "Idle" 입력
///
/// 캐릭터에 없는 액션(예: Woodcarver 에 Surprise/Stomp 가 아직 없는 경우)은
/// Animator 에 해당 Trigger 파라미터가 없으면 자동으로 후보에서 빠지고,
/// 남은 액션끼리 균등하게 나눠 가진다.
/// </summary>
[RequireComponent(typeof(Animator))]
public class CharacterController : MonoBehaviour
{
    [Header("Animator")]
    [Tooltip("Idle 상태에 지정한 Animator Tag")]
    [SerializeField] private string idleTag = "Idle";
    [SerializeField] private int layer = 0;

    [Header("확률")]
    [Range(0f, 1f)][SerializeField] private float idleRepeatChance = 0.5f;

    [Header("Idle 이후 실행할 액션 Trigger (균등 확률)")]
    [SerializeField]
    private string[] actionTriggers =
    {
        "EyeBlink", "LookAround", "Surprise", "Stomp"
    };

    private Animator animator;
    private readonly List<int> actionHashes = new List<int>();

    // Idle 에 진입한 뒤 몇 바퀴째까지 판정했는지
    private int judgedLoop = 0;
    private bool wasInIdle = false;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        BuildActionList();
    }

    private void OnEnable()
    {
        wasInIdle = false;
        judgedLoop = 0;
    }

    // Animator 에 실제로 존재하는 Trigger 만 후보로 등록
    private void BuildActionList()
    {
        actionHashes.Clear();
        foreach (string name in actionTriggers)
        {
            if (string.IsNullOrEmpty(name)) continue;
            int hash = Animator.StringToHash(name);
            if (HasTrigger(hash)) actionHashes.Add(hash);
            else Debug.LogWarning($"[{nameof(CharacterController)}] '{name}' Trigger 가 Animator 에 없어 제외합니다.", this);
        }
    }

    private bool HasTrigger(int hash)
    {
        foreach (var p in animator.parameters)
            if (p.nameHash == hash && p.type == AnimatorControllerParameterType.Trigger) return true;
        return false;
    }

    private void Update()
    {
        // 전환 중에는 판정하지 않는다 (Idle → X 로 나가는 중에 다시 트리거가 쌓이는 것 방지)
        if (animator.IsInTransition(layer)) return;

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(layer);
        bool inIdle = info.IsTag(idleTag);

        if (!inIdle)
        {
            wasInIdle = false;
            return;
        }

        // Idle 에 새로 진입 → 카운터 초기화
        if (!wasInIdle)
        {
            wasInIdle = true;
            judgedLoop = 0;
        }

        // normalizedTime 의 정수부 = 지금까지 끝낸 바퀴 수 (Loop Time ON 일 때 계속 증가)
        int finishedLoops = Mathf.FloorToInt(info.normalizedTime);
        if (finishedLoops <= judgedLoop) return;

        judgedLoop = finishedLoops;   // 이 바퀴는 판정 완료
        DecideNext();
    }

    private void DecideNext()
    {
        if (actionHashes.Count == 0) return;

        // 50% : Idle 한 번 더 → 아무것도 하지 않으면 루프가 이어진다
        if (Random.value < idleRepeatChance) return;

        // 나머지 50% : 액션 중 하나를 균등 확률로 선택
        int pick = actionHashes[Random.Range(0, actionHashes.Count)];

        foreach (int h in actionHashes) animator.ResetTrigger(h);
        animator.SetTrigger(pick);
    }
}