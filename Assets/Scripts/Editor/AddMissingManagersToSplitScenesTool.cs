using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;

/// <summary>
/// ★一時的な修復ツール（実行後は削除してよい）。02_Gem/04_Drinkシーンに
/// StaminaManager・InfiniteStoneManagerが複製されていなかったため追加する。
/// これらはGoldManagerと同じく DontDestroyOnLoad ではない設計のため、
/// HUD(StaminaHUD/InfiniteStoneHUD)を置いた各シーンに個別に配置する必要がある。
/// 03_AreaSelectには一切触れず、保存もしない。
/// </summary>
public static class AddMissingManagersToSplitScenesTool
{
    private const string AreaSelectScenePath = "Assets/Scenes/03_AreaSelect.unity";
    private const string GemScenePath = "Assets/Scenes/02_Gem.unity";
    private const string DrinkScenePath = "Assets/Scenes/04_Drink.unity";

    [MenuItem("Tools/★02_GemにStaminaManagerを追加")]
    public static void AddStaminaManagerToGem()
    {
        AddManagerFromAreaSelect(GemScenePath, "StaminaManager");
    }

    [MenuItem("Tools/★04_DrinkにStaminaManager+InfiniteStoneManagerを追加")]
    public static void AddManagersToDrink()
    {
        AddManagerFromAreaSelect(DrinkScenePath, "StaminaManager");
        AddManagerFromAreaSelect(DrinkScenePath, "InfiniteStoneManager");
    }

    private static void AddManagerFromAreaSelect(string targetScenePath, string objectName)
    {
        var areaSelectScene = EditorSceneManager.OpenScene(AreaSelectScenePath, OpenSceneMode.Single);
        var targetScene = EditorSceneManager.OpenScene(targetScenePath, OpenSceneMode.Additive);

        if (targetScene.GetRootGameObjects().Any(g => g.name == objectName))
        {
            Debug.LogWarning($"[AddMissingManagersToSplitScenesTool] {targetScene.name}には既に{objectName}があります。スキップします。");
            EditorSceneManager.CloseScene(targetScene, true);
            return;
        }

        var original = areaSelectScene.GetRootGameObjects()
            .FirstOrDefault(g => g.name == objectName);

        if (original == null)
        {
            Debug.LogError($"[AddMissingManagersToSplitScenesTool] 03_AreaSelectに{objectName}が見つかりません。中断します。");
            EditorSceneManager.CloseScene(targetScene, true);
            return;
        }

        var copy = Object.Instantiate(original);
        copy.name = objectName;
        SceneManager.MoveGameObjectToScene(copy, targetScene);

        EditorSceneManager.MarkSceneDirty(targetScene);
        EditorSceneManager.SaveScene(targetScene);

        Debug.Log($"[AddMissingManagersToSplitScenesTool] 完了。{objectName}を{targetScene.name}に追加しました。03_AreaSelectは変更していません。");
    }
}
