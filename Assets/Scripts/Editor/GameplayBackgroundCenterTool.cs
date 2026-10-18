using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 05_Game > Gameplay > Background（一番後ろの灰色グラデーション背景）の位置Xを0にする（2026/10/18）。
/// BackgroundFitter2D（Cover）で横幅が画面ちょうどに拡大されるため、位置Xが-0.1だと右端に0.1の隙間ができ、
/// 半透明の背景が重なるArea10ファイナルステージなどで、カメラの背景色が右端に帯のように見えていた。
/// 何度実行しても同じ結果。
/// </summary>
public static class GameplayBackgroundCenterTool
{
    private const string ScenePath = "Assets/Scenes/05_Game.unity";

    [MenuItem("Tools/画面比率/一番後ろの背景（Gameplay > Background）の位置を中央に揃える")]
    private static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool wasLoaded = scene.IsValid() && scene.isLoaded;
        if (!wasLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            Transform bg = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name != "Gameplay") continue;
                bg = root.transform.Find("Background");
                if (bg != null) break;
            }
            if (bg == null) { Debug.LogError("[GameplayBackgroundCenterTool] 05_Game > Gameplay > Background が見つかりません"); return; }

            Vector3 old = bg.localPosition;
            bg.localPosition = new Vector3(0f, old.y, old.z);
            EditorUtility.SetDirty(bg);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[GameplayBackgroundCenterTool] 05_Game > Gameplay > Background の位置X：{old.x} → 0（Y={old.y}はそのまま）。保存しました");
        }
        finally
        {
            if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
        }
    }
}
