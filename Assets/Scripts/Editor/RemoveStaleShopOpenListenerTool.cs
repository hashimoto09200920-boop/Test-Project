using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ★一時的な修復ツール（実行後は削除してよい）。03_AreaSelectのShopボタンに残っている、
/// Inspectorで直接設定されたShopUI.Open()への古いonClickリスナーを削除する。
/// GemManagementButtonで見つかった同種のバグと同じ対処（二重発火防止）。
/// コードは既にOnClickShop()経由でシーン遷移(04_Drink)するよう変更済みのため、
/// この直接呼び出しが残っていると旧シーン内表示と新シーン遷移が同時に走ってしまう。
/// </summary>
public static class RemoveStaleShopOpenListenerTool
{
    private const string ScenePath = "Assets/Scenes/03_AreaSelect.unity";

    [MenuItem("Tools/★古いShopUI.Open直接呼び出しを削除")]
    public static void RemoveStaleListener()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        int removed = 0;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var btn in root.GetComponentsInChildren<Button>(true))
            {
                for (int i = btn.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                {
                    var methodName = btn.onClick.GetPersistentMethodName(i);
                    var target = btn.onClick.GetPersistentTarget(i);
                    if (methodName == "Open" && target != null && target.GetType().Name == "ShopUI")
                    {
                        Debug.Log($"[RemoveStaleShopOpenListenerTool] {btn.gameObject.name} から ShopUI.Open() への直接リスナーを削除します。");
                        UnityEventTools.RemovePersistentListener(btn.onClick, i);
                        removed++;
                    }
                }
            }
        }

        if (removed == 0)
        {
            Debug.LogWarning("[RemoveStaleShopOpenListenerTool] 該当するリスナーが見つかりませんでした。");
            return;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[RemoveStaleShopOpenListenerTool] 完了。{removed}件削除し、シーンを保存しました。");
    }
}
