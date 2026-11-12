using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ミッション画面のPlay前セットアップ（2026/11/07）
/// 5. 画像の準備：周りの透明な余白を切り取り、伸び縮み（9スライス）の境界を設定する。元の画像はプロジェクト直下の MissionArtBackup に保存
/// 6. 03_AreaSelectにミッションボタン・バッジ・ミッション画面のHierarchyを作る（既にある場合は何もしない＝作り直さない）
/// </summary>
public static class MissionPanelBuilder
{
    private const string ArtFolder = "Assets/Art/AreaSelect/";
    private const string WindowPng = ArtFolder + "窓の枠.png";
    private const string RowPng = ArtFolder + "各行の背景.png";
    private const string ClaimPng = ArtFolder + "「受け取る」ボタン.png";
    private const string BannerPng = ArtFolder + "横長の巻物の帯のプロンプト.png";
    private const string MissionIconPng = ArtFolder + "ミッションアイコン.png";
    private const string BackIconPng = ArtFolder + "戻るアイコン.png";
    private const string FontAssetPath = "Assets/Fonts/NotoSansJP-Regular SDF.asset";

    // 伸び縮みしても崩さない四隅の幅（余白を切り取った後の画像に対する値。px）
    private static readonly Vector4 WindowBorder = new Vector4(60, 60, 60, 60);
    private static readonly Vector4 RowBorder = new Vector4(24, 22, 24, 22);
    private static readonly Vector4 ClaimBorder = new Vector4(70, 60, 70, 60);

    // =====================================================================
    // 5. 画像の準備
    // =====================================================================

    [MenuItem("Tools/ミッション/5. ミッション画面の画像を準備（余白の切り取り・9スライス）")]
    private static void PrepareImages()
    {
        if (!EditorUtility.DisplayDialog("ミッション画面の画像を準備",
                "次の4枚の、周りの透明な余白を切り取って上書きします。\n" +
                "・窓の枠.png\n・各行の背景.png\n・「受け取る」ボタン.png\n・横長の巻物の帯のプロンプト.png\n\n" +
                "元の画像は、プロジェクトのフォルダ直下の「MissionArtBackup」に保存します（Assetsの外なのでゲームには入りません）。\n" +
                "あわせて、伸び縮み（9スライス）の境界を設定します。続けますか？", "実行", "キャンセル"))
            return;

        var log = new System.Text.StringBuilder();
        TrimTransparent(WindowPng, log);
        TrimTransparent(RowPng, log);
        TrimTransparent(ClaimPng, log);
        TrimTransparent(BannerPng, log);
        AssetDatabase.Refresh();

        SetupSprite(WindowPng, WindowBorder, log);
        SetupSprite(RowPng, RowBorder, log);
        SetupSprite(ClaimPng, ClaimBorder, log);
        SetupSprite(BannerPng, Vector4.zero, log);
        AssetDatabase.SaveAssets();

        Debug.Log("[MissionPanelBuilder] 画像の準備が完了しました\n" + log);
        EditorUtility.DisplayDialog("ミッション画面の画像を準備", "完了しました。詳細はConsoleのログを見てください。", "OK");
    }

    private static void TrimTransparent(string assetPath, System.Text.StringBuilder log)
    {
        string full = Path.GetFullPath(assetPath);
        if (!File.Exists(full)) { log.AppendLine($"× 見つかりません：{assetPath}"); return; }
        byte[] bytes = File.ReadAllBytes(full);
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!tex.LoadImage(bytes)) { log.AppendLine($"× 読めません：{assetPath}"); return; }

        int w = tex.width, h = tex.height;
        var px = tex.GetPixels32();
        int minX = w, minY = h, maxX = -1, maxY = -1;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                if (px[y * w + x].a > 8)
                {
                    if (x < minX) minX = x; if (x > maxX) maxX = x;
                    if (y < minY) minY = y; if (y > maxY) maxY = y;
                }
        if (maxX < 0) { log.AppendLine($"× 全部透明：{assetPath}"); return; }
        const int pad = 2;
        minX = Mathf.Max(0, minX - pad); minY = Mathf.Max(0, minY - pad);
        maxX = Mathf.Min(w - 1, maxX + pad); maxY = Mathf.Min(h - 1, maxY + pad);
        int cw = maxX - minX + 1, ch = maxY - minY + 1;
        if (cw == w && ch == h) { log.AppendLine($"○ 余白なし（そのまま）：{assetPath} {w}x{h}"); Object.DestroyImmediate(tex); return; }

