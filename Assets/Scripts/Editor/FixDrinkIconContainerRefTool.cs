using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// ★一時的な修復ツール（実行後は削除してよい）。ShopUIとShopPanelを別々のタイミングで複製したため、
/// ShopUI.drinkIconContainer（Inspector手動配線の参照、ShopPanel/HeaderRow/TitleTextを指す）が
/// 複製時に切れてしまった。04_Drink内で改めて正しいTitleTextを探して再配線する。
/// </summary>
public static class FixDrinkIconContainerRefTool
{
    private const string DrinkScenePath = "Assets/Scenes/04_Drink.unity";

    [MenuItem("Tools/★05 Drink: drinkIconContainer参照を再配線")]
    public static void FixReference()
    {
        var scene = EditorSceneManager.OpenScene(DrinkScenePath, OpenSceneMode.Single);

        var shopUiType = System.Type.GetType("ShopUI, Assembly-CSharp");
        if (shopUiType == null)
        {
            Debug.LogError("[FixDrinkIconContainerRefTool] ShopUI型が見つかりません。");
            return;
        }

        Component shopUiComp = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            shopUiComp = root.GetComponentInChildren(shopUiType, true);
            if (shopUiComp != null) break;
        }

        if (shopUiComp == null)
        {
            Debug.LogError("[FixDrinkIconContainerRefTool] ShopUIコンポーネントが見つかりません。");
            return;
        }

        var titleTextTrans = GameObject.Find("Canvas")?.transform.Find("ShopPanel/HeaderRow/TitleText");
        if (titleTextTrans == null)
        {
            Debug.LogError("[FixDrinkIconContainerRefTool] Canvas/ShopPanel/HeaderRow/TitleTextが見つかりません。");
            return;
        }

        var so = new SerializedObject(shopUiComp);
        var prop = so.FindProperty("drinkIconContainer");
        if (prop == null)
        {
            Debug.LogError("[FixDrinkIconContainerRefTool] drinkIconContainerフィールドが見つかりません。");
            return;
        }

        prop.objectReferenceValue = titleTextTrans;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[FixDrinkIconContainerRefTool] 完了。drinkIconContainerをShopPanel/HeaderRow/TitleTextへ再配線しました。");
    }
}
