using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// StaminaManagerのスタミナ上限（maxStamina）を、StaminaManagerを置いている全シーンで同じ値に揃える。
/// 3シーンは同じ保存データ（PlayerPrefs）を共有するため、1シーンだけ変えると画面ごとに上限がズレる。
/// </summary>
public static class StaminaMaxSetupTool
{
    private const int NewMax = 3;
    private static readonly string[] ScenePaths =
    {
        "Assets/Scenes/02_Gem.unity",
        "Assets/Scenes/03_AreaSelect.unity",
        "Assets/Scenes/04_Drink.unity",
    };

    [MenuItem("Tools/AreaSelect/スタミナ上限を3にする（02_Gem・03_AreaSelect・04_Drink）")]
    private static void Apply()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        foreach (string path in ScenePaths)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

            int changed = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var sm in root.GetComponentsInChildren<StaminaManager>(true))
                {
                    var so = new SerializedObject(sm);
                    var p = so.FindProperty("maxStamina");
                    if (p.intValue == NewMax) continue;
                    Debug.Log($"[StaminaMaxSetupTool] {scene.name} / {sm.gameObject.name}：maxStamina {p.intValue} → {NewMax}");
                    p.intValue = NewMax;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    changed++;
                }
            }

            if (changed > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            else
            {
                Debug.Log($"[StaminaMaxSetupTool] {scene.name}：変更なし（既に{NewMax}、またはStaminaManagerなし）");
            }

            if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
        }
        Debug.Log("[StaminaMaxSetupTool] 完了");
    }
}
