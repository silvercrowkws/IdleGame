using System.Text;
using TMPro;
using UnityEngine;

public class IdleDebugView : MonoBehaviour
{
    [SerializeField] private TMP_Text text;
    private float nextRefresh;

    private static string Fmt(double s)
    {
        System.TimeSpan t = System.TimeSpan.FromSeconds(System.Math.Max(0, s));
        return $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.5f;

        var clock = IdleClock.Instance;
        var inv = JamoInventory.Instance;
        if (clock == null || inv == null) return;

        var sb = new StringBuilder();
        sb.AppendLine($"상태 {clock.State}   총 {Fmt(inv.Meta.totalSeconds)}");
        sb.AppendLine($"다음 자모까지 {Fmt(clock.SecondsToNext)}");

        if (clock.LastReport != null)
        {
            var r = clock.LastReport;
            sb.AppendLine($"마지막 방치 {Fmt(r.awaySeconds)} (반영 {Fmt(r.countedSeconds)}{(r.capped ? ", 최대" : "")}) +{r.produced.Count}개");
        }

        sb.AppendLine($"보유 {inv.Total}개");
        foreach (char c in JamoTable.All)
        {
            int n = inv.Count(c.ToString());
            if (n > 0) sb.Append($"{c}{n} ");
        }
        text.text = sb.ToString();
    }
}