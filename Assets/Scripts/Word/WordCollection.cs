using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class CollectedWord
{
    public string word;
    public long firstUtcTicks;   // 처음 찾은 시각
}

[Serializable]
public class WordCollectionData
{
    public List<CollectedWord> words = new List<CollectedWord>();
}

public class WordCollection : Singleton<WordCollection>
{
    public event Action OnChanged;
    public event Action<string> OnNewWord;   // 처음 찾은 단어일 때만

    private WordCollectionData data = new WordCollectionData();
    private readonly Dictionary<string, CollectedWord> map = new Dictionary<string, CollectedWord>();
    private bool loaded;

    private string FilePath => Path.Combine(Application.persistentDataPath, "dex.json");

    public IReadOnlyList<CollectedWord> Words { get { EnsureLoaded(); return data.words; } }
    public int Count { get { EnsureLoaded(); return data.words.Count; } }

    public bool Has(string word)
    {
        EnsureLoaded();
        return map.ContainsKey(word);
    }

    public bool TryGet(string word, out CollectedWord entry)
    {
        EnsureLoaded();
        return map.TryGetValue(word, out entry);
    }

    /// <summary>
    /// 단어를 기록한다. 처음 찾은 단어면 true, 이미 있으면 횟수만 늘리고 false
    /// </summary>
    public bool Add(string word)
    {
        EnsureLoaded();
        if (map.ContainsKey(word)) return false;

        var entry = new CollectedWord { word = word, firstUtcTicks = DateTime.UtcNow.Ticks };
        data.words.Add(entry);
        map[word] = entry;

        Save();
        OnChanged?.Invoke();
        OnNewWord?.Invoke(word);
        return true;
    }

    private void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;

        try
        {
            if (File.Exists(FilePath))
                data = JsonUtility.FromJson<WordCollectionData>(File.ReadAllText(FilePath)) ?? new WordCollectionData();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("도감 파일을 읽지 못했습니다: " + ex.Message);
            try { File.Copy(FilePath, FilePath + ".bak", true); } catch { }   // 망가진 파일은 백업해 둠
            data = new WordCollectionData();
        }

        if (data.words == null) data.words = new List<CollectedWord>();

        map.Clear();
        foreach (var w in data.words) map[w.word] = w;
    }

    public void Save()
    {
        EnsureLoaded();
        try
        {
            string tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(data, true));
            File.Copy(tmp, FilePath, true);
            File.Delete(tmp);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("도감 저장 실패: " + ex.Message);
        }
    }

    [ContextMenu("테스트: 샘플 단어 3개 추가")]
    private void DebugAddSamples()
    {
        Add("안녕"); Add("촉"); Add("사과");
    }

    [ContextMenu("테스트: 도감 모두 지우기")]
    private void DebugClear()
    {
        EnsureLoaded();
        data.words.Clear();
        map.Clear();
        Save();
        OnChanged?.Invoke();
        Debug.Log("도감을 비웠습니다.");
    }

    [ContextMenu("테스트: 샘플 단어 많이 추가")]
    private void DebugAddMany()
    {
        string[] samples =
        {
        "가", "가방", "가을", "가위", "거미", "거울", "계란", "고래", "공책", "구름",
        "기차", "나라", "나무", "나비", "나이", "노래", "눈사람", "다람쥐", "다리", "도시",
        "돼지", "라디오", "라면", "마늘", "마음", "모자", "무지개", "바나나", "바다", "바람",
        "별", "사과", "사랑", "사슴", "소금", "수박", "아기", "아침", "여름", "우산",
        "의자", "인형", "자두", "자전거", "장미", "차례", "차표", "책", "침대", "카메라",
        "카페", "코끼리", "타조", "토끼", "파도", "파리", "포도", "하늘", "하마", "학교",
        "해바라기", "호랑이"
    };
        foreach (string s in samples) Add(s);
    }
}