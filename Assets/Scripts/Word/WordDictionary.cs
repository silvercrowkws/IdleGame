using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct WordInfo
{
    public string type;         // 유형
    public string field;        // 전문 분야
    public string definition;   // 뜻
}

public class WordDictionary : Singleton<WordDictionary>
{
    [SerializeField] private bool loadOnStart = true;   // false면 조판 화면에 처음 들어갈 때 불러옴

    public bool IsLoaded { get; private set; }
    public int Count => words != null ? words.Count : 0;

    private Dictionary<string, List<WordInfo>> words;
    private bool loading;

    private void Start()
    {
        if (loadOnStart) Ensure();
    }

    /// <summary>
    /// 아직 안 불러왔으면 불러오기를 시작
    /// </summary>
    public void Ensure()
    {
        if (IsLoaded || loading) return;
        StartCoroutine(Load());
    }

    public bool TryFind(string word, out List<WordInfo> meanings)
    {
        meanings = null;
        return IsLoaded && words.TryGetValue(word, out meanings);
    }

    private IEnumerator Load()
    {
        loading = true;
        yield return null;   // 첫 화면을 먼저 그린 뒤 로딩 시작
        float start = Time.realtimeSinceStartup;

        TextAsset asset = Resources.Load<TextAsset>("words");
        if (asset == null)
        {
            Debug.LogError("words.txt를 찾을 수 없습니다. Assets/Resources 폴더를 확인하세요.");
            loading = false;
            yield break;
        }

        string text = asset.text;
        words = new Dictionary<string, List<WordInfo>>(250000);
        int pos = 0, count = 0;

        while (pos < text.Length)
        {
            int end = text.IndexOf('\n', pos);
            if (end < 0) end = text.Length;

            int t1 = text.IndexOf('\t', pos, end - pos);
            if (t1 > 0)
            {
                int t2 = text.IndexOf('\t', t1 + 1, end - t1 - 1);
                if (t2 > 0)
                {
                    int t3 = text.IndexOf('\t', t2 + 1, end - t2 - 1);
                    if (t3 > 0)
                    {
                        string key = text.Substring(pos, t1 - pos);
                        var info = new WordInfo
                        {
                            type = text.Substring(t1 + 1, t2 - t1 - 1),
                            field = text.Substring(t2 + 1, t3 - t2 - 1),
                            definition = text.Substring(t3 + 1, end - t3 - 1).TrimEnd('\r')
                        };

                        if (!words.TryGetValue(key, out var list))
                        {
                            list = new List<WordInfo>(1);
                            words[key] = list;
                        }
                        list.Add(info);
                    }
                }
            }

            pos = end + 1;
            if (++count % 20000 == 0) yield return null;
        }

        text = null;
        Resources.UnloadAsset(asset);   // 원본 글자 덩어리는 메모리에서 내림

        IsLoaded = true;
        loading = false;
        Debug.Log($"사전 {words.Count:N0}개 로딩 완료 ({Time.realtimeSinceStartup - start:F2}초)");
    }
}