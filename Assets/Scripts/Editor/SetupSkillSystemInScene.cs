using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using Game.Skills;
using Game.UI;
using System.Linq;

/// <summary>
/// 05_Gameシーンにスキルシステムを自動セットアップするEditorスクリプト
/// </summary>
public class SetupSkillSystemInScene : MonoBehaviour
{
    [MenuItem("Tools/Skills/Setup Skill System in Current Scene")]
    public static void SetupSkillSystem()
    {
        // スキルDefinitionを読み込み
        SkillDefinition[] allSkills = Resources.LoadAll<SkillDefinition>("GameData/Skills");
        if (allSkills == null || allSkills.Length == 0)
        {
            EditorUtility.DisplayDialog("Error",
                "スキル定義が見つかりません。\n先に Tools > Skills > Create All Skill Definitions を実行してください。",
                "OK");
            return;
        }

        SkillDefinition[] categoryASkills = allSkills.Where(s => s.category == SkillCategory.CategoryA).ToArray();
        SkillDefinition[] categoryBSkills = allSkills.Where(s => s.category == SkillCategory.CategoryB).ToArray();

        Debug.Log($"[SetupSkillSystem] Found {categoryASkills.Length} Category A skills, {categoryBSkills.Length} Category B skills");

        // 1. SkillManagerの作成
        GameObject skillManagerObj = CreateSkillManager();

        // 2. SkillSelectionUIの作成
        GameObject skillSelectionUIObj = CreateSkillSelectionUI(categoryASkills, categoryBSkills);

        // 3. EnemySpawnerにSkillSelectionUIをアサイン
        AssignSkillSelectionUIToEnemySpawner(skillSelectionUIObj);

        // 保存
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("[SetupSkillSystem] Setup complete!");
        EditorUtility.DisplayDialog("Complete",
            "スキルシステムのセットアップが完了しました！\n\n作成されたもの:\n- SkillManager\n- SkillSelectionUI (Canvas配下)\n- EnemySpawnerへの参照設定",
            "OK");
    }

    private static GameObject CreateSkillManager()
    {
        // 既存のSkillManagerを検索
        SkillManager existing = FindFirstObjectByType<SkillManager>();
        if (existing != null)
        {
            Debug.Log("[SetupSkillSystem] SkillManager already exists, skipping creation");
            return existing.gameObject;
        }

        GameObject obj = new GameObject("SkillManager");
        obj.AddComponent<SkillManager>();

        Debug.Log("[SetupSkillSystem] Created SkillManager");
        return obj;
    }

