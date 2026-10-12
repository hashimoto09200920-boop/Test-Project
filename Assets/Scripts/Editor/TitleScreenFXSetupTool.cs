using System.Collections.Generic;
using System.IO;
using Game.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Title画面（01_Title）のアップデートを組み込む。
/// 1回目（見やすさ）：①STARTの光の縁取り ②LANGUAGE画面の演出（SOUND画面とそろえる。選択中の光る枠・✓はユーザー判断で削除） ⑨ロゴの上の余白。
/// ③バージョン表記と、2回目（④起動時の登場 ⑤STARTを押した時 ⑥なぞると線 ⑦踊るダンサー ⑧BGMに合わせた光の脈動）は、
/// ユーザー判断で不要となり削除（シーンに残った部品・変更はメニュー「不要になった③〜⑧の部品を削除」で元に戻す）。
/// 何度実行しても安全（既にある部品は作り直さず、設定だけ合わせる。⑨は一度だけ適用し、その後の手調整は上書きしない）。
/// </summary>
public static class TitleScreenFXSetupTool
{
    private const string ScenePath = "Assets/Scenes/01_Title.unity";
    private const string ArtDir = "Assets/Art/UI/TitleFX";

    // STARTボタン（500×125）の外側に出す光の幅
    private const float AuraPad = 30f;


    // ⑨ ロゴ：元の位置・大きさ（このままの時だけ適用する）と、適用後の値
    private static readonly Vector2 LogoOriginalPos = new Vector2(10f, 15f);
    private const float LogoNewScale = 0.94f;
    private const float LogoNewPosY = 0f;

