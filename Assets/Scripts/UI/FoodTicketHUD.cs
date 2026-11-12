using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// お食事券の所持数の表示（2026/11/11）。画面右上の虹の雫（InfiniteStoneHUD）の下に、同じ枠・大きさで置く。
/// 0枚でも常に表示する（虹の雫と同じ）。作成は Tools/画面共通/「右上にお食事券の所持数を作る」
/// （02_Gem・03_AreaSelect・04_Drink・07_Shop の InfiniteStoneHUD を写して作る）
/// </summary>
public class FoodTicketHUD : MonoBehaviour
{
    [SerializeField] private Image ticketIcon;
    [SerializeField] private TextMeshProUGUI ticketText;

    private void OnEnable()
    {
        FoodTickets.OnChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        FoodTickets.OnChanged -= Refresh;
    }

    private void Refresh()
    {
        GameNumberFont.Apply(ticketText); // ★虹の雫・ゴールドと同じ数字用フォント（Bungee）
        if (ticketText != null) ticketText.text = FoodTickets.Owned.ToString();
    }

#if UNITY_EDITOR
    public void EditorWire(Image icon, TextMeshProUGUI text)
    {
        ticketIcon = icon;
        ticketText = text;
    }
#endif
}
