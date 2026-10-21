using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 05_Gameの中断メニューのHELP画面（PauseMenuUI.helpPanel。チュートリアルも共通）を、1ページ1項目のページ送り（HelpPagerUI）にする。
/// 旧来のスクロール表示（HelpScrollView）は削除せず非表示にして残す。何度実行しても安全（既存の部品は作り直さず設定を合わせる）。
/// 文言（pause.help.page0〜4のtitle/body、日本語・英語）はLocalizationTableに無い時だけ追加する
/// </summary>
public static class HelpPagerSetupTool
{
    private const string ScenePath = "Assets/Scenes/05_Game.unity";
    private const string ArtDir = "Assets/Art/UI/HelpFX";
    private const string FontPath = "Assets/Fonts/NotoSansJP-Regular SDF.asset";

    private static readonly (string jaTitle, string jaBody, string enTitle, string enBody)[] PageTexts =
    {
        ("赤線の特徴",
         "・ジャスト反射しやすい\n・ジャスト反射ダメージ・持続時間・反射加速・硬度が白線より高い\n・線ゲージの消費が大きく、回復も遅い\n・白線と組み合わせたり、ここぞという場面で使うと効果的",
         "Red Line Traits",
         "• Easier to land Just Reflects\n• Beats the White Line in Just Reflect damage, duration, reflection acceleration, and hardness\n• Uses more Line Gauge and recovers slowly\n• Best paired with the White Line or saved for key moments"),
        ("円の様々な効果",
         "・反射した弾と敵を同じ円で囲むと、短時間に何度もダメージを与えられる\n・HP0で抜け出したソウルを円で囲むと復活できる\n・特定の弾の効果を消せる\n・ブロックから出るゴールドやハートの取得量が増える",
         "Circle Effects",
         "• Encircle a reflected bullet and an enemy together to hit it many times in a short time\n• At 0 HP, encircle the soul that comes loose to revive\n• Cancels certain bullet effects\n• Increases the Gold and Hearts you get from Blocks"),
        ("ソウルの救済の注意点",
         "・救済するたびにHP全快で復活できる\n・ただし、再びソウルが抜け出すと落下が速くなり、救済が難しくなる",
         "Soul Rescue Caution",
         "• Each rescue revives you with full HP\n• But if the soul comes loose again, it falls faster and is harder to rescue"),
        ("ジェムの取得と効果",
         "・スキル効果を得られる不思議な宝石\n・エリアセレクトで着け外しできる\n・新しいエリアを開放するたびに、装備できる数が増える\n・プレイするたびに使用可能回数が減り、0になると壊れて消える",
         "Gems",
         "• Mysterious jewels that grant skill effects\n• Equip and unequip them in Area Select\n• You can equip more each time you unlock a new Area\n• Uses decrease each play; at 0 the gem breaks and disappears"),
        ("フードの購入と効果",
         "・1プレイ限りでスキルが一時的に強化される食べ物\n・エリアセレクトで購入できる（最大3つまで）\n・同じフードは複数購入できない",
         "Food",
         "• Food that gives a temporary skill boost for one play\n• Buy it in Area Select (up to 3)\n• You can't buy more than one of the same food"),
    };

    [MenuItem("Tools/中断画面/HELPをページ送りにする（05_Game）")]
    private static void Run()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool wasLoaded = scene.IsValid() && scene.isLoaded;
        if (!wasLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);

