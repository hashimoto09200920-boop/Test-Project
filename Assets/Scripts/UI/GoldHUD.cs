using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ゴールド表示UI。
/// 05_Game    : showSessionGold = true  → SessionGold を表示
/// 03_AreaSelect: showSessionGold = false → PersistentGold を表示
/// </summary>
public class GoldHUD : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image goldIcon;
    [SerializeField] private TextMeshProUGUI goldText;

    [Header("Settings")]
    [Tooltip("true = セッションゴールド（05_Game用）\nfalse = 永続ゴールド（03_AreaSelect用）")]
    [SerializeField] private bool showSessionGold = true;

    private void Start()
    {
        if (GoldManager.Instance == null)
        {
            Debug.LogWarning("[GoldHUD] GoldManager.Instance is null. Gold display will not update.");
            return;
        }

        GoldManager.Instance.OnSessionGoldChanged += OnSessionGoldChanged;
        GoldManager.Instance.OnPersistentGoldChanged += OnPersistentGoldChanged;

        // 初期表示
        int initial = showSessionGold ? GoldManager.Instance.SessionGold : GoldManager.Instance.PersistentGold;
        UpdateText(initial);
    }

    private void OnDestroy()
    {
        if (GoldManager.Instance != null)
        {
            GoldManager.Instance.OnSessionGoldChanged -= OnSessionGoldChanged;
            GoldManager.Instance.OnPersistentGoldChanged -= OnPersistentGoldChanged;
        }
    }

    private void OnSessionGoldChanged(int amount)
    {
        if (showSessionGold) UpdateText(amount);
    }

    private void OnPersistentGoldChanged(int amount)
    {
        if (!showSessionGold) UpdateText(amount);
    }

    private void UpdateText(int amount)
    {
        if (goldText != null)
            goldText.text = amount.ToString();
    }
}
