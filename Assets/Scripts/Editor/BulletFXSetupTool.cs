using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 未反射弾の演出（BulletFXManager）のセットアップメニュー。
/// 1 作成：画像・マテリアル・共有パーティクルと Assets/Resources/BulletFX.prefab を作る（既にあれば作り直さない）。
///   プレハブがあると、撃たれた弾が自動で登録されて演出が付く。
/// 2 ON/OFF：BulletFX.prefab の Fx Enabled を切り替える（OFFで従来の見た目）。
/// </summary>
public static class BulletFXSetupTool
{
    private const string GenDir     = "Assets/Generated/VFX";
    private const string MatSrc     = "Assets/Art/Background/Mat_StardustParticle.mat"; // URP Particles/Unlit・加算
    private const string SoftGlow   = "Assets/Generated/UI/SoftGlowCircle.png";
    private const string SpriteAdd  = "Assets/Art/Enemy/S3_10_NeonDancer/ND_WormholeAdditive.mat"; // スプライト用の加算
    private const string TexGlow    = GenDir + "/BulletFX_Glow.png";
    private const string TexRing    = GenDir + "/BulletFX_Ring.png";
    private const string TexLine    = GenDir + "/BulletFX_Line.png";
    private const string MatDot     = GenDir + "/Mat_BulletFX_Dot.mat";
    private const string MatSmoke   = GenDir + "/Mat_BulletFX_Smoke.mat";
    private const string MatLine    = GenDir + "/Mat_BulletFX_Line.mat";
    private const string PrefabPath = "Assets/Resources/BulletFX.prefab";

    [MenuItem("Tools/弾の見た目/1 未反射弾を派手にする（作成）")]
    private static void Create()
    {
        var softGlow = AssetDatabase.LoadAssetAtPath<Texture2D>(SoftGlow);
        var spriteAdd = AssetDatabase.LoadAssetAtPath<Material>(SpriteAdd);
        if (softGlow == null || spriteAdd == null || AssetDatabase.LoadMainAssetAtPath(MatSrc) == null)
        {
            EditorUtility.DisplayDialog("弾の見た目", $"コピー元が見つかりません：\n{SoftGlow}\n{SpriteAdd}\n{MatSrc}", "OK");
            return;
        }
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
        if (exists)
        {
            EditorUtility.DisplayDialog("弾の見た目", $"{PrefabPath} は既にあります（作り直しません）。\n設定はこのプレハブのInspectorで調整してください。", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("弾の見た目：作成",
                $"次を作成します：\n・画像3つ・マテリアル3つ（{GenDir}）\n・{PrefabPath}（管理役と共有パーティクル2つ）\n" +
                "作成後、撃たれた未反射弾（ビーム・ドリル以外）に自動で演出が付きます。弾の動き・当たり判定は変わりません。続けますか？", "作成", "キャンセル"))
            return;

        if (!AssetDatabase.IsValidFolder("Assets/Generated")) AssetDatabase.CreateFolder("Assets", "Generated");
        if (!AssetDatabase.IsValidFolder(GenDir)) AssetDatabase.CreateFolder("Assets/Generated", "VFX");
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");

        WriteTex(TexGlow, 128, 128, (x, y) => { float r2 = x * x + y * y; return Mathf.Exp(-r2 / 0.18f) * Mathf.Clamp01((1f - Mathf.Sqrt(r2)) / 0.15f); }, true);
        WriteTex(TexRing, 128, 128, (x, y) => { float r = Mathf.Sqrt(x * x + y * y); return (Mathf.Exp(-Mathf.Pow((r - 0.8f) / 0.06f, 2f)) + Mathf.Exp(-Mathf.Pow((r - 0.78f) / 0.18f, 2f)) * 0.3f) * Mathf.Clamp01((1f - r) / 0.06f); }, true);
        var lineTex = WriteTex(TexLine, 32, 32, (x, y) => Mathf.Exp(-y * y / 0.15f), false);

        var matDot = MakeMat(MatDot, softGlow, true);
        var matSmoke = MakeMat(MatSmoke, softGlow, false);
        var matLine = MakeMat(MatLine, lineTex, true);

        var root = new GameObject("BulletFX");
        try
        {
            var m = root.AddComponent<BulletFXManager>();
            m.sparkle = Layer(root, "Sparkle", matDot, 15, ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.1f);
                Alpha(ps, new[] { 1f, 0f });
                Size(ps, 1f, 0.3f);
            });
            m.smoke = Layer(root, "Smoke", matSmoke, 4, ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.24f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                var shape = ps.shape; shape.enabled = true; shape.shapeType = ParticleSystemShapeType.Circle; shape.radius = 0.02f;
                Alpha(ps, new[] { 0f, 1f, 0f });
                Size(ps, 0.6f, 1.6f);
            });
            m.glowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexGlow);
            m.ringSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexRing);
            m.additiveSpriteMaterial = spriteAdd;
            m.lineMaterial = matLine;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { Object.DestroyImmediate(root); }
        AssetDatabase.SaveAssets();
        Debug.Log($"[BulletFXSetupTool] {PrefabPath} を作成しました");
        EditorUtility.DisplayDialog("弾の見た目", $"作成しました。\n{PrefabPath}\n全体のON/OFFや各弾種の演出は、このプレハブのBulletFXManagerで調整できます。", "OK");
    }

    [MenuItem("Tools/弾の見た目/2 演出のON・OFFを切り替える")]
    private static void Toggle()
    {
        var m = AssetDatabase.LoadAssetAtPath<BulletFXManager>(PrefabPath);
        if (m == null) { EditorUtility.DisplayDialog("弾の見た目", $"{PrefabPath} がありません（先に「1 作成」を実行してください）。", "OK"); return; }
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        bool now;
        try
        {
            var mm = root.GetComponent<BulletFXManager>();
            mm.fxEnabled = !mm.fxEnabled;
            now = mm.fxEnabled;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("弾の見た目", now ? "演出をONにしました。" : "演出をOFFにしました（従来の見た目）。", "OK");
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
        main.maxParticles = 500;
        main.startColor = Color.white;
        var em = ps.emission; em.enabled = false;
        var shape0 = ps.shape; shape0.enabled = false;
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

    private static Material MakeMat(string path, Texture2D tex, bool additive)
    {
        if (AssetDatabase.LoadMainAssetAtPath(path) == null) AssetDatabase.CopyAsset(MatSrc, path);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        // URP Particles/Unlit：_Blend 0=アルファ 2=加算
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", additive ? 2f : 0f);
        mat.SetFloat("_SrcBlend", 5f);
        mat.SetFloat("_DstBlend", additive ? 1f : 10f);
        if (mat.HasProperty("_SrcBlendAlpha")) mat.SetFloat("_SrcBlendAlpha", 1f);
        if (mat.HasProperty("_DstBlendAlpha")) mat.SetFloat("_DstBlendAlpha", additive ? 1f : 10f);
        mat.SetFloat("_ZWrite", 0f);
        mat.renderQueue = 3000;
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
        if (sprite) { imp.spriteImportMode = SpriteImportMode.Single; imp.spritePixelsPerUnit = 100f; }
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = false;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
