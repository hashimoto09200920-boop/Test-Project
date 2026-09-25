using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ★一時的な修復ツール（実行後は削除してよい）。03_AreaSelect内のボタンに残っている
/// 「GemManagementUI.Openを直接呼ぶ」古いPersistent Listenerだけを削除する。
/// コード側(AreaSelectMenu.OnClickGemManagement)の新しいシーン遷移処理と二重発火していたため。
/// </summary>
public static class RemoveStaleGemOpenListenerTool
{
    private const string AreaSelectScenePath = "Assets/Scenes/03_AreaSelect.unity";

    [MenuItem("Tools/★古いGemManagementUI.Open直接呼び出しを削除")]
    public static void RemoveStaleListener()
    {
        var scene = EditorSceneManager.OpenScene(AreaSelectScenePath, OpenSceneMode.Single);

        var buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        int removedCount = 0;

        foreach (var button in buttons)
        {
            for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
            {
                var target = button.onClick.GetPersistentTarget(i);
                var methodName = button.onClick.GetPersistentMethodName(i);

                if (target != null && target.GetType().Name == "GemManagementUI" && methodName == "Open")
                {
                    Debug.Log($"[RemoveStaleGemOpenListenerTool] {button.name} から GemManagementUI.Open の呼び出しを削除します。");
                    UnityEventTools.RemovePersistentListener(button.onClick, i);
                    removedCount++;
                }
            }
        }

        if (removedCount == 0)
        {
            Debug.LogWarning("[RemoveStaleGemOpenListenerTool] 該当するリスナーが見つかりませんでした。");
            return;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[RemoveStaleGemOpenListenerTool] 完了。{removedCount}件削除して保存しました。");
    }
}
