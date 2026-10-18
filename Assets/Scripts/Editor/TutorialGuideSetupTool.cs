using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// チュートリアルのガイド表示（TutorialGuideUI）をPlay前に作成するメニュー。
/// 05_Gameシーンを開いた状態で実行する。既存のチュートリアルUI（カード等）は作り直さず、ガイド用の部品だけを追加する。
/// ・初回：ガイド部品の生成、画像の生成、文言（日本語・英語）の登録、ステップごとの初期設定、HUD部品の参照設定
/// ・2回目以降：既にあるガイド部品・設定は上書きしない（足りない画像・文言だけ追加）。作り直したい時は「作り直す」メニューを使う
/// </summary>
public static class TutorialGuideSetupTool
{
    private const string ArtDir = "Assets/Art/Tutorial/Guide";
    private const string FontPath = "Assets/Fonts/NotoSansJP-Regular SDF.asset";

    [MenuItem("Tools/チュートリアル/ガイドUIを作成")]
    private static void Create() => Run(rebuild: false);

    [MenuItem("Tools/チュートリアル/ガイドUIを作り直す（ガイド部品だけ削除して再生成）")]
    private static void Rebuild()
    {
        if (!EditorUtility.DisplayDialog("ガイドUIを作り直す",
            "Guide_Back / Guide_Front / Guide_CollapseToggle を削除して作り直します。\n" +
            "TutorialGuideUIの「ステップごとの設定」「キーワード」は残ります（空なら初期値を入れます）。\n" +
            "チュートリアルのカード本体は変更しません。", "作り直す", "やめる")) return;
        Run(rebuild: true);
    }

    private static void Run(bool rebuild)
    {
        var flow = Object.FindFirstObjectByType<TutorialFlowController>(FindObjectsInactive.Include);
        if (flow == null)
        {
            EditorUtility.DisplayDialog("ガイドUIを作成", "TutorialFlowControllerが見つかりません。05_Gameシーンを開いてから実行してください。", "OK");
            return;
        }

        var flowSo = new SerializedObject(flow);
        var panelRoot = flowSo.FindProperty("panelRoot").objectReferenceValue as GameObject;
        var bodyText = flowSo.FindProperty("bodyText").objectReferenceValue as TextMeshProUGUI;
        var nextButton = flowSo.FindProperty("nextButton").objectReferenceValue as Button;
        if (panelRoot == null || bodyText == null)
        {
            EditorUtility.DisplayDialog("ガイドUIを作成", "TutorialFlowControllerのPanel Root / Body Textが未設定です。", "OK");
            return;
        }
        Transform card = bodyText.transform.parent;

        // ---- 画像 ----
        var sprites = EnsureSprites();

        // ---- 部品 ----
        Transform existingBack = panelRoot.transform.Find("Guide_Back");
        Transform existingFront = panelRoot.transform.Find("Guide_Front");
        Transform existingToggle = card.Find("Guide_CollapseToggle");
        if (rebuild)
        {
            if (existingBack != null) Undo.DestroyObjectImmediate(existingBack.gameObject);
            if (existingFront != null) Undo.DestroyObjectImmediate(existingFront.gameObject);
            if (existingToggle != null) Undo.DestroyObjectImmediate(existingToggle.gameObject);
            existingBack = existingFront = existingToggle = null;
        }

        var guide = flow.GetComponent<TutorialGuideUI>();
        if (guide == null) guide = Undo.AddComponent<TutorialGuideUI>(flow.gameObject);
        var so = new SerializedObject(guide);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        bool built = false;
        if (existingBack == null || existingFront == null)
        {
            if (existingBack != null) Undo.DestroyObjectImmediate(existingBack.gameObject);
            if (existingFront != null) Undo.DestroyObjectImmediate(existingFront.gameObject);
            BuildBack(panelRoot.transform, so);
            BuildFront(panelRoot.transform, so, sprites, font);
            built = true;
        }
        if (card.Find("Guide_CollapseToggle") == null) BuildCollapseToggle(card, so, font);

        // カード・本文・次へアイコン・暗幕
        so.FindProperty("cardRect").objectReferenceValue = card as RectTransform;
        var bodyGroup = bodyText.GetComponent<CanvasGroup>();
        if (bodyGroup == null) bodyGroup = Undo.AddComponent<CanvasGroup>(bodyText.gameObject);
        bodyGroup.blocksRaycasts = false;
        so.FindProperty("bodyGroup").objectReferenceValue = bodyGroup;
        if (nextButton != null)
            so.FindProperty("nextButtonIcon").objectReferenceValue = nextButton.transform.Find("Icon") as RectTransform;
        so.FindProperty("panelDimImage").objectReferenceValue = panelRoot.GetComponent<Image>();

        // ---- キーワード・ステップごとの設定（空の時だけ初期値） ----
        if (so.FindProperty("keywords").arraySize == 0) FillKeywords(so);
        so.ApplyModifiedPropertiesWithoutUndo();
        if (guide.EditorEntries.Count == 0) FillEntries(guide);
        else
        {
            MigrateStep0Entries(guide);
            AssignMissingTargets(guide);
        }
        MigrateStep0Hints(flowSo);

        // ---- 参照をTutorialFlowControllerへ ----
        flowSo.FindProperty("guideUI").objectReferenceValue = guide;
        flowSo.ApplyModifiedPropertiesWithoutUndo();

        // ---- 文言（日本語・英語）----
        // ---- ミッション欄の枠（面取りの二重線＋見出しタブ）。何度実行しても同じ結果になる ----
        ApplyMissionFrame(so, sprites, font);
        so.ApplyModifiedPropertiesWithoutUndo();

        int added = AddLocalization();

        EditorUtility.SetDirty(guide);
        EditorUtility.SetDirty(flow);
        EditorSceneManager.MarkSceneDirty(flow.gameObject.scene);
        SceneView.RepaintAll();
        Debug.Log($"[TutorialGuideSetupTool] 完了：ガイド部品{(built ? "を生成" : "は既存のまま")}、文言を{added}件追加。シーンを保存してください。", guide);
    }

    // =====================================================================
    // 部品の生成
    // =====================================================================

    private static RectTransform NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Guide UI");
        go.transform.SetParent(parent, false);
        go.layer = parent.gameObject.layer;
        return (RectTransform)go.transform;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static void Center(RectTransform rt, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
    }

