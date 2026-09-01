using UnityEngine;

/// <summary>
/// 「無限化の石」（消費型アイテム。1個使うとジェム1個を無限化する）の所持数管理シングルトン。
/// GoldManagerと同じ、PlayerPrefsに直接個数を保存する方式（ProgressDataは汚さない）。
/// AreaSelectシーンに配置する想定（GoldManagerと同様、DontDestroyOnLoadはしない）。
/// </summary>
public class InfiniteStoneManager : MonoBehaviour
{
    public static InfiniteStoneManager Instance { get; private set; }

    private const string PERSISTENT_COUNT_KEY = "InfiniteStone_Persistent";

    private int count = 0;

    public int Count => count;

    /// <summary>所持数が変化した時に発火（引数: 現在の所持数）</summary>
    public event System.Action<int> OnCountChanged;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        count = PlayerPrefs.GetInt(PERSISTENT_COUNT_KEY, 0);
        Debug.Log($"[InfiniteStoneManager] Initialized. Count={count}");
    }

    /// <summary>
    /// 所持数を加算する（課金購入・エリア初回クリア報酬などから呼ぶ）。
    /// </summary>
    public void Add(int amount)
    {
        if (amount <= 0) return;
        count += amount;
        PlayerPrefs.SetInt(PERSISTENT_COUNT_KEY, count);
        PlayerPrefs.Save();
        OnCountChanged?.Invoke(count);
        Debug.Log($"[InfiniteStoneManager] Add: +{amount} → Count={count}");
    }

    /// <summary>
    /// 所持数を1個消費する。0個の場合は何もせず false を返す。
    /// </summary>
    public bool TryUse()
    {
        if (count <= 0) return false;
        count--;
        PlayerPrefs.SetInt(PERSISTENT_COUNT_KEY, count);
        PlayerPrefs.Save();
        OnCountChanged?.Invoke(count);
        Debug.Log($"[InfiniteStoneManager] TryUse: -1 → Count={count}");
        return true;
    }

    /// <summary>
    /// 所持数を0にリセットする（ゲーム進行度初期化用）。
    /// InfiniteStoneManagerのインスタンスが存在しないシーンからでも呼べるようstatic。
    /// </summary>
    public static void ResetPersistentCount()
    {
        PlayerPrefs.DeleteKey(PERSISTENT_COUNT_KEY);
        PlayerPrefs.Save();
        if (Instance != null)
        {
            Instance.count = 0;
            Instance.OnCountChanged?.Invoke(0);
        }
    }
}
