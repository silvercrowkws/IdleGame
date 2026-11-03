using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class GlyphPicker : MonoBehaviour
{
    [SerializeField] private CarvingBoard board;
    [SerializeField] private Button buttonPrefab;
    [SerializeField] private string glyphs = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎㅏㅐㅑㅒㅓㅔㅕㅖㅗㅘㅙㅚㅛㅜㅝㅞㅟㅠㅡㅢㅣ";

    private CanvasGroup group;
    private bool shown;

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();

        foreach (char ch in glyphs)
        {
            string g = ch.ToString();
            Button b = Instantiate(buttonPrefab, transform);
            b.GetComponentInChildren<TMP_Text>().text = g;
            b.onClick.AddListener(() => Select(g));
        }

        board.OnClosed += Show;   // 조각을 마치면 다시 선택 화면
        SetVisible(false);
    }

    private void OnDestroy()
    {
        if (board != null) board.OnClosed -= Show;
    }

    // 공방 버튼이 부르는 함수 (토글)
    public void Toggle()
    {
        if (board.IsOpen)
        {
            board.Cancel();       // 조각 중이면 초기화하고 닫기
            SetVisible(false);    // 선택 화면도 숨김
        }
        else
        {
            SetVisible(!shown);   // 선택 화면 열기/닫기
        }
    }

    public void Show()
    {
        if (board.IsOpen) return;
        SetVisible(true);
    }

    private void Select(string g)
    {
        SetVisible(false);
        board.Open(g);
    }

    private void SetVisible(bool on)
    {
        shown = on;
        group.alpha = on ? 1f : 0f;
        group.interactable = on;
        group.blocksRaycasts = on;
    }
}