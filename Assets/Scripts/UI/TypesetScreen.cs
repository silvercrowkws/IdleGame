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
    [Tooltip("해제하면 시험 모드(자모 소모도 inventory.json에 기록도 X)")]
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

    [Header("나만의 사전")]
    [SerializeField] private Button registerButton;   // "나만의 사전에 등록할까요?" 버튼 (결과 영역 안)

    /// <summary>
    /// 나만의 사전에 등록하려고 할 때 (조합된 단어, 쓴 자모 목록)
    /// </summary>
    public event Action<string, IReadOnlyList<string>> OnRegisterRequested;

    [Header("잉크")]
    [SerializeField] private int inkPerWordChar = 5;   // 새 단어 한 글자당 얻는 잉크

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

        registerButton.onClick.AddListener(RequestRegister);
        registerButton.gameObject.SetActive(false);

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

        // 들어올 때마다 이전 입력과 검색 결과를 비움
        typed.Clear();
        resultText.text = "";
        if (resultScroll != null) resultScroll.verticalNormalizedPosition = 1f;
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
        registerButton.gameObject.SetActive(false);   // 이전 제안을 숨김

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

        // 대사전에 없는 단어: 자모를 소모하지 않음 (입력 칸에 그대로 남음)
        if (!dict.TryFind(r.text, out List<WordInfo> meanings))
        {
            // 나만의 사전에 이미 있는 단어: 뜻을 보여주고 등록은 제안하지 않음
            if (MyDictionary.Instance.TryGet(r.text, out MyWord mine))
            {
                ShowWord(r.text, new List<WordInfo> { mine.ToInfo() },
                    $"'{r.text}'은(는) 나만의 사전에 있는 단어예요.\n자모는 소모되지 않았어요.");
                return;
            }

            // 어디에도 없는 단어: 나만의 사전 등록을 제안
            ShowMessage($"'{r.text}'은(는) 사전에 없는 단어예요.\n자모는 소모되지 않았어요.");
            registerButton.gameObject.SetActive(true);
            return;
        }

        // 이미 찾은 단어: 뜻은 보여주되 자모는 소모하지 않음
        if (inventory.HasWord(r.text))
        {
            WordCollection.Instance.Add(r.text);   // 도감에 아직 없으면 채워 넣음 (이미 있으면 무시)
            ShowWord(r.text, meanings, "이미 찾은 단어예요. 자모는 소모되지 않았어요.");
            return;
        }

        // 시험 모드: 소모도 기록도 하지 않음
        if (!consumeOnSuccess)
        {
            ShowWord(r.text, meanings, "(시험 모드) 자모를 소모하거나 기록하지 않았어요.");
            return;
        }

        // 새로 찾은 단어: 자모 소모 + 기록
        foreach (string g in typed) inventory.TryUse(g);
        inventory.AddWord(r.text);

        long gained = r.text.Length * inkPerWordChar;
        inventory.AddInk(gained);
        inventory.Save();   // 앱이 꺼져도 잃지 않게 바로 저장

        ShowWord(r.text, meanings, $"새로운 단어를 찾았어요! 기록했어요.\n잉크 +{gained}");
        typed.Clear();
        Refresh();

        WordCollection.Instance.Add(r.text);
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
        registerButton.gameObject.SetActive(false);

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

    private void ShowWord(string word, List<WordInfo> list, string note)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<size=72><b>{word}</b></size>");
        if (!string.IsNullOrEmpty(note)) sb.AppendLine($"<color=#FFD966>{note}</color>");

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

    // 등록 버튼이 부르는 함수
    private void RequestRegister()
    {
        // 눌린 시점의 입력을 다시 확인 (그 사이에 바뀌었을 수 있음)
        var r = HangulComposer.Compose(typed);
        if (!r.complete) return;

        if (OnRegisterRequested == null)
        {
            // 나만의 사전 탭이 아직 없을 때의 임시 안내
            ShowMessage("나만의 사전은 아직 준비 중이에요.");
            registerButton.gameObject.SetActive(false);
            return;
        }

        OnRegisterRequested.Invoke(r.text, typed);
    }
}