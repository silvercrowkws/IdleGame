/*using System.Collections.Generic;
using UnityEngine;

public class DebugLogKeeper : MonoBehaviour
{
    private const string Key = "dbg_cur";

    private readonly List<string> lines = new List<string>();
    private string prev;
    private float nextBeat;
    private bool show = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Boot()
    {
        GameObject go = new GameObject("DebugLogKeeper");
        DontDestroyOnLoad(go);
        go.AddComponent<DebugLogKeeper>();
    }

    private void Awake()
    {
        prev = PlayerPrefs.GetString(Key, "(이전 기록 없음)");   // 지난번 실행의 기록
        Add("start  mic=" + MicPermission.IsGranted);
    }

    private void Add(string s, bool replaceBeat = false)
    {
        string line = $"{Time.realtimeSinceStartup:F1}s f{Time.frameCount} {s}";

        // 심박은 한 줄만 계속 갱신해서 마지막으로 살아 있던 시각을 남김
        if (replaceBeat && lines.Count > 0 && lines[lines.Count - 1].EndsWith("beat"))
            lines[lines.Count - 1] = line;
        else
            lines.Add(line);

        if (lines.Count > 30) lines.RemoveAt(0);
        PlayerPrefs.SetString(Key, string.Join("\n", lines));
        PlayerPrefs.Save();
    }

    private void Update()
    {
        if (Time.realtimeSinceStartup < nextBeat) return;
        nextBeat = Time.realtimeSinceStartup + 1f;
        Add("beat", true);
    }

    private void OnApplicationPause(bool paused) { Add("pause " + paused); }
    private void OnApplicationFocus(bool focus) { Add("focus " + focus); }

    private void OnGUI()
    {
        if (!show) return;

        int fs = Mathf.Max(18, Screen.height / 55);
        GUI.skin.label.fontSize = fs;
        GUI.skin.button.fontSize = fs;

        float w = Screen.width * 0.94f;
        GUILayout.BeginArea(new Rect(Screen.width * 0.03f, Screen.height * 0.03f, w, Screen.height * 0.6f), GUI.skin.box);
        GUILayout.Label("지난 실행 기록\n" + prev);
        if (GUILayout.Button("닫기", GUILayout.Height(fs * 2.5f))) show = false;
        GUILayout.EndArea();
    }
}*/