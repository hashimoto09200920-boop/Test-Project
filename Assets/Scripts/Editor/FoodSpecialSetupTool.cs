using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// フード画面（04_Drink）に「本日のおすすめ」と購入枠5つを組み込む（2026/11/14）。
/// ① カードの見本（ShopUI.drinkCardTemplate）の子に、金色の枠 FX_SpecialFrame（カードの枠線にぴったり重ねる）
/// ② 下のページ送りの行（NavRow）の真ん中の上の方に、おすすめを知らせる文字 FX_SpecialLabel（ページの点と重ならない位置）
/// ③ 上の行の購入枠（FX_PurchaseSlots の Slot0〜）と、数のアイコン（DrinkIcon_0〜）を、ShopUI の上限（5）の数にそろえる
/// 何度実行しても安全（①②は作り直し、③は足りない分だけ足す）
/// </summary>
public static class FoodSpecialSetupTool
{
    private const string ScenePath = "Assets/Scenes/04_Drink.unity";
    private const string FontPath = "Assets/Fonts/NotoSansJP-Regular SDF.asset";
    private const string FramePng = "Assets/Art/UI/GemFX/Gem_SelectFrame.png";
    private const string PillPng = "Assets/Art/UI/GemFX/Gem_Pill.png";
    private const string RaysPng = "Assets/Art/UI/DrinkFX/Food_SpecialRays.png";
    private const string ShinePng = "Assets/Art/UI/DrinkFX/Food_SpecialShine.png";

    /// <summary>画像が無ければ作って読み込む（Sprite・はみ出し無し・圧縮なし）</summary>
    private static Sprite EnsureSprite(string path, System.Func<Texture2D> make)
    {
        if (!System.IO.File.Exists(path))
        {
            var dir = System.IO.Path.GetDirectoryName(path);
            if (!System.IO.Directory.Exists(dir)) System.IO.Directory.CreateDirectory(dir);
            var tex = make();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>後光：中心から外へ伸びる光の筋（12本）。外側ほど薄く</summary>
    private static Texture2D MakeRaysTex()
    {
        int s = 256; var t = new Texture2D(s, s, TextureFormat.RGBA32, false);
        float c = (s - 1) * 0.5f;
        for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
        {
            float dx = x - c, dy = y - c;
            float r = Mathf.Sqrt(dx * dx + dy * dy) / c;
            float ang = Mathf.Atan2(dy, dx);
            float ray = Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 6f)), 6f); // 12本の筋
            float fall = Mathf.Clamp01(1f - r);
            float core = Mathf.Clamp01(1f - r * 2.2f) * 0.6f;
            float a = Mathf.Clamp01(ray * fall * fall * 1.2f + core);
            t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        t.Apply(); return t;
    }

