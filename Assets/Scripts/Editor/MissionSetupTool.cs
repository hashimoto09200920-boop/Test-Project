using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using Game.Skills;

/// <summary>
/// ミッション機能のセットアップ用メニュー（2026/11/07）
/// 1. ミッション一覧（80個）の設定アセットを作る
/// 2. 各EnemyDataに大きさの区分（小型/中型/ボス）を自動で入れる
/// 3. Play中の進捗をログに出す／4. 進捗を消す（確認用）
/// </summary>
public static class MissionSetupTool
{
    private const string CatalogAssetPath = "Assets/Resources/GameData/MissionCatalog.asset";
    private const string SkillFolder = "Assets/Resources/GameData/Skills/";
    private const string AreaConfigFolder = "Assets/Data/AreaConfigs/";
    private const string EnemyDataFolder = "Assets/GameData/Enemies/";

    // =====================================================================
    // 1. ミッション一覧を作成
    // =====================================================================

    [MenuItem("Tools/ミッション/1. ミッション一覧を作成（89個）")]
    private static void CreateCatalog()
    {
        var existing = AssetDatabase.LoadAssetAtPath<MissionCatalog>(CatalogAssetPath);
        if (existing != null &&
            !EditorUtility.DisplayDialog("ミッション一覧を作成",
                $"{CatalogAssetPath} は既にあります。\n中身を決定済みの89個で上書きします（Inspectorで変えた回数・金額は元に戻ります）。続けますか？",
                "上書きする", "キャンセル"))
            return;

        var catalog = existing != null ? existing : ScriptableObject.CreateInstance<MissionCatalog>();
        catalog.missions = BuildDefaultMissions();

        // スキル引き直しの回数の表を5段目まで伸ばす（Inspectorで変えた1・2段目の値はそのまま残す）
        var pv = catalog.passiveValues ?? (catalog.passiveValues = new PassiveValues());
        if (pv.skillReroll == null || pv.skillReroll.Length < 5)
        {
            var arr = new[] { 1, 2, 3, 4, 5 };
            for (int i = 0; pv.skillReroll != null && i < pv.skillReroll.Length; i++) arr[i] = pv.skillReroll[i];
            pv.skillReroll = arr;
        }

        if (existing == null) AssetDatabase.CreateAsset(catalog, CatalogAssetPath);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        Selection.activeObject = catalog;
        EditorGUIUtility.PingObject(catalog);
        Debug.Log($"[MissionSetupTool] {CatalogAssetPath} に {catalog.missions.Count} 個のミッションを作成しました");
    }

    private static SkillDefinition LoadSkill(string fileName)
    {
        var s = AssetDatabase.LoadAssetAtPath<SkillDefinition>(SkillFolder + fileName + ".asset");
        if (s == null) Debug.LogError($"[MissionSetupTool] スキルが見つかりません：{SkillFolder}{fileName}.asset");
        return s;
    }

    private static MissionDefinition Passive(string id, string name, string nameEn, MissionStat stat, int target,
        MissionPassiveType type, int tier, string label, string labelEn, string requires = "", SkillDefinition skill = null)
        => new MissionDefinition
        {
            id = id, displayName = name, displayNameEn = nameEn, stat = stat, target = target,
            requiresClaimedId = requires, rewardType = MissionRewardType.Passive,
            passiveType = type, passiveTier = tier, passiveSkill = skill,
            rewardLabel = label, rewardLabelEn = labelEn,
        };

    private static MissionDefinition Gold(string id, string name, string nameEn, MissionStat stat, int target, int gold, int area = 0)
        => new MissionDefinition
        {
            id = id, displayName = name, displayNameEn = nameEn, stat = stat, areaNumber = area, target = target,
            rewardType = MissionRewardType.Gold, goldAmount = gold,
        };

    private static MissionDefinition RainbowDrop(string id, string name, string nameEn, MissionStat stat, int target, int amount, int area)
        => new MissionDefinition
        {
            id = id, displayName = name, displayNameEn = nameEn, stat = stat, areaNumber = area, target = target,
            rewardType = MissionRewardType.RainbowDrop, rainbowDropAmount = amount,
        };

    /// <summary>名前にArea名が入っているミッションに、そのAreaを記録する（解放前は名前のArea名を「？」で表示するため）</summary>
    private static MissionDefinition NameArea(MissionDefinition def, int area)
    {
        def.nameAreaNumber = area;
        def.nameAreaTitle = AreaTitle(area);
        return def;
    }

    /// <summary>ミッション名に出すArea名（AreaConfigのArea Nameを大文字にしたもの。例：SOUL TOWN）</summary>
    private static string AreaTitle(int area)
    {
        var config = AssetDatabase.LoadAssetAtPath<AreaConfig>($"{AreaConfigFolder}Area{area}Config.asset");
        if (config == null || string.IsNullOrEmpty(config.areaName))
        {
            Debug.LogError($"[MissionSetupTool] Area{area}Config のArea Nameが読めません（ミッション名は「Area{area}」のままにします）");
            return $"Area{area}";
        }
        return config.areaName.Trim().ToUpperInvariant();
    }

