using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ミッションの進捗を数えてセーブする管理役（2026/11/07）。シーンに置く必要なし（自動生成・DontDestroyOnLoad）。
/// ・各ゲーム処理から静的メソッド（OnLineReflect等）を1行呼ぶだけで数える
/// ・Tutorial中（GameSession.IsInTutorial）は一切数えない
/// ・保存はPlayerPrefsの専用キー（ProgressDataとは別）。保存タイミングはStageクリア・Areaクリア・Result表示・アプリ中断/終了
/// </summary>
public class MissionProgressManager : MonoBehaviour
{
    public static MissionProgressManager Instance { get; private set; }

    private const string SaveKey = "MISSION_PROGRESS_V1";

    [Serializable]
    private class SaveData
    {
        public int version = 1;
        public List<string> keys = new List<string>();
        public List<int> values = new List<int>();
        public List<string> claimedIds = new List<string>();
        // 虹の雫をミッションで渡す前（Areaの初回クリアで直接もらっていた頃）にクリア済みのAreaを、受け取り済みにしたか（2026/11/08）
        public bool rainbowDropMigrated;
    }

    private readonly Dictionary<string, int> counters = new Dictionary<string, int>();
    private readonly HashSet<string> claimedIds = new HashSet<string>();
    private MissionCatalog catalog;
    private bool dirty;
    private bool rainbowDropMigrated;

    /// <summary>今回のプレイ（05_Gameに入ってから）で新しく達成したミッションのid。プレイ中の通知とResult画面の枠で使う</summary>
    public static readonly List<string> NewlyAchievedThisRun = new List<string>();

    /// <summary>ミッションを新しく達成した瞬間に呼ばれる（プレイ中の通知用）</summary>
    public static event Action<MissionDefinition> OnMissionAchieved;

    // ---- 1Stage内の状態（Stageクリア時に判定してリセット） ----
    private static bool s_whiteOverheatThisStage;
    private static bool s_redOverheatThisStage;
    private static bool s_dancerHitThisStage;

    // ---- KillsはResult画面のKILLS（SessionStats.EnemyKillCount）と同じ数にするため、増えた分だけ足す ----
    private static int s_syncedSessionKills;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnsureInstance()
    {
        if (Instance != null) return;
        var go = new GameObject("MissionProgressManager");
        DontDestroyOnLoad(go);
        Instance = go.AddComponent<MissionProgressManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        catalog = Resources.Load<MissionCatalog>(MissionCatalog.ResourcesPath);
        if (catalog == null)
            Debug.LogWarning($"[Mission] Resources/{MissionCatalog.ResourcesPath} が見つかりません。Tools > ミッション > 「1. ミッション一覧を作成（89個）」で作成してください（進捗は数えますが達成判定はできません）");
        Load();
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnemySpawner.OnStageCleared += HandleStageCleared;
    }

    private void OnDestroy()
    {
        if (Instance != this) return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        EnemySpawner.OnStageCleared -= HandleStageCleared;
    }

