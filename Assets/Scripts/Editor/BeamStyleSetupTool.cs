using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ビームの見た目（BeamStyle / BeamStyleFX）のセットアップメニュー。
/// 1 作成・設定：画像・マテリアル・パーティクル・スタイル（Dragon/Obelisk/Bit）を作り、
///   EnemyData_Dragon / EnemyData_Obelisk のビームの弾と、Bit.prefabのBitControllerのビームにスタイルを設定する。Bitには照準リングも付ける。
///   スタイルのアセットが既にある場合は作り直さない（Inspectorで調整した値を残す）。
/// 2 元に戻す：スタイルの設定を外し、Bitの照準リングを削除する（従来の見た目に戻る）。
/// </summary>
public static class BeamStyleSetupTool
{
    private const string GenDir      = "Assets/Generated/VFX";
    private const string StyleDir    = "Assets/GameData/BeamStyles";
    private const string MatSrc      = "Assets/Art/Background/Mat_StardustParticle.mat"; // URP Particles/Unlit・加算
    private const string SoftGlow    = "Assets/Generated/UI/SoftGlowCircle.png";
    private const string SpriteAdd   = "Assets/Art/Enemy/S3_10_NeonDancer/ND_WormholeAdditive.mat"; // スプライト用の加算
    private const string TexSoft     = GenDir + "/BeamSoft.png";
    private const string TexFlow     = GenDir + "/BeamFlow.png";
    private const string TexHaze     = GenDir + "/BeamHaze.png";
    private const string TexFlare    = GenDir + "/BeamFlare.png";
    private const string TexRing     = GenDir + "/BeamAimRing.png";
    private const string MatSoft     = GenDir + "/Mat_Beam_Soft.mat";
    private const string MatFlow     = GenDir + "/Mat_Beam_Flow.mat";
    private const string MatHaze     = GenDir + "/Mat_Beam_Haze.mat";
    private const string MatDot      = GenDir + "/Mat_Beam_Dot.mat";
    private const string PfImpact    = "Assets/Prefabs/Effects/BeamFX_ImpactSparks.prefab";
    private const string PfEmbers    = "Assets/Prefabs/Effects/BeamFX_Embers.prefab";
    private const string DragonData  = "Assets/GameData/Enemies/EnemyData_Dragon.asset";
    private const string ObeliskData = "Assets/GameData/Enemies/EnemyData_Obelisk.asset";
    private const string NeonDancerData = "Assets/GameData/Enemies/EnemyData_NeonDancer.asset"; // ⑦Beam・⑦P2 SweepBeamは元がObeliskのビーム
    private const string BitPrefab   = "Assets/Prefabs/Enemies/Bit.prefab";

    [MenuItem("Tools/ビームの見た目/1 Dragon・Obelisk・Bit・NeonDancerのビームを豪華にする（作成・設定）")]
    private static void Setup()
    {
        var softGlow = AssetDatabase.LoadAssetAtPath<Texture2D>(SoftGlow);
        var spriteAdd = AssetDatabase.LoadAssetAtPath<Material>(SpriteAdd);
        if (softGlow == null || spriteAdd == null || AssetDatabase.LoadMainAssetAtPath(MatSrc) == null)
        {
            EditorUtility.DisplayDialog("ビームの見た目", $"コピー元が見つかりません：\n{SoftGlow}\n{SpriteAdd}\n{MatSrc}", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("ビームの見た目：作成・設定",
                $"次を作成・設定します：\n・画像5つ・マテリアル4つ（{GenDir}）\n・パーティクル2つ（着弾点の火花・火の粉）\n" +
                $"・スタイル3つ（{StyleDir}/BeamStyle_Dragon / _Obelisk / _Bit。既にあれば作り直さない）\n" +
                "・EnemyData_Dragon / EnemyData_Obelisk / EnemyData_NeonDancer（Obeliskと同じ見た目）のビームの弾、Bit.prefab の BitController のビームに Beam Style を設定\n" +
                "・Bit.prefab に照準リング（AimRing）を追加\n当たり判定・ダメージは変わりません。続けますか？", "実行", "キャンセル"))
            return;

        EnsureFolder("Assets/Generated"); EnsureFolder(GenDir);
        EnsureFolder("Assets/GameData"); EnsureFolder(StyleDir);

        // 画像
        var texSoft = WriteTex(TexSoft, 64, 64, (u, v) => Mathf.Exp(-v * v / 0.18f), false, TextureWrapMode.Clamp);
        var texFlow = WriteTex(TexFlow, 256, 64, FlowPixel, false, TextureWrapMode.Repeat);
        var texHaze = WriteTex(TexHaze, 256, 64, HazePixel, false, TextureWrapMode.Repeat);
        WriteTex(TexFlare, 128, 128, FlarePixel, true, TextureWrapMode.Clamp);
        WriteTex(TexRing, 256, 256, RingPixel, true, TextureWrapMode.Clamp);
        var flareSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexFlare);
        var ringSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexRing);

