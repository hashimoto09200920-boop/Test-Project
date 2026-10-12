using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// ジェム画面（02_Gem）の見やすさ改善（1回目）をPlay前に組み込む。
/// GemItemTemplate（カードの見本）とスロット表示に、GemScreenFX用の部品を「追加」するだけで、既存の部品は削除・作り直ししない
/// （元のジェムアイコン・「×1」表示は非表示にするだけ）。何度実行しても安全。
/// カード・行・名前行はレイアウトグループで並んでいるため、追加する部品にはLayoutElement(ignoreLayout)を付ける
/// </summary>
public static class GemScreenFXSetupTool
{
    private const string ScenePath = "Assets/Scenes/02_Gem.unity";
    private const string ArtDir = "Assets/Art/UI/GemFX";
    private const string FontPath = "Assets/Fonts/NotoSansJP-Regular SDF.asset";

    [MenuItem("Tools/ジェム画面/見やすさ改善（1回目）を組み込む（02_Gem）")]
    private static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool wasLoaded = scene.IsValid() && scene.isLoaded;
        if (!wasLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        GemManagementUI ui = null;
        foreach (var root in scene.GetRootGameObjects()) { ui = root.GetComponentInChildren<GemManagementUI>(true); if (ui != null) break; }
        if (ui == null) Debug.LogError("[GemScreenFXSetupTool] 02_GemにGemManagementUIが見つかりません");
        else if (Build(ui))
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[GemScreenFXSetupTool] 02_Gemのジェム画面に見やすさ改善（1回目）を組み込んで保存しました（文言{AddLocalization()}件追加）");
        }
        if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
    }

    // =====================================================================
    // 2回目：演出（⑧〜⑫）
    // =====================================================================

    [MenuItem("Tools/ジェム画面/演出（2回目）を組み込む（02_Gem）")]
    private static void Run2()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool wasLoaded = scene.IsValid() && scene.isLoaded;
        if (!wasLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        GemManagementUI ui = null;
        foreach (var root in scene.GetRootGameObjects()) { ui = root.GetComponentInChildren<GemManagementUI>(true); if (ui != null) break; }
        if (ui == null) Debug.LogError("[GemScreenFXSetupTool] 02_GemにGemManagementUIが見つかりません");
        else if (ui.GetComponent<GemScreenFX>() == null) Debug.LogError("[GemScreenFXSetupTool] 先に「見やすさ改善（1回目）を組み込む」を実行してください");
        else if (Build2(ui, scene))
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[GemScreenFXSetupTool] 02_Gemのジェム画面に演出（2回目）を組み込んで保存しました");
        }
        if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
    }

    private static bool Build2(GemManagementUI ui, Scene scene)
    {
        var o = new SerializedObject(ui);
        var template = o.FindProperty("gemItemTemplate").objectReferenceValue as GameObject;
        var panel = o.FindProperty("gemPanel").objectReferenceValue as GameObject;
        var dim = o.FindProperty("dimPanel").objectReferenceValue as GameObject;
        if (template == null || panel == null) { Debug.LogError("[GemScreenFXSetupTool] gemItemTemplate / gemPanel が未設定です"); return false; }
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        var sp = EnsureSprites();

        // ⑧ カードの光（カード全体を白く光らせる板。普段は非表示）
        var flash = Child(template.transform, "FX_Flash");
        IgnoreLayout(flash);
        flash.anchorMin = Vector2.zero; flash.anchorMax = Vector2.one; flash.offsetMin = new Vector2(-4f, -4f); flash.offsetMax = new Vector2(4f, 4f);
        var flashImg = Img(flash, sp["pill"], new Color(1f, 0.95f, 0.8f, 0f), Image.Type.Sliced);
        flashImg.enabled = false;
        var tagT = template.transform.Find("FX_EquippedTag");
        flash.SetSiblingIndex(tagT != null ? tagT.GetSiblingIndex() : template.transform.childCount - 1); // 装備中の札より奥

        // 最前面の入れ物：粒・輪・「+〇G」
        var front = Child(panel.transform, "FX_GemFront");
        IgnoreLayout(front);
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
        var rings = new List<Image>();
        for (int i = 0; i < 4; i++)
        {
            var r = Child(front, "Ring" + i); r.anchorMin = r.anchorMax = r.pivot = new Vector2(0.5f, 0.5f); r.sizeDelta = new Vector2(60, 60);
            var im = Img(r, sp["ring"], Color.white); im.enabled = false; rings.Add(im);
        }
        var pops = new List<TextMeshProUGUI>();
        for (int i = 0; i < 3; i++)
        {
            var t = Child(front, "Pop" + i); t.anchorMin = t.anchorMax = t.pivot = new Vector2(0.5f, 0.5f); t.sizeDelta = new Vector2(240, 50);
            var tx = Text(t, font, 32, TextAlignmentOptions.Center, Color.white); tx.fontStyle = FontStyles.Bold; tx.enabled = false; pops.Add(tx);
        }

        // ⑨ 上部の大きなジェムアイコン（「💎 7/10」）：後ろの光＋きらめき
        var slotIcon = FindDeep(panel.transform, "SlotIcon") as RectTransform;
        Image iconGlow = null; var twinkles = new List<Image>();
        if (slotIcon != null)
        {
            var g = Child(slotIcon, "FX_IconGlow"); g.anchorMin = g.anchorMax = g.pivot = new Vector2(0.5f, 0.5f); g.sizeDelta = new Vector2(150f, 150f);
            g.SetAsFirstSibling();
            iconGlow = Img(g, sp["glow"], new Color(0.7f, 0.9f, 1f, 0.35f));
            for (int i = 0; i < 3; i++)
            {
                var tw = Child(slotIcon, "FX_Twinkle" + i); tw.anchorMin = tw.anchorMax = tw.pivot = new Vector2(0.5f, 0.5f); tw.sizeDelta = new Vector2(30f, 30f);
                var im = Img(tw, sp["star"], Color.white); im.enabled = false; twinkles.Add(im);
            }
        }
        else Debug.LogWarning("[GemScreenFXSetupTool] SlotIconが見つからないため、⑨は作りませんでした");

        // ⑪ 背景（GemDimPanel＝背景画像の上、ジェム画面より奥）
        RectTransform bgRoot = null; var stars = new List<Image>(); var streaks = new List<Image>();
        if (dim != null)
        {
            bgRoot = Child(dim.transform, "FX_BgAnim");
            bgRoot.anchorMin = Vector2.zero; bgRoot.anchorMax = Vector2.one; bgRoot.pivot = new Vector2(0.5f, 0.5f); bgRoot.offsetMin = bgRoot.offsetMax = Vector2.zero;
            for (int i = 0; i < 16; i++)
            {
                var st = Child(bgRoot, "Star" + i); st.anchorMin = st.anchorMax = st.pivot = new Vector2(0.5f, 0.5f);
                float sz = 10f + (i % 4) * 5f; st.sizeDelta = new Vector2(sz, sz);
                stars.Add(Img(st, sp["star"], i % 2 == 0 ? new Color(0.5f, 0.95f, 1f, 0f) : new Color(0.85f, 0.55f, 1f, 0f)));
            }
            for (int i = 0; i < 4; i++)
            {
                var sk = Child(bgRoot, "Streak" + i); sk.anchorMin = sk.anchorMax = sk.pivot = new Vector2(0.5f, 0.5f); sk.sizeDelta = new Vector2(150f, 5f);
                var im = Img(sk, sp["bar"], i % 2 == 0 ? new Color(0.5f, 0.95f, 1f, 0f) : new Color(0.85f, 0.55f, 1f, 0f)); im.enabled = false; streaks.Add(im);
            }
        }
        else Debug.LogWarning("[GemScreenFXSetupTool] dimPanel（背景）が未設定のため、⑪は作りませんでした");

        // ⑫ コインの画像：シーン内の所持ゴールド表示のアイコンと同じもの
        Sprite coin = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            var hud = root.GetComponentInChildren<GoldHUD>(true);
            if (hud == null) continue;
            var icon = hud.transform.Find("GoldIcon")?.GetComponent<Image>();
            if (icon != null) coin = icon.sprite;
            break;
        }

        var anim = ui.GetComponent<GemScreenFXAnim>();
        if (anim == null) anim = ui.gameObject.AddComponent<GemScreenFXAnim>();
        var so = new SerializedObject(anim);
        so.FindProperty("gemUI").objectReferenceValue = ui;
        so.FindProperty("screenFX").objectReferenceValue = ui.GetComponent<GemScreenFX>();
        so.FindProperty("frontRoot").objectReferenceValue = front;
        SetArr(so, "particles", parts); SetArr(so, "rings", rings); SetArr(so, "popTexts", pops);
        so.FindProperty("coinSprite").objectReferenceValue = coin != null ? coin : sp["glow"];
        so.FindProperty("glowSprite").objectReferenceValue = sp["glow"];
        so.FindProperty("slotIcon").objectReferenceValue = slotIcon;
        so.FindProperty("slotIconGlow").objectReferenceValue = iconGlow;
        SetArr(so, "slotIconTwinkles", twinkles);
        so.FindProperty("bgRoot").objectReferenceValue = bgRoot;
        SetArr(so, "bgStars", stars); SetArr(so, "bgStreaks", streaks);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(anim);
        EditorUtility.SetDirty(template);
        return true;
    }

    private static void SetArr<T>(SerializedObject so, string name, List<T> items) where T : Object
    {
        var p = so.FindProperty(name);
        p.arraySize = items.Count;
        for (int i = 0; i < items.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }

    private static bool Build(GemManagementUI ui)
    {
        var o = new SerializedObject(ui);
        var template = o.FindProperty("gemItemTemplate").objectReferenceValue as GameObject;
        var slotText = o.FindProperty("slotLevelText").objectReferenceValue as TextMeshProUGUI;
        var preview = o.FindProperty("gemSkillPreviewHUD").objectReferenceValue as GemSkillPreviewHUD;
        if (template == null) { Debug.LogError("[GemScreenFXSetupTool] gemItemTemplateが未設定です"); return false; }
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        var sp = EnsureSprites();

        // ⑤ 選択中：暗く染める明滅 → 少し明るくなる明滅に（既存の点滅処理はそのまま、色だけ変更）
        o.FindProperty("selectedHighlightColor").colorValue = Color.white;
        o.FindProperty("selectedPulseColor").colorValue = new Color(0.82f, 0.93f, 1f, 1f);
        o.ApplyModifiedPropertiesWithoutUndo();

        BuildCard(template.transform, sp, font);

        // ④⑥ スロットのゲージ・所持数（「7/10」の文字の下）
        var cells = new List<Image>();
        List<Image> glowImages = new List<Image>();
        Image badgeRim = null;
        RectTransform inventoryBadge = null;
        TextMeshProUGUI invText = null;
        if (slotText != null)
        {
            var gauge = Child(slotText.transform, "FX_SlotGauge");
            IgnoreLayout(gauge);
            gauge.anchorMin = gauge.anchorMax = new Vector2(0f, 0f); gauge.pivot = new Vector2(0f, 1f);
            gauge.anchoredPosition = new Vector2(4f, 2f); gauge.sizeDelta = new Vector2(300f, 24f);
            // 溝（暗い板＋ネオンの枠）。マスより先に描く
            var track = Child(gauge, "Track");
            track.anchorMin = Vector2.zero; track.anchorMax = Vector2.one; track.pivot = new Vector2(0.5f, 0.5f);
            track.offsetMin = new Vector2(-6f, -5f); track.offsetMax = new Vector2(6f, 5f);
            Img(track, sp["pill"], new Color(0.04f, 0.06f, 0.12f, 0.85f), Image.Type.Sliced);
            var rim = Child(track, "Rim");
            rim.anchorMin = Vector2.zero; rim.anchorMax = Vector2.one; rim.offsetMin = new Vector2(-6f, -6f); rim.offsetMax = new Vector2(6f, 6f);
            Img(rim, sp["frame"], new Color(1f, 0.62f, 0.2f, 0.55f), Image.Type.Sliced);
            track.SetAsFirstSibling();
            // 光のにじみ（マスの後ろ）
            var glowRoot = Child(gauge, "Glows");
            glowRoot.anchorMin = Vector2.zero; glowRoot.anchorMax = Vector2.one; glowRoot.offsetMin = glowRoot.offsetMax = Vector2.zero;
            glowRoot.SetSiblingIndex(1);
            var glows = new List<Image>();
            for (int i = 0; i < 10; i++)
            {
                var gw = Child(glowRoot, "Glow" + i);
                gw.anchorMin = gw.anchorMax = new Vector2(0f, 0.5f); gw.pivot = new Vector2(0.5f, 0.5f);
                gw.anchoredPosition = new Vector2(i * 29f + 12f, 0f); gw.sizeDelta = new Vector2(46f, 40f);
                glows.Add(Img(gw, sp["glow"], new Color(1f, 0.6f, 0.2f, 0f)));
            }
            for (int i = 0; i < 10; i++)
            {
                var c = Child(gauge, "Cell" + i);
                c.anchorMin = c.anchorMax = new Vector2(0f, 0.5f); c.pivot = new Vector2(0f, 0.5f);
                c.anchoredPosition = new Vector2(i * 29f, 0f); c.sizeDelta = new Vector2(26f, 18f);
                c.SetAsLastSibling();
                cells.Add(Img(c, sp["para"], new Color(0.18f, 0.22f, 0.32f, 0.85f)));
            }
            glowImages = glows;

            // ⑥ 所持数：スロットの行から離し、ジェム一覧（ScrollView）の右上の外側へ（スロット数と紛らわしいため）
            var scrollView = slotText.GetComponentInParent<Canvas>() != null ? FindDeep(ui.transform.root, "GemManagementPanel")?.Find("ScrollView") : null;
            // 以前の版で作った文字（スロットの行の下、またはScrollView直下）を探して、バッジの中へ移す
            Transform oldInv = slotText.transform.Find("FX_InventoryText");
            if (oldInv == null && scrollView != null) oldInv = scrollView.Find("FX_InventoryText");
            RectTransform inv;
            if (scrollView != null)
            {
                // ★所持数のバッジ（暗い板＋ネオンの枠＋ジェムのアイコン＋文字）。背景の光の線の上でも読めるように
                var badge = Child(scrollView, "FX_InventoryBadge");
                IgnoreLayout(badge);
                badge.anchorMin = badge.anchorMax = new Vector2(1f, 1f); badge.pivot = new Vector2(1f, 0f);
                // ★ユーザー指示：枠とジェムのアイコンは不要（上のボタンと下のカードの間が狭いため）。暗い板だけ残し、高さも抑える
                badge.anchoredPosition = new Vector2(-4f, 4f); badge.sizeDelta = new Vector2(160f, 30f);
                Img(badge, sp["pill"], new Color(0.04f, 0.06f, 0.13f, 0.85f), Image.Type.Sliced);
                foreach (var old in new[] { "Rim", "Icon" })
                {
                    var t = badge.Find(old);
                    if (t != null) Object.DestroyImmediate(t.gameObject);
                }

                if (oldInv != null) { oldInv.SetParent(badge, false); inv = (RectTransform)oldInv; }
                else inv = Child(badge, "FX_InventoryText");
                IgnoreLayout(inv);
                inv.anchorMin = Vector2.zero; inv.anchorMax = Vector2.one; inv.pivot = new Vector2(0.5f, 0.5f);
                inv.offsetMin = new Vector2(12f, 0f); inv.offsetMax = new Vector2(-12f, 0f);
                invText = Text(inv, font, 22, TextAlignmentOptions.Center, Color.white);
                invText.richText = true;
                inventoryBadge = badge;
            }
            else
            {
                Debug.LogWarning("[GemScreenFXSetupTool] GemManagementPanel/ScrollViewが見つからないため、所持数はスロットの行の右に離して置きます");
                inv = oldInv != null ? (RectTransform)oldInv : Child(slotText.transform, "FX_InventoryText");
                IgnoreLayout(inv);
                inv.anchorMin = inv.anchorMax = new Vector2(0f, 0f); inv.pivot = new Vector2(0f, 0.5f);
                inv.anchoredPosition = new Vector2(420f, -5f); inv.sizeDelta = new Vector2(220f, 26f);
                invText = Text(inv, font, 19, TextAlignmentOptions.MidlineLeft, new Color(0.72f, 0.85f, 1f, 1f));
            }
            invText.text = "所持 0/32";
        }
        else Debug.LogWarning("[GemScreenFXSetupTool] slotLevelTextが未設定のため、スロットのゲージ・所持数は作りませんでした");

        var fx = ui.GetComponent<GemScreenFX>();
        if (fx == null) fx = ui.gameObject.AddComponent<GemScreenFX>();
        var so = new SerializedObject(fx);
        so.FindProperty("gemUI").objectReferenceValue = ui;
        so.FindProperty("previewHUD").objectReferenceValue = preview;
        var cp = so.FindProperty("slotCells"); cp.arraySize = cells.Count;
        for (int i = 0; i < cells.Count; i++) cp.GetArrayElementAtIndex(i).objectReferenceValue = cells[i];
        so.FindProperty("inventoryText").objectReferenceValue = invText;
        so.FindProperty("inventoryBadgeRim").objectReferenceValue = badgeRim;
        so.FindProperty("inventoryBadge").objectReferenceValue = inventoryBadge;
        var gp = so.FindProperty("slotGlows"); gp.arraySize = glowImages.Count;
        for (int i = 0; i < glowImages.Count; i++) gp.GetArrayElementAtIndex(i).objectReferenceValue = glowImages[i];
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(fx);
        EditorUtility.SetDirty(template);
        return true;
    }

    /// <summary>カードの見本に①②③⑤の部品を足す</summary>
    private static void BuildCard(Transform card, Dictionary<string, Sprite> sp, TMP_FontAsset font)
    {
        // ⑤ 選択中の光る枠＋四隅のカギ括弧（カードの外周に少しはみ出す）
        var sel = Child(card, "FX_SelectFrame");
        IgnoreLayout(sel);
        sel.anchorMin = Vector2.zero; sel.anchorMax = Vector2.one; sel.pivot = new Vector2(0.5f, 0.5f);
        sel.offsetMin = new Vector2(-6f, -6f); sel.offsetMax = new Vector2(6f, 6f);
        var glow = Child(sel, "Glow");
        glow.anchorMin = Vector2.zero; glow.anchorMax = Vector2.one; glow.offsetMin = glow.offsetMax = Vector2.zero;
        Img(glow, sp["frame"], new Color(0.55f, 0.95f, 1f, 1f), Image.Type.Sliced);
        string[] cn = { "CornerTL", "CornerTR", "CornerBR", "CornerBL" };
        Vector2[] anc = { new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector2(0, 0) };
        float[] rot = { 0f, -90f, 180f, 90f };
        for (int i = 0; i < 4; i++)
        {
            var c = Child(sel, cn[i]);
            c.anchorMin = c.anchorMax = anc[i]; c.pivot = new Vector2(0f, 1f);
            c.anchoredPosition = Vector2.zero; c.sizeDelta = new Vector2(30f, 30f);
            c.localRotation = Quaternion.Euler(0f, 0f, rot[i]);
            Img(c, sp["corner"], Color.white);
        }
        sel.gameObject.SetActive(false);

        // ③ 装備中の札（カード右下の内側。枠線にまたがると次の行のカード・スクロール範囲で下半分が隠れるため）
        var tag = Child(card, "FX_EquippedTag");
        IgnoreLayout(tag);
        tag.anchorMin = tag.anchorMax = new Vector2(1f, 0f); tag.pivot = new Vector2(1f, 0f);
        tag.anchoredPosition = new Vector2(-12f, 10f); tag.sizeDelta = new Vector2(98f, 28f);
        tag.SetAsLastSibling();
        // ★見やすさ改善：濃いオレンジの札＋白の太字（暗い影付き）＋チェックマーク
        Img(tag, sp["pill"], new Color(0.95f, 0.38f, 0.05f, 1f), Image.Type.Sliced);
        var chk = Child(tag, "Check");
        chk.anchorMin = chk.anchorMax = new Vector2(0f, 0.5f); chk.pivot = new Vector2(0f, 0.5f);
        chk.anchoredPosition = new Vector2(8f, 0f); chk.sizeDelta = new Vector2(16f, 16f);
        Img(chk, sp["check"], Color.white);
        // 影（同じ文字を暗い色で右下へ少しずらして重ねる。縁取り用の専用マテリアルを作らずに済む）
        var tagS = Child(tag, "Shadow");
        tagS.anchorMin = Vector2.zero; tagS.anchorMax = Vector2.one; tagS.offsetMin = new Vector2(27.5f, -1.5f); tagS.offsetMax = new Vector2(-4.5f, -1.5f);
        var ts = Text(tagS, font, 18, TextAlignmentOptions.Center, new Color(0.3f, 0.06f, 0f, 0.9f));
        ts.fontStyle = FontStyles.Bold; ts.text = "装備中";
        var tagT = Child(tag, "Text");
        tagT.anchorMin = Vector2.zero; tagT.anchorMax = Vector2.one; tagT.offsetMin = new Vector2(26f, 0f); tagT.offsetMax = new Vector2(-6f, 0f);
        tagT.SetAsLastSibling();
        var tt = Text(tagT, font, 18, TextAlignmentOptions.Center, Color.white);
        tt.fontStyle = FontStyles.Bold; tt.text = "装備中";
        tag.gameObject.SetActive(false);

        // ② 名前の行：元のジェムアイコン（残り回数バッジ付き）と「×1」を非表示にし、◆の数＋残り回数バッジに置き換える
        var nameRow = card.Find("TextContainer/NameRow");
        if (nameRow != null)
        {
            Sprite gemSprite = null;
            var gemIcon = nameRow.Find("GemIcon");
            if (gemIcon != null) { gemSprite = gemIcon.GetComponent<Image>()?.sprite; gemIcon.gameObject.SetActive(false); }
            var slotDisp = nameRow.Find("SlotDisplayText");
            if (slotDisp != null) slotDisp.gameObject.SetActive(false);

            var pill = Child(nameRow, "FX_UsesPill");
            IgnoreLayout(pill);
            pill.anchorMin = pill.anchorMax = new Vector2(1f, 0.5f); pill.pivot = new Vector2(1f, 0.5f);
            pill.anchoredPosition = new Vector2(-4f, 0f); pill.sizeDelta = new Vector2(50f, 24f);
            Img(pill, sp["pill"], new Color(0.12f, 0.45f, 0.65f, 0.95f), Image.Type.Sliced);
            var pt = Child(pill, "Text");
            pt.anchorMin = Vector2.zero; pt.anchorMax = Vector2.one; pt.offsetMin = pt.offsetMax = Vector2.zero;
            var ptt = Text(pt, font, 18, TextAlignmentOptions.Center, Color.white);
            ptt.fontStyle = FontStyles.Bold; ptt.text = "30";

            var pips = Child(nameRow, "FX_SlotPips");
            IgnoreLayout(pips);
            pips.anchorMin = pips.anchorMax = new Vector2(1f, 0.5f); pips.pivot = new Vector2(1f, 0.5f);
            pips.anchoredPosition = new Vector2(-60f, 0f); pips.sizeDelta = new Vector2(130f, 20f);
            // ★必要スロット数：上のスロットのゲージと同じ形・色のマスを必要な数だけ並べ、左に「スロット」の文字（A案）
            for (int i = 0; i < 3; i++)
            {
                var p = Child(pips, "Pip" + i);
                p.anchorMin = p.anchorMax = new Vector2(1f, 0.5f); p.pivot = new Vector2(1f, 0.5f);
                p.anchoredPosition = new Vector2(-i * 18f, 0f); p.sizeDelta = new Vector2(16f, 12f);
                Img(p, sp["para"], new Color(1f, 0.6f, 0.15f, 1f));
            }
            var lbl = Child(pips, "Label");
            lbl.anchorMin = lbl.anchorMax = new Vector2(1f, 0.5f); lbl.pivot = new Vector2(1f, 0.5f);
            lbl.anchoredPosition = new Vector2(-24f, 0f); lbl.sizeDelta = new Vector2(80f, 22f);
            // ★13・太字だと文字が潰れて塊に見えたため、16・通常の太さ・明るめの色にする
            var lt = Text(lbl, font, 16, TextAlignmentOptions.MidlineRight, new Color(1f, 0.88f, 0.68f, 1f));
            lt.fontStyle = FontStyles.Normal; lt.text = "スロット";
        }
        else Debug.LogWarning("[GemScreenFXSetupTool] TextContainer/NameRow が見つかりません");

        // ① スキル行：カテゴリ色（PopulateSkillRowが設定）を土台に、グラデーション＋左端の縦線＋上縁の光
        var icons = card.Find("TextContainer/SkillIconsContainer");
        if (icons != null)
        {
            foreach (var rn in new[] { "SkillRow_Base", "SkillRow_Bonus1", "SkillRow_Bonus2" })
            {
                var row = icons.Find(rn) as RectTransform;
                if (row == null) continue;
                var grad = row.GetComponent<UIHorizontalGradient>();
                if (grad == null) grad = row.gameObject.AddComponent<UIHorizontalGradient>();
                grad.colorLeft = Color.white;
                grad.colorRight = new Color(0.66f, 0.68f, 0.8f, 1f);
                grad.shimmer = false; // ★行数が多い（最大約36行）ため毎フレームの描き直しを避ける（OPPO等の負荷対策）

                var acc = Child(row, "FX_Accent");
                IgnoreLayout(acc);
                acc.anchorMin = new Vector2(0f, 0f); acc.anchorMax = new Vector2(0f, 1f); acc.pivot = new Vector2(0f, 0.5f);
                acc.anchoredPosition = Vector2.zero; acc.sizeDelta = new Vector2(6f, 0f);
                Img(acc, null, Color.white);

                var gl = Child(row, "FX_Gloss");
                IgnoreLayout(gl);
                gl.anchorMin = new Vector2(0f, 1f); gl.anchorMax = new Vector2(1f, 1f); gl.pivot = new Vector2(0.5f, 1f);
                gl.anchoredPosition = Vector2.zero; gl.sizeDelta = new Vector2(0f, 2f);
                Img(gl, null, new Color(1f, 1f, 1f, 0.45f));
            }
        }
        else Debug.LogWarning("[GemScreenFXSetupTool] TextContainer/SkillIconsContainer が見つかりません");
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
        Add("gem.ui.equipped", "装備中", "EQUIPPED");
        Add("gem.ui.inventory", "所持 {0}/{1}", "Owned {0}/{1}");
        Add("gem.ui.slot", "スロット", "SLOT");
        Add("gem.ui.ownedLabel", "所持", "Owned");
        if (added > 0) { so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(table); AssetDatabase.SaveAssets(); }
        return added;
    }

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

    // ===== 画像の生成（無い時だけ） =====
    private static Dictionary<string, Sprite> EnsureSprites()
    {
        if (!Directory.Exists(ArtDir)) Directory.CreateDirectory(ArtDir);
        return new Dictionary<string, Sprite>
        {
            ["pill"] = Ensure("Gem_Pill", () => RoundRect(48, 24, 11f, false, 0f), new Vector4(12, 11, 12, 11)),
            ["frame"] = Ensure("Gem_SelectFrame", () => RoundRect(96, 96, 16f, true, 8f), new Vector4(30, 30, 30, 30)),
            ["corner"] = Ensure("Gem_Corner", CornerTex, Vector4.zero),
            ["diamond"] = Ensure("Gem_Diamond", DiamondTex, Vector4.zero),
            ["para"] = Ensure("Gem_Parallelogram", ParaTex, Vector4.zero),
            ["check"] = Ensure("Gem_Check", CheckTex, Vector4.zero),
            ["glow"] = Ensure("Gem_Glow", GlowTex, Vector4.zero),
            ["ring"] = Ensure("Gem_Ring", RingTex, Vector4.zero),
            ["star"] = Ensure("Gem_Star", StarTex, Vector4.zero),
            ["bar"] = Ensure("Gem_Bar", BarTex, Vector4.zero),
        };
    }

    private static Sprite Ensure(string name, System.Func<Texture2D> make, Vector4 border)
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
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.spriteBorder = border;
            var st = new TextureImporterSettings();
            imp.ReadTextureSettings(st);
            st.spriteMeshType = SpriteMeshType.FullRect;
            imp.SetTextureSettings(st);
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Texture2D NewTex(int w, int h) { var t = new Texture2D(w, h, TextureFormat.RGBA32, false); t.SetPixels32(new Color32[w * h]); return t; }

    /// <summary>角丸（outline=true：枠線＋外側の光、false：塗り）</summary>
    private static Texture2D RoundRect(int w, int h, float r, bool outline, float glow)
    {
        var t = NewTex(w, h);
        float m = glow;
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            float px = x + 0.5f - w * 0.5f, py = y + 0.5f - h * 0.5f;
            float hx = w * 0.5f - m - r, hy = h * 0.5f - m - r;
            float qx = Mathf.Abs(px) - hx, qy = Mathf.Abs(py) - hy;
            float d = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
            float a;
            if (!outline) a = Mathf.Clamp01(0.5f - d);
            else
            {
                float line = Mathf.Clamp01(2f - Mathf.Abs(d + 2f) + 0.5f);
                float g = d > 0f ? Mathf.Clamp01(1f - d / glow) * 0.5f : Mathf.Clamp01(1f + d / 6f) * 0.25f;
                a = Mathf.Max(line, g);
            }
            if (a > 0f) t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        t.Apply(); return t;
    }

    /// <summary>左上のカギ括弧「┌」（左上がpivot）</summary>
    private static Texture2D CornerTex()
    {
        int s = 32; var t = NewTex(s, s);
        for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
        {
            float fy = s - 1 - y; // 上からの距離
            bool arm = (fy < 5f && x < 28) || (x < 5 && fy < 28);
            float a = arm ? 1f : 0f;
            float dx = Mathf.Min(x, 32f), dy = Mathf.Min(fy, 32f);
            if (!arm && (dx < 9f && fy < 30 || dy < 9f && x < 30)) a = 0.3f; // 周りの淡い光
            if (a > 0f) t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        t.Apply(); return t;
    }

    /// <summary>平行四辺形のマス（左のスキル欄のタイルと同じ形の傾き）。上側がやや明るい</summary>
    private static Texture2D ParaTex()
    {
        int w = 52, h = 36; var t = NewTex(w, h); float slant = 10f;
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            float off = slant * (y + 0.5f) / h;          // 上ほど右へずれる
            float left = off + 1f, right = w - slant + off - 1f;
            float a = Mathf.Clamp01(Mathf.Min(x + 0.5f - left, right - (x + 0.5f)) + 0.5f) * Mathf.Clamp01(Mathf.Min(y, h - 1 - y) + 0.5f);
            float bright = Mathf.Lerp(0.82f, 1f, y / (float)(h - 1));
            if (a > 0f) t.SetPixel(x, y, new Color(bright, bright, bright, a));
        }
        t.Apply(); return t;
    }

    /// <summary>チェックマーク（太めの線）</summary>
    private static Texture2D CheckTex()
    {
        int s = 48; var t = NewTex(s, s);
        Vector2 a = new Vector2(8f, 25f), b = new Vector2(19f, 12f), c = new Vector2(40f, 37f);
        for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
        {
            Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
            float d = Mathf.Min(Seg(p, a, b), Seg(p, b, c));
            float al = Mathf.Clamp01(4.5f - d + 0.5f);
            if (al > 0f) t.SetPixel(x, y, new Color(1f, 1f, 1f, al));
        }
        t.Apply(); return t;
    }

    private static float Seg(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a; float k = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * k);
    }

    private static Texture2D RingTex()
    {
        int s = 128; var t = NewTex(s, s); float c = (s - 1) * 0.5f;
        for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
        {
            float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
            float a = Mathf.Max(Mathf.Clamp01(60f - r + 0.5f) * Mathf.Clamp01(r - 53f + 0.5f), Mathf.Clamp01(1f - Mathf.Abs(r - 56f) / 9f) * 0.4f);
            if (a > 0f) t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        t.Apply(); return t;
    }

    /// <summary>4方向に光が伸びる星（きらめき）</summary>
    private static Texture2D StarTex()
    {
        int s = 64; var t = NewTex(s, s); float c = (s - 1) * 0.5f;
        for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
        {
            float dx = Mathf.Abs(x - c) / c, dy = Mathf.Abs(y - c) / c;
            float ray = Mathf.Max(Mathf.Clamp01(1f - dx) * Mathf.Clamp01(1f - dy * 9f), Mathf.Clamp01(1f - dy) * Mathf.Clamp01(1f - dx * 9f));
            float core = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 3f);
            float a = Mathf.Clamp01(Mathf.Max(ray * ray, core));
            if (a > 0f) t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        t.Apply(); return t;
    }

    private static Texture2D BarTex()
    {
        int w = 128, h = 16; var t = NewTex(w, h);
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            float ax = 1f - Mathf.Abs(x - (w - 1) * 0.5f) / (w * 0.5f);
            float ay = 1f - Mathf.Abs(y - (h - 1) * 0.5f) / (h * 0.5f);
            float a = Mathf.Clamp01(ax * 1.5f) * ay * ay;
            if (a > 0f) t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        t.Apply(); return t;
    }

    private static Texture2D GlowTex()
    {
        int s = 64; var t = NewTex(s, s); float c = (s - 1) * 0.5f;
        for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
        {
            float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
            float a = Mathf.Clamp01(1f - r); a = a * a;
            if (a > 0f) t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        t.Apply(); return t;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++) { var r = FindDeep(root.GetChild(i), name); if (r != null) return r; }
        return null;
    }

    private static Texture2D DiamondTex()
    {
        int s = 32; var t = NewTex(s, s); float c = (s - 1) * 0.5f;
        for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
        {
            float d = Mathf.Abs(x - c) / 14f + Mathf.Abs(y - c) / 11f;
            float a = Mathf.Clamp01((1f - d) * 12f);
            if (a > 0f) t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        t.Apply(); return t;
    }
}
