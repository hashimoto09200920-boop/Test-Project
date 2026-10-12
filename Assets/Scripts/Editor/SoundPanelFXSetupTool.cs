using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// SOUND画面（01_TitleのTitleMenu.soundPanel、05_GameのPauseMenuUI.soundPanel）に演出（SoundPanelFX）を組み込む。
/// 2シーンを順に開いて同じ部品を作り、保存する。何度実行しても安全（既にある部品は作り直さず、設定だけ合わせる）。
/// SoundPanelは縦並びの自動レイアウト（VerticalLayoutGroup）なので、追加する演出の親にはLayoutElement(ignoreLayout)を付ける
/// </summary>
public static class SoundPanelFXSetupTool
{
    private const string ArtDir = "Assets/Art/UI/SoundFX";
    private static readonly (string path, string owner)[] Targets =
    {
        ("Assets/Scenes/01_Title.unity", "TitleMenu"),
        ("Assets/Scenes/05_Game.unity", "PauseMenuUI"),
    };

    private static readonly Color BgmA = new Color(0.3f, 0.95f, 1f, 1f), BgmB = new Color(0.72f, 0.42f, 1f, 1f);
    private static readonly Color SeA = new Color(1f, 0.9f, 0.3f, 1f), SeB = new Color(1f, 0.5f, 0.18f, 1f);

    [MenuItem("Tools/サウンド画面/演出を作成（Title・05_Game）")]
    private static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var sp = EnsureSprites();

        foreach (var (path, owner) in Targets)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            bool wasLoaded = scene.IsValid() && scene.isLoaded;
            if (!wasLoaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

            MonoBehaviour ownerComp = null;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
                    if (mb != null && mb.GetType().Name == owner) { ownerComp = mb; break; }

            if (ownerComp == null) Debug.LogWarning($"[SoundPanelFXSetupTool] {scene.name}：{owner}が見つかりません");
            else if (Build(ownerComp, sp))
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[SoundPanelFXSetupTool] {scene.name}：SOUND画面の演出を組み込んで保存しました");
            }
            if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static bool Build(MonoBehaviour owner, Dictionary<string, Sprite> sp)
    {
        var o = new SerializedObject(owner);
        var panel = o.FindProperty("soundPanel").objectReferenceValue as GameObject;
        var bgmSlider = o.FindProperty("bgmVolumeSlider").objectReferenceValue as Slider;
        var seSlider = o.FindProperty("seVolumeSlider").objectReferenceValue as Slider;
        var bgmText = o.FindProperty("bgmVolumeText").objectReferenceValue as TextMeshProUGUI;
        var seText = o.FindProperty("seVolumeText").objectReferenceValue as TextMeshProUGUI;
        if (panel == null || bgmSlider == null || seSlider == null) { Debug.LogWarning($"[SoundPanelFXSetupTool] {owner.name}：soundPanel/スライダーが未設定です"); return false; }

        var fx = panel.GetComponent<SoundPanelFX>();
        if (fx == null) fx = panel.AddComponent<SoundPanelFX>();
        var so = new SerializedObject(fx);
        var frame = panel.transform.Find("SoundBg") as RectTransform;

        so.FindProperty("bgmSlider").objectReferenceValue = bgmSlider;
        so.FindProperty("seSlider").objectReferenceValue = seSlider;
        so.FindProperty("bgmText").objectReferenceValue = bgmText;
        so.FindProperty("seText").objectReferenceValue = seText;
        so.FindProperty("frameRect").objectReferenceValue = frame;

        // ① ③ スライダー（ネオンのゲージ・光る玉のつまみ）
        SetupSlider(bgmSlider, false, so, fx, sp);
        SetupSlider(seSlider, true, so, fx, sp);

        // ⑤ 音波アイコン ⑥ イコライザー（数値の行の右側）
        if (bgmText != null) { SetArray(so, "bgmWaves", BuildWaves(bgmText.rectTransform, sp)); SetArray(so, "eqBars", BuildEq(bgmText.rectTransform)); }
        if (seText != null) SetArray(so, "seWaves", BuildWaves(seText.rectTransform, sp));

        // ⑩ 枠を走る光：枠（SoundBg）のすぐ上・文字より奥
        var back = Child(panel.transform, "FX_Back");
        Stretch(back); IgnoreLayout(back);
        back.SetSiblingIndex(frame != null ? frame.GetSiblingIndex() + 1 : 0);
        // ⑨ 背景に浮かぶ音符はユーザー判断で不要：以前の版で作った部品が残っていれば削除
        for (int i = 0; i < 8; i++)
        {
            var oldNote = back.Find("Note" + i);
            if (oldNote != null) Object.DestroyImmediate(oldNote.gameObject);
        }
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
        so.FindProperty("runnerInset").vector2Value = new Vector2(80f, 55f); // MainBg.pngの枠線の位置（800x1050に引き伸ばした時）

        // 火花・波紋・光の帯：一番手前
        var front = Child(panel.transform, "FX_Front");
        Stretch(front); IgnoreLayout(front);
        front.SetAsLastSibling();
        var sparks = new List<Object>();
        for (int i = 0; i < 14; i++) { var s = Child(front, "Spark" + i); Center(s, new Vector2(14, 14)); var im = Img(s, sp["glow"], Color.white); im.enabled = false; sparks.Add(im); }
        SetArray(so, "sparks", sparks);
        var rip = new List<Object>();
        for (int i = 0; i < 4; i++) { var r = Child(front, "Ripple" + i); Center(r, new Vector2(40, 40)); var im = Img(r, sp["ring"], Color.white); im.enabled = false; rip.Add(im); }
        SetArray(so, "ripples", rip);
        // ⑧の光の帯（上から下りる横棒）はユーザー判断で不要：以前の版で作った部品が残っていれば削除
        var oldSweep = front.Find("Sweep");
        if (oldSweep != null) Object.DestroyImmediate(oldSweep.gameObject);

        // ⑦ 試し鳴らしの音（戻るボタンのホバー音を流用。Inspectorで差し替え可）
        if (so.FindProperty("sePreviewClip").objectReferenceValue == null)
            so.FindProperty("sePreviewClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/GEM/カーソル移動2.mp3");

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(fx);
        return true;
    }

