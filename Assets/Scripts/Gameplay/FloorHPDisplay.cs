using UnityEngine;
using TMPro;

public class FloorHPDisplay : MonoBehaviour
{
    [Header("Debug Display")]
    [SerializeField] private bool showFloorHP = true;

    [Header("References")]
    [SerializeField] private TMP_Text floorHPText;
    [SerializeField] private FloorHealth floorHealth;

    private void Awake()
    {
        if (floorHealth == null)
        {
            floorHealth = FindFirstObjectByType<FloorHealth>();
        }

        UpdateDisplay();
    }

    private void Update()
    {
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (floorHPText == null) return;

        if (!showFloorHP)
        {
            floorHPText.enabled = false;
            return;
        }

        floorHPText.enabled = true;

        if (floorHealth == null)
        {
            floorHPText.text = "Floor: --/--";
            return;
        }

        floorHPText.text = $"Floor: {floorHealth.CurrentHP}/{floorHealth.MaxHP}";
    }
}
