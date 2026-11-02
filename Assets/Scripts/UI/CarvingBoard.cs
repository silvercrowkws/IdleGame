using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class CarvingBoard : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [SerializeField] private Texture2D surfaceTex;       // 나무 이미지
    [SerializeField] private RawImage carvedImage;       // 파낸 자국을 그릴 층
    [SerializeField] private int brushRadius = 12;       // 조각칼 굵기(이미지 픽셀)
    [SerializeField] private float maxStrokeLength = 150f;
    private float strokeUsed;

    [Header("조각 느낌")]
    [SerializeField, Range(0.01f, 0.3f)] private float carveStrength = 0.06f;
    [SerializeField, Range(0f, 1f)] private float roughness = 0.6f;

    [Header("나무 조각")]
    [SerializeField] private WoodChip chipPrefab;
    [SerializeField] private int maxChips = 150;
    [SerializeField] private float chipsPerPixel = 0.15f;

    [Header("글자 가이드 / 판정")]
    [SerializeField] private GlyphGuide glyphGuide;
    [SerializeField] private string[] glyphs = { "ㄱ", "ㄴ", "ㅏ", "ㅓ" };   // 테스트용 글자 목록
    [SerializeField, Range(0.3f, 1f)] private float requiredCoverage = 0.8f;   // 이만큼 이상 파야 성공
    [SerializeField, Range(0.05f, 1f)] private float maxOvershoot = 0.3f;      // 벗어남이 이 값 미만이어야 성공

    public event System.Action<string, float, string> OnCarved;   // (글자, 정확도, 등급) 완성 신호

    private int glyphIndex;
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

    private int targetCount, hit, overshoot;
    private readonly List<WoodChip> chips = new List<WoodChip>();
    private float chipAcc;

    // 판정값
    public float Coverage => targetCount == 0 ? 0f : (float)hit / targetCount;
    public float Overshoot => targetCount == 0 ? 0f : (float)overshoot / targetCount;
    public float Accuracy => Mathf.Clamp01(Coverage - 0.5f * Overshoot);

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
    }

    private void Start()
    {
        if (glyphGuide != null) Begin(glyphGuide.Show(glyphs[0]));
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

        foreach (var c in chips) if (c != null) Destroy(c.gameObject);
        chips.Clear();

        UpdateResultText();
    }

    // ---------- 입력 ----------
    public void OnPointerDown(PointerEventData e)
    {
        if (finished) return;       // 완성 뒤 더 파기 금지
        strokeUsed = 0f;
        hasLast = false;
        if (ToPixel(e, out Vector2Int p))
        {
            CarveLine(p, p);
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
        float dist = Vector2Int.Distance(a, b);
        int steps = Mathf.Max(1, Mathf.CeilToInt(dist / (brushRadius * 0.5f)));
        for (int i = 0; i <= steps; i++)
        {
            Vector2 p = Vector2.Lerp(a, b, (float)i / steps);
            Stamp(Mathf.RoundToInt(p.x), Mathf.RoundToInt(p.y));
        }
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
        if (chipPrefab == null) return;

        chipAcc += dist * chipsPerPixel;
        while (chipAcc >= 1f)
        {
            chipAcc -= 1f;

            WoodChip c;
            if (chips.Count >= maxChips)
            {
                c = chips[0];
                chips.RemoveAt(0);
            }
            else
            {
                c = Instantiate(chipPrefab, transform);
            }
            chips.Add(c);

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

        string glyph = glyphs[glyphIndex];
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
        if (success) glyphIndex = (glyphIndex + 1) % glyphs.Length;   // 실패하면 같은 글자를 다시
        Begin(glyphGuide.Show(glyphs[glyphIndex]));
    }

    private void LateUpdate()
    {
        if (!dirty) return;
        result.Apply();
        UpdateResultText();
        dirty = false;
    }
}