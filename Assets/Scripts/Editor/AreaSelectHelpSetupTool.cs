using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 03_AreaSelect に HELP ボタンと HELP 画面を作る（2026/11/12）。
/// ・HelpButton：AreaPanel の ShopButton を写して作る（枠・大きさ・ホバー拡大・SE・点滅はそのまま）。中の絵を HELPアイコン.png に。
///   位置はミッションボタンの上で、右上のジェムボタンと同じ高さ（X はミッションボタン、Y はジェムボタン）
/// ・HELP画面：05_Game の中断メニューの HELP 画面（PauseMenuUI.helpPanel）を丸ごと写し、Canvas の子の HelpUI（上に重なる Canvas）に入れる。
///   後ろに暗幕を敷き、HELP画面の戻るボタンで閉じる
/// 何度実行しても安全：HelpButton は作り直さず設定だけ合わせる。HELP画面は毎回 05_Game から写し直す（05_Game 側の HELP を変えた後は再実行する）
/// </summary>
public static class AreaSelectHelpSetupTool
{
    private const string AreaSelectPath = "Assets/Scenes/03_AreaSelect.unity";
    private const string GamePath = "Assets/Scenes/05_Game.unity";
    private const string HelpIconPath = "Assets/Art/AreaSelect/HELPアイコン.png";
    private const string HelpRootName = "HelpUI";