    /// <summary>光の走り：縦長の帯（真ん中が明るく、左右へぼける）</summary>
    private static Texture2D MakeShineTex()
    {
        int w = 64, h = 16; var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            float k = 1f - Mathf.Abs((x - (w - 1) * 0.5f) / ((w - 1) * 0.5f));
            t.SetPixel(x, y, new Color(1f, 1f, 1f, k * k));
        }
        t.Apply(); return t;
    }

    [MenuItem("Tools/フード画面/本日のおすすめと購入枠5つを組み込む（04_Drink）")]
    private static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var setup = EditorSceneManager.GetSceneManagerSetup();
        var log = new System.Text.StringBuilder();
        try
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            ShopUI shop = null;
            foreach (var r in scene.GetRootGameObjects()) { shop = r.GetComponentInChildren<ShopUI>(true); if (shop != null) break; }
            if (shop == null) { Fail("04_Drink に ShopUI が見つかりません"); return; }
            var so = new SerializedObject(shop);
            int limit = so.FindProperty("drinkLimit5").intValue;

            // ---- ① 金色の枠（カードの見本の子）----
            var template = so.FindProperty("drinkCardTemplate").objectReferenceValue as Component;
            if (template == null) { Fail("ShopUI の Drink Card Template が未設定です"); return; }
            var card = template.transform;
            var old = card.Find("FX_SpecialFrame"); if (old != null) Object.DestroyImmediate(old.gameObject);
            var frameRt = NewUI("FX_SpecialFrame", card);
            IgnoreLayout(frameRt);
            // カードの枠線（CardBg の画像 DrinkMenu.png 500×500 の、端から 左27・右28・上28・下31px の線）に重ねる（選択中の枠と同じ計算）
            var cardBg = card.Find("CardBg") as RectTransform;
            Vector2 bgSize = cardBg != null ? cardBg.sizeDelta : new Vector2(500f, 390f);
            Vector2 bgPos = cardBg != null ? cardBg.anchoredPosition : Vector2.zero;
            const float imgW = 500f, imgH = 500f, lineL = 27f, lineR = 28f, lineT = 28f, lineB = 31f, inset = 6f;
            float sx = bgSize.x / imgW, sy = bgSize.y / imgH;
            float left = -bgSize.x * 0.5f + lineL * sx, right = bgSize.x * 0.5f - lineR * sx;
            float top = bgSize.y * 0.5f - lineT * sy, bottom = -bgSize.y * 0.5f + lineB * sy;
            frameRt.anchorMin = frameRt.anchorMax = frameRt.pivot = new Vector2(0.5f, 0.5f);
            frameRt.anchoredPosition = bgPos + new Vector2((left + right) * 0.5f, (top + bottom) * 0.5f);
            frameRt.sizeDelta = new Vector2(right - left + inset * 2f, top - bottom + inset * 2f);
            // 購入済みのハンコより後ろ・中身より手前（CardBg のすぐ後ろだと中身に隠れるので、最後から2番目に置く）
            var purchased = card.Find("PurchasedOverlay");
            if (purchased != null) frameRt.SetSiblingIndex(purchased.GetSiblingIndex()); else frameRt.SetAsLastSibling();
            var frameImg = frameRt.gameObject.AddComponent<Image>();
            frameImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(FramePng);
            frameImg.type = Image.Type.Sliced;
            frameImg.pixelsPerUnitMultiplier = 0.55f; // A5：枠の線を太く（約1.8倍）
            frameImg.color = new Color(1f, 0.82f, 0.25f, 1f);
            frameImg.raycastTarget = false;

            // A2 光の走り：枠の内側だけで見えるよう、はみ出しを隠す入れ物（RectMask2D）の中で光の帯を動かす
            var maskRt = NewUI("ShineMask", frameRt);
            maskRt.anchorMin = Vector2.zero; maskRt.anchorMax = Vector2.one;
            maskRt.offsetMin = new Vector2(inset + 4f, inset + 4f); maskRt.offsetMax = new Vector2(-inset - 4f, -inset - 4f);
            maskRt.gameObject.AddComponent<RectMask2D>();
            var shineRt = NewUI("Shine", maskRt);
            shineRt.anchorMin = shineRt.anchorMax = shineRt.pivot = new Vector2(0.5f, 0.5f);
            shineRt.sizeDelta = new Vector2(70f, (top - bottom) * 1.6f);
            shineRt.localRotation = Quaternion.Euler(0f, 0f, -25f);
            var shineImg = shineRt.gameObject.AddComponent<Image>();
            shineImg.sprite = EnsureSprite(ShinePng, MakeShineTex);
            shineImg.color = new Color(1f, 0.97f, 0.8f, 0.55f);
            shineImg.raycastTarget = false;

            // A1 後光：フードの絵（DrinkIcon）の後ろに置き、金色の光の筋を回す。おすすめのカードだけ出す（FoodSpecialFrame が出し入れ）
            var oldRays = card.Find("FX_SpecialRays"); if (oldRays != null) Object.DestroyImmediate(oldRays.gameObject);
            RectTransform raysRt = null;
            var foodIcon = card.Find("DrinkIcon") as RectTransform;
            if (foodIcon != null)
            {
                raysRt = NewUI("FX_SpecialRays", card);
                IgnoreLayout(raysRt);
                raysRt.anchorMin = foodIcon.anchorMin; raysRt.anchorMax = foodIcon.anchorMax; raysRt.pivot = new Vector2(0.5f, 0.5f);
                // 絵の真ん中に合わせる（絵の基準点が真ん中でなくても合うように）
                Vector2 iconCenter = foodIcon.anchoredPosition + new Vector2((0.5f - foodIcon.pivot.x) * foodIcon.sizeDelta.x, (0.5f - foodIcon.pivot.y) * foodIcon.sizeDelta.y);
                raysRt.anchoredPosition = iconCenter;
                float rs = Mathf.Max(foodIcon.sizeDelta.x, foodIcon.sizeDelta.y) * 1.7f;
                raysRt.sizeDelta = new Vector2(rs, rs);
                raysRt.SetSiblingIndex(foodIcon.GetSiblingIndex()); // 絵のすぐ後ろ
                var raysImg = raysRt.gameObject.AddComponent<Image>();
                raysImg.sprite = EnsureSprite(RaysPng, MakeRaysTex);
                raysImg.color = new Color(1f, 0.8f, 0.3f, 0.85f);
                raysImg.raycastTarget = false;
                raysRt.gameObject.SetActive(false);
                log.AppendLine($"・フードの絵の後ろに後光 FX_SpecialRays を作りました（大きさ {rs:F0}）");
            }
            else log.AppendLine("・※カードの見本に DrinkIcon が見つからないので、後光は作りませんでした");

            var pulse = frameRt.gameObject.AddComponent<FoodSpecialFrame>();
            pulse.EditorWire(frameImg, raysRt, shineRt);
            frameRt.gameObject.SetActive(false);
            log.AppendLine($"・カードの見本に金色の枠 FX_SpecialFrame（太め・光の走り付き）を作りました（大きさ {frameRt.sizeDelta}）{(frameImg.sprite == null ? "　※枠の画像が見つかりません" : "")}");

            // ---- ② おすすめの文字（下のページ送りの行）----
            Transform nav = FindDeep(shop.transform.root, "NavRow");
            if (nav == null) foreach (var r in scene.GetRootGameObjects()) { nav = FindDeep(r.transform, "NavRow"); if (nav != null) break; }
            if (nav == null) log.AppendLine("・※NavRow が見つからないので、おすすめの文字は作りませんでした");
            else
            {
                var oldL = nav.Find("FX_SpecialLabel"); if (oldL != null) Object.DestroyImmediate(oldL.gameObject);
                // 行（幅1320・高さ78）のうち、左右の矢印（幅200ずつ）の間の幅680に入れる。
                // B1 下地：幅660・高さ42を行の上の方（上端から2〜44）に置く。ページの点（大きさ28）はその下（行の真ん中→下寄り、44〜74）へずらす
                var plate = NewUI("FX_SpecialLabel", nav);
                IgnoreLayout(plate);
                plate.anchorMin = plate.anchorMax = new Vector2(0.5f, 1f); plate.pivot = new Vector2(0.5f, 1f);
                plate.anchoredPosition = new Vector2(0f, -2f); plate.sizeDelta = new Vector2(660f, 42f);
                var plateImg = plate.gameObject.AddComponent<Image>();
                plateImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PillPng);
                plateImg.type = Image.Type.Sliced;
                plateImg.color = new Color(0.08f, 0.05f, 0.02f, 0.92f);
                plateImg.raycastTarget = true; // B6：押せるようにする
                var plateOutline = plate.gameObject.AddComponent<Outline>();
                plateOutline.effectColor = new Color(1f, 0.8f, 0.3f, 0.95f); plateOutline.effectDistance = new Vector2(2f, -2f);
                var btn = plate.gameObject.AddComponent<Button>();
                btn.targetGraphic = plateImg;
                var cols = btn.colors; cols.highlightedColor = new Color(1.15f, 1.1f, 1f, 1f); cols.pressedColor = new Color(0.8f, 0.75f, 0.65f, 1f); btn.colors = cols;

                // B3 フードの小さな絵（左）と B2 文字（大きめ・黒いふちどり・金色）
                var iconRt = NewUI("Icon", plate);
                iconRt.anchorMin = iconRt.anchorMax = new Vector2(0f, 0.5f); iconRt.pivot = new Vector2(0f, 0.5f);
                iconRt.anchoredPosition = new Vector2(14f, 0f); iconRt.sizeDelta = new Vector2(34f, 34f);
                var iconImg = iconRt.gameObject.AddComponent<Image>(); iconImg.preserveAspect = true; iconImg.raycastTarget = false;
                var tr = NewUI("Text", plate);
                tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
                tr.offsetMin = new Vector2(14f + 34f + 8f, 2f); tr.offsetMax = new Vector2(-14f, -2f); // 文字の幅 660−14−34−8−14＝590
                var t = tr.gameObject.AddComponent<TextMeshProUGUI>();
                var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
                if (font != null) { t.font = font; t.fontSharedMaterial = font.material; }
                t.text = "本日のおすすめ：グラビティ・パンケーキ";
                t.alignment = TextAlignmentOptions.Center;
                t.enableAutoSizing = true; t.fontSizeMin = 18f; t.fontSizeMax = 28f; t.fontSize = 28f;
                t.color = new Color(1f, 0.86f, 0.35f, 1f);
                t.raycastTarget = false;
                t.textWrappingMode = TextWrappingModes.NoWrap;
                var sw = tr.gameObject.AddComponent<LanguageFontSwitch>();
                sw.kind = LanguageFontSwitch.Kind.Heading;
                // 黒いふちどりは、フォントを切り替えた後の描き方の設定で付ける（ShopUI.OnEnable で付ける。先に付けると文字が崩れるため）
                so.FindProperty("specialLabel").objectReferenceValue = t;
                so.FindProperty("specialLabelIcon").objectReferenceValue = iconImg;
                so.FindProperty("specialLabelButton").objectReferenceValue = btn;

                // ページの点を、下地の下へずらす。★行（NavRow）の並べ方には手を付けない（矢印の位置が変わらないように）。
                //   点の入れ物（DotsContainer・高さは行と同じ78）の中で、点を下寄せにする（点28 → 行の上端から46〜74）
                var dots = nav.Find("DotsContainer");
                var dotsHlg = dots != null ? dots.GetComponent<HorizontalLayoutGroup>() : null;
                if (dotsHlg != null)
                {
                    dotsHlg.childAlignment = TextAnchor.LowerCenter;
                    dotsHlg.padding = new RectOffset(dotsHlg.padding.left, dotsHlg.padding.right, dotsHlg.padding.top, 4);
                    EditorUtility.SetDirty(dotsHlg);
                    log.AppendLine("・ページの点を、おすすめの文字の下へ（点の入れ物の中で下寄せ）にしました");
                }
                else log.AppendLine("・※DotsContainer の HorizontalLayoutGroup が見つからないので、ページの点の位置は変えていません");
                log.AppendLine("・下のページ送りの行に、おすすめの文字（下地・フードの絵・押すとおすすめのページへ）を作りました（幅660・高さ42）");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(shop);

            // ---- ③ 購入枠と数のアイコンを上限の数に ----
            var fx = shop.GetComponent<DrinkScreenFX>();
            Transform slots = null;
            foreach (var r in scene.GetRootGameObjects()) { slots = FindDeep(r.transform, "FX_PurchaseSlots"); if (slots != null) break; }
            if (slots != null && fx != null)
            {
                var slot0 = slots.Find("Slot0");
                var icons = new List<Image>();
                for (int i = 0; i < limit; i++)
                {
                    var s = slots.Find("Slot" + i);
                    if (s == null && slot0 != null)
                    {
                        s = Object.Instantiate(slot0.gameObject, slots).transform;
                        s.name = "Slot" + i;
                        log.AppendLine($"・購入枠 Slot{i} を足しました");
                    }
                    if (s == null) continue;
                    var srt = (RectTransform)s;
                    srt.anchoredPosition = new Vector2(16f + i * 68f, srt.anchoredPosition.y); // 既存の並び（16 + i×68）と同じ
                    var ic = s.Find("Icon");
                    if (ic != null) icons.Add(ic.GetComponent<Image>());
                }
                var le = slots.GetComponent<LayoutElement>();
                if (le != null) { le.minWidth = le.preferredWidth = limit * 60f + (limit - 1) * 8f + 16f; EditorUtility.SetDirty(le); }
                var fso = new SerializedObject(fx);
                var arr = fso.FindProperty("slotIcons");
                arr.arraySize = icons.Count;
                for (int i = 0; i < icons.Count; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = icons[i];
                fso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(fx);
                log.AppendLine($"・購入枠を {icons.Count} 個にしました（上限 {limit}）");
            }
            else log.AppendLine("・※FX_PurchaseSlots または DrinkScreenFX が見つからないので、購入枠は変えていません");

            // 数のアイコン（DrinkIcon_0〜）も上限の数に
            Transform icon0 = null;
            foreach (var r in scene.GetRootGameObjects()) { icon0 = FindDeep(r.transform, "DrinkIcon_0"); if (icon0 != null) break; }
            if (icon0 != null)
            {
                var parent = icon0.parent;
                for (int i = 1; i < limit; i++)
                {
                    if (parent.Find("DrinkIcon_" + i) != null) continue;
                    var go = Object.Instantiate(icon0.gameObject, parent);
                    go.name = "DrinkIcon_" + i;
                    var slotsT = parent.Find("FX_PurchaseSlots");
                    if (slotsT != null) go.transform.SetSiblingIndex(slotsT.GetSiblingIndex());
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (setup != null && setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
        Debug.Log("[FoodSpecialSetupTool]\n" + log);
        EditorUtility.DisplayDialog("フード画面", log.ToString(), "OK");
    }

    private static void Fail(string msg)
    {
        Debug.LogError("[FoodSpecialSetupTool] " + msg);
        EditorUtility.DisplayDialog("フード画面", msg, "OK");
    }

    private static RectTransform NewUI(string name, Transform parent)
    {
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

    private static Transform FindDeep(Transform root, string name)
    {
        if (root == null) return null;
        if (root.name == name) return root;
        foreach (Transform c in root)
        {
            var f = FindDeep(c, name);
            if (f != null) return f;
        }
        return null;
    }
}
