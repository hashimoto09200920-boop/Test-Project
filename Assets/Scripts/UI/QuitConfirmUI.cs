using UnityEngine;
using UnityEngine.UI;
using Game.UI; // ★ 追加：SceneController を参照するため

public class QuitConfirmUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] GameObject quitPanel;     // 確認ダイアログのルート
    [SerializeField] GameObject modalBlocker;  // 画面全体のブロッカー（任意）
    [SerializeField] SceneController sceneController; // ★ Game.UI.SceneController

    [Header("Optional Buttons (自動割当可)")]
    [SerializeField] Button quitYesButton;
    [SerializeField] Button quitNoButton;

    void Awake()
    {
        // パネルは既定で非表示
        if (quitPanel      != null) quitPanel.SetActive(false);
        if (modalBlocker   != null) modalBlocker.SetActive(false);

        // 参照の自動取得
        if (sceneController == null) sceneController = GetComponent<SceneController>();
        if (quitYesButton == null)
        {
            var t = quitPanel != null ? quitPanel.transform : null;
            quitYesButton = t ? t.Find("YesButton")?.GetComponent<Button>() : null;
        }
        if (quitNoButton == null)
        {
            var t = quitPanel != null ? quitPanel.transform : null;
            quitNoButton = t ? t.Find("NoButton")?.GetComponent<Button>() : null;
        }

        // ボタンリスナー（あれば）
        if (quitYesButton != null) { quitYesButton.onClick.RemoveAllListeners(); quitYesButton.onClick.AddListener(OnClickQuitYes); }
        if (quitNoButton  != null) { quitNoButton .onClick.RemoveAllListeners(); quitNoButton .onClick.AddListener(OnClickQuitNo ); }
    }

    // Quit ボタンから呼ぶ
    public void OnClickQuit()
    {
        if (quitPanel    != null) quitPanel.SetActive(true);
        if (modalBlocker != null) modalBlocker.SetActive(true);
    }

    // 「はい」
    public void OnClickQuitYes()
    {
        if (sceneController != null)
        {
            sceneController.QuitGame();
        }
        else
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    // 「いいえ」
    public void OnClickQuitNo()
    {
        if (quitPanel    != null) quitPanel.SetActive(false);
        if (modalBlocker != null) modalBlocker.SetActive(false);
    }
}
