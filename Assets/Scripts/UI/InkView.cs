using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class InkView : MonoBehaviour
{
    private TMP_Text text;
    private JamoInventory inventory;

    private void Awake()
    {
        text = GetComponent<TMP_Text>();
    }

    private void Start()
    {
        inventory = JamoInventory.Instance;
        inventory.OnInkChanged += Refresh;
        Refresh(inventory.Ink);
    }

    private void OnDestroy()
    {
        if (inventory != null) inventory.OnInkChanged -= Refresh;
    }

    private void Refresh(long ink)
    {
        text.text = $"잉크 {ink:N0}";
    }
}