    [MenuItem("Tools/タイトル画面/見やすさ改善（1回目）を組み込む（01_Title）")]
    private static void Run1()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var sp = LoadSprites();
        if (sp == null) return;

        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool wasLoaded = scene.IsValid() && scene.isLoaded;
        if (!wasLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        TitleMenu menu = null;
        foreach (var root in scene.GetRootGameObjects()) { menu = root.GetComponentInChildren<TitleMenu>(true); if (menu != null) break; }
        if (menu == null) Debug.LogError("[TitleScreenFXSetupTool] 01_TitleにTitleMenuが見つかりません");
        else
        {
            bool ok = true;
            ok &= BuildStartAura(menu, sp);
            ok &= BuildLanguageFX(menu, sp);
            FitLogo(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(ok ? "[TitleScreenFXSetupTool] 01_TitleにTitle画面の見やすさ改善（1回目）を組み込んで保存しました"
                         : "[TitleScreenFXSetupTool] 一部の部品を作れませんでした（上の警告を確認してください）。作れた分は保存しました");
        }
        if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
    }

    // ---------- ① STARTの光の縁取り ----------
    private static bool BuildStartAura(TitleMenu menu, Dictionary<string, Sprite> sp)
    {
        if (menu.startButton == null) { Debug.LogWarning("[TitleScreenFXSetupTool] TitleMenu.startButtonが未設定のため、①は作りませんでした"); return false; }
        var btn = menu.startButton.transform;
        // ボタン画像のすぐ上・文字（ButtonNeonText）より奥
        var box = Child(btn, "FX_StartAura");
        Stretch(box); box.SetSiblingIndex(0);
        var auraRt = Child(box, "Aura");
        auraRt.anchorMin = Vector2.zero; auraRt.anchorMax = Vector2.one; auraRt.pivot = new Vector2(0.5f, 0.5f);
        auraRt.offsetMin = new Vector2(-AuraPad, -AuraPad); auraRt.offsetMax = new Vector2(AuraPad, AuraPad);
        var aura = Img(auraRt, sp["aura"], new Color(1f, 0.6f, 0.22f, 0f));
        var runners = new List<Object>();
        for (int i = 0; i < 2; i++)
        {
            var r = Child(box, "Runner" + i); Center(r, new Vector2(34f, 34f));
            runners.Add(Img(r, sp["glow"], new Color(1f, 0.88f, 0.65f, 0f)));
        }

        var fx = box.GetComponent<TitleStartAura>();
        if (fx == null) fx = box.gameObject.AddComponent<TitleStartAura>();
        var so = new SerializedObject(fx);
        so.FindProperty("button").objectReferenceValue = menu.startButton;
        so.FindProperty("aura").objectReferenceValue = aura;
        SetArray(so, "runners", runners);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(fx);
        return true;
    }

    // ---------- ② LANGUAGE画面 ----------
    private static bool BuildLanguageFX(TitleMenu menu, Dictionary<string, Sprite> sp)
    {
        var panel = menu.languagePanel;
        if (panel == null || menu.languageJapaneseButton == null || menu.languageEnglishButton == null)
        { Debug.LogWarning("[TitleScreenFXSetupTool] TitleMenuのlanguagePanel/言語ボタンが未設定のため、②は作りませんでした"); return false; }

        var fx = panel.GetComponent<LanguagePanelFX>();
        if (fx == null) fx = panel.AddComponent<LanguagePanelFX>();
        var so = new SerializedObject(fx);
        var frame = panel.transform.Find("LanguageBg") as RectTransform;
        so.FindProperty("frameRect").objectReferenceValue = frame;

        // 選択中の光る枠・✓はユーザー判断で不要：以前の版で作った部品が残っていれば削除
        RemoveSelectParts(menu.languageJapaneseButton);
        RemoveSelectParts(menu.languageEnglishButton);
        so.FindProperty("japaneseButton").objectReferenceValue = menu.languageJapaneseButton.transform;
        so.FindProperty("englishButton").objectReferenceValue = menu.languageEnglishButton.transform;

        // 枠を走る光：枠（LanguageBg）のすぐ上・文字より奥
        var back = Child(panel.transform, "FX_Back");
        Stretch(back); IgnoreLayout(back);
        back.SetSiblingIndex(frame != null ? frame.GetSiblingIndex() + 1 : 0);
        var dots = new List<Object>();
        for (int i = 0; i < 7; i++)
        {
            var d = Child(back, "Runner" + i);
            float s = Mathf.Lerp(30f, 9f, i / 6f);
            Center(d, new Vector2(s, s));
            Img(d, sp["glow"], Color.Lerp(new Color(0.5f, 1f, 1f, 1f), new Color(1f, 0.45f, 1f, 0.5f), i / 6f));
            dots.Add(d);
        }
        SetArray(so, "runnerDots", dots);
        // MainBg.png（408×612）の枠線は端から 左右約40・上下約32px。LanguageBg（800×810）に引き伸ばした時の値
        so.FindProperty("runnerInset").vector2Value = new Vector2(80f, 42f);

        // 光の粒：一番手前
        var front = Child(panel.transform, "FX_Front");
        Stretch(front); IgnoreLayout(front);
        front.SetAsLastSibling();
        var sparks = new List<Object>();
        for (int i = 0; i < 12; i++) { var s = Child(front, "Spark" + i); Center(s, new Vector2(16, 16)); var im = Img(s, sp["glow"], Color.white); im.enabled = false; sparks.Add(im); }
        SetArray(so, "sparks", sparks);
        // 開く時に上から下りる光の帯はユーザー判断で不要：以前の版で作った部品が残っていれば削除
        var oldSweep = front.Find("Sweep");
        if (oldSweep != null) Object.DestroyImmediate(oldSweep.gameObject);

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(fx);
        return true;
    }

    private static void RemoveSelectParts(Button button)
    {
        foreach (var n in new[] { "FX_SelectGlow", "FX_Check" })
        {
            var t = button.transform.Find(n);
            if (t != null) Object.DestroyImmediate(t.gameObject);
        }
    }

    // ---------- ⑨ ロゴの上の余白 ----------
    private static void FitLogo(Scene scene)
    {
        TitleLogoLetters logo = null;
        foreach (var root in scene.GetRootGameObjects()) { logo = root.GetComponentInChildren<TitleLogoLetters>(true); if (logo != null) break; }
        if (logo == null) { Debug.LogWarning("[TitleScreenFXSetupTool] NeonDancerLogo（TitleLogoLetters）が見つからないため、⑨は適用しませんでした"); return; }
        var rt = (RectTransform)logo.transform;
        bool original = Mathf.Approximately(rt.localScale.x, 1f) && Vector2.Distance(rt.anchoredPosition, LogoOriginalPos) < 0.01f;
        if (!original)
        {
            Debug.Log($"[TitleScreenFXSetupTool] ⑨ロゴは既に調整済みのため変更しませんでした（Pos={rt.anchoredPosition}, Scale={rt.localScale.x}）");
            return;
        }
        // Canvasは高さ1080基準（どの画面比率でも縦は同じ）。ロゴの一番上が画面上端から約40空き、DANCERの下とSTARTの間も約40空く値
        rt.localScale = new Vector3(LogoNewScale, LogoNewScale, 1f);
        rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, LogoNewPosY);
        EditorUtility.SetDirty(rt);
        Debug.Log("[TitleScreenFXSetupTool] ⑨ロゴを少し縮めて下げました（Scale 0.94、Pos Y 15→0）");
    }

