using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 02_Gem に「ジェムのリロール」（GemRerollUI）の部品をPlay前に作る（2026/11/08）。
/// ・リロールボタン（GemManagementPanelの子。左上の「スロットのアイコン」の左＝左のHUDとの間）
/// ・確認画面／結果画面（売却の確認画面 SellConfirmOverlay を複製して作る＝同じ見た目）
/// ・演出の部品（金貨・光の見本、入力止め）
/// 既にある時は作らない（作り直さない）。作った後の位置・大きさ・色・文言はInspectorで調整できる。
/// </summary>
public static class GemRerollBuilder
{
    private const string IconPath = "Assets/Art/AreaSelect/Shop/ジェムリロールアイコン.png";
    private const string OkFramePath = "Assets/Art/AreaSelect/「受け取る」ボタン.png";
    private const string FontAssetPath = "Assets/Fonts/NotoSansJP-Regular SDF.asset"; // 実際の文字はLanguageFontSwitch／GameNumberFontが差し替える
    private const string SeFolder = "Assets/Audio/GEM/";
    private const float ConfirmBgWidth = 1000f; // リロールの確認画面の背景の横幅（売却は800）
    private const string PlatePath ="Assets/Art/UI/GemFX/Gem_Pill.png"; // 右上の所持数の下地と同じ
    private static readonly Color PlateColor = new Color(0.04f, 0.06f, 0.13f, 0.85f);
    private static readonly Color LabelColor = Color.white;
    private static readonly Color PriceColor = new Color(1f, 0.88f, 0.35f, 1f);

    private static readonly Color Gold = new Color(1f, 0.85f, 0.3f, 1f);
    private static readonly Color White = new Color(0.95f, 0.96f, 1f, 1f);
    private static readonly Color Red = new Color(1f, 0.3f, 0.3f, 1f);
    private static readonly Color Muted = new Color(0.62f, 0.65f, 0.8f, 1f);

    [MenuItem("Tools/ジェム画面/ジェムのリロールを組み込む（02_Gemを開いて実行）")]
    private static void Build()
    {
        if (Object.FindFirstObjectByType<GemRerollUI>(FindObjectsInactive.Include) != null)
        {
            EditorUtility.DisplayDialog("ジェムのリロール", "既に組み込み済みです（GemRerollUI があります）。作り直しはしません。", "OK");
            return;
        }
        var gemUI = Object.FindFirstObjectByType<GemManagementUI>(FindObjectsInactive.Include);
        if (gemUI == null || gemUI.gameObject.scene.name != "02_Gem")
        {
            EditorUtility.DisplayDialog("ジェムのリロール", "02_Gem の GemManagementUI が見つかりません。02_Gem を開いてから実行してください。", "OK");
            return;
        }
        var gso = new SerializedObject(gemUI);
        var gemPanel = gso.FindProperty("gemPanel").objectReferenceValue as GameObject;
        var sellOverlay = gso.FindProperty("sellConfirmPanel").objectReferenceValue as GameObject;
        var sellButton = gso.FindProperty("sharedSellButton").objectReferenceValue as Button;
        var frameSprite = gso.FindProperty("neonFrameSprite").objectReferenceValue as Sprite;
        if (gemPanel == null || sellOverlay == null)
        {
            EditorUtility.DisplayDialog("ジェムのリロール", "GemManagementUI の Gem Panel か Sell Confirm Panel が未設定です。", "OK");
            return;
        }
        var canvas = gemUI.GetComponentInParent<Canvas>(true);
        if (canvas != null) canvas = canvas.rootCanvas;
        if (canvas == null) { EditorUtility.DisplayDialog("ジェムのリロール", "Canvas が見つかりません。", "OK"); return; }

        // アイコン画像をSpriteとして読めるように
        var importer = AssetImporter.GetAtPath(IconPath) as TextureImporter;
        if (importer != null && (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single))
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        var iconSprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
        var okFrame = AssetDatabase.LoadAssetAtPath<Sprite>(OkFramePath);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);

        // ゴールドのアイコン（右上のGoldHUD）
        var goldHud = Object.FindFirstObjectByType<GoldHUD>(FindObjectsInactive.Include);
        Image goldIcon = goldHud != null ? new SerializedObject(goldHud).FindProperty("goldIcon").objectReferenceValue as Image : null;
        Sprite goldSprite = goldIcon != null ? goldIcon.sprite : null;

        var log = new System.Text.StringBuilder();

        // ---------- 本体 ----------
        var host = NewUI("GemRerollUI", canvas.transform);
        Undo.RegisterCreatedObjectUndo(host, "ジェムのリロールを組み込む");
        Stretch(host);
        var ui = host.AddComponent<GemRerollUI>();
        var aus = host.AddComponent<AudioSource>(); aus.playOnAwake = false;

        // ---------- ① リロールボタン（GemManagementPanelの子・レイアウトの対象外） ----------
        var btnGo = NewUI("GemRerollButton", gemPanel.transform);
        btnGo.AddComponent<LayoutElement>().ignoreLayout = true;
        var brt = (RectTransform)btnGo.transform;
        brt.anchorMin = brt.anchorMax = new Vector2(0f, 1f); // パネルの左上を基準
        brt.pivot = new Vector2(0.5f, 0.5f);
        brt.anchoredPosition = new Vector2(-135f, -52f);      // スロットのアイコンの高さ・左のHUDとの間
        brt.sizeDelta = new Vector2(120f, 120f);
        var frame = btnGo.AddComponent<Image>();
        frame.sprite = frameSprite; frame.preserveAspect = false;
        var btn = btnGo.AddComponent<Button>();
        btn.transition = Selectable.Transition.None;
        btn.targetGraphic = frame;

