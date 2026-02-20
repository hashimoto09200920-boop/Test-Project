using UnityEngine;

public class PaddleCostManager : MonoBehaviour
{
    [Header("Left Line Cost (Length-based)")]
    [SerializeField] private float leftMaxCost = 20f;
    [SerializeField] private float leftRecoverPerSecond = 4f;
    [SerializeField] private float leftMinCostToDraw = 0.01f;

    [Header("Right Red Line Cost (Length-based)")]
    [SerializeField] private float redMaxCost = 6f;
    [SerializeField] private float redRecoverPerSecond = 2f;
    [SerializeField] private float redMinCostToDraw = 0.01f;

    [Header("Red Line Penalty")]
    [Tooltip("赤線使い切り後の回復停止時間（秒）")]
    [SerializeField] private float redPenaltyDelay = 3f;

    [Tooltip("ペナルティ中ループSE")]
    [SerializeField] private AudioClip redPenaltyLoopClip;

    [Tooltip("ペナルティ終了・回復開始SE")]
    [SerializeField] private AudioClip redRecoveryStartClip;

    [Tooltip("ループSE音量")]
    [SerializeField, Range(0f, 1f)] private float redLoopSeVolume = 0.5f;

    [Tooltip("ループSE専用AudioSource（未設定時は自動生成）")]
    [SerializeField] private AudioSource redLoopAudioSource;

    [Header("Recover Pause While Holding")]
    [SerializeField] private bool pauseLeftRecoverWhileDrawingLeft = false;  // 基本OFF推奨（白線は描いてない時に回復）
    [SerializeField] private bool pauseRedRecoverWhileDrawingRed = true;     // 赤線は「描いてる間は回復しない」推奨

    [Header("Left Line Penalty")]
    [Tooltip("白線使い切り後の回復停止時間（秒）")]
    [SerializeField] private float leftPenaltyDelay = 3f;

    [Tooltip("ペナルティ中ループSE")]
    [SerializeField] private AudioClip leftPenaltyLoopClip;

    [Tooltip("ペナルティ終了・回復開始SE")]
    [SerializeField] private AudioClip leftRecoveryStartClip;

    [Tooltip("ループSE音量")]
    [SerializeField, Range(0f, 1f)] private float leftLoopSeVolume = 0.5f;

    [Tooltip("ループSE専用AudioSource（未設定時は自動生成）")]
    [SerializeField] private AudioSource leftLoopAudioSource;

    [Header("Debug")]
    [SerializeField] private bool showLog = false;

    // ---- public props ----
    public float LeftMaxCost => leftMaxCost;
    public float LeftCurrentCost { get; private set; }
    public float LeftRecoverPerSecond => leftRecoverPerSecond;
    public bool IsLeftInPenaltyDelay => leftIsDepleted && leftPenaltyTimer > 0f;

    public float RedMaxCost => redMaxCost;
    public float RedCurrentCost { get; private set; }
    public float RedRecoverPerSecond => redRecoverPerSecond;
    public bool IsRedInPenaltyDelay => redIsDepleted && redPenaltyTimer > 0f;

    // PaddleDrawer から「いま描画中か」を渡してもらう
    private bool isDrawingLeft;
    private bool isDrawingRed;

    // 白線ペナルティ状態
    private bool leftIsDepleted = false;
    private float leftPenaltyTimer = 0f;
    // 赤線ペナルティ状態
    private bool redIsDepleted = false;
    private float redPenaltyTimer = 0f;
    private bool wasSkillSelectionShowing = false;

    private void Awake()
    {
        leftMaxCost = Mathf.Max(0f, leftMaxCost);
        redMaxCost = Mathf.Max(0f, redMaxCost);

        LeftCurrentCost = leftMaxCost;
        RedCurrentCost = redMaxCost;

        // ループSE用AudioSourceを自動生成（白線）
        if (leftLoopAudioSource == null)
        {
            leftLoopAudioSource = gameObject.AddComponent<AudioSource>();
            leftLoopAudioSource.loop = true;
            leftLoopAudioSource.playOnAwake = false;
        }

        // ループSE用AudioSourceを自動生成（赤線）
        if (redLoopAudioSource == null)
        {
            redLoopAudioSource = gameObject.AddComponent<AudioSource>();
            redLoopAudioSource.loop = true;
            redLoopAudioSource.playOnAwake = false;
        }
    }

    private void Start()
    {
        // PauseManagerのポーズ/再開イベントを購読
        if (PauseManager.Instance != null)
        {
            PauseManager.Instance.OnPauseStarted += OnGamePaused;
            PauseManager.Instance.OnPauseEnded += OnGameResumed;
        }
    }

    private void OnDestroy()
    {
        if (PauseManager.Instance != null)
        {
            PauseManager.Instance.OnPauseStarted -= OnGamePaused;
            PauseManager.Instance.OnPauseEnded -= OnGameResumed;
        }
    }

