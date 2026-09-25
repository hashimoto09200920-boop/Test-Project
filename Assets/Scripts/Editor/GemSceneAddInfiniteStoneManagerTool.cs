using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;

/// <summary>
/// ★一時的な修復ツール（実行後は削除してよい）。02_Gemシーンに03_AreaSelectの
/// InfiniteStoneManagerを複製して追加する（無限化ボタンが反応しない問題の対策）。
/// 03_AreaSelectには一切触れず、保存もしない。
/// </summary>
public static class GemSceneAddInfiniteStoneManagerTool
{
    private const string AreaSelectScenePath = "Assets/Scenes/03_AreaSelect.unity";
    private const string GemScenePath = "Assets/Scenes/02_Gem.unity";

    [MenuItem("Tools/★02_GemにInfiniteStoneManagerを追加")]
    public static void AddInfiniteStoneManager()
    {
        var areaSelectScene = EditorSceneManager.OpenScene(AreaSelectScenePath, OpenSceneMode.Single);
        var gemScene = EditorSceneManager.OpenScene(GemScenePath, OpenSceneMode.Additive);

        if (gemScene.GetRootGameObjects().Any(g => g.name == "InfiniteStoneManager"))
        {
            Debug.LogWarning("[GemSceneAddInfiniteStoneManagerTool] 02_Gemには既にInfiniteStoneManagerがあります。中断します。");
            return;
        }

        var original = areaSelectScene.GetRootGameObjects()
            .FirstOrDefault(g => g.name == "InfiniteStoneManager");

        if (original == null)
        {
            Debug.LogError("[GemSceneAddInfiniteStoneManagerTool] 03_AreaSelectにInfiniteStoneManagerが見つかりません。中断します。");
            return;
        }

        var copy = Object.Instantiate(original);
        copy.name = "InfiniteStoneManager";
        SceneManager.MoveGameObjectToScene(copy, gemScene);

        EditorSceneManager.MarkSceneDirty(gemScene);
        EditorSceneManager.SaveScene(gemScene);

        Debug.Log("[GemSceneAddInfiniteStoneManagerTool] 完了。InfiniteStoneManagerを02_Gemに追加しました。03_AreaSelectは変更していません。");
    }
}