    [MenuItem("Tools/エリアセレクト/HELPボタンとHELP画面を作る（05_GameのHELPを写す）")]
    private static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var setup = EditorSceneManager.GetSceneManagerSetup();
        var log = new System.Text.StringBuilder();
        try
        {
            Scene area = EditorSceneManager.OpenScene(AreaSelectPath, OpenSceneMode.Single);
            Scene game = EditorSceneManager.OpenScene(GamePath, OpenSceneMode.Additive);

            // ---- 05_Game の HELP 画面 ----
            PauseMenuUI pause = null;
            foreach (var r in game.GetRootGameObjects()) { pause = r.GetComponentInChildren<PauseMenuUI>(true); if (pause != null) break; }
            var srcPanel = pause != null ? new SerializedObject(pause).FindProperty("helpPanel").objectReferenceValue as GameObject : null;
            if (srcPanel == null) { Fail("05_Game の中断メニューに HELP 画面（PauseMenuUI の Help Panel）が見つかりません"); return; }

            // ---- 03_AreaSelect の Canvas・AreaPanel ----
            Transform canvas = null, areaPanel = null;
            foreach (var r in area.GetRootGameObjects())
            {
                if (r.name == "Canvas") canvas = r.transform;
                var ap = r.transform.Find("UI_Root/Panels/AreaPanel");
                if (ap != null) areaPanel = ap;
            }
            if (canvas == null || areaPanel == null) { Fail("03_AreaSelect に Canvas / Canvas > UI_Root > Panels > AreaPanel が見つかりません"); return; }

            // ---- HelpUI（上に重なる Canvas）を作り直す ----
            var old = canvas.Find(HelpRootName);
            if (old != null) { Object.DestroyImmediate(old.gameObject); log.AppendLine("・前に作った HelpUI を消して作り直しました"); }
            var root = new GameObject(HelpRootName, typeof(RectTransform));
            root.layer = canvas.gameObject.layer;
            root.transform.SetParent(canvas, false);
            root.transform.SetAsLastSibling();
            var rootRt = (RectTransform)root.transform;
            Stretch(rootRt);
            var c = root.AddComponent<Canvas>();
            c.overrideSorting = true;
            c.sortingOrder = 100; // エリアセレクトのボタン・ミッション画面より上（黒帯 32000 より下）
            root.AddComponent<GraphicRaycaster>();
            var ctrl = root.AddComponent<AreaSelectHelpUI>();

            var content = new GameObject("Content", typeof(RectTransform));
            content.layer = root.layer;
            content.transform.SetParent(root.transform, false);
            Stretch((RectTransform)content.transform);

            // 暗幕（後ろのボタンを押せないようにする）
            var dim = new GameObject("Dim", typeof(RectTransform));
            dim.layer = root.layer;
            dim.transform.SetParent(content.transform, false);
            var dimRt = (RectTransform)dim.transform;
            dimRt.anchorMin = Vector2.zero; dimRt.anchorMax = Vector2.one;
            dimRt.offsetMin = new Vector2(-2000f, -2000f); dimRt.offsetMax = new Vector2(2000f, 2000f); // 横長の画面でも端まで覆う
            var dimImg = dim.AddComponent<Image>();
            dimImg.color = new Color(0f, 0f, 0f, 0.6f);
            dimImg.raycastTarget = true;

            // HELP 画面を写す（05_Game の中断メニューの HelpPanel）
            var panel = Object.Instantiate(srcPanel);
            panel.name = "HelpPanel";
            SceneManager.MoveGameObjectToScene(panel, area);
            panel.transform.SetParent(content.transform, false);
            panel.SetActive(true);
            var prt = (RectTransform)panel.transform;
            prt.anchoredPosition = new Vector2(0f, ((RectTransform)srcPanel.transform).anchoredPosition.y); // エリアセレクトは左右にボタンがあるので真ん中に
            SetLayerRecursive(panel.transform, root.layer);

            // HELP 画面の戻るボタン → 閉じる（中断メニューへのつながりは外す）
            var back = panel.transform.Find("BackButton");
            var backBtn = back != null ? back.GetComponent<Button>() : null;
            if (backBtn != null)
            {
                ClearPersistent(backBtn.onClick);
                UnityEventTools.AddPersistentListener(backBtn.onClick, new UnityAction(ctrl.Close));
                log.AppendLine("・HELP画面の戻るボタンで閉じるようにしました");
            }
            else log.AppendLine("・※HELP画面の中に BackButton が見つかりません。閉じるボタンを手で設定してください");

            ctrl.EditorWire(content, null); // HelpButton のホバー演出は下で HelpButton を用意した後に設定する
            content.SetActive(false);
            log.AppendLine($"・05_Game の HELP 画面を写しました（{panel.GetComponentInChildren<HelpPagerUI>(true) != null}：ページ送りあり）");

            // ---- HelpButton（ShopButton を写す。既にあれば設定だけ合わせる）----
            var shop = areaPanel.Find("ShopButton");
            var gem = areaPanel.Find("GemManagementButton") as RectTransform;
            var mission = areaPanel.Find("MissionButton") as RectTransform;
            var btnTr = areaPanel.Find("HelpButton");
            if (btnTr == null)
            {
                if (shop == null) { Fail("AreaPanel に ShopButton が見つかりません"); return; }
                var go = Object.Instantiate(shop.gameObject, areaPanel);
                go.name = "HelpButton";
                // 左側のボタン群の並び（ジェム画面の暗幕より前）に入れる
                int idx = mission != null ? mission.GetSiblingIndex() : shop.GetSiblingIndex() + 1;
                go.transform.SetSiblingIndex(idx);
                var img = go.transform.Find("ShopImage");
                if (img != null) img.name = "HelpImage";
                btnTr = go.transform;
                log.AppendLine("・ShopButton を写して HelpButton を作りました");
            }
            var brt = (RectTransform)btnTr;
            float x = mission != null ? mission.anchoredPosition.x : -brt.anchoredPosition.x;
            float y = gem != null ? gem.anchoredPosition.y : brt.anchoredPosition.y;
            brt.anchoredPosition = new Vector2(x, y);
            var icon = btnTr.Find("HelpImage");
            var sp = ItemBuffBase.LoadSpriteForEditor(HelpIconPath);
            if (icon != null && sp != null) { var im = icon.GetComponent<Image>(); if (im != null) { im.sprite = sp; im.preserveAspect = true; EditorUtility.SetDirty(im); } }
            else log.AppendLine("・※HELPアイコンの画像、または HelpButton の子の HelpImage が見つかりません");
            var b = btnTr.GetComponent<Button>();
            if (b != null)
            {
                ClearPersistent(b.onClick);
                UnityEventTools.AddPersistentListener(b.onClick, new UnityAction(ctrl.Open));
            }
            log.AppendLine($"・HelpButton の位置：X {x}（ミッションボタンと同じ）・Y {y}（ジェムボタンと同じ）");

            // 閉じた時に HelpButton の拡大・点滅を元に戻すため、ホバー演出をつなぐ（2026/11/12）
            ctrl.EditorWire(content, btnTr.GetComponent<Game.UI.ButtonHoverEffect>());
            EditorUtility.SetDirty(ctrl);
            EditorSceneManager.MarkSceneDirty(area);
            EditorSceneManager.SaveScene(area);
            EditorSceneManager.CloseScene(game, true);
            Debug.Log("[AreaSelectHelpSetupTool]\n" + log);
            EditorUtility.DisplayDialog("エリアセレクトのHELP", log.ToString(), "OK");
        }
        finally
        {
            if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
    }

    private static void Fail(string msg)
    {
        Debug.LogError("[AreaSelectHelpSetupTool] " + msg);
        EditorUtility.DisplayDialog("エリアセレクトのHELP", msg, "OK");
    }

    private static void ClearPersistent(UnityEventBase ev)
    {
        for (int i = ev.GetPersistentEventCount() - 1; i >= 0; i--) UnityEventTools.RemovePersistentListener(ev, i);
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static void SetLayerRecursive(Transform t, int layer)
    {
        t.gameObject.layer = layer;
        foreach (Transform ch in t) SetLayerRecursive(ch, layer);
    }
}