    private void OnGamePaused()
    {
        if (leftLoopAudioSource != null && leftLoopAudioSource.isPlaying)
            leftLoopAudioSource.Pause();
        if (redLoopAudioSource != null && redLoopAudioSource.isPlaying)
            redLoopAudioSource.Pause();
    }

    private void OnGameResumed()
    {
        if (Game.UI.SkillSelectionUI.IsShowing) return;
        if (leftIsDepleted && leftPenaltyTimer > 0f && leftLoopAudioSource != null && leftLoopAudioSource.clip != null)
            leftLoopAudioSource.UnPause();
        if (redIsDepleted && redPenaltyTimer > 0f && redLoopAudioSource != null && redLoopAudioSource.clip != null)
            redLoopAudioSource.UnPause();
    }

    private void Update()
    {
        // スキル選択画面の表示/非表示切り替えを検出してループSEを制御
        bool isSkillSelectionShowing = Game.UI.SkillSelectionUI.IsShowing;
        if (isSkillSelectionShowing != wasSkillSelectionShowing)
        {
            wasSkillSelectionShowing = isSkillSelectionShowing;
            if (isSkillSelectionShowing)
            {
                if (leftLoopAudioSource != null && leftLoopAudioSource.isPlaying)
                    leftLoopAudioSource.Pause();
                if (redLoopAudioSource != null && redLoopAudioSource.isPlaying)
                    redLoopAudioSource.Pause();
            }
            else
            {
                if (leftIsDepleted && leftPenaltyTimer > 0f && leftLoopAudioSource != null && leftLoopAudioSource.clip != null)
                    leftLoopAudioSource.UnPause();
                if (redIsDepleted && redPenaltyTimer > 0f && redLoopAudioSource != null && redLoopAudioSource.clip != null)
                    redLoopAudioSource.UnPause();
            }
        }

        // 回復は「描いていない時」が基本（赤線は pause をON推奨）
        bool canRecoverLeft = !pauseLeftRecoverWhileDrawingLeft || !isDrawingLeft;
        bool canRecoverRed = !pauseRedRecoverWhileDrawingRed || !isDrawingRed;

        if (canRecoverLeft) RecoverLeft(Time.deltaTime);
        if (canRecoverRed) RecoverRed(Time.deltaTime);
    }

    // PaddleDrawer から毎フレーム呼ぶ
    public void SetDrawingState(bool drawingLeft, bool drawingRed)
    {
        isDrawingLeft = drawingLeft;
        isDrawingRed = drawingRed;
    }

    // ---------- Left ----------
    private void RecoverLeft(float dt)
    {
        // ペナルティタイマー処理
        if (leftIsDepleted && leftPenaltyTimer > 0f)
        {
            leftPenaltyTimer -= dt;
            if (leftPenaltyTimer <= 0f)
            {
                leftPenaltyTimer = 0f;
                leftIsDepleted = false;
                StopLeftLoopSound();
                PlayLeftSound(leftRecoveryStartClip);
            }
            return; // ペナルティ中は回復しない
        }

        if (leftRecoverPerSecond <= 0f) return;

        LeftCurrentCost += leftRecoverPerSecond * dt;
        if (LeftCurrentCost > leftMaxCost) LeftCurrentCost = leftMaxCost;
    }

    public bool CanConsumeLeft(float length)
    {
        if (length <= 0f) return LeftCurrentCost >= leftMinCostToDraw;
        return LeftCurrentCost >= length && LeftCurrentCost >= leftMinCostToDraw;
    }

    public bool TryConsumeLeft(float length)
    {
        length = Mathf.Max(0f, length);

        if (!CanConsumeLeft(length)) return false;

        LeftCurrentCost -= length;
        if (LeftCurrentCost < 0f) LeftCurrentCost = 0f;

        // コストが描画不可能レベル（leftMinCostToDraw未満）になった時にペナルティ発動
        // ※CanConsumeLeftが先にfalseを返すため厳密な0には到達しないので< leftMinCostToDrawで判定
        if (LeftCurrentCost < leftMinCostToDraw && !leftIsDepleted)
        {
            leftIsDepleted = true;
            leftPenaltyTimer = leftPenaltyDelay;
            PlayLeftLoopSound(leftPenaltyLoopClip);
            if (showLog)
                Debug.Log($"[PaddleCost:Left] Depleted! Penalty delay: {leftPenaltyDelay}s");
        }

        if (showLog)
        {
            Debug.Log($"[PaddleCost:Left] -{length:0.00} => {LeftCurrentCost:0.00}/{leftMaxCost:0.00}");
        }

        return true;
    }

