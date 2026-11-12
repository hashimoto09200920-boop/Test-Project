using UnityEngine;

/// <summary>
/// お食事券（フードチケット）の枚数（2026/11/11）。
/// ・道中で拾った分はこのプレイの分（Session）としてためておき、Areaをクリアした時・ゲームオーバーの時にまとめて持ち物（Owned）に入れる（ゴールドと同じ）。
///   中断メニューのリタイアで戻った時も同じく持ち物に入れる（2026/11/11）
/// ・ミッションの報酬はすぐ持ち物に入る
/// ・フード画面で、選んだフードを1枚で無料で買える（買える数の上限・同じフードは1回まで、はゴールドと同じく数える）
/// ・持てるのは99枚まで。保存は PlayerPrefs
/// </summary>
public static class FoodTickets
{
    private const string Key = "FoodTickets";
    public const int Max = 99;

    /// <summary>持っている枚数（保存されている分）</summary>
    public static int Owned => Mathf.Clamp(PlayerPrefs.GetInt(Key, 0), 0, Max);

    /// <summary>このプレイで拾って、まだ持ち物に入っていない枚数</summary>
    public static int Session { get; private set; }

    /// <summary>枚数が変わった時（表示の更新用）</summary>
    public static event System.Action OnChanged;

    /// <summary>道中で拾った（BlockItemManager）</summary>
    public static void AddSession(int n)
    {
        if (n <= 0) return;
        Session = Mathf.Min(Session + n, Max);
        OnChanged?.Invoke();
    }

    /// <summary>このプレイの分を捨てる（新しいプレイの始め。MissionProgressManager.OnSessionStatsReset）</summary>
    public static void ResetSession()
    {
        if (Session == 0) return;
        Session = 0;
        OnChanged?.Invoke();
    }

    /// <summary>Areaをクリアした時（EnemySpawner。ゴールドと同じ時）：このプレイの分を持ち物に入れる</summary>
    public static void TransferSession()
    {
        if (Session <= 0) return;
        int add = Session;
        Session = 0;
        SetOwned(Owned + add);
        Debug.Log($"[FoodTickets] このプレイで拾った {add} 枚を持ち物に入れました（持っている枚数 {Owned}）");
    }

    /// <summary>すぐ持ち物に入れる（ミッションの報酬）</summary>
    public static void AddOwned(int n)
    {
        if (n <= 0) return;
        SetOwned(Owned + n);
    }

    /// <summary>1枚使う（フード画面）。持っていなければfalse</summary>
    public static bool TryUse()
    {
        int o = Owned;
        if (o <= 0) return false;
        SetOwned(o - 1);
        return true;
    }

    private static void SetOwned(int v)
    {
        PlayerPrefs.SetInt(Key, Mathf.Clamp(v, 0, Max));
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }
}
