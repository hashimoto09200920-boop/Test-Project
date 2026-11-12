using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 弾の性質を変えるアイテム（スパイラルショット等）の表示と管理役を05_Gameに作る（2026/11/11）。何度実行しても、今あるものは消さずに使う。
/// ・シーン直下に「ShotItemManager」（ShotItemManager）
/// ・Canvas > PaddleCostBarUI > GoldBar の子に「ShotItemHUD」（ShotItemHUD）と、その子の「Icon」（Image）・「Count」（TextMeshProUGUI）
/// </summary>
public static class ShotItemSetupTool
{
    private const string DrillFxPrefabPath = "Assets/Prefabs/Effects/DrillFX.prefab";

    [MenuItem("Tools/アイテム/弾を変えるアイテムの表示を作る（05_Gameを開いて実行）")]
    private static void Build()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var goldHud = Object.FindFirstObjectByType<GoldHUD>(FindObjectsInactive.Include);
        if (goldHud == null)
        {
            EditorUtility.DisplayDialog("弾を変えるアイテム", "GoldHUD（GoldBar）が見つかりません。05_Game を開いてから実行してください。", "OK");
            return;
        }
        Transform goldBar = goldHud.transform;
        // GoldHUD に設定されているゴールドの数字（ShotItemHUD の数字を拾わないよう、GoldHUD の設定から読む）
        TextMeshProUGUI goldText = new SerializedObject(goldHud).FindProperty("goldText").objectReferenceValue as TextMeshProUGUI;
        if (goldText == null) goldText = goldBar.GetComponentInChildren<TextMeshProUGUI>(true);

        // ---- 表示（GoldBar の子） ----
        var hudT = goldBar.Find("ShotItemHUD");
        if (hudT == null)
        {
            var go = new GameObject("ShotItemHUD", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "弾を変えるアイテム");
            go.layer = goldBar.gameObject.layer;
            go.transform.SetParent(goldBar, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(150f, 0f); // ゴールドの数字の右に合わせて動く（Follow Gold Text）。OFFの時はこの位置
            rt.sizeDelta = new Vector2(120f, 40f);
            hudT = go.transform;
        }

        var iconT = hudT.Find("Icon");
        Image icon;
        if (iconT == null)
        {
            var go = new GameObject("Icon", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "弾を変えるアイテム");
            go.layer = hudT.gameObject.layer;
            go.transform.SetParent(hudT, false);
            icon = go.AddComponent<Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = false;
        }
        else { icon = iconT.GetComponent<Image>(); if (icon == null) icon = iconT.gameObject.AddComponent<Image>(); }

        var countT = hudT.Find("Count");
        TextMeshProUGUI count;
        if (countT == null)
        {
            var go = new GameObject("Count", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "弾を変えるアイテム");
            go.layer = hudT.gameObject.layer;
            go.transform.SetParent(hudT, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(80f, 40f);
            count = go.AddComponent<TextMeshProUGUI>();
            count.text = "x3";
            count.fontSize = goldText != null ? goldText.fontSize : 28f;
            if (goldText != null && goldText.font != null) { count.font = goldText.font; count.fontSharedMaterial = goldText.font.material; }
            count.alignment = TextAlignmentOptions.MidlineLeft;
            count.textWrappingMode = TextWrappingModes.NoWrap;
            count.raycastTarget = false;
            count.color = Color.white;
        }
        else { count = countT.GetComponent<TextMeshProUGUI>(); if (count == null) count = countT.gameObject.AddComponent<TextMeshProUGUI>(); }

        var hud = hudT.GetComponent<ShotItemHUD>();
        if (hud == null) hud = Undo.AddComponent<ShotItemHUD>(hudT.gameObject);
        Undo.RecordObject(hud, "弾を変えるアイテム");
        Sprite spiral = LoadFirstSprite(ShotItemPaths.Spiral);
        hud.EditorWire(icon, count, goldText, spiral, LoadFirstSprite(ShotItemPaths.Blast), LoadFirstSprite(ShotItemPaths.Split));
        EditorUtility.SetDirty(hud);

        // ---- 管理役（シーン直下） ----
        var mgr = Object.FindFirstObjectByType<ShotItemManager>(FindObjectsInactive.Include);
        if (mgr == null)
        {
            var go = new GameObject("ShotItemManager");
            Undo.RegisterCreatedObjectUndo(go, "弾を変えるアイテム");
            if (go.scene != scene) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
            mgr = go.AddComponent<ShotItemManager>();
        }
        Undo.RecordObject(mgr, "弾を変えるアイテム");
        var drillFx = AssetDatabase.LoadAssetAtPath<DrillFXManager>(DrillFxPrefabPath);
        mgr.EditorWire(hud, drillFx);
        EditorUtility.SetDirty(mgr);

        EditorSceneManager.MarkSceneDirty(scene);
        SceneView.RepaintAll();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();

        string msg = "作成しました。シーンを保存してください。\n・ShotItemManager（残り回数の管理・弾をドリルにする）\n・GoldBar の子の ShotItemHUD（アイコンと残り回数）"
                   + (drillFx == null ? "\n\n※ドリルの演出（" + DrillFxPrefabPath + "）が見つかりません。ShotItemManager の「Drill Fx Prefab」に入れてください。" : "")
                   + (spiral == null ? "\n\n※スパイラルショットの画像がまだありません。" + ShotItemPaths.Spiral + " を置いた後、もう一度このメニューを実行するか、ShotItemHUD の「Spiral Icon」に入れてください。BlockItemManager の右クリックメニュー「スパイラルショットの画像を設定」も実行してください。" : "");
        Debug.Log("[ShotItemSetupTool] " + msg);
        EditorUtility.DisplayDialog("弾を変えるアイテム", msg, "OK");
    }

    private static Sprite LoadFirstSprite(string path)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s != null) return s;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path)) if (o is Sprite sp) return sp;
        return null;
    }
}
