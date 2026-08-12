using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Game.Gems;

/// <summary>
/// ゲーム全体の状態を管理するマネージャー
/// ゲームオーバー処理などを担当
/// </summary>
public class GameManager : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("ゲームオーバー時に表示するリザルトUI（旧・未使用）")]
    [SerializeField] private GameResultUI gameResultUI;
    [Tooltip("ゲームオーバー時に表示するResultスクリーン")]
    [SerializeField] private ResultScreenUI resultScreenUI;

    [Header("Game Over Settings")]
    [Tooltip("ゲームオーバー後、リザルト画面を表示するまでの待機時間（秒）")]
    [SerializeField] private float gameOverDelay = 1.5f;

    private static GameManager instance;
    private bool isGameOver = false;
    private bool isGameOverInProgress = false;

    public static GameManager Instance => instance;
    public bool IsGameOver => isGameOver;

    private void Awake()
    {
        // シングルトン設定
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        SessionStats.Reset();

        // ★チュートリアル起動時はオープニング/エリアセレクトのBGMを止めずに鳴らし続ける
        // （GameSession.StartInTutorialModeは前シーンで既にセット済みのため、Awake実行順に依存せず安全に判定できる）
        if (!GameSession.StartInTutorialMode)
        {
            // AreaSelectから直接Gameシーンに入った場合、AreaSelectのBGMを停止
            CleanupPreviousSceneBGM();

            // ★ジェム使用回数システム：チュートリアルでは装備ジェム効果自体を適用しないため消費もしない。
            // 通常プレイはクリア/ゲームオーバー/リタイアいずれのルートで終了しても一律1回消費するため、
            // 分岐の多い「終了時」ではなく、確実に1回だけ通る「開始時」で減算する。
            Game.Gems.GemManager.Instance?.DecrementEquippedGemUses();
        }
    }

    private void Start()
    {
        SessionStats.StartTimer();

        // ★チュートリアル中は装備ジェム/ドリンク効果を反映しない（誰でも同じ条件で練習できるようにする）
        if (!GameSession.IsInTutorial)
        {
            // 装備中ジェムのスキルを SkillManager に適用（SkillManager.Awake() 完了後に実行）
            GemManager.Instance?.ApplyEquippedGems();
            // ドリンクブーストを SkillManager に適用
            GemManager.Instance?.ApplyDrinkBoosts();
        }
        // ジェム/ドリンクブースト適用後にHPを満タンに設定（最大値が確定してから全回復）
        Game.Skills.SkillManager.Instance?.RestoreHPToFull();
    }

    /// <summary>
    /// 前のシーンのBGMをクリーンアップ
    /// </summary>
    private void CleanupPreviousSceneBGM()
    {
        // AreaSelect の永続BGMを削除
        GameObject areaSelectBGM = GameObject.Find("AreaSelectBGM_Persistent");
        if (areaSelectBGM != null)
        {
            Debug.Log("[GameManager] Destroying AreaSelect persistent BGM");
            Destroy(areaSelectBGM);
        }

        // TitleBGMManager も削除（Titleから来た場合）
        GameObject titleBGM = GameObject.Find("TitleBGMManager");
        if (titleBGM != null)
        {
            Debug.Log("[GameManager] Destroying TitleBGMManager");
            Destroy(titleBGM);
        }
    }

    /// <summary>
    /// ゲームオーバーシーケンス開始（タイマー停止用）
    /// </summary>
    public void StartGameOverSequence()
    {
        isGameOverInProgress = true;
    }

    /// <summary>
    /// ゲームオーバーシーケンス解除（タイマー再開用）
    /// </summary>
    public void ClearGameOverSequence()
    {
        isGameOverInProgress = false;
    }

    /// <summary>
    /// ゲームオーバー処理を実行
    /// </summary>
    public void TriggerGameOver()
    {
        if (isGameOver)
        {
            return;
        }

        isGameOver = true;
        isGameOverInProgress = true;

        // ゲームオーバー処理
        Invoke(nameof(ShowGameOverResult), gameOverDelay);
    }

    /// <summary>
    /// ゲームオーバー進行中かどうかを取得
    /// </summary>
    public bool IsGameOverInProgress()
    {
        return isGameOverInProgress;
    }

    private void ShowGameOverResult()
    {
        if (resultScreenUI != null)
        {
            resultScreenUI.Show(() =>
            {
                GameSession.Reset();
                StartCoroutine(FadeOutAndReturnToAreaSelect());
            }, isVictory: false);
        }
        else
        {
            Debug.LogError("[GameManager] ResultScreenUI is not assigned in Inspector!");
        }
    }

    /// <summary>
    /// AreaSelectManager.FadeInOnStart()と対になるよう、黒画面へのフェードアウトを挟んでからエリアセレクトへ戻る
    /// （TitleMenu.FadeOutAndLoadScene()等と同じ構成）
    /// </summary>
    private IEnumerator FadeOutAndReturnToAreaSelect()
    {
        GameObject fadeObj = new GameObject("GameOverFadeOut");
        Canvas fadeCanvas = fadeObj.AddComponent<Canvas>();
        fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        fadeCanvas.sortingOrder = 9999;

        UnityEngine.UI.CanvasScaler scaler = fadeObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        GameObject imageObj = new GameObject("FadeImage");
        imageObj.transform.SetParent(fadeObj.transform, false);

        UnityEngine.UI.Image fadeImage = imageObj.AddComponent<UnityEngine.UI.Image>();
        fadeImage.color = new Color(0f, 0f, 0f, 0f);

        RectTransform rectTransform = imageObj.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.sizeDelta = Vector2.zero;

        float duration = 0.5f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            fadeImage.color = new Color(0f, 0f, 0f, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        SceneManager.LoadScene("03_AreaSelect");
    }
}
