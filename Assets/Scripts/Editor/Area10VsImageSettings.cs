using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Area10最終ボス「NeonDancer（Player分身）」のVS演出用画像を、プレイヤー側の既存画像から自動生成する設定。
/// - ボス画像（VS_Boss10_1〜N.png）：VsIntroUIに登録されたプレイヤーのVS画像（Dancer Poses）を1枚ずつ左右反転し、
///   暗い影の分身の配色＋テーマ色の縁取り・外側の光にする。各画像の表示サイズ・位置は、プレイヤー側の同じポーズと
///   同じ大きさ・同じ足元の高さになるように計算し、シーンのArea10FinalIntroController > Vs Boss Posesに設定する
///   （VSでは毎回ランダムに1枚選ぶ。プレイヤー側のポーズとは別に選ぶ）
/// - ネームプレート（Area10_ネームプレート.png）：プレイヤーの「NEONDANCER」ネオン管画像を、テーマ色1色のネオン管に塗り替える
/// Inspectorで数値を変えて「画像を生成してArea10Configに設定」を押すと、同じファイル名で作り直す。
/// 元画像は読むだけで変更しない。
/// </summary>
[CreateAssetMenu(menuName = "NeonDancer/Area10 VS Image Settings")]
public class Area10VsImageSettings : ScriptableObject
{
    public const string BossOutPathFormat = "Assets/Art/AreaSelect/VS/VS_Boss10_{0}.png";
    public const string NamePlateOutPath = "Assets/Art/AreaSelect/VS/Area10_ネームプレート.png";
    public const string Area10ConfigPath = "Assets/Data/AreaConfigs/Area10Config.asset";
    private const string BossImporterRefPath      = "Assets/Art/AreaSelect/VS/VS_Boss9.png";
    private const string NamePlateImporterRefPath = "Assets/Art/AreaSelect/VS/Area09_ネームプレート.png";

    [Header("元画像（読むだけで変更しない。ボス画像の元はシーンのVsIntroUI > Dancer Posesを使う）")]
    [Tooltip("塗り替えるプレイヤーのネームプレート（NEONDANCER）")]
    public Texture2D sourceNamePlate;

    [Header("色")]
    [Tooltip("ON：縁取り・ネームプレートの色にArea10ConfigのVs Boss Theme Colorを使う（VSのボス側の色はAreaカラーを使う方針）")]
    public bool useAreaThemeColor = true;
    [Tooltip("useAreaThemeColorがOFFの時の縁取り・ネームプレートの色")]
    public Color themeColor = new Color(0.91f, 0.79f, 0.42f, 1f);

    [Header("ボス画像（影の分身）")]
    [Tooltip("体の暗い色（影の色）")]
    public Color shadowColor = new Color(0.06f, 0.03f, 0.14f, 1f);
    [Tooltip("体の明るさ（元画像の明暗をどれだけ残すか）")]
    [Range(0f, 2f)] public float bodyShading = 0.9f;
    [Tooltip("元の色をどれだけ残すか（0=影の色だけ、1=元の色のまま）")]
    [Range(0f, 1f)] public float keepOriginalColor = 0.18f;
    [Tooltip("明るい部分（髪のツヤ・バイザー・靴など）をテーマ色で光らせる強さ")]
    [Range(0f, 2f)] public float highlightGlow = 0.9f;
    [Tooltip("内側の縁取りの幅（px）")]
    [Range(1, 30)] public int rimWidth = 6;
    [Tooltip("内側の縁取りの強さ")]
    [Range(0f, 1f)] public float rimStrength = 0.85f;
    [Tooltip("外側の光の幅（px）。画像の周りにこの分だけ余白を足す")]
    [Range(0, 60)] public int outerGlowWidth = 16;
    [Tooltip("外側の光の濃さ")]
    [Range(0f, 1f)] public float outerGlowAlpha = 0.6f;

    [Header("ネームプレート")]
    [Tooltip("ネオン管の明るさ")]
    [Range(0.2f, 3f)] public float namePlateGain = 1.4f;
    [Tooltip("明るい芯を白く飛ばし始める明るさ（0〜1）")]
    [Range(0f, 1f)] public float namePlateCoreStart = 0.78f;

    public Color ResolveThemeColor()
    {
        if (useAreaThemeColor)
        {
            var cfg = AssetDatabase.LoadAssetAtPath<AreaConfig>(Area10ConfigPath);
            if (cfg != null) return cfg.vsBossThemeColor;
        }
        return themeColor;
    }