    private static Image AddImage(RectTransform rt, Sprite sp, Color c, Image.Type type = Image.Type.Simple)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sp; img.color = c; img.type = type;
        img.raycastTarget = false;
        img.preserveAspect = false;
        return img;
    }

    private static TextMeshProUGUI AddText(RectTransform rt, TMP_FontAsset font, float size, TextAlignmentOptions align, Color c, string text)
    {
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) { t.font = font; t.fontSharedMaterial = font.material; }
        t.fontSize = size; t.alignment = align; t.color = c; t.text = text;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        return t;
    }

    /// <summary>カードより奥（Panelの最初の子）：スポットライトの暗幕</summary>
    private static void BuildBack(Transform panel, SerializedObject so)
    {
        var back = NewUI("Guide_Back", panel);
        Stretch(back);
        back.SetAsFirstSibling();
        var dims = new Image[4];
        string[] names = { "Guide_SpotDim_Top", "Guide_SpotDim_Bottom", "Guide_SpotDim_Left", "Guide_SpotDim_Right" };
        for (int i = 0; i < 4; i++)
        {
            var rt = NewUI(names[i], back);
            Center(rt, new Vector2(100, 100));
            dims[i] = AddImage(rt, null, new Color(0f, 0f, 0f, 0.5f));
            rt.gameObject.SetActive(false);
        }
        so.FindProperty("backRoot").objectReferenceValue = back;
        var p = so.FindProperty("spotDims");
        p.arraySize = 4;
        for (int i = 0; i < 4; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = dims[i];
    }

    /// <summary>カードより手前（Panelの最後の子）：指・目印・ミッション欄など</summary>
    private static void BuildFront(Transform panel, SerializedObject so, Dictionary<string, Sprite> sp, TMP_FontAsset font)
    {
        var front = NewUI("Guide_Front", panel);
        Stretch(front);
        front.SetAsLastSibling();
        var cg = front.gameObject.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false; cg.interactable = false;
        so.FindProperty("frontRoot").objectReferenceValue = front;
        so.FindProperty("frontGroup").objectReferenceValue = cg;

        Color yellow = new Color(1f, 0.85f, 0.3f, 1f);
        Color cyan = new Color(0.35f, 0.9f, 1f, 1f);

        // ⑥ スポットライトの枠・矢印
        var frame = NewUI("Guide_SpotFrame", front);
        Center(frame, new Vector2(100, 100));
        so.FindProperty("spotFrame").objectReferenceValue = frame;
        so.FindProperty("spotFrameImage").objectReferenceValue = AddImage(frame, sp["frame"], cyan, Image.Type.Sliced);
        frame.gameObject.SetActive(false);
        var arrow = NewUI("Guide_SpotArrow", front);
        Center(arrow, new Vector2(84, 84));
        AddImage(arrow, sp["arrow"], yellow);
        so.FindProperty("spotArrow").objectReferenceValue = arrow;
        arrow.gameObject.SetActive(false);

        // ⑦ 弾の目印
        var bg = NewUI("Guide_BulletGuide", front);
        Stretch(bg);
        so.FindProperty("bulletGuideRoot").objectReferenceValue = bg;
        var dotsProp = so.FindProperty("guideDots");
        dotsProp.arraySize = 8;
        for (int i = 0; i < 8; i++)
        {
            var d = NewUI("Guide_Dot" + i, bg);
            Center(d, new Vector2(16, 16));
            dotsProp.GetArrayElementAtIndex(i).objectReferenceValue = AddImage(d, sp["dot"], yellow);
            d.gameObject.SetActive(false);
        }
        var zone = NewUI("Guide_Zone", bg);
        Center(zone, new Vector2(200, 22));
        so.FindProperty("zoneMarker").objectReferenceValue = zone;
        so.FindProperty("zoneMarkerImage").objectReferenceValue = AddImage(zone, sp["bar"], yellow);
        var zl = NewUI("Guide_ZoneLabel", bg);
        Center(zl, new Vector2(420, 44));
        so.FindProperty("zoneLabel").objectReferenceValue = AddText(zl, font, 26, TextAlignmentOptions.Center, yellow, "ここに線を引く");
        bg.gameObject.SetActive(false);

        // ⑧ タイミングリング
        var ring = NewUI("Guide_TimingRing", front);
        Center(ring, new Vector2(200, 200));
        so.FindProperty("timingRing").objectReferenceValue = ring;
        so.FindProperty("timingRingImage").objectReferenceValue = AddImage(ring, sp["ring"], yellow);
        ring.gameObject.SetActive(false);
        var nowL = NewUI("Guide_NowLabel", front);
        Center(nowL, new Vector2(300, 70));
        var nowT = AddText(nowL, font, 46, TextAlignmentOptions.Center, new Color(1f, 0.35f, 0.35f, 1f), "今！");
        nowT.fontStyle = FontStyles.Bold;
        so.FindProperty("nowLabel").objectReferenceValue = nowT;
        nowL.gameObject.SetActive(false);

        // ⑤ 指（リング→指の順で指を手前に）
        var tr = NewUI("Guide_TouchRing", front);
        Center(tr, new Vector2(96, 96));
        so.FindProperty("touchRing").objectReferenceValue = tr;
        so.FindProperty("touchRingImage").objectReferenceValue = AddImage(tr, sp["ring"], Color.white);
        tr.gameObject.SetActive(false);
        var fg = NewUI("Guide_Finger", front);
        fg.anchorMin = fg.anchorMax = new Vector2(0.5f, 0.5f);
        fg.pivot = new Vector2(FingerTipPivotX, FingerTipPivotY); // 指先を位置に合わせる
        fg.sizeDelta = new Vector2(FingerW * 5, FingerH * 5);
        so.FindProperty("finger").objectReferenceValue = fg;
        so.FindProperty("fingerImage").objectReferenceValue = AddImage(fg, sp["finger"], Color.white);
        fg.gameObject.SetActive(false);

        // ①② ミッション欄
        var mp = NewUI("Guide_MissionPanel", front);
        mp.anchorMin = mp.anchorMax = new Vector2(0.5f, 1f);
        mp.pivot = new Vector2(0.5f, 1f);
        mp.anchoredPosition = new Vector2(330f, -24f);
        mp.sizeDelta = new Vector2(680f, 300f);
        AddImage(mp, null, new Color(0.07f, 0.08f, 0.13f, 0.88f));
        var outline = mp.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.35f, 0.9f, 1f, 0.8f);
        outline.effectDistance = new Vector2(2f, -2f);
        so.FindProperty("missionPanel").objectReferenceValue = mp;

        var head = NewUI("Guide_MissionHeader", mp);
        TopRow(head, 0f, 52f, 24f, 200f);
        var headT = AddText(head, font, 28, TextAlignmentOptions.MidlineLeft, cyan, "MISSION");
        headT.fontStyle = FontStyles.Bold;
        so.FindProperty("missionHeaderText").objectReferenceValue = headT;
        var prog = NewUI("Guide_MissionProgress", mp);
        TopRow(prog, 0f, 52f, 300f, 20f);
        so.FindProperty("missionProgressText").objectReferenceValue = AddText(prog, font, 22, TextAlignmentOptions.MidlineRight, new Color(0.8f, 0.85f, 0.95f, 1f), "タスク 1/4");

        var rowsProp = so.FindProperty("missionRows");
        rowsProp.arraySize = 4;
        for (int i = 0; i < 4; i++)
        {
            var row = NewUI("Guide_MissionRow" + i, mp);
            TopRow(row, 58f + i * 48f, 48f, 0f, 0f);
            var box = NewUI("Box", row);
            box.anchorMin = box.anchorMax = new Vector2(0f, 0.5f);
            box.pivot = new Vector2(0.5f, 0.5f);
            box.anchoredPosition = new Vector2(40f, 0f);
            box.sizeDelta = new Vector2(30f, 30f);
            var boxImg = AddImage(box, sp["box"], yellow);
            var chk = NewUI("Check", box);
            Center(chk, new Vector2(34f, 34f));
            chk.anchoredPosition = new Vector2(3f, 3f);
            var chkImg = AddImage(chk, sp["check"], new Color(0.45f, 0.95f, 0.6f, 1f));
            chkImg.enabled = false;
            var txt = NewUI("Text", row);
            txt.anchorMin = new Vector2(0f, 0f); txt.anchorMax = new Vector2(1f, 1f);
            txt.offsetMin = new Vector2(72f, 0f); txt.offsetMax = new Vector2(-18f, 0f);
            var t = AddText(txt, font, 24, TextAlignmentOptions.MidlineLeft, yellow, "ヒント");
            t.enableAutoSizing = true; t.fontSizeMin = 16; t.fontSizeMax = 24;
            var el = rowsProp.GetArrayElementAtIndex(i);
            el.FindPropertyRelative("root").objectReferenceValue = row;
            el.FindPropertyRelative("box").objectReferenceValue = boxImg;
            el.FindPropertyRelative("check").objectReferenceValue = chkImg;
            el.FindPropertyRelative("text").objectReferenceValue = t;
        }

        var extra = NewUI("Guide_ExtraHint", mp);
        TopRow(extra, 250f, 64f, 26f, 18f);
        var extraT = AddText(extra, font, 21, TextAlignmentOptions.MidlineLeft, new Color(1f, 0.62f, 0.3f, 1f), "ヒント：");
        so.FindProperty("extraHintText").objectReferenceValue = extraT;
        extra.gameObject.SetActive(false);

        var foot = NewUI("Guide_Footer", mp);
        TopRow(foot, 250f, 48f, 18f, 18f);
        var footT = AddText(foot, font, 24, TextAlignmentOptions.Center, cyan, "クリア！「→」で進めます");
        footT.fontStyle = FontStyles.Bold;
        so.FindProperty("footerText").objectReferenceValue = footT;
        foot.gameObject.SetActive(false);
        mp.gameObject.SetActive(false);

        // ③ 成功の演出
        var pop = NewUI("Guide_SuccessPop", front);
        pop.anchorMin = pop.anchorMax = new Vector2(0.5f, 1f);
        pop.pivot = new Vector2(0.5f, 0.5f);
        pop.anchoredPosition = new Vector2(330f, -400f);
        pop.sizeDelta = new Vector2(420f, 110f);
        AddImage(pop, sp["dot"], new Color(0f, 0f, 0f, 0.55f));
        var popTxt = NewUI("Text", pop);
        Stretch(popTxt);
        var popT = AddText(popTxt, font, 64, TextAlignmentOptions.Center, new Color(1f, 0.9f, 0.35f, 1f), "成功！");
        popT.fontStyle = FontStyles.Bold;
        so.FindProperty("successPop").objectReferenceValue = pop;
        so.FindProperty("successText").objectReferenceValue = popT;
        pop.gameObject.SetActive(false);
    }

    /// <summary>上端基準・横いっぱいの行（top=上からの距離, left/right=左右の余白）</summary>
    private static void TopRow(RectTransform rt, float top, float height, float left, float right)
    {
        rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.offsetMin = new Vector2(left, -top - height);
        rt.offsetMax = new Vector2(-right, -top);
    }

    /// <summary>⑨ カード右上の縮小/展開ボタン</summary>
    private static void BuildCollapseToggle(Transform card, SerializedObject so, TMP_FontAsset font)
    {
        var rt = NewUI("Guide_CollapseToggle", card);
        rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-14f, -14f);
        rt.sizeDelta = new Vector2(56f, 56f);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = new Color(0.2f, 0.22f, 0.3f, 0.9f);
        img.raycastTarget = true;
        var btn = rt.gameObject.AddComponent<Button>();
        btn.targetGraphic = img;
        var lbl = NewUI("Label", rt);
        Stretch(lbl);
        var t = AddText(lbl, font, 28, TextAlignmentOptions.Center, new Color(0.35f, 0.9f, 1f, 1f), "▲");
        so.FindProperty("collapseToggle").objectReferenceValue = btn;
        so.FindProperty("collapseToggleLabel").objectReferenceValue = t;
        rt.gameObject.SetActive(false);
    }

    // =====================================================================
    // 初期値
    // =====================================================================

    private static void FillKeywords(SerializedObject so)
    {
        var list = new (string, string)[]
        {
            ("ジャスト反射", "FFD94C"), ("Just Reflects", "FFD94C"), ("Just Reflect", "FFD94C"),
            ("赤線", "FF5A5A"), ("Red Lines", "FF5A5A"), ("Red Line", "FF5A5A"),
            ("白線", "BFE9FF"), ("White Lines", "BFE9FF"), ("White Line", "BFE9FF"),
            ("硬度", "5FD6FF"), ("Hardness", "5FD6FF"),
            ("貫通力", "FFA040"), ("Penetration", "FFA040"),
            ("線ゲージ", "7CFF9A"), ("Line Gauge", "7CFF9A"),
            ("オーバーヒート", "FF7A3D"), ("Overheat", "FF7A3D"),
            ("スローモーションゲージ", "B39CFF"), ("Slow Motion Gauge", "B39CFF"),
            ("スローモーション", "B39CFF"), ("Slow Motion", "B39CFF"),
            ("砂時計ボタン", "B39CFF"), ("hourglass button", "B39CFF"),
            ("一時停止ボタン", "9BE7C4"), ("pause button", "9BE7C4"),
            ("マルチライン", "9BE7C4"), ("Multi-Line", "9BE7C4"),
            ("ハードライン", "9BE7C4"), ("Hard Line", "9BE7C4"),
            ("ソウル", "E39BFF"), ("soul", "E39BFF"),
        };
        var p = so.FindProperty("keywords");
        p.arraySize = list.Length;
        for (int i = 0; i < list.Length; i++)
        {
            var el = p.GetArrayElementAtIndex(i);
            el.FindPropertyRelative("keyword").stringValue = list[i].Item1;
            ColorUtility.TryParseHtmlString("#" + list[i].Item2, out Color c);
            el.FindPropertyRelative("color").colorValue = c;
        }
    }

    private static RectTransform FindHud(string name)
    {
        foreach (var rt in Object.FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (rt.name == name && rt.gameObject.scene.IsValid()) return rt;
        Debug.LogWarning($"[TutorialGuideSetupTool] HUD部品「{name}」が見つかりませんでした。TutorialGuideUIのステップごとの設定で手動で指定してください。");
        return null;
    }

    private static TutorialGuideUI.GuideEntry E(string memo, int step, int hint) =>
        new TutorialGuideUI.GuideEntry { memo = memo, stepIndex = step, hintIndex = hint };

    private static void FillEntries(TutorialGuideUI guide)
    {
        Undo.RecordObject(guide, "Guide Entries");
        var list = guide.EditorEntries;
        list.Clear();
        var F = TutorialGuideUI.FingerMotion.Swipe;

        list.AddRange(Step0Entries());
        TutorialGuideUI.GuideEntry e;

        e = E("2/10① 線が消える前に2本目", 1, 0);
        e.finger = TutorialGuideUI.FingerMotion.DoubleSwipe; e.fingerAnchor = TutorialGuideUI.GuideAnchor.FieldCenter; e.fingerSize = 2f;
        list.Add(e);

        e = E("2/10② 1本の線で2つの弾を反射", 1, 1);
        e.extraHintKey = "tutorial.guide.extra.s1h1"; e.extraHintFallback = "2つの弾がそろう位置に長めの線を引こう";
        list.Add(e);

        e = E("3/10② Hardnessのカード", 2, 1);
        e.spotlightTarget = FindHud("SkillCard_Skill_B1_HardnessUp"); e.spotlightPadding = 8f;
        list.Add(e);

        e = E("4/10① 赤線を引く（長押し）", 3, 0);
        e.finger = TutorialGuideUI.FingerMotion.LongPressSwipe; e.fingerAnchor = TutorialGuideUI.GuideAnchor.FieldCenter; e.fingerSize = 2f;
        list.Add(e);

        e = E("4/10② 赤線で反射", 3, 1);
        e.extraHintKey = "tutorial.guide.extra.s3red"; e.extraHintFallback = "長押しかダブルタップで赤線にしよう";
        list.Add(e);

        e = E("4/10③ 赤線で反射（硬度）", 3, 2);
        e.extraHintKey = "tutorial.guide.extra.s3red"; e.extraHintFallback = "長押しかダブルタップで赤線にしよう";
        list.Add(e);

        e = E("5/10 線ゲージ（全ヒント）", 4, -1);
        e.spotlightTarget = FindHud("PaddleCostBarUI");
        list.Add(e);

        e = E("6/10 ダンサーとフロアのHP", 5, -1);
        e.spotlightTarget = FindHud("HPStatusHUD");
        list.Add(e);

        e = E("7/10 魂を円で囲む", 6, 0);
        e.finger = TutorialGuideUI.FingerMotion.Circle; e.fingerAnchor = TutorialGuideUI.GuideAnchor.Soul; e.fingerSize = 0.9f;
        list.Add(e);

        var slowBtn = FindHud("SlowMotionButton");
        e = E("8/10 砂時計ボタン", 7, 0);
        e.finger = TutorialGuideUI.FingerMotion.Hold; e.fingerAnchor = TutorialGuideUI.GuideAnchor.UITarget; e.fingerUITarget = slowBtn;
        e.spotlightTarget = slowBtn; e.spotlightPadding = 4f;
        list.Add(e);

        e = E("9/10 スローモーションゲージ（全ヒント）", 8, -1);
        e.spotlightTarget = FindHud("SlowMotionGauge");
        list.Add(e);

        var pauseBtn = FindHud("PauseButton");
        e = E("10/10 一時停止ボタン", 9, 0);
        e.finger = TutorialGuideUI.FingerMotion.Tap; e.fingerAnchor = TutorialGuideUI.GuideAnchor.UITarget; e.fingerUITarget = pauseBtn;
        e.spotlightTarget = pauseBtn;
        list.Add(e);

        EditorUtility.SetDirty(guide);
    }

    /// <summary>
    /// 1/10の設定（2026/10/11：旧①「弾に向かって線を引く」と旧②「反射した弾を敵に当てる」を統合し、タスク3つに）。
    /// ①の追加のヒントは失敗の種類で出し分ける（反射できず当たった／反射したが敵に外れた）
    /// </summary>
    private static List<TutorialGuideUI.GuideEntry> Step0Entries()
    {
        var list = new List<TutorialGuideUI.GuideEntry>();
        var e = E("1/10① 弾に向かって線を引き、反射した弾を敵に当てる", 0, 0);
        e.finger = TutorialGuideUI.FingerMotion.Swipe; e.fingerAnchor = TutorialGuideUI.GuideAnchor.BulletPath; e.fingerSize = 1.8f; e.bulletGuide = true;
        e.extraHintKey = "tutorial.guide.extra.s0h0"; e.extraHintFallback = "弾の通り道を横切るように線を引こう";
        e.extraHintKey2 = "tutorial.guide.extra.s0h1"; e.extraHintFallback2 = "線の傾きで跳ね返る向きが変わります。敵の方へ向くように斜めに引こう";
        list.Add(e);

        e = E("1/10② ジャスト反射", 0, 1);
        e.timingRing = true;
        e.extraHintKey = "tutorial.guide.extra.s0h2"; e.extraHintFallback = "弾が線に触れる直前に引くとジャスト反射になります";
        list.Add(e);

        e = E("1/10③ 円を素早く引く", 0, 2);
        e.finger = TutorialGuideUI.FingerMotion.Circle; e.fingerAnchor = TutorialGuideUI.GuideAnchor.FieldCenter; e.fingerSize = 1.1f;
        e.extraHintKey = "tutorial.guide.extra.s0h3"; e.extraHintFallback = "始点と終点を近づけて、素早く一筆で描こう";
        list.Add(e);
        return list;
    }

    /// <summary>旧4タスク版の1/10の設定（ヒント番号3がある）が残っていたら、新しい3タスク版に置き換える</summary>
    private static void MigrateStep0Entries(TutorialGuideUI guide)
    {
        var list = guide.EditorEntries;
        if (!list.Exists(x => x != null && x.stepIndex == 0 && x.hintIndex == 3)) return;
        Undo.RecordObject(guide, "Guide Entries");
        int at = list.FindIndex(x => x != null && x.stepIndex == 0);
        list.RemoveAll(x => x != null && x.stepIndex == 0);
        list.InsertRange(Mathf.Clamp(at, 0, list.Count), Step0Entries());
        EditorUtility.SetDirty(guide);
        Debug.Log("[TutorialGuideSetupTool] 1/10のガイド設定を3タスク版に更新しました。", guide);
    }

    /// <summary>TutorialFlowControllerのStepsに保存された1/10のヒント文と、LocalizationTableの1/10のヒント文を3タスク版に更新する</summary>
    private static void MigrateStep0Hints(SerializedObject flowSo)
    {
        var steps = flowSo.FindProperty("steps");
        if (steps != null && steps.arraySize > 0)
        {
            var hints = steps.GetArrayElementAtIndex(0).FindPropertyRelative("hints");
            if (hints != null && hints.arraySize == 4)
            {
                string just = hints.GetArrayElementAtIndex(2).stringValue;
                string circle = hints.GetArrayElementAtIndex(3).stringValue;
                hints.arraySize = 3;
                hints.GetArrayElementAtIndex(0).stringValue = "弾に向かって線を引き、反射した弾を敵に当てる";
                hints.GetArrayElementAtIndex(1).stringValue = just;
                hints.GetArrayElementAtIndex(2).stringValue = circle;
                flowSo.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log("[TutorialGuideSetupTool] 1/10のヒント文（Steps）を3タスク版に更新しました。");
            }
        }

        var table = Resources.Load<Game.Localization.LocalizationTable>("GameData/LocalizationTable");
        if (table == null) return;
        var so = new SerializedObject(table);
        var entries = so.FindProperty("entries");
        int Find(string key)
        {
            for (int i = 0; i < entries.arraySize; i++)
                if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("key").stringValue == key) return i;
            return -1;
        }
        int h0 = Find("tutorial.step0.hint0"), h1 = Find("tutorial.step0.hint1"), h2 = Find("tutorial.step0.hint2"), h3 = Find("tutorial.step0.hint3");
        if (h3 < 0 || h0 < 0 || h1 < 0 || h2 < 0) return; // 更新済み
        var e0 = entries.GetArrayElementAtIndex(h0); var e1 = entries.GetArrayElementAtIndex(h1);
        var e2 = entries.GetArrayElementAtIndex(h2); var e3 = entries.GetArrayElementAtIndex(h3);
        e0.FindPropertyRelative("japanese").stringValue = "弾に向かって線を引き、反射した弾を敵に当てる";
        e0.FindPropertyRelative("english").stringValue = "Draw a line toward a bullet and hit the enemy with the reflected bullet";
        e1.FindPropertyRelative("japanese").stringValue = e2.FindPropertyRelative("japanese").stringValue; // ジャスト反射
        e1.FindPropertyRelative("english").stringValue = e2.FindPropertyRelative("english").stringValue;
        e2.FindPropertyRelative("japanese").stringValue = e3.FindPropertyRelative("japanese").stringValue; // 円
        e2.FindPropertyRelative("english").stringValue = e3.FindPropertyRelative("english").stringValue;
        entries.DeleteArrayElementAtIndex(h3);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(table);
        AssetDatabase.SaveAssets();
        Debug.Log("[TutorialGuideSetupTool] LocalizationTableの1/10のヒント文を3タスク版に更新しました（tutorial.step0.hint3は削除）。");
    }

    /// <summary>既存の設定で、HUD参照が外れているものだけ名前で探し直す</summary>
    private static void AssignMissingTargets(TutorialGuideUI guide)
    {
        Undo.RecordObject(guide, "Guide Entries");
        foreach (var e in guide.EditorEntries)
        {
            if (e == null) continue;
            if (e.stepIndex == 2 && e.spotlightTarget == null) e.spotlightTarget = FindHud("SkillCard_Skill_B1_HardnessUp");
            if (e.stepIndex == 4 && e.spotlightTarget == null) e.spotlightTarget = FindHud("PaddleCostBarUI");
            if (e.stepIndex == 5 && e.spotlightTarget == null) e.spotlightTarget = FindHud("HPStatusHUD");
            if (e.stepIndex == 7) { if (e.spotlightTarget == null) e.spotlightTarget = FindHud("SlowMotionButton"); if (e.fingerUITarget == null) e.fingerUITarget = e.spotlightTarget; }
            if (e.stepIndex == 8 && e.spotlightTarget == null) e.spotlightTarget = FindHud("SlowMotionGauge");
            if (e.stepIndex == 9) { if (e.spotlightTarget == null) e.spotlightTarget = FindHud("PauseButton"); if (e.fingerUITarget == null) e.fingerUITarget = e.spotlightTarget; }
        }
        EditorUtility.SetDirty(guide);
    }

    // =====================================================================
    // 文言（日本語・英語）。既にあるキーは上書きしない
    // =====================================================================

    private static int AddLocalization()
    {
        var table = Resources.Load<Game.Localization.LocalizationTable>("GameData/LocalizationTable");
        if (table == null) { Debug.LogWarning("[TutorialGuideSetupTool] LocalizationTableが見つかりません"); return 0; }
        var rows = new (string key, string ja, string en)[]
        {
            ("tutorial.guide.mission", "チュートリアル", "TUTORIAL"),
            ("tutorial.guide.progress", "タスク {0}/{1}", "Task {0}/{1}"),
            ("tutorial.guide.success", "成功！", "Nice!"),
            ("tutorial.guide.stepClear", "クリア！「→」で進めます", "Clear! Press → to continue"),
            ("tutorial.guide.drawHere", "ここに線を引く", "Draw a line here"),
            ("tutorial.guide.now", "今！", "NOW!"),
            ("tutorial.guide.tipPrefix", "ヒント：", "Tip: "),
            ("tutorial.guide.extra.s0h0", "弾の通り道を横切るように線を引こう", "Draw a line across the bullet's path"),
            ("tutorial.guide.extra.s0h1", "線の傾きで跳ね返る向きが変わります。敵の方へ向くように斜めに引こう", "The line's angle decides where the bullet bounces. Slant it so the bullet heads toward the enemy"),
            ("tutorial.guide.extra.s0h2", "弾が線に触れる直前に引くとジャスト反射になります", "Draw right before the bullet touches the line for a Just Reflect"),
            ("tutorial.guide.extra.s0h3", "始点と終点を近づけて、素早く一筆で描こう", "Draw quickly in one stroke and bring the start and end points together"),
            ("tutorial.guide.extra.s1h1", "2つの弾がそろう位置に長めの線を引こう", "Draw a longer line where both bullets will pass"),
            ("tutorial.guide.extra.s3red", "長押しかダブルタップで赤線にしよう", "Hold or double-tap to draw a Red Line"),
        };
        var so = new SerializedObject(table);
        var entries = so.FindProperty("entries");
        var existing = new HashSet<string>();
        for (int i = 0; i < entries.arraySize; i++) existing.Add(entries.GetArrayElementAtIndex(i).FindPropertyRelative("key").stringValue);
        int added = 0;
        foreach (var r in rows)
        {
            if (existing.Contains(r.key)) continue;
            int idx = entries.arraySize;
            entries.InsertArrayElementAtIndex(idx);
            var el = entries.GetArrayElementAtIndex(idx);
            el.FindPropertyRelative("key").stringValue = r.key;
            el.FindPropertyRelative("japanese").stringValue = r.ja;
            el.FindPropertyRelative("english").stringValue = r.en;
            added++;
        }
        if (added > 0)
        {
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(table);
            AssetDatabase.SaveAssets();
        }
        return added;
    }

    // =====================================================================
    // 画像の生成（無い時だけ作る。差し替えたい時は同名PNGを上書きすればよい）
    // =====================================================================

    // 指（ドット絵）：o=輪郭 w=白 s=影。指先は上
    private static readonly string[] FingerRows =
    {
        "......oo........",
        ".....owwo.......",
        ".....owwo.......",
        ".....owwo.......",
        ".....owwo.......",
        ".....owwooo.....",
        ".....owwowwoo...",
        "..oo.owwowwowoo.",
        ".owwoowwowwowwwo",
        ".owwwowwwwwwwwwo",
        "..owwwwwwwwwwwwo",
        "..owwwwwwwwwwwso",
        "...owwwwwwwwwwso",
        "...owwwwwwwwwso.",
        "....owwwwwwwwso.",
        "....owwwwwwwso..",
        ".....owwwwwwso..",
        ".....owwwwwwso..",
        ".....oooooooo...",
    };
    private const int FingerW = 16;
    private static int FingerH => FingerRows.Length;
    private const float FingerTipPivotX = 7f / 16f; // 1行目の「oo」（x=6〜7）の中央
    private static float FingerTipPivotY => 1f - 0.5f / FingerRows.Length;

    private static Dictionary<string, Sprite> EnsureSprites()
    {
        if (!Directory.Exists(ArtDir)) Directory.CreateDirectory(ArtDir);
        var d = new Dictionary<string, Sprite>
        {
            ["finger"] = EnsureSprite("Guide_Finger", FingerTexture, point: true, border: Vector4.zero),
            ["ring"] = EnsureSprite("Guide_Ring", () => RingTexture(128, 58f, 50f), false, Vector4.zero),
            ["dot"] = EnsureSprite("Guide_Dot", () => SoftDot(64), false, Vector4.zero),
            ["bar"] = EnsureSprite("Guide_Bar", () => SoftBar(256, 32), false, Vector4.zero),
            ["box"] = EnsureSprite("Guide_CheckBox", () => RoundRectOutline(64, 64, 10f, 6f, 0f), false, Vector4.zero),
            ["check"] = EnsureSprite("Guide_CheckMark", CheckTexture, false, Vector4.zero),
            ["arrow"] = EnsureSprite("Guide_Arrow", ArrowTexture, false, Vector4.zero),
            ["frame"] = EnsureSprite("Guide_Frame", () => RoundRectOutline(96, 96, 22f, 6f, 8f), false, new Vector4(32, 32, 32, 32)),
            // ミッション欄：面取り（角を45°で切った八角形）の塗り・二重線の枠、見出しタブ（下が開いた形）の塗り・枠
            ["panelFill"] = EnsureSprite("Guide_MissionFill", () => ChamferTexture(96, 96, 18f, PanelGlow, fill: true, openBottom: false), false, new Vector4(30, 30, 30, 30)),
            ["panelFrame"] = EnsureSprite("Guide_MissionFrame", () => ChamferTexture(96, 96, 18f, PanelGlow, fill: false, openBottom: false), false, new Vector4(30, 30, 30, 30)),
            ["tabFill"] = EnsureSprite("Guide_MissionTabFill", () => ChamferTexture(96, 64, 14f, PanelGlow, fill: true, openBottom: true), false, new Vector4(26, 0, 26, 26)),
            ["tabFrame"] = EnsureSprite("Guide_MissionTabFrame", () => ChamferTexture(96, 64, 14f, PanelGlow, fill: false, openBottom: true), false, new Vector4(26, 0, 26, 26)),
        };
        return d;
    }

    private static Sprite EnsureSprite(string name, System.Func<Texture2D> make, bool point, Vector4 border)
    {
        string path = $"{ArtDir}/{name}.png";
        if (!File.Exists(path))
        {
            var tex = make();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.filterMode = point ? FilterMode.Point : FilterMode.Bilinear;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.spriteBorder = border;
            var st = new TextureImporterSettings();
            imp.ReadTextureSettings(st);
            st.spriteMeshType = SpriteMeshType.FullRect; // ぼかし系の画像が角ばって切れないように
            imp.SetTextureSettings(st);
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // =====================================================================
    // ミッション欄の枠（B面取り＋E見出しタブ）
    // =====================================================================

    /// <summary>枠画像の外側に付けた光のにじみ幅（px）。この分だけ枠を欄の外へはみ出させて配置する</summary>
    private const float PanelGlow = 6f;
    private const float TabHeight = 40f;

    private static void ApplyMissionFrame(SerializedObject so, Dictionary<string, Sprite> sp, TMP_FontAsset font)
    {
        var mp = so.FindProperty("missionPanel").objectReferenceValue as RectTransform;
        if (mp == null) return;
        Color cyan = new Color(0.35f, 0.9f, 1f, 1f);
        Color fill = new Color(0.06f, 0.08f, 0.14f, 0.85f);

        // 背景：面取りの半透明の板（旧：四角＋Outline）
        var bg = mp.GetComponent<Image>();
        if (bg != null)
        {
            Undo.RecordObject(bg, "Mission Frame");
            bg.sprite = sp["panelFill"]; bg.type = Image.Type.Sliced; bg.color = fill;
            bg.pixelsPerUnitMultiplier = 1f;
        }
        var ol = mp.GetComponent<Outline>();
        if (ol != null) Undo.DestroyObjectImmediate(ol);

        // 枠：面取りの二重線（光のにじみ分だけ外にはみ出す）
        var frame = mp.Find("Guide_MissionFrame") as RectTransform;
        if (frame == null)
        {
            frame = NewUI("Guide_MissionFrame", mp);
            frame.gameObject.AddComponent<Image>();
        }
        frame.SetAsFirstSibling();
        frame.anchorMin = Vector2.zero; frame.anchorMax = Vector2.one; frame.pivot = new Vector2(0.5f, 0.5f);
        frame.offsetMin = new Vector2(-PanelGlow, -PanelGlow); frame.offsetMax = new Vector2(PanelGlow, PanelGlow);
        var frameImg = frame.GetComponent<Image>();
        frameImg.sprite = sp["panelFrame"]; frameImg.type = Image.Type.Sliced; frameImg.color = cyan; frameImg.raycastTarget = false;
        so.FindProperty("missionFrameImage").objectReferenceValue = frameImg;

        // 見出しタブ：欄の上辺の左側から上へ飛び出す（下側は欄につながるので開いている）
        var tab = mp.Find("Guide_MissionTab") as RectTransform;
        if (tab == null) tab = NewUI("Guide_MissionTab", mp);
        tab.anchorMin = tab.anchorMax = new Vector2(0f, 1f);
        tab.pivot = new Vector2(0f, 0f);
        tab.anchoredPosition = new Vector2(22f, -2f); // 2px下げて欄の上辺の線にかぶせる
        tab.sizeDelta = new Vector2(Mathf.Max(tab.sizeDelta.x, 180f), TabHeight);

        var tabFill = tab.Find("Fill") as RectTransform;
        if (tabFill == null) { tabFill = NewUI("Fill", tab); tabFill.gameObject.AddComponent<Image>(); }
        tabFill.SetSiblingIndex(0);
        tabFill.anchorMin = Vector2.zero; tabFill.anchorMax = Vector2.one; tabFill.pivot = new Vector2(0.5f, 0.5f);
        tabFill.offsetMin = new Vector2(-PanelGlow, 0f); tabFill.offsetMax = new Vector2(PanelGlow, PanelGlow);
        var tfImg = tabFill.GetComponent<Image>();
        tfImg.sprite = sp["tabFill"]; tfImg.type = Image.Type.Sliced; tfImg.color = fill; tfImg.raycastTarget = false;

        var tabFrame = tab.Find("Frame") as RectTransform;
        if (tabFrame == null) { tabFrame = NewUI("Frame", tab); tabFrame.gameObject.AddComponent<Image>(); }
        tabFrame.SetSiblingIndex(1);
        tabFrame.anchorMin = Vector2.zero; tabFrame.anchorMax = Vector2.one; tabFrame.pivot = new Vector2(0.5f, 0.5f);
        tabFrame.offsetMin = new Vector2(-PanelGlow, 0f); tabFrame.offsetMax = new Vector2(PanelGlow, PanelGlow);
        var tfrImg = tabFrame.GetComponent<Image>();
        tfrImg.sprite = sp["tabFrame"]; tfrImg.type = Image.Type.Sliced; tfrImg.color = cyan; tfrImg.raycastTarget = false;
        so.FindProperty("missionTab").objectReferenceValue = tab;
        so.FindProperty("missionTabFrameImage").objectReferenceValue = tfrImg;

        // 見出し「ミッション」をタブの中へ移す
        var head = so.FindProperty("missionHeaderText").objectReferenceValue as TextMeshProUGUI;
        if (head != null)
        {
            var hr = head.rectTransform;
            Undo.SetTransformParent(hr, tab, "Mission Frame");
            hr.SetAsLastSibling();
            hr.anchorMin = Vector2.zero; hr.anchorMax = Vector2.one; hr.pivot = new Vector2(0.5f, 0.5f);
            hr.offsetMin = new Vector2(0f, 2f); hr.offsetMax = Vector2.zero;
            head.alignment = TextAlignmentOptions.Center;
            head.fontSize = 26;
            head.textWrappingMode = TextWrappingModes.NoWrap;
        }
        tab.SetAsLastSibling();

        // 見出しがタブへ移った分、欄の1行目は「タスク 1/4」だけの細い行にする
        var prog = so.FindProperty("missionProgressText").objectReferenceValue as TextMeshProUGUI;
        if (prog != null) TopRow(prog.rectTransform, 2f, 40f, 300f, 22f);
        so.FindProperty("headerHeight").floatValue = 44f;

        // タブが上へ飛び出す分、欄を下げる（既に下げてあれば何もしない）
        if (mp.anchoredPosition.y > -(24f + TabHeight - 1f))
            mp.anchoredPosition = new Vector2(mp.anchoredPosition.x, -(24f + TabHeight));
        EditorUtility.SetDirty(mp);
    }

    /// <summary>
    /// 面取り（角を45°で切った八角形）の画像。fill=true：塗り／false：外側の太線＋内側の細線の二重線＋外側の光のにじみ。
    /// openBottom=true：下辺が無い形（見出しタブ用。下側は欄につながる）
    /// </summary>
    private static Texture2D ChamferTexture(int w, int h, float chamfer, float glow, bool fill, bool openBottom)
    {
        var t = NewTex(w, h);
        float m = glow;                        // 外側のにじみ用の余白
        float cx = w * 0.5f;
        float hx = w * 0.5f - m;
        // 下を開く時は、図形を画像の下へ大きくはみ出させて下側の角と辺を画像の外に追い出す
        float top = h - m, bottom = openBottom ? -h * 2f : m;
        float cy = (top + bottom) * 0.5f, hy = (top - bottom) * 0.5f;
        const float outerW = 3.5f, gap = 3.5f, innerW = 1.5f;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float px = Mathf.Abs(x + 0.5f - cx), py = Mathf.Abs(y + 0.5f - cy);
                // 八角形までの符号付き距離（内側が負）
                float d = Mathf.Max(px - hx, Mathf.Max(py - hy, (px + py - (hx + hy - chamfer)) * 0.70710678f));
                float a;
                if (fill)
                {
                    a = Mathf.Clamp01(0.5f - d);
                }
                else
                {
                    float outer = Mathf.Clamp01(outerW * 0.5f - Mathf.Abs(d + outerW * 0.5f) + 0.5f);
                    float innerC = -(outerW + gap + innerW * 0.5f);
                    float inner = Mathf.Clamp01(innerW * 0.5f - Mathf.Abs(d - innerC) + 0.5f) * 0.8f;
                    float g = d > 0f ? Mathf.Clamp01(1f - d / glow) * 0.45f : 0f;
                    a = Mathf.Max(outer, Mathf.Max(inner, g));
                }
                if (a > 0f) t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        t.Apply();
        return t;
    }

    private static Texture2D NewTex(int w, int h)
    {
        var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var clear = new Color32[w * h];
        t.SetPixels32(clear);
        return t;
    }

    private static Texture2D FingerTexture()
    {
        int w = FingerW, h = FingerRows.Length;
        var t = NewTex(w, h);
        for (int y = 0; y < h; y++)
        {
            string row = FingerRows[y];
            for (int x = 0; x < w && x < row.Length; x++)
            {
                Color c;
                switch (row[x])
                {
                    case 'o': c = new Color(0.08f, 0.08f, 0.12f, 1f); break;
                    case 'w': c = Color.white; break;
                    case 's': c = new Color(0.78f, 0.8f, 0.88f, 1f); break;
                    default: continue;
                }
                t.SetPixel(x, h - 1 - y, c);
            }
        }
        t.Apply();
        return t;
    }

    private static Texture2D RingTexture(int size, float outer, float inner)
    {
        var t = NewTex(size, size);
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = Mathf.Clamp01(outer - r + 0.5f) * Mathf.Clamp01(r - inner + 0.5f);
                float glow = Mathf.Clamp01(1f - Mathf.Abs(r - (outer + inner) * 0.5f) / (outer - inner + 6f)) * 0.35f;
                a = Mathf.Max(a, glow);
                if (a > 0f) t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        t.Apply();
        return t;
    }

    private static Texture2D SoftDot(int size)
    {
        var t = NewTex(size, size);
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
                float a = Mathf.Clamp01(1f - r);
                a = a * a * (3f - 2f * a);
                if (a > 0f) t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        t.Apply();
        return t;
    }

    private static Texture2D SoftBar(int w, int h)
    {
        var t = NewTex(w, h);
        float rr = h * 0.5f;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float cx = Mathf.Clamp(x, rr, w - 1 - rr);
                float dx = x - cx, dy = y - (h - 1) * 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy) / rr;
                float a = Mathf.Clamp01(1f - r);
                a = Mathf.Clamp01(a * 1.8f);
                if (a > 0f) t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        t.Apply();
        return t;
    }

    /// <summary>角丸の枠（thickness=線の太さ, glow=外側のぼかし幅）</summary>
    private static Texture2D RoundRectOutline(int w, int h, float radius, float thickness, float glow)
    {
        var t = NewTex(w, h);
        float m = glow + 1f;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                // 角丸矩形までの符号付き距離
                float px = x + 0.5f, py = y + 0.5f;
                float hx = (w - 2 * m) * 0.5f - radius, hy = (h - 2 * m) * 0.5f - radius;
                float qx = Mathf.Abs(px - w * 0.5f) - hx, qy = Mathf.Abs(py - h * 0.5f) - hy;
                float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
                float sd = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                float line = Mathf.Clamp01(thickness * 0.5f - Mathf.Abs(sd + thickness * 0.5f) + 0.5f);
                float g = glow > 0f && sd > 0f ? Mathf.Clamp01(1f - sd / glow) * 0.45f : 0f;
                float a = Mathf.Max(line, g);
                if (a > 0f) t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        t.Apply();
        return t;
    }

    private static Texture2D CheckTexture()
    {
        int s = 64;
        var t = NewTex(s, s);
        Vector2 a = new Vector2(12, 32), b = new Vector2(26, 16), c = new Vector2(54, 50);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float d = Mathf.Min(SegDist(p, a, b), SegDist(p, b, c));
                float al = Mathf.Clamp01(5f - d + 0.5f);
                if (al > 0f) t.SetPixel(x, y, new Color(1f, 1f, 1f, al));
            }
        t.Apply();
        return t;
    }

    private static Texture2D ArrowTexture()
    {
        int s = 64;
        var t = NewTex(s, s);
        for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                float px = x + 0.5f, py = y + 0.5f - s * 0.5f;
                float a = 0f;
                if (px >= 6f && px <= 36f && Mathf.Abs(py) <= 7f) a = 1f;                     // 軸
                float head = 60f - px;                                                          // 先端(右)
                if (px >= 30f && px <= 60f && Mathf.Abs(py) <= head * 0.7f) a = 1f;              // 三角
                if (a > 0f) t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        t.Apply();
        return t;
    }

    private static float SegDist(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float k = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * k);
    }
}