    private void OnApplicationPause(bool paused) { if (paused) SaveIfDirty(); }
    private void OnApplicationQuit() { SaveIfDirty(); }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!rainbowDropMigrated) MigrateRainbowDrops();
        MigrateNoticeMissions();
        AutoClaimAchievedNotices();

        // 新しいプレイの開始時に、1プレイ分の状態をリセットする
        if (scene.name == "05_Game")
        {
            NewlyAchievedThisRun.Clear();
            ResetStageFlags();
        }
        else
        {
            SaveIfDirty();
        }
    }

    private static bool CanCount => Instance != null && !GameSession.IsInTutorial;

    // =====================================================================
    // 数える入口（ゲーム処理から1行で呼ぶ）
    // =====================================================================

    /// <summary>線で弾を反射した（PaddleDot・ShamanSpiritFlames）</summary>
    public static void OnLineReflect(PaddleDot.LineType lineType, bool isJust, Stroke stroke)
    {
        if (!CanCount) return;
        bool white = lineType == PaddleDot.LineType.Normal;
        Instance.AddInternal(white ? MissionStat.ReflectWhite : MissionStat.ReflectRed, 1);
        if (isJust) Instance.AddInternal(MissionStat.JustReflects, 1);
        if (stroke != null)
        {
            stroke.MissionReflectCount++;
            if (stroke.MissionReflectCount == 2) // 1本の線で2個目を反射した瞬間に1回だけ数える
                Instance.AddInternal(white ? MissionStat.MultiReflectWhite : MissionStat.MultiReflectRed, 1);
        }
    }

    /// <summary>少しでも線を引いた（Stroke.RegisterDotで2つ目の点ができた時。タップだけでは数えない）</summary>
    public static void OnLineDrawn() { if (CanCount) Instance.AddInternal(MissionStat.LinesDrawn, 1); }

    private static readonly List<Collider2D> s_overlapBuffer = new List<Collider2D>(64);
    private static readonly HashSet<EnemyBullet> s_bulletSet = new HashSet<EnemyBullet>();

    /// <summary>円ができた（StrokeManager.NotifyCircleFormed）。スローモーション中の円と、円の中の弾の数を数える</summary>
    public static void OnCircleFormed(Bounds circleBounds)
    {
        if (!CanCount) return;
        var slow = SlowMotionManager.Instance;
        if (slow != null && slow.IsSlowMotionActive) Instance.AddInternal(MissionStat.SlowMotionCircles, 1);

        // 1つの円で弾を5個以上同時に囲む（まだ達成していない時だけ調べる）
        if (Instance.GetCounter(MissionStat.CircleEnclose5Bullets, 0) >= 1) return;
        int mask = LayerMask.GetMask("UnreflectedBullet", "ReflectedBullet");
        if (mask == 0) return;
        Vector2 center = circleBounds.center;
        float radius = Mathf.Max(circleBounds.extents.x, circleBounds.extents.y);
        var filter = new ContactFilter2D { useLayerMask = true, layerMask = mask, useTriggers = true };
        s_overlapBuffer.Clear();
        Physics2D.OverlapCircle(center, radius, filter, s_overlapBuffer);
        s_bulletSet.Clear();
        for (int i = 0; i < s_overlapBuffer.Count; i++)
        {
            var c = s_overlapBuffer[i];
            if (c == null) continue;
            var b = c.GetComponentInParent<EnemyBullet>();
            if (b != null && b.gameObject.activeInHierarchy) s_bulletSet.Add(b);
        }
        int count = s_bulletSet.Count;
        s_bulletSet.Clear();
        s_overlapBuffer.Clear();
        if (count >= 5) Instance.SetAtLeast(MissionStat.CircleEnclose5Bullets, 0, 1);
    }

    /// <summary>線のゲージが満タンまで戻った（PaddleCostManager）。white=true:白線（Left）</summary>
    public static void OnGaugeFull(bool white) { if (CanCount) Instance.AddInternal(white ? MissionStat.GaugeFullWhite : MissionStat.GaugeFullRed, 1); }

    /// <summary>線がオーバーヒートした（PaddleCostManager）。white=true:白線（Left）</summary>
    public static void OnLineOverheat(bool white)
    {
        if (white) s_whiteOverheatThisStage = true; else s_redOverheatThisStage = true;
    }

    /// <summary>SessionStats.Reset()から呼ぶ</summary>
    public static void OnSessionStatsReset() { s_syncedSessionKills = 0; MissionPassives.ResetRunState(); FoodTickets.ResetSession(); }
    public static void OnFoodTicketCollected() { if (CanCount) Instance.AddInternal(MissionStat.FoodTicketCollected, 1); } // お食事券を取った回数（2026/11/11）

    /// <summary>SessionStats.RemoveEnemyKill()（Result画面の撃破数を後から-1する処理）から呼ぶ。
    /// 数え済みの位置を下げて、次の撃破が正しく+1されるようにする</summary>
    public static void OnSessionKillRemoved(int sessionKillCount)
    {
        if (s_syncedSessionKills > sessionKillCount) s_syncedSessionKills = sessionKillCount;
    }

    /// <summary>エネミーを撃破した（EnemyStats.Die。SessionStats.AddEnemyKillの直後に呼ぶ）。
    /// ・Kills：Result画面の撃破数（SessionStats.EnemyKillCount）が増えた分だけ足す。
    ///   ボスが呼び出した子分は撃破数が増えないので足されない。スサノオ（MissionExcludeKill）は足さずに数え済みにする
    /// ・大きさの区分ごとの撃破数</summary>
    public static void OnEnemyKilled(EnemyStats stats)
    {
        int sessionKills = SessionStats.EnemyKillCount;
        bool exclude = stats != null && stats.MissionExcludeKill;
        if (!CanCount || exclude)
        {
            s_syncedSessionKills = Mathf.Max(s_syncedSessionKills, sessionKills);
            return;
        }
        int diff = sessionKills - s_syncedSessionKills;
        if (diff > 0)
        {
            s_syncedSessionKills = sessionKills;
            Instance.AddInternal(MissionStat.Kills, diff);
        }

        if (stats == null) return;
        switch (stats.MissionSizeClass)
        {
            case EnemySizeClass.Small:  Instance.AddInternal(MissionStat.KillsSmall, 1); break;
            case EnemySizeClass.Medium: Instance.AddInternal(MissionStat.KillsMedium, 1); break;
            case EnemySizeClass.Boss:   Instance.AddInternal(MissionStat.KillsBoss, 1); break;
        }
    }

    /// <summary>シールドを壊した（EnemyShield）</summary>
    public static void OnShieldBroken(EnemyStats stats)
    {
        if (!CanCount || stats == null) return;
        switch (stats.MissionSizeClass)
        {
            case EnemySizeClass.Small:  Instance.AddInternal(MissionStat.ShieldBreaksSmall, 1); break;
            case EnemySizeClass.Medium: Instance.AddInternal(MissionStat.ShieldBreaksMedium, 1); break;
            case EnemySizeClass.Boss:   Instance.AddInternal(MissionStat.ShieldBreaksBoss, 1); break;
        }
    }

    public static void OnBlockDestroyed() { if (CanCount) Instance.AddInternal(MissionStat.BlocksDestroyed, 1); }
    public static void OnItemCollected() { if (CanCount) Instance.AddInternal(MissionStat.ItemsCollected, 1); }
    public static void OnSplitShotCollected() { if (CanCount) Instance.AddInternal(MissionStat.SplitShotCollected, 1); } // スプリットショットを取った回数（2026/11/11）
    public static void OnBlastShotCollected() { if (CanCount) Instance.AddInternal(MissionStat.BlastShotCollected, 1); } // ブラストショットを取った回数（2026/11/11）
    public static void OnSpiralShotCollected() { if (CanCount) Instance.AddInternal(MissionStat.SpiralShotCollected, 1); } // スパイラルショットを取った回数（2026/11/11）
    public static void OnMultiBoundCollected() { if (CanCount) Instance.AddInternal(MissionStat.MultiBoundCollected, 1); } // マルチバウンドを取った回数（2026/11/11）
    public static void OnGoldBoltCollected() { if (CanCount) Instance.AddInternal(MissionStat.GoldBoltCollected, 1); } // アクセラレーターを取った回数（2026/11/10）
    public static void OnGoldenShoesCollected() { if (CanCount) Instance.AddInternal(MissionStat.GoldenShoesCollected, 1); } // ゴールドシューズを取った回数（2026/11/10）
    public static void OnSoulRescued() { if (CanCount) Instance.AddInternal(MissionStat.SoulsRescued, 1); }
    public static void OnSlowMotionStarted() { if (CanCount) Instance.AddInternal(MissionStat.SlowMotionUses, 1); }
    public static void OnSkillSelected() { if (CanCount) Instance.AddInternal(MissionStat.SkillsSelected, 1); }
    public static void OnGoldEarned(int amount) { if (CanCount && amount > 0) Instance.AddInternal(MissionStat.GoldEarned, amount); }

    /// <summary>ダンサーが被弾した（PixelDancerController。実際にHPが減った時だけ）</summary>
    public static void OnDancerHit() { s_dancerHitThisStage = true; }

    /// <summary>Areaをクリアした（EnemySpawnerの最終Stageクリア保存の直後）</summary>
    public static void OnAreaCleared(int areaNumber)
    {
        if (!CanCount) return;
        if (areaNumber == 10) Instance.SetAtLeast(MissionStat.Area10Clear, 0, 1);
        if (areaNumber == 9 && SessionStats.DownCount == 0) Instance.SetAtLeast(MissionStat.Area9ClearNoDown, 0, 1);
        if (areaNumber >= 1)
        {
            if (SessionStats.DownCount == 0) Instance.SetAtLeast(MissionStat.AreaClearNoDown, areaNumber, 1);
            Instance.SetAtLeast(MissionStat.AreaFirstClear, areaNumber, 1); // Area2/5/8の虹の雫（ミッション画面で受け取る）
        }
        Instance.SaveIfDirty();
    }

    /// <summary>クリア時のランクが決まった（ResultScreenUI）</summary>
    public static void OnAreaRank(int areaNumber, string rank)
    {
        if (!CanCount) return;
        if (rank == "S" && areaNumber >= 1) Instance.SetAtLeast(MissionStat.RankS, areaNumber, 1);
        Instance.SaveIfDirty();
    }

    /// <summary>Result画面を出した時に呼ぶ（ゲームオーバー時も）。ここで保存する</summary>
    /// <summary>Result画面が出た時（MissionToastUIが残りの通知を捨てる）</summary>
    public static event Action OnResultShownEvent;
    public static void OnResultShown() { Instance?.SaveIfDirty(); OnResultShownEvent?.Invoke(); }

    private void HandleStageCleared(int stageIndex)
    {
        if (CanCount)
        {
            if (!s_whiteOverheatThisStage) AddInternal(MissionStat.StageClearNoWhiteOverheat, 1);
            if (!s_redOverheatThisStage) AddInternal(MissionStat.StageClearNoRedOverheat, 1);
            if (!s_dancerHitThisStage) AddInternal(MissionStat.StageClearNoDancerHit, 1);
        }
        ResetStageFlags();
        SaveIfDirty();
        ApplyStageClearHeal(stageIndex);
    }

    /// <summary>
    /// ミッション報酬のパッシブ：ステージクリア時の回復+1/+2（ダンサーとフロアの両方）。
    /// Area1〜9はStage1・Stage2のクリア時だけ（Stage3クリア＝Areaクリアでは回復しない）。
    /// Area10はStage1・Stage2・Stage3のクリア時（Final Stageの前まで）。Tutorialでは効かない（MissionPassives側で0）
    /// </summary>
    private static void ApplyStageClearHeal(int stageIndex)
    {
        int heal = MissionPassives.GetStageClearHeal();
        if (heal <= 0) return;
        int area = GameSession.SelectedArea != null ? GameSession.SelectedArea.areaNumber : 0;
        int lastHealStageIndex = area == 10 ? 2 : 1; // 0始まり：Area10はStage3(=2)まで、それ以外はStage2(=1)まで
        if (area <= 0 || stageIndex < 0 || stageIndex > lastHealStageIndex) return;

        if (Instance != null) Instance.StartCoroutine(StageClearHealRoutine(area, stageIndex, heal));
    }

    /// <summary>少し待ってから回復し、セルフヒールと同じ演出と「+N」の表示を出す。HPが満タンで回復しなかった方は演出を出さない</summary>
    private static System.Collections.IEnumerator StageClearHealRoutine(int area, int stageIndex, int heal)
    {
        var v = Instance != null && Instance.catalog != null ? Instance.catalog.passiveValues : null;
        float delay = v != null ? v.stageClearHealDelay : 0f;
        if (delay > 0f) yield return new WaitForSecondsRealtime(delay);

        var fx = BlockItemManager.Instance;
        var dancer = FindFirstObjectByType<PixelDancerController>();
        if (dancer != null && dancer.CurrentHP < dancer.MaxHP)
        {
            int before = dancer.CurrentHP;
            dancer.Heal(heal);
            if (fx != null) fx.PlayDancerHealFx(dancer.CurrentHP - before); // 回復エフェクト＋「ハート +N」
            else if (dancer.CurrentHP > before) dancer.PlayHealEffect();
        }
        var floor = FindFirstObjectByType<FloorHealth>();
        if (floor != null && floor.CurrentHP < floor.MaxHP)
        {
            int before = floor.CurrentHP;
            floor.Heal(heal);
            if (fx != null) fx.PlayFloorHealFx(floor.CurrentHP - before); // 回復エフェクト＋「床アイコン +N」
            else if (floor.CurrentHP > before) floor.PlayHealEffect();
        }
        Debug.Log($"[Mission] ステージクリア時の回復：Area{area} Stage{stageIndex + 1} ダンサーとフロアを最大+{heal}");
    }

    private static void ResetStageFlags()
    {
        s_whiteOverheatThisStage = false;
        s_redOverheatThisStage = false;
        s_dancerHitThisStage = false;
    }

    // =====================================================================
    // 進捗の参照（ミッション画面・報酬で使う）
    // =====================================================================

    public MissionCatalog Catalog => catalog;

    public int GetProgress(MissionDefinition def) => def == null ? 0 : GetCounter(def.stat, def.areaNumber);
    public bool IsAchieved(MissionDefinition def) => def != null && GetProgress(def) >= def.target;
    public bool IsClaimed(string id) => !string.IsNullOrEmpty(id) && claimedIds.Contains(id);

    /// <summary>報酬を受け取った瞬間に呼ばれる（ミッション画面の表示更新・バッジ更新用）</summary>
    public static event Action<MissionDefinition> OnMissionClaimed;

    /// <summary>ミッション画面に出すか。
    /// ・2段目（requiresClaimedIdあり）は、1段目を受け取るまで出さない
    /// ・1段目は、受け取った後に2段目があれば2段目に入れ替わるので出さない</summary>
    public bool IsVisible(MissionDefinition def)
    {
        if (def == null) return false;
        if (!string.IsNullOrEmpty(def.requiresClaimedId) && !IsClaimed(def.requiresClaimedId)) return false;
        if (IsClaimed(def.id) && catalog != null)
        {
            foreach (var other in catalog.missions)
                if (other != null && other.requiresClaimedId == def.id) return false;
        }
        return true;
    }

    /// <summary>受け取れる状態か（出ている・達成済み・まだ受け取っていない）</summary>
    public bool IsClaimable(MissionDefinition def) => def != null && IsVisible(def) && IsAchieved(def) && !IsClaimed(def.id);

    /// <summary>受け取れるミッションの数（AreaSelectのバッジ用）</summary>
    public int GetClaimableCount()
    {
        if (catalog == null) return 0;
        int n = 0;
        foreach (var m in catalog.missions) if (IsClaimable(m)) n++;
        return n;
    }

    /// <summary>受け取ったミッションの数（ミッション画面の「達成 ○ / 50」用）</summary>
    public int GetClaimedCount()
    {
        if (catalog == null) return 0;
        int n = 0;
        foreach (var m in catalog.missions) if (m != null && IsClaimed(m.id)) n++;
        return n;
    }

    /// <summary>
    /// 報酬を受け取る。ゴールドは持っているゴールドに足す。パッシブは受け取り済みとして記録する（効果はゲーム開始時に反映）。
    /// 受け取れない状態なら何もしないでfalseを返す。
    /// </summary>
    public bool TryClaim(MissionDefinition def)
    {
        if (!IsClaimable(def)) return false;
        if (def.rewardType == MissionRewardType.Gold)
        {
            if (GoldManager.Instance == null)
            {
                Debug.LogError($"[Mission] GoldManagerが無いため {def.id} のゴールドを渡せません（受け取りを中止）");
                return false;
            }
            GoldManager.Instance.AddPersistentGold(def.goldAmount);
        }
        else if (def.rewardType == MissionRewardType.RainbowDrop)
        {
            InfiniteStoneManager.AddPersistent(def.rainbowDropAmount); // 所持数の表示はInfiniteStoneManagerのOnCountChangedで更新される
        }
        else if (def.rewardType == MissionRewardType.FoodTicket)
        {
            FoodTickets.AddOwned(def.foodTicketAmount); // お食事券はすぐ持ち物に入れる（2026/11/11）
        }
        claimedIds.Add(def.id);
        MissionPassives.Invalidate();
        Save();
        string rewardLog = def.rewardType == MissionRewardType.Gold ? def.goldAmount + "G"
            : def.rewardType == MissionRewardType.RainbowDrop ? "虹の雫×" + def.rainbowDropAmount
            : def.rewardType == MissionRewardType.FoodTicket ? "お食事券×" + def.foodTicketAmount
            : def.rewardType == MissionRewardType.Notice ? "お知らせのみ" : def.rewardLabel;
        Debug.Log($"[Mission] 受け取り：{def.id}「{def.displayName}」報酬={rewardLog}");
        OnMissionClaimed?.Invoke(def);
        return true;
    }

    public int GetCounter(MissionStat stat, int areaNumber)
    {
        counters.TryGetValue(KeyOf(stat, areaNumber), out int v);
        return v;
    }

    /// <summary>Areaごとに数える項目か</summary>
    private static bool IsPerArea(MissionStat stat)
        => stat == MissionStat.RankS || stat == MissionStat.AreaClearNoDown || stat == MissionStat.AreaFirstClear;

    private static string KeyOf(MissionStat stat, int areaNumber)
        => (IsPerArea(stat) && areaNumber > 0) ? $"{stat}_A{areaNumber}" : stat.ToString();

    // =====================================================================
    // 内部処理
    // =====================================================================

    private void AddInternal(MissionStat stat, int amount)
    {
        if (amount <= 0) return;
        string key = KeyOf(stat, 0);
        counters.TryGetValue(key, out int before);
        long sum = (long)before + amount;
        int after = sum > int.MaxValue ? int.MaxValue : (int)sum;
        counters[key] = after;
        dirty = true;
        CheckNewlyAchieved(stat, 0, before, after);
    }

    private void SetAtLeast(MissionStat stat, int areaNumber, int value)
    {
        string key = KeyOf(stat, areaNumber);
        counters.TryGetValue(key, out int before);
        if (before >= value) return;
        counters[key] = value;
        dirty = true;
        CheckNewlyAchieved(stat, areaNumber, before, value);
    }

    // 項目ごとのミッション一覧（反射が大量に起きても50個全部を毎回調べないためのキャッシュ）
    private Dictionary<MissionStat, List<MissionDefinition>> missionsByStat;

    private void CheckNewlyAchieved(MissionStat stat, int areaNumber, int before, int after)
    {
        if (catalog == null) return;
        if (missionsByStat == null)
        {
            missionsByStat = new Dictionary<MissionStat, List<MissionDefinition>>();
            foreach (var def in catalog.missions)
            {
                if (def == null) continue;
                if (!missionsByStat.TryGetValue(def.stat, out var l)) missionsByStat[def.stat] = l = new List<MissionDefinition>();
                l.Add(def);
            }
        }
        if (!missionsByStat.TryGetValue(stat, out var list)) return;
        for (int i = 0; i < list.Count; i++)
        {
            var m = list[i];
            if (m == null) continue;
            if (IsPerArea(stat) && m.areaNumber != areaNumber) continue;
            if (before < m.target && after >= m.target)
            {
                if (!NewlyAchievedThisRun.Contains(m.id)) NewlyAchievedThisRun.Add(m.id);
                Debug.Log($"[Mission] 達成：{m.id}「{m.displayName}」（{after}/{m.target}）");
                // お知らせだけのミッション（解放・スロット+1）は、中身がクリアした時に自動で行われるので、受け取りボタンを押させず受け取り済みにする（2026/11/11）
                if (m.rewardType == MissionRewardType.Notice && claimedIds.Add(m.id)) dirty = true;
                OnMissionAchieved?.Invoke(m);
            }
        }
    }

    /// <summary>
    /// 以前はArea2/5/8の初回クリアで虹の雫を直接渡していた。その頃にクリア済みのAreaは、
    /// 虹の雫のミッションを「受け取り済み」にして、二重にもらわないようにする（1回だけ。ProgressManagerが読み込まれてから）
    /// </summary>
    private const string NoticeMigratedKey = "Mission_NoticeMigrated_v2"; // v2：Area2・5・8のお知らせを別のミッションに分けた時に、もう1回だけ行う（2026/11/11）

    /// <summary>
    /// お知らせのミッション（エリアのクリア・2026/11/11追加）は、それより前にクリア済みのAreaの分を「受け取り済み」にする（1回だけ）。
    /// お知らせだけで何も増えないので、昔クリアしたAreaの分が「受け取れる」にたまらないようにする
    /// </summary>
    private void MigrateNoticeMissions()
    {
        if (PlayerPrefs.GetInt(NoticeMigratedKey, 0) == 1) return;
        var pm = Game.Progress.ProgressManager.Instance;
        if (pm == null || catalog == null) return;
        bool has = false;
        foreach (var m in catalog.missions)
        {
            if (m == null || m.rewardType != MissionRewardType.Notice || m.stat != MissionStat.AreaFirstClear || m.areaNumber <= 0) continue;
            has = true;
            string areaId = $"Area_{m.areaNumber:D2}";
            var stages = Game.Progress.AreaDB.Instance != null ? Game.Progress.AreaDB.Instance.GetStageNumbers(areaId) : null;
            int finalStage = (stages != null && stages.Length > 0) ? stages[stages.Length - 1] : 3;
            if (!pm.IsStageCleared(areaId, finalStage)) continue;
            counters[KeyOf(MissionStat.AreaFirstClear, m.areaNumber)] = 1;
            claimedIds.Add(m.id);
        }
        if (!has) return; // 一覧にまだお知らせのミッションが無い間は、まだ済ませない
        PlayerPrefs.SetInt(NoticeMigratedKey, 1);
        Save();
        Debug.Log("[Mission] 以前にクリア済みのAreaの「エリアをクリアする」（お知らせ）を受け取り済みにしました");
    }

    /// <summary>達成済みで受け取っていないお知らせだけのミッションを、受け取り済みにする（受け取りボタンを押させない）（2026/11/11）</summary>
    private void AutoClaimAchievedNotices()
    {
        if (catalog == null) return;
        bool changed = false;
        foreach (var m in catalog.missions)
        {
            if (m == null || m.rewardType != MissionRewardType.Notice || claimedIds.Contains(m.id)) continue;
            if (GetCounter(m.stat, m.areaNumber) < m.target) continue;
            claimedIds.Add(m.id);
            changed = true;
        }
        if (changed) Save();
    }

    private void MigrateRainbowDrops()
    {
        var pm = Game.Progress.ProgressManager.Instance;
        if (pm == null || catalog == null) return;
        bool hasRainbowMission = false;
        var log = new System.Text.StringBuilder();
        foreach (var m in catalog.missions)
        {
            if (m == null || m.rewardType != MissionRewardType.RainbowDrop || m.stat != MissionStat.AreaFirstClear || m.areaNumber <= 0) continue;
            hasRainbowMission = true;
            string areaId = $"Area_{m.areaNumber:D2}";
            var stages = Game.Progress.AreaDB.Instance != null ? Game.Progress.AreaDB.Instance.GetStageNumbers(areaId) : null;
            int finalStage = (stages != null && stages.Length > 0) ? stages[stages.Length - 1] : 3;
            if (!pm.IsStageCleared(areaId, finalStage)) continue;
            counters[KeyOf(MissionStat.AreaFirstClear, m.areaNumber)] = 1;
            claimedIds.Add(m.id);
            log.Append($" {m.id}(Area{m.areaNumber})");
        }
        if (!hasRainbowMission) return; // 一覧が古い（虹の雫のミッションが無い）間は、まだ済ませない
        rainbowDropMigrated = true;
        Save();
        if (log.Length > 0) Debug.Log($"[Mission] 以前にクリア済みのAreaの虹の雫のミッションを受け取り済みにしました：{log}");
    }

    private void SaveIfDirty()
    {
        if (!dirty) return;
        Save();
    }

    public void Save()
    {
        try
        {
            var data = new SaveData();
            foreach (var kv in counters) { data.keys.Add(kv.Key); data.values.Add(kv.Value); }
            data.claimedIds.AddRange(claimedIds);
            data.rainbowDropMigrated = rainbowDropMigrated;
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
            dirty = false;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Mission] Save failed: {e}");
        }
    }

    private void Load()
    {
        counters.Clear();
        claimedIds.Clear();
        rainbowDropMigrated = false;
        MissionPassives.Invalidate();
        try
        {
            if (!PlayerPrefs.HasKey(SaveKey)) return;
            var data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey));
            if (data == null) return;
            rainbowDropMigrated = data.rainbowDropMigrated;
            int n = Mathf.Min(data.keys?.Count ?? 0, data.values?.Count ?? 0);
            for (int i = 0; i < n; i++) counters[data.keys[i]] = data.values[i];
            if (data.claimedIds != null) foreach (var id in data.claimedIds) claimedIds.Add(id);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Mission] Load failed: {e}");
        }
    }

    /// <summary>
    /// 確認用：パッシブのミッションを全部（2段目まで）受け取り済みにする。ゴールドの報酬は受け取らない（所持ゴールドを変えないため）。
    /// Play中でも、Play前でも使える（Play前はセーブを直接書き換える）
    /// </summary>
    public static int DebugClaimAllPassives(MissionCatalog cat)
    {
        if (cat == null) return 0;
        var ids = new List<string>();
        foreach (var m in cat.missions)
            if (m != null && m.rewardType == MissionRewardType.Passive && !string.IsNullOrEmpty(m.id)) ids.Add(m.id);

        if (Instance != null)
        {
            foreach (var id in ids) Instance.claimedIds.Add(id);
            Instance.Save();
        }
        else
        {
            SaveData data = null;
            try { if (PlayerPrefs.HasKey(SaveKey)) data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey)); } catch { data = null; }
            if (data == null) data = new SaveData();
            foreach (var id in ids) if (!data.claimedIds.Contains(id)) data.claimedIds.Add(id);
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
        }
        MissionPassives.Invalidate();
        return ids.Count;
    }

    /// <summary>セーブデータを全部消す（ProgressManager.ResetAllから呼ぶ）</summary>
    public static void ResetPersistent()
    {
        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();
        NewlyAchievedThisRun.Clear();
        if (Instance != null)
        {
            Instance.counters.Clear();
            Instance.claimedIds.Clear();
            Instance.rainbowDropMigrated = false; // 次のシーン読み込みで、クリア済みのAreaを見直す
            MissionPassives.Invalidate();
            Instance.dirty = false;
        }
    }

    /// <summary>確認用：今の進捗と、各ミッションの達成状況をログに出す</summary>
    public void LogAll()
    {
        var sb = new System.Text.StringBuilder("[Mission] 進捗一覧\n");
        foreach (var kv in counters) sb.AppendLine($"  {kv.Key} = {kv.Value}");
        if (catalog != null)
        {
            sb.AppendLine("  --- ミッション ---");
            foreach (var m in catalog.missions)
            {
                if (m == null) continue;
                string state = IsClaimed(m.id) ? "受取済" : IsAchieved(m) ? "達成" : "進行中";
                sb.AppendLine($"  {m.id} {state} {Mathf.Min(GetProgress(m), m.target)}/{m.target} 「{m.displayName}」");
            }
        }
        Debug.Log(sb.ToString());
    }
}
