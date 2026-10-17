using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 04_Drink のドリンクカードの見本（DrinkCardTemplate）で、ドリンク名・ゴールドアイコン・価格を少し上げる。
/// 行（NamePriceRow）ごと上げ、下のネオンの線（FX_NameLine）だけ元の画面上の位置に戻す。
/// 何度実行しても同じ結果（値をそのまま入れる）。
/// </summary>
public static class DrinkNameRowRaiseTool
{
    private const string ScenePath = "Assets/Scenes/04_Drink.unity";
    private const float RowY = -8f;   // 従来 -12 → 4px上
    private const float LineY = -2f;  // 従来 2（行が4px上がった分を戻し、線の画面上の位置は従来どおり）

    [MenuItem("Tools/ドリンク画面/ドリンク名・価格の位置を少し上げる（04_Drink）")]
    private static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool wasLoaded = scene.IsValid() && scene.isLoaded;
        if (!wasLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            int n = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name != "NamePriceRow" || t.parent == null || t.parent.name != "DrinkCardTemplate") continue;
                    var row = (RectTransform)t;
                    row.anchoredPosition = new Vector2(row.anchoredPosition.x, RowY);
                    EditorUtility.SetDirty(row);
                    var line = row.Find("FX_NameLine") as RectTransform;
                    if (line != null) { line.anchoredPosition = new Vector2(line.anchoredPosition.x, LineY); EditorUtility.SetDirty(line); }
                    else Debug.LogWarning("[DrinkNameRowRaiseTool] FX_NameLine が見つかりません（線は動かしていません）");
                    n++;
                }
            }
            if (n == 0) { Debug.LogError("[DrinkNameRowRaiseTool] 04_Drink に DrinkCardTemplate/NamePriceRow が見つかりません"); return; }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[DrinkNameRowRaiseTool] ドリンク名・ゴールドアイコン・価格を4px上げて保存しました（NamePriceRow Pos Y={RowY}、FX_NameLine Pos Y={LineY}）");
        }
        finally
        {
            if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
        }
    }
}
