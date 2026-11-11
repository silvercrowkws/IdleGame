using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class IdlePopup : MonoBehaviour
{
    [Header("방치 보고")]
    [SerializeField] private CanvasGroup reportGroup;
    [SerializeField] private TMP_Text awayText;
    [SerializeField] private TMP_Text listText;

    [Header("인터넷 필요")]
    [SerializeField] private CanvasGroup gateGroup;

    private IdleClock clock;

    private void Awake()
    {
        SetVisible(reportGroup, false);
        SetVisible(gateGroup, false);
    }

    private void Start()
    {
        clock = IdleClock.Instance;
        clock.OnSettled += ShowReport;
        clock.OnStateChanged += OnState;
        OnState(clock.State);
    }

    private void OnDestroy()
    {
        if (clock == null) return;
        clock.OnSettled -= ShowReport;
        clock.OnStateChanged -= OnState;
    }

    // 인터넷이 없으면 진행을 막는 화면을 띄운다
    private void OnState(IdleClock.ClockState s)
    {
        SetVisible(gateGroup, s == IdleClock.ClockState.NeedInternet);
    }

    private void ShowReport(IdleReport r)
    {
        awayText.text = r.capped
            ? $"{Format(r.awaySeconds)} 동안 자리를 비웠어요\n(최대 {Format(r.countedSeconds)}까지 반영돼요)"
            : $"{Format(r.awaySeconds)} 동안 자리를 비웠어요";

        if (r.produced.Count == 0)
        {
            listText.text = "아직 새로 만들어진 자모가 없어요";
        }
        else
        {
            // 같은 글자끼리 묶어서 세기
            var counts = new Dictionary<string, int>();
            foreach (string g in r.produced)
            {
                counts.TryGetValue(g, out int n);
                counts[g] = n + 1;
            }

            var sb = new StringBuilder();
            sb.Append($"자모 {r.produced.Count}개를 만들었어요\n\n");

            int col = 0;
            foreach (char c in JamoTable.All)
            {
                string g = c.ToString();
                if (!counts.TryGetValue(g, out int n)) continue;
                sb.Append($"{g} x{n}");
                sb.Append(++col % 4 == 0 ? "\n" : "     ");
            }
            listText.text = sb.ToString();
        }

        SetVisible(reportGroup, true);
    }

    // 확인 버튼
    public void Close()
    {
        SetVisible(reportGroup, false);
    }

    // 다시 시도 버튼
    public void Retry()
    {
        clock.Retry();
    }

    private static void SetVisible(CanvasGroup g, bool on)
    {
        g.alpha = on ? 1f : 0f;
        g.interactable = on;
        g.blocksRaycasts = on;
    }

    private static string Format(double seconds)
    {
        System.TimeSpan t = System.TimeSpan.FromSeconds(seconds);
        if (t.TotalHours >= 1) return $"{(int)t.TotalHours}시간 {t.Minutes}분";
        if (t.TotalMinutes >= 1) return $"{t.Minutes}분 {t.Seconds}초";
        return $"{t.Seconds}초";
    }
}