        PauseMenuUI pause = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            pause = root.GetComponentInChildren<PauseMenuUI>(true);
            if (pause != null) break;
        }
        if (pause == null) { Debug.LogError("[HelpPagerSetupTool] PauseMenuUIが見つかりません"); }
        else if (Build(pause, scene))
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[HelpPagerSetupTool] 05_GameのHELP画面をページ送りにして保存しました（文言{AddLocalization()}件追加）");
        }
        if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
    }

    // 赤線（1ページ目）とジェム（4ページ目）のアイコンだけ：横長・余白の多い画像で小さく、見出しより上に見えるため、大きくして少し下げる。
    // ドリンク（5ページ目）は少し小さく見えるため少しだけ大きくする
    private static readonly (int page, float scale, Vector2 offset)[] IconAdjusts =
    {
        (0, 1.3f, new Vector2(0f, -12f)),
        (3, 1.3f, new Vector2(0f, -6f)),
        (4, 1.15f, Vector2.zero),
    };

    [MenuItem("Tools/中断画面/HELPの赤線・ジェム・ドリンクのアイコンを大きくする（05_Game）")]
    private static void RunIconAdjust()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = SceneManager.GetSceneByPath(ScenePath);
        bool wasLoaded = scene.IsValid() && scene.isLoaded;
        if (!wasLoaded) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            int n = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var pager in root.GetComponentsInChildren<HelpPagerUI>(true))
                {
                    var so = new SerializedObject(pager);
                    var pages = so.FindProperty("pages");
                    foreach (var a in IconAdjusts)
                    {
                        if (a.page >= pages.arraySize) continue;
                        var el = pages.GetArrayElementAtIndex(a.page);
                        el.FindPropertyRelative("iconScale").floatValue = a.scale;
                        el.FindPropertyRelative("iconOffset").vector2Value = a.offset;
                        var sp = el.FindPropertyRelative("icon").objectReferenceValue;
                        Debug.Log($"[HelpPagerSetupTool] {pager.name} ページ{a.page + 1}（{(sp != null ? sp.name : "アイコン無し")}）：倍率{a.scale}・ずらし{a.offset}");
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(pager);
                    n++;
                }
            }
            if (n == 0) { Debug.LogError("[HelpPagerSetupTool] 05_GameにHelpPagerUIが見つかりません（先に「HELPをページ送りにする」を実行）"); return; }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (!wasLoaded) EditorSceneManager.CloseScene(scene, true);
        }
    }

    private static bool Build(PauseMenuUI pause, Scene scene)
    {
        var o = new SerializedObject(pause);
        var panel = o.FindProperty("helpPanel").objectReferenceValue as GameObject;
        if (panel == null) { Debug.LogError("[HelpPagerSetupTool] helpPanelが未設定です"); return false; }
        var scroll = panel.transform.Find("HelpScrollView") as RectTransform;
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        var sp = EnsureSprites();

        // ---- 本体：旧スクロール表示と同じ位置・同じレイアウト設定で差し替える ----
        var pager = Child(panel.transform, "HelpPager");
        if (scroll != null) pager.SetSiblingIndex(scroll.GetSiblingIndex());
        var le = GetOrAdd<LayoutElement>(pager);
        var oldLe = scroll != null ? scroll.GetComponent<LayoutElement>() : null;
        le.preferredHeight = oldLe != null ? oldLe.preferredHeight : 360f;
        le.flexibleHeight = oldLe != null ? oldLe.flexibleHeight : 1f;
        // スワイプを受け取るための透明な当たり判定
        var hit = GetOrAdd<Image>(pager);
        hit.color = new Color(0f, 0f, 0f, 0f); hit.raycastTarget = true;
        if (scroll != null) scroll.gameObject.SetActive(false);

        var helper = GetOrAdd<HelpPagerUI>(pager);
        var so = new SerializedObject(helper);

        // 内容（見出し＋区切り線＋本文）：切り替え時にスライド＆フェード
        var content = Child(pager, "Content");
        content.anchorMin = Vector2.zero; content.anchorMax = Vector2.one; content.pivot = new Vector2(0.5f, 0.5f);
        content.offsetMin = new Vector2(0f, 64f); content.offsetMax = Vector2.zero;
        var cg = GetOrAdd<CanvasGroup>(content);
        cg.blocksRaycasts = false;

        var icon = Child(content, "Icon");
        icon.anchorMin = icon.anchorMax = new Vector2(0f, 1f); icon.pivot = new Vector2(0f, 1f);
        icon.anchoredPosition = new Vector2(40f, -4f); icon.sizeDelta = new Vector2(60f, 60f);
        var iconImg = Img(icon, null, Color.white); iconImg.preserveAspect = true;

        var title = Child(content, "Title");
        title.anchorMin = new Vector2(0f, 1f); title.anchorMax = new Vector2(1f, 1f); title.pivot = new Vector2(0f, 1f);
        title.offsetMin = new Vector2(116f, -66f); title.offsetMax = new Vector2(-40f, -4f);
        var titleT = Text(title, font, 36, TextAlignmentOptions.MidlineLeft, new Color(1f, 0.85f, 0.3f, 1f));
        titleT.fontStyle = FontStyles.Bold;

        var line = Child(content, "Divider");
        line.anchorMin = new Vector2(0f, 1f); line.anchorMax = new Vector2(1f, 1f); line.pivot = new Vector2(0.5f, 1f);
        line.offsetMin = new Vector2(36f, -78f); line.offsetMax = new Vector2(-36f, -75f);
        Img(line, null, Color.white);
        var grad = GetOrAdd<UIHorizontalGradient>(line);
        grad.colorLeft = new Color(0.35f, 0.9f, 1f, 0.9f); grad.colorRight = new Color(1f, 0.4f, 1f, 0.15f);

        var body = Child(content, "Body");
        body.anchorMin = Vector2.zero; body.anchorMax = Vector2.one; body.pivot = new Vector2(0.5f, 1f);
        body.offsetMin = new Vector2(48f, 8f); body.offsetMax = new Vector2(-48f, -96f);
        var bodyT = Text(body, font, 27, TextAlignmentOptions.TopLeft, Color.white);
        bodyT.lineSpacing = 12f; bodyT.paragraphSpacing = 16f;
        bodyT.enableAutoSizing = true; bodyT.fontSizeMin = 20; bodyT.fontSizeMax = 27;

        // ナビ（◀ ● ○ ○ ○ ○ ▶）
        var nav = Child(pager, "Nav");
        nav.anchorMin = new Vector2(0f, 0f); nav.anchorMax = new Vector2(1f, 0f); nav.pivot = new Vector2(0.5f, 0f);
        nav.offsetMin = new Vector2(0f, 4f); nav.offsetMax = new Vector2(0f, 60f);
        var prev = ArrowButton(nav, "PrevButton", sp["arrow"], -190f, flip: true);
        var next = ArrowButton(nav, "NextButton", sp["arrow"], 190f, flip: false);
        var dotsP = so.FindProperty("dots"); dotsP.arraySize = PageTexts.Length;
        for (int i = 0; i < PageTexts.Length; i++)
        {
            var d = Child(nav, "Dot" + i);
            d.anchorMin = d.anchorMax = d.pivot = new Vector2(0.5f, 0.5f);
            d.anchoredPosition = new Vector2((i - (PageTexts.Length - 1) * 0.5f) * 30f, 0f); d.sizeDelta = new Vector2(16f, 16f);
            dotsP.GetArrayElementAtIndex(i).objectReferenceValue = Img(d, sp["dot"], Color.white);
        }

        so.FindProperty("iconImage").objectReferenceValue = iconImg;
        so.FindProperty("titleText").objectReferenceValue = titleT;
        so.FindProperty("bodyText").objectReferenceValue = bodyT;
        so.FindProperty("contentGroup").objectReferenceValue = cg;
        so.FindProperty("contentRect").objectReferenceValue = content;
        so.FindProperty("prevButton").objectReferenceValue = prev;
        so.FindProperty("nextButton").objectReferenceValue = next;
        if (so.FindProperty("pageTurnSe").objectReferenceValue == null)
            so.FindProperty("pageTurnSe").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/GEM/カーソル移動2.mp3");

        // ページ（アイコン・日本語の予備文言）
        Sprite redLine = FindRedLineIcon(scene);
        Sprite[] icons =
        {
            redLine,
            sp["circle"],
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/PixelDancer/Soul.png"),
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/AreaSelect/ジェムアイコン.png"),
            AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/AreaSelect/Shop/ボタンのアイコン.png"), // 2026/10/21 フード画面に変更（旧：ドリンクアイコン.png）
        };
        var pagesP = so.FindProperty("pages");
        pagesP.arraySize = PageTexts.Length;
        for (int i = 0; i < PageTexts.Length; i++)
        {
            var el = pagesP.GetArrayElementAtIndex(i);
            if (el.FindPropertyRelative("icon").objectReferenceValue == null) el.FindPropertyRelative("icon").objectReferenceValue = icons[i];
            el.FindPropertyRelative("fallbackTitle").stringValue = PageTexts[i].jaTitle;
            el.FindPropertyRelative("fallbackBody").stringValue = PageTexts[i].jaBody;
            if (icons[i] == null) Debug.LogWarning($"[HelpPagerSetupTool] ページ{i + 1}のアイコン画像が見つかりません。HelpPagerUIのPagesで手動設定してください");
        }

        // キーワードの色：チュートリアルのガイドと同じ一覧をコピー（空の時だけ）
        var kw = so.FindProperty("keywords");
        if (kw.arraySize == 0)
        {
            TutorialGuideUI guide = null;
            foreach (var root in scene.GetRootGameObjects()) { guide = root.GetComponentInChildren<TutorialGuideUI>(true); if (guide != null) break; }
            if (guide != null)
            {
                var src = guide.EditorKeywords;
                kw.arraySize = src.Count;
                for (int i = 0; i < src.Count; i++)
                {
                    kw.GetArrayElementAtIndex(i).FindPropertyRelative("keyword").stringValue = src[i].keyword;
                    kw.GetArrayElementAtIndex(i).FindPropertyRelative("color").colorValue = src[i].color;
                }
            }
            else Debug.LogWarning("[HelpPagerSetupTool] TutorialGuideUIが無いため、キーワードの色はコピーしませんでした（HelpPagerUIのKeywordsで設定可）");
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(helper);
        EditorUtility.SetDirty(panel);
        return true;
    }

    private static Sprite FindRedLineIcon(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == "Icon" && t.parent != null && t.parent.name == "RedBar" && t.parent.parent != null && t.parent.parent.name == "PaddleCostBarUI")
                {
                    var im = t.GetComponent<Image>();
                    if (im != null && im.sprite != null) return im.sprite;
                }
        return null;
    }

    private static Button ArrowButton(RectTransform parent, string name, Sprite arrow, float x, bool flip)
    {
        var rt = Child(parent, name);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(x, 0f); rt.sizeDelta = new Vector2(56f, 48f);
        rt.localScale = new Vector3(flip ? -1f : 1f, 1f, 1f);
        var img = Img(rt, arrow, new Color(0.45f, 0.95f, 1f, 1f));
        img.raycastTarget = true; img.preserveAspect = true;
        var b = GetOrAdd<Button>(rt);
        b.targetGraphic = img;
        var colors = b.colors; colors.disabledColor = Color.white; b.colors = colors; // 押せない時の濃さはHelpPagerUIが決める
        return b;
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
        for (int i = 0; i < PageTexts.Length; i++)
        {
            Add($"pause.help.page{i}.title", PageTexts[i].jaTitle, PageTexts[i].enTitle);
            Add($"pause.help.page{i}.body", PageTexts[i].jaBody, PageTexts[i].enBody);
        }
        if (added > 0) { so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(table); AssetDatabase.SaveAssets(); }
        return added;
    }

    // ===== 小物 =====
    private static T GetOrAdd<T>(Component c) where T : Component
    {
        var x = c.GetComponent<T>();
        return x != null ? x : c.gameObject.AddComponent<T>();
    }
    private static RectTransform Child(Transform parent, string name)
    {
        var t = parent.Find(name) as RectTransform;
        if (t != null) return t;
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }
    private static Image Img(RectTransform rt, Sprite s, Color c)
    {
        var im = GetOrAdd<Image>(rt);
        im.sprite = s; im.color = c; im.raycastTarget = false;
        return im;
    }
    private static TextMeshProUGUI Text(RectTransform rt, TMP_FontAsset font, float size, TextAlignmentOptions align, Color c)
    {
        var t = GetOrAdd<TextMeshProUGUI>(rt);
        if (font != null) { t.font = font; t.fontSharedMaterial = font.material; }
        t.fontSize = size; t.alignment = align; t.color = c; t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        return t;
    }

    private static Dictionary<string, Sprite> EnsureSprites()
    {
        if (!Directory.Exists(ArtDir)) Directory.CreateDirectory(ArtDir);
        return new Dictionary<string, Sprite>
        {
            ["arrow"] = Ensure("Help_Arrow", ArrowTex),
            ["dot"] = Ensure("Help_Dot", DotTex),
            ["circle"] = Ensure("Help_Circle", CircleTex),
        };
    }

    private static Sprite Ensure(string name, System.Func<Texture2D> make)
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
            var st = new TextureImporterSettings();
            imp.ReadTextureSettings(st);
            st.spriteMeshType = SpriteMeshType.FullRect;
            imp.SetTextureSettings(st);
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Texture2D NewTex(int w, int h) { var t = new Texture2D(w, h, TextureFormat.RGBA32, false); t.SetPixels32(new Color32[w * h]); return t; }

    /// <summary>右向きの三角（角を少し丸めた矢印）＋外側の光</summary>
    private static Texture2D ArrowTex()
    {
        int s = 64; var t = NewTex(s, s);
        Vector2 a = new Vector2(16f, 8f), b = new Vector2(16f, 56f), c = new Vector2(54f, 32f);
        for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
        {
            Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
            float d = TriSd(p, a, b, c);
            float alpha = Mathf.Max(Mathf.Clamp01(0.5f - d), d > 0f ? Mathf.Clamp01(1f - d / 6f) * 0.35f : 0f);
            if (alpha > 0f) t.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
        }
        t.Apply(); return t;
    }

    private static Texture2D DotTex()
    {
        int s = 32; var t = NewTex(s, s); float cc = (s - 1) * 0.5f;
        for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
        {
            float r = Mathf.Sqrt((x - cc) * (x - cc) + (y - cc) * (y - cc));
            float a = Mathf.Max(Mathf.Clamp01(9f - r + 0.5f), Mathf.Clamp01(1f - (r - 9f) / 6f) * 0.4f);
            if (a > 0f) t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
        }
        t.Apply(); return t;
    }

    /// <summary>「円」の見出し用：線で描いた円＋外側の光（ネオン風、シアン）</summary>
    private static Texture2D CircleTex()
    {
        int s = 96; var t = NewTex(s, s); float cc = (s - 1) * 0.5f;
        for (int y = 0; y < s; y++) for (int x = 0; x < s; x++)
        {
            float r = Mathf.Sqrt((x - cc) * (x - cc) + (y - cc) * (y - cc));
            float d = Mathf.Abs(r - 34f);
            float line = Mathf.Clamp01(3.5f - d + 0.5f);
            float glow = Mathf.Clamp01(1f - d / 12f) * 0.45f;
            float a = Mathf.Max(line, glow);
            Color col = Color.Lerp(new Color(0.4f, 0.95f, 1f, 1f), Color.white, line * 0.6f);
            if (a > 0f) t.SetPixel(x, y, new Color(col.r, col.g, col.b, a));
        }
        t.Apply(); return t;
    }

    private static float TriSd(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d = Mathf.Min(Seg(p, a, b), Mathf.Min(Seg(p, b, c), Seg(p, c, a)));
        bool inside = Cross(b - a, p - a) <= 0f && Cross(c - b, p - b) <= 0f && Cross(a - c, p - c) <= 0f
                   || Cross(b - a, p - a) >= 0f && Cross(c - b, p - b) >= 0f && Cross(a - c, p - c) >= 0f;
        return inside ? -d : d;
    }
    private static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;
    private static float Seg(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a; float k = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return Vector2.Distance(p, a + ab * k);
    }
}
