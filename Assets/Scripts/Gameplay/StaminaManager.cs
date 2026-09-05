using UnityEngine;

/// <summary>
/// スタミナ（Areaプレイ権）管理シングルトン。
/// GoldManager/InfiniteStoneManagerと同じ、PlayerPrefsに直接保存する方式（ProgressDataは汚さない）。
/// AreaSelect・05_Gameの両シーンに配置する想定（DontDestroyOnLoadはしない）。
/// 時間経過での自動回復はアプリ未起動中も進むよう、実時刻(DateTime.UtcNow)を保存して差分計算する。
/// </summary>
public class StaminaManager : MonoBehaviour
{
    public static StaminaManager Instance { get; private set; }

    public const int MaxStamina = 5;
    public const int RegenIntervalMinutes = 30;

    private const string PERSISTENT_COUNT_KEY = "Stamina_Persistent_Count";
    private const string PERSISTENT_LAST_TICKS_KEY = "Stamina_Persistent_LastTicks";
    private const string PERSISTENT_UNLIMITED_KEY = "Stamina_Persistent_Unlimited";

    private int count;
    private System.DateTime lastBelowMaxUtc;
    private bool isUnlimited;

    /// <summary>現在のスタミナ数（Unlimited中も内部値は保持するが、表示にはIsUnlimitedを優先すること）</summary>
    public int Count => count;
    public bool IsUnlimited => isUnlimited;

    /// <summary>スタミナ数・Unlimited状態が変化した時に発火</summary>
    public event System.Action OnStaminaChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        Load();
        ApplyPendingRegen();

