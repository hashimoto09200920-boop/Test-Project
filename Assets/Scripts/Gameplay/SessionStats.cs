using UnityEngine;

/// <summary>
/// 1プレイセッション中の統計カウンター（静的クラス）
/// GameManager.Awake() でリセット。各システムから直接呼ぶ。
/// </summary>
public static class SessionStats
{
    public static int   NormalReflectCount { get; private set; }
    public static int   JustReflectCount   { get; private set; }
    public static int   MaxJustStreak      { get; private set; }
    public static int   HpDamageDealt      { get; private set; }
    public static int   ShieldDamageDealt  { get; private set; }
    public static int   BlockDamageDealt   { get; private set; }
    public static int   DamageTaken        { get; private set; }
    public static int   OverheatCount      { get; private set; }
    public static int   GoldEarned         { get; private set; }
    public static int   DownCount          { get; private set; }
    public static float ClearTime          { get; private set; }
    public static int   EnemyKillCount     { get; private set; }
    public static int   BlockDestroyCount  { get; private set; }

    private static int   currentJustStreak;
    private static float sessionStartTime;

    public static int   TotalReflectCount => NormalReflectCount + JustReflectCount;
    public static float JustRate => TotalReflectCount > 0
        ? (float)JustReflectCount / TotalReflectCount
        : 0f;

    public static void Reset()
    {
        NormalReflectCount = 0;
        JustReflectCount   = 0;
        MaxJustStreak      = 0;
        currentJustStreak  = 0;
        HpDamageDealt      = 0;
        ShieldDamageDealt  = 0;
        BlockDamageDealt   = 0;
        DamageTaken        = 0;
        OverheatCount      = 0;
        GoldEarned         = 0;
        DownCount          = 0;
        ClearTime          = 0f;
        sessionStartTime   = 0f;
        EnemyKillCount     = 0;
        BlockDestroyCount  = 0;
    }

    public static void AddReflect(bool isJust)
    {
        if (isJust)
        {
            JustReflectCount++;
            currentJustStreak++;
            if (currentJustStreak > MaxJustStreak) MaxJustStreak = currentJustStreak;
        }
        else
        {
            NormalReflectCount++;
            currentJustStreak = 0;
        }
    }

    public static void AddHpDamage(int amount)     { if (amount > 0) HpDamageDealt     += amount; }
    public static void AddShieldDamage(int amount) { if (amount > 0) ShieldDamageDealt += amount; }
    public static void AddBlockDamage(int amount)  { if (amount > 0) BlockDamageDealt  += amount; }
    public static void AddDamageTaken(int amount)  { if (amount > 0) DamageTaken       += amount; }
    public static void AddOverheat()               { OverheatCount++; }
    public static void AddGold(int amount)         { if (amount > 0) GoldEarned        += amount; }
    public static void AddDown()                   { DownCount++; }
    public static void AddEnemyKill()              { EnemyKillCount++; }
    public static void AddBlockDestroy()           { BlockDestroyCount++; }

    public static void StartTimer() { sessionStartTime = Time.time; }
    public static void StopTimer()  { if (sessionStartTime > 0f) ClearTime = Time.time - sessionStartTime; }
}
