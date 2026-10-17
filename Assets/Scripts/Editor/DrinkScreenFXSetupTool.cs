using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// ドリンク購入画面（04_Drink）の見やすさ改善（1回目）をPlay前に組み込む。
/// DrinkCardTemplate（カードの見本）と上の行に、DrinkScreenFX用の部品を「追加」するだけで、既存の部品は削除・作り直ししない。
/// 何度実行しても安全。画像はジェム画面で生成したもの（Assets/Art/UI/GemFX）を使い回す
/// </summary>
public static class DrinkScreenFXSetupTool
{
    private const string ScenePath = "Assets/Scenes/04_Drink.unity";
    private const string GemArt = "Assets/Art/UI/GemFX";
    private const string FontPath = "Assets/Fonts/NotoSansJP-Regular SDF.asset";

    [MenuItem("Tools/ドリンク画面/見やすさ改善（1回目）を組み込む（04_Drink）")]
    private static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var sp = LoadSprites();
        if (sp == null) return;

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool wasLoaded = scene.IsValid() && scene.isLoaded;
        if (!wasLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        ShopUI shop = null;
        foreach (var root in scene.GetRootGameObjects()) { shop = root.GetComponentInChildren<ShopUI>(true); if (shop != null) break; }
        if (shop == null) Debug.LogError("[DrinkScreenFXSetupTool] 04_DrinkにShopUIが見つかりません");
        else if (Build(shop, scene, sp))
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[DrinkScreenFXSetupTool] 04_Drinkのドリンク画面に見やすさ改善（1回目）を組み込んで保存しました（文言{AddLocalization()}件追加）");
        }
        if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
    }

    private static Dictionary<string, Sprite> LoadSprites()
    {
        var d = new Dictionary<string, Sprite>();
        foreach (var (key, file) in new[] { ("pill", "Gem_Pill"), ("frame", "Gem_SelectFrame"), ("corner", "Gem_Corner"), ("glow", "Gem_Glow"), ("ring", "Gem_Ring"), ("star", "Gem_Star") })
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>($"{GemArt}/{file}.png");
            if (s == null)
            {
                EditorUtility.DisplayDialog("ドリンク画面", $"{GemArt}/{file}.png が見つかりません。先にジェム画面のメニュー（見やすさ改善・演出）を実行してください。", "OK");
                return null;
            }
            d[key] = s;
        }
        return d;
    }

    // =====================================================================
    // 2回目：演出（⑧〜⑫）
    // =====================================================================

    [MenuItem("Tools/ドリンク画面/演出（2回目）を組み込む（04_Drink）")]
    private static void Run2()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var sp = LoadSprites();
        if (sp == null) return;
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool wasLoaded = scene.IsValid() && scene.isLoaded;
        if (!wasLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        ShopUI shop = null;
        foreach (var root in scene.GetRootGameObjects()) { shop = root.GetComponentInChildren<ShopUI>(true); if (shop != null) break; }
        if (shop == null) Debug.LogError("[DrinkScreenFXSetupTool] 04_DrinkにShopUIが見つかりません");
        else if (shop.GetComponent<DrinkScreenFX>() == null) Debug.LogError("[DrinkScreenFXSetupTool] 先に「見やすさ改善（1回目）を組み込む」を実行してください");
        else if (Build2(shop, scene, sp))
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[DrinkScreenFXSetupTool] 04_Drinkのドリンク画面に演出（2回目）を組み込んで保存しました");
        }
        if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
    }

    private static bool Build2(ShopUI shop, Scene scene, Dictionary<string, Sprite> sp)
    {
        var o = new SerializedObject(shop);
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        var shopPanelGo = o.FindProperty("shopPanel").objectReferenceValue as GameObject;
        Transform panelT = shopPanelGo != null ? shopPanelGo.transform : FindDeep(shop.transform.root, "ShopPanel");
        if (panelT == null) { Debug.LogError("[DrinkScreenFXSetupTool] ShopPanelが見つかりません"); return false; }

        // 最前面の入れ物（粒・飛ぶドリンク・コイン・吹き出し）
        var front = Child(panelT, "FX_DrinkFront"); IgnoreLayout(front);
        front.anchorMin = Vector2.zero; front.anchorMax = Vector2.one; front.pivot = new Vector2(0.5f, 0.5f); front.offsetMin = front.offsetMax = Vector2.zero;
        front.SetAsLastSibling();
        var cg = front.GetComponent<CanvasGroup>(); if (cg == null) cg = front.gameObject.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false; cg.interactable = false;
        var parts = new List<Image>();
        for (int i = 0; i < 40; i++)
        {
            var p = Child(front, "Particle" + i); p.anchorMin = p.anchorMax = p.pivot = new Vector2(0.5f, 0.5f); p.sizeDelta = new Vector2(20, 20);
            var im = Img(p, sp["glow"], Color.white); im.enabled = false; parts.Add(im);
        }
        var fly = Child(front, "FlyingDrink"); fly.anchorMin = fly.anchorMax = fly.pivot = new Vector2(0.5f, 0.5f); fly.sizeDelta = new Vector2(100, 100);
        var flyImg = Img(fly, null, Color.white); flyImg.preserveAspect = true; flyImg.enabled = false;

        // ⑨（店主の吹き出し）はユーザー判断で不要：以前の版で作った部品が残っていれば削除
        var oldBubble = front.Find("SpeechBubble");
        if (oldBubble != null) Object.DestroyImmediate(oldBubble.gameObject);

        // ⑫（背景の泡・星）はユーザー判断で不要：以前の版で作った部品が残っていれば削除
        var bg = o.FindProperty("shopBgImage").objectReferenceValue as Image;
        Transform bgT = bg != null ? bg.transform : FindDeep(shop.transform.root, "ShopBgImage");
        var oldBg = bgT != null ? bgT.Find("FX_BgAnim") : null;
        if (oldBg != null) Object.DestroyImmediate(oldBg.gameObject);

        Sprite coin = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            var hud = root.GetComponentInChildren<GoldHUD>(true);
            if (hud == null) continue;
            coin = hud.transform.Find("GoldIcon")?.GetComponent<Image>()?.sprite;
            break;
        }
        GemSkillPreviewHUD preview = null;
        foreach (var root in scene.GetRootGameObjects()) { preview = root.GetComponentInChildren<GemSkillPreviewHUD>(true); if (preview != null) break; }

        var anim = shop.GetComponent<DrinkScreenFXAnim>();
        if (anim == null) anim = shop.gameObject.AddComponent<DrinkScreenFXAnim>();
        var so = new SerializedObject(anim);
        so.FindProperty("shop").objectReferenceValue = shop;
        so.FindProperty("screenFX").objectReferenceValue = shop.GetComponent<DrinkScreenFX>();
        so.FindProperty("previewHUD").objectReferenceValue = preview;
        so.FindProperty("frontRoot").objectReferenceValue = front;
        var pp = so.FindProperty("particles"); pp.arraySize = parts.Count; for (int i = 0; i < parts.Count; i++) pp.GetArrayElementAtIndex(i).objectReferenceValue = parts[i];
        so.FindProperty("flyingDrink").objectReferenceValue = flyImg;
        so.FindProperty("glowSprite").objectReferenceValue = sp["glow"];
        so.FindProperty("coinSprite").objectReferenceValue = coin != null ? coin : sp["glow"];
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(anim);

        return true;
    }

    private static bool Build(ShopUI shop, Scene scene, Dictionary<string, Sprite> sp)
    {
        var o = new SerializedObject(shop);
        var tplUI = o.FindProperty("drinkCardTemplate").objectReferenceValue as DrinkCardUI;
        var iconCont = o.FindProperty("drinkIconContainer").objectReferenceValue as RectTransform;
        if (tplUI == null) { Debug.LogError("[DrinkScreenFXSetupTool] drinkCardTemplateが未設定です"); return false; }
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        var card = tplUI.transform;

        // ⑤ 選択中：カード背景を暗く染める明滅 → 少し明るくなる明滅に（明滅処理はそのまま、色だけ）
        var tso = new SerializedObject(tplUI);
        tso.FindProperty("selectedHighlightColor").colorValue = Color.white;
        tso.FindProperty("selectedPulseColor").colorValue = new Color(0.85f, 0.93f, 1f, 1f);
        tso.ApplyModifiedPropertiesWithoutUndo();

        // ① スキル行（ジェム画面と同じ）
        var skills = card.Find("SkillsContainer");
        if (skills != null)
            for (int r = 1; r <= 3; r++)
            {
                var row = skills.Find("SkillRow_" + r) as RectTransform;
                if (row == null) continue;
                var grad = row.GetComponent<UIHorizontalGradient>();
                if (grad == null) grad = row.gameObject.AddComponent<UIHorizontalGradient>();
                grad.colorLeft = Color.white; grad.colorRight = new Color(0.66f, 0.68f, 0.8f, 1f); grad.shimmer = false;
                var acc = Child(row, "FX_Accent"); IgnoreLayout(acc);
                acc.anchorMin = new Vector2(0f, 0f); acc.anchorMax = new Vector2(0f, 1f); acc.pivot = new Vector2(0f, 0.5f);
                acc.anchoredPosition = Vector2.zero; acc.sizeDelta = new Vector2(6f, 0f);
                Img(acc, null, Color.white);
                var gl = Child(row, "FX_Gloss"); IgnoreLayout(gl);
                gl.anchorMin = new Vector2(0f, 1f); gl.anchorMax = new Vector2(1f, 1f); gl.pivot = new Vector2(0.5f, 1f);
                gl.anchoredPosition = Vector2.zero; gl.sizeDelta = new Vector2(0f, 2f);
                Img(gl, null, new Color(1f, 1f, 1f, 0.45f));
            }

        // ② 価格を大きく（ゴールドのアイコンは文字に重ならないよう少し左へ）
        var price = card.Find("NamePriceRow/PriceText")?.GetComponent<TextMeshProUGUI>();
        if (price != null) { price.fontSize = 26; price.fontStyle = FontStyles.Bold; }
        var goldIcon = card.Find("NamePriceRow/GoldIcon") as RectTransform;
        if (goldIcon != null) { goldIcon.sizeDelta = new Vector2(32f, 32f); goldIcon.anchoredPosition = new Vector2(-68f, 0f); }

        // ② ゴールド不足（カードを暗くして中央に文字。クリックは下のカードへ通す）
        var ng = Child(card, "FX_NoGold");
        ng.anchorMin = Vector2.zero; ng.anchorMax = Vector2.one; ng.offsetMin = new Vector2(6f, 6f); ng.offsetMax = new Vector2(-6f, -6f);
        Img(ng, sp["pill"], new Color(0.02f, 0.02f, 0.06f, 0.55f), Image.Type.Sliced);
        var ngS = Child(ng, "Shadow"); Stretch(ngS, new Vector2(2f, -2f));
        var ngSt = Text(ngS, font, 30, TextAlignmentOptions.Center, new Color(0f, 0f, 0f, 0.8f)); ngSt.fontStyle = FontStyles.Bold; ngSt.text = "ゴールド不足";
        var ngT = Child(ng, "Text"); Stretch(ngT, Vector2.zero);
        var ngTt = Text(ngT, font, 30, TextAlignmentOptions.Center, new Color(1f, 0.45f, 0.45f, 1f)); ngTt.fontStyle = FontStyles.Bold; ngTt.text = "ゴールド不足";
        ngT.SetAsLastSibling();
        ng.gameObject.SetActive(false);

        // ③ 効果の種類の札（ドリンクの絵の下端に重ねる）。下のスキル行に近すぎたため、絵と札を上へ。文字は大きく・影付き
        var drinkIcon = card.Find("DrinkIcon") as RectTransform;
        if (drinkIcon != null) drinkIcon.anchoredPosition = new Vector2(-130f, 57f);
        var badge = Child(card, "FX_EffectBadge");
        badge.anchorMin = badge.anchorMax = new Vector2(0.5f, 0.5f); badge.pivot = new Vector2(0.5f, 0.5f);
        badge.anchoredPosition = new Vector2(-130f, 4f); badge.sizeDelta = new Vector2(150f, 34f);
        Img(badge, sp["pill"], new Color(0.62f, 0.35f, 1f, 1f), Image.Type.Sliced);
        var brim = Child(badge, "Rim"); brim.anchorMin = Vector2.zero; brim.anchorMax = Vector2.one; brim.offsetMin = new Vector2(-5f, -5f); brim.offsetMax = new Vector2(5f, 5f);
        Img(brim, sp["frame"], new Color(1f, 1f, 1f, 0.55f), Image.Type.Sliced);
        var bs = Child(badge, "Shadow"); Stretch(bs, new Vector2(1.5f, -1.5f));
        var bst = Text(bs, font, 21, TextAlignmentOptions.Center, new Color(0.12f, 0f, 0.25f, 0.9f)); bst.fontStyle = FontStyles.Bold; bst.text = "ランダム +2";
        var bt = Child(badge, "Text"); Stretch(bt, Vector2.zero);
        var btt = Text(bt, font, 21, TextAlignmentOptions.Center, Color.white); btt.fontStyle = FontStyles.Bold; btt.text = "ランダム +2";
        brim.SetSiblingIndex(0); bs.SetSiblingIndex(1); bt.SetAsLastSibling();
        badge.gameObject.SetActive(false);

        // ドリンク名：大きく・太く・明るく＋影＋下にネオンの線
        var nameT = card.Find("NamePriceRow/DrinkNameText")?.GetComponent<TextMeshProUGUI>();
        if (nameT != null)
        {
            var nr = nameT.rectTransform;
            nr.sizeDelta = new Vector2(250f, nr.sizeDelta.y);
            nameT.fontSize = 24; nameT.fontStyle = FontStyles.Bold; nameT.color = new Color(1f, 0.97f, 0.88f, 1f);
            nameT.enableAutoSizing = true; nameT.fontSizeMin = 17; nameT.fontSizeMax = 24;
            var ns = Child(nameT.transform.parent, "FX_NameShadow");
            ns.anchorMin = nr.anchorMin; ns.anchorMax = nr.anchorMax; ns.pivot = nr.pivot;
            ns.anchoredPosition = nr.anchoredPosition + new Vector2(2f, -2f); ns.sizeDelta = nr.sizeDelta;
            var nst = Text(ns, font, 24, nameT.alignment, new Color(0f, 0.02f, 0.08f, 0.85f)); nst.fontStyle = FontStyles.Bold;
            nst.enableAutoSizing = false; nst.text = nameT.text;
            // 名前の奥（再実行時に影が名前より後ろの番号だと、同じ番号に入れると名前の手前に来てしまうため、後ろにある時だけ動かす）
            if (ns.GetSiblingIndex() > nameT.transform.GetSiblingIndex()) ns.SetSiblingIndex(nameT.transform.GetSiblingIndex());
            var nl = Child(nameT.transform.parent, "FX_NameLine");
            nl.anchorMin = nl.anchorMax = new Vector2(0f, 0f); nl.pivot = new Vector2(0f, 0.5f);
            nl.anchoredPosition = new Vector2(0f, -2f); nl.sizeDelta = new Vector2(240f, 2f); // 行（NamePriceRow）を4px上げた分を戻す（線の画面上の位置は従来どおり）
            Img(nl, null, Color.white);
            var ng2 = nl.GetComponent<UIHorizontalGradient>(); if (ng2 == null) ng2 = nl.gameObject.AddComponent<UIHorizontalGradient>();
            ng2.colorLeft = new Color(0.45f, 0.95f, 1f, 0.9f); ng2.colorRight = new Color(1f, 0.4f, 1f, 0f); ng2.shimmer = false;
        }

        // ⑤ 選択中の光る枠＋四隅のカギ括弧：カードの枠線（背景画像CardBgの中に描かれている線）にぴったり重ねる。
        //   DrinkMenu.png（500×500）の枠線は端から 左27・右28・上28・下31px。CardBgはこれを縦横別の倍率で引き伸ばして表示している。
        //   光る枠の画像は、線が自分の枠の端から約10内側にあるため、その分だけ外へ広げる
        // ★カードの中に置くと隣のカードの背景画像の下に隠れ、一覧の表示範囲でも切れるため、ShopPanelの直下（一覧より手前）に1つだけ置く
        var oldSel = card.Find("FX_SelectFrame");
        if (oldSel != null) Object.DestroyImmediate(oldSel.gameObject);
        // ★04_DrinkではShopUIのshopPanelが空で、実行時に名前で探してつなぐ作りのため、ここでも名前で探す
        var shopPanelGo = o.FindProperty("shopPanel").objectReferenceValue as GameObject;
        Transform panelT = shopPanelGo != null ? shopPanelGo.transform : FindDeep(shop.transform.root, "ShopPanel");
        if (panelT == null) { Debug.LogError("[DrinkScreenFXSetupTool] ShopPanelが見つかりません"); return false; }
        var misplaced = card.Find("FX_SelectOverlay"); // 以前の版でカードの見本の中にできてしまったもの
        if (misplaced != null && panelT.Find("FX_SelectOverlay") == null) misplaced.SetParent(panelT, false);
        else if (misplaced != null) Object.DestroyImmediate(misplaced.gameObject);
        var sel = Child(panelT, "FX_SelectOverlay");
        IgnoreLayout(sel);
        var cardBg = card.Find("CardBg") as RectTransform;
        Vector2 bgSize = cardBg != null ? cardBg.sizeDelta : new Vector2(500f, 390f);
        Vector2 bgPos = cardBg != null ? cardBg.anchoredPosition : Vector2.zero;
        const float imgW = 500f, imgH = 500f, lineL = 27f, lineR = 28f, lineT = 28f, lineB = 31f, glowInset = 10f;
        float sx = bgSize.x / imgW, sy = bgSize.y / imgH;
        float left = -bgSize.x * 0.5f + lineL * sx, right = bgSize.x * 0.5f - lineR * sx;
        float top = bgSize.y * 0.5f - lineT * sy, bottom = -bgSize.y * 0.5f + lineB * sy;
        sel.anchorMin = sel.anchorMax = new Vector2(0.5f, 0.5f); sel.pivot = new Vector2(0.5f, 0.5f);
        sel.anchoredPosition = Vector2.zero;
        Vector2 selOffset = bgPos + new Vector2((left + right) * 0.5f, (top + bottom) * 0.5f);
        sel.sizeDelta = new Vector2(right - left + glowInset * 2f, top - bottom + glowInset * 2f);
        var glow = Child(sel, "Glow"); Stretch(glow, Vector2.zero);
        Img(glow, sp["frame"], new Color(0.55f, 0.95f, 1f, 1f), Image.Type.Sliced);
        // 四隅のカギ括弧（太め）：腕の中心が枠線に乗る位置へ
        string[] cn = { "CornerTL", "CornerTR", "CornerBR", "CornerBL" };
        Vector2[] anc = { new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector2(0, 0) };
        float[] rot = { 0f, -90f, 180f, 90f };
        float inset = glowInset - 4.5f; // 腕の太さ9の半分
        Vector2[] pos = { new Vector2(inset, -inset), new Vector2(-inset, -inset), new Vector2(-inset, inset), new Vector2(inset, inset) };
        var thick = EnsureThickCorner();
        for (int i = 0; i < 4; i++)
        {
            var c = Child(sel, cn[i]);
            c.anchorMin = c.anchorMax = anc[i]; c.pivot = new Vector2(0f, 1f);
            c.anchoredPosition = pos[i]; c.sizeDelta = new Vector2(40f, 40f);
            c.localRotation = Quaternion.Euler(0f, 0f, rot[i]);
            Img(c, thick != null ? thick : sp["corner"], Color.white);
        }
        sel.gameObject.SetActive(false);
        badge.SetAsLastSibling();
        var purchased = card.Find("PurchasedOverlay"); if (purchased != null) purchased.SetAsLastSibling(); // 購入済みのハンコは一番手前のまま

        // ④ 購入枠（上の行の「0/3」の右。並び順は実行時に「0/3」の後ろへ回す）
        RectTransform slots = null; var slotIcons = new List<Image>();
        if (iconCont != null)
        {
            slots = Child(iconCont, "FX_PurchaseSlots");
            var le = slots.GetComponent<LayoutElement>(); if (le == null) le = slots.gameObject.AddComponent<LayoutElement>();
            le.minWidth = le.preferredWidth = 3 * 60f + 2 * 8f + 16f; le.minHeight = le.preferredHeight = 60f;
            for (int i = 0; i < 3; i++)
            {
                var s = Child(slots, "Slot" + i);
                s.anchorMin = s.anchorMax = new Vector2(0f, 0.5f); s.pivot = new Vector2(0f, 0.5f);
                s.anchoredPosition = new Vector2(16f + i * 68f, 0f); s.sizeDelta = new Vector2(60f, 60f);
                Img(s, sp["pill"], new Color(0.04f, 0.06f, 0.13f, 0.85f), Image.Type.Sliced);
                var rim = Child(s, "Rim"); rim.anchorMin = Vector2.zero; rim.anchorMax = Vector2.one; rim.offsetMin = new Vector2(-6f, -6f); rim.offsetMax = new Vector2(6f, 6f);
                Img(rim, sp["frame"], new Color(0.85f, 0.5f, 1f, 0.6f), Image.Type.Sliced);
                var ic = Child(s, "Icon"); ic.anchorMin = Vector2.zero; ic.anchorMax = Vector2.one; ic.offsetMin = new Vector2(6f, 6f); ic.offsetMax = new Vector2(-6f, -6f);
                var im = Img(ic, null, new Color(1f, 1f, 1f, 0f)); im.preserveAspect = true;
                slotIcons.Add(im);
            }
        }
        else Debug.LogWarning("[DrinkScreenFXSetupTool] drinkIconContainerが未設定のため、④は作りませんでした");

        // ⑥ 左のスキル欄
        GemSkillPreviewHUD preview = null;
        foreach (var root in scene.GetRootGameObjects()) { preview = root.GetComponentInChildren<GemSkillPreviewHUD>(true); if (preview != null) break; }

        var fx = shop.GetComponent<DrinkScreenFX>();
        if (fx == null) fx = shop.gameObject.AddComponent<DrinkScreenFX>();
        var so = new SerializedObject(fx);
        so.FindProperty("shop").objectReferenceValue = shop;
        so.FindProperty("previewHUD").objectReferenceValue = preview;
        so.FindProperty("purchaseSlots").objectReferenceValue = slots;
        var sp2 = so.FindProperty("slotIcons"); sp2.arraySize = slotIcons.Count;
        for (int i = 0; i < slotIcons.Count; i++) sp2.GetArrayElementAtIndex(i).objectReferenceValue = slotIcons[i];
        so.FindProperty("dotSprite").objectReferenceValue = sp["glow"];
        so.FindProperty("selectOverlay").objectReferenceValue = sel;
        so.FindProperty("selectFrameOffset").vector2Value = selOffset;
        so.FindProperty("selectedScale").floatValue = 1f;   // ★拡大すると右端・下の段のカードが表示範囲から切れるため
        so.FindProperty("dotSize").floatValue = 34f;
        so.FindProperty("dotActiveScale").floatValue = 1.35f;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(fx);
        EditorUtility.SetDirty(tplUI.gameObject);
        return true;
    }

    /// <summary>ドリンク画面専用の太いカギ括弧（ジェム画面のGem_Cornerは細いまま残すため別画像）</summary>
    /// <summary>ジェム画面のメニュー（GemScreenFXSetupTool）からも使う</summary>
    internal static Sprite EnsureThickCorner()
    {
        const string dir = "Assets/Art/UI/DrinkFX";
        string path = dir + "/Drink_CornerThick.png";
        if (!System.IO.File.Exists(path))
        {
            if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
            int s = 48; var t = new Texture2D(s, s, TextureFormat.RGBA32, false); t.SetPixels32(new Color32[s * s]);
            const float thick = 9f, len = 40f;
            for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
            {
                float fy = s - 1 - y; // 上からの距離
                // 腕（上の横棒・左の縦棒）までの距離
                float dTop = (x <= len) ? Mathf.Max(0f, fy - thick) : Mathf.Sqrt(Mathf.Pow(x - len, 2) + Mathf.Pow(Mathf.Max(0f, fy - thick), 2));
                float dLeft = (fy <= len) ? Mathf.Max(0f, x - thick) : Mathf.Sqrt(Mathf.Pow(fy - len, 2) + Mathf.Pow(Mathf.Max(0f, x - thick), 2));
                float d = Mathf.Min(dTop, dLeft);
                float a = d <= 0f ? 1f : Mathf.Clamp01(1f - d / 4f) * 0.4f; // 外側に淡い光
                if (a > 0f) t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            t.Apply();
            System.IO.File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single;
            imp.mipmapEnabled = false; imp.alphaIsTransparency = true; imp.textureCompression = TextureImporterCompression.Uncompressed;
            var st = new TextureImporterSettings(); imp.ReadTextureSettings(st); st.spriteMeshType = SpriteMeshType.FullRect; imp.SetTextureSettings(st);
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Transform FindDeep(Transform r, string n)
    {
        if (r.name == n) return r;
        for (int i = 0; i < r.childCount; i++) { var x = FindDeep(r.GetChild(i), n); if (x != null) return x; }
        return null;
    }

    private static int AddLocalization()
    {
        var table = Resources.Load<Game.Localization.LocalizationTable>("GameData/LocalizationTable");
        if (table == null) return 0;
        var so = new SerializedObject(table);
        var entries = so.FindProperty("entries");
        var existing = new HashSet<string>();
        for (int i = 0; i < entries.arraySize; i++) existing.Add(entries.GetArrayElementAtIndex(i).FindPropertyRelative("key").stringValue);
        int added = 0;
        void Add(string key, string ja, string en)
        {
            if (existing.Contains(key)) return;
            int idx = entries.arraySize;
            entries.InsertArrayElementAtIndex(idx);
            var el = entries.GetArrayElementAtIndex(idx);
            el.FindPropertyRelative("key").stringValue = key;
            el.FindPropertyRelative("japanese").stringValue = ja;
            el.FindPropertyRelative("english").stringValue = en;
            added++;
        }
        Set("drink.badge.fixed", "固定 +{0}", "Fixed +{0}"); // ★「確定」→「固定」（ユーザー指示）
        Add("drink.badge.random", "ランダム +{0}", "Random +{0}");
        Add("drink.badge.randomRange", "ランダム +{0}〜{1}", "Random +{0}-{1}");
        Add("drink.noGold", "ゴールド不足", "Not enough Gold");
        // ドリンクの説明文：感想の文を外し、効果の概要だけにする（ユーザー指示）。データとLocalizationTableの両方を書き換える
        foreach (var (asset, ja, en) in DrinkTexts)
        {
            Set($"drink.{asset}.desc", ja, en);
            var def = AssetDatabase.LoadAssetAtPath<Game.Shop.DrinkDefinition>($"Assets/Resources/GameData/Drinks/{asset}.asset");
            if (def != null && def.description != ja) { Undo.RecordObject(def, "Drink Text"); def.description = ja; EditorUtility.SetDirty(def); }
        }
        if (added > 0) { so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(table); }
        AssetDatabase.SaveAssets();
        return added;

        // 既にあっても上書きする
        void Set(string key, string ja, string en)
        {
            for (int i = 0; i < entries.arraySize; i++)
            {
                var el = entries.GetArrayElementAtIndex(i);
                if (el.FindPropertyRelative("key").stringValue != key) continue;
                if (el.FindPropertyRelative("japanese").stringValue != ja || el.FindPropertyRelative("english").stringValue != en)
                {
                    el.FindPropertyRelative("japanese").stringValue = ja;
                    el.FindPropertyRelative("english").stringValue = en;
                    added++;
                }
                return;
            }
            Add(key, ja, en);
        }
    }

    /// <summary>ドリンクの説明文（対象スキルの効果の概要）。効果の量は札（固定 +2／ランダム +2）で表示する</summary>
    private static readonly (string asset, string ja, string en)[] DrinkTexts =
    {
        ("Drink_ 1",  "白線のゲージ最大値・回復速度・持続時間のどれかがランダムに上がる", "Randomly boosts White Line max gauge, recovery, or duration"),
        ("Drink_ 2",  "赤線のゲージ最大値・回復速度・持続時間のどれかがランダムに上がる", "Randomly boosts Red Line max gauge, recovery, or duration"),
        ("Drink_ 3",  "同時に引ける線の数が増える", "Increases the number of lines you can draw at once"),
        ("Drink_ 4",  "反射弾が敵に当たるたびにダメージが上がる", "Reflected bullets deal more damage with each enemy hit"),
        ("Drink_ 5",  "ブロックへのダメージが上がる", "Increases damage to blocks"),
        ("Drink_ 6",  "スローモーションの持続時間と回復速度が上がる", "Increases Slow Motion duration and recovery"),
        ("Drink_ 7",  "線の硬度が上がり、弾に破られにくくなる", "Raises line hardness so bullets break through less"),
        ("Drink_ 8",  "フロア硬度・ライフ・自動回復のどれかがランダムに上がる", "Randomly boosts floor hardness, life, or auto-recovery"),
        ("Drink_ 9",  "敵の移動速度低下・敵シールドの回復遅延のどれかがランダムに上がる", "Randomly boosts enemy slowdown or enemy shield recovery delay"),
        ("Drink_ 10", "ジャスト反射のダメージが上がる", "Increases Just Reflect damage"),
        ("Drink_ 11", "敵シールドへのダメージ・シールド破壊後のダメージのどれかがランダムに上がる", "Randomly boosts damage to enemy shields or damage after breaking one"),
        ("Drink_ 12", "ジャスト反射・サークルの猶予時間のどれかがランダムに上がる", "Randomly extends the Just Reflect or Circle timing window"),
    };

    // ===== 小物 =====
    private static RectTransform Child(Transform parent, string name)
    {
        var t = parent.Find(name) as RectTransform;
        if (t != null) return t;
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }
    private static void Stretch(RectTransform rt, Vector2 offset)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = offset; rt.offsetMax = offset;
    }
    private static void IgnoreLayout(RectTransform rt)
    {
        var le = rt.GetComponent<LayoutElement>();
        if (le == null) le = rt.gameObject.AddComponent<LayoutElement>();
        le.ignoreLayout = true;
    }
    private static Image Img(RectTransform rt, Sprite s, Color c, Image.Type type = Image.Type.Simple)
    {
        var im = rt.GetComponent<Image>();
        if (im == null) im = rt.gameObject.AddComponent<Image>();
        im.sprite = s; im.color = c; im.type = type; im.raycastTarget = false; im.preserveAspect = false;
        return im;
    }
    private static TextMeshProUGUI Text(RectTransform rt, TMP_FontAsset font, float size, TextAlignmentOptions align, Color c)
    {
        var t = rt.GetComponent<TextMeshProUGUI>();
        if (t == null) t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) { t.font = font; t.fontSharedMaterial = font.material; }
        t.fontSize = size; t.alignment = align; t.color = c; t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        return t;
    }
}
