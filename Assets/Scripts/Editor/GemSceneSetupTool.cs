using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;

/// <summary>
/// ★一時的なセットアップツール（02_Gemシーンの構築が完了したら削除してよい）。
/// 03_AreaSelectの内容には一切触れず（削除・保存もしない）、Canvasを複製して
/// 02_Gemシーンを新規に構築する。AreaSelect側は動作確認が取れるまでそのまま残す。
/// </summary>
public static class GemSceneSetupTool
{
    private const string AreaSelectScenePath = "Assets/Scenes/03_AreaSelect.unity";
    private const string GemScenePath = "Assets/Scenes/02_Gem.unity";

    [MenuItem("Tools/★02_Gemシーンを構築")]
    public static void SetupGemScene()
    {
        // 03_AreaSelectを読み取り専用ソースとしてSingleで開く（保存はしない）
        var areaSelectScene = EditorSceneManager.OpenScene(AreaSelectScenePath, OpenSceneMode.Single);

        GameObject originalCanvas = areaSelectScene.GetRootGameObjects()
            .FirstOrDefault(g => g.name == "Canvas");
        GameObject originalGoldManager = areaSelectScene.GetRootGameObjects()
            .FirstOrDefault(g => g.name == "GoldManager");

        if (originalCanvas == null)
        {
            Debug.LogError("[GemSceneSetupTool] 03_AreaSelectにCanvasが見つかりません。中断します。");
            return;
        }
        if (originalCanvas.transform.Find("GemManagementUI") == null)
        {
            Debug.LogError("[GemSceneSetupTool] Canvas内にGemManagementUIが見つかりません。中断します。");
            return;
        }

        // 02_Gemを新規シーンとして作成（既存の03_AreaSelectをSingleで開いたことでUntitledな追加シーンになる）
        var gemScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

        // Canvasを複製して02_Gemへ移動
        var newCanvas = Object.Instantiate(originalCanvas);
        newCanvas.name = "Canvas";
        SceneManager.MoveGameObjectToScene(newCanvas, gemScene);

        if (newCanvas.transform.Find("GemManagementUI") == null)
        {
            Debug.LogError("[GemSceneSetupTool] 複製後にGemManagementUIが見つかりません。中断します（保存はしていません）。");
            return;
        }

        // UI_Root > Panels > AreaPanel > GemDimPanel を救出してCanvas直下へ
        Transform uiRoot = newCanvas.transform.Find("UI_Root");
        if (uiRoot != null)
        {
            var panels = uiRoot.Find("Panels");
            var areaPanel = panels != null ? panels.Find("AreaPanel") : null;
            var gemDimPanel = areaPanel != null ? areaPanel.Find("GemDimPanel") : null;

            if (gemDimPanel != null)
            {
                gemDimPanel.SetParent(newCanvas.transform, true);
                Debug.Log("[GemSceneSetupTool] GemDimPanelをCanvas直下へ退避しました。");
            }
            else
            {
                Debug.LogWarning("[GemSceneSetupTool] GemDimPanelが見つかりませんでした。UI_Rootは無効化せず残します。手動確認が必要です。");
                uiRoot = null; // 無効化をスキップさせる
            }
        }
        else
        {
            Debug.LogWarning("[GemSceneSetupTool] UI_Rootが見つかりませんでした。");
        }

        if (uiRoot != null)
        {
            uiRoot.gameObject.SetActive(false);
            Debug.Log("[GemSceneSetupTool] UI_Rootを無効化しました（削除はしていません）。");
        }

        // GoldManagerを複製して移動
        if (originalGoldManager != null)
        {
            var newGoldManager = Object.Instantiate(originalGoldManager);
            newGoldManager.name = "GoldManager";
            SceneManager.MoveGameObjectToScene(newGoldManager, gemScene);
        }
        else
        {
            Debug.LogWarning("[GemSceneSetupTool] GoldManagerが見つかりませんでした。");
        }

        // 02_Gemシーンを保存（新規パスとして）
        bool saved = EditorSceneManager.SaveScene(gemScene, GemScenePath);
        if (!saved)
        {
            Debug.LogError("[GemSceneSetupTool] 02_Gemの保存に失敗しました。");
            return;
        }

        // Build Settingsに登録（重複登録は避ける）
        var scenes = EditorBuildSettings.scenes.ToList();
        if (!scenes.Any(s => s.path == GemScenePath))
        {
            scenes.Add(new EditorBuildSettingsScene(GemScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[GemSceneSetupTool] Build Settingsに02_Gemを追加しました。");
        }

        Debug.Log("[GemSceneSetupTool] 完了。03_AreaSelectは一切変更していません。");
    }
}