        var iconGo = NewUI("Icon", btnGo.transform);
        var irt = (RectTransform)iconGo.transform;
        irt.anchorMin = irt.anchorMax = new Vector2(0.5f, 0.5f);
        irt.sizeDelta = new Vector2(88f, 88f);
        var icon = iconGo.AddComponent<Image>();
        icon.sprite = iconSprite; icon.preserveAspect = false; icon.raycastTarget = false;

        var label = NewText("Label", btnGo.transform, font, "リロール", 26f, White, LanguageFontSwitch.Kind.Heading);
        Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -22f), new Vector2(200f, 34f));
        var price = NewText("Price", btnGo.transform, font, "760 G", 26f, Gold, null);
        Place(price.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, -54f), new Vector2(200f, 32f));

        // ホバー：売却ボタンの設定をコピーして、光らせる対象だけ差し替える
        var hover = btnGo.AddComponent<Game.UI.ButtonHoverEffect>();
        var srcHover = sellButton != null ? sellButton.GetComponent<Game.UI.ButtonHoverEffect>() : null;
        if (srcHover != null) EditorUtility.CopySerialized(srcHover, hover);
        var hso = new SerializedObject(hover);
        hso.FindProperty("blinkTarget").objectReferenceValue = frame;
        var add = hso.FindProperty("additionalBlinkTargets"); add.arraySize = 1; add.GetArrayElementAtIndex(0).objectReferenceValue = icon;
        hso.FindProperty("requireInteractable").boolValue = true;
        hso.FindProperty("lockAfterClick").boolValue = true; // 売却・無限化ボタンと同じ：確認画面が閉じるまで拡大を維持
        hso.ApplyModifiedPropertiesWithoutUndo();
        log.AppendLine("・リロールボタン：GemManagementPanel > GemRerollButton");

        // ---------- ② 確認画面（売却の確認画面を複製） ----------
        var confirm = Object.Instantiate(sellOverlay, sellOverlay.transform.parent);
        confirm.name = "GemRerollConfirmOverlay";
        confirm.transform.SetSiblingIndex(sellOverlay.transform.GetSiblingIndex() + 1);
        Undo.RegisterCreatedObjectUndo(confirm, "ジェムのリロールの確認画面");
        ClearPersistentListeners(confirm);
        var cDialog = FindChild(confirm.transform, "SellConfirmDialog");
        if (cDialog != null) cDialog.name = "GemRerollConfirmDialog";
        DisableLayout(cDialog);
        DestroyChild(cDialog, "InfiniteStoneCountRow");
        var cText = FindChild(cDialog, "ConfirmText")?.GetComponent<TextMeshProUGUI>();
        if (cText != null)
        {
            cText.text = "宵闇の夜石をリロールしますか？\n<size=75%>付いているスキルと、スキルの数をすべて引き直します。</size>";
            cText.richText = true;
            cText.textWrappingMode = TextWrappingModes.Normal;
            Place(cText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 110f), new Vector2(700f, 120f));
        }
        var cost = NewText("CostText", cDialog, font, "760 G", 48f, Gold, null);
        Place(cost.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 25f), new Vector2(400f, 60f));
        // 所持ゴールド：［金貨］12,000 → ［金貨］11,240
        var goldRow = NewUI("GoldRow", cDialog);
        Place((RectTransform)goldRow.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -32f), new Vector2(520f, 44f));
        AddCoin(goldRow.transform, "BeforeCoin", goldSprite, new Vector2(-215f, 0f));
        var before = NewText("BeforeText", goldRow.transform, font, "12,000", 30f, White, null);
        before.alignment = TextAlignmentOptions.Left;
        Place(before.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-105f, 0f), new Vector2(180f, 44f));
        var arrow = NewText("Arrow", goldRow.transform, font, "→", 30f, Muted, null);
        Place(arrow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 0f), new Vector2(40f, 44f));
        AddCoin(goldRow.transform, "AfterCoin", goldSprite, new Vector2(45f, 0f));
        var after = NewText("AfterText", goldRow.transform, font, "11,240", 30f, White, null);
        after.alignment = TextAlignmentOptions.Left;
        Place(after.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(155f, 0f), new Vector2(180f, 44f));
        var notEnough = NewText("NotEnoughText", cDialog, font, "ゴールドが足りません", 26f, Red, LanguageFontSwitch.Kind.Body);
        Place(notEnough.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -78f), new Vector2(500f, 36f));
        notEnough.gameObject.SetActive(false);
        var cButtons = FindChild(cDialog, "ButtonContainer");
        if (cButtons != null) ((RectTransform)cButtons).anchoredPosition = new Vector2(((RectTransform)cButtons).anchoredPosition.x, -140f);
        var yes = FindChild(cButtons, "YesButton")?.GetComponent<Button>();
        var no = FindChild(cButtons, "NoButton")?.GetComponent<Button>();
        confirm.SetActive(false);
        log.AppendLine("・確認画面：Canvas > GemRerollConfirmOverlay");

        // ---------- ③ 結果画面（売却の確認画面を複製して中身を作り替え） ----------
        var result = Object.Instantiate(sellOverlay, sellOverlay.transform.parent);
        result.name = "GemRerollResultOverlay";
        result.transform.SetSiblingIndex(confirm.transform.GetSiblingIndex() + 1);
        Undo.RegisterCreatedObjectUndo(result, "ジェムのリロールの結果画面");
        ClearPersistentListeners(result);
        var rDialog = FindChild(result.transform, "SellConfirmDialog");
        if (rDialog != null) rDialog.name = "GemRerollResultDialog";
        DisableLayout(rDialog);
        DestroyChild(rDialog, "InfiniteStoneCountRow");
        DestroyChild(rDialog, "ButtonContainer");
        var rTitle = FindChild(rDialog, "ConfirmText")?.GetComponent<TextMeshProUGUI>();
        if (rTitle != null)
        {
            rTitle.gameObject.name = "TitleText";
            rTitle.text = "リロール結果";
            Place(rTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 168f), new Vector2(600f, 56f));
        }
        var bLabel = NewText("BeforeLabel", rDialog, font, "前", 26f, Muted, LanguageFontSwitch.Kind.Heading);
        Place(bLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-190f, 112f), new Vector2(330f, 36f));
        var aLabel = NewText("AfterLabel", rDialog, font, "後", 26f, new Color(0.72f, 1f, 0.24f, 1f), LanguageFontSwitch.Kind.Heading);
        Place(aLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(190f, 112f), new Vector2(330f, 36f));
        var rArrow = NewText("Arrow", rDialog, font, "▶", 34f, Muted, null);
        Place(rArrow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(40f, 50f));

        var uso = new SerializedObject(ui);
        var bRows = uso.FindProperty("beforeRows"); bRows.arraySize = 3;
        var aRows = uso.FindProperty("afterRows"); aRows.arraySize = 3;
        for (int i = 0; i < 3; i++)
        {
            float y = 64f - i * 62f;
            BuildResultRow(rDialog, "Before" + (i + 1), new Vector2(-190f, y), font, bRows.GetArrayElementAtIndex(i));
            BuildResultRow(rDialog, "After" + (i + 1), new Vector2(190f, y), font, aRows.GetArrayElementAtIndex(i));
        }
        var okGo = NewUI("OkButton", rDialog);
        Place((RectTransform)okGo.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -160f), new Vector2(240f, 84f));
        var okImg = okGo.AddComponent<Image>();
        okImg.sprite = okFrame; okImg.type = Image.Type.Sliced; okImg.pixelsPerUnitMultiplier = 1.4f;
        var okBtn = okGo.AddComponent<Button>();
        okBtn.transition = Selectable.Transition.None; okBtn.targetGraphic = okImg;
        var okLabel = NewText("Label", okGo.transform, font, "OK", 34f, White, LanguageFontSwitch.Kind.Heading);
        var olrt = okLabel.rectTransform; olrt.anchorMin = Vector2.zero; olrt.anchorMax = Vector2.one; olrt.offsetMin = olrt.offsetMax = Vector2.zero;
        var okHover = okGo.AddComponent<Game.UI.ButtonHoverEffect>();
        if (srcHover != null) EditorUtility.CopySerialized(srcHover, okHover);
        var ohso = new SerializedObject(okHover);
        ohso.FindProperty("blinkTarget").objectReferenceValue = okImg;
        ohso.FindProperty("additionalBlinkTargets").arraySize = 0;
        ohso.FindProperty("requireInteractable").boolValue = true;
        ohso.FindProperty("lockAfterClick").boolValue = false;
        ohso.ApplyModifiedPropertiesWithoutUndo();
        result.SetActive(false);
        log.AppendLine("・結果画面：Canvas > GemRerollResultOverlay");

        // ---------- ④ 演出の部品 ----------
        var fx = NewUI("GemRerollFX", canvas.transform);
        Stretch(fx);
        var blocker = fx.AddComponent<Image>();
        blocker.color = new Color(0f, 0f, 0f, 0f); // 透明・演出中のタップを止める
        var coinTpl = NewUI("CoinTemplate", fx.transform);
        var ctrt = (RectTransform)coinTpl.transform;
        ctrt.anchorMin = ctrt.anchorMax = new Vector2(0.5f, 0.5f); ctrt.sizeDelta = new Vector2(44f, 44f);
        var coinImg = coinTpl.AddComponent<Image>(); coinImg.sprite = goldSprite; coinImg.preserveAspect = true; coinImg.raycastTarget = false;
        coinTpl.SetActive(false);
        var flashTpl = NewUI("FlashTemplate", fx.transform);
        Stretch(flashTpl);
        flashTpl.AddComponent<LayoutElement>().ignoreLayout = true; // 行（HorizontalLayoutGroup）の子にしても並べ替えられないように
        var flashImg = flashTpl.AddComponent<Image>(); flashImg.color = Color.white; flashImg.raycastTarget = false;
        flashTpl.SetActive(false);
        fx.SetActive(false);
        log.AppendLine("・演出の部品：Canvas > GemRerollFX");

        // ---------- 参照・効果音 ----------
        uso.FindProperty("gemUI").objectReferenceValue = gemUI;
        uso.FindProperty("rerollButton").objectReferenceValue = btn;
        uso.FindProperty("buttonFrame").objectReferenceValue = frame;
        uso.FindProperty("buttonIcon").objectReferenceValue = icon;
        uso.FindProperty("buttonLabel").objectReferenceValue = label;
        uso.FindProperty("priceText").objectReferenceValue = price;
        uso.FindProperty("confirmOverlay").objectReferenceValue = confirm;
        uso.FindProperty("confirmText").objectReferenceValue = cText;
        uso.FindProperty("costText").objectReferenceValue = cost;
        uso.FindProperty("goldBeforeText").objectReferenceValue = before;
        uso.FindProperty("goldAfterText").objectReferenceValue = after;
        uso.FindProperty("notEnoughText").objectReferenceValue = notEnough;
        uso.FindProperty("confirmYesButton").objectReferenceValue = yes;
        uso.FindProperty("confirmNoButton").objectReferenceValue = no;
        uso.FindProperty("resultOverlay").objectReferenceValue = result;
        uso.FindProperty("resultTitleText").objectReferenceValue = rTitle;
        uso.FindProperty("resultBeforeLabel").objectReferenceValue = bLabel;
        uso.FindProperty("resultAfterLabel").objectReferenceValue = aLabel;
        uso.FindProperty("resultOkButton").objectReferenceValue = okBtn;
        uso.FindProperty("resultOkLabel").objectReferenceValue = okLabel;
        uso.FindProperty("fxLayer").objectReferenceValue = fx.transform;
        uso.FindProperty("coinTemplate").objectReferenceValue = coinImg;
        uso.FindProperty("flashTemplate").objectReferenceValue = flashImg;
        uso.FindProperty("goldTarget").objectReferenceValue = goldIcon != null ? goldIcon.rectTransform : null;

        uso.FindProperty("buttonSE").objectReferenceValue = gso.FindProperty("sellButtonSE").objectReferenceValue;
        uso.FindProperty("cancelSE").objectReferenceValue = gso.FindProperty("sellCancelSE").objectReferenceValue;
        uso.FindProperty("notEnoughSE").objectReferenceValue = gso.FindProperty("slotFullSE").objectReferenceValue;
        uso.FindProperty("paySE").objectReferenceValue = gso.FindProperty("sellSE").objectReferenceValue;
        uso.FindProperty("spinTickSE").objectReferenceValue = LoadSE("カーソル移動1.mp3");
        uso.FindProperty("rowLandSE").objectReferenceValue = LoadSE("決定ボタンを押す19.mp3");
        uso.FindProperty("rowAddSE").objectReferenceValue = LoadSE("ひらめく2.mp3");
        uso.FindProperty("rowRemoveSE").objectReferenceValue = LoadSE("刀を鞘にしまう1.mp3");
        uso.FindProperty("finishSE").objectReferenceValue = LoadSE("可愛く輝く1.mp3");
        uso.ApplyModifiedPropertiesWithoutUndo();

        if (goldSprite == null) log.AppendLine("⚠ GoldHUD のアイコンが見つからず、金貨の画像が空です（GemRerollUI の Coin Template / 確認画面の BeforeCoin・AfterCoin に設定してください）");
        if (iconSprite == null) log.AppendLine("⚠ " + IconPath + " が読めません");

        ApplyButtonLook(ui, log);
        ApplyConfirmLayout(ui, log);
        ApplyResultLayout(ui, log);

        EditorSceneManager.MarkSceneDirty(gemUI.gameObject.scene);
        SceneView.RepaintAll();
        Selection.activeObject = btnGo;
        Debug.Log("[GemRerollBuilder]\n" + log + "シーンを保存してください。");
        EditorUtility.DisplayDialog("ジェムのリロール", log + "\nシーンを保存してください。", "OK");
    }

    // =====================================================================
    // リロールボタンを他のボタン（無限化ボタン）と同じ大きさ・作りにし、文字の下に暗い下地を敷く（2026/11/08）
    // =====================================================================

    [MenuItem("Tools/ジェム画面/リロールボタンを他のボタンと同じ大きさにする（02_Gemを開いて実行）")]
    private static void ApplyButtonLookMenu()
    {
        var ui = Object.FindFirstObjectByType<GemRerollUI>(FindObjectsInactive.Include);
        if (ui == null) { EditorUtility.DisplayDialog("ジェムのリロール", "GemRerollUI が見つかりません。02_Gem を開いて、先に「ジェムのリロールを組み込む」を実行してください。", "OK"); return; }
        var log = new System.Text.StringBuilder();
        ApplyButtonLook(ui, log);
        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
        SceneView.RepaintAll();
        Debug.Log("[GemRerollBuilder]\n" + log + "シーンを保存してください。");
        EditorUtility.DisplayDialog("ジェムのリロール", log + "\nシーンを保存してください。", "OK");
    }

    private static void ApplyButtonLook(GemRerollUI ui, System.Text.StringBuilder log)
    {
        var uso = new SerializedObject(ui);
        var btn = uso.FindProperty("rerollButton").objectReferenceValue as Button;
        var icon = uso.FindProperty("buttonIcon").objectReferenceValue as Image;
        var label = uso.FindProperty("buttonLabel").objectReferenceValue as TMP_Text;
        var price = uso.FindProperty("priceText").objectReferenceValue as TMP_Text;
        var gemUI = uso.FindProperty("gemUI").objectReferenceValue as GemManagementUI;
        if (btn == null || gemUI == null) { log.AppendLine("⚠ リロールボタンか GemManagementUI の参照がありません"); return; }
        var gso = new SerializedObject(gemUI);
        var refBtn = gso.FindProperty("sharedInfiniteButton").objectReferenceValue as Button;
        var refFrame = gso.FindProperty("sharedInfiniteButtonImage").objectReferenceValue as Image;
        float iconSize = gso.FindProperty("actionIconSizeNoText").floatValue;

        var rt = (RectTransform)btn.transform;
        Undo.RecordObject(rt, "リロールボタンの大きさ");
        // ① 大きさ：無限化ボタンの枠（InfiniteBg）と同じ
        Vector2 frameSize = refFrame != null ? refFrame.rectTransform.sizeDelta : new Vector2(180f, 120f);
        rt.sizeDelta = frameSize;

        // ② 枠の中の暗い塗り（Fill）：無限化ボタンのFillを複製
        var refFill = refBtn != null ? refBtn.transform.Find("Fill") : null;
        var oldFill = btn.transform.Find("Fill");
        if (oldFill != null) Undo.DestroyObjectImmediate(oldFill.gameObject);
        if (refFill != null)
        {
            var fill = Object.Instantiate(refFill.gameObject, btn.transform);
            fill.name = "Fill";
            fill.transform.SetSiblingIndex(0);
            foreach (var g in fill.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
            Undo.RegisterCreatedObjectUndo(fill, "リロールボタンのFill");
        }

        // ③ アイコン：無限化ボタンと同じ大きさ（GemManagementUI の Action Icon Size No Text）
        if (icon != null)
        {
            Undo.RecordObject(icon.rectTransform, "リロールボタンのアイコン");
            icon.rectTransform.sizeDelta = new Vector2(iconSize, iconSize);
            icon.rectTransform.anchoredPosition = Vector2.zero;
            icon.transform.SetAsLastSibling();
        }

        // ④ 文字：暗い下地を敷き、文字を大きく・はっきりした色に
        var plateSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlatePath);
        var plateT = btn.transform.Find("TextPlate") as RectTransform;
        if (plateT == null)
        {
            var go = NewUI("TextPlate", btn.transform);
            plateT = (RectTransform)go.transform;
            Undo.RegisterCreatedObjectUndo(go, "リロールボタンの文字の下地");
        }
        plateT.anchorMin = plateT.anchorMax = new Vector2(0.5f, 0f);
        plateT.pivot = new Vector2(0.5f, 1f);
        plateT.anchoredPosition = new Vector2(0f, -4f);
        plateT.sizeDelta = new Vector2(frameSize.x, 76f);
        var plate = plateT.GetComponent<Image>();
        if (plate == null) plate = plateT.gameObject.AddComponent<Image>();
        plate.sprite = plateSprite; plate.type = plateSprite != null ? Image.Type.Sliced : Image.Type.Simple;
        plate.color = PlateColor; plate.raycastTarget = false;
        if (label != null)
        {
            Undo.RecordObject(label, "リロールの文字");
            label.transform.SetParent(plateT, false);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            label.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            label.rectTransform.anchoredPosition = new Vector2(0f, -20f);
            label.rectTransform.sizeDelta = new Vector2(frameSize.x, 34f);
            label.fontSize = 28f;
            label.color = LabelColor;
        }
        if (price != null)
        {
            Undo.RecordObject(price, "リロールの値段");
            price.transform.SetParent(plateT, false);
            price.rectTransform.anchorMin = price.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            price.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            price.rectTransform.anchoredPosition = new Vector2(0f, -54f);
            price.rectTransform.sizeDelta = new Vector2(frameSize.x, 32f);
            price.fontSize = 28f;
            price.color = PriceColor;
        }
        uso.FindProperty("priceColor").colorValue = PriceColor;
        uso.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(btn.gameObject);
        log.AppendLine($"・リロールボタンを無限化ボタンと同じ大きさ（枠 {frameSize.x}×{frameSize.y}、アイコン {iconSize}）にし、文字の下に暗い下地を敷きました");
    }

    // =====================================================================
    // 確認画面の配置を調整し直す（2026/11/08）：画面を大きく・所持金の行と説明文を削除・文字／値段／YES・NOを並べ直す
    // =====================================================================

    [MenuItem("Tools/ジェム画面/リロールの確認画面の配置を調整し直す（02_Gemを開いて実行）")]
    private static void ApplyConfirmLayoutMenu()
    {
        var ui = Object.FindFirstObjectByType<GemRerollUI>(FindObjectsInactive.Include);
        if (ui == null) { EditorUtility.DisplayDialog("ジェムのリロール", "GemRerollUI が見つかりません。02_Gem を開いて、先に「ジェムのリロールを組み込む」を実行してください。", "OK"); return; }
        var log = new System.Text.StringBuilder();
        ApplyConfirmLayout(ui, log);
        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
        SceneView.RepaintAll();
        Debug.Log("[GemRerollBuilder]\n" + log + "シーンを保存してください。");
        EditorUtility.DisplayDialog("ジェムのリロール", log + "\nシーンを保存してください。", "OK");
    }

    private static void ApplyConfirmLayout(GemRerollUI ui, System.Text.StringBuilder log)
    {
        var uso = new SerializedObject(ui);
        var text = uso.FindProperty("confirmText").objectReferenceValue as TMP_Text;
        var cost = uso.FindProperty("costText").objectReferenceValue as TMP_Text;
        var notEnough = uso.FindProperty("notEnoughText").objectReferenceValue as TMP_Text;
        var goldBefore = uso.FindProperty("goldBeforeText").objectReferenceValue as TMP_Text;
        var yes = uso.FindProperty("confirmYesButton").objectReferenceValue as Button;
        if (text == null) { log.AppendLine("⚠ 確認画面の文字（Confirm Text）の参照がありません"); return; }
        var dialog = text.transform.parent;

        // ① 所持金の行（12,000 → 11,240）を削除
        if (goldBefore != null && goldBefore.transform.parent != null && goldBefore.transform.parent.name == "GoldRow")
        {
            Undo.DestroyObjectImmediate(goldBefore.transform.parent.gameObject);
            log.AppendLine("・所持金の行（GoldRow）を削除しました");
        }
        uso.FindProperty("goldBeforeText").objectReferenceValue = null;
        uso.FindProperty("goldAfterText").objectReferenceValue = null;
        uso.ApplyModifiedPropertiesWithoutUndo();

        // ② 売却の確認画面（SellConfirmDialog）と同じ大きさ・文字・配置にする（値を写す）
        var gemUI = uso.FindProperty("gemUI").objectReferenceValue as GemManagementUI;
        var sellText = gemUI != null ? new SerializedObject(gemUI).FindProperty("sellConfirmText").objectReferenceValue as TextMeshProUGUI : null;
        if (sellText == null) { log.AppendLine("⚠ 売却の確認画面の文字（GemManagementUI の Sell Confirm Text）が見つかりません"); return; }
        var sellDialog = sellText.transform.parent;

        var myBg = FindChild(dialog, "ConfirmBg") as RectTransform;
        CopyRect(FindChild(sellDialog, "ConfirmBg") as RectTransform, myBg);
        // 横幅だけ広げる：「このジェムを○○Gでリロールしますか？」は売却の文より長く、売却と同じ幅だとはみ出すため（高さ・文字の大きさは売却と同じ）
        if (myBg != null) myBg.sizeDelta = new Vector2(ConfirmBgWidth, myBg.sizeDelta.y);

        // 文字：売却の文字の設定（フォント・大きさ・折り返し・配置）をそのまま写し、文だけ変える
        Undo.RecordObject(text, "確認画面の文字");
        Undo.RecordObject(text.rectTransform, "確認画面の文字の位置");
        EditorUtility.CopySerialized(sellText, text);
        text.text = "このジェムを400Gでリロールしますか？";
        CopyRect(sellText.rectTransform, text.rectTransform);

        // YES・NO：売却と同じ位置・大きさ
        var sellYes = new SerializedObject(gemUI).FindProperty("sellConfirmYesBtn").objectReferenceValue as Button;
        var buttons = yes != null ? yes.transform.parent as RectTransform : null;
        CopyRect(sellYes != null ? sellYes.transform.parent as RectTransform : null, buttons);

        // ③ 値段だけの行は使わない（文の中に値段が入るため）
        if (cost != null)
        {
            Undo.RecordObject(cost.gameObject, "値段の行を隠す");
            cost.gameObject.SetActive(false);
        }
        uso.FindProperty("costText").objectReferenceValue = null;
        uso.ApplyModifiedPropertiesWithoutUndo();

        // ④ ゴールドが足りない時の文字：YES・NOの下（足りない時だけ出る）
        if (notEnough != null && buttons != null)
        {
            Undo.RecordObject(notEnough.rectTransform, "ゴールドが足りません");
            Place(notEnough.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, buttons.anchoredPosition.y - 80f), new Vector2(600f, 36f));
            notEnough.fontSize = 26f;
        }
        EditorUtility.SetDirty(dialog.gameObject);
        log.AppendLine($"・確認画面：売却の確認画面と同じ文字・配置にし、背景の横幅だけ {ConfirmBgWidth} に広げました（値段だけの行は隠しています）");
    }

    // =====================================================================
    // 結果画面の配置を調整し直す（2026/11/08）：画面を広く・行を枠の内側に収める・▶を真ん中の行の高さに・見出しの文言
    // =====================================================================

    // 背景の画像（MainBg.png）は外側が透明で、見える枠は指定サイズの約8割。1200では行（左右±485）が枠からはみ出したため広げた
    private static readonly Vector2 ResultBgSize = new Vector2(1440f, 800f);
    private const float ResultColumnX = 265f;   // 左右の列の中心（画面の中心からの距離）
    private const float ResultRowWidth = 440f;
    private const float ResultRowHeight = 70f;  // ジェム画面のカードの行（Skill Row Height）と同じ
    private const float ResultRowGap = 82f;     // 行と行の中心の間隔

    [MenuItem("Tools/ジェム画面/リロールの結果画面の配置を調整し直す（02_Gemを開いて実行）")]
    private static void ApplyResultLayoutMenu()
    {
        var ui = Object.FindFirstObjectByType<GemRerollUI>(FindObjectsInactive.Include);
        if (ui == null) { EditorUtility.DisplayDialog("ジェムのリロール", "GemRerollUI が見つかりません。02_Gem を開いて、先に「ジェムのリロールを組み込む」を実行してください。", "OK"); return; }
        var log = new System.Text.StringBuilder();
        ApplyResultLayout(ui, log);
        EditorSceneManager.MarkSceneDirty(ui.gameObject.scene);
        SceneView.RepaintAll();
        Debug.Log("[GemRerollBuilder]\n" + log + "シーンを保存してください。");
        EditorUtility.DisplayDialog("ジェムのリロール", log + "\nシーンを保存してください。", "OK");
    }

    private static void ApplyResultLayout(GemRerollUI ui, System.Text.StringBuilder log)
    {
        var uso = new SerializedObject(ui);
        var title = uso.FindProperty("resultTitleText").objectReferenceValue as TMP_Text;
        var bLabel = uso.FindProperty("resultBeforeLabel").objectReferenceValue as TMP_Text;
        var aLabel = uso.FindProperty("resultAfterLabel").objectReferenceValue as TMP_Text;
        var ok = uso.FindProperty("resultOkButton").objectReferenceValue as Button;
        if (title == null) { log.AppendLine("⚠ 結果画面の見出し（Result Title Text）の参照がありません"); return; }
        var dialog = title.transform.parent;

        // 背景を広く
        var bg = FindChild(dialog, "ConfirmBg") as RectTransform;
        if (bg != null) { Undo.RecordObject(bg, "結果画面の背景"); bg.anchoredPosition = Vector2.zero; bg.sizeDelta = ResultBgSize; }

        float top = ResultBgSize.y / 2f;
        // 見出し
        Undo.RecordObject(title.rectTransform, "結果画面の見出し");
        Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, top - 145f), new Vector2(ResultBgSize.x - 300f, 80f));
        Undo.RecordObject(title, "結果画面の見出しの大きさ");
        title.enableAutoSizing = false;
        title.fontSize = 52f;

        // 見出しの左右に、リロールボタンと同じ回転矢印のアイコン（位置は文字の長さに合わせてGemRerollUIが毎回並べる）
        var iconSprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath);
        var iconL = EnsureTitleIcon(dialog, "TitleIconLeft", iconSprite);
        var iconR = EnsureTitleIcon(dialog, "TitleIconRight", iconSprite);
        uso.FindProperty("resultTitleIconLeft").objectReferenceValue = iconL;
        uso.FindProperty("resultTitleIconRight").objectReferenceValue = iconR;
        float half = title.GetPreferredValues("リロール結果").x * 0.5f, gap = uso.FindProperty("resultTitleIconGap").floatValue;
        iconL.anchoredPosition = new Vector2(-half - gap, title.rectTransform.anchoredPosition.y);
        iconR.anchoredPosition = new Vector2(half + gap, title.rectTransform.anchoredPosition.y);

        // 列の見出し（元のスキル／新しいスキル）
        if (bLabel != null) { Undo.RecordObject(bLabel.rectTransform, "列の見出し"); Place(bLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-ResultColumnX, ResultRowGap + 72f), new Vector2(ResultRowWidth, 40f)); bLabel.fontSize = 28f; }
        if (aLabel != null) { Undo.RecordObject(aLabel.rectTransform, "列の見出し"); Place(aLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(ResultColumnX, ResultRowGap + 72f), new Vector2(ResultRowWidth, 40f)); aLabel.fontSize = 28f; }
        uso.FindProperty("beforeLabelJa").stringValue = "元のスキル";
        uso.FindProperty("afterLabelJa").stringValue = "新しいスキル";

        // 行：ジェム画面のカードの行（GemItemTemplateのSkillRow_Base）を複製して使う＝同じ作り（カテゴリ色の背景・左端の縦線・上縁の光・黒い太字）
        var gemUIRef = uso.FindProperty("gemUI").objectReferenceValue as GemManagementUI;
        var gso2 = gemUIRef != null ? new SerializedObject(gemUIRef) : null;
        var template = gso2 != null ? gso2.FindProperty("gemItemTemplate").objectReferenceValue as GameObject : null;
        var templateRow = template != null ? template.transform.Find("TextContainer/SkillIconsContainer/SkillRow_Base") : null;
        if (templateRow == null) log.AppendLine("⚠ ジェムカードの見本の行（GemItemTemplate > TextContainer > SkillIconsContainer > SkillRow_Base）が見つかりません");
        if (gso2 != null)
        {
            // カテゴリの色はジェム画面（GemManagementUI）と同じ値
            uso.FindProperty("categoryAColor").colorValue = gso2.FindProperty("categoryAColor").colorValue;
            uso.FindProperty("categoryBColor").colorValue = gso2.FindProperty("categoryBColor").colorValue;
            uso.FindProperty("categoryCColor").colorValue = gso2.FindProperty("categoryCColor").colorValue;
        }
        float iconColW = gso2 != null ? gso2.FindProperty("iconColumnWidth").floatValue : 80f;

        // 3行を上・真ん中・下に（真ん中の行が画面の中心の高さ）
        foreach (var (prop, sign, prefix) in new[] { ("beforeRows", -1f, "Before"), ("afterRows", 1f, "After") })
        {
            var rows = uso.FindProperty(prop);
            for (int i = 0; i < rows.arraySize; i++)
            {
                var el = rows.GetArrayElementAtIndex(i);
                var root = el.FindPropertyRelative("root").objectReferenceValue as GameObject;
                // まだ前の作り（灰色の行）なら、カードの行の複製に置き換える
                if (templateRow != null && (root == null || root.transform.Find("IconContainer") == null))
                {
                    if (root != null) Undo.DestroyObjectImmediate(root);
                    root = Object.Instantiate(templateRow.gameObject, dialog);
                    root.name = prefix + (i + 1);
                    root.SetActive(true);
                    Undo.RegisterCreatedObjectUndo(root, "結果画面の行");
                }
                if (root == null) continue;
                var rrt = (RectTransform)root.transform;
                Undo.RecordObject(rrt, "結果画面の行");
                Place(rrt, new Vector2(0.5f, 0.5f), new Vector2(sign * ResultColumnX, ResultRowGap * (1 - i)), new Vector2(ResultRowWidth, ResultRowHeight));
                rrt.localScale = Vector3.one;

                var iconCont = root.transform.Find("IconContainer");
                var icLE = iconCont != null ? iconCont.GetComponent<LayoutElement>() : null;
                if (icLE != null) { icLE.minWidth = icLE.preferredWidth = iconColW; icLE.preferredHeight = ResultRowHeight; }
                var nm = root.transform.Find("SkillName")?.GetComponent<TextMeshProUGUI>();
                if (nm != null)
                {
                    nm.enableAutoSizing = true; nm.fontSizeMax = 24f; nm.fontSizeMin = 16f; // 長いスキル名でもはみ出さない（文字色・太字はカードのまま）
                    nm.overflowMode = TextOverflowModes.Ellipsis;
                }
                el.FindPropertyRelative("root").objectReferenceValue = root;
                el.FindPropertyRelative("icon").objectReferenceValue = iconCont != null ? iconCont.Find("IconImage")?.GetComponent<Image>() : null;
                el.FindPropertyRelative("nameText").objectReferenceValue = nm;
                el.FindPropertyRelative("background").objectReferenceValue = root.GetComponent<Image>();
                el.FindPropertyRelative("accent").objectReferenceValue = root.transform.Find("FX_Accent")?.GetComponent<Image>();
            }
        }

        // ▶：左右の列の真ん中・真ん中の行の高さ
        var arrow = FindChild(dialog, "Arrow");
        if (arrow != null)
        {
            var art = (RectTransform)arrow;
            Undo.RecordObject(art, "結果画面の矢印");
            Place(art, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f, 60f));
            var at = arrow.GetComponent<TMP_Text>();
            if (at != null) { Undo.RecordObject(at, "結果画面の矢印"); at.fontSize = 40f; at.alignment = TextAlignmentOptions.Center; }
        }

        // OK
        if (ok != null)
        {
            var ort = (RectTransform)ok.transform;
            Undo.RecordObject(ort, "OKボタン");
            Place(ort, new Vector2(0.5f, 0.5f), new Vector2(0f, -top + 175f), new Vector2(360f, 116f));
            var okText = ok.GetComponentInChildren<TMP_Text>(true);
            if (okText != null) { Undo.RecordObject(okText, "OKの文字"); okText.fontSize = 44f; }
        }
        uso.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(dialog.gameObject);
        log.AppendLine($"・結果画面：背景 {ResultBgSize.x}×{ResultBgSize.y}・OKボタン 360×116・見出し52、行を枠の内側に収め、▶を真ん中の行の高さに、見出しを「元のスキル」「新しいスキル」にし、「リロール結果」の左右にアイコンを置き、行をジェムカードと同じ作り（カテゴリ色）にしました");
    }

    private const float TitleIconSize = 64f;

    /// <summary>見出しの横のアイコンを作る（既にあれば大きさと画像だけ直す）</summary>
    private static RectTransform EnsureTitleIcon(Transform dialog, string name, Sprite sprite)
    {
        var t = dialog.Find(name) as RectTransform;
        if (t == null)
        {
            var go = NewUI(name, dialog);
            t = (RectTransform)go.transform;
            Undo.RegisterCreatedObjectUndo(go, "見出しのアイコン");
        }
        var img = t.GetComponent<Image>();
        if (img == null) img = t.gameObject.AddComponent<Image>();
        Undo.RecordObject(t, "見出しのアイコン");
        t.anchorMin = t.anchorMax = new Vector2(0.5f, 0.5f);
        t.pivot = new Vector2(0.5f, 0.5f);
        t.sizeDelta = new Vector2(TitleIconSize, TitleIconSize);
        img.sprite = sprite; img.preserveAspect = true; img.raycastTarget = false;
        return t;
    }

    /// <summary>位置・大きさ・拡大率だけを写す（親は変えない）</summary>
    private static void CopyRect(RectTransform from, RectTransform to)
    {
        if (from == null || to == null) return;
        Undo.RecordObject(to, "位置と大きさを揃える");
        to.anchorMin = from.anchorMin;
        to.anchorMax = from.anchorMax;
        to.pivot = from.pivot;
        to.anchoredPosition = from.anchoredPosition;
        to.sizeDelta = from.sizeDelta;
        to.localScale = from.localScale;
    }

    private static void BuildResultRow(Transform parent, string name, Vector2 pos, TMP_FontAsset font, SerializedProperty prop)
    {
        var row = NewUI(name, parent);
        Place((RectTransform)row.transform, new Vector2(0.5f, 0.5f), pos, new Vector2(330f, 54f));
        var bg = row.AddComponent<Image>(); bg.color = new Color(1f, 1f, 1f, 0.06f); bg.raycastTarget = false;
        var iconGo = NewUI("Icon", row.transform);
        Place((RectTransform)iconGo.transform, new Vector2(0f, 0.5f), new Vector2(32f, 0f), new Vector2(44f, 44f));
        var icon = iconGo.AddComponent<Image>(); icon.preserveAspect = true; icon.raycastTarget = false;
        var nm = NewText("Name", row.transform, font, "スキル名", 24f, White, LanguageFontSwitch.Kind.Body);
        nm.alignment = TextAlignmentOptions.Left;
        nm.overflowMode = TextOverflowModes.Ellipsis;
        var nrt = nm.rectTransform;
        nrt.anchorMin = new Vector2(0f, 0f); nrt.anchorMax = new Vector2(1f, 1f);
        nrt.offsetMin = new Vector2(64f, 0f); nrt.offsetMax = new Vector2(-8f, 0f);
        prop.FindPropertyRelative("root").objectReferenceValue = row;
        prop.FindPropertyRelative("icon").objectReferenceValue = icon;
        prop.FindPropertyRelative("nameText").objectReferenceValue = nm;
    }

    private static void AddCoin(Transform parent, string name, Sprite sprite, Vector2 pos)
    {
        var go = NewUI(name, parent);
        Place((RectTransform)go.transform, new Vector2(0.5f, 0.5f), pos, new Vector2(36f, 36f));
        var img = go.AddComponent<Image>(); img.sprite = sprite; img.preserveAspect = true; img.raycastTarget = false;
    }

    /// <summary>複製したダイアログの縦並び（VerticalLayoutGroup）を止める。中の部品は位置を直接指定するため（元の部品は保存済みの位置のまま）</summary>
    private static void DisableLayout(Transform dialog)
    {
        if (dialog == null) return;
        var vlg = dialog.GetComponent<VerticalLayoutGroup>();
        if (vlg != null) vlg.enabled = false;
    }

    private static AudioClip LoadSE(string file) => AssetDatabase.LoadAssetAtPath<AudioClip>(SeFolder + file);

    /// <summary>複製したボタンに、元の画面の処理（Inspectorで登録したOnClick）が残らないように消す</summary>
    private static void ClearPersistentListeners(GameObject root)
    {
        foreach (var b in root.GetComponentsInChildren<Button>(true))
            for (int i = b.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(b.onClick, i);
    }

    private static Transform FindChild(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name.Trim() == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var r = FindChild(root.GetChild(i), name);
            if (r != null) return r;
        }
        return null;
    }

    private static void DestroyChild(Transform root, string name)
    {
        var t = FindChild(root, name);
        if (t != null && t != root) Object.DestroyImmediate(t.gameObject);
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
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private static void Place(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private static TextMeshProUGUI NewText(string name, Transform parent, TMP_FontAsset font, string text, float size, Color color,
        LanguageFontSwitch.Kind? kind)
    {
        var go = NewUI(name, parent);
        var t = go.AddComponent<TextMeshProUGUI>();
        if (font != null) { t.font = font; t.fontSharedMaterial = font.material; }
        t.text = text; t.fontSize = size; t.color = color;
        t.alignment = TextAlignmentOptions.Center;
        t.fontStyle = FontStyles.Bold;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        if (kind.HasValue) go.AddComponent<LanguageFontSwitch>().kind = kind.Value;
        return t;
    }
}
