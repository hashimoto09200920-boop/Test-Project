using UnityEngine;
using UnityEditor;
using System.IO;
using Game.Skills;

/// <summary>
/// 11個のスキル定義を一括作成するEditorスクリプト
/// </summary>
public class CreateAllSkillDefinitions : MonoBehaviour
{
    [MenuItem("Tools/Skills/Create All Skill Definitions")]
    public static void CreateAllSkills()
    {
        // 保存先フォルダのパス
        string folderPath = "Assets/Resources/GameData/Skills";

        // フォルダが存在しない場合は作成
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
            AssetDatabase.Refresh();
            Debug.Log($"[CreateAllSkillDefinitions] Created folder: {folderPath}");
        }

        int createdCount = 0;

        // カテゴリA（Stage 1クリア後）のスキル
        createdCount += CreateSkill(folderPath, "Skill_A1_LeftMaxCostUp",
            "白線最大値アップ", "白線の最大値が増加する",
            SkillCategory.CategoryA, SkillEffectType.LeftMaxCostUp, 5f, false);

        createdCount += CreateSkill(folderPath, "Skill_A2_RedMaxCostUp",
            "赤線最大値アップ", "赤線の最大値が増加する",
            SkillCategory.CategoryA, SkillEffectType.RedMaxCostUp, 2f, false);

        createdCount += CreateSkill(folderPath, "Skill_A3_LeftRecoveryUp",
            "白線回復速度アップ", "白線の回復速度が増加する",
            SkillCategory.CategoryA, SkillEffectType.LeftRecoveryUp, 1f, false);

        createdCount += CreateSkill(folderPath, "Skill_A4_RedRecoveryUp",
            "赤線回復速度アップ", "赤線の回復速度が増加する",
            SkillCategory.CategoryA, SkillEffectType.RedRecoveryUp, 0.5f, false);

        createdCount += CreateSkill(folderPath, "Skill_A5_MaxStrokesUp",
            "線本数アップ", "同時に引ける線の数が増加する",
            SkillCategory.CategoryA, SkillEffectType.MaxStrokesUp, 1f, false);

        createdCount += CreateSkill(folderPath, "Skill_A6_JustDamageUp",
            "Just反射強化", "Just反射時のダメージが増加する",
            SkillCategory.CategoryA, SkillEffectType.JustDamageUp, 0.2f, false);

        // カテゴリB（Stage 2クリア後）のスキル
        createdCount += CreateSkill(folderPath, "Skill_B1_LeftLifetimeUp",
            "白線持続時間延長", "白線が消えるまでの時間が延長される",
            SkillCategory.CategoryB, SkillEffectType.LeftLifetimeUp, 0.3f, false);

        createdCount += CreateSkill(folderPath, "Skill_B2_RedLifetimeUp",
            "赤線持続時間延長", "赤線が消えるまでの時間が延長される",
            SkillCategory.CategoryB, SkillEffectType.RedLifetimeUp, 0.3f, false);

        createdCount += CreateSkill(folderPath, "Skill_B3_HardnessUp",
            "線の硬度アップ", "線が弾に強くなる",
            SkillCategory.CategoryB, SkillEffectType.HardnessUp, 1f, false);

        createdCount += CreateSkill(folderPath, "Skill_B4_PixelDancerHPUp",
            "プレイヤーHP増加", "Pixel DancerのHPが増加する",
            SkillCategory.CategoryB, SkillEffectType.PixelDancerHPUp, 1f, false);

        createdCount += CreateSkill(folderPath, "Skill_B5_FloorHPUp",
            "床HP増加", "床のHPが増加する",
            SkillCategory.CategoryB, SkillEffectType.FloorHPUp, 2f, false);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[CreateAllSkillDefinitions] Created {createdCount} skill definitions in {folderPath}");
        EditorUtility.DisplayDialog("Complete", $"{createdCount}個のスキル定義を作成しました！\n\n保存先: {folderPath}", "OK");
    }

    private static int CreateSkill(string folderPath, string fileName, string skillName, string description,
        SkillCategory category, SkillEffectType effectType, float effectValue, bool isMultiplier)
    {
        string assetPath = $"{folderPath}/{fileName}.asset";

        // 既に存在する場合はスキップ
        if (File.Exists(assetPath))
        {
            Debug.Log($"[CreateAllSkillDefinitions] Skipped (already exists): {fileName}");
            return 0;
        }

        // ScriptableObjectを作成
        SkillDefinition skill = ScriptableObject.CreateInstance<SkillDefinition>();
        skill.skillName = skillName;
        skill.description = description;
        skill.category = category;
        skill.effectType = effectType;
        skill.effectValue = effectValue;
        skill.isMultiplier = isMultiplier;
        skill.rarityColor = new Color(0.2f, 0.2f, 0.2f, 1f); // Dark gray background for white text

        // アセットとして保存
        AssetDatabase.CreateAsset(skill, assetPath);

        Debug.Log($"[CreateAllSkillDefinitions] Created: {fileName}");
        return 1;
    }
}
