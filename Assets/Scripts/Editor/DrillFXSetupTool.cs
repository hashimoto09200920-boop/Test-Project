using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ドリル弾の演出（DrillFXManager / DrillFX）のセットアップメニュー。
/// 1 作成・設定：画像・マテリアル・DrillFX.prefab（共有の管理役と3つのパーティクル）を作り、
///   Tsukuyomi.prefab の TsukuyomiController と NeonDancer.prefab の NeonDancerController の Drill Fx Prefab に設定する。
///   DrillFX.prefabが既にある場合は作り直さない（Inspectorで調整した値を残す）。
/// 2 元に戻す：両コントローラーの Drill Fx Prefab を空にする（従来の見た目に戻る）。
/// </summary>
public static class DrillFXSetupTool
{
    private const string GenDir       = "Assets/Generated/VFX";
    private const string MatSrc       = "Assets/Art/Background/Mat_StardustParticle.mat"; // URP Particles/Unlit・加算
    private const string SoftGlow     = "Assets/Generated/UI/SoftGlowCircle.png";
    private const string SpriteAdd    = "Assets/Art/Enemy/S3_10_NeonDancer/ND_WormholeAdditive.mat"; // スプライト用の加算
    private const string TexRing      = GenDir + "/DrillRing.png";
    private const string TexGlow      = GenDir + "/DrillGlow.png";
    private const string TexSigil     = GenDir + "/DrillSigil.png";
    private const string TexShard     = GenDir + "/DrillShard.png";
    private const string TexBolt      = GenDir + "/DrillBoltLine.png";
    private const string MatDot       = GenDir + "/Mat_Drill_Dot.mat";
    private const string MatShard     = GenDir + "/Mat_Drill_Shard.mat";
    private const string MatBolt      = GenDir + "/Mat_Drill_Bolt.mat";
    private const string PrefabPath   = "Assets/Prefabs/Effects/DrillFX.prefab";
    private const string TsukuyomiPrefab  = "Assets/Prefabs/Enemies/Tsukuyomi.prefab";
    private const string NeonDancerPrefab = "Assets/Prefabs/Enemies/NeonDancer.prefab";

    [MenuItem("Tools/ドリルの見た目/1 Tsukuyomi・NeonDancerのドリルを派手にする（作成・設定）")]
    private static void Setup()
    {
        var softGlow = AssetDatabase.LoadAssetAtPath<Texture2D>(SoftGlow);
        var spriteAdd = AssetDatabase.LoadAssetAtPath<Material>(SpriteAdd);
        if (softGlow == null || spriteAdd == null || AssetDatabase.LoadMainAssetAtPath(MatSrc) == null)
        {
            EditorUtility.DisplayDialog("ドリルの見た目", $"コピー元が見つかりません：\n{SoftGlow}\n{SpriteAdd}\n{MatSrc}", "OK");
            return;
        }
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
        if (!EditorUtility.DisplayDialog("ドリルの見た目：作成・設定",
                $"次を作成・設定します：\n・画像5つ・マテリアル3つ（{GenDir}）\n" +
                $"・{PrefabPath}（共有の管理役とパーティクル3つ）{(exists ? "※既にあるので作り直さない" : "")}\n" +
                "・Tsukuyomi.prefab の TsukuyomiController、NeonDancer.prefab の NeonDancerController の Drill Fx Prefab に設定\n" +
                "ドリルの動き・当たり判定は変わりません。続けますか？", "実行", "キャンセル"))
            return;

        if (!AssetDatabase.IsValidFolder("Assets/Generated")) AssetDatabase.CreateFolder("Assets", "Generated");
        if (!AssetDatabase.IsValidFolder(GenDir)) AssetDatabase.CreateFolder("Assets/Generated", "VFX");

        WriteTex(TexRing, 128, 128, RingPixel, true);
        WriteTex(TexGlow, 128, 128, GlowPixel, true);
        WriteTex(TexSigil, 256, 256, SigilPixel, true);
        var shardTex = WriteTex(TexShard, 32, 64, ShardPixel, false);
        var boltTex = WriteTex(TexBolt, 32, 32, (x, y) => Mathf.Exp(-y * y / 0.15f), false);
        var matDot = MakeMat(MatDot, softGlow);
        var matShard = MakeMat(MatShard, shardTex);
        var matBolt = MakeMat(MatBolt, boltTex);

        if (!exists) BuildPrefab(matDot, matShard, matBolt, spriteAdd);
        var mgr = AssetDatabase.LoadAssetAtPath<DrillFXManager>(PrefabPath);

        string log = SetField(TsukuyomiPrefab, typeof(TsukuyomiController), mgr) + "\n" + SetField(NeonDancerPrefab, typeof(NeonDancerController), mgr);
        AssetDatabase.SaveAssets();
        Debug.Log("[DrillFXSetupTool] ドリルの見た目を設定しました\n" + log);
        EditorUtility.DisplayDialog("ドリルの見た目", "設定しました。\n" + log, "OK");
    }