        string backupDir = Path.GetFullPath("MissionArtBackup");
        Directory.CreateDirectory(backupDir);
        string backupPath = Path.Combine(backupDir, Path.GetFileName(full));
        if (!File.Exists(backupPath)) File.Copy(full, backupPath);

        var outTex = new Texture2D(cw, ch, TextureFormat.RGBA32, false);
        outTex.SetPixels(tex.GetPixels(minX, minY, cw, ch));
        outTex.Apply();
        File.WriteAllBytes(full, outTex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        Object.DestroyImmediate(outTex);
        log.AppendLine($"○ 余白を切り取り：{assetPath} {w}x{h} → {cw}x{ch}（元の画像：MissionArtBackup/{Path.GetFileName(full)}）");
    }

    private static void SetupSprite(string assetPath, Vector4 border, System.Text.StringBuilder log)
    {
        var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) { log.AppendLine($"× 読み込み設定が見つかりません：{assetPath}"); return; }
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect; // 9スライスはFullRectが必要
        settings.spriteBorder = border;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        log.AppendLine($"○ 設定：{assetPath} 境界(左,下,右,上)={border}");
    }

    // =====================================================================
    // 6. ミッション画面を作成（03_AreaSelect）
    // =====================================================================

    private static readonly Color Cyan = new Color(0.45f, 0.85f, 1f, 1f);
    private static readonly Color Lime = new Color(0.72f, 1f, 0.23f, 1f);
    private static readonly Color Muted = new Color(0.55f, 0.58f, 0.75f, 1f);

    [MenuItem("Tools/ミッション/6. ミッション画面を作成（03_AreaSelectを開いて実行）")]
    private static void BuildPanel()
    {
        var scene = EditorSceneManager.GetActiveScene();
        if (scene.name != "03_AreaSelect")
        {
            EditorUtility.DisplayDialog("ミッション画面を作成", "03_AreaSelectを開いてから実行してください。", "OK");
            return;
        }
        var canvas = GameObject.Find("Canvas");
        var areaPanel = GameObject.Find("Canvas/UI_Root/Panels/AreaPanel");
        var shopButton = GameObject.Find("Canvas/UI_Root/Panels/AreaPanel/ShopButton");
        var backButton = GameObject.Find("Canvas/UI_Root/Panels/AreaPanel/BackButton");
        if (canvas == null || areaPanel == null || shopButton == null)
        {
            EditorUtility.DisplayDialog("ミッション画面を作成", "Canvas/UI_Root/Panels/AreaPanel/ShopButton が見つかりません。", "OK");
            return;
        }
        if (canvas.transform.Find("MissionUI") != null)
        {
            EditorUtility.DisplayDialog("ミッション画面を作成",
                "Canvas/MissionUI が既にあります。\n作り直しによる設定の消失を防ぐため、何もしません。\n（作り直す場合は MissionUI を手で削除してから実行してください。MissionButton は残したままで大丈夫です）", "OK");
            return;
        }

        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
        var sWindow = AssetDatabase.LoadAssetAtPath<Sprite>(WindowPng);
        var sRow = AssetDatabase.LoadAssetAtPath<Sprite>(RowPng);
        var sClaim = AssetDatabase.LoadAssetAtPath<Sprite>(ClaimPng);
        var sBanner = AssetDatabase.LoadAssetAtPath<Sprite>(BannerPng);
        var sMission = AssetDatabase.LoadAssetAtPath<Sprite>(MissionIconPng);
        var sBack = AssetDatabase.LoadAssetAtPath<Sprite>(BackIconPng);
        var sKnob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        var sPill = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        var hoverSource = backButton != null ? backButton.GetComponent<Game.UI.ButtonHoverEffect>() : null;
        if (sWindow != null && sWindow.border == Vector4.zero &&
            !EditorUtility.DisplayDialog("ミッション画面を作成", "画像の9スライスの境界がまだ設定されていません。\n先に「5. ミッション画面の画像を準備」を実行することをおすすめします。このまま続けますか？", "続ける", "キャンセル"))
            return;

        // ---------------- ミッションボタン（既にあればそのまま使う） ----------------
        var missionBtnTr = areaPanel.transform.Find("MissionButton");
        GameObject missionBtnGO;
        if (missionBtnTr != null) missionBtnGO = missionBtnTr.gameObject;
        else
        {
            missionBtnGO = Object.Instantiate(shopButton, areaPanel.transform);
            missionBtnGO.name = "MissionButton";
            Undo.RegisterCreatedObjectUndo(missionBtnGO, "Create Mission UI");
            // 右側のボタン群の並び（GemDimPanelより前）に入れる。末尾に置くとジェム画面の暗幕より手前に出てしまう
            missionBtnGO.transform.SetSiblingIndex(shopButton.transform.GetSiblingIndex() + 1);
            var shopRT = shopButton.GetComponent<RectTransform>();
            missionBtnGO.GetComponent<RectTransform>().anchoredPosition = new Vector2(-shopRT.anchoredPosition.x, shopRT.anchoredPosition.y);
            // ShopButton自身のImageは枠（ネオン枠.png）なのでそのまま使い、子の絵（ShopImage＝フードの絵）をミッションアイコンに差し替える
            var iconTr = missionBtnGO.transform.Find("ShopImage");
            if (iconTr != null)
            {
                iconTr.name = "MissionImage";
                var iconImg = iconTr.GetComponent<Image>();
                if (iconImg != null && sMission != null) iconImg.sprite = sMission;
            }
            var b = missionBtnGO.GetComponent<Button>();
            if (b != null)
                for (int i = b.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                    UnityEventTools.RemovePersistentListener(b.onClick, i);
        }
        var mbButton = missionBtnGO.GetComponent<Button>();

        var badgeTr = missionBtnGO.transform.Find("Badge");
        GameObject badge;
        TextMeshProUGUI badgeText;
        if (badgeTr != null)
        {
            badge = badgeTr.gameObject;
            badgeText = badge.GetComponentInChildren<TextMeshProUGUI>(true);
        }
        else
        {
            badge = NewUI("Badge", missionBtnGO.transform);
            SetRect(badge, new Vector2(1, 1), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-28, -28), new Vector2(64, 64));
            var badgeImg = badge.AddComponent<Image>();
            badgeImg.sprite = sKnob; badgeImg.color = Lime; badgeImg.raycastTarget = false;
            var badgeOutline = badge.AddComponent<Outline>();
            badgeOutline.effectColor = new Color(0.04f, 0.07f, 0f, 1f); badgeOutline.effectDistance = new Vector2(3, -3);
            badgeText = NewText("BadgeText", badge.transform, font, "3", 38, new Color(0.04f, 0.07f, 0f, 1f), TextAlignmentOptions.Center, null, false);
            Stretch(badgeText.gameObject);
        }

        // ---------------- MissionUI（管理役）とMissionPanel ----------------
        var ui = NewUI("MissionUI", canvas.transform);
        Undo.RegisterCreatedObjectUndo(ui, "Create Mission UI");
        Stretch(ui);
        ui.transform.SetAsLastSibling();
        var audio = ui.AddComponent<AudioSource>();
        audio.playOnAwake = false;
        var panelUI = ui.AddComponent<MissionPanelUI>();

        var panel = NewUI("MissionPanel", ui.transform);
        Stretch(panel);

        var dim = NewUI("Dim", panel.transform);
        Stretch(dim);
        dim.AddComponent<Image>().color = new Color(0.01f, 0.01f, 0.04f, 0.72f);

        var window = NewUI("Window", panel.transform);
        SetRect(window, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(1900, 960));
        var winImg = window.AddComponent<Image>();
        winImg.sprite = sWindow; winImg.type = Image.Type.Sliced;

        // タイトル（英語固定の文字なので常に英語用フォント＝Bungee）
        var banner = NewUI("TitleBanner", window.transform);
        SetRect(banner, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, 10), new Vector2(620, 166));
        var bannerImg = banner.AddComponent<Image>();
        bannerImg.sprite = sBanner; bannerImg.preserveAspect = true; bannerImg.raycastTarget = false;
        var titleText = NewText("TitleText", banner.transform, font, "MISSION", 60, Color.white, TextAlignmentOptions.Center, LanguageFontSwitch.Kind.Heading, false, alwaysEnglish: true);
        Stretch(titleText.gameObject);
        titleText.characterSpacing = 8;

        // 閉じるボタン（ホバーで拡大・点滅・SE＝AreaSelectの戻るボタンと同じ設定）
        var close = NewUI("CloseButton", window.transform);
        SetRect(close, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(150, -150), new Vector2(150, 150));
        var closeHit = close.AddComponent<Image>();
        closeHit.color = new Color(0, 0, 0, 0); // 透明なタップ範囲（絵の周りも押せる）
        var closeBtn = close.AddComponent<Button>();
        closeBtn.transition = Selectable.Transition.None;
        var closeRing = NewUI("HoverRing", close.transform);
        SetRect(closeRing, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(146, 146));
        var closeRingImg = closeRing.AddComponent<Image>();
        closeRingImg.sprite = sKnob; closeRingImg.color = new Color(Lime.r, Lime.g, Lime.b, 0.12f); closeRingImg.raycastTarget = false;
        var closeIcon = NewUI("Icon", close.transform);
        SetRect(closeIcon, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(110, 110));
        var closeIconImg = closeIcon.AddComponent<Image>();
        closeIconImg.sprite = sBack; closeIconImg.preserveAspect = true; closeIconImg.raycastTarget = false;
        closeBtn.targetGraphic = closeHit;
        AddHoverEffect(close, hoverSource, closeIconImg, new[] { closeRingImg }, requireInteractable: false, hoverScale: 1.2f); // 1.5倍だと窓の枠に重なるので小さめ

        // 右上の達成数（案A：「達成」＋数字＋細いゲージ）
        var ach = NewUI("AchievedBox", window.transform);
        SetRect(ach, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-60, -44), new Vector2(460, 112));
        var achRow = NewUI("Row", ach.transform);
        SetRect(achRow, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(0, 78));
        var ahl = achRow.AddComponent<HorizontalLayoutGroup>();
        ahl.spacing = 8; ahl.childAlignment = TextAnchor.LowerLeft;
        ahl.childControlWidth = true; ahl.childControlHeight = true; ahl.childForceExpandWidth = false; ahl.childForceExpandHeight = true;
        var achLabel = NewText("Label", achRow.transform, font, "達成", 38, Cyan, TextAlignmentOptions.BottomLeft, LanguageFontSwitch.Kind.Heading, true);
        AddLayout(achLabel.gameObject, -1, 1);
        var achCount = NewText("Count", achRow.transform, font, "0", 68, Color.white, TextAlignmentOptions.BottomRight, null, false);
        AddLayout(achCount.gameObject, -1, 0);
        var achTotal = NewText("Total", achRow.transform, font, "/ 50", 36, Muted, TextAlignmentOptions.BottomLeft, null, false);
        AddLayout(achTotal.gameObject, -1, 0);
        achCount.margin = new Vector4(0, 0, 0, -4);
        var gauge = NewUI("Gauge", ach.transform);
        SetRect(gauge, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(0, 12));
        gauge.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.12f, 1f);
        gauge.AddComponent<Outline>().effectColor = new Color(0.15f, 0.15f, 0.35f, 1f);
        var gaugeFill = NewUI("Fill", gauge.transform);
        Stretch(gaugeFill);
        gaugeFill.AddComponent<Image>().color = Lime;

        // 列の幅（見出しと各行で共通。Inspectorで各LayoutElementを変えれば調整できる）
        const float progressW = 240f, rewardW = 400f, buttonW = 220f, spacing = 24f;
        const int padL = 30, padR = 24;

        var header = NewUI("HeaderRow", window.transform);
        SetRect(header, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, Vector2.zero);
        var headerRT = header.GetComponent<RectTransform>();
        headerRT.offsetMin = new Vector2(190, -215); headerRT.offsetMax = new Vector2(-60, -160);
        var hl = header.AddComponent<HorizontalLayoutGroup>();
        hl.padding = new RectOffset(padL, padR, 0, 0); hl.spacing = spacing;
        hl.childControlWidth = true; hl.childControlHeight = true; hl.childForceExpandWidth = false; hl.childForceExpandHeight = true;
        var hMission = NewText("HeaderMission", header.transform, font, "ミッション", 38, Cyan, TextAlignmentOptions.Center, LanguageFontSwitch.Kind.Heading, true);
        AddLayout(hMission.gameObject, -1, 1);
        var hProgress = NewText("HeaderProgress", header.transform, font, "進捗", 38, Cyan, TextAlignmentOptions.Center, LanguageFontSwitch.Kind.Heading, true);
        AddLayout(hProgress.gameObject, progressW, 0);
        var hReward = NewText("HeaderReward", header.transform, font, "報酬", 38, Cyan, TextAlignmentOptions.Center, LanguageFontSwitch.Kind.Heading, true);
        AddLayout(hReward.gameObject, rewardW, 0);
        var hSpace = NewUI("HeaderButtonSpace", header.transform);
        AddLayout(hSpace, buttonW, 0);

        // スクロール一覧
        var scroll = NewUI("ScrollView", window.transform);
        Stretch(scroll);
        var scrollRT = scroll.GetComponent<RectTransform>();
        scrollRT.offsetMin = new Vector2(190, 40); scrollRT.offsetMax = new Vector2(-60, -225);
        var scrollRect = scroll.AddComponent<ScrollRect>();
        scrollRect.horizontal = false; scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped; scrollRect.scrollSensitivity = 40f;
        var viewport = NewUI("Viewport", scroll.transform);
        Stretch(viewport);
        viewport.AddComponent<Image>().color = new Color(0, 0, 0, 0); // ドラッグを受けるための透明な面
        viewport.AddComponent<RectMask2D>();
        var contentGO = NewUI("Content", viewport.transform);
        SetRect(contentGO, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, Vector2.zero);
        var vl = contentGO.AddComponent<VerticalLayoutGroup>();
        vl.padding = new RectOffset(4, 4, 10, 10); vl.spacing = 14;
        vl.childControlWidth = true; vl.childControlHeight = true; vl.childForceExpandWidth = true; vl.childForceExpandHeight = false;
        contentGO.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.viewport = viewport.GetComponent<RectTransform>();
        scrollRect.content = contentGO.GetComponent<RectTransform>();

        // ---------------- 1行の見本（RowTemplate） ----------------
        var row = NewUI("RowTemplate", contentGO.transform);
        var rowBg = row.AddComponent<Image>();
        rowBg.sprite = sRow; rowBg.type = Image.Type.Sliced; rowBg.color = Cyan; rowBg.raycastTarget = false;
        var rowLE = row.AddComponent<LayoutElement>(); rowLE.minHeight = 160; rowLE.preferredHeight = 160; // ミッション名3行分で統一
        var rl = row.AddComponent<HorizontalLayoutGroup>();
        rl.padding = new RectOffset(padL, padR, 10, 10); rl.spacing = spacing; rl.childAlignment = TextAnchor.MiddleLeft;
        rl.childControlWidth = true; rl.childControlHeight = true; rl.childForceExpandWidth = false; rl.childForceExpandHeight = true;
        var rowUI = row.AddComponent<MissionRowUI>();

        // ミッション名（左揃え・本文用フォント）＋2段目の「II」
        var nameArea = NewUI("NameArea", row.transform);
        AddLayout(nameArea, -1, 1);
        var nhl = nameArea.AddComponent<HorizontalLayoutGroup>();
        nhl.spacing = 12; nhl.childAlignment = TextAnchor.MiddleLeft;
        nhl.childControlWidth = true; nhl.childControlHeight = true; nhl.childForceExpandWidth = false; nhl.childForceExpandHeight = true;
        var nameText = NewText("NameText", nameArea.transform, font, "線を累計100本引く", 34, Color.white, TextAlignmentOptions.MidlineLeft, LanguageFontSwitch.Kind.Body, false);
        nameText.textWrappingMode = TextWrappingModes.Normal;
        AddLayout(nameText.gameObject, -1, 1);
        var tierText = NewText("TierTag", nameArea.transform, font, "II", 26, Cyan, TextAlignmentOptions.Center, LanguageFontSwitch.Kind.Heading, true, alwaysEnglish: true);
        AddLayout(tierText.gameObject, 50, 0);

        // 進捗（バーと数字はどちらも真ん中揃え。数字はプレイ中に数字用フォントにする）
        var progArea = NewUI("ProgressArea", row.transform);
        AddLayout(progArea, progressW, 0);
        var barBg = NewUI("BarBg", progArea.transform);
        SetRect(barBg, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 14), new Vector2(0, 18));
        barBg.AddComponent<Image>().color = new Color(0.04f, 0.04f, 0.12f, 1f);
        barBg.AddComponent<Outline>().effectColor = new Color(0.15f, 0.15f, 0.35f, 1f);
        var fill = NewUI("Fill", barBg.transform);
        Stretch(fill);
        var fillImg = fill.AddComponent<Image>(); fillImg.color = Cyan; fillImg.raycastTarget = false;
        var progText = NewText("ProgressText", progArea.transform, font, "0 / 100", 28, Muted, TextAlignmentOptions.Center, null, false);
        SetRect(progText.gameObject, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(0, 36));

        // 報酬（真ん中揃え：種類の札＋中身）
        var reward = NewUI("RewardBox", row.transform);
        AddLayout(reward, rewardW, 0);
        var rvl = reward.AddComponent<VerticalLayoutGroup>();
        rvl.padding = new RectOffset(8, 8, 6, 6); rvl.spacing = 4; rvl.childAlignment = TextAnchor.MiddleCenter;
        rvl.childControlWidth = true; rvl.childControlHeight = true; rvl.childForceExpandWidth = false; rvl.childForceExpandHeight = false;
        var chip = NewUI("TypeChip", reward.transform);
        var chipImg = chip.AddComponent<Image>();
        chipImg.sprite = sPill; chipImg.type = Image.Type.Sliced; chipImg.color = new Color(Lime.r, Lime.g, Lime.b, 0.15f); chipImg.raycastTarget = false;
        var chipHl = chip.AddComponent<HorizontalLayoutGroup>();
        chipHl.padding = new RectOffset(16, 16, 2, 2); chipHl.childAlignment = TextAnchor.MiddleCenter;
        chipHl.childControlWidth = true; chipHl.childControlHeight = true; chipHl.childForceExpandWidth = false; chipHl.childForceExpandHeight = false;
        AddLayoutHeight(chip, 36);
        var rType = NewText("RewardType", chip.transform, font, "パッシブ", 24, Lime, TextAlignmentOptions.Center, LanguageFontSwitch.Kind.Heading, true);
        var rValue = NewText("RewardValue", reward.transform, font, "マルチライン", 28, Color.white, TextAlignmentOptions.Center, LanguageFontSwitch.Kind.Heading, true);
        rValue.enableAutoSizing = false; rValue.textWrappingMode = TextWrappingModes.Normal; // 長い報酬名は縮小せず折り返す（文字の大きさを統一）
        AddLayout(rValue.gameObject, -1, 1);
        var rGold = NewText("RewardGold", reward.transform, font, "300 G", 32, new Color(1f, 0.82f, 0.29f, 1f), TextAlignmentOptions.Center, null, false);
        AddLayout(rGold.gameObject, -1, 1); AddLayoutHeight(rGold.gameObject, 44);

        // 「受け取る」ボタン（呼吸する光＋背景＋文字。ホバーで拡大・点滅・SE。受取済みでは背景を消して文字だけ残す）
        var claim = NewUI("ClaimButton", row.transform);
        AddLayout(claim, buttonW, 0);
        var claimBtn = claim.AddComponent<Button>();
        claimBtn.transition = Selectable.Transition.None;
        var glow = NewUI("Glow", claim.transform);
        SetRect(glow, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20, 104));
        var glowImg = glow.AddComponent<Image>();
        glowImg.sprite = sClaim; glowImg.type = Image.Type.Sliced; glowImg.pixelsPerUnitMultiplier = 1.6f;
        glowImg.color = new Color(Lime.r, Lime.g, Lime.b, 0.4f); glowImg.raycastTarget = false;
        var claimBg = NewUI("ButtonBg", claim.transform);
        SetRect(claimBg, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(0, 84));
        var claimBgImg = claimBg.AddComponent<Image>();
        claimBgImg.sprite = sClaim; claimBgImg.type = Image.Type.Sliced;
        claimBgImg.pixelsPerUnitMultiplier = 2f; // 画像(763x182)よりボタン(230x84)が小さいので、四隅を半分の大きさで描く
        claimBtn.targetGraphic = claimBgImg;
        var claimText = NewText("ClaimText", claim.transform, font, "受け取る", 30, Color.white, TextAlignmentOptions.Center, LanguageFontSwitch.Kind.Heading, true);
        Stretch(claimText.gameObject);
        AddHoverEffect(claim, hoverSource, claimBgImg, null, requireInteractable: true);

        rowUI.background = rowBg;
        rowUI.nameText = nameText;
        rowUI.tierTag = tierText.gameObject;
        rowUI.tierText = tierText;
        rowUI.progressFill = fill.GetComponent<RectTransform>();
        rowUI.progressFillImage = fillImg;
        rowUI.progressText = progText;
        rowUI.rewardChipImage = chipImg;
        rowUI.rewardTypeText = rType;
        rowUI.rewardValueText = rValue;
        rowUI.rewardGoldText = rGold;
        rowUI.claimButton = claimBtn;
        rowUI.claimButtonImage = claimBgImg;
        rowUI.claimGlow = glowImg;
        rowUI.claimText = claimText;
        row.SetActive(false);

        // 受け取った時に浮かぶ文字の見本
        var pop = NewText("ClaimPopTemplate", panel.transform, font, "+300 G", 40, Lime, TextAlignmentOptions.Center, LanguageFontSwitch.Kind.Heading, true);
        SetRect(pop.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400, 60));
        pop.gameObject.SetActive(false);

        panel.SetActive(false);

        // ---------------- MissionPanelUIの参照と効果音 ----------------
        var so = new SerializedObject(panelUI);
        so.FindProperty("missionButton").objectReferenceValue = mbButton;
        so.FindProperty("badgeRoot").objectReferenceValue = badge;
        so.FindProperty("badgeText").objectReferenceValue = badgeText;
        so.FindProperty("panelRoot").objectReferenceValue = panel;
        so.FindProperty("closeButton").objectReferenceValue = closeBtn;
        so.FindProperty("titleText").objectReferenceValue = titleText;
        so.FindProperty("headerMissionText").objectReferenceValue = hMission;
        so.FindProperty("headerProgressText").objectReferenceValue = hProgress;
        so.FindProperty("headerRewardText").objectReferenceValue = hReward;
        so.FindProperty("scrollRect").objectReferenceValue = scrollRect;
        so.FindProperty("content").objectReferenceValue = contentGO.GetComponent<RectTransform>();
        so.FindProperty("rowTemplate").objectReferenceValue = rowUI;
        so.FindProperty("achievedLabelText").objectReferenceValue = achLabel;
        so.FindProperty("achievedCountText").objectReferenceValue = achCount;
        so.FindProperty("achievedTotalText").objectReferenceValue = achTotal;
        so.FindProperty("achievedGaugeFill").objectReferenceValue = gaugeFill.GetComponent<RectTransform>();
        so.FindProperty("claimPopTemplate").objectReferenceValue = pop;
        so.FindProperty("audioSource").objectReferenceValue = audio;
        so.FindProperty("goldClaimSE").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Gold/お金を落とす2.mp3");
        so.FindProperty("passiveSkillClaimSE").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SkillCard/データ表示2.mp3");
        so.FindProperty("passiveOtherClaimSE").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SkillCard/データ表示4.mp3");
        so.FindProperty("openCloseSE").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Title/決定ボタン.mp3");
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(panelUI);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = ui;
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
        if (hoverSource == null) Debug.LogWarning("[MissionPanelBuilder] BackButtonのButtonHoverEffectが見つからないため、閉じるボタン・受け取るボタンのホバー効果音は空です。Inspectorで設定してください");
        Debug.Log("[MissionPanelBuilder] 03_AreaSelectにMissionUI（Canvas）を作成しました（MissionButtonは既存のものを使用／無ければ作成）。シーンを保存してください");
    }

    // =====================================================================
    // 部品作成の補助
    // =====================================================================

    /// <summary>
    /// ホバーで拡大・点滅・効果音（ButtonHoverEffect）を付ける。設定値はAreaSelectのBackButtonから写して他のボタンとそろえる
    /// （点滅させる画像だけ、このボタンの物に差し替える）
    /// </summary>
    private static void AddHoverEffect(GameObject go, Game.UI.ButtonHoverEffect source, Image blinkTarget, Image[] additional, bool requireInteractable, float hoverScale = -1f)
    {
        var dst = go.AddComponent<Game.UI.ButtonHoverEffect>();
        if (source != null) EditorUtility.CopySerialized(source, dst);
        var so = new SerializedObject(dst);
        so.FindProperty("blinkTarget").objectReferenceValue = blinkTarget;
        var add = so.FindProperty("additionalBlinkTargets");
        add.arraySize = additional != null ? additional.Length : 0;
        for (int i = 0; i < add.arraySize; i++) add.GetArrayElementAtIndex(i).objectReferenceValue = additional[i];
        so.FindProperty("requireInteractable").boolValue = requireInteractable;
        so.FindProperty("lockAfterClick").boolValue = false;
        if (hoverScale > 0f) so.FindProperty("hoverScale").floatValue = hoverScale;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = LayerMask.NameToLayer("UI");
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void Stretch(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    private static void SetRect(GameObject go, Vector2 aMin, Vector2 aMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    /// <summary>
    /// 文字を作る。kindを指定するとLanguageFontSwitchを付けて、言語でフォントを自動で切り替える
    /// （日本語：見出し＝M PLUS 1 ExtraBold／本文＝M PLUS 1 Medium、英語：見出し＝Bungee／本文＝Rajdhani）。
    /// kind=null は数字だけの文字（プレイ中にGameNumberFontで数字用フォント＝Bungeeにする）
    /// </summary>
    private static TextMeshProUGUI NewText(string name, Transform parent, TMP_FontAsset font, string text, float size, Color color,
        TextAlignmentOptions align, LanguageFontSwitch.Kind? kind, bool bold, bool alwaysEnglish = false)
    {
        var go = NewUI(name, parent);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) { t.font = font; t.fontSharedMaterial = font.material; }
        t.text = text; t.fontSize = size; t.color = color; t.alignment = align;
        t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        if (kind.HasValue)
        {
            var sw = go.AddComponent<LanguageFontSwitch>();
            sw.kind = kind.Value;
            sw.alwaysUseEnglishFont = alwaysEnglish;
        }
        return t;
    }

    /// <summary>width&lt;0なら幅は文字や中身の大きさ（preferred）か、伸び縮み（flexibleWidth）で決める</summary>
    private static void AddLayout(GameObject go, float width, float flexible)
    {
        var le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        if (width >= 0) { le.minWidth = width; le.preferredWidth = width; }
        le.flexibleWidth = flexible;
    }

    private static void AddLayoutHeight(GameObject go, float height)
    {
        var le = go.GetComponent<LayoutElement>();
        if (le == null) le = go.AddComponent<LayoutElement>();
        le.minHeight = height; le.preferredHeight = height;
    }
}
