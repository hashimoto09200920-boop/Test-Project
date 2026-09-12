using UnityEngine;

/// <summary>
/// ポーズ（中断）機能のコアシステム
/// Time.timeScaleを制御し、ゲーム全体の一時停止を管理
/// </summary>
public class PauseManager : MonoBehaviour
{
    public static PauseManager Instance { get; private set; }

    [Header("Debug")]
    [SerializeField] private bool showDebugLog = false;

    [Header("Pause State")]
    [Tooltip("現在ポーズ中かどうか（読み取り専用、デバッグ用）")]
    [SerializeField] private bool isPaused = false;

    [Header("Audio")]
    [Tooltip("ポーズ開始時のSE")]
    [SerializeField] private AudioClip pauseStartClip;

    [Tooltip("ポーズ解除時のSE")]
    [SerializeField] private AudioClip pauseEndClip;

    [Tooltip("ボタンクリック時のSE")]
    [SerializeField] private AudioClip buttonClickClip;

    [Tooltip("確認ボタン（Yes）のSE")]
    [SerializeField] private AudioClip confirmClip;

    [Tooltip("キャンセルボタン（No）のSE")]
    [SerializeField] private AudioClip cancelClip;

    [Header("References")]
    [SerializeField] private AudioSource audioSource;

    // 保存されたタイムスケール（ポーズ解除時に復元）
    private float savedTimeScale = 1f;

    // カットイン等でポーズ入力を一時的に無効化するフラグ
    private bool isPauseBlocked = false;

    // Public properties
    public bool IsPaused => isPaused;
    public bool IsPauseBlocked => isPauseBlocked;

    /// <summary>
    /// ポーズ入力を一時的にブロック/解除する（カットイン演出中などに使用）
    /// </summary>
    public void SetPauseBlocked(bool blocked)
    {
        isPauseBlocked = blocked;
    }

    // Events
    public System.Action OnPauseStarted;
    public System.Action OnPauseEnded;

    private void Awake()
    {
        // ★Singleton setup：前のシーンのPauseManagerが何らかの理由で完全に破棄されずに残っていた場合、
        //   「先勝ち」で古い方を優先するとisPaused=trueのまま新しいシーンに引き継がれてしまい、
        //   線を引く操作が一切できなくなる重大な不具合になっていた（実機調査で確認済み：
        //   ポーズしたままAreaSelectへ戻り、別のArea/チュートリアルへ再入場すると、古いPauseManagerの
        //   Instanceが生きたまま残っていて新しいPauseManagerがAwakeで自滅し、古いisPaused=trueが
        //   そのまま使われ続けていた）。1シーンにつき1つのPauseManagerが正しい状態のため、
        //   常に「最新（今のシーンの）インスタンス」を優先し、古い方を明示的に破棄する。
        if (Instance != null && Instance != this)
        {
            Destroy(Instance.gameObject);
        }
        Instance = this;

        // AudioSource取得
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        // AudioSourceはポーズ中も動作するように設定（SE再生用）
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f; // 2D
    }

    private void Update()
    {
        // ESCキーでポーズ切り替え（ブロック中は無効）
        if (Input.GetKeyDown(KeyCode.Escape) && !isPauseBlocked)
        {
            TogglePause();
        }
    }

    /// <summary>
    /// ポーズの切り替え
    /// </summary>
    public void TogglePause()
    {
        if (isPaused)
        {
            Resume();
        }
        else
        {
            Pause();
        }
    }

    /// <summary>
    /// ポーズ開始
    /// </summary>
    public void Pause()
    {
        if (isPaused || isPauseBlocked) return;

        isPaused = true;

        // Time.timeScaleを保存して0に設定
        savedTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        // SE再生（unscaledTimeで再生）
        PlaySoundUnscaled(pauseStartClip);

        // スローモーションボタンを無効化
        DisableSlowMotionButton();

        // イベント通知
        int listenerCount = OnPauseStarted != null ? OnPauseStarted.GetInvocationList().Length : 0;
        if (showDebugLog) Debug.Log($"[PauseManager] Game paused. Notifying {listenerCount} listeners.");
        OnPauseStarted?.Invoke();
    }

    /// <summary>
    /// ポーズ解除
    /// </summary>
    public void Resume()
    {
        if (!isPaused) return;

        isPaused = false;

        // Time.timeScaleを復元
        Time.timeScale = savedTimeScale;

        // SE再生（unscaledTimeで再生）
        PlaySoundUnscaled(pauseEndClip);

        // スローモーションボタンを有効化
        EnableSlowMotionButton();

        // イベント通知
        int listenerCount = OnPauseEnded != null ? OnPauseEnded.GetInvocationList().Length : 0;
        if (showDebugLog) Debug.Log($"[PauseManager] Game resumed. Notifying {listenerCount} listeners.");
        OnPauseEnded?.Invoke();
    }

    /// <summary>
    /// スローモーションボタンを無効化
    /// </summary>
    public void DisableSlowMotionButton()
    {
        SlowMotionUIManager slowMotionUI = FindFirstObjectByType<SlowMotionUIManager>();
        if (slowMotionUI != null)
        {
            var button = slowMotionUI.GetComponentInChildren<UnityEngine.UI.Button>();
            if (button != null)
            {
                button.interactable = false;
            }
        }
    }

    /// <summary>
    /// スローモーションボタンを有効化
    /// </summary>
    public void EnableSlowMotionButton()
    {
        SlowMotionUIManager slowMotionUI = FindFirstObjectByType<SlowMotionUIManager>();
        if (slowMotionUI != null)
        {
            var button = slowMotionUI.GetComponentInChildren<UnityEngine.UI.Button>();
            if (button != null)
            {
                button.interactable = true;
            }
        }
    }

    /// <summary>
    /// SE再生（unscaledTimeで再生、ポーズ中も鳴る）
    /// </summary>
    private void PlaySoundUnscaled(AudioClip clip)
    {
        if (clip != null && audioSource != null)
        {
            // SoundSettingsManagerからSE音量を取得
            float volume = 1f;
            if (SoundSettingsManager.Instance != null)
            {
                volume = SoundSettingsManager.Instance.SEVolume;
            }
            audioSource.PlayOneShot(clip, volume);
        }
    }

    /// <summary>
    /// ボタンクリックSEを再生
    /// </summary>
    public void PlayButtonClickSound()
    {
        PlaySoundUnscaled(buttonClickClip);
    }

    /// <summary>
    /// 確認SE（Yes）を再生
    /// </summary>
    public void PlayConfirmSound()
    {
        PlaySoundUnscaled(confirmClip);
    }

    /// <summary>
    /// キャンセルSE（No）を再生
    /// </summary>
    public void PlayCancelSound()
    {
        PlaySoundUnscaled(cancelClip);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;

            // ゲーム終了時にTime.timeScaleを復元
            Time.timeScale = 1f;
        }
    }
}
