using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Callbacks;
using Game.UI;

public static class TouchHoverFixMigrator
{
    [DidReloadScripts]
    private static void OnScriptsReloaded()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        EditorApplication.delayCall += Run;
    }

    [MenuItem("Tools/TouchHover/Fix Missing Touch Hover Effects")]
    private static void Run()
    {
        bool changed = false;
        changed |= FixAreaSelectScene();
        changed |= FixTitleScene();
        changed |= FixGameScene();
        if (!changed)
            Debug.Log("[TouchHoverFixMigrator] 対象シーンが開かれていないか、既に適用済みのため変更はありませんでした。");
    }

    private static bool FixAreaSelectScene()
    {
        var menu = Object.FindFirstObjectByType<AreaSelectMenu>(FindObjectsInactive.Include);
        var manager = Object.FindFirstObjectByType<AreaSelectManager>(FindObjectsInactive.Include);
        var gemUI = Object.FindFirstObjectByType<GemManagementUI>(FindObjectsInactive.Include);
        var shopUI = Object.FindFirstObjectByType<ShopUI>(FindObjectsInactive.Include);

        if (menu == null && manager == null && gemUI == null && shopUI == null) return false;

        bool changed = false;

        if (menu != null)
        {
            var so = new SerializedObject(menu);
            changed |= SetLockAfterClickFromProp(so, "backButton");
        }

        if (manager != null)
        {
            var so = new SerializedObject(manager);
            changed |= SetLockAfterClickFromProp(so, "viewTutorialButton");

            // Areaノード（Btn_Area_01〜10）：クリック後は必ず05_Gameへ遷移するため、
            // Back/Tutorialボタンと同じくクリック後も拡大維持で問題ない。
            // ★非アクティブな場合があるため、GameObject.Findではなく
            //   AreaConstellationFXのnodes配列から直接参照を取る。
            var fx = Object.FindFirstObjectByType<AreaConstellationFX>(FindObjectsInactive.Include);
            if (fx != null)
            {
                var fxSo = new SerializedObject(fx);
                var nodesProp = fxSo.FindProperty("nodes");
                if (nodesProp != null)
                {
                    for (int i = 0; i < nodesProp.arraySize; i++)
                    {
                        var buttonProp = nodesProp.GetArrayElementAtIndex(i).FindPropertyRelative("button");
                        if (buttonProp != null && buttonProp.objectReferenceValue is RectTransform rt && rt != null)
                            changed |= SetLockAfterClick(rt.gameObject);
                    }
                }
            }
        }

        if (gemUI != null)
        {
            var so = new SerializedObject(gemUI);
            changed |= AddTouchTapEnlargeFromProp(so, "sharedEquipButton");
            changed |= AddTouchTapEnlargeFromProp(so, "sharedSellButton");
            changed |= AddTouchTapEnlargeFromProp(so, "closeButton");
        }

        if (shopUI != null)
        {
            var so = new SerializedObject(shopUI);
            changed |= AddTouchTapEnlargeFromProp(so, "buyButton");
            changed |= AddTouchTapEnlargeFromProp(so, "closeButton");
            changed |= AddTouchTapEnlargeFromProp(so, "prevPageButton");
            changed |= AddTouchTapEnlargeFromProp(so, "nextPageButton");
        }

        if (changed)
        {
            Component anyComp = (Component)menu ?? (Component)manager ?? (Component)gemUI ?? shopUI;
            var scene = anyComp.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[TouchHoverFixMigrator] 03_AreaSelect.unity のタッチホバー修正を保存しました。");
        }
        return changed;
    }

    private static bool FixTitleScene()
    {
        var title = Object.FindFirstObjectByType<TitleMenu>(FindObjectsInactive.Include);
        if (title == null) return false;

        var so = new SerializedObject(title);
        bool changed = false;
        changed |= AddTouchTapEnlargeFromProp(so, "startButton");
        changed |= AddTouchTapEnlargeFromProp(so, "resetButton");
        changed |= AddTouchTapEnlargeFromProp(so, "quitButton");
        changed |= AddTouchTapEnlargeFromProp(so, "languageButton");
        changed |= AddTouchTapEnlargeFromProp(so, "soundBackButton");

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(title.gameObject.scene);
            EditorSceneManager.SaveScene(title.gameObject.scene);
            Debug.Log("[TouchHoverFixMigrator] 01_Title.unity のタッチホバー修正を保存しました。");
        }
        return changed;
    }

    private static bool FixGameScene()
    {
        var pauseMenu = Object.FindFirstObjectByType<PauseMenuUI>(FindObjectsInactive.Include);
        if (pauseMenu == null) return false;

        var so = new SerializedObject(pauseMenu);
        bool changed = false;
        changed |= AddTouchTapEnlargeFromProp(so, "confirmYesButton");
        changed |= AddTouchTapEnlargeFromProp(so, "confirmNoButton");
        changed |= AddTouchTapEnlargeFromProp(so, "soundBackButton");
        changed |= AddTouchTapEnlargeFromProp(so, "helpBackButton");

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(pauseMenu.gameObject.scene);
            EditorSceneManager.SaveScene(pauseMenu.gameObject.scene);
            Debug.Log("[TouchHoverFixMigrator] 05_Game.unity のタッチホバー修正を保存しました。");
        }
        return changed;
    }

    private static bool AddTouchTapEnlargeFromProp(SerializedObject so, string propName)
    {
        var prop = so.FindProperty(propName);
        if (prop == null)
        {
            Debug.LogWarning($"[TouchHoverFixMigrator] フィールド '{propName}' が見つかりません。");
            return false;
        }
        if (!(prop.objectReferenceValue is Button btn) || btn == null)
        {
            Debug.LogWarning($"[TouchHoverFixMigrator] フィールド '{propName}' が未アサインです。");
            return false;
        }
        return AddTouchTapEnlarge(btn.gameObject);
    }

    private static bool AddTouchTapEnlarge(GameObject go)
    {
        if (go.GetComponent<TouchTapEnlarge>() != null) return false;
        if (go.GetComponent<TouchTapToConfirm>() != null) return false; // 2段階確定式のボタンは対象外
        if (go.GetComponent<ButtonHoverEffect>() == null)
        {
            Debug.LogWarning($"[TouchHoverFixMigrator] {go.name} に ButtonHoverEffect が無いためスキップしました。");
            return false;
        }
        go.AddComponent<TouchTapEnlarge>();
        EditorUtility.SetDirty(go);
        Debug.Log($"[TouchHoverFixMigrator] {go.name} に TouchTapEnlarge を追加しました。");
        return true;
    }

    private static bool SetLockAfterClickFromProp(SerializedObject so, string propName)
    {
        var prop = so.FindProperty(propName);
        if (prop == null || !(prop.objectReferenceValue is Button btn) || btn == null) return false;
        return SetLockAfterClick(btn.gameObject);
    }

    private static bool SetLockAfterClick(GameObject go)
    {
        var hover = go.GetComponent<ButtonHoverEffect>();
        if (hover == null) return false;

        var hoverSo = new SerializedObject(hover);
        var lockProp = hoverSo.FindProperty("lockAfterClick");
        if (lockProp == null || lockProp.boolValue) return false;

        lockProp.boolValue = true;
        hoverSo.ApplyModifiedProperties();
        EditorUtility.SetDirty(hover);
        Debug.Log($"[TouchHoverFixMigrator] {go.name} の lockAfterClick を true にしました。");
        return true;
    }
}
