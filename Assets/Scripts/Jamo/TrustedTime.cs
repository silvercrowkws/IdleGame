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