    // ======================================================
    // 生成
    // ======================================================

    public class BossPoseResult
    {
        public Sprite sprite;
        public float scale;
        public Vector2 positionOffset;
    }

    /// <summary>
    /// 開いているシーンのVsIntroUIからプレイヤー側のポーズ（画像・Scale・Position Offset）と画像枠の大きさを読み、
    /// ポーズごとにボス画像を生成して、プレイヤー側と同じ大きさ・同じ足元の高さになるScale/Position Offsetを計算する。
    /// </summary>
    public bool GenerateAll(out BossPoseResult[] results)
    {
        results = null;
        if (sourceNamePlate == null)
        {
            EditorUtility.DisplayDialog("Area10 VS画像", "元画像（Source Name Plate）を設定してください。", "OK");
            return false;
        }
        var vsIntro = Object.FindFirstObjectByType<VsIntroUI>(FindObjectsInactive.Include);
        if (vsIntro == null)
        {
            EditorUtility.DisplayDialog("Area10 VS画像", "VsIntroUIが見つかりません。05_Gameシーンを開いてから実行してください。", "OK");
            return false;
        }
        var vsSo = new SerializedObject(vsIntro);
        var poses = vsSo.FindProperty("dancerPoses");
        var dancerImage = vsSo.FindProperty("dancerImage").objectReferenceValue as UnityEngine.UI.Image;
        var bossImage = vsSo.FindProperty("bossImage").objectReferenceValue as UnityEngine.UI.Image;
        if (poses == null || poses.arraySize == 0 || dancerImage == null || bossImage == null)
        {
            EditorUtility.DisplayDialog("Area10 VS画像", "VsIntroUIのDancer Poses / Dancer Image / Boss Imageが未設定です。", "OK");
            return false;
        }
        Vector2 dancerRect = ((RectTransform)dancerImage.transform).rect.size;
        Vector2 bossRect = ((RectTransform)bossImage.transform).rect.size;

        Color theme = ResolveThemeColor();
        var list = new System.Collections.Generic.List<BossPoseResult>();
        for (int i = 0; i < poses.arraySize; i++)
        {
            var e = poses.GetArrayElementAtIndex(i);
            var sp = e.FindPropertyRelative("sprite").objectReferenceValue as Sprite;
            if (sp == null) continue;
            float scale = e.FindPropertyRelative("scale").floatValue;
            Vector2 offset = e.FindPropertyRelative("positionOffset").vector2Value;

            Texture2D src = LoadReadable(sp.texture);
            int w = src.width, h = src.height, pad = outerGlowWidth;
            string outPath = string.Format(BossOutPathFormat, list.Count + 1);
            WritePng(outPath, BuildBoss(src, theme), BossImporterRefPath);
            Object.DestroyImmediate(src);

            // 画像枠（下端中央が基準・縦横比を保って枠内に収まる）で、プレイヤー側と同じ見た目の大きさ・足元の高さにする
            float fd = Mathf.Min(dancerRect.x / w, dancerRect.y / h);
            float wp = w + pad * 2f, hp = h + pad * 2f;
            float fb = Mathf.Min(bossRect.x / wp, bossRect.y / hp);
            float bossScale = scale * fd / fb;
            float dancerFeetY = offset.y + scale * (dancerRect.y - h * fd) * 0.5f;
            float bossOffsetY = dancerFeetY - bossScale * ((bossRect.y - hp * fb) * 0.5f + pad * fb);

            list.Add(new BossPoseResult
            {
                sprite = AssetDatabase.LoadAssetAtPath<Sprite>(outPath),
                scale = bossScale,
                positionOffset = new Vector2(-offset.x, bossOffsetY),
            });
            Debug.Log($"[Area10VsImageSettings] {outPath} ← {sp.name}（scale={bossScale:F3}, offset=({-offset.x:F1}, {bossOffsetY:F1})）");
        }
        WritePng(NamePlateOutPath, BuildNamePlate(LoadReadable(sourceNamePlate), theme), NamePlateImporterRefPath);
        results = list.ToArray();
        return results.Length > 0;
    }

    // 元画像はインポート設定（Read/Write）を変えずに、PNGファイルを直接読み込む
    private static Texture2D LoadReadable(Texture2D src)
    {
        string path = AssetDatabase.GetAssetPath(src);
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(File.ReadAllBytes(path));
        return tex;
    }

