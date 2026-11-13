using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class TypesetScreen : MonoBehaviour
{
    [Header("화면 전환")]
    [SerializeField] private CanvasGroup[] normalUI;   // 조판 중에 숨길 기존 UI (TopUI, BottomUI, MainHud)

    [Header("자모 키패드")]
    [SerializeField] private JamoKey keyPrefab;
    [SerializeField] private Transform keypad;

    [Header("입력 / 결과")]
    [SerializeField] private TMP_Text composedText;    // 조합된 글자
    [SerializeField] private TMP_Text rawText;         // 누른 자모 순서
    [SerializeField] private TMP_Text resultText;
    [SerializeField] private ScrollRect resultScroll;

    [Header("규칙")]
    [SerializeField] private bool consumeOnSuccess = true;   // 단어가 사전에 있으면 쓴 자모를 소모

    private const int MaxTyped = 24;

    /// <summary>
    /// 사전에 있는 단어를 찾았을 때 (나중에 도감이 이 신호를 받음)
    /// </summary>
    public event Action<string, List<WordInfo>> OnWordFound;

    private CanvasGroup group;
    private JamoInventory inventory;
    private readonly Dictionary<string, JamoKey> keys = new Dictionary<string, JamoKey>();
    private readonly List<string> typed = new List<string>();

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();

        foreach (char c in JamoTable.All)
        {
            string g = c.ToString();
            JamoKey k = Instantiate(keyPrefab, keypad);
            k.Setup(g, OnKey);
            keys[g] = k;
        }

        Apply(group, false);   // 처음엔 숨김
    }

    private void Start()
    {
        inventory = JamoInventory.Instance;
        inventory.OnChanged += OnCountChanged;
        Refresh();
    }

    private void OnDestroy()
    {
        if (inventory != null) inventory.OnChanged -= OnCountChanged;
    }

    // 조판 버튼이 부르는 함수
    public void Enter()
    {
        foreach (var g in normalUI) Apply(g, false);
        Apply(group, true);
        WordDictionary.Instance.Ensure();
        Refresh();
    }

    // 나가기 버튼이 부르는 함수
    public void Exit()
    {
        Apply(group, false);
        foreach (var g in normalUI) Apply(g, true);
    }

    // ---------- 입력 ----------
    private void OnKey(string g)
    {
        if (typed.Count >= MaxTyped || Available(g) <= 0) return;
        typed.Add(g);
        Refresh();
    }

    public void Backspace()
    {
        if (typed.Count == 0) return;
        typed.RemoveAt(typed.Count - 1);
        Refresh();
    }

    public void ClearAll()
    {
        typed.Clear();
        Refresh();
    }

    // ---------- 검색 ----------
    public void Search()
    {
        if (typed.Count == 0)
        {
            ShowMessage("아래 자모를 눌러 단어를 만들어 보세요.");
            return;
        }

        var r = HangulComposer.Compose(typed);
        if (!r.complete)
        {
            ShowMessage("완성되지 않은 글자가 있어요.\n글자마다 초성과 중성이 필요해요.");
            return;
        }

        var dict = WordDictionary.Instance;
        if (!dict.IsLoaded)
        {
            dict.Ensure();
            ShowMessage("사전을 불러오는 중이에요.\n잠시 후 다시 눌러 주세요.");
            return;
        }

        if (!dict.TryFind(r.text, out List<WordInfo> meanings))
        {
            ShowMessage($"'{r.text}'은(는) 사전에 없는 단어예요.");   // 자모는 그대로 둠
            return;
        }

        ShowWord(r.text, meanings);

        if (consumeOnSuccess)
            foreach (string g in typed) inventory.TryUse(g);

        typed.Clear();
        Refresh();
        OnWordFound?.Invoke(r.text, meanings);
    }

    // ---------- 화면 ----------
    private int Available(string g)
    {
        int n = inventory.Count(g);
        foreach (string t in typed) if (t == g) n--;
        return n;
    }

    private void OnCountChanged(string glyph, int count)
    {
        if (keys.TryGetValue(glyph, out JamoKey k)) k.SetCount(Available(glyph));
    }

    private void Refresh()
    {
        if (inventory == null) return;

        var r = HangulComposer.Compose(typed);
        composedText.text = typed.Count == 0 ? "자모를 눌러 단어를 만들어 보세요" : r.text;
        composedText.color = (typed.Count == 0 || r.complete) ? Color.white : new Color(1f, 0.75f, 0.5f);
        rawText.text = string.Concat(typed);

        foreach (var kv in keys) kv.Value.SetCount(Available(kv.Key));
    }

    private void ShowMessage(string msg)
    {
        resultText.text = msg;
        if (resultScroll != null) resultScroll.verticalNormalizedPosition = 1f;
    }

    private void ShowWord(string word, List<WordInfo> list)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<size=72><b>{word}</b></size>");

        for (int i = 0; i < list.Count; i++)
        {
            WordInfo m = list[i];
            sb.AppendLine();
            sb.AppendLine(list.Count > 1 ? $"[{i + 1}] {Safe(m.type)}" : Safe(m.type));
            sb.AppendLine($"전문 분야: {Safe(m.field)}");
            sb.AppendLine(Safe(m.definition));
        }

        resultText.text = sb.ToString();
        if (resultScroll != null) resultScroll.verticalNormalizedPosition = 1f;
    }

    // 뜻에 '<' 가 들어 있어도 서식 기호로 읽히지 않게 함
    private static string Safe(string s)
    {
        return string.IsNullOrEmpty(s) ? "" : s.Replace("<", "<noparse><</noparse>");
    }

    private static void Apply(CanvasGroup g, bool on)
    {
        g.alpha = on ? 1f : 0f;
        g.interactable = on;
        g.blocksRaycasts = on;
    }
}