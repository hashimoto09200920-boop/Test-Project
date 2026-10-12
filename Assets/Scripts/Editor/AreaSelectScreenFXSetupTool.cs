using System.Collections.Generic;
using Game.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 03_AreaSelectのアップデートを組み込む。
/// 1回目（見やすさ）：①右上の所持数に暗い下地 ②スタミナ0の時の強調（「あと mm:ss」を下地の左へ移す）
/// ④次に挑むAreaの「NEXT」 ⑤ジェムボタンの「！」（点滅＋拡大縮小）。③ロック中Areaの解放条件はユーザー判断で不要となり削除。
/// 何度実行しても安全（既にある部品は作り直さず、設定だけ合わせる）。
/// ★「Build Constellation」等のAreaConstellationFXのメニューは実行しない（ノードの子に追加した部品だけを扱う）。
/// </summary>
public static class AreaSelectScreenFXSetupTool
{
    private const string ScenePath = "Assets/Scenes/03_AreaSelect.unity";
    private const string FontPath = "Assets/Fonts/NotoSansJP-Regular SDF.asset";
    private static readonly Color PlateColor = new Color(0.04f, 0.06f, 0.13f, 0.72f);

    [MenuItem("Tools/エリア選択画面/見やすさ改善（1回目）を組み込む（03_AreaSelect）")]
    private static void Run1()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var pill = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/GemFX/Gem_Pill.png");
        var orb = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/SoundFX/Sound_Orb.png");
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (pill == null || orb == null || font == null) { Debug.LogError("[AreaSelectScreenFXSetupTool] Gem_Pill.png / Sound_Orb.png / フォントのどれかが見つかりません"); return; }

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool wasLoaded = scene.IsValid() && scene.isLoaded;
        if (!wasLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        AreaConstellationFX constellation = null;
        foreach (var root in scene.GetRootGameObjects()) { constellation = root.GetComponentInChildren<AreaConstellationFX>(true); if (constellation != null) break; }
        if (constellation == null) Debug.LogError("[AreaSelectScreenFXSetupTool] 03_AreaSelectにAreaConstellationFXが見つかりません");
        else if (Build(constellation, pill, orb, font))
        {
            int removed = RemoveLocalization();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[AreaSelectScreenFXSetupTool] 03_AreaSelectに見やすさ改善（1回目）を組み込んで保存しました（不要になった文言{removed}件を削除）");
        }
        if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
    }