    // =====================================================================
    // 2回目（④〜⑧）はすべてユーザー判断で不要となり削除。シーンに残った部品・変更をメニューで元に戻す
    // =====================================================================

    private static readonly string[] LetterNames = { "N1", "E1", "O1", "N2", "D", "A", "N3", "C", "E2", "R" };
    private static readonly string[] ButtonImageNames = { "StartButtonImage", "SettingsButtonImage", "LanguageButtonImage", "QuitButtonImage" };
    // 丸ごと消す部品：③VersionLabel ⑤FX_TitleFront ⑥FX_TouchTrail ⑦FX_TitleDancer ⑧FX_BeatPulse
    private static readonly string[] RemovedPartNames = { "VersionLabel", "FX_TitleFront", "FX_TouchTrail", "FX_TitleDancer", "FX_BeatPulse" };

    [MenuItem("Tools/タイトル画面/不要になった③〜⑧の部品を削除（01_Title）")]
    private static void RunRemove()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool wasLoaded = scene.IsValid() && scene.isLoaded;
        if (!wasLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        int n = 0;
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == null || System.Array.IndexOf(RemovedPartNames, t.name) < 0) continue;
                Object.DestroyImmediate(t.gameObject);
                n++;
            }

        TitleMenu menu = null; TitleLogoLetters logo = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (menu == null) menu = root.GetComponentInChildren<TitleMenu>(true);
            if (logo == null) logo = root.GetComponentInChildren<TitleLogoLetters>(true);
        }

        // ④：TitlePanelのTitleIntroFX（スクリプト削除済みで「Missing Script」になっている）を外す
        if (menu != null && menu.startButton != null)
        {
            var panel = menu.startButton.transform.parent;
            int m = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(panel.gameObject);
            if (m > 0) { n += m; EditorUtility.SetDirty(panel.gameObject); }
            // ④で足したCanvasGroup（4つのボタン画像）を外す
            foreach (var bn in ButtonImageNames)
            {
                var b = panel.Find(bn);
                if (b != null) n += RemoveCanvasGroup(b.gameObject);
            }
        }
        // ④で足したCanvasGroupを外し、OFFにした文字の「起動時の点灯シーケンス」を元どおりONに戻す
        if (logo != null)
        {
            foreach (var ln in LetterNames)
            {
                var l = logo.transform.Find(ln);
                if (l == null) continue;
                n += RemoveCanvasGroup(l.gameObject);
                var ne = l.GetComponent<TitleNeonEffect>();
                if (ne == null) continue;
                var nso = new SerializedObject(ne);
                nso.FindProperty("powerOnSequenceEnabled").boolValue = true;
                nso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(ne);
            }
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[TitleScreenFXSetupTool] 01_Titleから③〜⑧の部品を{n}個削除し、ロゴの点灯シーケンスを元に戻して保存しました");
        if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
    }

    private static int RemoveCanvasGroup(GameObject go)
    {
        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) return 0;
        Object.DestroyImmediate(cg);
        EditorUtility.SetDirty(go);
        return 1;
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
    private static void Stretch(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; rt.pivot = new Vector2(0.5f, 0.5f); }
    private static void Center(RectTransform rt, Vector2 size) { rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f); rt.sizeDelta = size; }
    private static void IgnoreLayout(RectTransform rt) { var le = rt.GetComponent<LayoutElement>(); if (le == null) le = rt.gameObject.AddComponent<LayoutElement>(); le.ignoreLayout = true; }
    private static Image Img(RectTransform rt, Sprite s, Color c, Image.Type type = Image.Type.Simple)
    {
        var im = rt.GetComponent<Image>();
        if (im == null) im = rt.gameObject.AddComponent<Image>();
        im.sprite = s; im.color = c; im.type = type; im.raycastTarget = false; im.preserveAspect = false;
        return im;
    }
    private static void SetArray(SerializedObject so, string name, List<Object> items)
    {
        var p = so.FindProperty(name);
        p.arraySize = items.Count;
        for (int i = 0; i < items.Count; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = items[i];
    }

    // =====================================================================
    // 画像（既存のジェム画面・SOUND画面の画像を流用し、STARTの縁取りだけ新しく作る）
    // =====================================================================
    private static Dictionary<string, Sprite> LoadSprites()
    {
        var d = new Dictionary<string, Sprite>
        {
            ["glow"] = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/SoundFX/Sound_Glow.png"),
            ["bar"] = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/UI/SoundFX/Sound_Bar.png"),
            ["aura"] = EnsureAura(),
        };
        foreach (var kv in d)
            if (kv.Value == null) { Debug.LogError($"[TitleScreenFXSetupTool] 画像「{kv.Key}」が見つかりません（ジェム画面・SOUND画面のメニューで作られる画像を使います）"); return null; }
        return d;
    }

    /// <summary>STARTボタン（500×125のピル形）の外側に広がる光＋枠の外側の線に重なる細い線。白で描き、Imageの色で着色する</summary>
    private static Sprite EnsureAura()
    {
        string path = ArtDir + "/Title_StartAura.png";
        if (!File.Exists(path))
        {
            if (!Directory.Exists(ArtDir)) Directory.CreateDirectory(ArtDir);
            const float bw = 500f, bh = 125f, pad = AuraPad;
            int w = (int)(bw + pad * 2f), h = (int)(bh + pad * 2f);
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.SetPixels32(new Color32[w * h]);
            float r = bh * 0.5f, half = bw * 0.5f - r;
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                float px = x + 0.5f - w * 0.5f, py = y + 0.5f - h * 0.5f;
                float qx = Mathf.Max(Mathf.Abs(px) - half, 0f);
                float d = Mathf.Sqrt(qx * qx + py * py) - r; // ボタンの外周からの距離（外側がプラス）
                float glow = d >= 0f ? Mathf.Pow(Mathf.Clamp01(1f - d / pad), 2f) * 0.85f : 0f;
                float rim = Mathf.Clamp01(1f - Mathf.Abs(d + 3.5f) / 2.5f); // 枠の外側の線（外周から約3.5内側）
                float a = Mathf.Max(glow, rim);
                if (a > 0f) t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            t.Apply();
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite; imp.spriteImportMode = SpriteImportMode.Single;
            imp.mipmapEnabled = false; imp.alphaIsTransparency = true; imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.npotScale = TextureImporterNPOTScale.None;
            var st = new TextureImporterSettings(); imp.ReadTextureSettings(st); st.spriteMeshType = SpriteMeshType.FullRect; imp.SetTextureSettings(st);
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
