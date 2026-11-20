public static class WordChain
{
    private const int First = 0xAC00, Last = 0xD7A3;

    private static bool IsHangul(char c) { return c >= First && c <= Last; }

    /// <summary>
    /// 끝말잇기에 쓸 수 있는 단어: 한글 두 글자 이상
    /// </summary>
    public static bool IsChainable(string word)
    {
        if (string.IsNullOrEmpty(word) || word.Length < 2) return false;
        return IsHangul(word[0]) && IsHangul(word[word.Length - 1]);
    }

    /// <summary>
    /// next가 prev에 이어지는지 (두음법칙 허용)
    /// </summary>
    public static bool Follows(string prev, string next)
    {
        if (!IsChainable(prev) || !IsChainable(next)) return false;

        char tail = prev[prev.Length - 1];
        char head = next[0];
        return head == tail || head == ApplyInitialRule(tail);
    }

    /// <summary>
    /// 이을 글자 안내. "방" 또는 "래 (내)"
    /// </summary>
    public static string Hint(string prev)
    {
        if (!IsChainable(prev)) return "";

        char tail = prev[prev.Length - 1];
        char alt = ApplyInitialRule(tail);
        return alt == tail ? tail.ToString() : $"{tail} ({alt})";
    }

    // 두음법칙: 라→나, 리→이, 녀→여 ... (받침은 그대로. 력→역)
    private static char ApplyInitialRule(char c)
    {
        int code = c - First;
        int jong = code % 28;
        int jung = (code / 28) % 21;
        int cho = code / 28 / 21;

        const int N = 2, R = 5, Ieung = 11;

        if (cho == R)
        {
            if (jung == 0 || jung == 1 || jung == 8 || jung == 11 || jung == 13 || jung == 18) cho = N;            // 라 래 로 뢰 루 르
            else if (jung == 2 || jung == 6 || jung == 7 || jung == 12 || jung == 17 || jung == 20) cho = Ieung;   // 랴 려 례 료 류 리
            else return c;
        }
        else if (cho == N)
        {
            if (jung == 2 || jung == 6 || jung == 12 || jung == 17 || jung == 20) cho = Ieung;                      // 냐 녀 뇨 뉴 니
            else return c;
        }
        else return c;

        return (char)(First + (cho * 21 + jung) * 28 + jong);
    }
}