using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// ★一時的な調査用ツール（原因特定でき次第削除）。04_Drink内のGemSkillPreviewHUDの
/// 全SkillHUDCardUIのdebugHideIcon/debugHideTilesを一括設定する。
/// </summary>
public static class DrinkSceneSkillCardDebugTool
{
    private const string ScenePath = "Assets/Scenes/04_Drink.unity";

    [MenuItem("Tools/★07 Drink: 全カードのタイルを表示に戻す")]
    public static void ShowTiles() => SetFlags(false, false);

    private static void SetFlags(bool hideIcon, bool hideTiles)
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var cardType = System.Type.GetType("SkillHUDCardUI, Assembly-CSharp");
        if (cardType == null)
        {
            Debug.LogError("[DrinkSceneSkillCardDebugTool] SkillHUDCardUI型が見つかりません。");
            return;
        }

        int count = 0;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var comp in root.GetComponentsInChildren(cardType, true))
            {
                var so = new SerializedObject(comp);
                var hideIconProp = so.FindProperty("debugHideIcon");
                var hideTilesProp = so.FindProperty("debugHideTiles");
                if (hideIconProp == null || hideTilesProp == null) continue;
                hideIconProp.boolValue = hideIcon;
                hideTilesProp.boolValue = hideTiles;
                so.ApplyModifiedProperties();
                count++;
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[DrinkSceneSkillCardDebugTool] {count}件をdebugHideIcon={hideIcon}, debugHideTiles={hideTiles}に設定し、シーンを保存しました。");
    }
}
