using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// 03_AreaSelectシーンに「無限化の石」用のInfiniteStoneManager（GoldManagerと同じ役割）と
/// InfiniteStoneHUD（GoldHUDのすぐ下に表示するアイコン+個数表示）をセットアップする。
/// </summary>
public static class InfiniteStoneSetupMigrator
{
    /// <summary>
    /// GoldManagerと同じ階層にInfiniteStoneManagerを配置する。05_Game/03_AreaSelectの
    /// 両方でGoldManagerが個別に配置されているのと同じパターンで、両シーンで実行する想定。
    /// </summary>
    [MenuItem("Tools/AreaSelect/Setup Infinite Stone Manager (現在のシーン)")]
    public static void SetupManagerOnly()
    {
        Scene scene = SceneManager.GetActiveScene();
        GameObject goldManagerGo = FindDeepInScene(scene, "GoldManager");
        if (goldManagerGo == null)
        {
            Debug.LogError("[InfiniteStoneSetupMigrator] GoldManagerが見つかりません。現在のシーン: " + scene.name);
            return;
        }

        GameObject stoneManagerGo = FindDeepInScene(scene, "InfiniteStoneManager");
        if (stoneManagerGo == null)
        {
            stoneManagerGo = new GameObject("InfiniteStoneManager");
            stoneManagerGo.transform.SetParent(goldManagerGo.transform.parent, false);
        }
        if (stoneManagerGo.GetComponent<InfiniteStoneManager>() == null)
            stoneManagerGo.AddComponent<InfiniteStoneManager>();

        EditorUtility.SetDirty(stoneManagerGo);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[InfiniteStoneSetupMigrator] InfiniteStoneManagerを{scene.name}に配置しました。");
    }

    [MenuItem("Tools/AreaSelect/Setup Infinite Stone HUD")]
    public static void Run()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "03_AreaSelect")
        {
            Debug.LogWarning("[InfiniteStoneSetupMigrator] 03_AreaSelectシーンを開いた状態で実行してください。現在: " + scene.name);
            return;
        }

        GameObject goldManagerGo = FindDeepInScene(scene, "GoldManager");
        if (goldManagerGo == null)
        {
            Debug.LogError("[InfiniteStoneSetupMigrator] GoldManagerが見つかりません。");
            return;
        }

        // ---- InfiniteStoneManager（GoldManagerと同じ階層にsiblingとして作成） ----
        GameObject stoneManagerGo = FindDeepInScene(scene, "InfiniteStoneManager");
        if (stoneManagerGo == null)
        {
            stoneManagerGo = new GameObject("InfiniteStoneManager");
            stoneManagerGo.transform.SetParent(goldManagerGo.transform.parent, false);
        }
        if (stoneManagerGo.GetComponent<InfiniteStoneManager>() == null)
            stoneManagerGo.AddComponent<InfiniteStoneManager>();

        // ---- InfiniteStoneHUD（GoldHUDのすぐ下に同じ親・同じ構成で作成） ----
        GameObject goldHudGo = FindDeepInScene(scene, "GoldHUD");
        if (goldHudGo == null)
        {
            Debug.LogError("[InfiniteStoneSetupMigrator] GoldHUDが見つかりません。");
            return;
        }
        var goldHudRect = goldHudGo.GetComponent<RectTransform>();
        if (goldHudRect == null)
        {
            Debug.LogError("[InfiniteStoneSetupMigrator] GoldHUDにRectTransformがありません。");
            return;
        }

        GameObject existingHud = FindDeepInScene(scene, "InfiniteStoneHUD");
        if (existingHud != null) Object.DestroyImmediate(existingHud);

        GameObject hudGo = new GameObject("InfiniteStoneHUD", typeof(RectTransform));
        hudGo.transform.SetParent(goldHudGo.transform.parent, false);
        hudGo.transform.SetSiblingIndex(goldHudGo.transform.GetSiblingIndex() + 1);

        var hudRect = hudGo.GetComponent<RectTransform>();
        hudRect.anchorMin = goldHudRect.anchorMin;
        hudRect.anchorMax = goldHudRect.anchorMax;
        hudRect.pivot = goldHudRect.pivot;
        hudRect.sizeDelta = goldHudRect.sizeDelta;
        // GoldHUDのすぐ下（Y方向にGoldHUDの高さ分だけマイナス）に配置
        hudRect.anchoredPosition = goldHudRect.anchoredPosition - new Vector2(0f, goldHudRect.sizeDelta.y);

        // ---- アイコン（GoldIconと同じ相対位置） ----
        var goldIconTrans = goldHudGo.transform.Find("GoldIcon");
        var iconGo = new GameObject("StoneIcon", typeof(RectTransform), typeof(Image));
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
        iconImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/AreaSelect/無限化アイコン.png");
        iconImg.preserveAspect = true;

        // ---- 個数テキスト（GoldTextと同じ相対位置） ----
        var goldTextTrans = goldHudGo.transform.Find("GoldText");
        var textGo = new GameObject("StoneText", typeof(RectTransform));
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
        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = "0";
        if (srcTmp != null)
        {
            tmp.font = srcTmp.font;
            tmp.fontSize = srcTmp.fontSize;
            tmp.color = srcTmp.color;
            tmp.alignment = srcTmp.alignment;
        }

        var hud = hudGo.AddComponent<InfiniteStoneHUD>();
        var so = new SerializedObject(hud);
        so.FindProperty("stoneIcon").objectReferenceValue = iconImg;
        so.FindProperty("stoneText").objectReferenceValue = tmp;
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(hudGo);
        EditorUtility.SetDirty(stoneManagerGo);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[InfiniteStoneSetupMigrator] InfiniteStoneManager / InfiniteStoneHUD をセットアップしました。");
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
