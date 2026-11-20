using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class MyWord
{
    public string word;
    public string type;          // 유형 (고유어, 한자어 등)
    public string field;         // 전문 분야
    public string definition;    // 뜻
    public long createdUtcTicks; // 등록한 시각

    // 대사전 결과와 같은 모양으로 바꿔서 조판 결과 화면을 그대로 쓸 수 있게 함
    public WordInfo ToInfo()
    {
        return new WordInfo { type = type, field = field, definition = definition };
    }
}

[Serializable]
public class MyDictData
{
    public List<MyWord> words = new List<MyWord>();
}

public class MyDictionary : Singleton<MyDictionary>
{
    /// <summary>
    /// 단어가 추가, 수정, 삭제되었을 때
    /// </summary>
    public event Action OnChanged;

    private MyDictData data = new MyDictData();
    private readonly Dictionary<string, MyWord> map = new Dictionary<string, MyWord>();
    private bool loaded;

    private string FilePath => Path.Combine(Application.persistentDataPath, "mydict.json");

    /// <summary>
    /// 등록한 순서대로의 단어 목록
    /// </summary>
    public IReadOnlyList<MyWord> Words { get { EnsureLoaded(); return data.words; } }

    public bool Has(string word)
    {
        EnsureLoaded();
        return map.ContainsKey(word);
    }

    public bool TryGet(string word, out MyWord entry)
    {
        EnsureLoaded();
        return map.TryGetValue(word, out entry);
    }

    /// <summary>
    /// 새 단어를 등록한다. 이미 있으면 false
    /// </summary>
    public bool Add(MyWord entry)
    {
        EnsureLoaded();
        if (map.ContainsKey(entry.word)) return false;

        if (entry.createdUtcTicks == 0) entry.createdUtcTicks = DateTime.UtcNow.Ticks;
        data.words.Add(entry);
        map[entry.word] = entry;

        Save();
        OnChanged?.Invoke();
        return true;
    }

    public int Count { get { EnsureLoaded(); return data.words.Count; } }

    /// <summary>
    /// 단어를 지운다. 없으면 false
    /// </summary>
    public bool Remove(string word)
    {
        EnsureLoaded();
        if (!map.TryGetValue(word, out MyWord w)) return false;

        data.words.Remove(w);
        map.Remove(word);
        Save();
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// 단어의 뜻을 바꾼다. 없으면 false
    /// </summary>
    public bool SetDefinition(string word, string definition)
    {
        EnsureLoaded();
        if (!map.TryGetValue(word, out MyWord w)) return false;

        w.definition = definition;
        Save();
        OnChanged?.Invoke();
        return true;
    }

    private void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;

        try
        {
            if (File.Exists(FilePath))
                data = JsonUtility.FromJson<MyDictData>(File.ReadAllText(FilePath)) ?? new MyDictData();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("나만의 사전 파일을 읽지 못했습니다: " + ex.Message);
            try { File.Copy(FilePath, FilePath + ".bak", true); } catch { }   // 망가진 파일은 백업해 둠
            data = new MyDictData();
        }

        if (data.words == null) data.words = new List<MyWord>();

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
            Debug.LogWarning("나만의 사전 저장 실패: " + ex.Message);
        }
    }

    [ContextMenu("테스트: '아아아' 등록")]
    private void DebugAddSample()
    {
        Add(new MyWord { word = "아아아", type = "고유어", field = "없음", definition = "테스트로 등록한 말." });
    }

    [ContextMenu("테스트: 나만의 사전 모두 지우기")]
    private void DebugClear()
    {
        EnsureLoaded();
        data.words.Clear();
        map.Clear();
        Save();
        OnChanged?.Invoke();
        Debug.Log("나만의 사전을 비웠습니다.");
    }

    [ContextMenu("테스트: 샘플 단어 50개 추가")]
    private void DebugAddMany()
    {
        string[] names =
        {
        "가", "가방", "가을", "가위", "거미", "거울", "계란", "고래", "공책", "구름",
        "기차", "나라", "나무", "나비", "나이", "노래", "눈사람", "다람쥐", "다리", "도시",
        "돼지", "라디오", "라면", "마늘", "마음", "모자", "무지개", "바나나", "바다", "바람",
        "별", "사과", "사랑", "사슴", "소금", "수박", "아기", "아침", "여름", "우산",
        "의자", "인형", "자두", "자전거", "장미", "차례", "차표", "책", "침대", "카메라"
    };
        foreach (string n in names)
            Add(new MyWord { word = n, type = "나만의 말", field = "없음", definition = "테스트로 등록한 말." });
    }
}