    [MenuItem("Tools/ドリルの見た目/2 元の見た目に戻す")]
    private static void Revert()
    {
        if (!EditorUtility.DisplayDialog("ドリルの見た目：元に戻す",
                "Tsukuyomi / NeonDancer の Drill Fx Prefab を空にします（作成したアセットは残します）。続けますか？", "戻す", "キャンセル"))
            return;
        string log = SetField(TsukuyomiPrefab, typeof(TsukuyomiController), null) + "\n" + SetField(NeonDancerPrefab, typeof(NeonDancerController), null);
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("ドリルの見た目", "元に戻しました。\n" + log, "OK");
    }

    private static string SetField(string prefabPath, System.Type type, DrillFXManager value)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
        try
        {
            var comp = root.GetComponentInChildren(type, true);
            if (comp == null) return $"{Path.GetFileName(prefabPath)}：{type.Name}が見つかりません";
            var so = new SerializedObject(comp);
            so.FindProperty("drillFxPrefab").objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            return $"{Path.GetFileName(prefabPath)} > {type.Name} > Drill Fx Prefab：{(value != null ? value.name : "なし")}";
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    private static void BuildPrefab(Material dot, Material shard, Material bolt, Material spriteAdd)
    {
        var root = new GameObject("DrillFX");
        try
        {
            var mgr = root.AddComponent<DrillFXManager>();
            mgr.dust = Layer(root, "Dust", dot, 14, ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
                var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = 0.05f;
                Alpha(ps, new[] { 0f, 1f, 0f });
                Size(ps, 1f, 0.3f);
            });
            mgr.sparks = Layer(root, "Sparks", dot, 16, ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
                main.gravityModifier = 0.6f;
                var shape = ps.shape; shape.enabled = false;
                var r = ps.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.05f; r.lengthScale = 1.4f;
                Alpha(ps, new[] { 1f, 0f });
            });
            mgr.shards = Layer(root, "Shards", shard, 16, ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
                main.gravityModifier = 1.2f;
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                var rot = ps.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-12f, 12f);
                var shape = ps.shape; shape.enabled = false;
                Alpha(ps, new[] { 1f, 1f, 0f });
            });
            mgr.ringSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexRing);
            mgr.glowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexGlow);
            mgr.sigilSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexSigil);
            mgr.spriteMaterial = spriteAdd;
            mgr.boltMaterial = bolt;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static ParticleSystem Layer(GameObject root, string name, Material mat, int order, System.Action<ParticleSystem> setup)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 400;
        main.startColor = Color.white;
        var em = ps.emission; em.enabled = false;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.sortingLayerName = "Default";
        r.sortingOrder = order;
        setup(ps);
        return ps;
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

    private static void Size(ParticleSystem ps, float from, float to)
    {
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, from, 1f, to));
    }

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

    private delegate float PixelAlpha(float x, float y); // x,y：-1〜1

    private static Texture2D WriteTex(string path, int w, int h, PixelAlpha f, bool sprite)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int py = 0; py < h; py++)
            for (int px = 0; px < w; px++)
            {
                float x = (px + 0.5f) / w * 2f - 1f, y = (py + 0.5f) / h * 2f - 1f;
                tex.SetPixel(px, py, new Color(1f, 1f, 1f, Mathf.Clamp01(f(x, y))));
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
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // 螺旋の輪・ヒットの輪：細い輪
    private static float RingPixel(float x, float y)
    {
        float r = Mathf.Sqrt(x * x + y * y);
        return (Mathf.Exp(-Mathf.Pow((r - 0.8f) / 0.06f, 2f)) + Mathf.Exp(-Mathf.Pow((r - 0.78f) / 0.18f, 2f)) * 0.3f) * Mathf.Clamp01((1f - r) / 0.06f);
    }

    // 輝き・オーラ：柔らかい光
    private static float GlowPixel(float x, float y)
    {
        float r2 = x * x + y * y;
        return (Mathf.Exp(-r2 / 0.12f) * 0.8f + Mathf.Exp(-r2 / 0.45f) * 0.35f) * Mathf.Clamp01((1f - Mathf.Sqrt(r2)) / 0.1f);
    }

    // 紋章：二重の輪＋三日月＋8つの目盛り（Tsukuyomiの月の意匠）
    private static float SigilPixel(float x, float y)
    {
        float r = Mathf.Sqrt(x * x + y * y);
        float rings = Mathf.Exp(-Mathf.Pow((r - 0.9f) / 0.025f, 2f)) + Mathf.Exp(-Mathf.Pow((r - 0.72f) / 0.02f, 2f)) * 0.8f;
        float ang = Mathf.Atan2(y, x);
        float ticks = Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 4f)), 60f) * (r > 0.74f && r < 0.88f ? 1f : 0f);
        // 三日月：大きい円から少しずらした円を引く
        float moonA = Mathf.Clamp01((0.42f - r) / 0.03f);
        float r2 = Mathf.Sqrt((x - 0.16f) * (x - 0.16f) + (y - 0.08f) * (y - 0.08f));
        float moonB = Mathf.Clamp01((0.38f - r2) / 0.03f);
        float moon = Mathf.Clamp01(moonA - moonB) * 0.9f;
        return (rings + ticks + moon) * Mathf.Clamp01((1f - r) / 0.04f);
    }

    // 破片：細長いひし形（結晶のかけら）
    private static float ShardPixel(float x, float y)
    {
        float d = Mathf.Abs(x) / 0.9f + Mathf.Abs(y);
        return Mathf.Clamp01((1f - d) / 0.12f);
    }
}
