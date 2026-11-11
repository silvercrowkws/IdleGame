using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;

public class IdleReport
{
    public double awaySeconds;      // 실제로 꺼져 있던 시간
    public double countedSeconds;   // 생산에 반영한 시간 (최대치 적용)
    public bool capped;             // 최대 시간에 걸렸는지
    public List<string> produced = new List<string>();   // 그동안 만들어진 자모
}

public class IdleClock : Singleton<IdleClock>
{
    public enum ClockState { Syncing, NeedInternet, Running }

    [Header("방치 생산")]

    [Tooltip("자모 1개를 만드는 시간")]
    [SerializeField] private float secondsPerJamo = 300f;        // 자모 1개를 만드는 시간 (5분)

    [Tooltip("오프라인 동안 인정하는 최대 시간")]
    [SerializeField] private float maxOfflineSeconds = 28800f;   // 꺼져 있는 동안 인정하는 최대 시간 (8시간)

    [Tooltip("이 이상으로 오프라인일 때만 보고")]
    [SerializeField] private float minReportSeconds = 30f;       // 이 시간 이상 꺼져 있었을 때만 보고

    private const float AutoSaveInterval = 10f;

    private static readonly string[] Urls =
    {
        "https://www.google.com",
        "https://www.cloudflare.com",
        "https://www.microsoft.com"
    };

    public ClockState State { get; private set; } = ClockState.Syncing;
    public IdleReport LastReport { get; private set; }

    public float SecondsPerJamo => Mathf.Max(1f, secondsPerJamo);

    /// <summary>
    /// 다음 자모까지 남은 시간(초)
    /// </summary>
    public double SecondsToNext => SecondsPerJamo - JamoInventory.Instance.Meta.progress;

    /// <summary>
    /// 정산이 끝났을 때 (오래 꺼져 있었을 때만)
    /// </summary>
    public event Action<IdleReport> OnSettled;
    public event Action<ClockState> OnStateChanged;

    /// <summary>
    /// 앱을 켜 둔 동안 자모가 만들어졌을 때
    /// </summary>
    public event Action<List<string>> OnProduced;

    private bool syncing;
    private DateTime lastTick;
    private float saveTimer;

    private void Start()
    {
        if (!syncing) StartCoroutine(Sync());
    }

    private void Update()
    {
        if (State != ClockState.Running) return;

        Tick(true);

        saveTimer += Time.unscaledDeltaTime;
        if (saveTimer >= AutoSaveInterval)
        {
            saveTimer = 0f;
            JamoInventory.Instance.Save();
        }
    }

    // 앱이 켜져 있는 동안 흐른 시간을 진행도에 더한다
    private void Tick(bool notify)
    {
        DateTime now = TrustedTime.NowUtc;
        double dt = (now - lastTick).TotalSeconds;
        lastTick = now;
        if (dt <= 0) return;

        List<string> produced = AddProgress(dt);
        JamoInventory.Instance.Meta.lastUtcTicks = now.Ticks;   // 진행도와 시각을 항상 같이 갱신

        if (notify && produced.Count > 0) OnProduced?.Invoke(produced);
    }

    private List<string> AddProgress(double seconds)
    {
        var meta = JamoInventory.Instance.Meta;
        meta.progress += seconds;
        meta.totalSeconds += seconds;

        var produced = new List<string>();
        float period = SecondsPerJamo;
        while (meta.progress >= period)
        {
            meta.progress -= period;
            string g = JamoTable.RandomGlyph();
            JamoInventory.Instance.Add(g);
            produced.Add(g);
        }
        return produced;
    }

    private IEnumerator Sync()
    {
        syncing = true;
        SetState(ClockState.Syncing);
        
        DateTime? net = null;
        for (int i = 0; i < 2 && !net.HasValue; i++)
        {
            yield return FetchUtcTime(t => net = t);
            if (!net.HasValue) yield return new WaitForSecondsRealtime(1.5f);
        }

        if (!net.HasValue)
        {
            SetState(ClockState.NeedInternet);   // 인터넷 없음 → 진행 차단 (화면은 다음 단계에서)
            syncing = false;
            yield break;
        }

        TrustedTime.SetAnchor(net.Value);

        var meta = JamoInventory.Instance.Meta;
        double gap = 0;
        if (meta.lastUtcTicks > 0)
        {
            DateTime lastUtc = new DateTime(meta.lastUtcTicks, DateTimeKind.Utc);
            gap = Math.Max(0, (net.Value - lastUtc).TotalSeconds);   // 음수 방지
        }

        // 꺼져 있던 시간 정산 (최대 시간 제한)
        double counted = Math.Min(gap, maxOfflineSeconds);
        var report = new IdleReport
        {
            awaySeconds = gap,
            countedSeconds = counted,
            capped = gap > maxOfflineSeconds,
            produced = AddProgress(counted)
        };

        lastTick = net.Value;
        meta.lastUtcTicks = net.Value.Ticks;
        JamoInventory.Instance.Save();

        LastReport = report;
        SetState(ClockState.Running);
        syncing = false;

        if (gap >= minReportSeconds) OnSettled?.Invoke(report);
    }

    private void SetState(ClockState s)
    {
        State = s;
        OnStateChanged?.Invoke(s);
    }

    // "다시 시도" 버튼이 부르는 함수
    public void Retry()
    {
        if (!syncing) StartCoroutine(Sync());
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            if (State == ClockState.Running) Tick(false);       // 백그라운드로 나가는 순간에 만들어진 자모는 알림 없이 쌓임
            JamoInventory.Instance.Save();
        }
        else if (!syncing)
        {
            StartCoroutine(Sync());   // 복귀: 새로 시각을 받아 정산
        }
    }

    // 3개 주소를 순서대로 시도, 먼저 성공한 값을 사용
    private IEnumerator FetchUtcTime(Action<DateTime?> onDone)
    {
        foreach (string url in Urls)
        {
            using (UnityWebRequest req = UnityWebRequest.Head(url))
            {
                req.timeout = 5;
                yield return req.SendWebRequest();

                if (req.result != UnityWebRequest.Result.Success) continue;

                string header = req.GetResponseHeader("Date");
                if (!string.IsNullOrEmpty(header) &&
                    DateTime.TryParse(header, CultureInfo.InvariantCulture,
                        DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                        out DateTime t))
                {
                    onDone(t);
                    yield break;
                }
            }
        }
        onDone(null);
    }
}