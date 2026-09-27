using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ★一時的な修復ツール（実行後は削除してよい）。04_DrinkにStaminaHUD/InfiniteStoneHUDが
/// 複製されていなかったため追加する。他のオブジェクトには触れない。
/// </summary>
public static class AddMissingHudToDrinkSceneTool
{
    private const string DrinkScenePath = "Assets/Scenes/04_Drink.unity";
    private const string AreaSelectPath = "Assets/Scenes/03_AreaSelect.unity";

    private static readonly string[] NamesToAdd = new[]
    {
        "StaminaHUD",
        "InfiniteStoneHUD",
    };

    [MenuItem("Tools/★08 Drink: Stamina/InfiniteStoneHUDを追加複製")]
    public static void AddMissingHud()
    {
        var drinkScene = EditorSceneManager.OpenScene(DrinkScenePath, OpenSceneMode.Single);
        var drinkCanvasObj = GameObject.Find("Canvas");
        if (drinkCanvasObj == null)
        {
            Debug.LogError("[AddMissingHudToDrinkSceneTool] 04_DrinkにCanvasが見つかりません。");
            return;
        }

        var areaSelectScene = EditorSceneManager.OpenScene(AreaSelectPath, OpenSceneMode.Additive);

        int added = 0;
        foreach (var name in NamesToAdd)
        {
            if (drinkCanvasObj.transform.Find(name) != null)
            {
                Debug.LogWarning($"[AddMissingHudToDrinkSceneTool] 04_Drinkに既に{name}が存在します。スキップします。");
                continue;
            }

            GameObject found = null;
            foreach (var root in areaSelectScene.GetRootGameObjects())
            {
                found = FindDeepChild(root.transform, name);
                if (found != null) break;
            }

            if (found == null)
            {
                Debug.LogWarning($"[AddMissingHudToDrinkSceneTool] 03_AreaSelectに{name}が見つかりませんでした。");
                continue;
            }

            var clone = Object.Instantiate(found);
            clone.name = name;
            SceneManager.MoveGameObjectToScene(clone, drinkScene);
            clone.transform.SetParent(drinkCanvasObj.transform, false);
            added++;
        }

        EditorSceneManager.CloseScene(areaSelectScene, true);
        EditorSceneManager.SaveScene(drinkScene);
        Debug.Log($"[AddMissingHudToDrinkSceneTool] {added}件を04_DrinkのCanvas配下へ追加複製しました。03_AreaSelect.unityは変更していません。");
    }

    private static GameObject FindDeepChild(Transform parent, string name)
    {
        if (parent.name == name) return parent.gameObject;
        for (int i = 0; i < parent.childCount; i++)
        {
            var result = FindDeepChild(parent.GetChild(i), name);
            if (result != null) return result;
        }
        return null;
    }
}
