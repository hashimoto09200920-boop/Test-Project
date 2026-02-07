using UnityEngine;
using TMPro;

public class PaddleCostUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PaddleCostManager costManager;
    [SerializeField] private StrokeManager strokeManager;
    [SerializeField] private TextMeshProUGUI text;

    [Header("Format")]
    [SerializeField] private bool showPercent = true;

    private void Awake()
    {
        if (text == null) text = GetComponent<TextMeshProUGUI>();
        if (text == null) Debug.LogError("[PaddleCostUI] TextMeshProUGUI not found.");
        if (costManager == null) Debug.LogWarning("[PaddleCostUI] CostManager is not assigned.");
        if (strokeManager == null) Debug.LogWarning("[PaddleCostUI] StrokeManager is not assigned.");
    }

    private void Update()
    {
        if (costManager == null || text == null) return;

        // Left
        float lcur = costManager.LeftCurrentCost;
        float lmax = costManager.LeftMaxCost;

        string leftText;
        if (lmax <= 0.0001f) leftText = "L:-";
        else if (showPercent)
        {
            float p = (lcur / lmax) * 100f;
            leftText = $"L:{lcur:0.0}/{lmax:0.0}({p:0}%)";
        }
        else
        {
            leftText = $"L:{lcur:0.0}/{lmax:0.0}";
        }

        // Right (Red) : Numeric
        float rcur = costManager.RedCurrentCost;
        float rmax = costManager.RedMaxCost;

        string rightText;
        if (rmax <= 0.0001f) rightText = "R:-";
        else if (showPercent)
        {
            float p = (rcur / rmax) * 100f;
            rightText = $"R:{rcur:0.0}/{rmax:0.0}({p:0}%)";
        }
        else
        {
            rightText = $"R:{rcur:0.0}/{rmax:0.0}";
        }

        // Stroke
        string strokeText = "S:-";
        if (strokeManager != null)
        {
            strokeText = $"S:{strokeManager.ActiveStrokesCount}/{strokeManager.MaxStrokes}";
        }

        text.text = $"{leftText}\n{rightText}\n{strokeText}";
    }
}