    private static bool Build(AreaConstellationFX constellation, Sprite pill, Sprite orb, TMP_FontAsset font)
    {
        var panel = constellation.transform.parent; // AreaPanel
        var guide = panel.GetComponent<AreaSelectGuideFX>();
        if (guide == null) guide = panel.gameObject.AddComponent<AreaSelectGuideFX>();
        var so = new SerializedObject(guide);

        // ① 所持数（スタミナ・ゴールド・∞ストーン）の暗い下地：アイコン〜数字の後ろに敷く
        Image staminaPlate = null;
        foreach (var name in new[] { "StaminaHUD", "GoldHUD", "InfiniteStoneHUD" })
        {
            var hud = panel.Find(name);
            if (hud == null) { Debug.LogWarning($"[AreaSelectScreenFXSetupTool] {name}が見つかりません"); continue; }
            var plate = Child(hud, "FX_Plate");
            plate.SetSiblingIndex(0);
            plate.anchorMin = plate.anchorMax = new Vector2(0f, 0.5f); plate.pivot = new Vector2(0f, 0.5f);
            plate.anchoredPosition = new Vector2(12f, 0f); plate.sizeDelta = new Vector2(200f, 54f);
            var img = Img(plate, pill, PlateColor, Image.Type.Sliced);
            if (name == "StaminaHUD") staminaPlate = img;
        }

        // ② スタミナ：「あと mm:ss」は元の位置だと画面の右端からはみ出すため、下地の左隣へ移し、専用の下地を敷く
        var stamina = panel.Find("StaminaHUD");
        TextMeshProUGUI countText = null, countdownText = null; Image countdownPlate = null;
        if (stamina != null)
        {
            countText = stamina.Find("StaminaCountText")?.GetComponent<TextMeshProUGUI>();
            countdownText = stamina.Find("StaminaCountdownText")?.GetComponent<TextMeshProUGUI>();
            if (countdownText != null)
            {
                var cp = Child(stamina, "FX_CountdownPlate");
                cp.SetSiblingIndex(countdownText.transform.GetSiblingIndex());
                cp.anchorMin = cp.anchorMax = new Vector2(0f, 0.5f); cp.pivot = new Vector2(1f, 0.5f);
                cp.anchoredPosition = new Vector2(4f, 0f); cp.sizeDelta = new Vector2(150f, 38f);
                countdownPlate = Img(cp, pill, PlateColor, Image.Type.Sliced);

                var ct = countdownText.rectTransform;
                ct.anchorMin = ct.anchorMax = new Vector2(0f, 0.5f); ct.pivot = new Vector2(1f, 0.5f);
                ct.anchoredPosition = new Vector2(4f - 14f, 0f); ct.sizeDelta = new Vector2(240f, 30f);
                countdownText.horizontalAlignment = HorizontalAlignmentOptions.Right;
                countdownText.textWrappingMode = TextWrappingModes.NoWrap;
                EditorUtility.SetDirty(countdownText);
            }
        }
        so.FindProperty("staminaPlate").objectReferenceValue = staminaPlate;
        so.FindProperty("staminaCountText").objectReferenceValue = countText;
        so.FindProperty("staminaCountdownText").objectReferenceValue = countdownText;
        so.FindProperty("countdownPlate").objectReferenceValue = countdownPlate;

        // ④ 各Areaのノード（③解放条件はユーザー判断で不要：以前の版で作った部品が残っていれば削除）
        var sp = EnsureSprites();
        var glow = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/SoundFX/Sound_Glow.png");
        var rim = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/SoundFX/Sound_TrackRim.png");
        var cso = new SerializedObject(constellation);
        var nodesProp = cso.FindProperty("nodes");
        var list = new List<(StageButton stage, GameObject next)>();
        for (int i = 0; i < nodesProp.arraySize; i++)
        {
            var np = nodesProp.GetArrayElementAtIndex(i);
            var btn = np.FindPropertyRelative("button").objectReferenceValue as RectTransform;
            if (btn == null) continue;
            var stage = btn.GetComponent<StageButton>();
            if (stage == null) { Debug.LogWarning($"[AreaSelectScreenFXSetupTool] {btn.name}にStageButtonがありません"); continue; }
            Color color = np.FindPropertyRelative("color").colorValue; color.a = 1f;
            Vector2 rankOffset = np.FindPropertyRelative("rankBadgeOffset").vector2Value;

            var oldHint = btn.Find("FX_UnlockHint");
            if (oldHint != null) Object.DestroyImmediate(oldHint.gameObject);

            list.Add((stage, BuildNextTag(btn, color, rankOffset, pill, glow, rim, sp["triangle"], font)));
        }
        var arr = so.FindProperty("nodes");
        arr.arraySize = list.Count;
        for (int i = 0; i < list.Count; i++)
        {
            var e = arr.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("stage").objectReferenceValue = list[i].stage;
            e.FindPropertyRelative("nextTag").objectReferenceValue = list[i].next;
        }

        // ⑤ ジェムボタンの「！」：ボタン画像（160×160）の右上の角
        var gemBtn = panel.Find("GemManagementButton");
        if (gemBtn != null)
        {
            var badge = Child(gemBtn, "FX_AlertBadge");
            badge.SetAsLastSibling();
            badge.anchorMin = badge.anchorMax = badge.pivot = new Vector2(0.5f, 0.5f);
            badge.anchoredPosition = new Vector2(60f, 60f); badge.sizeDelta = new Vector2(56f, 56f);
            RemoveImage(badge); // 旧版は本体に画像があった
            var cg = badge.GetComponent<CanvasGroup>(); if (cg == null) cg = badge.gameObject.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false; cg.interactable = false;

            // 後ろの赤い光 → 赤い丸 → 白い縁 → 「！」
            var g = Child(badge, "Glow"); Center(g, new Vector2(120f, 120f)); g.SetSiblingIndex(0);
            var glowImg = Img(g, glow, new Color(1f, 0.15f, 0.2f, 0.6f));
            var body = Child(badge, "Body"); Stretch(body); body.SetSiblingIndex(1);
            Img(body, sp["circle"], new Color(0.92f, 0.12f, 0.2f, 1f));
            var ring = Child(badge, "Ring"); Stretch(ring); ring.SetSiblingIndex(2);
            Img(ring, sp["ring"], Color.white);
            var mt = Child(badge, "Mark"); Stretch(mt); mt.SetSiblingIndex(3);
            mt.offsetMin = new Vector2(0f, 2f); mt.offsetMax = new Vector2(0f, 2f); // 「！」は文字の形の都合で少し下に見えるため持ち上げる
            var mark = Text(mt, font, 38f, Color.white, FontStyles.Bold);
            mark.text = "!";
            badge.gameObject.SetActive(false);
            so.FindProperty("gemAlertBadge").objectReferenceValue = badge;
            so.FindProperty("gemAlertGroup").objectReferenceValue = cg;
            so.FindProperty("gemAlertGlow").objectReferenceValue = glowImg;
        }
        else Debug.LogWarning("[AreaSelectScreenFXSetupTool] GemManagementButtonが見つからないため、⑤は作りませんでした");

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(guide);
        return true;
    }