    private static GameObject CreateSkillSelectionUI(SkillDefinition[] categoryASkills, SkillDefinition[] categoryBSkills)
    {
        // 既存のSkillSelectionUIを検索
        SkillSelectionUI existing = FindFirstObjectByType<SkillSelectionUI>();
        if (existing != null)
        {
            Debug.Log("[SetupSkillSystem] SkillSelectionUI already exists, updating references");
            existing.GetType().GetField("categoryASkills",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(existing, categoryASkills);
            existing.GetType().GetField("categoryBSkills",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(existing, categoryBSkills);
            EditorUtility.SetDirty(existing);
            return existing.gameObject;
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
            Debug.Log("[SetupSkillSystem] Created Canvas");
        }

        // SkillSelectionUI GameObject作成
        GameObject skillSelectionUIObj = new GameObject("SkillSelectionUI");
        skillSelectionUIObj.transform.SetParent(canvas.transform, false);
        SkillSelectionUI skillSelectionUI = skillSelectionUIObj.AddComponent<SkillSelectionUI>();

        // SelectionPanel作成
        GameObject panelObj = new GameObject("SelectionPanel");
        panelObj.transform.SetParent(skillSelectionUIObj.transform, false);
        Image panelImage = panelObj.AddComponent<Image>();
        panelImage.color = new Color(0, 0, 0, 0.8f);
        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.sizeDelta = Vector2.zero;

        // TitleText作成
        GameObject titleObj = new GameObject("TitleText");
        titleObj.transform.SetParent(panelObj.transform, false);
        TextMeshProUGUI titleText = titleObj.AddComponent<TextMeshProUGUI>();
        titleText.text = "スキル選択";
        titleText.fontSize = 48;
        titleText.alignment = TextAlignmentOptions.Center;
        RectTransform titleRect = titleObj.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 1f);
        titleRect.anchorMax = new Vector2(0.5f, 1f);
        titleRect.anchoredPosition = new Vector2(0, -100);
        titleRect.sizeDelta = new Vector2(800, 100);

        // SkillCard 3枚作成
        SkillCardUI[] skillCards = new SkillCardUI[3];
        for (int i = 0; i < 3; i++)
        {
            GameObject cardObj = CreateSkillCard($"SkillCard{i + 1}", i);
            cardObj.transform.SetParent(panelObj.transform, false);
            skillCards[i] = cardObj.GetComponent<SkillCardUI>();
        }

        // SkillSelectionUIの設定
        var selectionPanelField = skillSelectionUI.GetType().GetField("selectionPanel",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        selectionPanelField?.SetValue(skillSelectionUI, panelObj);

        var skillCardsField = skillSelectionUI.GetType().GetField("skillCards",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        skillCardsField?.SetValue(skillSelectionUI, skillCards);

        var titleTextField = skillSelectionUI.GetType().GetField("titleText",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        titleTextField?.SetValue(skillSelectionUI, titleText);

        var categoryAField = skillSelectionUI.GetType().GetField("categoryASkills",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        categoryAField?.SetValue(skillSelectionUI, categoryASkills);

        var categoryBField = skillSelectionUI.GetType().GetField("categoryBSkills",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        categoryBField?.SetValue(skillSelectionUI, categoryBSkills);

        // 初期状態は非表示
        panelObj.SetActive(false);

        EditorUtility.SetDirty(skillSelectionUI);
        Debug.Log("[SetupSkillSystem] Created SkillSelectionUI with 3 cards");

        return skillSelectionUIObj;
    }

    private static GameObject CreateSkillCard(string name, int index)
    {
        // カード本体
        GameObject cardObj = new GameObject(name);
        Image cardImage = cardObj.AddComponent<Image>();
        cardImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
        Button cardButton = cardObj.AddComponent<Button>();

        RectTransform cardRect = cardObj.GetComponent<RectTransform>();
        cardRect.anchorMin = new Vector2(0.5f, 0.5f);
        cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.sizeDelta = new Vector2(300, 400);
        // 3枚を横並び（-400, 0, 400）
        cardRect.anchoredPosition = new Vector2((index - 1) * 400, 0);

        // SkillNameText
        GameObject nameObj = new GameObject("SkillNameText");
        nameObj.transform.SetParent(cardObj.transform, false);
        TextMeshProUGUI nameText = nameObj.AddComponent<TextMeshProUGUI>();
        nameText.text = "スキル名";
        nameText.fontSize = 32;
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.color = Color.white;
        RectTransform nameRect = nameObj.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0, 1);
        nameRect.anchorMax = new Vector2(1, 1);
        nameRect.anchoredPosition = new Vector2(0, -50);
        nameRect.sizeDelta = new Vector2(-40, 80);

        // DescriptionText
        GameObject descObj = new GameObject("DescriptionText");
        descObj.transform.SetParent(cardObj.transform, false);
        TextMeshProUGUI descText = descObj.AddComponent<TextMeshProUGUI>();
        descText.text = "説明文";
        descText.fontSize = 20;
        descText.alignment = TextAlignmentOptions.Center;
        descText.color = Color.white;
        RectTransform descRect = descObj.GetComponent<RectTransform>();
        descRect.anchorMin = new Vector2(0, 0.5f);
        descRect.anchorMax = new Vector2(1, 0.5f);
        descRect.anchoredPosition = new Vector2(0, 0);
        descRect.sizeDelta = new Vector2(-40, 150);

        // EffectValueText
        GameObject valueObj = new GameObject("EffectValueText");
        valueObj.transform.SetParent(cardObj.transform, false);
        TextMeshProUGUI valueText = valueObj.AddComponent<TextMeshProUGUI>();
        valueText.text = "+5";
        valueText.fontSize = 40;
        valueText.alignment = TextAlignmentOptions.Center;
        valueText.color = Color.green;
        RectTransform valueRect = valueObj.GetComponent<RectTransform>();
        valueRect.anchorMin = new Vector2(0, 0);
        valueRect.anchorMax = new Vector2(1, 0);
        valueRect.anchoredPosition = new Vector2(0, 50);
        valueRect.sizeDelta = new Vector2(-40, 80);

        // SkillCardUIコンポーネント
        SkillCardUI skillCardUI = cardObj.AddComponent<SkillCardUI>();
        var buttonField = skillCardUI.GetType().GetField("button",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        buttonField?.SetValue(skillCardUI, cardButton);

        var nameField = skillCardUI.GetType().GetField("skillNameText",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        nameField?.SetValue(skillCardUI, nameText);

        var descField = skillCardUI.GetType().GetField("descriptionText",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        descField?.SetValue(skillCardUI, descText);

        var valueField = skillCardUI.GetType().GetField("effectValueText",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        valueField?.SetValue(skillCardUI, valueText);

        var bgField = skillCardUI.GetType().GetField("backgroundImage",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        bgField?.SetValue(skillCardUI, cardImage);

        EditorUtility.SetDirty(skillCardUI);

        return cardObj;
    }

    private static void AssignSkillSelectionUIToEnemySpawner(GameObject skillSelectionUIObj)
    {
        EnemySpawner enemySpawner = FindFirstObjectByType<EnemySpawner>();
        if (enemySpawner == null)
        {
            Debug.LogWarning("[SetupSkillSystem] EnemySpawner not found in scene. Please assign SkillSelectionUI manually.");
            return;
        }

        var skillSelectionUI = skillSelectionUIObj.GetComponent<SkillSelectionUI>();
        var field = enemySpawner.GetType().GetField("skillSelectionUI",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        if (field != null)
        {
            field.SetValue(enemySpawner, skillSelectionUI);
            EditorUtility.SetDirty(enemySpawner);
            Debug.Log("[SetupSkillSystem] Assigned SkillSelectionUI to EnemySpawner");
        }
        else
        {
            Debug.LogWarning("[SetupSkillSystem] Could not find skillSelectionUI field in EnemySpawner");
        }
    }
}
