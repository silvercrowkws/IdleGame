using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class IdleHud : MonoBehaviour
{
    [SerializeField] private TMP_Text timerText;   // 다음 자모까지 남은 시간
    [SerializeField] private TMP_Text toastText;   // 획득 알림
    [SerializeField] private float toastSeconds = 2.2f;

    private IdleClock clock;
    private Coroutine toast;
    private int lastSec = -1;

    private void Start()
    {
        clock = IdleClock.Instance;
        clock.OnProduced += ShowToast;
        toastText.alpha = 0f;
    }

    private void OnDestroy()
    {
        if (clock != null) clock.OnProduced -= ShowToast;
    }

    private void Update()
    {
        if (clock.State != IdleClock.ClockState.Running)
        {
            timerText.text = "시간 확인 중...";
            lastSec = -1;
            return;
        }

        // 초가 바뀔 때만 글자를 갱신
        int sec = Mathf.CeilToInt((float)clock.SecondsToNext);
        if (sec == lastSec) return;
        lastSec = sec;

        timerText.text = $"다음 자모 {Format(sec)}";
    }

    private void ShowToast(List<string> glyphs)
    {
        string msg;
        if (glyphs.Count == 1)
        {
            // 자음 이름은 받침으로 끝나서 '을', 모음 이름은 '를'
            char c = glyphs[0][0];
            msg = $"{c}{(c <= '\u314E' ? "을" : "를")} 획득했어요!";
        }
        else
        {
            msg = $"자모 {glyphs.Count}개 획득!";
        }

        if (toast != null) StopCoroutine(toast);
        toast = StartCoroutine(ToastRoutine(msg));
    }

    private IEnumerator ToastRoutine(string msg)
    {
        toastText.text = msg;
        toastText.alpha = 1f;

        yield return new WaitForSecondsRealtime(toastSeconds);

        // 서서히 사라짐
        for (float t = 0f; t < 0.5f; t += Time.unscaledDeltaTime)
        {
            toastText.alpha = 1f - t / 0.5f;
            yield return null;
        }
        toastText.alpha = 0f;
        toast = null;
    }

    private static string Format(int seconds)
    {
        System.TimeSpan t = System.TimeSpan.FromSeconds(Mathf.Max(0, seconds));
        return t.TotalHours >= 1
            ? $"{(int)t.TotalHours}:{t.Minutes:00}:{t.Seconds:00}"
            : $"{t.Minutes:00}:{t.Seconds:00}";
    }
}