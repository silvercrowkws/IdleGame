using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public class MyWordPopup : MonoBehaviour
{
    [SerializeField] private TMP_Text wordText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private ScrollRect bodyScroll;
    [SerializeField] private TMP_Text deleteLabel;

    private CanvasGroup group;
    private MyWord current;
    private bool armed;
    private Coroutine armRoutine;
    private string defaultDeleteLabel;

    public bool IsOpen { get; private set; }

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
        defaultDeleteLabel = deleteLabel.text;
        Apply(false);   // 처음엔 숨김
    }

    public void Show(MyWord word)
    {
        current = word;
        wordText.text = word.word;

        string date = new System.DateTime(word.createdUtcTicks, System.DateTimeKind.Utc)
            .ToLocalTime().ToString("yyyy.MM.dd");
        bodyText.text = $"{Safe(word.definition)}\n\n<size=80%><color=#7A6A55>만든 날 {date}</color></size>";

        Disarm();
        Apply(true);
        if (bodyScroll != null) bodyScroll.verticalNormalizedPosition = 1f;
    }

    // 닫기 버튼과 바깥 배경이 부르는 함수
    public void Hide()
    {
        Disarm();
        Apply(false);
    }

    // 삭제 버튼이 부르는 함수: 두 번 눌러야 지워짐
    public void Delete()
    {
        if (current == null) return;

        if (!armed)
        {
            armed = true;
            deleteLabel.text = "한 번 더 눌러 삭제";
            armRoutine = StartCoroutine(DisarmLater());
            return;
        }

        MyDictionary.Instance.Remove(current.word);
        Hide();
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
        deleteLabel.text = defaultDeleteLabel;
    }

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