using UnityEngine;

/// <summary>
/// 1プレイセッション中の統計カウンター（静的クラス）
/// GameManager.Awake() でリセット。各システムから直接呼ぶ。
/// </summary>
public static class SessionStats
{
    public static int NormalReflectCount { get; private set; }
    public static int JustReflectCount   { get; private set; }
    public static int HpDamageDealt      { get; private set; }
    public static int ShieldDamageDealt  { get; private set; }
    public static int BlockDamageDealt   { get; private set; }
    public static int DamageTaken        { get; private set; }
    public static int OverheatCount      { get; private set; }
    public static int GoldEarned         { get; private set; }

    public static int TotalReflectCount => NormalReflectCount + JustReflectCount;
    public static float JustRate => TotalReflectCount > 0
        ? (float)JustReflectCount / TotalReflectCount
        : 0f;

    public static void Reset()
    {
        NormalReflectCount = 0;
        JustReflectCount   = 0;
        HpDamageDealt      = 0;
        ShieldDamageDealt  = 0;
        BlockDamageDealt   = 0;
        DamageTaken        = 0;
        OverheatCount      = 0;
        GoldEarned         = 0;
    }

    public static void AddReflect(bool isJust)
    {
        if (isJust) JustReflectCount++;
        else        NormalReflectCount++;
    }

    public static void AddHpDamage(int amount)     { if (amount > 0) HpDamageDealt     += amount; }
    public static void AddShieldDamage(int amount) { if (amount > 0) ShieldDamageDealt += amount; }
    public static void AddBlockDamage(int amount)  { if (amount > 0) BlockDamageDealt  += amount; }
    public static void AddDamageTaken(int amount)  { if (amount > 0) DamageTaken       += amount; }
    public static void AddOverheat()               { OverheatCount++; }
    public static void AddGold(int amount)         { if (amount > 0) GoldEarned        += amount; }
}
