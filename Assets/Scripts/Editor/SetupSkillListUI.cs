using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using Game.UI;

/// <summary>
/// 05_GameシーンにSkillListUIを自動セットアップするEditorスクリプト
/// </summary>
public class SetupSkillListUI : MonoBehaviour
{
    [MenuItem("Tools/Skills/Setup Skill List UI in Current Scene")]
    public static void SetupSkillList()
    {
        // DebugProgressOverlayを無効化
        DisableDebugProgressOverlay();


        // 既存のSkillListUIを検索
        SkillListUI existing = FindFirstObjectByType<SkillListUI>();
        if (existing != null)
        {
            EditorUtility.DisplayDialog("Info",
                "SkillListUIは既に存在します。\n手動で調整してください。",
                "OK");
            Selection.activeGameObject = existing.gameObject;
            return;
        }

        // Canvasを探す or 作成
        Canvas canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasObj = new GameObject("Canvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
            Debug.Log("[SetupSkillListUI] Created Canvas");
        }

        // SkillListUI GameObject作成
        GameObject skillListUIObj = new GameObject("SkillListUI");
        skillListUIObj.transform.SetParent(canvas.transform, false);
        SkillListUI skillListUI = skillListUIObj.AddComponent<SkillListUI>();

        // SkillListUI自体のRectTransformを設定（画面全体に広げる）
        RectTransform skillListRect = skillListUIObj.GetComponent<RectTransform>();
        if (skillListRect == null)
        {
            skillListRect = skillListUIObj.AddComponent<RectTransform>();
        }
        skillListRect.anchorMin = Vector2.zero;
        skillListRect.anchorMax = Vector2.one;
        skillListRect.sizeDelta = Vector2.zero;
        skillListRect.anchoredPosition = Vector2.zero;

        // TextMeshProUGUI作成
        GameObject textObj = new GameObject("SkillListText");
        textObj.transform.SetParent(skillListUIObj.transform, false);
        TextMeshProUGUI textComponent = textObj.AddComponent<TextMeshProUGUI>();

        // テキスト設定
        textComponent.text = "■カテゴリA\n(なし)\n\n■カテゴリB\n(なし)";
        textComponent.fontSize = 18;  // フォントサイズ（調整可能: 14〜24推奨）
        textComponent.alignment = TextAlignmentOptions.BottomRight;
        textComponent.color = Color.white;

        // RectTransform設定（右下配置）
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(1, 0);  // 右下
        textRect.anchorMax = new Vector2(1, 0);  // 右下
        textRect.pivot = new Vector2(1, 0);      // 右下
        textRect.anchoredPosition = new Vector2(-24, 24);  // 右下から少し内側
        textRect.sizeDelta = new Vector2(300, 200);

        // 背景を追加（見やすくするため）
        GameObject backObj = new GameObject("Background");
        backObj.transform.SetParent(skillListUIObj.transform, false);
        backObj.transform.SetSiblingIndex(0);  // テキストの後ろに配置
        Image backImage = backObj.AddComponent<Image>();
        backImage.color = new Color(0, 0, 0, 0.6f);  // 半透明黒

        RectTransform backRect = backObj.GetComponent<RectTransform>();
        backRect.anchorMin = new Vector2(1, 0);
        backRect.anchorMax = new Vector2(1, 0);
        backRect.pivot = new Vector2(1, 0);
        backRect.anchoredPosition = new Vector2(-24, 24);
        backRect.sizeDelta = new Vector2(320, 220);

        // SkillListUIの設定
        var skillListTextField = skillListUI.GetType().GetField("skillListText",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        skillListTextField?.SetValue(skillListUI, textComponent);

        // エディット画面では非表示にする（Playモード開始時にStart()で表示される）
        skillListUIObj.SetActive(false);

        EditorUtility.SetDirty(skillListUI);

        // 保存
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("[SetupSkillListUI] Setup complete!");
        EditorUtility.DisplayDialog("Complete",
            "SkillListUIのセットアップが完了しました！\n\n作成されたもの:\n- SkillListUI (Canvas配下)\n- 右下にスキルリストを表示",
            "OK");

        // 作成したオブジェクトを選択
        Selection.activeGameObject = skillListUIObj;
    }

    /// <summary>
    /// DebugProgressOverlayを無効化する
    /// </summary>
    private static void DisableDebugProgressOverlay()
    {
        // シーン内の全てのDebugProgressOverlayを検索
        DebugProgressOverlay[] overlays = FindObjectsByType<DebugProgressOverlay>(FindObjectsSortMode.None);

        if (overlays == null || overlays.Length == 0)
        {
            Debug.Log("[SetupSkillListUI] DebugProgressOverlay not found in scene");
            return;
        }

        foreach (var overlay in overlays)
        {
            if (overlay != null)
            {
                // GameObjectを無効化
                overlay.gameObject.SetActive(false);
                EditorUtility.SetDirty(overlay.gameObject);
                Debug.Log($"[SetupSkillListUI] Disabled DebugProgressOverlay: {overlay.gameObject.name}");
            }
        }

        // 保存
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
    }
}