    private Texture2D BuildBoss(Texture2D src, Color theme)
    {
        int sw = src.width, sh = src.height;
        int pad = outerGlowWidth;
        int w = sw + pad * 2, h = sh + pad * 2;
        Color[] s = src.GetPixels();

        // 左右反転＋余白付きのアルファマスク
        var col = new Color[w * h];
        var mask = new float[w * h];
        for (int y = 0; y < sh; y++)
            for (int x = 0; x < sw; x++)
            {
                Color c = s[y * sw + (sw - 1 - x)];
                int i = (y + pad) * w + (x + pad);
                col[i] = c;
                mask[i] = c.a;
            }

        float[] rimBlur  = BoxBlur(mask, w, h, rimWidth, 2);
        float[] glowBlur = pad > 0 ? BoxBlur(mask, w, h, Mathf.Max(1, pad / 2), 3) : new float[w * h];

        var outPx = new Color[w * h];
        for (int i = 0; i < outPx.Length; i++)
        {
            float a = mask[i];
            // 外側の光（テーマ色）
            float glowA = Mathf.Clamp01(glowBlur[i] * 2f) * outerGlowAlpha;
            Color glow = new Color(theme.r, theme.g, theme.b, glowA);

            if (a <= 0.001f) { outPx[i] = glow; continue; }

            Color c = col[i];
            float lum = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
            // 体：影の色に元の明暗を乗せ、元の色を少しだけ残す
            Color body = shadowColor * (0.45f + bodyShading * lum);
            body = Color.Lerp(body, c * lum, keepOriginalColor);
            // 明るい部分をテーマ色で光らせる
            float hl = EdgeStep(0.55f, 1f, lum) * highlightGlow;
            body += theme * hl;
            // 内側の縁取り（輪郭に近いほど強い）
            float rim = Mathf.Clamp01((1f - rimBlur[i]) * 2.2f) * rimStrength;
            body = Color.Lerp(body, theme * 1.15f, rim);
            body.a = a;

            // 外側の光の上に重ねる（通常のアルファ合成）
            float outA = body.a + glow.a * (1f - body.a);
            Color rgb = outA > 0f ? (body * body.a + glow * glow.a * (1f - body.a)) / outA : Color.clear;
            outPx[i] = new Color(Mathf.Clamp01(rgb.r), Mathf.Clamp01(rgb.g), Mathf.Clamp01(rgb.b), outA);
        }

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.SetPixels(outPx);
        tex.Apply();
        return tex;
    }

    private Texture2D BuildNamePlate(Texture2D src, Color theme)
    {
        Color[] s = src.GetPixels();
        var outPx = new Color[s.Length];
        for (int i = 0; i < s.Length; i++)
        {
            Color c = s[i];
            if (c.a <= 0.001f) { outPx[i] = Color.clear; continue; }
            // 元の明暗を保ったまま1色のネオン管に塗り替え、明るい芯は白く飛ばす
            float lum = Mathf.Max(c.r, Mathf.Max(c.g, c.b)) * 0.5f + (c.r * 0.299f + c.g * 0.587f + c.b * 0.114f) * 0.5f;
            Color tube = theme * (lum * namePlateGain);
            float core = EdgeStep(namePlateCoreStart, 1f, lum);
            Color rgb = Color.Lerp(tube, Color.white, core);
            outPx[i] = new Color(Mathf.Clamp01(rgb.r), Mathf.Clamp01(rgb.g), Mathf.Clamp01(rgb.b), c.a);
        }
        var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false);
        tex.SetPixels(outPx);
        tex.Apply();
        return tex;
    }

    // xがedge0以下なら0、edge1以上なら1、その間はなめらかに0→1（シェーダーのsmoothstepと同じ）。
    // ★Mathf.SmoothStep(from, to, t)は「fromとtoの間を補間した値」を返す別物のため使わない
    private static float EdgeStep(float edge0, float edge1, float x)
    {
        float t = Mathf.InverseLerp(edge0, edge1, x);
        return t * t * (3f - 2f * t);
    }

    // 分離型の箱ぼかし（passes回くり返してガウスぼかしに近づける）
    private static float[] BoxBlur(float[] src, int w, int h, int radius, int passes)
    {
        float[] a = (float[])src.Clone();
        float[] b = new float[a.Length];
        float inv = 1f / (radius * 2 + 1);
        for (int p = 0; p < passes; p++)
        {
            for (int y = 0; y < h; y++)
            {
                float sum = 0f;
                for (int k = -radius; k <= radius; k++) sum += a[y * w + Mathf.Clamp(k, 0, w - 1)];
                for (int x = 0; x < w; x++)
                {
                    b[y * w + x] = sum * inv;
                    sum += a[y * w + Mathf.Min(x + radius + 1, w - 1)] - a[y * w + Mathf.Max(x - radius, 0)];
                }
            }
            for (int x = 0; x < w; x++)
            {
                float sum = 0f;
                for (int k = -radius; k <= radius; k++) sum += b[Mathf.Clamp(k, 0, h - 1) * w + x];
                for (int y = 0; y < h; y++)
                {
                    a[y * w + x] = sum * inv;
                    sum += b[Mathf.Min(y + radius + 1, h - 1) * w + x] - b[Mathf.Max(y - radius, 0) * w + x];
                }
            }
        }
        return a;
    }

    // 既存のVS画像と同じインポート設定でSpriteとして保存する
    private static void WritePng(string path, Texture2D tex, string importerRefPath)
    {
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        var refImporter = AssetImporter.GetAtPath(importerRefPath) as TextureImporter;
        if (refImporter != null)
        {
            var settings = new TextureImporterSettings();
            refImporter.ReadTextureSettings(settings);
            importer.SetTextureSettings(settings);
            importer.textureCompression = refImporter.textureCompression;
            importer.maxTextureSize = refImporter.maxTextureSize;
        }
        else
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
        }
        importer.SaveAndReimport();
    }
}

