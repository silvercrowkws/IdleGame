using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class DexWordPopup : MonoBehaviour
{
    [SerializeField] private TMP_Text wordText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private ScrollRect bodyScroll;

    private CanvasGroup group;
    private string current;
    private Coroutine waiting;

    public bool IsOpen { get; private set; }

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
        Apply(false);   // 처음엔 숨김
    }

    public void Show(string word)
    {
        current = word;
        wordText.text = word;
        Apply(true);
        Render();
    }

    // 닫기 버튼과 바깥 배경이 부르는 함수
    public void Hide()
    {
        if (waiting != null) { StopCoroutine(waiting); waiting = null; }
        Apply(false);
    }

    private void Render()
    {
        var dict = WordDictionary.Instance;

        if (!dict.IsLoaded)
        {
            dict.Ensure();
            bodyText.text = "사전을 불러오는 중이에요...";
            if (waiting == null) waiting = StartCoroutine(WaitAndRender());
        }
        else if (dict.TryFind(current, out List<WordInfo> meanings))
        {
            bodyText.text = Build(meanings);
        }
        else
        {
            bodyText.text = "사전에서 뜻을 찾지 못했어요.";
        }

        if (bodyScroll != null) bodyScroll.verticalNormalizedPosition = 1f;
    }

    private IEnumerator WaitAndRender()
    {
        float t = 0f;
        while (!WordDictionary.Instance.IsLoaded && t < 30f)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
        waiting = null;
        if (IsOpen) Render();
    }

    private string Build(List<WordInfo> list)
    {
        var sb = new StringBuilder();

        for (int i = 0; i < list.Count; i++)
        {
            WordInfo m = list[i];
            sb.AppendLine(list.Count > 1 ? $"[{i + 1}] {Safe(m.type)}" : Safe(m.type));
            sb.AppendLine($"전문 분야: {Safe(m.field)}");
            sb.AppendLine(Safe(m.definition));
            sb.AppendLine();
        }

        if (WordCollection.Instance.TryGet(current, out CollectedWord cw))
        {
            string date = new System.DateTime(cw.firstUtcTicks, System.DateTimeKind.Utc)
                .ToLocalTime().ToString("yyyy.MM.dd");
            sb.Append($"<size=80%><color=#7A6A55>처음 찾은 날 {date}</color></size>");
        }

        return sb.ToString();
    }

    // 뜻에 '<' 가 들어 있어도 서식 기호로 읽히지 않게 함
    private static string Safe(string s)
    {
        return string.IsNullOrEmpty(s) ? "" : s.Replace("<", "<noparse><</noparse>");
    }

    private void Apply(bool on)
    {
        IsOpen = on;
        group.alpha = on ? 1f : 0f;
        group.interactable = on;
        group.blocksRaycasts = on;
    }
}