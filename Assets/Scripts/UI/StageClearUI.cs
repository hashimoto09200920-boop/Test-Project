using UnityEngine;
using TMPro;

/// <summary>
/// ステージクリア・全ステージクリアのメッセージを表示するUI
/// </summary>
public class StageClearUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("ステージクリアメッセージ表示用のTextMeshPro")]
    [SerializeField] private TextMeshProUGUI messageText;

    [Header("Display Settings")]
    [Tooltip("メッセージ表示時間（秒）")]
    [SerializeField] private float displayDuration = 2.5f;

    [Header("Messages")]
    [Tooltip("ステージクリア時のメッセージ")]
    [SerializeField] private string stageClearMessage = "Stage Clear!";

    [Tooltip("全ステージクリア時のメッセージ")]
    [SerializeField] private string allStagesClearMessage = "All Stages Clear!";

    private float displayTimer = 0f;
    private bool isDisplaying = false;

    private void Start()
    {
        // 初期状態では非表示
        if (messageText != null)
        {
            messageText.gameObject.SetActive(false);
        }
    }

    private void Update()
    {
        if (isDisplaying)
        {
            displayTimer -= Time.deltaTime;

            if (displayTimer <= 0f)
            {
                HideMessage();
            }
        }
    }

    /// <summary>
    /// ステージクリアメッセージを表示
    /// </summary>
    /// <param name="stageIndex">クリアしたステージ番号（1始まり）</param>
    public void ShowStageClear(int stageIndex)
    {
        if (messageText == null) return;

        messageText.text = $"{stageClearMessage}\nStage {stageIndex}";
        messageText.color = Color.yellow;
        ShowMessage();
    }

    /// <summary>
    /// 全ステージクリアメッセージを表示
    /// </summary>
    public void ShowAllStagesClear()
    {
        if (messageText == null) return;

        messageText.text = allStagesClearMessage;
        messageText.color = Color.cyan;
        ShowMessage();
    }

    /// <summary>
    /// メッセージを表示
    /// </summary>
    private void ShowMessage()
    {
        if (messageText == null) return;

        messageText.gameObject.SetActive(true);
        displayTimer = displayDuration;
        isDisplaying = true;
    }

    /// <summary>
    /// メッセージを非表示
    /// </summary>
    private void HideMessage()
    {
        if (messageText == null) return;

        messageText.gameObject.SetActive(false);
        isDisplaying = false;
    }
}
