using System.Collections.Generic;
using Game.Skills;
using UnityEngine;

/// <summary>
/// ミッション報酬のパッシブを調べる窓口（2026/11/07）。
/// ・受け取り済み（MissionProgressManagerの記録）のパッシブだけが効く
/// ・Tutorial中（GameSession.IsInTutorial）は一切効かない。Area10は効く
/// ・効果の数値は MissionCatalog の passiveValues（Inspectorで調整）
/// </summary>
public static class MissionPassives
{
    private static MissionProgressManager Mgr => MissionProgressManager.Instance;
    private static MissionCatalog Catalog => Mgr != null ? Mgr.Catalog : null;
    private static bool Enabled => !GameSession.IsInTutorial && Catalog != null;

    // 段の計算結果（反射弾1ヒットごとに50個を調べ直さないため）。受け取り・セーブ消去でInvalidate()
    private static readonly Dictionary<MissionPassiveType, int> s_tierCache = new Dictionary<MissionPassiveType, int>();

    /// <summary>受け取り状態が変わった時に呼ぶ（MissionProgressManager）</summary>
    public static void Invalidate() => s_tierCache.Clear();

    /// <summary>そのパッシブの受け取り済みの段（0＝未受け取り、1または2）</summary>
    public static int GetTier(MissionPassiveType type)
    {
        if (!Enabled) return 0;
        if (s_tierCache.TryGetValue(type, out int cached)) return cached;
        int tier = 0;
        foreach (var m in Catalog.missions)
        {
            if (m == null || m.rewardType != MissionRewardType.Passive || m.passiveType != type) continue;
            if (Mgr.IsClaimed(m.id)) tier = Mathf.Max(tier, m.passiveTier);
        }
        s_tierCache[type] = tier;
        return tier;
    }

    public static bool Has(MissionPassiveType type) => GetTier(type) > 0;

    // ソウルリバイブ：1プレイ（SessionStats.Resetから次のResetまで。Area10は全体で1回）に1回だけ
    private static bool s_soulReviveUsed;

    /// <summary>1プレイ分の使用状態を戻す（SessionStats.Reset → MissionProgressManager.OnSessionStatsReset から呼ぶ）</summary>
    public static void ResetRunState() { s_soulReviveUsed = false; s_rerollUsed = 0; }

    // スキル引き直し：1プレイ（SessionStats.Resetから次のResetまで）で使った回数
    private static int s_rerollUsed;

    /// <summary>このプレイで残っているスキル引き直しの回数</summary>
    public static int GetSkillRerollRemaining() => Mathf.Max(0, GetSkillRerollCount() - s_rerollUsed);

    /// <summary>残りがあれば1回使ってtrue</summary>
    public static bool TryUseSkillReroll()
    {
        if (GetSkillRerollRemaining() <= 0) return false;
        s_rerollUsed++;
        return true;
    }

    /// <summary>ソウルリバイブを使えるなら使った扱いにしてtrueを返す（受け取り済み・このプレイでまだ使っていない時だけ）</summary>
    public static bool TryUseSoulRevive()
    {
        if (s_soulReviveUsed || !Has(MissionPassiveType.AutoSoulRescue)) return false;
        s_soulReviveUsed = true;
        return true;
    }

    /// <summary>受け取り済みのCategoryAのパッシブのスキル（同じスキルは1回だけ）</summary>
    public static List<SkillDefinition> GetClaimedCategoryASkills()
    {
        var list = new List<SkillDefinition>();
        if (!Enabled) return list;
        foreach (var m in Catalog.missions)
        {
            if (m == null || m.rewardType != MissionRewardType.Passive || m.passiveType != MissionPassiveType.CategoryASkill) continue;
            if (m.passiveSkill == null || !Mgr.IsClaimed(m.id) || list.Contains(m.passiveSkill)) continue;
            list.Add(m.passiveSkill);
        }
        return list;
    }

    /// <summary>そのスキルのパッシブの数（ジェム画面・フード画面のHUD用。スキルのアセット名で調べる）</summary>
    public static int GetCategoryACount(string skillName)
    {
        if (string.IsNullOrEmpty(skillName)) return 0;
        foreach (var s in GetClaimedCategoryASkills())
            if (s != null && s.name == skillName) return 1;
        return 0;
    }

    /// <summary>
    /// ゲーム開始時に、受け取り済みのCategoryAのパッシブをスキルとして付ける（ジェム・フードより先に呼ぶ）。
    /// 各スキルの最大取得数（maxAcquisitionCount）に含めるので、上限に達していれば付けない
    /// </summary>
    public static void ApplyCategoryASkills(SkillManager skillManager)
    {
        if (skillManager == null) return;
        foreach (var skill in GetClaimedCategoryASkills())
        {
            if (!skillManager.CanAcquireSkill(skill)) continue;
            skillManager.AddSkill(skill, SkillSource.Passive);
            Debug.Log($"[MissionPassives] パッシブを付けました：{skill.skillName}");
        }
    }

    // =====================================================================
    // 数値の効果（③-2で各処理から呼ぶ）
    // =====================================================================

    private static PassiveValues V => Catalog != null ? Catalog.passiveValues : null;