    private static void SetupSlider(Slider s, bool isSE, SerializedObject so, SoundPanelFX fx, Dictionary<string, Sprite> sp)
    {
        Color a = isSE ? SeA : BgmA, b = isSE ? SeB : BgmB;
        var t = s.transform;

        // 溝：暗い角丸＋ふちの光。太すぎないよう上下30%ずつ細くする
        var bg = t.Find("Background") as RectTransform;
        if (bg != null)
        {
            bg.anchorMin = new Vector2(0f, 0.3f); bg.anchorMax = new Vector2(1f, 0.7f);
            bg.offsetMin = Vector2.zero; bg.offsetMax = Vector2.zero;
            var im = bg.GetComponent<Image>();
            if (im != null) { im.sprite = sp["track"]; im.type = Image.Type.Sliced; im.color = new Color(0.04f, 0.06f, 0.12f, 0.92f); }
            var rim = Child(bg, "FX_TrackRim");
            rim.anchorMin = Vector2.zero; rim.anchorMax = Vector2.one; rim.offsetMin = new Vector2(-3, -3); rim.offsetMax = new Vector2(3, 3);
            Img(rim, sp["rim"], new Color(a.r, a.g, a.b, 0.55f), Image.Type.Sliced);
        }
        var fillArea = t.Find("Fill Area") as RectTransform;
        if (fillArea != null)
        {
            fillArea.anchorMin = new Vector2(fillArea.anchorMin.x, 0.3f); fillArea.anchorMax = new Vector2(fillArea.anchorMax.x, 0.7f);
            fillArea.offsetMin = new Vector2(fillArea.offsetMin.x, 0f); fillArea.offsetMax = new Vector2(fillArea.offsetMax.x, 0f);
        }
        // 塗り：グラデーション＋流れる光＋先端の光
        var fill = s.fillRect;
        if (fill != null)
        {
            var im = fill.GetComponent<Image>();
            if (im != null) { im.sprite = sp["track"]; im.type = Image.Type.Sliced; im.color = Color.white; }
            var g = fill.GetComponent<UIHorizontalGradient>();
            if (g == null) g = fill.gameObject.AddComponent<UIHorizontalGradient>();
            g.colorLeft = a; g.colorRight = b;
            var tip = Child(fill, "FX_FillTip");
            tip.anchorMin = tip.anchorMax = new Vector2(1f, 0.5f); tip.pivot = new Vector2(0.5f, 0.5f);
            tip.anchoredPosition = Vector2.zero; tip.sizeDelta = new Vector2(46, 46);
            so.FindProperty(isSE ? "seFillTip" : "bgmFillTip").objectReferenceValue = Img(tip, sp["glow"], Color.Lerp(b, Color.white, 0.4f));
        }
        // つまみ：光る玉＋♪／スピーカー
        var handle = s.handleRect;
        if (handle != null)
        {
            handle.sizeDelta = new Vector2(50f, 0f);
            var im = handle.GetComponent<Image>();
            if (im != null) { im.sprite = sp["orb"]; im.type = Image.Type.Simple; im.color = Color.white; im.preserveAspect = true; }
            var icon = Child(handle, "FX_HandleIcon");
            Center(icon, new Vector2(26, 26));
            Img(icon, isSE ? sp["speaker"] : sp["note1"], new Color(0.1f, 0.12f, 0.28f, 1f));
            so.FindProperty(isSE ? "seHandle" : "bgmHandle").objectReferenceValue = handle;
        }
        var relay = s.GetComponent<SoundSliderPointerRelay>();
        if (relay == null) relay = s.gameObject.AddComponent<SoundSliderPointerRelay>();
        relay.fx = fx; relay.isSE = isSE;
        EditorUtility.SetDirty(relay);
        EditorUtility.SetDirty(s.gameObject);
    }