    private static List<MissionDefinition> BuildDefaultMissions()
    {
        var A = MissionPassiveType.CategoryASkill;
        var list = new List<MissionDefinition>
        {
            // ---- CategoryAのパッシブ（10個） ----
            Passive("M01", "白線で弾を300回反射する", "Reflect bullets 300 times with white lines", MissionStat.ReflectWhite, 300, A, 1, "ホワイトバッテリー+1", "White Battery +1", skill: LoadSkill("Skill_A1_LeftMaxCostUp")),
            Passive("M02", "赤線で弾を300回反射する", "Reflect bullets 300 times with red lines", MissionStat.ReflectRed, 300, A, 1, "レッドバッテリー+1", "Red Battery +1", skill: LoadSkill("Skill_A2_RedMaxCostUp")),
            Passive("M03", "白線のゲージを200回回復する", "Refill the white line gauge 200 times", MissionStat.GaugeFullWhite, 200, A, 1, "ホワイトリカバリー+1", "White Recovery +1", skill: LoadSkill("Skill_A3_LeftRecoveryUp")),
            Passive("M04", "赤線のゲージを200回回復する", "Refill the red line gauge 200 times", MissionStat.GaugeFullRed, 200, A, 1, "レッドリカバリー+1", "Red Recovery +1", skill: LoadSkill("Skill_A4_RedRecoveryUp")),
            Passive("M05", "白線で複数の弾を100回反射する", "Reflect multiple bullets with one white line 100 times", MissionStat.MultiReflectWhite, 100, A, 1, "ホワイトホールド+1", "White Hold +1", skill: LoadSkill("Skill_A5_LeftLifetimeUp")),
            Passive("M06", "赤線で複数の弾を100回反射する", "Reflect multiple bullets with one red line 100 times", MissionStat.MultiReflectRed, 100, A, 1, "レッドホールド+1", "Red Hold +1", skill: LoadSkill("Skill_A6_RedLifetimeUp")),
            Passive("M07", "線を100本引く", "Draw 100 lines", MissionStat.LinesDrawn, 100, A, 1, "マルチライン+1", "Multi Line +1", skill: LoadSkill("Skill_A7_MaxStrokesUp")),
            Passive("M08", "反射弾でエネミーを300体撃破する", "Defeat 300 enemies with reflected bullets", MissionStat.Kills, 300, A, 1, "リフレクトブースト+1", "Reflect Boost +1", skill: LoadSkill("Skill_A8_ReflectedBulletSpeedUp")),
            Passive("M09", "ブロックを100個壊す", "Destroy 100 blocks", MissionStat.BlocksDestroyed, 100, A, 1, "ブロックバスター+1", "Block Buster +1", skill: LoadSkill("Skill_A9_BlockDamageUp")),
            Passive("M10", "スローモーション中に線で円を100回作る", "Make 100 circles with lines during slow motion", MissionStat.SlowMotionCircles, 100, A, 1, "クロックハック+1", "Clock Hack +1", skill: LoadSkill("Skill_A10_SlowMotionEffectUp")),

            // ---- 単独のパッシブ（2個） ----
            Passive("M11", "アイテムを100個取る", "Collect 100 items", MissionStat.ItemsCollected, 100, MissionPassiveType.AutoItemCollect, 1, "アイテムマグネット", "Item Magnet"),
            Passive("M12", "ソウルを10体救出する", "Rescue 10 souls", MissionStat.SoulsRescued, 10, MissionPassiveType.AutoSoulRescue, 1, "ソウルリバイブ", "Soul Revive"),

            // ---- 2段階のパッシブ（24個） ----
            Passive("M13", "小型エネミーを200体撃破する", "Defeat 200 small enemies", MissionStat.KillsSmall, 200, MissionPassiveType.DamageSmall, 1, "小型エネミーへのダメージ+1", "Damage to small enemies +1"),
            Passive("M14", "小型エネミーを500体撃破する", "Defeat 500 small enemies", MissionStat.KillsSmall, 500, MissionPassiveType.DamageSmall, 2, "小型エネミーへのダメージ+2", "Damage to small enemies +2", "M13"),
            Passive("M15", "中型エネミーを150体撃破する", "Defeat 150 medium enemies", MissionStat.KillsMedium, 150, MissionPassiveType.DamageMedium, 1, "中型エネミーへのダメージ+1", "Damage to medium enemies +1"),
            Passive("M16", "中型エネミーを400体撃破する", "Defeat 400 medium enemies", MissionStat.KillsMedium, 400, MissionPassiveType.DamageMedium, 2, "中型エネミーへのダメージ+2", "Damage to medium enemies +2", "M15"),
            Passive("M17", "ボスを10体撃破する", "Defeat 10 bosses", MissionStat.KillsBoss, 10, MissionPassiveType.DamageBoss, 1, "ボスへのダメージ+1", "Damage to bosses +1"),
            Passive("M18", "ボスを30体撃破する", "Defeat 30 bosses", MissionStat.KillsBoss, 30, MissionPassiveType.DamageBoss, 2, "ボスへのダメージ+2", "Damage to bosses +2", "M17"),
            Passive("M19", "小型エネミーのシールドを100回壊す", "Break small enemies' shields 100 times", MissionStat.ShieldBreaksSmall, 100, MissionPassiveType.ShieldDamageSmall, 1, "小型エネミーへのシールドダメージ+1", "Small shield damage +1"),
            Passive("M20", "小型エネミーのシールドを300回壊す", "Break small enemies' shields 300 times", MissionStat.ShieldBreaksSmall, 300, MissionPassiveType.ShieldDamageSmall, 2, "小型エネミーへのシールドダメージ+2", "Small shield damage +2", "M19"),
            Passive("M21", "中型エネミーのシールドを50回壊す", "Break medium enemies' shields 50 times", MissionStat.ShieldBreaksMedium, 50, MissionPassiveType.ShieldDamageMedium, 1, "中型エネミーへのシールドダメージ+1", "Medium shield damage +1"),
            Passive("M22", "中型エネミーのシールドを150回壊す", "Break medium enemies' shields 150 times", MissionStat.ShieldBreaksMedium, 150, MissionPassiveType.ShieldDamageMedium, 2, "中型エネミーへのシールドダメージ+2", "Medium shield damage +2", "M21"),
            Passive("M23", "ボスのシールドを30回壊す", "Break bosses' shields 30 times", MissionStat.ShieldBreaksBoss, 30, MissionPassiveType.ShieldDamageBoss, 1, "ボスへのシールドダメージ+1", "Shield damage to bosses +1"),
            Passive("M24", "ボスのシールドを100回壊す", "Break bosses' shields 100 times", MissionStat.ShieldBreaksBoss, 100, MissionPassiveType.ShieldDamageBoss, 2, "ボスへのシールドダメージ+2", "Shield damage to bosses +2", "M23"),
            Passive("M25", "白線オーバーヒートなしでSTAGE10回クリア", "Clear 10 stages without overheating white lines", MissionStat.StageClearNoWhiteOverheat, 10, MissionPassiveType.WhiteOverheatShorten, 1, "白線のオーバーヒート時間10%短縮", "White OH time -10%"),
            Passive("M26", "白線オーバーヒートなしでSTAGE30回クリア", "Clear 30 stages without overheating white lines", MissionStat.StageClearNoWhiteOverheat, 30, MissionPassiveType.WhiteOverheatShorten, 2, "白線のオーバーヒート時間20%短縮", "White OH time -20%", "M25"),
            Passive("M27", "赤線オーバーヒートなしでSTAGE10回クリア", "Clear 10 stages without overheating red lines", MissionStat.StageClearNoRedOverheat, 10, MissionPassiveType.RedOverheatShorten, 1, "赤線のオーバーヒート時間10%短縮", "Red OH time -10%"),
            Passive("M28", "赤線オーバーヒートなしでSTAGE30回クリア", "Clear 30 stages without overheating red lines", MissionStat.StageClearNoRedOverheat, 30, MissionPassiveType.RedOverheatShorten, 2, "赤線のオーバーヒート時間20%短縮", "Red OH time -20%", "M27"),
            Passive("M29", "スローモーションを100回使う", "Use slow motion 100 times", MissionStat.SlowMotionUses, 100, MissionPassiveType.SlowMotionOverheatShorten, 1, "スローモーションのオーバーヒート時間10%短縮", "Slow motion overheat time -10%"),
            Passive("M30", "スローモーションを400回使う", "Use slow motion 400 times", MissionStat.SlowMotionUses, 400, MissionPassiveType.SlowMotionOverheatShorten, 2, "スローモーションのオーバーヒート時間20%短縮", "Slow motion overheat time -20%", "M29"),
            Passive("M31", "ゴールドを5,000獲得する", "Earn 5,000 gold", MissionStat.GoldEarned, 5000, MissionPassiveType.GoldGainUp, 1, "ゴールド獲得量+25%", "Gold gain +25%"),
            Passive("M32", "ゴールドを20,000獲得する", "Earn 20,000 gold", MissionStat.GoldEarned, 20000, MissionPassiveType.GoldGainUp, 2, "ゴールド獲得量+50%", "Gold gain +50%", "M31"),
            Passive("M33", "スキルカードを50回引く", "Draw skill cards 50 times", MissionStat.SkillsSelected, 50, MissionPassiveType.SkillReroll, 1, "スキル引き直し 1回", "Skill Reroll x1"),
            Passive("M34", "スキルカードを200回引く", "Draw skill cards 200 times", MissionStat.SkillsSelected, 200, MissionPassiveType.SkillReroll, 2, "スキル引き直し 2回", "Skill Reroll x2", "M33"),
            // スキル引き直しの3〜5段目（2026/11/08追加・案A：引く回数を増やしていく）。idはセーブに使うので末尾の番号
            Passive("M63", "スキルカードを400回引く", "Draw skill cards 400 times", MissionStat.SkillsSelected, 400, MissionPassiveType.SkillReroll, 3, "スキル引き直し 3回", "Skill Reroll x3", "M34"),
            Passive("M64", "スキルカードを700回引く", "Draw skill cards 700 times", MissionStat.SkillsSelected, 700, MissionPassiveType.SkillReroll, 4, "スキル引き直し 4回", "Skill Reroll x4", "M63"),
            Passive("M65", "スキルカードを1000回引く", "Draw skill cards 1000 times", MissionStat.SkillsSelected, 1000, MissionPassiveType.SkillReroll, 5, "スキル引き直し 5回", "Skill Reroll x5", "M64"),
            Passive("M35", "ダメージを受けずにSTAGEを5回クリアする", "Clear 5 stages without taking damage", MissionStat.StageClearNoDancerHit, 5, MissionPassiveType.StageClearHeal, 1, "ステージクリア時の回復+1", "Heal on stage clear +1"),
            Passive("M36", "ダメージを受けずにSTAGEを20回クリアする", "Clear 20 stages without taking damage", MissionStat.StageClearNoDancerHit, 20, MissionPassiveType.StageClearHeal, 2, "ステージクリア時の回復+2", "Heal on stage clear +2", "M35"),
        };

        // ★idはセーブの受け取り済み記録に使うので変えない。M51以降は2026/11/08に追加
        // ---- ゴールド：ランクS（10個）（2026/11/08 金額変更） ----
        int[] rankSGold = { 600, 800, 1200, 1400, 1700, 2000, 2500, 4000, 5000, 10000 };
        for (int area = 1; area <= 10; area++)
        {
            string a = AreaTitle(area);
            list.Add(NameArea(Gold($"M{36 + area:D2}", $"エリア「{a}」でランクS評価を獲得", $"Get rank S in area \"{a}\"", MissionStat.RankS, 1, rankSGold[area - 1], area), area));
        }

        // ---- ゴールド：その他（3個） ----
        list.Add(NameArea(Gold("M47", $"エリア「{AreaTitle(10)}」をクリアする", $"Clear area \"{AreaTitle(10)}\"", MissionStat.Area10Clear, 1, 6000), 10));
        list.Add(Gold("M48", "Just反射を1000回する", "Perform 1000 Just reflects", MissionStat.JustReflects, 1000, 1000));
        list.Add(Gold("M49", "1つの円で弾を5個以上同時に囲む", "Enclose 5 or more bullets in one circle", MissionStat.CircleEnclose5Bullets, 1, 500));

        // ---- ゴールド：ダウンせずにクリア（10個）。Area9は以前からのM50（進捗を残すため項目もArea9ClearNoDownのまま） ----
        int[] noDownGold = { 200, 300, 400, 500, 600, 700, 800, 900, 1000, 2000 };
        for (int area = 1; area <= 10; area++)
        {
            string a = AreaTitle(area);
            string id = area == 9 ? "M50" : area == 10 ? "M59" : $"M{50 + area:D2}"; // Area1〜8=M51〜M58、Area9=M50、Area10=M59
            var stat = area == 9 ? MissionStat.Area9ClearNoDown : MissionStat.AreaClearNoDown;
            list.Add(NameArea(Gold(id, $"ダウンせずにエリア「{a}」をクリアする", $"Clear area \"{a}\" without going down", stat, 1, noDownGold[area - 1], area == 9 ? 0 : area), area));
        }

        // ---- 虹の雫：Area2/5/8の初回クリア（3個）（以前はクリア時に直接渡していた） ----
        int[] dropAreas = { 2, 5, 8 };
        for (int i = 0; i < dropAreas.Length; i++)
        {
            int area = dropAreas[i];
            string a = AreaTitle(area);
            list.Add((NameArea(RainbowDrop($"M{60 + i}", $"エリア「{a}」をクリアする", $"Clear area \"{a}\"", MissionStat.AreaFirstClear, 1, 1, area), area)));
        }

        // ---- ゴールドシューズの効果時間（2個）（2026/11/10） ----
        list.AddRange(GoldenShoesMissions());
        // ---- アクセラレーターの効果時間（2個）（2026/11/10） ----
        list.AddRange(GoldBoltMissions());
        // ---- マルチバウンドの効果時間（2個）（2026/11/11） ----
        list.AddRange(MultiBoundMissions());
        // ---- スパイラルショットのヒット回数（2個）（2026/11/11） ----
        list.AddRange(SpiralShotMissions());
        // ---- ブラストショット（2個）（2026/11/11） ----
        list.AddRange(BlastShotMissions());
        // ---- スプリットショット（2個）（2026/11/11） ----
        list.AddRange(SplitShotMissions());
        // ---- お食事券（3個）（2026/11/11） ----
        list.AddRange(FoodTicketMissions());
        // ---- エリアのクリア（お知らせ：次のエリアの解放・ジェムの装備スロット+1）（6個）（2026/11/11）。Area2・5・8は虹の雫のミッションにお知らせを付けた ----
        list.AddRange(AreaClearNoticeMissions());
        return list;
    }

