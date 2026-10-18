using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 02_Gem・04_Drink の右上の表示（StaminaHUD・GoldHUD・InfiniteStoneHUD）の配置の基準を「画面の右上」から「画面の上の中央」に変える（2026/10/18）。
/// EXITボタン（ShopPanel > HeaderRow > CloseButton など）は中央のパネル基準で動くため、右上基準のままだと画面の幅が狭いほどEXITに近づき、16:9では重なっていた。
/// 位置は、20:9のスマホ（キャンバスの幅2400）での見た目が今とまったく同じになる値に換算する。
/// 何度実行しても安全（既に中央基準になっているものは変えない）。
/// </summary>
public static class TopRightHudReanchorTool
{
    private static readonly string[] ScenePaths = { "Assets/Scenes/02_Gem.unity", "Assets/Scenes/04_Drink.unity" };
    private static readonly string[] HudNames = { "StaminaHUD", "GoldHUD", "InfiniteStoneHUD" };
    private const float ReferenceCanvasWidth = 2400f; // 20:9・高さ1080のキャンバスの幅（この幅での見た目を保つ）

    [MenuItem("Tools/画面比率/ジェム・ドリンク画面の右上の表示をEXITに合わせて配置し直す")]
    private static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (string path in ScenePaths)
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                int changed = 0;
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var rt in root.GetComponentsInChildren<RectTransform>(true))
                    {
                        if (System.Array.IndexOf(HudNames, rt.name.Trim()) < 0) continue;
                        if (!Mathf.Approximately(rt.anchorMin.x, rt.anchorMax.x) || Mathf.Approximately(rt.anchorMin.x, 0.5f)) continue; // 横に伸びる設定・中央基準済みは対象外
                        float oldAnchorX = rt.anchorMin.x;
                        Vector2 pos = rt.anchoredPosition;
                        float newX = pos.x + (oldAnchorX - 0.5f) * ReferenceCanvasWidth;
                        rt.anchorMin = new Vector2(0.5f, rt.anchorMin.y);
                        rt.anchorMax = new Vector2(0.5f, rt.anchorMax.y);
                        rt.anchoredPosition = new Vector2(newX, pos.y);
                        EditorUtility.SetDirty(rt);
                        changed++;
                        Debug.Log($"[TopRightHudReanchorTool] {scene.name} > {GetPath(rt)}：アンカーX {oldAnchorX}→0.5、位置X {pos.x}→{newX}");
                    }
                }
                if (changed > 0) { EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
                Debug.Log($"[TopRightHudReanchorTool] {scene.name}：{changed}個を配置し直して保存しました");
            }
        }
        finally
        {
            if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
    }

    private static string GetPath(Transform t)
    {
        string p = t.name;
        while (t.parent != null) { t = t.parent; p = t.name + " > " + p; }
        return p;
    }
}