    /// <summary>
    /// ④ NEXTタグ：暗い下地 → Areaの色のネオンの縁 → 白い「NEXT」、ノードを指す小さな三角。点滅・拡大縮小はAreaSelectGuideFXが行う
    /// （後ろのAreaの色の光はユーザー判断で不要となり削除）。
    /// 置き場所はランクバッジの位置（未クリアのAreaにはランクが無いので空いている）
    /// </summary>
    private static GameObject BuildNextTag(RectTransform btn, Color color, Vector2 offset, Sprite pill, Sprite glow, Sprite rim, Sprite triangle, TMP_FontAsset font)
    {
        var next = Child(btn, "FX_NextTag");
        next.SetAsLastSibling();
        next.anchorMin = next.anchorMax = next.pivot = new Vector2(0.5f, 0.5f);
        next.anchoredPosition = offset; next.sizeDelta = new Vector2(118f, 42f);
        RemoveImage(next); // 旧版は本体に画像があった
        var cg = next.GetComponent<CanvasGroup>(); if (cg == null) cg = next.gameObject.AddComponent<CanvasGroup>();
        cg.blocksRaycasts = false; cg.interactable = false;
        Color bright = Color.Lerp(color, Color.white, 0.35f);

        var oldGlow = next.Find("Glow");
        if (oldGlow != null) Object.DestroyImmediate(oldGlow.gameObject);
        var plate = Child(next, "Plate"); Stretch(plate); plate.SetSiblingIndex(0);
        Img(plate, pill, new Color(0.03f, 0.04f, 0.1f, 0.92f), Image.Type.Sliced);
        var r = Child(next, "Rim"); Stretch(r); r.offsetMin = new Vector2(-3f, -3f); r.offsetMax = new Vector2(3f, 3f); r.SetSiblingIndex(1);
        Img(r, rim, bright, Image.Type.Sliced);

        // ノードを指す三角：タグの縁の、ノードに向いた側
        Vector2 dir = offset.sqrMagnitude > 0.01f ? -offset.normalized : Vector2.down;
        float edge = Mathf.Min(59f / Mathf.Max(0.001f, Mathf.Abs(dir.x)), 21f / Mathf.Max(0.001f, Mathf.Abs(dir.y)));
        var a = Child(next, "Arrow"); a.anchorMin = a.anchorMax = a.pivot = new Vector2(0.5f, 0.5f); a.SetSiblingIndex(2);
        a.sizeDelta = new Vector2(18f, 14f); a.anchoredPosition = dir * (edge + 9f);
        a.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f);
        Img(a, triangle, bright);

