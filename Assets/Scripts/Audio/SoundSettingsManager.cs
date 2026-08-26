using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// サウンド設定を管理するマネージャー
/// BGM/SEの音量をPlayerPrefsで保存・読み込み
/// </summary>
public class SoundSettingsManager : MonoBehaviour
{
    public static SoundSettingsManager Instance { get; private set; }

    [Header("Volume Settings")]
    [Tooltip("BGM音量（0-1）")]
    [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.5f;

    [Tooltip("SE音量（0-1）")]
    [SerializeField, Range(0f, 1f)] private float seVolume = 1f;

    [Header("PlayerPrefs Keys")]
    [SerializeField] private string bgmVolumeKey = "BGMVolume";
    [SerializeField] private string seVolumeKey = "SEVolume";

    // Public properties
    public float BGMVolume
    {
        get => bgmVolume;
        set
        {
            bgmVolume = Mathf.Clamp01(value);
            ApplyBGMVolume();
            SaveSettings();
        }
    }

    public float SEVolume
    {
        get => seVolume;
        set
        {
            seVolume = Mathf.Clamp01(value);
            ApplySEVolume();
            SaveSettings();
        }
    }

    // Events
    public System.Action<float> OnBGMVolumeChanged;
    public System.Action<float> OnSEVolumeChanged;

    private void Awake()
    {
        // Singleton setup
        // ★Destroy(gameObject)にすると、このコンポーネントと同じGameObjectに同居している
        //   別の重要なコンポーネント(例:05_GameのPauseSystem内の一時停止制御スクリプト)まで
        //   巻き込んで破棄してしまうため、自分自身のコンポーネントだけを破棄する。
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;

        // ★シーンをまたいで同じ設定を保持し続けるため永続化する。
        //   (以前はTitleシーンにこのコンポーネント自体が存在せず、Instanceがnullのままだったため
        //    タイトル画面のサウンド設定が一切反映されない不具合があった)
        DontDestroyOnLoad(gameObject);

        // 設定を読み込み
        LoadSettings();

        // ★このコンポーネント自体はDontDestroyOnLoadでシーンをまたいで生き続けるが、
        //   BGM再生コンポーネント(GameplayBgmRandomPlayer等)は各シーンで新しく生成される。
        //   シーン遷移のたびに音量を再適用しないと、新しいシーンのBGMには設定値が反映されないまま
        //   （中断画面のサウンド設定を開いて初めて反映される、という不具合があった）。
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void Start()
    {
        // 音量を適用
        ApplyBGMVolume();
        ApplySEVolume();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyBGMVolume();
        ApplySEVolume();
    }

    /// <summary>
    /// 設定を読み込み
    /// </summary>
    private void LoadSettings()
    {
        if (PlayerPrefs.HasKey(bgmVolumeKey))
        {
            bgmVolume = PlayerPrefs.GetFloat(bgmVolumeKey, 0.5f);
        }

        if (PlayerPrefs.HasKey(seVolumeKey))
        {
            seVolume = PlayerPrefs.GetFloat(seVolumeKey, 1f);
        }

        Debug.Log($"[SoundSettingsManager] Settings loaded - BGM: {bgmVolume:F2}, SE: {seVolume:F2}");
    }

    /// <summary>
    /// 設定を保存
    /// </summary>
    private void SaveSettings()
    {
        PlayerPrefs.SetFloat(bgmVolumeKey, bgmVolume);
        PlayerPrefs.SetFloat(seVolumeKey, seVolume);
        PlayerPrefs.Save();

        Debug.Log($"[SoundSettingsManager] Settings saved - BGM: {bgmVolume:F2}, SE: {seVolume:F2}");
    }

    /// <summary>
    /// BGM音量を適用。
    /// ★どの画面で実際に鳴っているBGMかによって管理コンポーネントが異なるため、
    /// 該当しうる音源すべてに音量を適用する(存在しないものは内部でnullチェックされスキップされるだけ)。
    /// ・GameplayBgmRandomPlayer: 通常プレイ中のエリア別BGM(05_Game、チュートリアル以外)
    /// ・TitleBGMManager: タイトル画面のBGM
    /// ・AreaSelectBGM_Persistent: AreaSelectから引き継いで鳴り続ける永続BGM(チュートリアル中はこれが鳴っている)
    /// </summary>
    private void ApplyBGMVolume()
    {
        bool applied = false;

        // GameplayBgmRandomPlayerを探す(通常プレイ中)
        GameplayBgmRandomPlayer bgmPlayer = FindFirstObjectByType<GameplayBgmRandomPlayer>();
        if (bgmPlayer != null)
        {
            bgmPlayer.Volume = bgmVolume;
            Debug.Log($"[SoundSettingsManager] BGM volume applied: {bgmVolume:F2} to {bgmPlayer.gameObject.name}");
            applied = true;
        }

        // タイトル画面のBGM
        if (Game.UI.TitleBGMManager.IsPlaying)
        {
            Game.UI.TitleBGMManager.Volume = bgmVolume;
            Debug.Log($"[SoundSettingsManager] BGM volume applied: {bgmVolume:F2} to TitleBGMManager");
            applied = true;
        }

        // AreaSelectから引き継いだ永続BGM(チュートリアル中を含む)
        GameObject persistentBGM = GameObject.Find("AreaSelectBGM_Persistent");
        if (persistentBGM != null)
        {
            AudioSource persistentSource = persistentBGM.GetComponent<AudioSource>();
            if (persistentSource != null)
            {
                persistentSource.volume = bgmVolume;
                Debug.Log($"[SoundSettingsManager] BGM volume applied: {bgmVolume:F2} to AreaSelectBGM_Persistent");
                applied = true;
            }
        }

        if (!applied)
        {
            Debug.LogWarning("[SoundSettingsManager] BGMを再生中のコンポーネントが見つかりませんでした。");
        }

        // イベント通知
        OnBGMVolumeChanged?.Invoke(bgmVolume);
    }

    /// <summary>
    /// SE音量を適用
    /// </summary>
    private void ApplySEVolume()
    {
        // ここでは何もしない（各SEはこのマネージャーからSEVolumeを取得して再生）
        // イベント通知のみ
        OnSEVolumeChanged?.Invoke(seVolume);

        Debug.Log($"[SoundSettingsManager] SE volume setting: {seVolume:F2}");
    }

    /// <summary>
    /// SEを再生（音量設定を適用）
    /// </summary>
    public void PlaySE(AudioSource audioSource, AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip, seVolume);
        }
    }

    /// <summary>
    /// 設定をリセット（デフォルト値に戻す）
    /// </summary>
    public void ResetToDefault()
    {
        BGMVolume = 0.5f;
        SEVolume = 1f;

        Debug.Log("[SoundSettingsManager] Settings reset to default");
    }

    /// <summary>
    /// 進行度初期化用：BGM/SE音量をデフォルト値(0.5 / 1.0)に戻す。
    /// SoundSettingsManagerのインスタンスが存在しないシーン（Titleより前等）からでも呼べるようstatic。
    /// 生きているインスタンスがあれば、そのまま現在再生中の音にも即座に反映する。
    /// </summary>
    public static void ResetVolumeToDefault()
    {
        const string BgmKey = "BGMVolume";
        const string SeKey = "SEVolume";
        const float DefaultBgm = 0.5f;
        const float DefaultSe = 1f;

        PlayerPrefs.SetFloat(BgmKey, DefaultBgm);
        PlayerPrefs.SetFloat(SeKey, DefaultSe);
        PlayerPrefs.Save();

        if (Instance != null)
        {
            Instance.ResetToDefault();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Instance = null;
        }
    }
}
