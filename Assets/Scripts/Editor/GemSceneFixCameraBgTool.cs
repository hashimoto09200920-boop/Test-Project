using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Linq;

/// <summary>
/// ★一時的な修復ツール（実行後は削除してよい）。02_GemのMain Cameraの背景色を
/// デフォルトの青から黒に変更する（シーン切り替えの一瞬の隙間で見える色を黒幕と
/// 同じ色にして目立たなくする）。
/// </summary>
public static class GemSceneFixCameraBgTool
{
    private const string GemScenePath = "Assets/Scenes/02_Gem.unity";

    [MenuItem("Tools/★02_GemのCamera背景色を黒に修正")]
    public static void FixCameraBackground()
    {
        var scene = EditorSceneManager.OpenScene(GemScenePath, OpenSceneMode.Single);

        var cameraObj = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Main Camera");
        if (cameraObj == null)
        {
            Debug.LogError("[GemSceneFixCameraBgTool] Main Cameraが見つかりません。");
            return;
        }

        var cam = cameraObj.GetComponent<Camera>();
        if (cam == null)
        {
            Debug.LogError("[GemSceneFixCameraBgTool] Cameraコンポーネントが見つかりません。");
            return;
        }

        cam.backgroundColor = Color.black;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[GemSceneFixCameraBgTool] 完了。Main Cameraの背景色を黒にしました。");
    }
}
