using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// GoldManagerと同じ階層にStaminaManagerを配置する。GoldManager/InfiniteStoneManagerと同じく
/// 03_AreaSelect・05_Gameの両シーンで個別に実行する想定（DontDestroyOnLoadはしない）。
/// StaminaHUD（見た目）はアイコンデザイン確定後に別途セットアップする。
/// </summary>
public static class StaminaSetupMigrator
{
    [MenuItem("Tools/AreaSelect/Setup Stamina Manager (現在のシーン)")]
    public static void SetupManagerOnly()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject goldManagerGo = FindDeepInScene(scene, "GoldManager");
        if (goldManagerGo == null)
        {
            Debug.LogError("[StaminaSetupMigrator] GoldManagerが見つかりません。現在のシーン: " + scene.name);
            return;
        }

        GameObject staminaManagerGo = FindDeepInScene(scene, "StaminaManager");
        if (staminaManagerGo == null)
        {
            staminaManagerGo = new GameObject("StaminaManager");
            staminaManagerGo.transform.SetParent(goldManagerGo.transform.parent, false);
        }
        if (staminaManagerGo.GetComponent<StaminaManager>() == null)
            staminaManagerGo.AddComponent<StaminaManager>();

        EditorUtility.SetDirty(staminaManagerGo);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[StaminaSetupMigrator] StaminaManagerを{scene.name}に配置しました。");
    }

    /// <summary>
    /// StaminaHUDを配置し、既存のGoldHUD/InfiniteStoneHUDを1行分ずつ下にずらす。
    /// 最終順番：一番上にStamina、真ん中にGold、一番下に無限化石（ユーザー確定仕様）。
    /// 03_AreaSelectシーンで実行する想定。再実行しても安全（既存分は位置を再計算するだけ）。
    /// </summary>
    [MenuItem("Tools/AreaSelect/Setup Stamina HUD (Stamina上/Gold中/無限化石下)")]
    public static void SetupHUD()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "03_AreaSelect")
        {
            Debug.LogWarning("[StaminaSetupMigrator] 03_AreaSelectシーンを開いた状態で実行してください。現在: " + scene.name);
            return;
        }

        GameObject goldHudGo = FindDeepInScene(scene, "GoldHUD");
        if (goldHudGo == null)
        {
            Debug.LogError("[StaminaSetupMigrator] GoldHUDが見つかりません。");
            return;
        }
        GameObject stoneHudGo = FindDeepInScene(scene, "InfiniteStoneHUD");
        if (stoneHudGo == null)
        {
            Debug.LogError("[StaminaSetupMigrator] InfiniteStoneHUDが見つかりません。先にInfiniteStoneのセットアップを済ませてください。");
            return;
        }

        var goldRect = goldHudGo.GetComponent<RectTransform>();
        var stoneRect = stoneHudGo.GetComponent<RectTransform>();
        if (goldRect == null || stoneRect == null)
        {
            Debug.LogError("[StaminaSetupMigrator] GoldHUD/InfiniteStoneHUDにRectTransformがありません。");
            return;
        }

        float rowHeight = goldRect.sizeDelta.y;

        // ★再実行時の二重ずらしを防ぐため、既にStaminaHUDが存在するならその位置を
        //   「一番上のスロット」の基準として使い回す（無ければ今のGoldHUDの位置を初回の基準にする）。
        GameObject existingHud = FindDeepInScene(scene, "StaminaHUD");
        Vector2 topSlotPos = existingHud != null
            ? existingHud.GetComponent<RectTransform>().anchoredPosition
            : goldRect.anchoredPosition;
        if (existingHud != null) Object.DestroyImmediate(existingHud);

        // ---- StaminaManager（GoldManagerと同じ階層） ----
        SetupManagerOnly();

        GameObject hudGo = new GameObject("StaminaHUD", typeof(RectTransform));
        hudGo.transform.SetParent(goldHudGo.transform.parent, false);

        var hudRect = hudGo.GetComponent<RectTransform>();
        hudRect.anchorMin = goldRect.anchorMin;
        hudRect.anchorMax = goldRect.anchorMax;
        hudRect.pivot = goldRect.pivot;
        hudRect.sizeDelta = goldRect.sizeDelta;
        hudRect.anchoredPosition = topSlotPos;

        // アイコン（GoldIconと同じ相対位置）
        var goldIconTrans = goldHudGo.transform.Find("GoldIcon");
        var iconGo = new GameObject("StaminaIcon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(hudGo.transform, false);
        var iconRect = iconGo.GetComponent<RectTransform>();
        if (goldIconTrans != null)
        {
            var goldIconRect = (RectTransform)goldIconTrans;
            iconRect.anchorMin = goldIconRect.anchorMin;
            iconRect.anchorMax = goldIconRect.anchorMax;
            iconRect.pivot = goldIconRect.pivot;
            iconRect.anchoredPosition = goldIconRect.anchoredPosition;
            iconRect.sizeDelta = goldIconRect.sizeDelta;
        }
        else
        {
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(50f, 0f);
            iconRect.sizeDelta = new Vector2(40f, 40f);
        }
        var iconImg = iconGo.GetComponent<Image>();
        iconImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/AreaSelect/StaminaIcon2.png");
        iconImg.preserveAspect = true;

        // 個数テキスト（GoldTextと同じ相対位置）
        var goldTextTrans = goldHudGo.transform.Find("GoldText");
        var textGo = new GameObject("StaminaCountText", typeof(RectTransform));
        textGo.transform.SetParent(hudGo.transform, false);
        var textRect = textGo.GetComponent<RectTransform>();
        TextMeshProUGUI srcTmp = goldTextTrans != null ? goldTextTrans.GetComponent<TextMeshProUGUI>() : null;
        if (goldTextTrans != null)
        {
            var goldTextRect = (RectTransform)goldTextTrans;
            textRect.anchorMin = goldTextRect.anchorMin;
            textRect.anchorMax = goldTextRect.anchorMax;
            textRect.pivot = goldTextRect.pivot;
            textRect.anchoredPosition = goldTextRect.anchoredPosition;
            textRect.sizeDelta = goldTextRect.sizeDelta;
        }
        else
        {
            textRect.anchorMin = new Vector2(0f, 0.5f);
            textRect.anchorMax = new Vector2(0f, 0.5f);
            textRect.anchoredPosition = new Vector2(150f, 0f);
            textRect.sizeDelta = new Vector2(100f, 50f);
        }
        var countTmp = textGo.AddComponent<TextMeshProUGUI>();
        countTmp.text = "5/5";
        if (srcTmp != null)
        {
            countTmp.font = srcTmp.font;
            countTmp.fontSize = srcTmp.fontSize;
            countTmp.color = srcTmp.color;
            countTmp.alignment = srcTmp.alignment;
        }

        // カウントダウンテキスト（個数テキストのすぐ下、小さいフォント）
        var countdownGo = new GameObject("StaminaCountdownText", typeof(RectTransform));
        countdownGo.transform.SetParent(hudGo.transform, false);
        var countdownRect = countdownGo.GetComponent<RectTransform>();
        countdownRect.anchorMin = textRect.anchorMin;
        countdownRect.anchorMax = textRect.anchorMax;
        countdownRect.pivot = textRect.pivot;
        countdownRect.anchoredPosition = textRect.anchoredPosition - new Vector2(0f, textRect.sizeDelta.y * 0.6f);
        countdownRect.sizeDelta = new Vector2(textRect.sizeDelta.x + 40f, 26f);
        var countdownTmp = countdownGo.AddComponent<TextMeshProUGUI>();
        countdownTmp.text = "";
        countdownTmp.fontSize = (srcTmp != null ? srcTmp.fontSize : 24f) * 0.65f;
        countdownTmp.color = new Color(0.7f, 0.7f, 0.8f, 1f);
        countdownTmp.alignment = srcTmp != null ? srcTmp.alignment : TextAlignmentOptions.Left;
        if (srcTmp != null) countdownTmp.font = srcTmp.font;

        var hud = hudGo.AddComponent<StaminaHUD>();
        var so = new SerializedObject(hud);
        so.FindProperty("staminaIcon").objectReferenceValue = iconImg;
        so.FindProperty("staminaCountText").objectReferenceValue = countTmp;
        so.FindProperty("countdownText").objectReferenceValue = countdownTmp;
        so.ApplyModifiedProperties();

        // ---- 既存のGold/無限化石を1行ずつ下にずらす ----
        goldRect.anchoredPosition = topSlotPos - new Vector2(0f, rowHeight);
        stoneRect.anchoredPosition = topSlotPos - new Vector2(0f, rowHeight * 2f);

        EditorUtility.SetDirty(hudGo);
        EditorUtility.SetDirty(goldHudGo);
        EditorUtility.SetDirty(stoneHudGo);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[StaminaSetupMigrator] StaminaHUDを一番上に配置し、Gold/無限化石を1行ずつ下にずらしました。");
    }

    private static GameObject FindDeepInScene(Scene scene, string name)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var t = FindDeep(root.transform, name);
            if (t != null) return t.gameObject;
        }
        return null;
    }

    private static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            var r = FindDeep(t.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }
}
