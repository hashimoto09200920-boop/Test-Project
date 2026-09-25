using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;

/// <summary>
/// ★一時的な修復ツール（実行後は削除してよい）。02_Gemシーンに03_AreaSelectの
/// Main Cameraを複製して追加する（「No cameras rendering」対策）。
/// 03_AreaSelectには一切触れず、保存もしない。
/// </summary>
public static class GemSceneAddCameraTool
{
    private const string AreaSelectScenePath = "Assets/Scenes/03_AreaSelect.unity";
    private const string GemScenePath = "Assets/Scenes/02_Gem.unity";

    [MenuItem("Tools/★02_GemにCameraを追加")]
    public static void AddCamera()
    {
        var areaSelectScene = EditorSceneManager.OpenScene(AreaSelectScenePath, OpenSceneMode.Single);
        var gemScene = EditorSceneManager.OpenScene(GemScenePath, OpenSceneMode.Additive);

        if (gemScene.GetRootGameObjects().Any(g => g.GetComponentInChildren<Camera>(true) != null))
        {
            Debug.LogWarning("[GemSceneAddCameraTool] 02_Gemには既にCameraがあります。中断します。");
            return;
        }

        var originalCamera = areaSelectScene.GetRootGameObjects()
            .FirstOrDefault(g => g.name == "Main Camera");

        if (originalCamera == null)
        {
            Debug.LogError("[GemSceneAddCameraTool] 03_AreaSelectにMain Cameraが見つかりません。中断します。");
            return;
        }

        var newCamera = Object.Instantiate(originalCamera);
        newCamera.name = "Main Camera";
        SceneManager.MoveGameObjectToScene(newCamera, gemScene);

        EditorSceneManager.MarkSceneDirty(gemScene);
        EditorSceneManager.SaveScene(gemScene);

        Debug.Log("[GemSceneAddCameraTool] 完了。Main Cameraを02_Gemに追加しました。03_AreaSelectは変更していません。");
    }
}
