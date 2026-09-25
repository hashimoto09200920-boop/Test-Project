using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ★一時的な修復ツール（実行後は削除してよい）。02_GemのGemSkillPreviewHUD配下、
/// 各SkillCard_X > IconBackground > IconImage に、05_Gameと同じ方式で
/// SkillDefinitionのiconスプライトを直接セットする（実行時のコード割り当てに頼らない）。
/// </summary>
public static class GemSceneSetIconSpritesTool
{
    private const string GemScenePath = "Assets/Scenes/02_Gem.unity";

    [MenuItem("Tools/★02_GemのアイコンSpriteを直接セット")]
    public static void SetIconSprites()
    {
        var scene = EditorSceneManager.OpenScene(GemScenePath, OpenSceneMode.Single);

        var allSkills = AssetDatabase.FindAssets("t:SkillDefinition")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<Game.Skills.SkillDefinition>)
            .Where(s => s != null)
            .ToList();

        int setCount = 0;
        int skipCount = 0;

        foreach (var skill in allSkills)
        {
            string cardName = $"SkillCard_{skill.name}";
            Transform cardT = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                var found = FindDeepChild(root.transform, cardName);
                if (found != null) { cardT = found; break; }
            }

            if (cardT == null)
            {
                Debug.LogWarning($"[GemSceneSetIconSpritesTool] {cardName} が見つかりません。");
                skipCount++;
                continue;
            }

            var iconImage = cardT.Find("IconBackground/IconImage")?.GetComponent<Image>();
            if (iconImage == null)
            {
                Debug.LogWarning($"[GemSceneSetIconSpritesTool] {cardName}/IconBackground/IconImage が見つかりません。");
                skipCount++;
                continue;
            }

            if (skill.icon == null)
            {
                Debug.LogWarning($"[GemSceneSetIconSpritesTool] {skill.name} のSkillDefinitionにiconが設定されていません。");
                skipCount++;
                continue;
            }

            iconImage.sprite = skill.icon;
            iconImage.color = Color.white;
            iconImage.enabled = true;
            EditorUtility.SetDirty(iconImage);
            setCount++;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[GemSceneSetIconSpritesTool] 完了。{setCount}件セット、{skipCount}件スキップ。");
    }

    private static Transform FindDeepChild(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            var result = FindDeepChild(child, name);
            if (result != null) return result;
        }
        return null;
    }
}
