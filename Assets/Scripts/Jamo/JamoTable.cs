using UnityEngine;

public static class JamoTable
{
    public const string All = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎㅏㅐㅑㅒㅓㅔㅕㅖㅗㅘㅙㅚㅛㅜㅝㅞㅟㅠㅡㅢㅣ";

    // 방치로 나올 확률의 가중치 (클수록 자주 나옴)
    private static int Weight(char c)
    {
        // 지금은 가중치를 둬서 쌍자음이나 복합 모음은 덜 나오게 했는데, 나중에 완전 랜덤으로 변경할 수도 있음
        if ("ㄲㄸㅃㅆㅉ".IndexOf(c) >= 0) return 3;            // 쌍자음
        if ("ㅒㅖ".IndexOf(c) >= 0) return 4;                  // 이중 모음
        if ("ㅘㅙㅚㅝㅞㅟㅢ".IndexOf(c) >= 0) return 2;        // 복합 모음
        return 10;                                             // 기본 자모
    }

    public static string RandomGlyph()
    {
        int total = 0;
        foreach (char c in All) total += Weight(c);

        int r = Random.Range(0, total);
        foreach (char c in All)
        {
            r -= Weight(c);
            if (r < 0) return c.ToString();
        }
        return All[0].ToString();
    }
}