    /// <summary>
    /// お知らせのミッションを作る Area の番号と id（10は6000ゴールドのミッションのまま）。
    /// ★2026/11/11 Area2・5・8も虹の雫のミッションとは別のミッションに分けた（M87〜M89）。id はセーブに使うので変えない
    /// </summary>
    private static readonly int[] NoticeAreas = { 1, 3, 4, 6, 7, 9, 2, 5, 8 };
    private static readonly string[] NoticeIds = { "M81", "M82", "M83", "M84", "M85", "M86", "M87", "M88", "M89" };

    private static List<MissionDefinition> AreaClearNoticeMissions()
    {
        var list = new List<MissionDefinition>();
        for (int i = 0; i < NoticeAreas.Length; i++)
        {
            int area = NoticeAreas[i];
            string a = AreaTitle(area);
            var def = new MissionDefinition
            {
                id = NoticeIds[i], displayName = $"エリア「{a}」をクリアする", displayNameEn = $"Clear area \"{a}\"",
                stat = MissionStat.AreaFirstClear, areaNumber = area, target = 1, rewardType = MissionRewardType.Notice,
            };
            list.Add(WithAreaClearNotice(NameArea(def, area), area));
        }
        return list;
    }

    /// <summary>
    /// エリアをクリアした時のお知らせ（次のエリアの解放・ジェムの装備スロット+1）を付ける。
    /// ★Area9をクリアしても Area10 はすぐには解放されない（Area1〜9すべてでランクA以上が必要）ので、Area9 はスロット+1だけ
    /// </summary>
    private static MissionDefinition WithAreaClearNotice(MissionDefinition def, int area)
    {
        var ja = new List<string>();
        var en = new List<string>();
        if (area <= 8)
        {
            string next = AreaTitle(area + 1);
            ja.Add($"次のエリア「{next}」をアンロック");
            en.Add($"Unlocked next area \"{next}\"");
        }
        ja.Add("ジェムの装備スロット+1");
        en.Add("Gem slot +1");
        def.noticeLines = ja.ToArray();
        def.noticeLinesEn = en.ToArray();
        return def;
    }

