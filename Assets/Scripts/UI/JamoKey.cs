using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class JamoKey : MonoBehaviour
{
    [SerializeField] private TMP_Text glyphText;   // 가운데 큰 글자
    [SerializeField] private TMP_Text countText;   // 오른쪽 아래 수량

    private Button button;

    public void Setup(string glyph, Action<string> onClick)
    {
        button = GetComponent<Button>();
        glyphText.text = glyph;
        button.onClick.AddListener(() => onClick(glyph));
    }

    public void SetCount(int n)
    {
        countText.text = n.ToString();
        bool has = n > 0;
        button.interactable = has;           // 0개면 눌리지 않음
        countText.alpha = has ? 1f : 0.35f;
    }
}