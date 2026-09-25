using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Linq;

/// <summary>
/// ★一時的な修復ツール（実行後は削除してよい）。02_Gem内のViewTutorialButton
/// （AreaSelect用、Gem画面には不要）を無効化する。03_AreaSelectには触れない。
/// </summary>
public static class GemSceneHideTutorialButtonTool
{
    private const string GemScenePath = "Assets/Scenes/02_Gem.unity";

    [MenuItem("Tools/★02_GemのViewTutorialButtonを非表示")]
    public static void HideTutorialButton()
    {
        var gemScene = EditorSceneManager.OpenScene(GemScenePath, OpenSceneMode.Single);

        Transform canvas = gemScene.GetRootGameObjects()
            .FirstOrDefault(g => g.name == "Canvas")?.transform;
        if (canvas == null)
        {
            Debug.LogError("[GemSceneHideTutorialButtonTool] Canvasが見つかりません。");
            return;
        }

        var tutorialButton = canvas.Find("ViewTutorialButton");
        if (tutorialButton == null)
        {
            Debug.LogWarning("[GemSceneHideTutorialButtonTool] ViewTutorialButtonが見つかりません。");
            return;
        }

        tutorialButton.gameObject.SetActive(false);
        EditorSceneManager.MarkSceneDirty(gemScene);
        EditorSceneManager.SaveScene(gemScene);

        Debug.Log("[GemSceneHideTutorialButtonTool] 完了。ViewTutorialButtonを無効化しました。");
    }
}