        // マテリアル
        var matSoft = MakeMat(MatSoft, texSoft);
        var matFlow = MakeMat(MatFlow, texFlow);
        var matHaze = MakeMat(MatHaze, texHaze);
        var matDot = MakeMat(MatDot, softGlow);

        // パーティクル
        var impact = MakeParticlePrefab(PfImpact, matDot, ps =>
        {
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
            main.gravityModifier = 0.5f;
            var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = 0.05f;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.05f; r.lengthScale = 1.5f;
            Alpha(ps, new[] { 1f, 0f });
        });
        var embers = MakeParticlePrefab(PfEmbers, matDot, ps =>
        {
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.7f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.08f);
            main.gravityModifier = -0.25f;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.9f, 0.4f), 0f), new GradientColorKey(new Color(1f, 0.35f, 0.05f), 1f) },
                         new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            var sc = new ParticleSystem.MinMaxGradient(grad); sc.mode = ParticleSystemGradientMode.RandomColor;
            main.startColor = sc;
            var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = 0.1f;
            var noise = ps.noise; noise.enabled = true; noise.strength = new ParticleSystem.MinMaxCurve(0.5f); noise.frequency = 1.5f; noise.quality = ParticleSystemNoiseQuality.Low;
            Alpha(ps, new[] { 1f, 0.8f, 0f });
            var sol = ps.sizeOverLifetime; sol.enabled = true; sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));
        });

        // スタイル
        var dragon = MakeStyle("BeamStyle_Dragon", s =>
        {
            s.useHaze = true; s.hazeMaterial = matHaze; s.hazeColor = new Color(1f, 0.45f, 0.1f, 1f);
            s.embersPrefab = embers;
            s.useLightning = false;
            s.coreColor = new Color(1f, 0.97f, 0.85f, 1f);
            s.flowSpeed = 4f; s.wobbleAmplitude = 0.22f; s.wobbleFrequency = 20f;
        }, matSoft, matFlow, flareSprite, spriteAdd, impact);
        var obelisk = MakeStyle("BeamStyle_Obelisk", s =>
        {
            s.useLightning = true; s.boltCount = 3; s.boltColor = new Color(0.92f, 0.75f, 1f, 1f); s.boltJitter = 0.32f;
            s.flowSpeed = 7f; s.wobbleAmplitude = 0.12f;
        }, matSoft, matFlow, flareSprite, spriteAdd, impact);
        var bit = MakeStyle("BeamStyle_Bit", s =>
        {
            s.useLightning = true; s.boltCount = 1; s.boltColor = new Color(0.9f, 0.75f, 1f, 1f); s.boltJitter = 0.15f; s.boltWidth = 0.025f;
            s.flowSpeed = 8f; s.glowWidthMul = 3.6f; s.flareSizeMul = 8f; s.impactSparkRate = 20f;
        }, matSoft, matFlow, flareSprite, spriteAdd, impact);

        // 設定
        var log = new System.Text.StringBuilder();
        log.AppendLine(SetDataStyle(DragonData, dragon));
        log.AppendLine(SetDataStyle(ObeliskData, obelisk));
        log.AppendLine(SetDataStyle(NeonDancerData, obelisk));
        log.AppendLine(SetupBit(bit, ringSprite, spriteAdd));

        AssetDatabase.SaveAssets();
        Debug.Log("[BeamStyleSetupTool] ビームの見た目を設定しました\n" + log);
        EditorUtility.DisplayDialog("ビームの見た目", "設定しました。\n" + log, "OK");
    }

    [MenuItem("Tools/ビームの見た目/2 元の見た目に戻す")]
    private static void Revert()
    {
        if (!EditorUtility.DisplayDialog("ビームの見た目：元に戻す",
                "EnemyData_Dragon / EnemyData_Obelisk / EnemyData_NeonDancer / Bit.prefab のビームの Beam Style を外し、Bitの照準リングを削除します。\n" +
                "（作成した画像・マテリアル・スタイルのアセットは残します）続けますか？", "戻す", "キャンセル"))
            return;
        SetDataStyle(DragonData, null);
        SetDataStyle(ObeliskData, null);
        SetDataStyle(NeonDancerData, null);
        GameObject root = PrefabUtility.LoadPrefabContents(BitPrefab);
        try
        {
            var bc = root.GetComponentInChildren<BitController>(true);
            if (bc != null)
            {
                var so = new SerializedObject(bc);
                so.FindProperty("beamBulletType.beamStyle").objectReferenceValue = null;
                var ringProp = so.FindProperty("aimRing");
                var ring = ringProp.objectReferenceValue as SpriteRenderer;
                ringProp.objectReferenceValue = null;
                so.ApplyModifiedPropertiesWithoutUndo();
                if (ring != null) Object.DestroyImmediate(ring.gameObject);
            }
            PrefabUtility.SaveAsPrefabAsset(root, BitPrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("ビームの見た目", "元に戻しました。", "OK");
    }

    // ---------- 設定 ----------
    private static string SetDataStyle(string path, BeamStyle style)
    {
        var data = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
        if (data == null || data.bulletTypes == null) return $"{path}：見つかりません";
        Undo.RecordObject(data, "Beam Style");
        int count = 0;
        foreach (var bt in data.bulletTypes)
        {
            if (bt == null || !bt.useBeam) continue;
            bt.beamStyle = style;
            count++;
        }
        EditorUtility.SetDirty(data);
        return $"{data.name}：ビームの弾 {count}種に {(style != null ? style.name : "なし")}";
    }

    private static string SetupBit(BeamStyle style, Sprite ringSprite, Material spriteAdd)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(BitPrefab);
        try
        {
            var bc = root.GetComponentInChildren<BitController>(true);
            if (bc == null) return "Bit.prefab：BitControllerが見つかりません";
            var so = new SerializedObject(bc);
            so.FindProperty("beamBulletType.beamStyle").objectReferenceValue = style;

            var ringProp = so.FindProperty("aimRing");
            var ring = ringProp.objectReferenceValue as SpriteRenderer;
            if (ring == null)
            {
                var bodySr = bc.GetComponentInChildren<SpriteRenderer>(true);
                var go = new GameObject("AimRing");
                go.transform.SetParent(bc.transform, false);
                ring = go.AddComponent<SpriteRenderer>();
                ring.sprite = ringSprite;
                ring.sharedMaterial = spriteAdd;
                if (bodySr != null) { ring.sortingLayerID = bodySr.sortingLayerID; ring.sortingOrder = bodySr.sortingOrder + 2; }
                ring.enabled = false;
                ringProp.objectReferenceValue = ring;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, BitPrefab);
            return $"Bit.prefab：BitControllerのビームに {(style != null ? style.name : "なし")}、照準リング AimRing";
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static BeamStyle MakeStyle(string name, System.Action<BeamStyle> tweak, Material soft, Material flow, Sprite flare, Material flareMat, ParticleSystem impact)
    {
        string path = $"{StyleDir}/{name}.asset";
        var s = AssetDatabase.LoadAssetAtPath<BeamStyle>(path);
        if (s != null) return s; // 既にあれば作り直さない（Inspectorで調整した値を残す）
        s = ScriptableObject.CreateInstance<BeamStyle>();
        s.softLineMaterial = soft;
        s.flowMaterial = flow;
        s.flareSprite = flare;
        s.flareMaterial = flareMat;
        s.impactSparksPrefab = impact;
        tweak(s);
        AssetDatabase.CreateAsset(s, path);
        return s;
    }

    // ---------- パーティクル ----------
    private static ParticleSystem MakeParticlePrefab(string path, Material mat, System.Action<ParticleSystem> setup)
    {
        var existing = AssetDatabase.LoadAssetAtPath<ParticleSystem>(path);
        if (existing != null) return existing; // 既にあれば作り直さない
        var go = new GameObject(Path.GetFileNameWithoutExtension(path));
        try
        {
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 300;
            var em = ps.emission; em.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.sortingLayerName = "Default";
            r.sortingOrder = 16;
            setup(ps);
            PrefabUtility.SaveAsPrefabAsset(go, path);
        }
        finally { Object.DestroyImmediate(go); }
        return AssetDatabase.LoadAssetAtPath<ParticleSystem>(path);
    }

    private static void Alpha(ParticleSystem ps, float[] a)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        var ak = new GradientAlphaKey[a.Length];
        for (int i = 0; i < a.Length; i++) ak[i] = new GradientAlphaKey(a[i], a.Length == 1 ? 0f : i / (float)(a.Length - 1));
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, ak);
        col.color = new ParticleSystem.MinMaxGradient(g);
    }

    // ---------- マテリアル・画像 ----------
    private static Material MakeMat(string path, Texture2D tex)
    {
        if (AssetDatabase.LoadMainAssetAtPath(path) == null) AssetDatabase.CopyAsset(MatSrc, path);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private delegate float PixelAlpha(float u, float v); // u：0〜1（長さ方向）、v：-1〜1（太さ方向）

    private static Texture2D WriteTex(string path, int w, int h, PixelAlpha f, bool sprite, TextureWrapMode wrap)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w, v = (y + 0.5f) / h * 2f - 1f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(f(u, v))));
            }
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = sprite ? TextureImporterType.Sprite : TextureImporterType.Default;
        if (sprite)
        {
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = 100f;
            // Full Rect：Tight（画像の形に合わせた多角形）だと、ぼかした光の薄い外側が多角形の辺で切れて角ばって見える
            var ts = new TextureImporterSettings();
            imp.ReadTextureSettings(ts);
            ts.spriteMeshType = SpriteMeshType.FullRect;
            imp.SetTextureSettings(ts);
        }
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = false;
        imp.wrapMode = wrap;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // 流れる模様：長さ方向に明るい筋が並び（端でつながる）、太さ方向はぼかす
    private static float FlowPixel(float u, float v)
    {
        float across = Mathf.Exp(-v * v / 0.12f);
        float streak = 0.35f + 0.65f * Mathf.Pow(0.5f + 0.5f * Mathf.Sin(u * Mathf.PI * 2f * 3f), 3f);
        float fine = 0.85f + 0.15f * Mathf.Sin(u * Mathf.PI * 2f * 11f + v * 2f);
        return across * streak * fine;
    }

    // 陽炎：ゆらゆら揺れる帯（長さ方向に端でつながる）
    private static float HazePixel(float u, float v)
    {
        float wave = 0.25f * Mathf.Sin(u * Mathf.PI * 2f * 2f) + 0.12f * Mathf.Sin(u * Mathf.PI * 2f * 5f + 1.3f);
        float d = v - wave;
        float band = Mathf.Exp(-d * d / 0.25f);
        float flicker = 0.6f + 0.4f * Mathf.Sin(u * Mathf.PI * 2f * 4f + v * 3f);
        return band * flicker * 0.9f;
    }

    // フレア：柔らかい光の玉＋十字の光芒（u,v：0〜1 / -1〜1 を中心からの座標にする）
    private static float FlarePixel(float u, float v)
    {
        float x = u * 2f - 1f, y = v;
        float r = Mathf.Sqrt(x * x + y * y);
        float glow = Mathf.Exp(-r * r / 0.08f) + Mathf.Exp(-r * r / 0.4f) * 0.35f;
        float rays = (Mathf.Exp(-y * y / 0.002f) * Mathf.Clamp01(1f - Mathf.Abs(x)) + Mathf.Exp(-x * x / 0.002f) * Mathf.Clamp01(1f - Mathf.Abs(y))) * 0.6f;
        return (glow + rays) * Mathf.Clamp01((1f - r) / 0.15f + 0.3f);
    }

    // 照準リング：細い輪＋4か所の目盛り
    private static float RingPixel(float u, float v)
    {
        float x = u * 2f - 1f, y = v;
        float r = Mathf.Sqrt(x * x + y * y);
        float ring = Mathf.Exp(-Mathf.Pow((r - 0.8f) / 0.035f, 2f));
        float ang = Mathf.Atan2(y, x);
        float tick = Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 2f)), 40f) * Mathf.Exp(-Mathf.Pow((r - 0.66f) / 0.07f, 2f));
        return (ring + tick) * Mathf.Clamp01((1f - r) / 0.05f);
    }
}
