using UnityEngine;
using UnityEngine.UI;

public class WoodChip : RecycleObject
{
    [SerializeField] private float friction = 4f;           // 클수록 빨리 멈춤
    [SerializeField] private float fadeTime = 0.5f;         // 판 밖에서 완전히 사라지기까지 걸리는 시간
    [SerializeField] private bool onlyWhenBlown = false;    // true면 바람에 날린 조각만 판 밖에서 사라짐

    public System.Action<WoodChip> onGone;   // 날아가서 사라질 때 알림
    private bool blown;
    private float outsideTime;      // 판 밖에 머문 시간
    private RectTransform parentRt; // 부모(ChipPool, Board와 같은 크기)
    private float windResist;                // 조각마다 바람에 버티는 정도가 다름

    private RectTransform rt;
    private Image img;
    private Vector2 vel;
    private float spin;

    // ---------- 뾰족한 조각 모양 (코드로 만들어 둠) ----------
    private static Sprite[] shapes;

    // 0~1 사이 좌표로 그린 도형들
    private static readonly Vector2[][] polys =
    {
        // 가늘고 긴 바늘 조각
        new[] { new Vector2(0.00f, 0.36f), new Vector2(0.70f, 0.45f), new Vector2(1.00f, 0.50f),
                new Vector2(0.70f, 0.56f), new Vector2(0.00f, 0.64f) },

        // 삼각형 파편
        new[] { new Vector2(0.05f, 0.10f), new Vector2(1.00f, 0.35f), new Vector2(0.30f, 0.95f) },

        // 깨진 오각형 조각
        new[] { new Vector2(0.00f, 0.35f), new Vector2(0.50f, 0.00f), new Vector2(1.00f, 0.40f),
                new Vector2(0.70f, 1.00f), new Vector2(0.20f, 0.75f) },

        // 들쭉날쭉한 쪼개진 조각
        new[] { new Vector2(0.00f, 0.40f), new Vector2(0.30f, 0.28f), new Vector2(0.55f, 0.42f),
                new Vector2(1.00f, 0.50f), new Vector2(0.55f, 0.62f), new Vector2(0.35f, 0.74f),
                new Vector2(0.00f, 0.62f) },

        // 쐐기 모양
        new[] { new Vector2(0.00f, 0.00f), new Vector2(1.00f, 0.50f), new Vector2(0.15f, 0.55f) },
    };

    private static void BuildShapes()
    {
        if (shapes != null) return;

        const int N = 64;
        shapes = new Sprite[polys.Length];

        for (int s = 0; s < polys.Length; s++)
        {
            var px = new Color32[N * N];
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    // 2x2로 나눠서 가장자리를 부드럽게
                    int hit = 0;
                    for (int sy = 0; sy < 2; sy++)
                        for (int sx = 0; sx < 2; sx++)
                            if (Inside(polys[s], (x + 0.25f + 0.5f * sx) / N, (y + 0.25f + 0.5f * sy) / N)) hit++;

                    px[y * N + x] = new Color32(255, 255, 255, (byte)(hit * 255 / 4));
                }
            }

            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.SetPixels32(px);
            tex.Apply();
            shapes[s] = Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
        }
    }

    // 점이 도형 안에 있는지 검사
    private static bool Inside(Vector2[] poly, float x, float y)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if ((poly[i].y > y) != (poly[j].y > y) &&
                x < (poly[j].x - poly[i].x) * (y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                inside = !inside;
        }
        return inside;
    }

    // ---------- 동작 ----------
    private void Awake()
    {
        rt = (RectTransform)transform;
        parentRt = (RectTransform)rt.parent;
        img = GetComponent<Image>();
        BuildShapes();
    }

    public void Launch(Vector2 pos, Vector2 velocity, float spinSpeed, float size)
    {
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(size, size * Random.Range(0.5f, 0.9f));
        rt.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

        img.sprite = shapes[Random.Range(0, shapes.Length)];   // 랜덤 모양 고르기
        img.color = Color.Lerp(new Color(0.78f, 0.5f, 0.25f), new Color(0.5f, 0.3f, 0.14f), Random.value);

        vel = velocity;
        spin = spinSpeed;

        blown = false;
        outsideTime = 0f;
        windResist = Random.Range(0.6f, 1.4f);
    }

    // 나중에 바람(마이크)으로 밀 때 쓰는 함수
    public void Push(Vector2 impulse)
    {
        vel += impulse;
        spin += Random.Range(-360f, 360f);
    }

    // 바람을 맞음 (dir: 바람 방향, accel: 힘, dt: 프레임 시간)
    public void Blow(Vector2 dir, float accel, float dt)
    {
        // 방향을 조금씩 흩뜨려서 자연스럽게
        Vector2 d = Quaternion.Euler(0f, 0f, Random.Range(-25f, 25f)) * dir.normalized;

        vel += d * (accel / windResist) * dt;
        spin += Random.Range(-720f, 720f) * dt;
        blown = true;
    }

    private void Update()
    {
        float dt = Time.deltaTime;

        // 조각이 나무판(부모 영역) 밖에 있는지 확인
        bool outside = !parentRt.rect.Contains(rt.localPosition);
        if (onlyWhenBlown && !blown) outside = false;

        if (outside)
        {
            // 판 밖: 서서히 투명해지다가 사라짐
            outsideTime += dt;
            float a = 1f - Mathf.Clamp01(outsideTime / fadeTime);
            SetAlpha(a);

            if (a <= 0f)
            {
                onGone?.Invoke(this);          // 보드의 목록에서 빼도록 알림
                gameObject.SetActive(false);   // 풀로 복귀
                return;
            }
        }
        else if (outsideTime > 0f)
        {
            // 판 안으로 돌아오면 원래대로
            outsideTime = 0f;
            SetAlpha(1f);
        }

        if (vel.sqrMagnitude < 1f && Mathf.Abs(spin) < 1f) return;

        rt.anchoredPosition += vel * dt;
        rt.Rotate(0f, 0f, spin * dt);

        float slow = Mathf.Exp(-friction * dt);
        vel *= slow;
        spin *= slow;
    }

    private void SetAlpha(float a)
    {
        Color c = img.color;
        c.a = a;
        img.color = c;
    }
}