using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// ★一時的な調査用ツール（原因特定でき次第削除）。ドリンク画面(03_AreaSelect内のGemSkillPreviewHUD)の
/// チカチカ切り分け用に、全SkillHUDCardUIのdebugHideIcon/debugHideTilesを一括設定する。
/// </summary>
public static class AreaSelectSkillCardDebugTool
{
    private const string ScenePath = "Assets/Scenes/03_AreaSelect.unity";

    [MenuItem("Tools/★03_AreaSelectの全カードのアイコン・タイルを非表示にする")]
    public static void HideIconAndTiles() => SetFlags(true, true);

    [MenuItem("Tools/★03_AreaSelectの全カードのアイコン・タイルを元に戻す")]
    public static void RestoreIconAndTiles() => SetFlags(false, false);

    [MenuItem("Tools/★03_AreaSelectの全カードをアイコンのみ表示にする")]
    public static void ShowIconOnly() => SetFlags(false, true);

    [MenuItem("Tools/★03_AreaSelectの全カードをタイルのみ表示にする")]
    public static void ShowTilesOnly() => SetFlags(true, false);

    private static void SetFlags(bool hideIcon, bool hideTiles)
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var cardType = System.Type.GetType("SkillHUDCardUI, Assembly-CSharp");
        if (cardType == null)
        {
            Debug.LogError("[AreaSelectSkillCardDebugTool] SkillHUDCardUI型が見つかりません。");
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
        Debug.Log($"[AreaSelectSkillCardDebugTool] {count}件をdebugHideIcon={hideIcon}, debugHideTiles={hideTiles}に設定し、シーンを保存しました。");
    }
}
