using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class DexScreen : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
{
    [Header("화면 전환")]
    [SerializeField] private CanvasGroup[] normalUI;   // 도감에서 숨길 기존 UI (TopUI, BottomUI, MainHud)

    [Header("책 (쪽 오브젝트 안에 WordList, Number 순서로 있어야 해요)")]
    [SerializeField] private RectTransform leftPage;
    [SerializeField] private TMP_Text leftText;
    [SerializeField] private TMP_Text leftNumber;
    [SerializeField] private RectTransform rightPage;
    [SerializeField] private TMP_Text rightText;
    [SerializeField] private TMP_Text rightNumber;
    [SerializeField] private TMP_Text infoText;

    [Header("단어 팝업")]
    [SerializeField] private DexWordPopup popup;

    [Header("버튼")]
    [SerializeField] private Button prevButton;
    [SerializeField] private Button nextButton;
    [SerializeField] private TMP_Text sortLabel;

    [Header("규칙")]
    [Tooltip("한 쪽에 적을 단어 수. 0이면 쪽 크기에 맞춰 자동")]
    [SerializeField] private int wordsPerPage = 0;
    [SerializeField, Range(0.3f, 1f)] private float dragToFlip = 0.5f;   // 책 폭의 이만큼 끌면 끝까지 넘어감
    [SerializeField] private float flickSpeed = 900f;                    // 이 속도 이상으로 쓸면 넘어감
    [SerializeField] private float flipTime = 0.35f;

    [Header("소리")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] flipSounds;
    [SerializeField, Range(0f, 1f)] private float flipVolume = 0.6f;

    private class Page
    {
        public RectTransform root;
        public TMP_Text text;
        public TMP_Text number;
        public Image bg;
        public Color baseColor;
    }

    private CanvasGroup group;
    private Canvas rootCanvas;
    private Page left, right, flipLeft, flipRight;

    private readonly List<string> view = new List<string>();
    private int rows = 14;
    private int spread;               // 몇 번째 펼침면인지 (왼쪽+오른쪽 쪽이 한 펼침면)
    private string lastWord;          // 다시 열었을 때 보던 곳을 기억 (왼쪽 쪽의 첫 단어)
    private bool byAlpha = true;      // true: 가나다순, false: 등록순

    private bool dragging, animating;
    private int flipDir;              // +1 다음, -1 이전, 0 넘기는 중 아님
    private float flipProgress;       // 0 ~ 1
    private float dragX, velocity;
    private Coroutine anim;

    private int SpreadCount => Mathf.Max(1, Mathf.CeilToInt(view.Count / (float)(rows * 2)));
    private float Scale => rootCanvas != null ? rootCanvas.scaleFactor : 1f;
    private float BookWidth => Mathf.Max(leftPage.rect.width + rightPage.rect.width, 1f);

    // ---------- 준비 ----------
    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
        Canvas c = GetComponentInParent<Canvas>();
        rootCanvas = c != null ? c.rootCanvas : null;

        left = MakePage(leftPage, leftText, leftNumber);
        right = MakePage(rightPage, rightText, rightNumber);
        flipLeft = ClonePage(left);     // 넘어가는 종이는 쪽을 복제해서 사용
        flipRight = ClonePage(right);

        Apply(group, false);   // 처음엔 숨김
    }

    private static Page MakePage(RectTransform root, TMP_Text text, TMP_Text number)
    {
        var p = new Page { root = root, text = text, number = number, bg = root.GetComponent<Image>() };
        if (p.bg != null) p.baseColor = p.bg.color;
        return p;
    }

    private static Page ClonePage(Page src)
    {
        GameObject go = Instantiate(src.root.gameObject, src.root.parent);
        go.name = src.root.name + "_Flip";
        var root = (RectTransform)go.transform;
        Page p = MakePage(root,
            root.GetChild(src.text.transform.GetSiblingIndex()).GetComponent<TMP_Text>(),
            root.GetChild(src.number.transform.GetSiblingIndex()).GetComponent<TMP_Text>());
        go.SetActive(false);
        return p;
    }

    // ---------- 화면 전환 ----------
    // 도감 탭 버튼이 부르는 함수
    public void Enter()
    {
        foreach (var g in normalUI) Apply(g, false);
        Apply(group, true);

        StopFlip();
        popup.Hide();
        WordDictionary.Instance.Ensure();   // 팝업에서 뜻을 보여주려면 사전이 필요

        byAlpha = true;
        lastWord = null;
        Rebuild();
    }

    public void Exit()
    {
        StopFlip();
        popup.Hide();
        Apply(group, false);
        foreach (var g in normalUI) Apply(g, true);
    }

    public void Prev() { Flip(-1); }
    public void Next() { Flip(1); }

    public void ToggleSort()
    {
        if (animating || dragging) return;
        byAlpha = !byAlpha;
        Rebuild();   // 보던 단어가 있던 쪽을 유지
    }

    // ---------- 목록 ----------
    private void Rebuild()
    {
        view.Clear();
        foreach (var w in WordCollection.Instance.Words) view.Add(w.word);
        if (byAlpha) view.Sort((a, b) => string.CompareOrdinal(a, b));

        sortLabel.text = byAlpha ? "가나다순" : "등록순";

        Canvas.ForceUpdateCanvases();
        rows = ComputeRows();

        spread = 0;
        if (lastWord != null)
        {
            int i = view.IndexOf(lastWord);
            if (i >= 0) spread = i / (rows * 2);
        }
        spread = Mathf.Clamp(spread, 0, SpreadCount - 1);

        ShowSpread();
    }

    // 쪽 높이에 들어가는 줄 수를 글꼴 크기에 맞춰 계산
    private int ComputeRows()
    {
        if (wordsPerPage > 0) return wordsPerPage;

        float one = leftText.GetPreferredValues("가").y;
        float two = leftText.GetPreferredValues("가\n가").y;
        float step = two - one;
        if (step < 1f) return 12;

        float h = leftText.rectTransform.rect.height;
        return Mathf.Clamp(Mathf.FloorToInt((h - one) / step) + 1, 4, 60);
    }

    private void ShowSpread()
    {
        FillPage(left, spread, false);
        FillPage(right, spread, true);

        int start = spread * rows * 2;
        if (start < view.Count) lastWord = view[start];

        infoText.text = view.Count == 0 ? "" : $"찾은 단어 {view.Count}개";
        prevButton.interactable = CanFlip(-1);
        nextButton.interactable = CanFlip(1);
    }

    // 한 쪽에 단어 목록을 채움
    private void FillPage(Page p, int s, bool isRight)
    {
        p.number.text = (s * 2 + (isRight ? 2 : 1)).ToString();

        if (view.Count == 0)
        {
            p.text.text = isRight ? "" : "<size=70%>아직 찾은 단어가\n없어요.\n\n조판에서 단어를\n만들어 보세요.</size>";
            return;
        }

        int start = s * rows * 2 + (isRight ? rows : 0);
        var sb = new StringBuilder();
        for (int i = 0; i < rows; i++)
        {
            int idx = start + i;
            if (idx >= view.Count) break;
            if (i > 0) sb.Append('\n');
            sb.Append(view[idx]);
        }
        p.text.text = sb.ToString();
    }

    // ---------- 쪽 넘기기 ----------
    private bool CanFlip(int dir)
    {
        return dir > 0 ? spread < SpreadCount - 1 : spread > 0;
    }

    // 버튼용: 처음부터 끝까지 자동으로 넘김
    public void Flip(int dir)
    {
        if (animating || dragging || !CanFlip(dir)) return;
        BeginFlip(dir);
        PlayFlipSound();
        anim = StartCoroutine(Animate(1f, true));
    }

    private void BeginFlip(int dir)
    {
        flipDir = dir;
        int t = spread + dir;

        if (dir > 0)
        {
            CopyPage(right, flipRight);       // 넘어가는 종이 = 지금의 오른쪽 쪽
            FillPage(right, t, true);         // 아래에서 드러날 새 오른쪽 쪽
            FillPage(flipLeft, t, false);     // 나중에 왼쪽에 내려앉을 쪽
        }
        else
        {
            CopyPage(left, flipLeft);         // 넘어가는 종이 = 지금의 왼쪽 쪽
            FillPage(left, t, false);         // 아래에서 드러날 새 왼쪽 쪽
            FillPage(flipRight, t, true);     // 나중에 오른쪽에 내려앉을 쪽
        }

        SetFlipProgress(0f);
    }

    // p: 0 → 0.5 종이가 책등 쪽으로 접힘, 0.5 → 1 반대편에서 펼쳐짐
    private void SetFlipProgress(float p)
    {
        flipProgress = p;
        bool first = p < 0.5f;
        float fold = 1f - 2f * p;
        float unfold = 2f * p - 1f;

        if (flipDir > 0)
        {
            SetFlipper(flipRight, first, fold);
            SetFlipper(flipLeft, !first, unfold);
        }
        else
        {
            SetFlipper(flipLeft, first, fold);
            SetFlipper(flipRight, !first, unfold);
        }
    }

    private static void SetFlipper(Page f, bool on, float scaleX)
    {
        if (f.root.gameObject.activeSelf != on) f.root.gameObject.SetActive(on);
        if (!on) return;

        scaleX = Mathf.Clamp(scaleX, 0.001f, 1f);
        f.root.localScale = new Vector3(scaleX, 1f, 1f);

        if (f.bg != null)
        {
            float k = 1f - 0.4f * (1f - scaleX);   // 접힐수록 어두워짐
            Color c = f.baseColor;
            f.bg.color = new Color(c.r * k, c.g * k, c.b * k, c.a);
        }
    }

    private static void CopyPage(Page src, Page dst)
    {
        dst.text.text = src.text.text;
        dst.number.text = src.number.text;
    }

    private void HideFlippers()
    {
        foreach (Page f in new[] { flipLeft, flipRight })
        {
            f.root.gameObject.SetActive(false);
            f.root.localScale = Vector3.one;
            if (f.bg != null) f.bg.color = f.baseColor;
        }
    }

    private IEnumerator Animate(float to, bool commit)
    {
        animating = true;
        float from = flipProgress;
        float dur = Mathf.Max(0.01f, flipTime * Mathf.Abs(to - from));

        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / dur;
            SetFlipProgress(Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t))));
            yield return null;
        }
        SetFlipProgress(to);

        if (commit) spread += flipDir;
        HideFlippers();
        flipDir = 0;
        ShowSpread();   // 취소했을 때도 원래 쪽 내용으로 되돌림

        animating = false;
        anim = null;
    }

    private void StopFlip()
    {
        if (anim != null) { StopCoroutine(anim); anim = null; }
        animating = false;
        dragging = false;
        flipDir = 0;
        HideFlippers();
    }

    private int lastSoundIndex = -1;

    private void PlayFlipSound()
    {
        if (audioSource == null || flipSounds == null || flipSounds.Length == 0) return;

        int i = Random.Range(0, flipSounds.Length);
        /*if (flipSounds.Length > 1 && i == lastSoundIndex)
            i = (i + 1 + Random.Range(0, flipSounds.Length - 1)) % flipSounds.Length;   // 직전과 다른 소리로*/
        lastSoundIndex = i;

        audioSource.pitch = Random.Range(0.92f, 1.08f);   // 매번 조금씩 다르게
        audioSource.PlayOneShot(flipSounds[i], flipVolume);
    }

    // ---------- 드래그 ----------
    public void OnBeginDrag(PointerEventData e)
    {
        if (animating || popup.IsOpen) return;
        dragging = true;
        dragX = 0f;
        velocity = 0f;
        flipDir = 0;
    }

    public void OnDrag(PointerEventData e)
    {
        if (!dragging) return;

        dragX += e.delta.x / Scale;
        float inst = e.delta.x / Scale / Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
        velocity = Mathf.Lerp(velocity, inst, 0.5f);

        if (Mathf.Abs(dragX) < 6f) return;

        int dir = dragX < 0f ? 1 : -1;   // 오른쪽에서 왼쪽으로 끌면 다음 쪽
        if (dir != flipDir)
        {
            if (flipDir != 0)            // 끌던 중에 방향을 바꿈: 원래대로 되돌리고 다시 시작
            {
                HideFlippers();
                ShowSpread();
                flipDir = 0;
            }
            if (CanFlip(dir)) BeginFlip(dir);
        }

        if (flipDir != 0)
            SetFlipProgress(Mathf.Clamp01(Mathf.Abs(dragX) / (BookWidth * dragToFlip)));
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (!dragging) return;
        dragging = false;
        if (flipDir == 0) return;

        bool flick = Mathf.Abs(velocity) > flickSpeed
                     && (velocity < 0f) == (flipDir > 0)
                     && Mathf.Abs(dragX) > 30f;
        bool commit = flipProgress > 0.3f || flick;

        if (commit) PlayFlipSound();   // 끝까지 넘어갈 때만 소리 (취소하면 소리 없음)

        anim = StartCoroutine(Animate(commit ? 1f : 0f, commit));
    }

    // ---------- 단어 누르기 ----------
    public void OnPointerClick(PointerEventData e)
    {
        if (animating || dragging || popup.IsOpen || view.Count == 0) return;

        Camera cam = e.pressEventCamera;
        string word = null;

        if (RectTransformUtility.RectangleContainsScreenPoint(leftText.rectTransform, e.position, cam))
            word = WordAtLine(leftText, false, e.position, cam);
        else if (RectTransformUtility.RectangleContainsScreenPoint(rightText.rectTransform, e.position, cam))
            word = WordAtLine(rightText, true, e.position, cam);

        if (word != null) popup.Show(word);
    }

    private string WordAtLine(TMP_Text text, bool isRight, Vector2 pos, Camera cam)
    {
        int line = TMP_TextUtilities.FindIntersectingLine(text, pos, cam);
        if (line < 0 || line >= rows) return null;

        int idx = spread * rows * 2 + (isRight ? rows : 0) + line;
        return idx < view.Count ? view[idx] : null;
    }

    private static void Apply(CanvasGroup g, bool on)
    {
        g.alpha = on ? 1f : 0f;
        g.interactable = on;
        g.blocksRaycasts = on;
    }
}