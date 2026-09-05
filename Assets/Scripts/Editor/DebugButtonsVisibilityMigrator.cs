using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>
/// 03_AreaSelectに散らばっている全デバッグボタンを一括表示/非表示にする
/// DebugButtonsVisibilityToggleコンポーネントを配置する。
/// 切り替えはInspectorのチェックボックス(showDebugButtons)で行う（キーボードショートカットは使わない）。
/// </summary>
public static class DebugButtonsVisibilityMigrator
{
    [MenuItem("Tools/AreaSelect/Setup Debug Buttons Visibility Toggle")]
    public static void Setup()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "03_AreaSelect")
        {
            Debug.LogWarning("[DebugButtonsVisibilityMigrator] 03_AreaSelectシーンを開いた状態で実行してください。現在: " + scene.name);
            return;
        }

        GameObject holderGo = FindDeepInScene(scene, "DebugButtonsVisibilityToggle");
        if (holderGo == null)
        {
            holderGo = new GameObject("DebugButtonsVisibilityToggle");
        }
        if (holderGo.GetComponent<DebugButtonsVisibilityToggle>() == null)
        {
            holderGo.AddComponent<DebugButtonsVisibilityToggle>();
        }

        EditorUtility.SetDirty(holderGo);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[DebugButtonsVisibilityMigrator] DebugButtonsVisibilityToggleを配置しました。Hierarchyで選択し、Inspectorの「Show Debug Buttons」チェックボックスで一括表示/非表示を切り替えてください。");
    }

    private static GameObject FindDeepInScene(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var t = FindDeep(root.transform, name);
            if (t != null) return t.gameObject;
        }
        return null;
    }

    private static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            var r = FindDeep(t.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }
}
