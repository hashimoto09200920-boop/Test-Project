using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 「無限化の石」の所持数表示UI（03_AreaSelect用）。
/// GoldHUDと同じ構成（Icon + TextMeshProUGUI）。0個でも常に表示する。
/// </summary>
public class InfiniteStoneHUD : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image stoneIcon;
    [SerializeField] private TextMeshProUGUI stoneText;

    private void Start()
    {
        if (InfiniteStoneManager.Instance == null)
        {
            Debug.LogWarning("[InfiniteStoneHUD] InfiniteStoneManager.Instance is null. Count display will not update.");
            return;
        }

        InfiniteStoneManager.Instance.OnCountChanged += UpdateText;
        UpdateText(InfiniteStoneManager.Instance.Count);
    }

    private void OnDestroy()
    {
        if (InfiniteStoneManager.Instance != null)
            InfiniteStoneManager.Instance.OnCountChanged -= UpdateText;
    }

    private void UpdateText(int amount)
    {
        GameNumberFont.Apply(stoneText); // ★数字用フォント（Bungee）に切り替え（初回だけ。数字の高さは従来と同じに補正）
        if (stoneText != null)
            stoneText.text = amount.ToString();
    }
}
