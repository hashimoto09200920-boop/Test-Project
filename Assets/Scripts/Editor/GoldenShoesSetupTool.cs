using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ゴールドシューズの表示を05_Gameに作る（2026/11/10）。何度実行しても、今あるものは消さずに使う（足りない物だけ作る）。
/// ・シーン直下に「GoldenShoesBuff」（GoldenShoesBuff）と、子の「Icon」（SpriteRenderer）・「Seconds」（TextMesh）
/// ・PaddleCostBarUI に GoldenShoesGaugeFX と、子の「GoldenShoesParticles」（粒子の入れ物）
/// </summary>
public static class GoldenShoesSetupTool
{
    private const string GlowSpritePath = "Assets/Art/UI/GemFX/Gem_Glow.png";

    [MenuItem("Tools/アイテム/ゴールドシューズの表示を作る（05_Gameを開いて実行）")]
    private static void Build()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var barUI = Object.FindFirstObjectByType<PaddleCostBarUI>(FindObjectsInactive.Include);
        if (barUI == null)
        {
            EditorUtility.DisplayDialog("ゴールドシューズ", "PaddleCostBarUI が見つかりません。05_Game を開いてから実行してください。", "OK");
            return;
        }
        Sprite glow = LoadFirstSprite(GlowSpritePath);
        if (glow == null) Debug.LogWarning("[GoldenShoesSetupTool] " + GlowSpritePath + " が読めません（粒子・足元の光の画像は後でInspectorで入れてください）");
        Sprite shoes = AssetDatabase.LoadAssetAtPath<Sprite>(GoldenShoesBuff.ShoesSpritePath);

        // ---- 線ゲージ側 ----
        Transform bar = barUI.transform;
        Image whiteFill = FindImage(bar, "WhiteBar/FillBar");
        Image redFill = FindImage(bar, "RedBar/FillBar");
        if (whiteFill == null || redFill == null)
            Debug.LogWarning($"[GoldenShoesSetupTool] ゲージの中身が見つかりません（WhiteBar/FillBar={whiteFill != null}・RedBar/FillBar={redFill != null}）");

