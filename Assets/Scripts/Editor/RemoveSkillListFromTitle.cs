using UnityEngine;
using UnityEditor;
using Game.UI;

/// <summary>
/// Titleシーンから誤って作成されたSkillListUIを削除
/// </summary>
public class RemoveSkillListFromTitle : MonoBehaviour
{
    [MenuItem("Tools/Skills/Remove Skill List UI from Current Scene")]
    public static void RemoveSkillList()
    {
        // シーン内の全てのSkillListUIを検索
        SkillListUI[] skillLists = FindObjectsByType<SkillListUI>(FindObjectsSortMode.None);

        if (skillLists == null || skillLists.Length == 0)
        {
            EditorUtility.DisplayDialog("Info",
                "SkillListUIが見つかりませんでした。",
                "OK");
            return;
        }

        int count = 0;
        foreach (var skillList in skillLists)
        {
            if (skillList != null)
            {
                DestroyImmediate(skillList.gameObject);
                count++;
                Debug.Log($"[RemoveSkillList] Removed SkillListUI: {skillList.gameObject.name}");
            }
        }

        // 保存
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        EditorUtility.DisplayDialog("Complete",
            $"{count}個のSkillListUIを削除しました。",
            "OK");
    }
}
