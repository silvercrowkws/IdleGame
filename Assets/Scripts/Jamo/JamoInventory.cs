using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class JamoEntry
{
    public string glyph;
    public int count;
}

[Serializable]
public class SaveData
{
    public List<JamoEntry> jamo = new List<JamoEntry>();
    public List<string> foundWords = new List<string>();   // 지금까지 찾은 단어 (찾은 순서)
    public long lastUtcTicks;     // 마지막으로 정산한 신뢰 시각
    public double progress;       // 다음 자모를 만들기까지 쌓인 시간(초)
    public double totalSeconds;   // 총 누적 시간(초)
    public long ink;   // 보유 잉크

    public long points;                // 보유 포인트
    public int combo;                  // 끝말잇기 콤보
    public int bestCombo;              // 최고 콤보
    public string chainWord = "";      // 직전에 이은 단어 (한 글자 단어는 제외)
}

public class JamoInventory : Singleton<JamoInventory>
{
    /// <summary>
    /// 수량이 바뀔 때 알림 (글자, 바뀐 뒤 수량)
    /// </summary>
    public event Action<string, int> OnChanged;

    /// <summary>
    /// 새 단어를 찾아 기록했을 때 알림
    /// </summary>
    public event Action<string> OnWordAdded;

    /// <summary>
    /// 잉크가 바뀔 때 알림 (바뀐 뒤 수량)
    /// </summary>
    public event Action<long> OnInkChanged;

    /// <summary>
    /// 포인트가 바뀔 때 알림 (바뀐 뒤 수량)
    /// </summary>
    public event Action<long> OnPointsChanged;

    /// <summary>
    /// 끝말잇기 상태(직전 단어, 콤보)가 바뀔 때 알림
    /// </summary>
    public event Action OnChainChanged;

    private readonly Dictionary<string, int> counts = new Dictionary<string, int>();
    private readonly HashSet<string> found = new HashSet<string>();
    private SaveData data = new SaveData();
    private bool loaded, dirty;
    private float lastSave;

    private string FilePath => Path.Combine(Application.persistentDataPath, "inventory.json");

    /// <summary>
    /// 시간 정산 정보(IdleClock이 사용)
    /// </summary>
    public SaveData Meta { get { EnsureLoaded(); return data; } }

    /// <summary>
    /// 지금까지 찾은 단어 (찾은 순서). 도감이 사용할 예정
    /// </summary>
    public IReadOnlyList<string> FoundWords { get { EnsureLoaded(); return data.foundWords; } }

    public int Total
    {
        get
        {
            EnsureLoaded();
            int sum = 0;
            foreach (var kv in counts) sum += kv.Value;
            return sum;
        }
    }

    public int Count(string glyph)
    {
        EnsureLoaded();
        return counts.TryGetValue(glyph, out int n) ? n : 0;
    }

    public void Add(string glyph, int n = 1)
    {
        EnsureLoaded();
        counts[glyph] = Count(glyph) + n;
        dirty = true;
        OnChanged?.Invoke(glyph, counts[glyph]);
    }

    /// <summary>
    /// 자모를 쓴다(-n). 모자라면 false
    /// </summary>
    public bool TryUse(string glyph, int n = 1)
    {
        EnsureLoaded();
        if (Count(glyph) < n) return false;

        counts[glyph] -= n;
        dirty = true;
        OnChanged?.Invoke(glyph, counts[glyph]);
        return true;
    }

    // ---------- 잉크 ----------
    public long Ink { get { EnsureLoaded(); return data.ink; } }

    public void AddInk(long n)
    {
        if (n <= 0) return;
        EnsureLoaded();
        data.ink += n;
        dirty = true;
        OnInkChanged?.Invoke(data.ink);
    }

    /// <summary>
    /// 잉크를 쓴다. 모자라면 false
    /// </summary>
    public bool TryUseInk(long n)
    {
        EnsureLoaded();
        if (n < 0 || data.ink < n) return false;

        data.ink -= n;
        dirty = true;
        OnInkChanged?.Invoke(data.ink);
        return true;
    }

    // ---------- 포인트와 끝말잇기 ----------
    public long Points { get { EnsureLoaded(); return data.points; } }
    public int Combo { get { EnsureLoaded(); return data.combo; } }
    public int BestCombo { get { EnsureLoaded(); return data.bestCombo; } }
    public string LastChainWord { get { EnsureLoaded(); return data.chainWord ?? ""; } }

    public void AddPoints(long n)
    {
        if (n <= 0) return;
        EnsureLoaded();
        data.points += n;
        dirty = true;
        OnPointsChanged?.Invoke(data.points);
    }

    /// <summary>
    /// 포인트를 쓴다. 모자라면 false (나중에 상점이 사용)
    /// </summary>
    public bool TryUsePoints(long n)
    {
        EnsureLoaded();
        if (n < 0 || data.points < n) return false;

        data.points -= n;
        dirty = true;
        OnPointsChanged?.Invoke(data.points);
        return true;
    }

