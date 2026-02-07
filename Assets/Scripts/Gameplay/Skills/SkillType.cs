namespace Game.Skills
{
    /// <summary>
    /// スキルのカテゴリ
    /// </summary>
    public enum SkillCategory
    {
        CategoryA,  // Stage 1 クリア後
        CategoryB   // Stage 2 クリア後
    }

    /// <summary>
    /// スキルの効果タイプ
    /// </summary>
    public enum SkillEffectType
    {
        // Category A (攻撃・リソース系)
        LeftMaxCostUp,          // 1. 白線の最大値アップ
        RedMaxCostUp,           // 2. 赤線の最大値アップ
        LeftRecoveryUp,         // 3. 白線の回復量アップ
        RedRecoveryUp,          // 4. 赤線の回復量アップ
        MaxStrokesUp,           // 5. 一度に引ける線の最大本数アップ
        JustDamageUp,           // 6. Just反射時の弾ダメージアップ

        // Category B (防御・耐久系)
        LeftLifetimeUp,         // 7. 白線が消えるまでの時間延長
        RedLifetimeUp,          // 8. 赤線が消えるまでの時間延長
        HardnessUp,             // 9. 白線・赤線のHardnessアップ
        PixelDancerHPUp,        // 10. Pixel dancerのHPアップ
        FloorHPUp               // 11. FloorのHPアップ
    }
}
