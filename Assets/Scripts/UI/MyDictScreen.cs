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
    //[SerializeField] private Button rowPrefab;
    //[SerializeField] private Transform listContent;
    //[SerializeField] private GameObject emptyText;
    [SerializeField] private CanvasGroup listPanel;
    [SerializeField] private BookView book;
    [SerializeField] private TMP_Text countText;
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
    [SerializeField] private GameObject[] hideInEdit;   // [수정] 뜻을 고칠 때 숨길 것 (BackspaceButton, ClearButton)

    [Header("규칙 (인스펙터에서 조절)")]
    [SerializeField] private int inkPerMeaningChar = 3;   // 뜻 한 글자당 잉크 (공백 제외)
    [SerializeField] private int maxWordLength = 8;       // 단어 이름 최대 글자 수
    [SerializeField] private int maxMeaningLength = 100;  // 뜻 최대 글자 수

    private const int MaxTyped = 40;

    private CanvasGroup group;
    private JamoInventory inventory;
    private readonly Dictionary<string, JamoKey> keys = new Dictionary<string, JamoKey>();
    private readonly List<string> typed = new List<string>();
    private bool open, armed;
    private bool editing;          // [수정] 뜻 수정 중인지
    private MyWord editTarget;     // [수정] 고치는 중인 단어
    private string defaultRegisterLabel;
    private Coroutine armRoutine, flashRoutine, liveRoutine, dictRoutine;

    private string focusWord;    // 목록을 다시 그릴 때 이 단어가 있는 쪽을 펼침
    private bool resetBook;      // 탭에 들어올 때는 첫 장부터

    // 입력 칸의 확정된 글자 + (PC에서) 아직 확정되지 않은 한글 조합 중인 글자
    private string LiveText
    {
        get
        {
            string s = meaningInput.text ?? "";

#if ENABLE_LEGACY_INPUT_MANAGER
            s += Input.compositionString ?? "";
#endif

            return s.Replace("\u200B", "");   // 입력 칸이 붙이는 안 보이는 글자는 뺌
        }
    }

    private string Meaning => LiveText.Trim();
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
        meaningInput.onSelect.AddListener(_ => StartLiveWatch());       // 입력창을 누르면 확인 시작
        meaningInput.onDeselect.AddListener(_ => OnFieldDeselected());  // 벗어나면 확인 끝
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
        if (popup != null) popup.OnEditRequested += BeginEdit;   // [수정]

        book.OnWordClicked += OnBookWordClicked;
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
        if (popup != null) popup.OnEditRequested -= BeginEdit;   // [수정]

        if (book != null) book.OnWordClicked -= OnBookWordClicked;
    }

    // ---------- 화면 전환 ----------
    // 탭 버튼이 부르는 함수
    public void Enter()
    {
        foreach (var g in normalUI) Apply(g, false);
        Apply(group, true);
        open = true;
        resetBook = true;   // 탭에 들어올 때마다 첫 장부터

        popup.Hide();
        WordDictionary.Instance.Ensure();
        ShowList();
    }

    // 나가기 버튼이 부르는 함수
    public void Exit()
    {
        open = false;
        popup.Hide();
        StopLiveWatch();
        if (dictRoutine != null) { StopCoroutine(dictRoutine); dictRoutine = null; }
        meaningInput.DeactivateInputField();
        Disarm();
        Apply(group, false);
        foreach (var g in normalUI) Apply(g, true);
    }

    private void ShowList()
    {
        EndEdit();   // [수정]
        Apply(formPanel, false);
        Apply(listPanel, true);
        StopLiveWatch();
        meaningInput.DeactivateInputField();
        Disarm();
        RebuildList();
    }

    private void ShowForm()
    {
        Apply(listPanel, false);
        Apply(formPanel, true);
        WordDictionary.Instance.Ensure();
        if (!WordDictionary.Instance.IsLoaded && dictRoutine == null)
            dictRoutine = StartCoroutine(WaitDictionary());
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

    // ---------- 뜻 수정 ----------  [수정] 이 블록 전체가 새로 추가됨
    // 팝업의 수정 버튼이 부름
    private void BeginEdit(MyWord word)
    {
        if (word == null) return;

        editing = true;
        editTarget = word;
        typed.Clear();
        meaningInput.text = word.definition;   // 기존 뜻을 입력 칸에 넣어 둠
        SetEditLayout(true);
        ShowForm();
    }

    private void EndEdit()
    {
        if (!editing) return;
        editing = false;
        editTarget = null;
        SetEditLayout(false);
    }

    // 수정 중에는 키패드와 자모 버튼을 숨김 (단어 이름은 못 바꾸니까)
    private void SetEditLayout(bool on)
    {
        keypad.gameObject.SetActive(!on);
        if (hideInEdit == null) return;
        foreach (var o in hideInEdit) if (o != null) o.SetActive(!on);
    }

    // ---------- 목록 ----------
    private void RebuildList()
    {
        var names = new List<string>();
        foreach (MyWord w in MyDictionary.Instance.Words) names.Add(w.word);
        names.Sort((a, b) => string.CompareOrdinal(a, b));   // 가나다순

        if (flashRoutine == null) UpdateCount();

        book.SetWords(names, focusWord);
        if (resetBook) { book.GoToFirst(); resetBook = false; }
        focusWord = null;
    }

    // 책에서 단어를 눌렀을 때
    private void OnBookWordClicked(string word)
    {
        if (MyDictionary.Instance.TryGet(word, out MyWord entry)) popup.Show(entry);
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
        if (editing) return;   // [수정]
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
        if (string.IsNullOrEmpty(s)) return 0;
        foreach (char c in s) if (!char.IsWhiteSpace(c) && c != '\u200B') n++;
        return n;
    }

    private void RefreshForm()
    {
        if (inventory == null) return;
        Disarm();   // 내용이 바뀌면 "한 번 더 눌러 등록"은 취소

        foreach (var kv in keys) kv.Value.SetCount(Available(kv.Key));

        int chars = CountChars(Meaning);

        // [수정] 뜻 수정 중: 늘어난 글자만큼만 잉크를 받음
        if (editing && editTarget != null)
        {
            int oldChars = CountChars(editTarget.definition);
            int added = Mathf.Max(0, chars - oldChars);
            long editCost = added * (long)inkPerMeaningChar;
            string editHint = CheckEdit(chars, editCost);

            composedText.text = editTarget.word;
            composedText.color = Color.white;
            rawText.text = "단어 이름은 바꿀 수 없어요";

            infoText.text = $"뜻 {chars}자 (기존 {oldChars}자)  늘어난 {added}자 x {inkPerMeaningChar} = 잉크 {editCost:N0}   (보유 {inventory.Ink:N0})\n"
                          + (editHint == null ? "<color=#8FD3FF>수정할 수 있어요</color>" : $"<color=#FFB380>{editHint}</color>");
            registerButton.interactable = editHint == null;
            return;
        }

        var r = HangulComposer.Compose(typed);
        composedText.text = typed.Count == 0 ? "자모를 눌러 단어 이름을 만들어 주세요" : r.text;
        composedText.color = (typed.Count == 0 || r.complete) ? Color.white : new Color(1f, 0.75f, 0.5f);
        rawText.text = string.Concat(typed);

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

    // [수정] 뜻을 고칠 수 없는 이유를 돌려준다. 고칠 수 있으면 null
    private string CheckEdit(int chars, long cost)
    {
        if (chars == 0) return "뜻을 적어 주세요.";
        if (Meaning == editTarget.definition) return "바뀐 내용이 없어요.";
        if (inventory.Ink < cost) return $"잉크가 부족해요. (필요 {cost:N0})";
        return null;
    }

    // "등록"(수정 중에는 "수정") 버튼: 첫 번째는 확인, 두 번째에 실제로 처리
    public void OnRegisterClicked()
    {
        int chars = CountChars(Meaning);

        // [수정] 뜻 수정
        if (editing && editTarget != null)
        {
            int added = Mathf.Max(0, chars - CountChars(editTarget.definition));
            long editCost = added * (long)inkPerMeaningChar;

            if (CheckEdit(chars, editCost) != null) { RefreshForm(); return; }

            if (!armed)
            {
                armed = true;
                registerLabel.text = "한 번 더 눌러 수정";
                armRoutine = StartCoroutine(DisarmLater());
                return;
            }

            Disarm();

            string editedWord = editTarget.word;
            inventory.TryUseInk(editCost);   // 줄어든 만큼은 돌려주지 않음
            MyDictionary.Instance.SetDefinition(editedWord, Meaning);
            inventory.Save();

            focusWord = editedWord;
            meaningInput.text = "";
            ShowList();
            ShowFlash($"'{editedWord}'의 뜻을 고쳤어요!");
            return;
        }

        var r = HangulComposer.Compose(typed);
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

        focusWord = r.text;
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
        if (registerLabel != null) registerLabel.text = editing ? "수정" : defaultRegisterLabel;   // [수정]
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

    // ---------- 뜻 입력 확인 ----------
    // 한글을 치는 동안은 조합 중인 글자가 값에 들어오지 않아서, 입력 중일 때만 짧은 간격으로 확인한다
    private void StartLiveWatch()
    {
        if (liveRoutine == null) liveRoutine = StartCoroutine(LiveWatch());
    }

    private void StopLiveWatch()
    {
        if (liveRoutine != null) { StopCoroutine(liveRoutine); liveRoutine = null; }
    }

    private void OnFieldDeselected()
    {
        StopLiveWatch();
        if (open && FormVisible) RefreshForm();   // 입력이 끝났을 때 한 번 정리
    }

    private IEnumerator LiveWatch()
    {
        string last = LiveText;
        while (true)
        {
            yield return new WaitForSecondsRealtime(0.1f);

            string live = LiveText;
            if (live == last) continue;

            last = live;
            RefreshForm();
        }
    }

    private IEnumerator WaitDictionary()
    {
        while (!WordDictionary.Instance.IsLoaded)
            yield return new WaitForSecondsRealtime(0.3f);

        dictRoutine = null;
        if (open && FormVisible) RefreshForm();
    }
}