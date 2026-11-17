using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class MyDictScreen : MonoBehaviour
{
    [Header("화면 전환")]
    [SerializeField] private CanvasGroup[] normalUI;   // 이 화면에서 숨길 기존 UI (TopUI, BottomUI, MainHud)
    [SerializeField] private TypesetScreen typeset;    // 조판의 "나만의 사전에 등록할까요?" 버튼을 받음

    [Header("목록")]
    [SerializeField] private CanvasGroup listPanel;
    [SerializeField] private Button rowPrefab;
    [SerializeField] private Transform listContent;
    [SerializeField] private TMP_Text countText;
    [SerializeField] private GameObject emptyText;
    [SerializeField] private MyWordPopup popup;

    [Header("등록 폼")]
    [SerializeField] private CanvasGroup formPanel;
    [SerializeField] private JamoKey keyPrefab;
    [SerializeField] private Transform keypad;
    [SerializeField] private TMP_Text composedText;
    [SerializeField] private TMP_Text rawText;
    [SerializeField] private TMP_InputField meaningInput;
    [SerializeField] private TMP_Text infoText;
    [SerializeField] private Button registerButton;
    [SerializeField] private TMP_Text registerLabel;

    [Header("규칙 (인스펙터에서 조절)")]
    [SerializeField] private int inkPerMeaningChar = 3;   // 뜻 한 글자당 잉크 (공백 제외)
    [SerializeField] private int maxWordLength = 8;       // 단어 이름 최대 글자 수
    [SerializeField] private int maxMeaningLength = 100;  // 뜻 최대 글자 수

    private const int MaxTyped = 40;

    private CanvasGroup group;
    private JamoInventory inventory;
    private readonly Dictionary<string, JamoKey> keys = new Dictionary<string, JamoKey>();
    private readonly List<string> typed = new List<string>();
    private bool open, dictReady, armed;
    private string defaultRegisterLabel;
    private Coroutine armRoutine, flashRoutine;

    private string Meaning => (meaningInput.text ?? "").Trim();
    private bool FormVisible => formPanel.alpha > 0.5f;

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

        meaningInput.characterLimit = maxMeaningLength;
        meaningInput.onValueChanged.AddListener(_ => RefreshForm());
        defaultRegisterLabel = registerLabel.text;

        Apply(group, false);
        Apply(listPanel, false);
        Apply(formPanel, false);
    }

    private void Start()
    {
        inventory = JamoInventory.Instance;
        inventory.OnChanged += OnJamoChanged;
        inventory.OnInkChanged += OnInkChanged;
        MyDictionary.Instance.OnChanged += OnDictChanged;
        if (typeset != null) typeset.OnRegisterRequested += OnRegisterRequested;
    }

    private void OnDestroy()
    {
        if (inventory != null)
        {
            inventory.OnChanged -= OnJamoChanged;
            inventory.OnInkChanged -= OnInkChanged;
        }
        if (MyDictionary.Instance != null) MyDictionary.Instance.OnChanged -= OnDictChanged;
        if (typeset != null) typeset.OnRegisterRequested -= OnRegisterRequested;
    }

    private void Update()
    {
        // 사전을 다 불러오면 폼의 안내 문구를 갱신
        if (!open || dictReady) return;
        if (WordDictionary.Instance.IsLoaded)
        {
            dictReady = true;
            if (FormVisible) RefreshForm();
        }
    }

    // ---------- 화면 전환 ----------
    // 탭 버튼이 부르는 함수
    public void Enter()
    {
        foreach (var g in normalUI) Apply(g, false);
        Apply(group, true);
        open = true;

        popup.Hide();
        WordDictionary.Instance.Ensure();
        dictReady = WordDictionary.Instance.IsLoaded;
        ShowList();
    }

    // 나가기 버튼이 부르는 함수
    public void Exit()
    {
        open = false;
        popup.Hide();
        meaningInput.DeactivateInputField();
        Disarm();
        Apply(group, false);
        foreach (var g in normalUI) Apply(g, true);
    }

    private void ShowList()
    {
        Apply(formPanel, false);
        Apply(listPanel, true);
        meaningInput.DeactivateInputField();
        Disarm();
        RebuildList();
    }

    private void ShowForm()
    {
        Apply(listPanel, false);
        Apply(formPanel, true);
        WordDictionary.Instance.Ensure();
        RefreshForm();
    }

    // "새 단어 만들기" 버튼
    public void OpenNewForm()
    {
        typed.Clear();
        meaningInput.text = "";
        ShowForm();
    }

    // "취소" 버튼
    public void CancelForm()
    {
        typed.Clear();
        meaningInput.text = "";
        ShowList();
    }

    // 조판에서 "나만의 사전에 등록할까요?"를 눌렀을 때
    private void OnRegisterRequested(string word, IReadOnlyList<string> jamo)
    {
        var copy = new List<string>(jamo);

        typeset.Exit();   // 조판 화면을 닫고 (기존 UI가 잠깐 켜지지만 같은 프레임에 다시 숨김)
        Enter();

        typed.Clear();
        typed.AddRange(copy);   // 조판에서 만든 자모를 그대로 가져옴
        meaningInput.text = "";
        ShowForm();
    }

    // ---------- 목록 ----------
    private void RebuildList()
    {
        foreach (Transform child in listContent) Destroy(child.gameObject);

        var words = new List<MyWord>(MyDictionary.Instance.Words);
        words.Sort((a, b) => b.createdUtcTicks.CompareTo(a.createdUtcTicks));   // 최근에 만든 것부터

        if (flashRoutine == null) UpdateCount();
        emptyText.SetActive(words.Count == 0);

        foreach (MyWord w in words)
        {
            MyWord word = w;
            Button b = Instantiate(rowPrefab, listContent);
            b.GetComponentInChildren<TMP_Text>().text =
                $"{word.word}   <size=65%><color=#7A6A55>{Truncate(word.definition, 20)}</color></size>";
            b.onClick.AddListener(() => popup.Show(word));
        }
    }

    private void UpdateCount()
    {
        countText.text = $"내가 만든 단어 {MyDictionary.Instance.Count}개";
    }

    private void ShowFlash(string msg)
    {
        if (flashRoutine != null) StopCoroutine(flashRoutine);
        flashRoutine = StartCoroutine(FlashRoutine(msg));
    }

    private IEnumerator FlashRoutine(string msg)
    {
        countText.text = $"<color=#8FD3FF>{msg}</color>";
        yield return new WaitForSecondsRealtime(2.5f);
        flashRoutine = null;
        UpdateCount();
    }

    private static string Truncate(string s, int max)
    {
        if (string.IsNullOrEmpty(s)) return "";
        s = s.Replace("\n", " ");
        return s.Length <= max ? s : s.Substring(0, max) + "…";
    }

    // ---------- 등록 폼 ----------
    private void OnKey(string g)
    {
        if (typed.Count >= MaxTyped || Available(g) <= 0) return;
        typed.Add(g);
        RefreshForm();
    }

    public void Backspace()
    {
        if (typed.Count == 0) return;
        typed.RemoveAt(typed.Count - 1);
        RefreshForm();
    }

    public void ClearAll()
    {
        typed.Clear();
        RefreshForm();
    }

    private int Available(string g)
    {
        int n = inventory.Count(g);
        foreach (string t in typed) if (t == g) n--;
        return n;
    }

    private static int CountChars(string s)
    {
        int n = 0;
        foreach (char c in s) if (!char.IsWhiteSpace(c) && c != '\u200B') n++;
        return n;
    }

    private void RefreshForm()
    {
        if (inventory == null) return;
        Disarm();   // 내용이 바뀌면 "한 번 더 눌러 등록"은 취소

        var r = HangulComposer.Compose(typed);
        composedText.text = typed.Count == 0 ? "자모를 눌러 단어 이름을 만들어 주세요" : r.text;
        composedText.color = (typed.Count == 0 || r.complete) ? Color.white : new Color(1f, 0.75f, 0.5f);
        rawText.text = string.Concat(typed);

        foreach (var kv in keys) kv.Value.SetCount(Available(kv.Key));

        int chars = CountChars(Meaning);
        long cost = chars * (long)inkPerMeaningChar;
        string hint = Check(r, chars, cost);

        infoText.text = $"뜻 {chars}자 x {inkPerMeaningChar} = 잉크 {cost:N0}   (보유 {inventory.Ink:N0})\n"
                      + (hint == null ? "<color=#8FD3FF>등록할 수 있어요</color>" : $"<color=#FFB380>{hint}</color>");
        registerButton.interactable = hint == null;
    }

    // 등록할 수 없는 이유를 돌려준다. 등록할 수 있으면 null
    private string Check(HangulComposer.Result r, int chars, long cost)
    {
        if (typed.Count == 0) return "자모를 눌러 단어 이름을 만들어 주세요.";
        if (!r.complete) return "완성되지 않은 글자가 있어요.";
        if (r.text.Length > maxWordLength) return $"단어는 {maxWordLength}글자까지예요.";

        var dict = WordDictionary.Instance;
        if (!dict.IsLoaded) { dict.Ensure(); return "사전을 불러오는 중이에요."; }
        if (dict.TryFind(r.text, out _)) return $"'{r.text}'은(는) 표준국어대사전에 있는 단어예요.";
        if (MyDictionary.Instance.Has(r.text)) return $"'{r.text}'은(는) 이미 나만의 사전에 있어요.";

        if (chars == 0) return "뜻을 적어 주세요.";
        if (inventory.Ink < cost) return $"잉크가 부족해요. (필요 {cost:N0})";
        return null;
    }

    // "등록" 버튼: 첫 번째는 확인, 두 번째에 실제로 등록
    public void OnRegisterClicked()
    {
        var r = HangulComposer.Compose(typed);
        int chars = CountChars(Meaning);
        long cost = chars * (long)inkPerMeaningChar;

        if (Check(r, chars, cost) != null) { RefreshForm(); return; }

        if (!armed)
        {
            armed = true;
            registerLabel.text = "한 번 더 눌러 등록";
            armRoutine = StartCoroutine(DisarmLater());
            return;
        }

        Disarm();

        // 단어 이름의 자모와 뜻 글자 수만큼의 잉크를 소모
        foreach (string g in typed) inventory.TryUse(g);
        inventory.TryUseInk(cost);

        MyDictionary.Instance.Add(new MyWord
        {
            word = r.text,
            type = "나만의 말",
            field = "없음",
            definition = Meaning
        });
        inventory.Save();

        typed.Clear();
        meaningInput.text = "";
        ShowList();
        ShowFlash($"'{r.text}'을(를) 등록했어요!");
    }

    private IEnumerator DisarmLater()
    {
        yield return new WaitForSecondsRealtime(3f);
        armRoutine = null;
        Disarm();
    }

    private void Disarm()
    {
        armed = false;
        if (armRoutine != null) { StopCoroutine(armRoutine); armRoutine = null; }
        if (registerLabel != null) registerLabel.text = defaultRegisterLabel;
    }

    // ---------- 변화 알림 ----------
    private void OnJamoChanged(string glyph, int count)
    {
        if (open && FormVisible) RefreshForm();
    }

    private void OnInkChanged(long ink)
    {
        if (open && FormVisible) RefreshForm();
    }

    private void OnDictChanged()
    {
        if (open && listPanel.alpha > 0.5f) RebuildList();
    }

    private static void Apply(CanvasGroup g, bool on)
    {
        g.alpha = on ? 1f : 0f;
        g.interactable = on;
        g.blocksRaycasts = on;
    }
}