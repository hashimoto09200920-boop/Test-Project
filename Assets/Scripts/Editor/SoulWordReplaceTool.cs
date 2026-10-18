using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 05_Game の画面に出る文言の「魂」を「ソウル」に置き換える（2026/10/17）。
/// 対象：シーン内の全コンポーネントの文字列の項目（HELPの予備の文言・チュートリアルの予備の文言・キーワードの色分け・古いHELP本文の文字など）。
/// エディタ上のメモ（memo）は画面に出ないので対象外。置き換えた箇所はすべてConsoleに出す。何度実行しても同じ結果。
/// </summary>
public static class SoulWordReplaceTool
{
    private const string ScenePath = "Assets/Scenes/05_Game.unity";

    [MenuItem("Tools/文言/「魂」を「ソウル」に置き換える（05_Game）")]
    private static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool wasLoaded = scene.IsValid() && scene.isLoaded;
        if (!wasLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        int count = 0;
        try
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null) continue;
                    var so = new SerializedObject(mb);
                    var p = so.GetIterator();
                    bool changed = false;
                    while (p.Next(true))
                    {
                        if (p.propertyType != SerializedPropertyType.String) continue;
                        if (p.name == "memo") continue; // エディタ上のメモは画面に出ない
                        string v = p.stringValue;
                        if (string.IsNullOrEmpty(v) || !v.Contains("魂")) continue;
                        p.stringValue = v.Replace("魂", "ソウル");
                        changed = true;
                        count++;
                        Debug.Log($"[SoulWordReplaceTool] {mb.gameObject.name} > {mb.GetType().Name} > {p.propertyPath}");
                    }
                    if (changed) { so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(mb); }
                }
            }
            if (count > 0) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
            Debug.Log($"[SoulWordReplaceTool] 05_Game の「魂」を「ソウル」に置き換えて保存しました（{count}か所）");
        }
        finally
        {
            if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
        }
    }
}