    /// <summary>
    /// 끝말잇기 상태를 바꾼다 (직전 단어와 콤보)
    /// </summary>
    public void SetChain(string word, int combo)
    {
        EnsureLoaded();
        data.chainWord = word ?? "";
        data.combo = Mathf.Max(0, combo);
        if (data.combo > data.bestCombo) data.bestCombo = data.combo;
        dirty = true;
        OnChainChanged?.Invoke();
    }

    // ---------- 찾은 단어 ----------
    public bool HasWord(string word)
    {
        EnsureLoaded();
        return found.Contains(word);
    }

    /// <summary>
    /// 찾은 단어로 기록한다. 이미 있으면 false
    /// </summary>
    public bool AddWord(string word)
    {
        EnsureLoaded();
        if (!found.Add(word)) return false;

        data.foundWords.Add(word);
        dirty = true;
        OnWordAdded?.Invoke(word);
        return true;
    }

    private void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;

        try
        {
            if (File.Exists(FilePath))
                data = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath)) ?? new SaveData();
        }
        catch (Exception ex)
        {
            Debug.LogWarning("저장 파일을 읽지 못했습니다: " + ex.Message);
            try { File.Copy(FilePath, FilePath + ".bak", true); } catch { }   // 망가진 파일은 백업해 둠
            data = new SaveData();
        }

        // 예전 저장 파일에는 없는 항목이 비어 있을 수 있음
        if (data.jamo == null) data.jamo = new List<JamoEntry>();
        if (data.foundWords == null) data.foundWords = new List<string>();

        counts.Clear();
        foreach (var entry in data.jamo) counts[entry.glyph] = entry.count;

        found.Clear();
        foreach (string w in data.foundWords) found.Add(w);
    }

    public void Save()
    {
        EnsureLoaded();
        dirty = false;
        lastSave = Time.unscaledTime;

        // 40개 전부 기록 (0개인 것도 포함)
        data.jamo.Clear();
        foreach (char c in JamoTable.All)
        {
            string g = c.ToString();
            counts.TryGetValue(g, out int n);
            data.jamo.Add(new JamoEntry { glyph = g, count = n });
        }

        try
        {
            string tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonUtility.ToJson(data, true));
            File.Copy(tmp, FilePath, true);
            File.Delete(tmp);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("저장 실패: " + ex.Message);
        }
    }

    private void Update()
    {
        // 수량이 바뀐 뒤 3초가 지나면 저장 (조각 직후에 바로 꺼져도 잃지 않게)
        if (dirty && Time.unscaledTime - lastSave >= 3f) Save();
    }

    /*[ContextMenu("전체 초기화 (저장 파일 삭제)")]
    public void ResetAll()
    {
        loaded = true;
        counts.Clear();
        found.Clear();
        data = new SaveData();
        try { if (File.Exists(FilePath)) File.Delete(FilePath); } catch { }
        Debug.Log("자모 인벤토리를 초기화했습니다.");
    }*/

    [ContextMenu("전체 초기화 (저장 파일 삭제)")]
    public void ResetAll()
    {
        EnsureLoaded();

        // 새 객체로 바꾸지 않고 안의 값만 비움 (Meta를 들고 있는 스크립트가 끊기지 않게)
        counts.Clear();
        found.Clear();
        data.jamo.Clear();
        data.foundWords.Clear();
        data.progress = 0; 
        data.totalSeconds = 0;
        data.ink = 0;

        data.points = 0;
        data.combo = 0;
        data.bestCombo = 0;
        data.chainWord = "";
        dirty = false;

        try { if (File.Exists(FilePath)) File.Delete(FilePath); } catch { }

        // 화면이 바뀌도록 알림
        foreach (char c in JamoTable.All) OnChanged?.Invoke(c.ToString(), 0);
        OnInkChanged?.Invoke(0);
        OnPointsChanged?.Invoke(0);
        OnChainChanged?.Invoke();

        Debug.Log("자모 인벤토리를 초기화했습니다.");
    }

    [ContextMenu("테스트: 모든 자모 +5")]
    private void DebugAddAll()
    {
        foreach (char c in JamoTable.All) Add(c.ToString(), 5);
    }

    [ContextMenu("테스트: 찾은 단어 모두 지우기")]
    private void DebugClearWords()
    {
        found.Clear();
        data.foundWords.Clear();
        dirty = true;
        Debug.Log("찾은 단어 목록을 지웠습니다.");
    }

    [ContextMenu("테스트: 잉크 +100")]
    private void DebugAddInk() { AddInk(100); }

    [ContextMenu("테스트: 포인트 +100")]
    private void DebugAddPoints() { AddPoints(100); }

    [ContextMenu("테스트: 끝말잇기 초기화")]
    private void DebugResetChain() { SetChain("", 0); } 
}