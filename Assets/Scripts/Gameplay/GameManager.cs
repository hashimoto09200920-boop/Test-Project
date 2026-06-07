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

        // AreaSelectから直接Gameシーンに入った場合、AreaSelectのBGMを停止
        CleanupPreviousSceneBGM();
    }

    private void Start()
    {
        SessionStats.StartTimer();

        // 装備中ジェムのスキルを SkillManager に適用（SkillManager.Awake() 完了後に実行）
        GemManager.Instance?.ApplyEquippedGems();
        // ドリンクブーストを SkillManager に適用
        GemManager.Instance?.ApplyDrinkBoosts();
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
                SceneManager.LoadScene("03_AreaSelect");
            });
        }
        else
        {
            Debug.LogError("[GameManager] ResultScreenUI is not assigned in Inspector!");
        }
    }
}
