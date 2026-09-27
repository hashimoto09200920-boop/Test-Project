using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// ★一時的な修復ツール（実行後は削除してよい）。04_DrinkのShopUIには、03_AreaSelectで
/// オーバーレイ表示していた頃の調査用フラグ(debugHideAreaPanelBackground等)がそのまま
/// 複製されてしまっている。04_Drinkは独立シーンでAreaSelectの背景と同居しないため、
/// これらのフラグは意味を失うどころか、GemSkillPreviewHUD自身の"Background"という
/// 名前のオブジェクトを誤って探して非表示にしてしまう副作用がある。無効化する。
/// </summary>
public static class FixDrinkShopUIFlagsTool
{
    private const string DrinkScenePath = "Assets/Scenes/04_Drink.unity";

    [MenuItem("Tools/★06 Drink: ShopUIの不要な調査用フラグを無効化")]
    public static void FixFlags()
    {
        var scene = EditorSceneManager.OpenScene(DrinkScenePath, OpenSceneMode.Single);

        var shopUiType = System.Type.GetType("ShopUI, Assembly-CSharp");
        if (shopUiType == null)
        {
            Debug.LogError("[FixDrinkShopUIFlagsTool] ShopUI型が見つかりません。");
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
            Debug.LogError("[FixDrinkShopUIFlagsTool] ShopUIコンポーネントが見つかりません。");
            return;
        }

        var so = new SerializedObject(shopUiComp);

        void SetBool(string fieldName, bool value)
        {
            var prop = so.FindProperty(fieldName);
            if (prop == null) return;
            Debug.Log($"[FixDrinkShopUIFlagsTool] {fieldName}: {prop.boolValue} -> {value}");
            prop.boolValue = value;
        }

        // AreaSelectの背景・星座演出は04_Drinkに存在しないため、これらのフラグは不要かつ有害
        SetBool("debugHideAreaPanelBackground", false);
        SetBool("debugSuspendConstellationFX", false);

        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[FixDrinkShopUIFlagsTool] 完了。");
    }
}
