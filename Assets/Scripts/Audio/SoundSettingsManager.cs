using UnityEngine;

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
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 設定を読み込み
        LoadSettings();
    }

    private void Start()
    {
        // 音量を適用
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
    /// BGM音量を適用
    /// </summary>
    private void ApplyBGMVolume()
    {
        // GameplayBgmRandomPlayerを探す
        GameplayBgmRandomPlayer bgmPlayer = FindFirstObjectByType<GameplayBgmRandomPlayer>();
        if (bgmPlayer != null)
        {
            // GameplayBgmRandomPlayerのVolumeプロパティを変更
            bgmPlayer.Volume = bgmVolume;
            Debug.Log($"[SoundSettingsManager] BGM volume applied: {bgmVolume:F2} to {bgmPlayer.gameObject.name}");
        }
        else
        {
            Debug.LogWarning("[SoundSettingsManager] GameplayBgmRandomPlayer not found in scene!");
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

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