        var t = Child(next, "Text"); Stretch(t); t.SetSiblingIndex(3);
        var text = Text(t, font, 24f, Color.white, FontStyles.Bold);
        text.characterSpacing = 6f;
        text.text = "NEXT";
        next.gameObject.SetActive(false);
        return next.gameObject;
    }

    private static void RemoveImage(RectTransform rt)
    {
        var im = rt.GetComponent<Image>();
        if (im != null) Object.DestroyImmediate(im);
    }

    /// <summary>③（解放条件）の文言はユーザー判断で不要になったため、以前の版で追加したものを消す</summary>
    private static int RemoveLocalization()
    {
        var table = Resources.Load<Game.Localization.LocalizationTable>("GameData/LocalizationTable");
        if (table == null) return 0;
        var so = new SerializedObject(table);
        var entries = so.FindProperty("entries");
        int removed = 0;
        for (int i = entries.arraySize - 1; i >= 0; i--)
        {
            string key = entries.GetArrayElementAtIndex(i).FindPropertyRelative("key").stringValue;
            if (key == "area.unlock.clearArea" || key == "area.unlock.clearStage" || key == "area.unlock.allRankA")
            { entries.DeleteArrayElementAtIndex(i); removed++; }
        }
        if (removed > 0) { so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(table); AssetDatabase.SaveAssets(); }
        return removed;
    }

    // ===== 画像（無い時だけ作る） =====
    private const string ArtDir = "Assets/Art/UI/AreaSelectFX";

    private static Dictionary<string, Sprite> EnsureSprites()
    {
        if (!System.IO.Directory.Exists(ArtDir)) System.IO.Directory.CreateDirectory(ArtDir);
        return new Dictionary<string, Sprite>
        {
            ["circle"] = Ensure("Area_Circle", () => Disc(128, 58f, 0f)),
            ["ring"] = Ensure("Area_Ring", () => Disc(128, 62f, 53f)),
            ["triangle"] = Ensure("Area_Triangle", Triangle),
        };
    }

    private static Sprite Ensure(string name, System.Func<Texture2D> make)
    {
        string path = $"{ArtDir}/{name}.png";
        if (!System.IO.File.Exists(path))
        {
            var tex = make();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single;
            imp.mipmapEnabled = false; imp.alphaIsTransparency = true; imp.textureCompression = TextureImporterCompression.Uncompressed;
            var st = new TextureImporterSettings(); imp.ReadTextureSettings(st); st.spriteMeshType = SpriteMeshType.FullRect; imp.SetTextureSettings(st);
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>丸（inner&gt;0なら輪）。白で描き、Imageの色で着色する</summary>
    private static Texture2D Disc(int s, float outer, float inner)
    {
        var t = new Texture2D(s, s, TextureFormat.RGBA32, false); t.SetPixels32(new Color32[s * s]);
        float c = (s - 1) * 0.5f;
        for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
        {
            float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
            float a = Mathf.Clamp01(outer - r + 0.5f);
            if (inner > 0f) a *= Mathf.Clamp01(r - inner + 0.5f);
            if (a > 0f) t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        t.Apply(); return t;
    }

    /// <summary>上向きの三角（角を少し丸める）</summary>
    private static Texture2D Triangle()
    {
        int w = 64, h = 48; var t = new Texture2D(w, h, TextureFormat.RGBA32, false); t.SetPixels32(new Color32[w * h]);
        Vector2 a = new Vector2(6f, 4f), b = new Vector2(58f, 4f), c = new Vector2(32f, 44f);
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
            float d = Mathf.Max(EdgeDist(p, a, b), Mathf.Max(EdgeDist(p, b, c), EdgeDist(p, c, a)));
            float al = Mathf.Clamp01(0.5f - d);
            if (al > 0f) t.SetPixel(x, y, new Color(1f, 1f, 1f, al));
        }
        t.Apply(); return t;
    }

    // 三角形（反時計回り）の辺からの距離（内側がマイナス）
    private static float EdgeDist(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 e = b - a; Vector2 n = new Vector2(e.y, -e.x).normalized;
        return Vector2.Dot(p - a, n);
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
    private static void Center(RectTransform rt, Vector2 size) { rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = size; }
    private static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; rt.pivot = new Vector2(0.5f, 0.5f); }
    private static Image Img(RectTransform rt, Sprite s, Color c, Image.Type type = Image.Type.Simple)
    {
        var im = rt.GetComponent<Image>();
        if (im == null) im = rt.gameObject.AddComponent<Image>();
        im.sprite = s; im.color = c; im.type = type; im.raycastTarget = false; im.preserveAspect = false;
        EditorUtility.SetDirty(im);
        return im;
    }
    private static TextMeshProUGUI Text(RectTransform rt, TMP_FontAsset font, float size, Color color, FontStyles style)
    {
        var t = rt.GetComponent<TextMeshProUGUI>();
        if (t == null) t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = font; t.fontSharedMaterial = font.material;
        t.fontSize = size; t.color = color; t.fontStyle = style;
        t.alignment = TextAlignmentOptions.Center;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.raycastTarget = false;
        EditorUtility.SetDirty(t);
        return t;
    }
}
