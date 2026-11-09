using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class CarvingBoard : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField] public Texture2D surfaceTex;        // 나무 이미지
    [SerializeField] private RawImage carvedImage;       // 파낸 자국을 그릴 층
    [SerializeField] private int brushRadius = 12;       // 조각칼 굵기(이미지 픽셀)
    [SerializeField] private float maxStrokeLength = 150f;
    [SerializeField] private CarveSound carveSound;
    private float strokeUsed;

    [Header("조각 느낌")]
    [SerializeField, Range(0.01f, 0.6f)] private float carveStrength = 0.15f;
    private float stampCarry;   // 마지막 도장 이후 움직인 거리
    [SerializeField, Range(0f, 1f)] private float roughness = 0.6f;

    [Header("나무 조각")]    
    [SerializeField] private int maxChips = 150;
    [SerializeField] private float chipsPerPixel = 0.15f;

    [Header("글자 가이드 / 판정")]
    [SerializeField] private GlyphGuide glyphGuide;    
    [SerializeField, Range(0.3f, 1f)] private float requiredCoverage = 0.8f;   // 이만큼 이상 파야 성공
    [SerializeField, Range(0.05f, 1f)] private float maxOvershoot = 0.3f;      // 벗어남이 이 값 미만이어야 성공

    public event System.Action<string, float, string> OnCarved;   // (글자, 정확도, 등급) 완성 신호

    private string currentGlyph = "";
    private CanvasGroup group;

    public event System.Action OnClosed;   // Board가 닫힐 때 알리는 신호
    public bool IsOpen { get; private set; }

    private bool finished;
    [SerializeField] private TMP_Text resultText;

    private Texture2D result;
    private Color32[] surface, groove, clearPixels;
    private float[] depth;
    private float[] grain;
    private bool[] target;    // 가이드 글자 안쪽인 픽셀
    private bool[] counted;   // 이미 "팠다"고 센 픽셀
    private int w, h;
    private bool hasLast, dirty;
    private Vector2Int last;
    private Vector2 lastLocal;

    [SerializeField] private CanvasGroup controls;   // 뒤로, 결과, 완료 버튼 묶음

    private int targetCount, hit, overshoot;
    private readonly List<WoodChip> chips = new List<WoodChip>();
    private float chipAcc;

    // 판정값
    public float Coverage => targetCount == 0 ? 0f : (float)hit / targetCount;
    public float Overshoot => targetCount == 0 ? 0f : (float)overshoot / targetCount;
    public float Accuracy => Mathf.Clamp01(Coverage - 0.5f * Overshoot);


    [Header("후~ 불기")]
    [SerializeField] private BreathInput breath;
    [SerializeField] private Vector2 windDir = Vector2.up;   // 바람 방향 (마이크가 폰 아래쪽에 있어서 위로)
    [SerializeField] private float windAccel = 3000f;         // 최대 세기일 때의 힘


    private void Awake()
    {
        w = surfaceTex.width;
        h = surfaceTex.height;
        surface = surfaceTex.GetPixels32();

        depth = new float[w * h];
        grain = new float[w * h];
        groove = new Color32[w * h];
        clearPixels = new Color32[w * h];
        target = new bool[w * h];
        counted = new bool[w * h];

        for (int i = 0; i < surface.Length; i++)
        {
            grain[i] = Random.value;

            float v = Mathf.Lerp(1f, 0.7f + grain[i] * 0.6f, roughness);
            Color32 c = surface[i];
            groove[i] = new Color32(
                (byte)Mathf.Min(255f, c.r * 0.55f * v),
                (byte)Mathf.Min(255f, c.g * 0.50f * v),
                (byte)Mathf.Min(255f, c.b * 0.45f * v), 255);

            clearPixels[i] = groove[i];
            clearPixels[i].a = 0;
        }

        result = new Texture2D(w, h, TextureFormat.RGBA32, false);
        result.SetPixels32(clearPixels);
        result.Apply();

        carvedImage.texture = result;
        carvedImage.color = Color.white;

        group = GetComponent<CanvasGroup>();
        SetVisible(false);   // 처음엔 숨김
    }

    // 새 글자로 시작 (판을 깨끗이 비우고 판정 기준을 갱신)
    public void Begin(Texture2D mask)
    {
        finished = false;
        Color32[] m = mask.GetPixels32();
        int mw = mask.width, mh = mask.height;

        targetCount = 0;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int mx = x * mw / w;
                int my = y * mh / h;
                bool on = m[my * mw + mx].a > 100;
                target[y * w + x] = on;
                if (on) targetCount++;
            }
        }

        System.Array.Clear(depth, 0, depth.Length);
        System.Array.Clear(counted, 0, counted.Length);
        hit = overshoot = 0;
        strokeUsed = 0f;

        result.SetPixels32(clearPixels);
        result.Apply();

        ClearChips();

        UpdateResultText();
    }

    // ---------- 입력 ----------
    public void OnPointerDown(PointerEventData e)
    {
        if (finished) return;       // 완성 뒤 더 파기 금지
        strokeUsed = 0f;

        if (carveSound != null) carveSound.StartStroke();

        hasLast = false;
        if (ToPixel(e, out Vector2Int p))
        {
            Stamp(p.x, p.y);
            stampCarry = 0f;
            dirty = true;

            last = p;
            hasLast = true;
        }
    }

    public void OnDrag(PointerEventData e)
    {
        if (finished) return;       // 완성 뒤 더 파기 금지
        if (strokeUsed >= maxStrokeLength) return;
        if (!ToPixel(e, out Vector2Int p)) { hasLast = false; return; }

        Vector2Int from = hasLast ? last : p;
        float dist = Vector2Int.Distance(from, p);

        float remain = maxStrokeLength - strokeUsed;
        if (dist > remain)
        {
            Vector2 cut = Vector2.Lerp(from, p, remain / dist);
            p = new Vector2Int(Mathf.RoundToInt(cut.x), Mathf.RoundToInt(cut.y));
            dist = remain;
        }

        CarveLine(from, p);
        SpawnChips(lastLocal, dist);

        if (carveSound != null) carveSound.Feed(dist);

        strokeUsed += dist;
        last = p;
        hasLast = true;
    }

    public void OnPointerUp(PointerEventData e)
    {
        hasLast = false;
    }

    private bool ToPixel(PointerEventData e, out Vector2Int p)
    {
        RectTransform rt = (RectTransform)transform;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, e.position, e.pressEventCamera, out Vector2 local))
        {
            lastLocal = local;
            Rect r = rt.rect;
            float u = (local.x - r.x) / r.width;
            float v = (local.y - r.y) / r.height;
            p = new Vector2Int(Mathf.RoundToInt(u * (w - 1)), Mathf.RoundToInt(v * (h - 1)));
            return u >= 0f && u <= 1f && v >= 0f && v <= 1f;
        }
        p = default;
        return false;
    }

    // ---------- 조각 ----------
    private void CarveLine(Vector2Int a, Vector2Int b)
    {
        float spacing = Mathf.Max(1f, brushRadius * 0.5f);
        float dist = Vector2Int.Distance(a, b);

        // 지난번 도장 이후 남은 거리를 이어서, 일정 간격마다 찍는다
        float next = spacing - stampCarry;
        while (next <= dist)
        {
            Vector2 p = Vector2.Lerp(a, b, next / dist);
            Stamp(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y));
            next += spacing;
        }
        stampCarry = spacing - (next - dist);
        dirty = true;
    }

    private void Stamp(int cx, int cy)
    {
        int r = brushRadius;
        for (int y = -r; y <= r; y++)
        {
            int py = cy + y;
            if (py < 0 || py >= h) continue;
            for (int x = -r; x <= r; x++)
            {
                if (x * x + y * y > r * r) continue;
                int px = cx + x;
                if (px < 0 || px >= w) continue;

                int i = py * w + px;
                if (surface[i].a < 128) continue;   // 나무판 밖(투명한 곳)은 파지 않음
                float d = Mathf.Sqrt(x * x + y * y) / r;

                if (d > 1f - roughness * 0.3f * grain[i]) continue;

                float amount = carveStrength * (1f - d) * (0.5f + grain[i]);
                depth[i] = Mathf.Min(1f, depth[i] + amount);
                Refresh(px, py, i);

                // 어느 정도 이상 파였으면 "팠다"고 한 번만 세기
                if (!counted[i] && depth[i] >= 0.4f)
                {
                    counted[i] = true;
                    if (target[i]) hit++; else overshoot++;
                }
            }
        }
    }

    private void Refresh(int px, int py, int i)
    {
        float t = Mathf.SmoothStep(0f, 1f, depth[i] * 1.2f - grain[i] * roughness * 0.5f);
        Color32 c = groove[i];
        c.a = (byte)(t * 255f);
        result.SetPixel(px, py, c);
    }

    // ---------- 나무 조각 ----------
    private void SpawnChips(Vector2 localPos, float dist)
    {
        chipAcc += dist * chipsPerPixel;
        while (chipAcc >= 1f)
        {
            chipAcc -= 1f;

            // 한도를 넘으면 가장 오래된 조각을 풀로 돌려보낸다
            if (chips.Count >= maxChips)
            {
                chips[0].gameObject.SetActive(false);
                chips.RemoveAt(0);
            }

            WoodChip c = Factory.Instance.GetWoodChip();
            if (c == null) return;
            chips.Add(c);
            c.onGone = OnChipGone;

            Vector2 dir = Random.insideUnitCircle.normalized;
            c.Launch(localPos, dir * Random.Range(150f, 450f), Random.Range(-540f, 540f), Random.Range(16f, 34f));
        }
    }

    // ---------- 결과 표시 ----------
    private string GetGrade()
    {
        float acc = Accuracy;
        return acc >= 0.9f ? "S" : acc >= 0.75f ? "A" : acc >= 0.55f ? "B" : "C";
    }

    private void UpdateResultText()
    {
        if (resultText == null) return;
        resultText.text = $"판 곳 {Coverage * 100f:F0}%   벗어남 {Overshoot * 100f:F0}%   등급 {GetGrade()}";
    }

    // 완료 버튼이 부르는 함수
    public void Finish()
    {
        if (finished) return;

        // 80% 미만이면 판정하지 않고 더 파게 한다 (실패 아님)
        if (Coverage < requiredCoverage)
        {
            resultText.text = $"아직 덜 팠어요! ({Coverage * 100f:F0}% / {requiredCoverage * 100f:F0}% 필요)";
            return;
        }

        finished = true;

        string glyph = currentGlyph;
        string grade = GetGrade();

        bool success = Overshoot < maxOvershoot
                    && grade != "C";   // B 이상만 인정

        if (success)
        {
            resultText.text = $"'{glyph}' 완성!   등급 {grade}   (정확도 {Accuracy * 100f:F0}%)";
            OnCarved?.Invoke(glyph, Accuracy, grade);   // 나중에 인벤토리가 이 신호를 받음
        }
        else
        {
            resultText.text = $"'{glyph}' 실패...   (판 곳 {Coverage * 100f:F0}% / 벗어남 {Overshoot * 100f:F0}%)";
        }

        StartCoroutine(NextGlyph(success));
    }

    private System.Collections.IEnumerator NextGlyph(bool success)
    {
        yield return new WaitForSeconds(1.5f);
        if (success) Close();                           // 성공: 글자 선택 화면으로 돌아감
        else Begin(glyphGuide.Show(currentGlyph));      // 실패: 같은 글자를 다시
    }

    // 글자를 정해서 Board를 열기
    public void Open(string glyph)
    {
        StopAllCoroutines();
        currentGlyph = glyph;
        SetVisible(true);
        if (breath != null) breath.StartListening();

        Begin(glyphGuide.Show(glyph));   // 가이드 변경 + 판 비우기 + 일치율 0%로 초기화
    }

    // Board 닫기 (글자 선택 화면으로)
    public void Close()
    {
        StopAllCoroutines();
        SetVisible(false);
        if (breath != null) breath.StopListening();

        OnClosed?.Invoke();
    }

    private void SetVisible(bool on)
    {
        IsOpen = on;
        group.alpha = on ? 1f : 0f;
        group.interactable = on;
        group.blocksRaycasts = on;   // 숨겨진 동안 터치도 막음

        if (controls != null)
        {
            controls.alpha = on ? 1f : 0f;
            controls.interactable = on;
            controls.blocksRaycasts = on;
        }
    }

    // 하던 조각을 버리고 조용히 닫기 (글자 선택 화면은 띄우지 않음)
    public void Cancel()
    {
        StopAllCoroutines();

        System.Array.Clear(depth, 0, depth.Length);
        System.Array.Clear(counted, 0, counted.Length);
        hit = overshoot = 0;
        strokeUsed = 0f;
        finished = false;

        result.SetPixels32(clearPixels);
        result.Apply();

        ClearChips();

        SetVisible(false);
        if (breath != null) breath.StopListening();
    }

    // 모든 조각을 풀로 돌려보내기
    private void ClearChips()
    {
        foreach (var c in chips)
            if (c != null) c.gameObject.SetActive(false);   // 비활성화되면 풀의 레디큐로 자동 복귀
        chips.Clear();
    }

    private void LateUpdate()
    {
        if (!dirty) return;
        result.Apply();
        UpdateResultText();
        dirty = false;
    }

    // 조각이 날아가서 사라질 때 목록에서 빼기
    private void OnChipGone(WoodChip c)
    {
        chips.Remove(c);
    }

    private void Update()
    {
        if (!IsOpen || breath == null || chips.Count == 0) return;

        float power = breath.Power;
        if (power <= 0f) return;

        for (int i = chips.Count - 1; i >= 0; i--)
            chips[i].Blow(windDir, windAccel * power, Time.deltaTime);
    }
}