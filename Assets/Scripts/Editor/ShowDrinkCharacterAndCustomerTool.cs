using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// ★一時的な修復ツール（実行後は削除してよい）。04_Drinkのバーテンダー・お客(ダンサー)表示と
/// バーテンダーのアニメーション再生を切り替える。
/// </summary>
public static class ShowDrinkCharacterAndCustomerTool
{
    private const string ScenePath = "Assets/Scenes/04_Drink.unity";

    [MenuItem("Tools/★04_Drinkのバーテンダー・お客(ダンサー)静止画を表示する")]
    public static void ShowCharacterAndCustomer()
    {
        SetFlag("debugHideCharacterAndCustomer", false);
    }

    [MenuItem("Tools/★04_Drinkのバーテンダーアニメーションをオンにする")]
    public static void EnableCharacterAnim()
    {
        SetFlag("debugFreezeCharacterAnim", false);
    }

    [MenuItem("Tools/★04_Drinkの購入済み表示(スタンプ)をオンにする")]
    public static void EnablePurchasedVisuals()
    {
        SetCardFlag("debugDisablePurchasedVisuals", false);
    }

    [MenuItem("Tools/★04_Drinkの背景明暗アニメーションをオンにする")]
    public static void EnableBgBrightnessAnim()
    {
        SetFlag("debugDisableBgBrightnessAnim", false);
    }

    private static void SetFlag(string fieldName, bool value)
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var shopUiType = System.Type.GetType("ShopUI, Assembly-CSharp");
        if (shopUiType == null)
        {
            Debug.LogError("[ShowDrinkCharacterAndCustomerTool] ShopUI型が見つかりません。");
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
            Debug.LogError("[ShowDrinkCharacterAndCustomerTool] ShopUIコンポーネントが見つかりません。");
            return;
        }

        var so = new SerializedObject(shopUiComp);
        var prop = so.FindProperty(fieldName);
        if (prop == null)
        {
            Debug.LogError($"[ShowDrinkCharacterAndCustomerTool] {fieldName}フィールドが見つかりません。");
            return;
        }

        Debug.Log($"[ShowDrinkCharacterAndCustomerTool] {fieldName}: {prop.boolValue} -> {value}");
        prop.boolValue = value;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[ShowDrinkCharacterAndCustomerTool] 完了。04_Drinkを保存しました。");
    }

    private static void SetCardFlag(string fieldName, bool value)
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var cardType = System.Type.GetType("DrinkCardUI, Assembly-CSharp");
        if (cardType == null)
        {
            Debug.LogError("[ShowDrinkCharacterAndCustomerTool] DrinkCardUI型が見つかりません。");
            return;
        }

        Component cardComp = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            cardComp = root.GetComponentInChildren(cardType, true);
            if (cardComp != null) break;
        }

        if (cardComp == null)
        {
            Debug.LogError("[ShowDrinkCharacterAndCustomerTool] DrinkCardUIコンポーネントが見つかりません。");
            return;
        }

        var so = new SerializedObject(cardComp);
        var prop = so.FindProperty(fieldName);
        if (prop == null)
        {
            Debug.LogError($"[ShowDrinkCharacterAndCustomerTool] {fieldName}フィールドが見つかりません。");
            return;
        }

        Debug.Log($"[ShowDrinkCharacterAndCustomerTool] DrinkCardUI.{fieldName}: {prop.boolValue} -> {value}");
        prop.boolValue = value;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[ShowDrinkCharacterAndCustomerTool] 完了。04_Drinkを保存しました。");
    }
}
