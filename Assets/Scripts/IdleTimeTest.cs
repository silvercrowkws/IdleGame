using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;

// 앵커(검증된 네트워크 시각) + 단조 시계로 인터넷이 끊겨도 신뢰 가능한 시각 계산
public static class TrustedTime
{
    private static DateTime anchorUtc; 
    private static long anchorTimestamp; 
    private static bool hasAnchor;

    public static void SetAnchor(DateTime networkUtc)
    {
        anchorUtc = networkUtc;
        anchorTimestamp = Stopwatch.GetTimestamp();
        hasAnchor = true;
    }

    public static DateTime NowUtc
    {
        get
        {
            if (!hasAnchor) return DateTime.UtcNow;
            double sec = (double)(Stopwatch.GetTimestamp() - anchorTimestamp) / Stopwatch.Frequency;
            return anchorUtc.AddSeconds(sec);
        }
    }
}

public class IdleTimeTest : MonoBehaviour
{
    private const string KeyElapsed = "elapsed";
    private const string KeyLastUtc = "lastUtcTicks";
    private const float AutoSaveInterval = 10f;

    private static readonly string[] Urls =
    {
        "https://www.google.com",
        "https://www.cloudflare.com",
        "https://www.microsoft.com"
    };

    private enum State { Syncing, NeedInternet, Running }
    private State state = State.Syncing;
    private bool syncing;

    private double baseElapsed;          // 기준 시점까지의 누적 경과 시간(초)
    private DateTime baseUtc;            // 기준 시점의 신뢰 시각
    private double lastOfflineSeconds;   // 마지막 복귀 때 꺼져 있던 시간(초)
    private float saveTimer;

    private double CurrentElapsed =>
        baseElapsed + (TrustedTime.NowUtc - baseUtc).TotalSeconds;

    private void Start()
    {
        StartCoroutine(Sync());
    }

    private void Update()
    {
        if (state != State.Running) return;

        saveTimer += Time.unscaledDeltaTime;
        if (saveTimer >= AutoSaveInterval)
        {
            saveTimer = 0f;
            Save();
        }
    }

    // 네트워크 시각을 받아서 저장값과 합산 (시작 및 복귀 시 호출)
    private IEnumerator Sync()
    {
        syncing = true;
        state = State.Syncing;

        DateTime? net = null;
        yield return FetchUtcTime(t => net = t);

        if (!net.HasValue)
        {
            state = State.NeedInternet;   // 인터넷 없음 → 게임 진행 차단
            syncing = false;
            yield break;
        }

        TrustedTime.SetAnchor(net.Value);

        double savedElapsed = 0;
        //lastOfflineSeconds = 0;
        double gap = 0;                      // 이번 정산에서 계산된 꺼져 있던 시간

        string e = PlayerPrefs.GetString(KeyElapsed, "");
        string t = PlayerPrefs.GetString(KeyLastUtc, "");
        if (e != "" && t != "" &&
            double.TryParse(e, NumberStyles.Float, CultureInfo.InvariantCulture, out savedElapsed) &&
            long.TryParse(t, out long ticks))
        {
            DateTime lastUtc = new DateTime(ticks, DateTimeKind.Utc);
            //lastOfflineSeconds = Math.Max(0, (net.Value - lastUtc).TotalSeconds); // 음수 방지
            gap = Math.Max(0, (net.Value - lastUtc).TotalSeconds);   // 음수 방지
        }

        // 추가
        if (gap >= 2) lastOfflineSeconds = gap;   // 아주 짧은 간격은 표시값을 덮어쓰지 않음

        //baseElapsed = savedElapsed + lastOfflineSeconds;  // 끄기 전 + 꺼져 있던 시간
        baseElapsed = savedElapsed + gap;         // 합산에는 항상 gap 사용
        baseUtc = net.Value;

        state = State.Running;
        syncing = false;
        Save();
    }

    private void Save()
    {
        if (state != State.Running) return;   // 검증 전에는 저장하지 않음

        PlayerPrefs.SetString(KeyElapsed, CurrentElapsed.ToString("R", CultureInfo.InvariantCulture));
        PlayerPrefs.SetString(KeyLastUtc, TrustedTime.NowUtc.Ticks.ToString());
        PlayerPrefs.Save();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused) Save();                         // 백그라운드 진입: 네트워크 없이 저장
        else if (!syncing) StartCoroutine(Sync());  // 복귀: 새로 시각을 받아 정산
    }

    private void OnApplicationQuit()
    {
        Save();
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

    private static string Format(double seconds)
    {
        TimeSpan ts = TimeSpan.FromSeconds(seconds);
        return $"{(int)ts.TotalHours:00}:{ts.Minutes:00}:{ts.Seconds:00}";
    }

    private void OnGUI()
    {
        int fs = Mathf.RoundToInt(Screen.height * 0.035f);
        var label = new GUIStyle(GUI.skin.label)
        { fontSize = fs, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        var button = new GUIStyle(GUI.skin.button) { fontSize = fs };

        GUILayout.BeginArea(new Rect(0, 0, Screen.width, Screen.height));
        GUILayout.FlexibleSpace();

        switch (state)
        {
            case State.Syncing:
                GUILayout.Label("시간 확인 중...", label);
                break;

            case State.NeedInternet:
                GUILayout.Label("인터넷 연결이 필요합니다.", label);
                if (GUILayout.Button("다시 시도", button, GUILayout.Height(fs * 2.5f)))
                    StartCoroutine(Sync());
                break;

            case State.Running:
                GUILayout.Label("총 경과 시간", label);
                GUILayout.Label(Format(CurrentElapsed), label);
                GUILayout.Label($"마지막으로 꺼져 있던 시간: {Format(lastOfflineSeconds)}", label);
                GUILayout.Space(fs);
                if (GUILayout.Button("기록 초기화", button, GUILayout.Height(fs * 2.5f)))
                {
                    PlayerPrefs.DeleteAll();
                    baseElapsed = 0;
                    baseUtc = TrustedTime.NowUtc;
                    lastOfflineSeconds = 0;
                }
                break;
        }

        GUILayout.FlexibleSpace();
        GUILayout.EndArea();
    }
}