    // ---------- Red ----------
    private void RecoverRed(float dt)
    {
        // ペナルティタイマー処理
        if (redIsDepleted && redPenaltyTimer > 0f)
        {
            redPenaltyTimer -= dt;
            if (redPenaltyTimer <= 0f)
            {
                redPenaltyTimer = 0f;
                redIsDepleted = false;
                StopRedLoopSound();
                PlayRedSound(redRecoveryStartClip);
            }
            return; // ペナルティ中は回復しない
        }

        if (redRecoverPerSecond <= 0f) return;

        RedCurrentCost += redRecoverPerSecond * dt;
        if (RedCurrentCost > redMaxCost) RedCurrentCost = redMaxCost;
    }

    public bool CanConsumeRed(float length)
    {
        if (length <= 0f) return RedCurrentCost >= redMinCostToDraw;
        return RedCurrentCost >= length && RedCurrentCost >= redMinCostToDraw;
    }

    public bool TryConsumeRed(float length)
    {
        length = Mathf.Max(0f, length);

        if (!CanConsumeRed(length)) return false;

        RedCurrentCost -= length;
        if (RedCurrentCost < 0f) RedCurrentCost = 0f;

        // コストが描画不可能レベル（redMinCostToDraw未満）になった時にペナルティ発動
        // ※CanConsumeRedが先にfalseを返すため厳密な0には到達しないので< redMinCostToDrawで判定
        if (RedCurrentCost < redMinCostToDraw && !redIsDepleted)
        {
            redIsDepleted = true;
            redPenaltyTimer = redPenaltyDelay;
            PlayRedLoopSound(redPenaltyLoopClip);
            if (showLog)
                Debug.Log($"[PaddleCost:Red] Depleted! Penalty delay: {redPenaltyDelay}s");
        }

        if (showLog)
        {
            Debug.Log($"[PaddleCost:Red] -{length:0.00} => {RedCurrentCost:0.00}/{redMaxCost:0.00}");
        }

        return true;
    }

    // =========================================================
    // Skill System Setters
    // =========================================================

    /// <summary>
    /// 白線の最大値を設定（スキルシステム用）
    /// </summary>
    public void SetLeftMaxCost(float value)
    {
        leftMaxCost = Mathf.Max(0f, value);
        // スキル取得時は満タン状態から開始、ペナルティもリセット
        LeftCurrentCost = leftMaxCost;
        leftIsDepleted = false;
        leftPenaltyTimer = 0f;
        StopLeftLoopSound();
    }

    /// <summary>
    /// 赤線の最大値を設定（スキルシステム用）
    /// </summary>
    public void SetRedMaxCost(float value)
    {
        redMaxCost = Mathf.Max(0f, value);
        // スキル取得時は満タン状態から開始、ペナルティもリセット
        RedCurrentCost = redMaxCost;
        redIsDepleted = false;
        redPenaltyTimer = 0f;
        StopRedLoopSound();
    }

    /// <summary>
    /// 白線の回復量を設定（スキルシステム用）
    /// </summary>
    public void SetLeftRecoverPerSecond(float value)
    {
        leftRecoverPerSecond = Mathf.Max(0f, value);
    }

    /// <summary>
    /// 赤線の回復量を設定（スキルシステム用）
    /// </summary>
    public void SetRedRecoverPerSecond(float value)
    {
        redRecoverPerSecond = Mathf.Max(0f, value);
    }

    // =========================================================
    // Audio Helpers
    // =========================================================

    private void PlayLeftLoopSound(AudioClip clip)
    {
        if (leftLoopAudioSource == null) return;
        StopLeftLoopSound();
        if (clip == null) return;
        leftLoopAudioSource.clip = clip;
        leftLoopAudioSource.volume = leftLoopSeVolume;
        leftLoopAudioSource.Play();
    }

    private void StopLeftLoopSound()
    {
        if (leftLoopAudioSource != null && leftLoopAudioSource.isPlaying)
            leftLoopAudioSource.Stop();
    }

    private void PlayLeftSound(AudioClip clip)
    {
        if (clip == null || leftLoopAudioSource == null) return;
        leftLoopAudioSource.PlayOneShot(clip, leftLoopSeVolume);
    }

    private void PlayRedLoopSound(AudioClip clip)
    {
        if (redLoopAudioSource == null) return;
        StopRedLoopSound();
        if (clip == null) return;
        redLoopAudioSource.clip = clip;
        redLoopAudioSource.volume = redLoopSeVolume;
        redLoopAudioSource.Play();
    }

    private void StopRedLoopSound()
    {
        if (redLoopAudioSource != null && redLoopAudioSource.isPlaying)
            redLoopAudioSource.Stop();
    }

    private void PlayRedSound(AudioClip clip)
    {
        if (clip == null || redLoopAudioSource == null) return;
        redLoopAudioSource.PlayOneShot(clip, redLoopSeVolume);
    }
}
