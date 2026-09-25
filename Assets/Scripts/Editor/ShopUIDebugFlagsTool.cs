using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// ★一時的な修復ツール（原因特定でき次第削除）。ShopUIのdebugHideNavRow等が
/// コードのデフォルト値変更を反映しない問題への対処として、シーンへ明示的に値を書き込む。
/// </summary>
public static class ShopUIDebugFlagsTool
{
    private const string ScenePath = "Assets/Scenes/03_AreaSelect.unity";

    [MenuItem("Tools/★03_AreaSelectのShopUIデバッグフラグをリセットする")]
    public static void ResetFlags()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var shopUiType = System.Type.GetType("ShopUI, Assembly-CSharp");
        if (shopUiType == null)
        {
            Debug.LogError("[ShopUIDebugFlagsTool] ShopUI型が見つかりません。");
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
            Debug.LogError("[ShopUIDebugFlagsTool] ShopUIコンポーネントが見つかりません。");
            return;
        }

        var so = new SerializedObject(shopUiComp);

        void SetBool(string fieldName, bool value)
        {
            var prop = so.FindProperty(fieldName);
            if (prop == null)
            {
                Debug.LogWarning($"[ShopUIDebugFlagsTool] フィールド'{fieldName}'が見つかりません。");
                return;
            }
            Debug.Log($"[ShopUIDebugFlagsTool] {fieldName}: {prop.boolValue} -> {value}");
            prop.boolValue = value;
        }

        SetBool("debugFreezeCharacterAnim", true);
        SetBool("debugHideCharacterAndCustomer", true);
        SetBool("debugDisableBgBrightnessAnim", true);
        SetBool("debugHideNavRow", false);
        SetBool("debugHideDrinkMenu", false);
        SetBool("debugDisableScrollMask", false);
        SetBool("debugSuspendConstellationFX", true);
        SetBool("debugHideAreaPanelBackground", true);
        SetBool("debugDisableVignetteFlash", false);
        SetBool("debugDisableDrinkEffectAnimations", false);
        SetBool("debugPrewarmAllPages", true);

        so.ApplyModifiedProperties();

        // DrinkCardTemplate(DrinkCardUI)側のデバッグフラグも同時にリセットする
        var cardType = System.Type.GetType("DrinkCardUI, Assembly-CSharp");
        if (cardType != null)
        {
            Component cardComp = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                cardComp = root.GetComponentInChildren(cardType, true);
                if (cardComp != null) break;
            }
            if (cardComp != null)
            {
                var cardSo = new SerializedObject(cardComp);
                var prop = cardSo.FindProperty("debugHideAllText");
                if (prop != null)
                {
                    Debug.Log($"[ShopUIDebugFlagsTool] DrinkCardUI.debugHideAllText: {prop.boolValue} -> False");
                    prop.boolValue = false;
                }
                var purchasedProp = cardSo.FindProperty("debugDisablePurchasedVisuals");
                if (purchasedProp != null)
                {
                    Debug.Log($"[ShopUIDebugFlagsTool] DrinkCardUI.debugDisablePurchasedVisuals: {purchasedProp.boolValue} -> True");
                    purchasedProp.boolValue = true;
                }
                var pulseProp = cardSo.FindProperty("debugDisableSelectionPulse");
                if (pulseProp != null)
                {
                    Debug.Log($"[ShopUIDebugFlagsTool] DrinkCardUI.debugDisableSelectionPulse: {pulseProp.boolValue} -> False");
                    pulseProp.boolValue = false;
                }
                void SetCardBool(string fieldName, bool value)
                {
                    var p = cardSo.FindProperty(fieldName);
                    if (p == null) return;
                    Debug.Log($"[ShopUIDebugFlagsTool] DrinkCardUI.{fieldName}: {p.boolValue} -> {value}");
                    p.boolValue = value;
                }
                SetCardBool("debugHideDrinkIcon", false);
                SetCardBool("debugHideSkillIcon", false);
                SetCardBool("debugHideGoldIcon", false);
                cardSo.ApplyModifiedProperties();
            }
            else
            {
                Debug.LogWarning("[ShopUIDebugFlagsTool] DrinkCardUIコンポーネントが見つかりません。");
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[ShopUIDebugFlagsTool] 完了。ShopUI/DrinkCardUIのデバッグフラグをシーンに保存しました。");
    }
}
