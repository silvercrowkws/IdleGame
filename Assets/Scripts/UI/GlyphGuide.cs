using TMPro;
using UnityEngine;

public class GlyphGuide : MonoBehaviour
{
    [SerializeField] private TextMeshPro glyphText;    // GlyphText
    [SerializeField] private Camera glyphCamera;       // GlyphCamera
    [SerializeField] private RenderTexture rt;         // GlyphRT
    [SerializeField, Range(0.3f, 0.95f)] private float fill = 0.7f;   // 글자가 화면을 채우는 비율

    private Texture2D readTex;

    public Texture2D Show(string ch)
    {
        // 1) 글자를 바꾸고 원래 크기로 되돌려서 실제 크기를 잰다
        glyphText.transform.localScale = Vector3.one;
        glyphText.text = ch;
        glyphText.ForceMeshUpdate();

        Bounds b = glyphText.textBounds;
        float maxSide = Mathf.Max(b.size.x, b.size.y);

        // 2) 긴 쪽이 화면의 fill 비율이 되도록 크기를 맞춘다
        float viewSize = glyphCamera.orthographicSize * 2f;
        float scale = maxSide > 0.0001f ? viewSize * fill / maxSide : 1f;
        glyphText.transform.localScale = Vector3.one * scale;

        // 3) 글자의 한가운데가 카메라 한가운데에 오도록 옮긴다
        Vector3 cam = glyphCamera.transform.position;
        glyphText.transform.position = new Vector3(
            cam.x - b.center.x * scale,
            cam.y - b.center.y * scale,
            0f);

        // 4) 찍어서 읽기
        glyphCamera.Render();

        if (readTex == null || readTex.width != rt.width || readTex.height != rt.height)
            readTex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        readTex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        readTex.Apply();
        RenderTexture.active = prev;

        return readTex;
    }
}