        var layerT = bar.Find("GoldenShoesParticles");
        if (layerT == null)
        {
            var go = new GameObject("GoldenShoesParticles", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(go, "ゴールドシューズ");
            go.layer = barUI.gameObject.layer;
            go.transform.SetParent(bar, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            layerT = go.transform;
        }
        layerT.SetAsLastSibling(); // ゲージより手前

        // 「線ゲージ消費量半減」の文字は取ったアイテムのそばに出す（BlockItemManager）ので、ゲージの近くの文字は作らない（以前作った物があれば隠すだけ）
        var textT = bar.Find("GoldenShoesText");
        TextMeshProUGUI text = textT != null ? textT.GetComponent<TextMeshProUGUI>() : null;

        var fx = barUI.GetComponent<GoldenShoesGaugeFX>();
        if (fx == null) fx = Undo.AddComponent<GoldenShoesGaugeFX>(barUI.gameObject);
        Undo.RecordObject(fx, "ゴールドシューズ");
        fx.EditorWire(whiteFill, redFill, (RectTransform)layerT, text, glow);
        EditorUtility.SetDirty(fx);

        // ---- ダンサーの上のアイコン ----
        var buff = Object.FindFirstObjectByType<GoldenShoesBuff>(FindObjectsInactive.Include);
        if (buff == null)
        {
            var go = new GameObject("GoldenShoesBuff");
            Undo.RegisterCreatedObjectUndo(go, "ゴールドシューズ");
            SceneManager_MoveToScene(go, scene);
            buff = go.AddComponent<GoldenShoesBuff>();
        }
        Transform bt = buff.transform;

        var iconT = bt.Find("Icon");
        SpriteRenderer icon;
        if (iconT == null)
        {
            var go = new GameObject("Icon");
            Undo.RegisterCreatedObjectUndo(go, "ゴールドシューズ");
            go.transform.SetParent(bt, false);
            icon = go.AddComponent<SpriteRenderer>();
        }
        else { icon = iconT.GetComponent<SpriteRenderer>(); if (icon == null) icon = iconT.gameObject.AddComponent<SpriteRenderer>(); }
        if (icon.sprite == null && shoes != null) icon.sprite = shoes;

        var secT = bt.Find("Seconds");
        TextMesh sec;
        if (secT == null)
        {
            var go = new GameObject("Seconds");
            Undo.RegisterCreatedObjectUndo(go, "ゴールドシューズ");
            go.transform.SetParent(bt, false);
            sec = go.AddComponent<TextMesh>();
            sec.anchor = TextAnchor.MiddleCenter;
            sec.alignment = TextAlignment.Center;
            sec.color = Color.white;
            sec.text = "30";
        }
        else { sec = secT.GetComponent<TextMesh>(); if (sec == null) sec = secT.gameObject.AddComponent<TextMesh>(); }

        Undo.RecordObject(buff, "ゴールドシューズ");
        buff.EditorWire(icon, sec, glow, fx);
        EditorUtility.SetDirty(buff);

        EditorSceneManager.MarkSceneDirty(scene);
        SceneView.RepaintAll();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();

        string msg = "作成しました。シーンを保存してください。\n"
                   + "・GoldenShoesBuff（アイコン・残り秒数・足元の光）\n"
                   + "・PaddleCostBarUI の GoldenShoesGaugeFX（粒子）\n"
                   + (shoes == null ? "\n※アイコンの画像がまだありません。" + GoldenShoesBuff.ShoesSpritePath + " を置いた後、GoldenShoesBuff の右クリックメニュー「アイコンの画像を設定」と、BlockItemManager の右クリックメニュー「ゴールドシューズの画像を設定」を実行してください。" : "");
        Debug.Log("[GoldenShoesSetupTool] " + msg);
        EditorUtility.DisplayDialog("ゴールドシューズ", msg, "OK");
    }

    /// <summary>
    /// アクセラレーターの表示を作る（2026/11/10）。シーン直下に「GoldBoltBuff」（GoldBoltBuff）と、子の「Icon」・「Seconds」。
    /// アイコンの大きさ・高さ・数字の大きさなどは、調整済みのゴールドシューズ（GoldenShoesBuff）の値を写す（並べた時にそろうように）
    /// </summary>
    [MenuItem("Tools/アイテム/アクセラレーターの表示を作る（05_Gameを開いて実行）")]
    private static void BuildBolt() => BuildBuff<GoldBoltBuff>("アクセラレーター", "GoldBoltBuff", GoldBoltBuff.BoltSpritePath, (b, i, s, g) => b.EditorWire(i, s, g));

    [MenuItem("Tools/アイテム/マルチバウンドの表示を作る（05_Gameを開いて実行）")]
    private static void BuildMultiBound() => BuildBuff<MultiBoundBuff>("マルチバウンド", "MultiBoundBuff", MultiBoundBuff.MultiBoundSpritePath, (b, i, s, g) => b.EditorWire(i, s, g));

    /// <summary>
    /// 一定時間効くアイテムの表示を作る（共通）。シーン直下に goName の GameObject と、子の「Icon」・「Seconds」。
    /// アイコンの大きさ・高さ・数字の大きさなどは、調整済みのゴールドシューズ（GoldenShoesBuff）の値を写す（並べた時にそろうように）
    /// </summary>
    private static void BuildBuff<T>(string label, string goName, string spritePath, System.Action<T, SpriteRenderer, TextMesh, Sprite> wire) where T : ItemBuffBase
    {
        var scene = EditorSceneManager.GetActiveScene();
        var shoes = Object.FindFirstObjectByType<GoldenShoesBuff>(FindObjectsInactive.Include);
        if (Object.FindFirstObjectByType<PaddleCostBarUI>(FindObjectsInactive.Include) == null)
        {
            EditorUtility.DisplayDialog(label, "05_Game を開いてから実行してください。", "OK");
            return;
        }
        Sprite glow = LoadFirstSprite(GlowSpritePath);
        Sprite itemSprite = LoadFirstSprite(spritePath);

        T buff = Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
        if (buff == null)
        {
            var go = new GameObject(goName);
            Undo.RegisterCreatedObjectUndo(go, label);
            SceneManager_MoveToScene(go, scene);
            buff = go.AddComponent<T>();
        }
        Transform bt = buff.transform;

        var iconT = bt.Find("Icon");
        SpriteRenderer icon;
        if (iconT == null)
        {
            var go = new GameObject("Icon");
            Undo.RegisterCreatedObjectUndo(go, label);
            go.transform.SetParent(bt, false);
            icon = go.AddComponent<SpriteRenderer>();
        }
        else { icon = iconT.GetComponent<SpriteRenderer>(); if (icon == null) icon = iconT.gameObject.AddComponent<SpriteRenderer>(); }
        if (icon.sprite == null && itemSprite != null) icon.sprite = itemSprite;

        var secT = bt.Find("Seconds");
        TextMesh sec;
        if (secT == null)
        {
            var go = new GameObject("Seconds");
            Undo.RegisterCreatedObjectUndo(go, label);
            go.transform.SetParent(bt, false);
            sec = go.AddComponent<TextMesh>();
            sec.anchor = TextAnchor.MiddleCenter;
            sec.alignment = TextAlignment.Center;
            sec.color = Color.white;
            sec.text = "30";
        }
        else { sec = secT.GetComponent<TextMesh>(); if (sec == null) sec = secT.gameObject.AddComponent<TextMesh>(); }

        Undo.RecordObject(buff, label);
        wire(buff, icon, sec, glow);

        // シューズで調整済みの値を写す
        if (shoes != null)
        {
            var src = new SerializedObject(shoes);
            var dst = new SerializedObject(buff);
            foreach (var name in new[] { "iconOffsetX", "iconHeight", "followSharpness", "iconSize", "numberHeight", "numberOffsetY", "numberColor", "sortingOrder", "iconSpacing", "blinkLastSeconds", "blinkPerSecond", "blinkMinAlpha", "startBurstCount", "startBurstSpeed", "startBurstDamping", "startBurstLife", "startBurstSize" })
            {
                var p = src.FindProperty(name);
                if (p != null) dst.CopyFromSerializedProperty(p);
            }
            dst.ApplyModifiedProperties();
        }
        EditorUtility.SetDirty(buff);

        EditorSceneManager.MarkSceneDirty(scene);
        SceneView.RepaintAll();
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();

        string msg = "作成しました。シーンを保存してください。\n・" + goName + "（アイコン・残り秒数・取った時と切れた時の粒子）"
                   + (shoes != null ? "\n・アイコンの大きさ・高さなどはゴールドシューズの値を写しました" : "\n※GoldenShoesBuff が見つからないので、アイコンの大きさ・高さは初期値です")
                   + (itemSprite == null ? "\n\n※アイコンの画像がまだありません。" + spritePath + " を置いた後、" + goName + " の右クリックメニュー「アイコンの画像を設定」と、BlockItemManager の右クリックメニュー「" + label + "の画像を設定」を実行してください。" : "");
        Debug.Log("[GoldenShoesSetupTool] " + label + "：" + msg);
        EditorUtility.DisplayDialog(label, msg, "OK");
    }

    private static void SceneManager_MoveToScene(GameObject go, UnityEngine.SceneManagement.Scene scene)
    {
        if (go.scene != scene) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
    }

    private static Image FindImage(Transform root, string path)
    {
        var t = root.Find(path);
        return t != null ? t.GetComponent<Image>() : null;
    }

    private static Sprite LoadFirstSprite(string path)
    {
        var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (s != null) return s;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path)) if (o is Sprite sp) return sp;
        return null;
    }
}
