using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// お食事券のボタンと枚数の表示を作る（2026/11/11）。何度実行しても、今あるものは消さずに使う。
/// ・02_Gem・03_AreaSelect・04_Drink の3つのシーンの ShopUI（フード画面）：HeaderRow の BuyButton（購入ボタン）をそのまま写して、
///   左に「TicketButton」を作る（同じ大きさ・デザイン）。アイコンをお食事券に、右下に枚数「TicketCount」
/// ・03_AreaSelect の ShopButton の横の枚数の表示（TicketBadge）は2026/11/11にやめた。以前作った物は消す
/// 3つのシーンを順に開いて保存し、最後に元のシーンに戻る
/// </summary>
public static class FoodTicketSetupTool
{
    private static readonly string[] Scenes =
    {
        "Assets/Scenes/02_Gem.unity",
        "Assets/Scenes/03_AreaSelect.unity",
        "Assets/Scenes/04_Drink.unity",
    };

    [MenuItem("Tools/フード画面/お食事券のボタンを作る（02_Gem・03_AreaSelect・04_Drink）")]
    private static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        string original = EditorSceneManager.GetActiveScene().path;
        Sprite ticket = LoadFirstSprite(BlockItemManager.FoodTicketSpritePath);
        var log = new System.Text.StringBuilder();

        foreach (var path in Scenes)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            int made = 0;
            foreach (var shop in Object.FindObjectsByType<ShopUI>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (BuildTicketButton(shop, ticket, log)) made++;
            if (path.EndsWith("03_AreaSelect.unity")) RemoveBadge(log); // Area選択の枚数の表示はやめた（2026/11/11 ユーザー指示）。以前作った物を消す
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            log.AppendLine($"・{System.IO.Path.GetFileNameWithoutExtension(path)}：ShopUI {made} 個を処理して保存しました");
        }

        if (!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original, OpenSceneMode.Single);
        if (ticket == null) log.AppendLine("\n※お食事券の画像（" + BlockItemManager.FoodTicketSpritePath + "）がまだありません。画像を置いた後、もう一度このメニューを実行してください。");
        Debug.Log("[FoodTicketSetupTool]\n" + log);
        EditorUtility.DisplayDialog("お食事券のボタン", log.ToString(), "OK");
    }

    private static bool BuildTicketButton(ShopUI shop, Sprite ticket, System.Text.StringBuilder log)
    {
        var so = new SerializedObject(shop);
        var panel = so.FindProperty("shopPanel").objectReferenceValue as GameObject;
        if (panel == null)
        {
            var canvas = shop.GetComponentInParent<Canvas>(true);
            var t = canvas != null ? canvas.transform.Find("ShopPanel") : null;
            if (t != null) panel = t.gameObject;
        }
        if (panel == null) { log.AppendLine($"  ※{shop.name}：ShopPanel が見つかりません"); return false; }
        var header = panel.transform.Find("HeaderRow");
        var buy = header != null ? header.Find("BuyButton") : null;
        if (buy == null) { log.AppendLine($"  ※{shop.name}：HeaderRow/BuyButton が見つかりません"); return false; }

        var ticketT = header.Find("TicketButton");
        if (ticketT == null)
        {
            var go = Object.Instantiate(buy.gameObject, header);
            go.name = "TicketButton";
            Undo.RegisterCreatedObjectUndo(go, "お食事券のボタン");
            go.transform.SetSiblingIndex(buy.GetSiblingIndex());
            // 写した購入ボタンの押した時の処理（Inspectorで入れた分）は消す。押した時の処理は ShopUI が付ける
            var btnSo = new SerializedObject(go.GetComponent<Button>());
            var calls = btnSo.FindProperty("m_OnClick.m_PersistentCalls.m_Calls");
            if (calls != null) { calls.ClearArray(); btnSo.ApplyModifiedPropertiesWithoutUndo(); }
            ticketT = go.transform;
        }
        var rt = (RectTransform)ticketT;
        var buyRt = (RectTransform)buy;
        rt.sizeDelta = buyRt.sizeDelta;
        rt.anchoredPosition = buyRt.anchoredPosition + new Vector2(-(buyRt.sizeDelta.x + 8f), 0f);

        var iconT = ticketT.Find("Icon");
        var icon = iconT != null ? iconT.GetComponent<Image>() : null;
        if (icon != null && ticket != null) { icon.sprite = ticket; icon.preserveAspect = true; }

        // ★枚数はボタンには出さない（2026/11/11 右上の所持数 FoodTicketHUD に出すことにした）
        TextMeshProUGUI count = null;

        Undo.RecordObject(shop, "お食事券のボタン");
        so.Update();
        so.FindProperty("ticketButton").objectReferenceValue = ticketT.GetComponent<Button>();
        var bgT = ticketT.Find("BuyBg");
        so.FindProperty("ticketBgImage").objectReferenceValue = bgT != null ? bgT.GetComponent<Image>() : null;
        so.FindProperty("ticketButtonIcon").objectReferenceValue = icon;
        so.FindProperty("ticketCountText").objectReferenceValue = count;
        if (ticket != null) so.FindProperty("ticketIconSprite").objectReferenceValue = ticket;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(shop);
        return true;
    }

    /// <summary>以前作った Area選択の ShopButton > TicketBadge を消す</summary>
    private static void RemoveBadge(System.Text.StringBuilder log)
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t == null || t.name != "TicketBadge" || t.parent == null || t.parent.name != "ShopButton") continue;
            Undo.DestroyObjectImmediate(t.gameObject);
            log.AppendLine("  ・03_AreaSelect：ShopButton > TicketBadge（お食事券の枚数の表示）を消しました");
            return;
        }
    }

    private static Sprite LoadFirstSprite(string path)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s != null) return s;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path)) if (o is Sprite sp) return sp;
        return null;
    }
}
