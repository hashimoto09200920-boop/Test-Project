using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ★一時的な修復ツール（実行後は削除してよい）。CreateDrinkSceneToolの複製リストに
/// "ShopPanel"（ドリンクカード一覧・ヘッダー・ボタン類の実体）を入れ忘れていたため、
/// 04_Drinkに追加で複製する。他のオブジェクトには触れない。
/// </summary>
public static class AddShopPanelToDrinkSceneTool
{
    private const string DrinkScenePath = "Assets/Scenes/04_Drink.unity";
    private const string AreaSelectPath = "Assets/Scenes/03_AreaSelect.unity";
    private const string TargetName = "ShopPanel";

    [MenuItem("Tools/★04 Drink: ShopPanelを追加複製")]
    public static void AddShopPanel()
    {
        var drinkScene = EditorSceneManager.OpenScene(DrinkScenePath, OpenSceneMode.Single);
        var drinkCanvasObj = GameObject.Find("Canvas");
        if (drinkCanvasObj == null)
        {
            Debug.LogError("[AddShopPanelToDrinkSceneTool] 04_DrinkにCanvasが見つかりません。");
            return;
        }

        if (drinkCanvasObj.transform.Find(TargetName) != null)
        {
            Debug.LogWarning($"[AddShopPanelToDrinkSceneTool] 04_Drinkに既に{TargetName}が存在します。重複を避けるため中断します。");
            return;
        }

        var areaSelectScene = EditorSceneManager.OpenScene(AreaSelectPath, OpenSceneMode.Additive);

        GameObject found = null;
        foreach (var root in areaSelectScene.GetRootGameObjects())
        {
            found = FindDeepChild(root.transform, TargetName);
            if (found != null) break;
        }

        if (found == null)
        {
            Debug.LogError($"[AddShopPanelToDrinkSceneTool] 03_AreaSelectに{TargetName}が見つかりませんでした。");
            EditorSceneManager.CloseScene(areaSelectScene, true);
            return;
        }

        var clone = Object.Instantiate(found);
        clone.name = TargetName;
        SceneManager.MoveGameObjectToScene(clone, drinkScene);
        clone.transform.SetParent(drinkCanvasObj.transform, false);

        // 03_AreaSelectは読み取り専用で開いただけなので、保存せずにAdditiveシーンを閉じる
        EditorSceneManager.CloseScene(areaSelectScene, true);

        EditorSceneManager.SaveScene(drinkScene);
        Debug.Log($"[AddShopPanelToDrinkSceneTool] {TargetName} を04_DrinkのCanvas配下へ複製しました。03_AreaSelect.unityは変更していません。");
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
