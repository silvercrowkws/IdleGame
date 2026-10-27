using System.Collections.Generic;
using UnityEngine;

public class WordSearchTest : MonoBehaviour
{
    private struct WordInfo
    {
        public string type;
        public string definition;
    }

    private Dictionary<string, WordInfo> words;
    private string input = "";
    private string resultWord = "";
    private string resultType = "";
    private string resultDef = "";
    private string message = "";
    private bool loaded;

    private void Start()
    {
        message = "로딩 중...";
        StartCoroutine(LoadWords());
    }

    private System.Collections.IEnumerator LoadWords()
    {
        yield return null;   // 첫 화면을 먼저 그린 뒤 로딩 시작
        float start = Time.realtimeSinceStartup;

        TextAsset asset = Resources.Load<TextAsset>("words");
        if (asset == null)
        {
            message = "words.txt를 찾을 수 없습니다. Assets/Resources 폴더를 확인하세요.";
            yield break;
        }

        string text = asset.text;
        words = new Dictionary<string, WordInfo>(200000);
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
                    words[text.Substring(pos, t1 - pos)] = new WordInfo
                    {
                        type = text.Substring(t1 + 1, t2 - t1 - 1),
                        definition = text.Substring(t2 + 1, end - t2 - 1).TrimEnd('\r')
                    };
                }
            }

            pos = end + 1;
            if (++count % 20000 == 0) yield return null;   // 일정량마다 화면 갱신
        }

        loaded = true;
        float sec = Time.realtimeSinceStartup - start;
        message = $"단어 {words.Count:N0}개 로딩 완료 ({sec:F2}초)";
    }

    private void Search()
    {
        string key = input.Trim();
        if (key.Length == 0) return;

        if (words.TryGetValue(key, out WordInfo info))
        {
            resultWord = key;
            resultType = info.type;
            resultDef = info.definition;
            message = "";
        }
        else
        {
            resultWord = resultType = resultDef = "";
            message = $"'{key}'은(는) 사전에 없는 단어입니다.";
        }
    }

    private void OnGUI()
    {
        int fs = Mathf.RoundToInt(Screen.height * 0.035f);
        var label = new GUIStyle(GUI.skin.label) { fontSize = fs, wordWrap = true };
        var field = new GUIStyle(GUI.skin.textField) { fontSize = fs };
        var button = new GUIStyle(GUI.skin.button) { fontSize = fs };
        var title = new GUIStyle(label) { fontSize = Mathf.RoundToInt(fs * 1.5f), fontStyle = FontStyle.Bold };

        float margin = Screen.width * 0.05f;
        GUILayout.BeginArea(new Rect(margin, margin, Screen.width - margin * 2, Screen.height - margin * 2));

        GUILayout.Label("단어 검색", label);

        GUILayout.BeginHorizontal();
        GUI.SetNextControlName("input");
        input = GUILayout.TextField(input, field, GUILayout.Height(fs * 2f));
        GUI.enabled = loaded;
        bool clicked = GUILayout.Button("검색", button, GUILayout.Width(fs * 5f), GUILayout.Height(fs * 2f));
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        // 엔터키로도 검색
        Event e = Event.current;
        bool enter = e.type == EventType.KeyDown &&
                     (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) &&
                     GUI.GetNameOfFocusedControl() == "input";

        if (loaded && (clicked || enter))
        {
            Search();
            if (enter) e.Use();
        }

        GUILayout.Space(fs);

        if (resultWord != "")
        {
            GUILayout.Label(resultWord, title);
            GUILayout.Label(resultType, label);
            GUILayout.Label(resultDef, label);
        }

        if (message != "") GUILayout.Label(message, label);

        GUILayout.EndArea();
    }
}