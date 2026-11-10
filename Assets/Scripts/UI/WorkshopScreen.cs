using UnityEngine;

[RequireComponent(typeof(CanvasGroup))]
public class WorkshopScreen : MonoBehaviour
{
    [SerializeField] private CanvasGroup[] normalUI;   // 공방 중에 숨길 기존 UI (TopUI, BottomUI)
    [SerializeField] private CarvingBoard board;
    [SerializeField] private GlyphPicker picker;
    [SerializeField] private BreathInput breath;

    private CanvasGroup group;

    private void Awake()
    {
        group = GetComponent<CanvasGroup>();
        Apply(group, false);   // 처음엔 숨김
    }

    // 공방 버튼이 부르는 함수
    public void Enter()
    {
        foreach (var g in normalUI) Apply(g, false);
        Apply(group, true);
        picker.Show();
        breath.StartListening();
    }

    // 나가기 버튼이 부르는 함수
    public void Exit()
    {
        if (board.IsOpen) board.Cancel();   // 조각 중이면 초기화하고 닫기
        picker.Hide();
        Apply(group, false);
        foreach (var g in normalUI) Apply(g, true);
        breath.StopListening();
    }

    private static void Apply(CanvasGroup g, bool on)
    {
        g.alpha = on ? 1f : 0f;
        g.interactable = on;
        g.blocksRaycasts = on;
    }
}