    /// <summary>大きさの区分ごとの、HPへのダメージの追加量</summary>
    public static int GetDamageBonus(EnemySizeClass size)
    {
        if (V == null) return 0;
        var type = size == EnemySizeClass.Small ? MissionPassiveType.DamageSmall
                 : size == EnemySizeClass.Medium ? MissionPassiveType.DamageMedium
                 : size == EnemySizeClass.Boss ? MissionPassiveType.DamageBoss
                 : MissionPassiveType.None;
        return type == MissionPassiveType.None ? 0 : PassiveValues.Pick(V.damageBonus, GetTier(type));
    }

    /// <summary>大きさの区分ごとの、シールドへのダメージの追加量</summary>
    public static int GetShieldDamageBonus(EnemySizeClass size)
    {
        if (V == null) return 0;
        var type = size == EnemySizeClass.Small ? MissionPassiveType.ShieldDamageSmall
                 : size == EnemySizeClass.Medium ? MissionPassiveType.ShieldDamageMedium
                 : size == EnemySizeClass.Boss ? MissionPassiveType.ShieldDamageBoss
                 : MissionPassiveType.None;
        return type == MissionPassiveType.None ? 0 : PassiveValues.Pick(V.shieldDamageBonus, GetTier(type));
    }

    /// <summary>オーバーヒート時間に掛ける倍率（10%短縮なら0.9）</summary>
    public static float GetOverheatTimeMultiplier(MissionPassiveType type)
    {
        if (V == null) return 1f;
        return 1f - Mathf.Clamp01(PassiveValues.Pick(V.overheatShorten, GetTier(type)));
    }

    /// <summary>ゴールド獲得量の増加割合（+25%なら0.25）</summary>
    public static float GetGoldGainRate() => V == null ? 0f : PassiveValues.Pick(V.goldGain, GetTier(MissionPassiveType.GoldGainUp));

    /// <summary>1回のプレイで使えるスキル引き直しの回数</summary>
    public static int GetSkillRerollCount() => V == null ? 0 : PassiveValues.Pick(V.skillReroll, GetTier(MissionPassiveType.SkillReroll));

    /// <summary>ステージクリア時のダンサーとフロアの回復量</summary>
    /// <summary>ゴールドシューズの効果時間（秒）。パッシブを受け取っていなければ baseDuration</summary>
    public static float GetGoldenShoesDuration(float baseDuration)
    {
        int tier = GetTier(MissionPassiveType.GoldenShoesDuration);
        if (tier <= 0 || V == null) return baseDuration;
        return Mathf.Max(baseDuration, PassiveValues.Pick(V.goldenShoesDuration, tier));
    }

    /// <summary>アクセラレーターの効果時間（秒）。パッシブを受け取っていなければ baseDuration</summary>
    public static float GetGoldBoltDuration(float baseDuration)
    {
        int tier = GetTier(MissionPassiveType.GoldBoltDuration);
        if (tier <= 0 || V == null) return baseDuration;
        return Mathf.Max(baseDuration, PassiveValues.Pick(V.goldBoltDuration, tier));
    }

    /// <summary>マルチバウンドの効果時間（秒）。パッシブを受け取っていなければ baseDuration</summary>
    public static float GetMultiBoundDuration(float baseDuration)
    {
        int tier = GetTier(MissionPassiveType.MultiBoundDuration);
        if (tier <= 0 || V == null) return baseDuration;
        return Mathf.Max(baseDuration, PassiveValues.Pick(V.multiBoundDuration, tier));
    }

    /// <summary>スパイラルショットの敵へのヒット回数の追加（受け取っていなければ0）</summary>
    public static int GetSpiralShotHitBonus() => V == null ? 0 : PassiveValues.Pick(V.spiralShotHitBonus, GetTier(MissionPassiveType.SpiralShotHits));

    /// <summary>ブラストショットの爆発ダメージ（受け取っていなければ baseDamage）</summary>
    public static int GetBlastShotDamage(int baseDamage)
    {
        int tier = GetTier(MissionPassiveType.BlastShotPower);
        if (tier <= 0 || V == null) return baseDamage;
        return Mathf.Max(baseDamage, PassiveValues.Pick(V.blastShotDamage, tier));
    }

    /// <summary>ブラストショットの爆発の範囲（半径）。受け取っていない・その段で広がらない時は baseRadius</summary>
    public static float GetBlastShotRadius(float baseRadius)
    {
        int tier = GetTier(MissionPassiveType.BlastShotPower);
        if (tier <= 0 || V == null) return baseRadius;
        return Mathf.Max(baseRadius, PassiveValues.Pick(V.blastShotRadius, tier));
    }

    /// <summary>スプリットショットのダメージ倍率（受け取っていなければ1）</summary>
    public static float GetSplitShotDamageMul()
    {
        int tier = GetTier(MissionPassiveType.SplitShotPower);
        if (tier <= 0 || V == null) return 1f;
        return Mathf.Max(1f, PassiveValues.Pick(V.splitShotDamageMul, tier));
    }

    /// <summary>スプリットショットの分かれる数の追加（受け取っていなければ0）</summary>
    public static int GetSplitShotExtraSplits() => V == null ? 0 : Mathf.Max(0, PassiveValues.Pick(V.splitShotExtraSplits, GetTier(MissionPassiveType.SplitShotPower)));

    public static int GetStageClearHeal() => V == null ? 0 : PassiveValues.Pick(V.stageClearHeal, GetTier(MissionPassiveType.StageClearHeal));
}
