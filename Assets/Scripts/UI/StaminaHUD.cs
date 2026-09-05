using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// スタミナ表示UI（03_AreaSelect用）。GoldHUD/InfiniteStoneHUDと同じ構成(Icon + Text)に加えて、
/// 次の1回復までの残り時間カウントダウンを表示する。広告解除(Unlimited)中は「∞」表示にする。
/// </summary>
public class StaminaHUD : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image staminaIcon;
    [SerializeField] private TextMeshProUGUI staminaCountText;
    [Tooltip("「次の回復まで残り◯分」を表示するテキスト。上限中・Unlimited中は空表示にする")]
    [SerializeField] private TextMeshProUGUI countdownText;

    private void Start()
    {
        if (StaminaManager.Instance == null)
        {
            Debug.LogWarning("[StaminaHUD] StaminaManager.Instance is null. Display will not update.");
            return;
        }

        StaminaManager.Instance.OnStaminaChanged += UpdateDisplay;
        UpdateDisplay();
    }

    private void OnDestroy()
    {
        if (StaminaManager.Instance != null)
            StaminaManager.Instance.OnStaminaChanged -= UpdateDisplay;
    }

    private void Update()
    {
        // ★カウントダウンは毎フレーム更新する必要があるため、イベント駆動のUpdateDisplayとは別に呼ぶ
        UpdateCountdownOnly();
    }

    private void UpdateDisplay()
    {
        if (StaminaManager.Instance == null) return;

        if (staminaCountText != null)
        {
            staminaCountText.text = StaminaManager.Instance.IsUnlimited
                ? "∞"
                : $"{StaminaManager.Instance.Count}/{StaminaManager.MaxStamina}";
        }

        UpdateCountdownOnly();
    }

    private void UpdateCountdownOnly()
    {
        if (countdownText == null || StaminaManager.Instance == null) return;

        float seconds = StaminaManager.Instance.GetSecondsUntilNextStamina();
        if (seconds <= 0f)
        {
            countdownText.text = "";
            return;
        }

        int totalSeconds = Mathf.CeilToInt(seconds);
        int minutes = totalSeconds / 60;
        int secs = totalSeconds % 60;
        string timeStr = $"{minutes:00}:{secs:00}";
        string template = Game.Localization.LocalizationManager.Instance != null
            ? Game.Localization.LocalizationManager.Instance.Get("stamina.countdown")
            : "あと{0}";
        countdownText.text = string.Format(template, timeStr);
    }
}
