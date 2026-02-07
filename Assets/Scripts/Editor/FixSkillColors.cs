using UnityEngine;
using UnityEditor;
using Game.Skills;

/// <summary>
/// 既存のスキル定義のrarityColorを修正するEditorスクリプト
/// </summary>
public class FixSkillColors : MonoBehaviour
{
    [MenuItem("Tools/Skills/Fix Skill Colors (Update Existing Skills)")]
    public static void FixAllSkillColors()
    {
        // スキル定義を読み込み
        SkillDefinition[] allSkills = Resources.LoadAll<SkillDefinition>("GameData/Skills");

        if (allSkills == null || allSkills.Length == 0)
        {
            EditorUtility.DisplayDialog("Error",
                "スキル定義が見つかりません。\nAssets/Resources/GameData/Skills にスキルが存在することを確認してください。",
                "OK");
            return;
        }

        int updatedCount = 0;
        Color targetColor = new Color(0.2f, 0.2f, 0.2f, 1f); // Dark gray

        foreach (var skill in allSkills)
        {
            if (skill != null)
            {
                // 白色のスキルのみ更新（既に正しい色の場合はスキップ）
                if (skill.rarityColor == Color.white ||
                    (skill.rarityColor.r > 0.9f && skill.rarityColor.g > 0.9f && skill.rarityColor.b > 0.9f))
                {
                    skill.rarityColor = targetColor;
                    EditorUtility.SetDirty(skill);
                    updatedCount++;
                    Debug.Log($"[FixSkillColors] Updated: {skill.skillName}");
                }
            }
        }

        if (updatedCount > 0)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[FixSkillColors] Updated {updatedCount} skill definitions");
            EditorUtility.DisplayDialog("Complete",
                $"{updatedCount}個のスキル定義の色を修正しました！\n\n背景色: 暗いグレー (R:0.2, G:0.2, B:0.2)\nテキストの白色が見えるようになります。",
                "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Info",
                "更新が必要なスキルはありませんでした。\n全てのスキルは既に正しい色に設定されています。",
                "OK");
        }
    }
}
