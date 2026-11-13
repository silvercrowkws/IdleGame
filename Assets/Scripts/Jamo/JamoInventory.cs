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
    public long lastUtcTicks;     // 마지막으로 정산한 신뢰 시각
    public double progress;       // 다음 자모를 만들기까지 쌓인 시간(초)
    public double totalSeconds;   // 총 누적 시간(초)
}

public class JamoInventory : Singleton<JamoInventory>
{
    /// <summary>
    /// 수량이 바뀔 때 알림 (글자, 바뀐 뒤 수량)
    /// </summary>
    public event Action<string, int> OnChanged;

    private readonly Dictionary<string, int> counts = new Dictionary<string, int>();
    private SaveData data = new SaveData();
    private bool loaded, dirty;
    private float lastSave;

    private string FilePath => Path.Combine(Application.persistentDataPath, "inventory.json");

    /// <summary>
    /// 시간 정산 정보(IdleClock이 사용)
    /// </summary>
    public SaveData Meta { get { EnsureLoaded(); return data; } }

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

        counts.Clear();
        foreach (var entry in data.jamo) counts[entry.glyph] = entry.count;
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

    [ContextMenu("전체 초기화 (저장 파일 삭제)")]
    private void ResetAll()
    {
        loaded = true;
        counts.Clear();
        data = new SaveData();
        try { if (File.Exists(FilePath)) File.Delete(FilePath); } catch { }
        Debug.Log("자모 인벤토리를 초기화했습니다.");
    }



    [ContextMenu("테스트: 모든 자모 +5")]
    private void DebugAddAll()
    {
        foreach (char c in JamoTable.All) Add(c.ToString(), 5);
    }
}