    private static List<Object> BuildWaves(RectTransform row, Dictionary<string, Sprite> sp)
    {
        var box = Child(row, "FX_Waves");
        box.anchorMin = box.anchorMax = new Vector2(1f, 0.5f); box.pivot = new Vector2(1f, 0.5f);
        box.anchoredPosition = new Vector2(-6f, 0f); box.sizeDelta = new Vector2(78f, 40f);
        var spk = Child(box, "Speaker"); spk.anchorMin = spk.anchorMax = new Vector2(0f, 0.5f); spk.anchoredPosition = new Vector2(14f, 0f); spk.sizeDelta = new Vector2(26, 26);
        Img(spk, sp["speaker"], new Color(0.75f, 0.95f, 1f, 1f));
        var list = new List<Object>();
        for (int i = 0; i < 3; i++)
        {
            var w = Child(box, "Wave" + i);
            w.anchorMin = w.anchorMax = new Vector2(0f, 0.5f);
            w.anchoredPosition = new Vector2(34f + i * 12f, 0f);
            w.sizeDelta = new Vector2(10f + i * 3f, 14f + i * 9f);
            list.Add(Img(w, sp["arc"], new Color(0.6f, 0.95f, 1f, 1f)));
        }
        return list;
    }

    private static List<Object> BuildEq(RectTransform row)
    {
        var box = Child(row, "FX_Equalizer");
        box.anchorMin = box.anchorMax = new Vector2(1f, 0.5f); box.pivot = new Vector2(1f, 0.5f);
        box.anchoredPosition = new Vector2(-92f, 0f); box.sizeDelta = new Vector2(92f, 40f);
        var list = new List<Object>();
        for (int i = 0; i < 8; i++)
        {
            var bar = Child(box, "Bar" + i);
            bar.anchorMin = bar.anchorMax = new Vector2(0f, 0f); bar.pivot = new Vector2(0.5f, 0f);
            bar.anchoredPosition = new Vector2(6f + i * 11.5f, 3f); bar.sizeDelta = new Vector2(7f, 6f);
            Img(bar, null, Color.Lerp(BgmA, BgmB, i / 7f));
            list.Add(bar);
        }
        return list;
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
    // 画像の生成（無い時だけ作る）
    // =====================================================================
    private static Dictionary<string, Sprite> EnsureSprites()
    {
        if (!Directory.Exists(ArtDir)) Directory.CreateDirectory(ArtDir);
        return new Dictionary<string, Sprite>
        {
            ["track"] = Ensure("Sound_Track", () => RoundRect(64, 32, 15f, 0f, false), new Vector4(16, 15, 16, 15)),
            ["rim"] = Ensure("Sound_TrackRim", () => RoundRect(64, 36, 17f, 2.5f, true), new Vector4(18, 17, 18, 17)),
            ["orb"] = Ensure("Sound_Orb", OrbTexture, Vector4.zero),
            ["glow"] = Ensure("Sound_Glow", () => Glow(64), Vector4.zero),
            ["ring"] = Ensure("Sound_Ring", () => Ring(128, 60f, 52f), Vector4.zero),
            ["bar"] = Ensure("Sound_Bar", () => SoftBar(256, 32), Vector4.zero),
            ["note1"] = Ensure("Sound_Note1", () => Note(false), Vector4.zero),
            ["note2"] = Ensure("Sound_Note2", () => Note(true), Vector4.zero),
            ["speaker"] = Ensure("Sound_Speaker", Speaker, Vector4.zero),
            ["arc"] = Ensure("Sound_Arc", Arc, Vector4.zero),
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
    private static void Put(Texture2D t, int x, int y, float a, float bright = 1f) { if (a > 0f) t.SetPixel(x, y, new Color(bright, bright, bright, Mathf.Clamp01(a))); }

    private static float RoundRectSd(float px, float py, float w, float h, float r)
    {
        float qx = Mathf.Abs(px - w * 0.5f) - (w * 0.5f - r), qy = Mathf.Abs(py - h * 0.5f) - (h * 0.5f - r);
        return new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }

    private static Texture2D RoundRect(int w, int h, float r, float thick, bool outline)
    {
        var t = NewTex(w, h);
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            float d = RoundRectSd(x + 0.5f, y + 0.5f, w, h, r);
            float a = outline ? Mathf.Max(Mathf.Clamp01(thick * 0.5f - Mathf.Abs(d + thick * 0.5f + 0.5f) + 0.5f), d > -thick ? 0f : 0f)
                              : Mathf.Clamp01(0.5f - d);
            Put(t, x, y, a);
        }
        t.Apply(); return t;
    }

    private static Texture2D OrbTexture()
    {
        int s = 96; var t = NewTex(s, s); float c = (s - 1) * 0.5f;
        for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
        {
            float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
            float core = Mathf.Clamp01(30f - r + 0.5f);                    // 玉本体（半径30）
            float glow = Mathf.Clamp01(1f - (r - 30f) / 18f) * 0.55f;       // 外側の光
            float a = Mathf.Max(core, r > 30f ? glow * glow : 0f);
            Put(t, x, y, a);
        }
        t.Apply(); return t;
    }

    private static Texture2D Glow(int s)
    {
        var t = NewTex(s, s); float c = (s - 1) * 0.5f;
        for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
        {
            float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c;
            float a = Mathf.Clamp01(1f - r); a = a * a * (3f - 2f * a);
            Put(t, x, y, a);
        }
        t.Apply(); return t;
    }

    private static Texture2D Ring(int s, float outer, float inner)
    {
        var t = NewTex(s, s); float c = (s - 1) * 0.5f;
        for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
        {
            float r = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
            Put(t, x, y, Mathf.Clamp01(outer - r + 0.5f) * Mathf.Clamp01(r - inner + 0.5f));
        }
        t.Apply(); return t;
    }

    private static Texture2D SoftBar(int w, int h)
    {
        var t = NewTex(w, h);
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            float ax = 1f - Mathf.Abs(x - (w - 1) * 0.5f) / (w * 0.5f);
            float ay = 1f - Mathf.Abs(y - (h - 1) * 0.5f) / (h * 0.5f);
            Put(t, x, y, Mathf.Clamp01(ax * 1.6f) * ay * ay);
        }
        t.Apply(); return t;
    }

    /// <summary>音符（double=true で連桁の2つ玉）。白で描き、Imageの色で着色する</summary>
    private static Texture2D Note(bool dbl)
    {
        int s = 64; var t = NewTex(s, s);
        for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
        {
            Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
            float a = 0f;
            // 玉（傾いた楕円）
            a = Mathf.Max(a, Ellipse(p, new Vector2(dbl ? 16f : 24f, 14f), 9f, 6.5f, -25f));
            if (dbl) a = Mathf.Max(a, Ellipse(p, new Vector2(44f, 18f), 9f, 6.5f, -25f));
            // 棒
            a = Mathf.Max(a, Box(p, new Vector2(dbl ? 23f : 31f, 14f), new Vector2(dbl ? 26f : 34f, 56f)));
            if (dbl)
            {
                a = Mathf.Max(a, Box(p, new Vector2(51f, 18f), new Vector2(54f, 60f)));
                a = Mathf.Max(a, Quad(p, new Vector2(23f, 50f), new Vector2(54f, 54f), 6f)); // 連桁
            }
            else
            {
                a = Mathf.Max(a, Quad(p, new Vector2(33f, 55f), new Vector2(46f, 40f), 4f)); // 旗
            }
            Put(t, x, y, a);
        }
        t.Apply(); return t;
    }

    private static Texture2D Speaker()
    {
        int s = 64; var t = NewTex(s, s);
        for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
        {
            Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
            float a = Box(p, new Vector2(10f, 24f), new Vector2(24f, 40f));                       // 箱
            float coneHalf = Mathf.Lerp(8f, 22f, Mathf.Clamp01((p.x - 22f) / 22f));                 // ラッパ（右に広がる）
            if (p.x >= 22f && p.x <= 44f) a = Mathf.Max(a, Mathf.Clamp01(coneHalf - Mathf.Abs(p.y - 32f) + 0.5f));
            Put(t, x, y, a);
        }
        t.Apply(); return t;
    }

    /// <summary>右向きの円弧「)」（音波）</summary>
    private static Texture2D Arc()
    {
        int w = 32, h = 64; var t = NewTex(w, h);
        Vector2 c = new Vector2(-12f, 32f); float r = 36f;
        for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
        {
            Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
            float d = Mathf.Abs((p - c).magnitude - r);
            float ang = Mathf.Abs(Mathf.Atan2(p.y - c.y, p.x - c.x)) * Mathf.Rad2Deg;
            float a = Mathf.Clamp01(3f - d + 0.5f) * Mathf.Clamp01((60f - ang) / 6f);
            Put(t, x, y, a);
        }
        t.Apply(); return t;
    }

    private static float Ellipse(Vector2 p, Vector2 c, float rx, float ry, float deg)
    {
        float rad = deg * Mathf.Deg2Rad; Vector2 d = p - c;
        float x = d.x * Mathf.Cos(rad) + d.y * Mathf.Sin(rad), y = -d.x * Mathf.Sin(rad) + d.y * Mathf.Cos(rad);
        float k = Mathf.Sqrt((x * x) / (rx * rx) + (y * y) / (ry * ry));
        return Mathf.Clamp01((1f - k) * Mathf.Min(rx, ry) + 0.5f);
    }
    private static float Box(Vector2 p, Vector2 min, Vector2 max)
    {
        float dx = Mathf.Max(min.x - p.x, p.x - max.x), dy = Mathf.Max(min.y - p.y, p.y - max.y);
        return Mathf.Clamp01(0.5f - Mathf.Max(dx, dy));
    }
    private static float Quad(Vector2 p, Vector2 a, Vector2 b, float thick)
    {
        Vector2 ab = b - a; float k = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Mathf.Clamp01(thick * 0.5f - Vector2.Distance(p, a + ab * k) + 0.5f);
    }
}
