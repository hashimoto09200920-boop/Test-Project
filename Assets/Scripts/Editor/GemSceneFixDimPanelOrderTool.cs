using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Linq;

/// <summary>
/// ★一時的な修復ツール（実行後は削除してよい）。02_Gem内でGemDimPanelがCanvasの
/// 一番手前（最後尾sibling）に来てしまい、ジェムパネル本体を覆い隠していた問題を修正する。
/// GemDimPanelを一番奥（最初のsibling）へ戻す。
/// </summary>
public static class GemSceneFixDimPanelOrderTool
{
    private const string GemScenePath = "Assets/Scenes/02_Gem.unity";

    [MenuItem("Tools/★02_GemのGemDimPanel描画順を修正")]
    public static void FixOrder()
    {
        var scene = EditorSceneManager.OpenScene(GemScenePath, OpenSceneMode.Single);

        Transform canvas = scene.GetRootGameObjects()
            .FirstOrDefault(g => g.name == "Canvas")?.transform;
        if (canvas == null)
        {
            Debug.LogError("[GemSceneFixDimPanelOrderTool] Canvasが見つかりません。");
            return;
        }

        var dimPanel = canvas.Find("GemDimPanel");
        if (dimPanel == null)
        {
            Debug.LogError("[GemSceneFixDimPanelOrderTool] GemDimPanelが見つかりません。");
            return;
        }

        dimPanel.SetAsFirstSibling();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[GemSceneFixDimPanelOrderTool] 完了。GemDimPanelを一番奥へ移動しました。");
    }
}
