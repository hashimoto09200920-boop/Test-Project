using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Linq;

/// <summary>
/// ★一時的な修復ツール（実行後は削除してよい）。03_AreaSelectのMain Cameraの背景色を
/// 02_Gem/04_Drinkと同じ黒に変更する。シーン遷移の境目で標準の青いデフォルト背景色が
/// 一瞬見えてしまう「青フラッシュ」を、どの方向の遷移でも起きないようにするための対策。
/// AreaSelectは元々暗い星空背景のため、黒に変えても見た目への影響はない。
/// </summary>
public static class FixAreaSelectCameraBgTool
{
    private const string ScenePath = "Assets/Scenes/03_AreaSelect.unity";

    [MenuItem("Tools/★03_AreaSelectのCamera背景色を黒に修正")]
    public static void FixCameraBackground()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        Camera cam = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            cam = root.GetComponentInChildren<Camera>(true);
            if (cam != null) break;
        }

        if (cam == null)
        {
            Debug.LogError("[FixAreaSelectCameraBgTool] Main Cameraが見つかりません。");
            return;
        }

        Debug.Log($"[FixAreaSelectCameraBgTool] {cam.gameObject.name}.backgroundColor: {cam.backgroundColor} -> black");
        cam.backgroundColor = Color.black;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[FixAreaSelectCameraBgTool] 完了。Main Cameraの背景色を黒にしました。");
    }
}