    /// <summary>
    /// 今のミッション一覧（Inspectorで変えた回数・金額はそのまま）の名前・報酬名・お知らせを最新にして、足りないミッションを足す（2026/11/11）。
    /// 文言の変更：STAGE・ダメージを受けずに・エリア「〇〇」・アイテム「〇〇」・ランクS評価を獲得・線で円を など
    /// </summary>
    [MenuItem("Tools/ミッション/23. ミッションの文言を最新にする・エリアクリアのお知らせを追加（回数・金額はそのまま）")]
    private static void UpdateTextsAndAddNotices()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<MissionCatalog>(CatalogAssetPath);
        if (catalog == null) { EditorUtility.DisplayDialog("ミッションの文言", $"{CatalogAssetPath} がありません。先に「1. ミッション一覧を作成」を実行してください。", "OK"); return; }
        Undo.RecordObject(catalog, "ミッションの文言を最新にする");
        int updated = 0, added = 0;
        foreach (var d in BuildDefaultMissions())
        {
            var m = catalog.Find(d.id);
            if (m == null) { catalog.missions.Add(d); added++; continue; }
            m.displayName = d.displayName;
            m.displayNameEn = d.displayNameEn;
            m.nameAreaTitle = d.nameAreaTitle;
            m.nameAreaNumber = d.nameAreaNumber;
            m.rewardLabel = d.rewardLabel;
            m.rewardLabelEn = d.rewardLabelEn;
            m.noticeLines = d.noticeLines;
            m.noticeLinesEn = d.noticeLinesEn;
            updated++;
        }
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        string msg = $"{updated} 個の文言を最新にしました・{added} 個追加しました（全部で {catalog.missions.Count} 個）。回数・金額は変えていません。";
        Debug.Log("[MissionSetupTool] " + msg);
        EditorUtility.DisplayDialog("ミッションの文言", msg, "OK");
    }

    private static MissionDefinition FoodTicketReward(string id, string name, string nameEn, int target, int amount, string requires = "")
        => new MissionDefinition
        {
            id = id, displayName = name, displayNameEn = nameEn, stat = MissionStat.FoodTicketCollected, target = target,
            requiresClaimedId = requires, rewardType = MissionRewardType.FoodTicket, foodTicketAmount = amount,
        };

    /// <summary>お食事券の3段階のミッション（M78：3回→1枚、M79：10回→3枚、M80：30回→10枚）。報酬はお食事券</summary>
    private static List<MissionDefinition> FoodTicketMissions() => new List<MissionDefinition>
    {
        FoodTicketReward("M78", "アイテム「お食事券」を3回取る", "Collect item \"Food Ticket\" 3 times", 3, 1),
        FoodTicketReward("M79", "アイテム「お食事券」を10回取る", "Collect item \"Food Ticket\" 10 times", 10, 3, "M78"),
        FoodTicketReward("M80", "アイテム「お食事券」を30回取る", "Collect item \"Food Ticket\" 30 times", 30, 10, "M79"),
    };

    /// <summary>今のミッション一覧に、お食事券の3個だけを足す</summary>
    [MenuItem("Tools/ミッション/22. お食事券のミッションを追加（3個・今の一覧に足す）")]
    private static void AddFoodTicketMissions()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<MissionCatalog>(CatalogAssetPath);
        if (catalog == null) { EditorUtility.DisplayDialog("お食事券のミッション", $"{CatalogAssetPath} がありません。先に「1. ミッション一覧を作成」を実行してください。", "OK"); return; }
        Undo.RecordObject(catalog, "お食事券のミッションを追加");
        int added = 0;
        foreach (var def in FoodTicketMissions())
        {
            if (catalog.Find(def.id) != null) continue;
            catalog.missions.Add(def);
            added++;
        }
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        string msg = added > 0 ? $"{added} 個追加しました（全部で {catalog.missions.Count} 個）。" : $"もう入っています（全部で {catalog.missions.Count} 個）。";
        Debug.Log("[MissionSetupTool] お食事券のミッション：" + msg);
        EditorUtility.DisplayDialog("お食事券のミッション", msg, "OK");
    }

    /// <summary>スプリットショットの2段階のミッション（M76：30回→ダメージアップ、M77：100回→分裂数+1）。報酬名は細かい数値を出さない</summary>
    private static List<MissionDefinition> SplitShotMissions() => new List<MissionDefinition>
    {
        Passive("M76", "アイテム「スプリットショット」を30回取る", "Collect item \"Split Shot\" 30 times", MissionStat.SplitShotCollected, 30, MissionPassiveType.SplitShotPower, 1, "アイテム「スプリットショット」のダメージアップ", "Item \"Split Shot\" damage up"),
        Passive("M77", "アイテム「スプリットショット」を100回取る", "Collect item \"Split Shot\" 100 times", MissionStat.SplitShotCollected, 100, MissionPassiveType.SplitShotPower, 2, "アイテム「スプリットショット」の分裂数+1", "Item \"Split Shot\" splits +1", "M76"),
    };

    /// <summary>今のミッション一覧に、スプリットショットの2個だけを足す</summary>
    [MenuItem("Tools/ミッション/21. スプリットショットのミッションを追加（2個・今の一覧に足す）")]
    private static void AddSplitShotMissions()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<MissionCatalog>(CatalogAssetPath);
        if (catalog == null) { EditorUtility.DisplayDialog("アイテム「スプリットショット」のミッション", $"{CatalogAssetPath} がありません。先に「1. ミッション一覧を作成」を実行してください。", "OK"); return; }
        Undo.RecordObject(catalog, "アイテム「スプリットショット」のミッションを追加");
        int added = 0;
        foreach (var def in SplitShotMissions())
        {
            if (catalog.Find(def.id) != null) continue;
            catalog.missions.Add(def);
            added++;
        }
        var pv = catalog.passiveValues ?? (catalog.passiveValues = new PassiveValues());
        if (pv.splitShotDamageMul == null || pv.splitShotDamageMul.Length < 2) pv.splitShotDamageMul = new[] { 1.5f, 1.5f };
        if (pv.splitShotExtraSplits == null || pv.splitShotExtraSplits.Length < 2) pv.splitShotExtraSplits = new[] { 0, 1 };
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        string msg = added > 0 ? $"{added} 個追加しました（全部で {catalog.missions.Count} 個）。" : $"もう入っています（全部で {catalog.missions.Count} 個）。";
        Debug.Log("[MissionSetupTool] スプリットショットのミッション：" + msg);
        EditorUtility.DisplayDialog("アイテム「スプリットショット」のミッション", msg, "OK");
    }

    /// <summary>ブラストショットの2段階のミッション（M74：30回→ダメージアップ、M75：100回→爆発範囲アップ）。報酬名は細かい数値を出さない</summary>
    private static List<MissionDefinition> BlastShotMissions() => new List<MissionDefinition>
    {
        Passive("M74", "アイテム「ブラストショット」を30回取る", "Collect item \"Blast Shot\" 30 times", MissionStat.BlastShotCollected, 30, MissionPassiveType.BlastShotPower, 1, "アイテム「ブラストショット」のダメージアップ", "Item \"Blast Shot\" damage up"),
        Passive("M75", "アイテム「ブラストショット」を100回取る", "Collect item \"Blast Shot\" 100 times", MissionStat.BlastShotCollected, 100, MissionPassiveType.BlastShotPower, 2, "アイテム「ブラストショット」の爆発範囲アップ", "Item \"Blast Shot\" range up", "M74"),
    };

    /// <summary>今のミッション一覧に、ブラストショットの2個だけを足す</summary>
    [MenuItem("Tools/ミッション/20. ブラストショットのミッションを追加（2個・今の一覧に足す）")]
    private static void AddBlastShotMissions()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<MissionCatalog>(CatalogAssetPath);
        if (catalog == null) { EditorUtility.DisplayDialog("アイテム「ブラストショット」のミッション", $"{CatalogAssetPath} がありません。先に「1. ミッション一覧を作成」を実行してください。", "OK"); return; }
        Undo.RecordObject(catalog, "アイテム「ブラストショット」のミッションを追加");
        int added = 0;
        foreach (var def in BlastShotMissions())
        {
            if (catalog.Find(def.id) != null) continue;
            catalog.missions.Add(def);
            added++;
        }
        var pv = catalog.passiveValues ?? (catalog.passiveValues = new PassiveValues());
        if (pv.blastShotDamage == null || pv.blastShotDamage.Length < 2) pv.blastShotDamage = new[] { 20, 20 };
        if (pv.blastShotRadius == null || pv.blastShotRadius.Length < 2) pv.blastShotRadius = new[] { 0f, 4f };
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        string msg = added > 0 ? $"{added} 個追加しました（全部で {catalog.missions.Count} 個）。" : $"もう入っています（全部で {catalog.missions.Count} 個）。";
        Debug.Log("[MissionSetupTool] ブラストショットのミッション：" + msg);
        EditorUtility.DisplayDialog("アイテム「ブラストショット」のミッション", msg, "OK");
    }

    /// <summary>スパイラルショットの2段階のミッション（M72：30回→ヒット回数+4、M73：100回→+8）。報酬名は細かい数値を出さない（ユーザー指示）</summary>
    private static List<MissionDefinition> SpiralShotMissions() => new List<MissionDefinition>
    {
        Passive("M72", "アイテム「スパイラルショット」を30回取る", "Collect item \"Spiral Shot\" 30 times", MissionStat.SpiralShotCollected, 30, MissionPassiveType.SpiralShotHits, 1, "アイテム「スパイラルショット」のヒット回数+4", "Item \"Spiral Shot\" hits +4"),
        Passive("M73", "アイテム「スパイラルショット」を100回取る", "Collect item \"Spiral Shot\" 100 times", MissionStat.SpiralShotCollected, 100, MissionPassiveType.SpiralShotHits, 2, "アイテム「スパイラルショット」のヒット回数+8", "Item \"Spiral Shot\" hits +8", "M72"),
    };

    /// <summary>今のミッション一覧（Inspectorで変えた回数・金額はそのまま）に、スパイラルショットの2個だけを足す</summary>
    [MenuItem("Tools/ミッション/19. スパイラルショットのミッションを追加（2個・今の一覧に足す）")]
    private static void AddSpiralShotMissions()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<MissionCatalog>(CatalogAssetPath);
        if (catalog == null) { EditorUtility.DisplayDialog("アイテム「スパイラルショット」のミッション", $"{CatalogAssetPath} がありません。先に「1. ミッション一覧を作成」を実行してください。", "OK"); return; }
        Undo.RecordObject(catalog, "アイテム「スパイラルショット」のミッションを追加");
        int added = 0;
        foreach (var def in SpiralShotMissions())
        {
            if (catalog.Find(def.id) != null) continue;
            catalog.missions.Add(def);
            added++;
        }
        var pv = catalog.passiveValues ?? (catalog.passiveValues = new PassiveValues());
        if (pv.spiralShotHitBonus == null || pv.spiralShotHitBonus.Length < 2) pv.spiralShotHitBonus = new[] { 4, 8 };
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        string msg = added > 0 ? $"{added} 個追加しました（全部で {catalog.missions.Count} 個）。" : $"もう入っています（全部で {catalog.missions.Count} 個）。";
        Debug.Log("[MissionSetupTool] スパイラルショットのミッション：" + msg);
        EditorUtility.DisplayDialog("アイテム「スパイラルショット」のミッション", msg, "OK");
    }

    /// <summary>マルチバウンドの2段階のミッション（M70：30回→40秒、M71：100回→50秒）</summary>
    private static List<MissionDefinition> MultiBoundMissions() => new List<MissionDefinition>
    {
        Passive("M70", "アイテム「マルチバウンド」を30回取る", "Collect item \"Multi Bound\" 30 times", MissionStat.MultiBoundCollected, 30, MissionPassiveType.MultiBoundDuration, 1, "アイテム「マルチバウンド」の効果時間40秒", "Item \"Multi Bound\" time 40s"),
        Passive("M71", "アイテム「マルチバウンド」を100回取る", "Collect item \"Multi Bound\" 100 times", MissionStat.MultiBoundCollected, 100, MissionPassiveType.MultiBoundDuration, 2, "アイテム「マルチバウンド」の効果時間50秒", "Item \"Multi Bound\" time 50s", "M70"),
    };

    /// <summary>今のミッション一覧（Inspectorで変えた回数・金額はそのまま）に、マルチバウンドの2個だけを足す</summary>
    [MenuItem("Tools/ミッション/18. マルチバウンドのミッションを追加（2個・今の一覧に足す）")]
    private static void AddMultiBoundMissions()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<MissionCatalog>(CatalogAssetPath);
        if (catalog == null) { EditorUtility.DisplayDialog("アイテム「マルチバウンド」のミッション", $"{CatalogAssetPath} がありません。先に「1. ミッション一覧を作成」を実行してください。", "OK"); return; }
        Undo.RecordObject(catalog, "アイテム「マルチバウンド」のミッションを追加");
        int added = 0;
        foreach (var def in MultiBoundMissions())
        {
            if (catalog.Find(def.id) != null) continue;
            catalog.missions.Add(def);
            added++;
        }
        var pv = catalog.passiveValues ?? (catalog.passiveValues = new PassiveValues());
        if (pv.multiBoundDuration == null || pv.multiBoundDuration.Length < 2) pv.multiBoundDuration = new[] { 40f, 50f };
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        string msg = added > 0 ? $"{added} 個追加しました（全部で {catalog.missions.Count} 個）。" : $"もう入っています（全部で {catalog.missions.Count} 個）。";
        Debug.Log("[MissionSetupTool] マルチバウンドのミッション：" + msg);
        EditorUtility.DisplayDialog("アイテム「マルチバウンド」のミッション", msg, "OK");
    }

    /// <summary>アクセラレーターの2段階のミッション（M68：30回→40秒、M69：100回→50秒）</summary>
    private static List<MissionDefinition> GoldBoltMissions() => new List<MissionDefinition>
    {
        Passive("M68", "アイテム「アクセラレーター」を30回取る", "Collect item \"Accelerator\" 30 times", MissionStat.GoldBoltCollected, 30, MissionPassiveType.GoldBoltDuration, 1, "アイテム「アクセラレーター」の効果時間40秒", "Item \"Accelerator\" time 40s"),
        Passive("M69", "アイテム「アクセラレーター」を100回取る", "Collect item \"Accelerator\" 100 times", MissionStat.GoldBoltCollected, 100, MissionPassiveType.GoldBoltDuration, 2, "アイテム「アクセラレーター」の効果時間50秒", "Item \"Accelerator\" time 50s", "M68"),
    };

    /// <summary>今のミッション一覧（Inspectorで変えた回数・金額はそのまま）に、アクセラレーターの2個だけを足す</summary>
    [MenuItem("Tools/ミッション/17. アクセラレーターのミッションを追加・名前を更新（2個・今の一覧に足す）")]
    private static void AddGoldBoltMissions()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<MissionCatalog>(CatalogAssetPath);
        if (catalog == null) { EditorUtility.DisplayDialog("アイテム「アクセラレーター」のミッション", $"{CatalogAssetPath} がありません。先に「1. ミッション一覧を作成」を実行してください。", "OK"); return; }
        Undo.RecordObject(catalog, "アイテム「アクセラレーター」のミッションを追加");
        int added = 0, renamed = 0;
        foreach (var def in GoldBoltMissions())
        {
            var existing = catalog.Find(def.id);
            if (existing != null)
            {
                // ★2026/11/11 名前を「ゴールドボルト」から「アクセラレーター」に変えた。回数・報酬の数値はそのまま、名前だけ直す
                if (existing.displayName != def.displayName || existing.displayNameEn != def.displayNameEn || existing.rewardLabel != def.rewardLabel || existing.rewardLabelEn != def.rewardLabelEn)
                {
                    existing.displayName = def.displayName;
                    existing.displayNameEn = def.displayNameEn;
                    existing.rewardLabel = def.rewardLabel;
                    existing.rewardLabelEn = def.rewardLabelEn;
                    renamed++;
                }
                continue;
            }
            catalog.missions.Add(def);
            added++;
        }
        var pv = catalog.passiveValues ?? (catalog.passiveValues = new PassiveValues());
        if (pv.goldBoltDuration == null || pv.goldBoltDuration.Length < 2) pv.goldBoltDuration = new[] { 40f, 50f };
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        string msg = (added > 0 ? $"{added} 個追加しました" : "追加はありません") + (renamed > 0 ? $"・{renamed} 個の名前を直しました" : "") + $"（全部で {catalog.missions.Count} 個）。";
        Debug.Log("[MissionSetupTool] アクセラレーターのミッション：" + msg);
        EditorUtility.DisplayDialog("アイテム「アクセラレーター」のミッション", msg, "OK");
    }

    /// <summary>ゴールドシューズの2段階のミッション（M66：30回→40秒、M67：100回→50秒）</summary>
    private static List<MissionDefinition> GoldenShoesMissions() => new List<MissionDefinition>
    {
        Passive("M66", "アイテム「ゴールドシューズ」を30回取る", "Collect item \"Gold Shoes\" 30 times", MissionStat.GoldenShoesCollected, 30, MissionPassiveType.GoldenShoesDuration, 1, "アイテム「ゴールドシューズ」の効果時間40秒", "Item \"Gold Shoes\" time 40s"),
        Passive("M67", "アイテム「ゴールドシューズ」を100回取る", "Collect item \"Gold Shoes\" 100 times", MissionStat.GoldenShoesCollected, 100, MissionPassiveType.GoldenShoesDuration, 2, "アイテム「ゴールドシューズ」の効果時間50秒", "Item \"Gold Shoes\" time 50s", "M66"),
    };

    /// <summary>今のミッション一覧（Inspectorで変えた回数・金額はそのまま）に、ゴールドシューズの2個だけを足す</summary>
    [MenuItem("Tools/ミッション/16. ゴールドシューズのミッションを追加（2個・今の一覧に足す）")]
    private static void AddGoldenShoesMissions()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<MissionCatalog>(CatalogAssetPath);
        if (catalog == null) { EditorUtility.DisplayDialog("アイテム「ゴールドシューズ」のミッション", $"{CatalogAssetPath} がありません。先に「1. ミッション一覧を作成」を実行してください。", "OK"); return; }
        Undo.RecordObject(catalog, "アイテム「ゴールドシューズ」のミッションを追加");
        int added = 0;
        foreach (var def in GoldenShoesMissions())
        {
            if (catalog.Find(def.id) != null) continue;
            catalog.missions.Add(def);
            added++;
        }
        var pv = catalog.passiveValues ?? (catalog.passiveValues = new PassiveValues());
        if (pv.goldenShoesDuration == null || pv.goldenShoesDuration.Length < 2) pv.goldenShoesDuration = new[] { 40f, 50f };
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        string msg = added > 0 ? $"{added} 個追加しました（全部で {catalog.missions.Count} 個）。" : $"もう入っています（全部で {catalog.missions.Count} 個）。";
        Debug.Log("[MissionSetupTool] ゴールドシューズのミッション：" + msg);
        EditorUtility.DisplayDialog("アイテム「ゴールドシューズ」のミッション", msg, "OK");
    }

    // =====================================================================
    // 2. エネミーの大きさの区分を自動で入れる
    // =====================================================================

    [MenuItem("Tools/ミッション/2. エネミーの大きさの区分を自動で入れる")]
    private static void AssignEnemySizeClasses()
    {
        if (!EditorUtility.DisplayDialog("エネミーの大きさの区分",
                "Area1〜Area10のAreaConfigを読んで、各EnemyDataの「Size Class」を入れ直します。\n" +
                "・Area1〜9のStage1に配置 → Small（小型）\n・Stage2に配置 → Medium（中型）\n・Stage3とArea10の全Stage → Boss\n" +
                "・IronNestのNM01〜03 → Boss\n・Susanoo →「Exclude From Mission Kill Count」をON\n続けますか？",
                "実行", "キャンセル"))
            return;

        var result = new Dictionary<EnemyData, EnemySizeClass>();
        var conflicts = new StringBuilder();

        for (int area = 1; area <= 10; area++)
        {
            var config = AssetDatabase.LoadAssetAtPath<AreaConfig>($"{AreaConfigFolder}Area{area}Config.asset");
            if (config == null || config.waveStages == null) { Debug.LogWarning($"[MissionSetupTool] Area{area}Config が読めません"); continue; }
            for (int s = 0; s < config.waveStages.Length; s++)
            {
                var stage = config.waveStages[s];
                if (stage?.formations == null) continue;
                EnemySizeClass cls = area == 10 ? EnemySizeClass.Boss
                    : s == 0 ? EnemySizeClass.Small
                    : s == 1 ? EnemySizeClass.Medium
                    : EnemySizeClass.Boss;
                foreach (var f in stage.formations)
                {
                    if (f?.entries == null) continue;
                    foreach (var e in f.entries)
                    {
                        if (e?.enemyData == null) continue;
                        if (result.TryGetValue(e.enemyData, out var prev) && prev != cls)
                        {
                            // 複数の区分に出てくる場合は大きい方（Boss＞Medium＞Small）を採用し、報告する
                            conflicts.AppendLine($"{e.enemyData.name}：{prev} と {cls}（Area{area} Stage{s + 1}）");
                            if (cls > prev) result[e.enemyData] = cls;
                        }
                        else result[e.enemyData] = cls;
                    }
                }
            }
        }

        // IronNestのNM01〜03（ボスの部位。AreaConfigには載っていない）
        foreach (var n in new[] { "EnemyData_IronNest_NM01", "EnemyData_IronNest_NM02", "EnemyData_IronNest_NM03" })
        {
            var d = AssetDatabase.LoadAssetAtPath<EnemyData>($"{EnemyDataFolder}{n}.asset");
            if (d != null) result[d] = EnemySizeClass.Boss; else Debug.LogWarning($"[MissionSetupTool] {n} が見つかりません");
        }

        var summary = new StringBuilder();
        int changed = 0;
        foreach (var kv in result)
        {
            var d = kv.Key;
            bool isSusanoo = d.name == "EnemyData_Susanoo";
            if (d.sizeClass != kv.Value || d.excludeFromMissionKillCount != isSusanoo)
            {
                Undo.RecordObject(d, "Assign Enemy Size Class");
                d.sizeClass = kv.Value;
                d.excludeFromMissionKillCount = isSusanoo;
                EditorUtility.SetDirty(d);
                changed++;
            }
        }
        AssetDatabase.SaveAssets();

        int small = 0, medium = 0, boss = 0;
        var names = new SortedDictionary<EnemySizeClass, List<string>>();
        foreach (var kv in result)
        {
            if (!names.TryGetValue(kv.Value, out var l)) names[kv.Value] = l = new List<string>();
            l.Add(kv.Key.name.Replace("EnemyData_", ""));
            if (kv.Value == EnemySizeClass.Small) small++; else if (kv.Value == EnemySizeClass.Medium) medium++; else if (kv.Value == EnemySizeClass.Boss) boss++;
        }
        foreach (var kv in names) { kv.Value.Sort(); summary.AppendLine($"{kv.Key}（{kv.Value.Count}）：{string.Join(", ", kv.Value)}"); }

        Debug.Log($"[MissionSetupTool] 区分を入れました（変更 {changed} 件）\n{summary}" +
                  (conflicts.Length > 0 ? $"\n★複数の区分に出てくるエネミー（大きい方を採用）：\n{conflicts}" : ""));
        EditorUtility.DisplayDialog("エネミーの大きさの区分",
            $"完了しました（変更 {changed} 件）\n小型 {small}種／中型 {medium}種／ボス {boss}種\n" +
            (conflicts.Length > 0 ? "\n★複数の区分に出てくるエネミーがあります。Consoleのログを確認してください。" : "") +
            "\n詳細はConsoleのログに出しています。", "OK");
    }

    // =====================================================================
    // 3・4. 確認用
    // =====================================================================

    [MenuItem("Tools/ミッション/3. 進捗をログに出す（Play中）")]
    private static void LogProgress()
    {
        if (MissionProgressManager.Instance == null) { Debug.LogWarning("[MissionSetupTool] Play中に実行してください"); return; }
        MissionProgressManager.Instance.LogAll();
    }

    [MenuItem("Tools/ミッション/4. 進捗を全部消す（確認用）")]
    private static void ResetProgress()
    {
        if (!EditorUtility.DisplayDialog("ミッションの進捗を消す", "ミッションの進捗と受け取り済みの記録を全部消します。続けますか？", "消す", "キャンセル"))
            return;
        MissionProgressManager.ResetPersistent();
        Debug.Log("[MissionSetupTool] ミッションの進捗を消しました");
    }

    [MenuItem("Tools/ミッション/7. パッシブを全部受け取った状態にする（確認用）")]
    private static void ClaimAllPassivesForTest()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<MissionCatalog>(CatalogAssetPath);
        if (catalog == null) { EditorUtility.DisplayDialog("パッシブを全部受け取る", $"{CatalogAssetPath} がありません。先に「1. ミッション一覧を作成」を実行してください。", "OK"); return; }
        if (!EditorUtility.DisplayDialog("パッシブを全部受け取る",
                "パッシブのミッションを全部（2段目まで）受け取り済みにします。ゴールドの報酬は受け取りません。\n" +
                "CategoryAのパッシブ（HUDのライム色のマス）は、次にゲームを始めた時から付きます。\n" +
                "元に戻すには「4. 進捗を全部消す（確認用）」を使ってください。続けますか？", "受け取る", "キャンセル"))
            return;
        int n = MissionProgressManager.DebugClaimAllPassives(catalog);
        Debug.Log($"[MissionSetupTool] パッシブのミッション {n} 個を受け取り済みにしました（確認用）");
    }
}
