using System.Collections.Generic;
using UnityEngine;

public class WordSearchTest : MonoBehaviour
{
    private Vector2 scroll;
    private Rect dragArea;     // 스크롤 영역 (화면 GUI 좌표)
    private bool dragging;
    private float lastY;

    private struct WordInfo
    {
        public string type;         // 유형
        public string field;        // 전문 분야
        public string definition;   // 뜻
    }

    private Dictionary<string, List<WordInfo>> words;
    private List<WordInfo> resultMeanings = new List<WordInfo>();
    private string input = "";
    private string resultWord = "";
    private string resultType = "";
    private string resultField = "";
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

        loaded = true;
        float sec = Time.realtimeSinceStartup - start;
        message = $"단어 {words.Count:N0}개 로딩 완료 ({sec:F2}초)";
    }

    private void Search()
    {
        scroll = Vector2.zero;

        string key = input.Trim();
        if (key.Length == 0) return;

        if (words.TryGetValue(key, out var list))
        {
            resultWord = key;
            resultMeanings = list;
            message = "";
        }
        else
        {
            resultWord = "";
            resultMeanings = new List<WordInfo>();
            message = $"'{key}'은(는) 사전에 없는 단어입니다.";
        }
    }

    private void OnGUI()
    {
        Event e = Event.current;

        // 1) 드래그 스크롤: 스크롤뷰가 이벤트를 가져가기 전에 먼저 처리
        if (e.type == EventType.MouseDown)
        {
            dragging = dragArea.Contains(e.mousePosition);
            lastY = e.mousePosition.y;
        }
        else if (e.type == EventType.MouseDrag && dragging)
        {
            float dy = e.mousePosition.y - lastY;
            scroll.y = Mathf.Max(0f, scroll.y - dy);   // 손가락을 위로 올리면 아래 내용이 보임
            lastY = e.mousePosition.y;
            e.Use();                                    // 스크롤뷰가 또 처리하지 않도록 소비
        }
        else if (e.type == EventType.MouseUp)
        {
            dragging = false;
        }

        int fs = Mathf.RoundToInt(Screen.height * 0.035f);
        var label = new GUIStyle(GUI.skin.label) { fontSize = fs, wordWrap = true };
        var field = new GUIStyle(GUI.skin.textField) { fontSize = fs };
        var button = new GUIStyle(GUI.skin.button) { fontSize = fs };
        var title = new GUIStyle(label) { fontSize = Mathf.RoundToInt(fs * 1.5f), fontStyle = FontStyle.Bold };

        GUI.skin.verticalScrollbar.fixedWidth = fs * 0.8f;
        GUI.skin.verticalScrollbarThumb.fixedWidth = fs * 0.8f;

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

        bool enter = e.type == EventType.KeyDown &&
                     (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) &&
                     GUI.GetNameOfFocusedControl() == "input";

        if (loaded && (clicked || enter))
        {
            Search();
            if (enter) e.Use();
        }

        GUILayout.Space(fs);

        if (message != "") GUILayout.Label(message, label);

        if (resultWord != "")
        {
            scroll = GUILayout.BeginScrollView(scroll, false, false,
                GUIStyle.none, GUI.skin.verticalScrollbar, GUILayout.ExpandHeight(true));

            GUILayout.Label(resultWord, title);

            for (int i = 0; i < resultMeanings.Count; i++)
            {
                WordInfo m = resultMeanings[i];

                if (resultMeanings.Count > 1)
                    GUILayout.Label($"[{i + 1}] {m.type}", label);
                else
                    GUILayout.Label(m.type, label);

                GUILayout.Label($"전문 분야: {m.field}", label);
                GUILayout.Label(m.definition, label);
                GUILayout.Space(fs * 0.5f);
            }

            GUILayout.EndScrollView();

            // 스크롤 영역 위치 저장 (BeginArea 안의 좌표라서 margin을 더해 화면 GUI 좌표로 변환)
            if (e.type == EventType.Repaint)
            {
                Rect r = GUILayoutUtility.GetLastRect();
                dragArea = new Rect(r.x + margin, r.y + margin, r.width, r.height);
            }
        }

        GUILayout.EndArea();
    }
}