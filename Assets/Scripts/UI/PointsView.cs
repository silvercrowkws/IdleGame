using TMPro;
using UnityEngine;

[RequireComponent(typeof(TMP_Text))]
public class PointsView : MonoBehaviour
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
        inventory.OnPointsChanged += Refresh;
        Refresh(inventory.Points);
    }

    private void OnDestroy()
    {
        if (inventory != null) inventory.OnPointsChanged -= Refresh;
    }

    private void Refresh(long points)
    {
        text.text = $"포인트 {points:N0}";
    }
}