        Debug.Log($"[StaminaManager] Initialized. Count={count}/{MaxStamina} Unlimited={isUnlimited}");
    }

    private void Load()
    {
        isUnlimited = PlayerPrefs.GetInt(PERSISTENT_UNLIMITED_KEY, 0) == 1;
        count = PlayerPrefs.GetInt(PERSISTENT_COUNT_KEY, MaxStamina);

        string savedTicksStr = PlayerPrefs.GetString(PERSISTENT_LAST_TICKS_KEY, "");
        if (long.TryParse(savedTicksStr, out long savedTicks))
        {
            lastBelowMaxUtc = new System.DateTime(savedTicks, System.DateTimeKind.Utc);
        }
        else
        {
            lastBelowMaxUtc = System.DateTime.UtcNow;
        }
    }

    private void SaveCount()
    {
        // ★PlayerPrefs.SetIntだけではメモリ上のキャッシュに乗るだけでディスクに書かれない。
        //   アプリ強制終了時にUnityの自動フラッシュ(OnApplicationQuit相当)が走らず、
        //   他システムのPlayerPrefs.Save()に便乗できるタイミングより前に強制終了すると
        //   消費が無かったことになるバグがあった。ここで確実にSaveまで行う。
        PlayerPrefs.SetInt(PERSISTENT_COUNT_KEY, count);
        PlayerPrefs.Save();
    }

    private void SaveTimestamp()
    {
        // ★以前ここに「時計巻き戻し防止」処理があったが、lastBelowMaxUtcは常に過去の時刻のため
        //   「now > lastBelowMaxUtc」が通常時は常にtrueになり、スタミナを消費するたびに
        //   タイマーがリセットされてしまうバグだった（4/5→3/5消費で20:00→29:55に化ける等）。
        //   対策は入れない方針で確定しているため、単純に保存するだけにする。
        PlayerPrefs.SetString(PERSISTENT_LAST_TICKS_KEY, lastBelowMaxUtc.Ticks.ToString());
        PlayerPrefs.Save();
    }

    /// <summary>
    /// 経過時間から回復分を計算して反映する。Awake時に加え、表示更新のたびに呼んで
    /// リアルタイムに反映されるようにする（HUD側から毎フレーム呼ばれる想定）。
    /// </summary>
    public void RefreshRegen()
    {
        ApplyPendingRegen();
    }

    private void ApplyPendingRegen()
    {
        if (isUnlimited) return;

        if (count >= MaxStamina)
        {
            // ★上限中は経過時間を裏で貯めない。基準時刻を常に「今」に更新し続ける。
            lastBelowMaxUtc = System.DateTime.UtcNow;
            SaveTimestamp();
            return;
        }

        double elapsedMinutes = (System.DateTime.UtcNow - lastBelowMaxUtc).TotalMinutes;
        if (elapsedMinutes < RegenIntervalMinutes) return;

        int gained = (int)(elapsedMinutes / RegenIntervalMinutes);
        if (gained <= 0) return;

        int before = count;
        count = Mathf.Min(MaxStamina, count + gained);

        if (count >= MaxStamina)
        {
            lastBelowMaxUtc = System.DateTime.UtcNow;
        }
        else
        {
            // 消費しきれなかった端数時間は繰り越す（次の回復までの待ち時間として維持）
            lastBelowMaxUtc = lastBelowMaxUtc.AddMinutes(gained * RegenIntervalMinutes);
        }

        SaveCount();
        SaveTimestamp();

        if (count != before)
        {
            Debug.Log($"[StaminaManager] Regen: +{count - before} → Count={count}/{MaxStamina}");
            OnStaminaChanged?.Invoke();
        }
    }

    /// <summary>
    /// 次の1回復までの残り秒数。上限中・Unlimited中は0を返す。
    /// </summary>
    public float GetSecondsUntilNextStamina()
    {
        RefreshRegen();
        if (isUnlimited || count >= MaxStamina) return 0f;

        double elapsedSeconds = (System.DateTime.UtcNow - lastBelowMaxUtc).TotalSeconds;
        double remaining = RegenIntervalMinutes * 60d - elapsedSeconds;
        return (float)System.Math.Max(0d, remaining);
    }

    /// <summary>
    /// Area1〜10のStage1開始時に呼ぶ。Unlimited中は常にtrue。0の場合はfalseを返す（呼び出し側で広告視聴導線へ）。
    /// </summary>
    public bool TryConsume()
    {
        RefreshRegen();

        if (isUnlimited)
        {
            Debug.Log("[StaminaManager] TryConsume: Unlimited中のため消費なし");
            return true;
        }

        if (count <= 0) return false;

        bool wasAtMax = count >= MaxStamina;
        count--;
        if (wasAtMax)
        {
            // ★上限から減った瞬間から新たに30分カウントを開始する
            lastBelowMaxUtc = System.DateTime.UtcNow;
        }
        SaveCount();
        SaveTimestamp();
        OnStaminaChanged?.Invoke();
        Debug.Log($"[StaminaManager] TryConsume: -1 → Count={count}/{MaxStamina}");
        return true;
    }

    /// <summary>
    /// 広告視聴（仮実装）成功時などに呼ぶ。上限を超えては加算しない。
    /// </summary>
    public void Add(int amount)
    {
        if (amount <= 0) return;
        RefreshRegen();

        int before = count;
        count = Mathf.Min(MaxStamina, count + amount);
        SaveCount();
        if (count >= MaxStamina) SaveTimestamp();

        if (count != before)
        {
            OnStaminaChanged?.Invoke();
            Debug.Log($"[StaminaManager] Add: +{amount} → Count={count}/{MaxStamina}");
        }
    }

    /// <summary>
    /// デバッグ用：広告解除（購入）状態をトグルする。ONの間はスタミナ消費・表示ともに無制限(∞)扱い。
    /// </summary>
    public void SetUnlimited(bool value)
    {
        isUnlimited = value;
        PlayerPrefs.SetInt(PERSISTENT_UNLIMITED_KEY, value ? 1 : 0);
        PlayerPrefs.Save();
        OnStaminaChanged?.Invoke();
        Debug.Log($"[StaminaManager] SetUnlimited: {value}");
    }

    /// <summary>
    /// スタミナ状態を初期値に戻す（ゲーム進行度初期化用）。
    /// StaminaManagerのインスタンスが存在しないシーンからでも呼べるようstatic。
    /// </summary>
    public static void ResetPersistentState()
    {
        PlayerPrefs.DeleteKey(PERSISTENT_COUNT_KEY);
        PlayerPrefs.DeleteKey(PERSISTENT_LAST_TICKS_KEY);
        PlayerPrefs.DeleteKey(PERSISTENT_UNLIMITED_KEY);
        PlayerPrefs.Save();
        if (Instance != null)
        {
            Instance.count = MaxStamina;
            Instance.lastBelowMaxUtc = System.DateTime.UtcNow;
            Instance.isUnlimited = false;
            Instance.OnStaminaChanged?.Invoke();
        }
    }
}
