using UnityEngine;

/// <summary>
/// ゴールド（通貨）管理シングルトン
/// - SessionGold  : 1ゲーム内のゴールド（Stage3クリアで永続化、失敗でロスト）
/// - PersistentGold: PlayerPrefsに保存する永続ゴールド（03_AreaSelectで表示、ショップで消費）
/// </summary>
public class GoldManager : MonoBehaviour
{
    public static GoldManager Instance { get; private set; }

    [Header("Gold SE")]
    [SerializeField] private AudioClip goldSE;
    [SerializeField] private float goldSEVolume = 1f;

    [Header("Debug")]
    [Tooltip("ONにするとゴールド加算のたびにログを出す。敵の大量同時撃破時、無条件ログはEditor上でスタックトレース取得コストが積み重なりフリーズの原因になりうるため、既定でOFF")]
    [SerializeField] private bool showDebugLog = false;

    private const string PERSISTENT_GOLD_KEY = "Gold_Persistent";
    private const int MAX_GOLD = 99999;

    private int sessionGold = 0;
    private int persistentGold = 0;
    private AudioSource audioSource;

    public int SessionGold => sessionGold;
    public int PersistentGold => persistentGold;

    /// <summary>セッションゴールドが変化した時に発火（引数: 現在のSessionGold）</summary>
    public event System.Action<int> OnSessionGoldChanged;

    /// <summary>永続ゴールドが変化した時に発火（引数: 現在のPersistentGold）</summary>
    public event System.Action<int> OnPersistentGoldChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        persistentGold = PlayerPrefs.GetInt(PERSISTENT_GOLD_KEY, 0);
        sessionGold = 0;

        Debug.Log($"[GoldManager] Initialized. PersistentGold={persistentGold}");
    }

    /// <summary>
    /// 敵撃破時に呼ぶ。SE再生＋SessionGold加算。
    /// </summary>
    public void AddSessionGold(int amount)
    {
        if (amount <= 0) return;
        sessionGold = Mathf.Min(sessionGold + amount, MAX_GOLD);
        SessionStats.AddGold(amount);
        OnSessionGoldChanged?.Invoke(sessionGold);
        PlayGoldSE();
        if (showDebugLog)
            Debug.Log($"[GoldManager] AddSessionGold: +{amount} → SessionGold={sessionGold}");
    }

    /// <summary>
    /// Stage3クリア時に呼ぶ。SessionGold を PersistentGold に加算して PlayerPrefs に保存。
    /// </summary>
    public void TransferSessionGoldToPersistent()
    {
        if (sessionGold <= 0) return;

        persistentGold = Mathf.Min(persistentGold + sessionGold, MAX_GOLD);
        PlayerPrefs.SetInt(PERSISTENT_GOLD_KEY, persistentGold);
        PlayerPrefs.Save();

        Debug.Log($"[GoldManager] TransferSessionGoldToPersistent: +{sessionGold} → PersistentGold={persistentGold}");

        sessionGold = 0;
        OnPersistentGoldChanged?.Invoke(persistentGold);
    }

    /// <summary>
    /// ショップでゴールドを消費する（後で使用）。
    /// </summary>
    public void SpendPersistentGold(int amount)
    {
        if (amount <= 0) return;
        persistentGold = Mathf.Max(0, persistentGold - amount);
        PlayerPrefs.SetInt(PERSISTENT_GOLD_KEY, persistentGold);
        PlayerPrefs.Save();
        OnPersistentGoldChanged?.Invoke(persistentGold);
        Debug.Log($"[GoldManager] SpendPersistentGold: -{amount} → PersistentGold={persistentGold}");
    }

    /// <summary>
    /// PersistentGold（PlayerPrefs保存分）を0にリセットする（ゲーム進行度初期化用）。
    /// GoldManagerのインスタンスが存在しないシーン（Titleなど）からでも呼べるようstatic。
    /// </summary>
    public static void ResetPersistentGold()
    {
        PlayerPrefs.DeleteKey(PERSISTENT_GOLD_KEY);
        PlayerPrefs.Save();
        if (Instance != null)
        {
            Instance.persistentGold = 0;
            Instance.OnPersistentGoldChanged?.Invoke(0);
        }
    }

    /// <summary>
    /// PersistentGold を指定値に直接セットする（デバッグ用途など）。
    /// </summary>
    public void SetPersistentGold(int amount)
    {
        persistentGold = Mathf.Clamp(amount, 0, MAX_GOLD);
        PlayerPrefs.SetInt(PERSISTENT_GOLD_KEY, persistentGold);
        PlayerPrefs.Save();
        OnPersistentGoldChanged?.Invoke(persistentGold);
        Debug.Log($"[GoldManager] SetPersistentGold: PersistentGold={persistentGold}");
    }

    /// <summary>
    /// AreaSelectなどでゴールドを直接 PersistentGold に加算する（ジェム売却など）。
    /// </summary>
    public void AddPersistentGold(int amount)
    {
        if (amount <= 0) return;
        persistentGold = Mathf.Min(persistentGold + amount, MAX_GOLD);
        PlayerPrefs.SetInt(PERSISTENT_GOLD_KEY, persistentGold);
        PlayerPrefs.Save();
        OnPersistentGoldChanged?.Invoke(persistentGold);
        Debug.Log($"[GoldManager] AddPersistentGold: +{amount} → PersistentGold={persistentGold}");
    }

    private void PlayGoldSE()
    {
        if (audioSource == null || !audioSource.isActiveAndEnabled || goldSE == null) return;
        // ★大量の敵が同時に撃破されると、同一フレーム内でこのSEが何十回も重なるため、
        //   既存のSeSimultaneousGuard（PaddleDrawer等と同じ仕組み）で同一フレーム内は1回に制限する
        if (!SeSimultaneousGuard.TryAllow("GoldSE")) return;
        float vol = goldSEVolume * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
        audioSource.PlayOneShot(goldSE, vol);
    }
}