[CustomEditor(typeof(Area10VsImageSettings))]
public class Area10VsImageSettingsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var settings = (Area10VsImageSettings)target;

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "05_Gameシーンを開いた状態で押してください。\n" +
            $"出力：VS_Boss10_1〜N.png（VsIntroUIのDancer Posesと同じ枚数）、{Area10VsImageSettings.NamePlateOutPath}\n" +
            "生成後、シーンのArea10FinalIntroController > Vs Boss Poses（画像・Scale・Position Offset）と、" +
            "Area10ConfigのVs Boss Sprite（予備：1枚目）/ Vs Boss Name Spriteに設定します。シーンは保存してください。",
            MessageType.Info);

        if (GUILayout.Button("画像を生成してArea10Configに設定", GUILayout.Height(30)))
        {
            if (!settings.GenerateAll(out var results)) return;

            // シーンのArea10FinalIntroController > Vs Boss Poses
            var intro = Object.FindFirstObjectByType<Area10FinalIntroController>(FindObjectsInactive.Include);
            if (intro != null)
            {
                var iso = new SerializedObject(intro);
                var arr = iso.FindProperty("vsBossPoses");
                arr.arraySize = results.Length;
                for (int i = 0; i < results.Length; i++)
                {
                    var e = arr.GetArrayElementAtIndex(i);
                    e.FindPropertyRelative("sprite").objectReferenceValue = results[i].sprite;
                    e.FindPropertyRelative("scale").floatValue = results[i].scale;
                    e.FindPropertyRelative("positionOffset").vector2Value = results[i].positionOffset;
                }
                iso.ApplyModifiedProperties();
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(intro.gameObject.scene);
            }
            else Debug.LogWarning("[Area10VsImageSettings] シーンにArea10FinalIntroControllerがありません（先に「Final Stage開始演出をシーンに配置」を実行）");

            // Area10Config（予備：Vs Boss Posesが空の時に使う1枚目と、ネームプレート）
            var cfg = AssetDatabase.LoadAssetAtPath<AreaConfig>(Area10VsImageSettings.Area10ConfigPath);
            if (cfg != null)
            {
                var so = new SerializedObject(cfg);
                so.FindProperty("vsBossSprite").objectReferenceValue = results[0].sprite;
                so.FindProperty("vsBossScale").floatValue = results[0].scale;
                so.FindProperty("vsBossPositionOffset").vector2Value = results[0].positionOffset;
                so.FindProperty("vsBossNameSprite").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>(Area10VsImageSettings.NamePlateOutPath);
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(cfg);
                AssetDatabase.SaveAssets();
            }
            else Debug.LogWarning($"[Area10VsImageSettings] {Area10VsImageSettings.Area10ConfigPath} が見つかりません");

            Debug.Log("[Area10VsImageSettings] VS画像を生成しました");
        }
    }
}
