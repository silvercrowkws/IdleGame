using System.Collections.Generic;
using System.Text;

public static class HangulComposer
{
    public struct Result
    {
        public string text;      // 조합된 글자
        public bool complete;    // 모든 글자가 초성+중성을 갖췄는지
    }

    private const string Cho = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ";
    private const string Jung = "ㅏㅐㅑㅒㅓㅔㅕㅖㅗㅘㅙㅚㅛㅜㅝㅞㅟㅠㅡㅢㅣ";
    private const string Jong = " ㄱㄲㄳㄴㄵㄶㄷㄹㄺㄻㄼㄽㄾㄿㅀㅁㅂㅄㅅㅆㅇㅈㅊㅋㅌㅍㅎ";

    // 겹받침 규칙: 앞 받침 + 뒤 자음 = 합쳐진 받침
    private static readonly (char a, char b, char r)[] Pairs =
    {
        ('ㄱ', 'ㅅ', 'ㄳ'), ('ㄴ', 'ㅈ', 'ㄵ'), ('ㄴ', 'ㅎ', 'ㄶ'),
        ('ㄹ', 'ㄱ', 'ㄺ'), ('ㄹ', 'ㅁ', 'ㄻ'), ('ㄹ', 'ㅂ', 'ㄼ'), ('ㄹ', 'ㅅ', 'ㄽ'),
        ('ㄹ', 'ㅌ', 'ㄾ'), ('ㄹ', 'ㅍ', 'ㄿ'), ('ㄹ', 'ㅎ', 'ㅀ'), ('ㅂ', 'ㅅ', 'ㅄ'),
    };

    private class Syl
    {
        public char cho, jung, jong;   // 없으면 '\0'
    }

    private static bool IsConsonant(char c) { return c >= '\u3131' && c <= '\u314E'; }
    private static bool CanBeJong(char c) { return c != 'ㄸ' && c != 'ㅃ' && c != 'ㅉ'; }

    private static char MergeJong(char a, char b)
    {
        foreach (var p in Pairs) if (p.a == a && p.b == b) return p.r;
        return '\0';
    }

    private static (char first, char second) SplitJong(char j)
    {
        foreach (var p in Pairs) if (p.r == j) return (p.a, p.b);
        return ('\0', j);   // 겹받침이 아니면 통째로 다음 글자로 넘김
    }

    public static Result Compose(IReadOnlyList<string> jamo)
    {
        var syls = new List<Syl>();
        Syl cur = null;

        foreach (string s in jamo)
        {
            char c = s[0];

            if (IsConsonant(c))
            {
                if (cur != null && cur.jung != '\0')
                {
                    if (cur.jong == '\0')
                    {
                        if (CanBeJong(c)) { cur.jong = c; continue; }          // 받침이 됨
                    }
                    else
                    {
                        char merged = MergeJong(cur.jong, c);
                        if (merged != '\0') { cur.jong = merged; continue; }   // 겹받침이 됨
                    }
                }
                cur = new Syl { cho = c };   // 새 글자의 초성
                syls.Add(cur);
            }
            else
            {
                if (cur != null && cur.cho != '\0' && cur.jung == '\0')
                {
                    cur.jung = c;   // 초성 뒤의 모음
                }
                else if (cur != null && cur.jong != '\0')
                {
                    // 받침을 다음 글자의 초성으로 넘긴다 (안 + ㅕ → 안녀)
                    var split = SplitJong(cur.jong);
                    cur.jong = split.first;
                    cur = new Syl { cho = split.second, jung = c };
                    syls.Add(cur);
                }
                else
                {
                    cur = new Syl { jung = c };   // 초성 없는 모음 (완성되지 않은 글자)
                    syls.Add(cur);
                }
            }
        }

        var sb = new StringBuilder();
        bool complete = syls.Count > 0;

        foreach (var s in syls)
        {
            if (s.cho != '\0' && s.jung != '\0')
            {
                int ci = Cho.IndexOf(s.cho);
                int ji = Jung.IndexOf(s.jung);
                int ki = s.jong == '\0' ? 0 : Jong.IndexOf(s.jong);
                sb.Append((char)(0xAC00 + (ci * 21 + ji) * 28 + ki));
            }
            else
            {
                complete = false;
                sb.Append(s.cho != '\0' ? s.cho : s.jung);   // 덜 된 글자는 낱자 그대로 보여줌
            }
        }

        return new Result { text = sb.ToString(), complete = complete };
    }
}