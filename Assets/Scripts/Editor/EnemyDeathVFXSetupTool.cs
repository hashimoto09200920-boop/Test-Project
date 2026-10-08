using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 新しい撃破エフェクト（VFX_EnemyDeath / EnemyDeathVFX）のセットアップメニュー。
/// 1 作成：画像（リング・破片）を生成し、マテリアルとプレハブを作る
/// 2 切り替え：全EnemyDataのDeath Effect Prefab Overrideと、エネミーPrefabのEnemyStats（従来のエフェクトを使っているもの）を新しいエフェクトにし、
///   色テーマとボスの連鎖爆発を設定する
/// 3 元に戻す：従来のVFX_Explosion_A_RingSparksに戻す（色テーマ・ボス連鎖の設定値は残るが、従来のエフェクトでは使われない）
/// テスト再生：Play中に画面に並べて再生する
/// </summary>
public static class EnemyDeathVFXSetupTool
{
    private const string PrefabPath   = "Assets/Prefabs/Effects/VFX_EnemyDeath.prefab";
    private const string OldPrefab    = "Assets/Prefabs/Effects/VFX_Explosion_A_RingSparks.prefab";
    private const string GenDir       = "Assets/Generated/VFX";
    private const string RingTexPath  = GenDir + "/DeathRing.png";
    private const string SquareTexPath = GenDir + "/DeathSquare.png";
    private const string SoftGlowPath = "Assets/Generated/UI/SoftGlowCircle.png";
    private const string MatSrc       = "Assets/Art/Background/Mat_StardustParticle.mat"; // URP Particles/Unlit・加算（_Blend=2）
    private const string MatGlowAdd   = GenDir + "/Mat_Death_GlowAdd.mat";
    private const string MatSmoke     = GenDir + "/Mat_Death_SmokeAlpha.mat";
    private const string MatRingAdd   = GenDir + "/Mat_Death_RingAdd.mat";
    private const string MatDebris    = GenDir + "/Mat_Death_DebrisAlpha.mat";
    private const string EnemyDataDir = "Assets/GameData/Enemies";
    private const string EnemyPrefabDir = "Assets/Prefabs/Enemies";

    // ---------- 色テーマ（EnemyDataの名前 → テーマ） ----------
    private static readonly Dictionary<string, DeathVfxTheme> ThemeTable = new Dictionary<string, DeathVfxTheme>
    {
        // ① 炎
        { "Jaguar", DeathVfxTheme.Fire }, { "PuppetHead", DeathVfxTheme.Fire }, { "Mask", DeathVfxTheme.Fire }, { "Cactus", DeathVfxTheme.Fire },
        { "Camel", DeathVfxTheme.Fire }, { "GyroWard", DeathVfxTheme.Fire }, { "Gyrorb", DeathVfxTheme.Fire }, { "Shaman", DeathVfxTheme.Fire },
        { "TestBeam", DeathVfxTheme.Fire },
        // ② 紅蓮
        { "NeonMonster04", DeathVfxTheme.Crimson }, { "Bat", DeathVfxTheme.Crimson }, { "Bear", DeathVfxTheme.Crimson }, { "Cauldron", DeathVfxTheme.Crimson },
        { "GravePole", DeathVfxTheme.Crimson }, { "Balloon", DeathVfxTheme.Crimson }, { "Dragon", DeathVfxTheme.Crimson }, { "Fingers", DeathVfxTheme.Crimson },
        { "Doll_A", DeathVfxTheme.Crimson }, { "Doll_B", DeathVfxTheme.Crimson },
        // ③ ネオン水色
        { "BoneFish", DeathVfxTheme.NeonCyan }, { "Turtle", DeathVfxTheme.NeonCyan }, { "NeonMonster03", DeathVfxTheme.NeonCyan }, { "Toucan", DeathVfxTheme.NeonCyan },
        { "Drone", DeathVfxTheme.NeonCyan }, { "Condor", DeathVfxTheme.NeonCyan }, { "Zephyr", DeathVfxTheme.NeonCyan }, { "Obelisk", DeathVfxTheme.NeonCyan },
        // ④ ネオン紫
        { "Minimo", DeathVfxTheme.NeonPurple }, { "NeonMonster02", DeathVfxTheme.NeonPurple }, { "Ghost", DeathVfxTheme.NeonPurple }, { "NeonMonster01", DeathVfxTheme.NeonPurple },
        // ⑤ ネオン虹（NeonDancerだけ）
        { "NeonDancer", DeathVfxTheme.NeonRainbow },
        // ⑥ 毒
        { "Slime", DeathVfxTheme.Toxic },
        // ⑦ 機械
        { "Fortress", DeathVfxTheme.Mech }, { "WalkerMech", DeathVfxTheme.Mech }, { "IronNest", DeathVfxTheme.Mech },
        { "IronNest_NM01", DeathVfxTheme.Mech }, { "IronNest_NM02", DeathVfxTheme.Mech }, { "IronNest_NM03", DeathVfxTheme.Mech }, { "Marshal", DeathVfxTheme.Mech },
        // ⑧ 遺跡
        { "Golem", DeathVfxTheme.Ruins }, { "GuardBeast", DeathVfxTheme.Ruins }, { "ArcGuard", DeathVfxTheme.Ruins },
        // ⑨ 闇
        { "Phantom", DeathVfxTheme.Shadow },
        // ⑩ 月光
        { "Tsukuyomi", DeathVfxTheme.Moonlight }, { "Susanoo", DeathVfxTheme.Moonlight },
    };

    // ボスの連鎖爆発（Areaのボスのみ）
    private static readonly HashSet<string> BossChain = new HashSet<string>
    {
        "GravePole", "Condor", "IronNest", "Balloon", "Fingers", "Shaman", "ArcGuard", "Obelisk", "Tsukuyomi", "Susanoo", "NeonDancer",
    };

    // 切り替え前から、EnemyDataのDeath Effect Prefab Overrideに従来のエフェクトが入っていたもの（元に戻す時に戻す）
    private static readonly HashSet<string> HadOldOverride = new HashSet<string>
    {
        "ArcGuard", "Dragon", "Fingers", "GuardBeast", "GyroWard", "Gyrorb", "Marshal", "NeonDancer", "Obelisk", "Shaman", "Susanoo", "Tsukuyomi", "Zephyr",
    };

    // ======================================================
    // 1 作成
    // ======================================================
    [MenuItem("Tools/撃破エフェクト/1 新しい撃破エフェクトを作成")]
    private static void Create()
    {
        var softGlow = AssetDatabase.LoadAssetAtPath<Texture2D>(SoftGlowPath);
        if (softGlow == null || AssetDatabase.LoadMainAssetAtPath(MatSrc) == null)
        {
            EditorUtility.DisplayDialog("撃破エフェクト", $"コピー元が見つかりません：\n{SoftGlowPath}\n{MatSrc}", "OK");
            return;
        }
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
        string msg = exists
            ? $"{PrefabPath} は既にあります。\n作り直すと、プレハブのInspectorで調整した値（数・色テーマ・各層のParticle System）が初期値に戻ります。\n作り直しますか？"
            : $"次を作成します：\n・画像 {RingTexPath} / {SquareTexPath}\n・マテリアル4つ（{GenDir}）\n・プレハブ {PrefabPath}\n続けますか？";
        if (!EditorUtility.DisplayDialog("撃破エフェクト：作成", msg, exists ? "作り直す" : "作成", "キャンセル")) return;

        if (!AssetDatabase.IsValidFolder("Assets/Generated")) AssetDatabase.CreateFolder("Assets", "Generated");
        if (!AssetDatabase.IsValidFolder(GenDir)) AssetDatabase.CreateFolder("Assets/Generated", "VFX");

        var ringTex = WriteTexture(RingTexPath, 256, 256, RingPixel);
        var squareTex = WriteTexture(SquareTexPath, 32, 32, SquarePixel);

        var glowAdd = MakeMaterial(MatGlowAdd, softGlow, additive: true);
        var smokeMat = MakeMaterial(MatSmoke, softGlow, additive: false);
        var ringAdd = MakeMaterial(MatRingAdd, ringTex, additive: true);
        var debrisMat = MakeMaterial(MatDebris, squareTex, additive: false);

        BuildPrefab(glowAdd, smokeMat, ringAdd, debrisMat);
        AssetDatabase.SaveAssets();
        Debug.Log($"[EnemyDeathVFXSetupTool] 新しい撃破エフェクトを作成しました：{PrefabPath}");
        EditorUtility.DisplayDialog("撃破エフェクト", $"作成しました。\n{PrefabPath}\n\n次に「2 全エネミーを新しい撃破エフェクトに切り替え」を実行してください。", "OK");
    }

    // ---------- 画像 ----------
    private delegate float PixelAlpha(float u, float v); // u,v：-1〜1

    private static Texture2D WriteTexture(string path, int w, int h, PixelAlpha f)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w * 2f - 1f, v = (y + 0.5f) / h * 2f - 1f;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(f(u, v))));
            }
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = TextureImporterType.Default;
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = false;
        imp.wrapMode = TextureWrapMode.Clamp;
        imp.textureCompression = TextureImporterCompression.Uncompressed;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // 細いリング（外側にふんわり光が広がる）
    private static float RingPixel(float u, float v)
    {
        float r = Mathf.Sqrt(u * u + v * v);
        float core = Mathf.Exp(-Mathf.Pow((r - 0.82f) / 0.05f, 2f));
        float glow = Mathf.Exp(-Mathf.Pow((r - 0.8f) / 0.16f, 2f)) * 0.35f;
        float edgeFade = Mathf.Clamp01((1f - r) / 0.06f);
        return (core + glow) * edgeFade;
    }

    // 破片（角が少しだけ柔らかい四角）
    private static float SquarePixel(float u, float v)
    {
        float d = Mathf.Max(Mathf.Abs(u), Mathf.Abs(v));
        return Mathf.Clamp01((1f - d) / 0.12f);
    }

    // ---------- マテリアル（Mat_StardustParticleをコピーして画像と合成方法を設定） ----------
    private static Material MakeMaterial(string path, Texture2D tex, bool additive)
    {
        if (AssetDatabase.LoadMainAssetAtPath(path) == null) AssetDatabase.CopyAsset(MatSrc, path);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        // URP Particles/Unlit：_Blend 0=アルファ 2=加算。_SrcBlend 5=SrcAlpha、_DstBlend 1=One（加算）/10=OneMinusSrcAlpha（アルファ）
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

    // ---------- プレハブ ----------
    private static void BuildPrefab(Material glowAdd, Material smokeMat, Material ringAdd, Material debrisMat)
    {
        var root = new GameObject("VFX_EnemyDeath");
        try
        {
            var comp = root.AddComponent<EnemyDeathVFX>();

            // 描画順：従来の撃破エフェクト（Defaultレイヤー・0〜10）と同じ並びで、層ごとに前後を付ける
            var smoke = Layer(root, "Smoke", smokeMat, 8, ps =>
            {
                Main(ps, life: new Vector2(0.9f, 1.3f), size: new Vector2(0.8f, 1.3f), speed: new Vector2(0.2f, 0.7f));
                Circle(ps, 0.25f);
                RandomRotation(ps, 30f);
                SizeOverLife(ps, 0.6f, 1.6f);
                AlphaOverLife(ps, new[] { 0f, 1f, 0.6f, 0f }, new[] { 0f, 0.15f, 0.6f, 1f });
                Velocity(ps, 0f, 0.35f);
            });
            var ringSlow = Layer(root, "RingSlow", ringAdd, 9, ps =>
            {
                Main(ps, life: new Vector2(0.6f, 0.6f), size: new Vector2(5f, 5f), speed: Vector2.zero);
                NoShape(ps);
                SizeOverLife(ps, 0.1f, 1f, easeOut: true);
                AlphaOverLife(ps, new[] { 0.8f, 0.5f, 0f }, new[] { 0f, 0.5f, 1f });
            });
            var fireball = Layer(root, "Fireball", glowAdd, 10, ps =>
            {
                Main(ps, life: new Vector2(0.35f, 0.6f), size: new Vector2(0.55f, 1.0f), speed: new Vector2(0.2f, 1.4f));
                Circle(ps, 0.15f);
                RandomRotation(ps, 90f);
                SizeOverLife(ps, 0.6f, 1.5f);
                Dampen(ps, 0.1f);
            });
            var ringFast = Layer(root, "RingFast", ringAdd, 11, ps =>
            {
                Main(ps, life: new Vector2(0.28f, 0.28f), size: new Vector2(3.2f, 3.2f), speed: Vector2.zero);
                NoShape(ps);
                SizeOverLife(ps, 0.15f, 1f, easeOut: true);
                AlphaOverLife(ps, new[] { 1f, 0.6f, 0f }, new[] { 0f, 0.5f, 1f });
            });
            var debris = Layer(root, "Debris", debrisMat, 12, ps =>
            {
                Main(ps, life: new Vector2(0.6f, 1.0f), size: new Vector2(0.06f, 0.16f), speed: new Vector2(2f, 5f));
                var main = ps.main; main.gravityModifier = 1.2f;
                Circle(ps, 0.1f);
                RandomRotation(ps, 720f);
                AlphaOverLife(ps, new[] { 1f, 1f, 0f }, new[] { 0f, 0.7f, 1f });
                Dampen(ps, 0.03f);
            });
            var sparks = Layer(root, "Sparks", glowAdd, 13, ps =>
            {
                Main(ps, life: new Vector2(0.3f, 0.8f), size: new Vector2(0.05f, 0.3f), speed: new Vector2(3f, 9f));
                var main = ps.main; main.gravityModifier = 0.2f;
                Circle(ps, 0.05f);
                var r = ps.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.velocityScale = 0.06f;
                r.lengthScale = 1.2f;
                SizeOverLife(ps, 1f, 0.3f);
                AlphaOverLife(ps, new[] { 1f, 1f, 0f }, new[] { 0f, 0.6f, 1f });
                Dampen(ps, 0.08f);
            });
            var embers = Layer(root, "Embers", glowAdd, 13, ps =>
            {
                Main(ps, life: new Vector2(0.8f, 1.4f), size: new Vector2(0.04f, 0.09f), speed: new Vector2(0.5f, 2f));
                var main = ps.main; main.gravityModifier = -0.12f;
                Circle(ps, 0.3f);
                SizeOverLife(ps, 1f, 0.4f);
                AlphaOverLife(ps, new[] { 0f, 1f, 1f, 0f }, new[] { 0f, 0.1f, 0.6f, 1f });
                Dampen(ps, 0.05f);
            });
            var flash = Layer(root, "Flash", glowAdd, 14, ps =>
            {
                Main(ps, life: new Vector2(0.14f, 0.14f), size: new Vector2(2.6f, 2.6f), speed: Vector2.zero);
                NoShape(ps);
                SizeOverLife(ps, 1f, 0.25f);
                AlphaOverLife(ps, new[] { 1f, 0f }, new[] { 0f, 1f });
            });
            var starRays = Layer(root, "StarRays", glowAdd, 14, ps =>
            {
                Main(ps, life: new Vector2(0.25f, 0.4f), size: new Vector2(0.15f, 0.25f), speed: new Vector2(10f, 16f));
                Circle(ps, 0.05f);
                var r = ps.GetComponent<ParticleSystemRenderer>();
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.velocityScale = 0.05f;
                r.lengthScale = 3f;
                AlphaOverLife(ps, new[] { 1f, 0f }, new[] { 0f, 1f });
                Dampen(ps, 0.1f);
            });
            var rainbowRings = Layer(root, "RainbowRings", ringAdd, 15, ps =>
            {
                Main(ps, life: new Vector2(0.7f, 0.7f), size: new Vector2(2.4f, 2.4f), speed: Vector2.zero);
                NoShape(ps);
                SizeOverLife(ps, 0.1f, 1f, easeOut: true);
                AlphaOverLife(ps, new[] { 1f, 0.7f, 0f }, new[] { 0f, 0.5f, 1f });
            });

            var so = new SerializedObject(comp);
            so.FindProperty("flash").objectReferenceValue = flash;
            so.FindProperty("fireball").objectReferenceValue = fireball;
            so.FindProperty("sparks").objectReferenceValue = sparks;
            so.FindProperty("ringFast").objectReferenceValue = ringFast;
            so.FindProperty("ringSlow").objectReferenceValue = ringSlow;
            so.FindProperty("debris").objectReferenceValue = debris;
            so.FindProperty("smoke").objectReferenceValue = smoke;
            so.FindProperty("embers").objectReferenceValue = embers;
            so.FindProperty("rainbowRings").objectReferenceValue = rainbowRings;
            so.FindProperty("starRays").objectReferenceValue = starRays;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static ParticleSystem Layer(GameObject root, string name, Material mat, int order, System.Action<ParticleSystem> setup)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;   // EnemyDeathVFXがEmitで出す
        main.loop = true;
        main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 300;
        main.startColor = Color.white;
        var em = ps.emission;
        em.enabled = false;
        em.rateOverTime = 0f;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.sortingLayerName = "Default";
        r.sortingOrder = order;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        setup(ps);
        return ps;
    }

    private static void Main(ParticleSystem ps, Vector2 life, Vector2 size, Vector2 speed)
    {
        var main = ps.main;
        main.startLifetime = new ParticleSystem.MinMaxCurve(life.x, life.y);
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
    }

    private static void Circle(ParticleSystem ps, float radius)
    {
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle; // XY平面で外向きに飛ぶ
        shape.radius = radius;
        shape.radiusThickness = 1f;
    }

    private static void NoShape(ParticleSystem ps)
    {
        var shape = ps.shape;
        shape.enabled = false;
    }

    private static void RandomRotation(ParticleSystem ps, float spinDegPerSec)
    {
        var main = ps.main;
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-spinDegPerSec * Mathf.Deg2Rad, spinDegPerSec * Mathf.Deg2Rad);
    }

    private static void SizeOverLife(ParticleSystem ps, float from, float to, bool easeOut = false)
    {
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        AnimationCurve c = easeOut
            ? new AnimationCurve(new Keyframe(0f, from, 0f, (to - from) * 2.5f), new Keyframe(1f, to, 0f, 0f))
            : AnimationCurve.Linear(0f, from, 1f, to);
        sol.size = new ParticleSystem.MinMaxCurve(1f, c);
    }

    private static void AlphaOverLife(ParticleSystem ps, float[] alphas, float[] times)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        var ak = new GradientAlphaKey[alphas.Length];
        for (int i = 0; i < alphas.Length; i++) ak[i] = new GradientAlphaKey(alphas[i], times[i]);
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, ak);
        col.color = new ParticleSystem.MinMaxGradient(g);
    }

    // 最初は速く、急に減速する
    private static void Dampen(ParticleSystem ps, float dampen)
    {
        var lim = ps.limitVelocityOverLifetime;
        lim.enabled = true;
        lim.limit = new ParticleSystem.MinMaxCurve(0.3f);
        lim.dampen = dampen;
    }

    // ★x/y/zは未使用軸も含め全て同じモード（Constant）で明示設定する
    private static void Velocity(ParticleSystem ps, float x, float y)
    {
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(x);
        vel.y = new ParticleSystem.MinMaxCurve(y);
        vel.z = new ParticleSystem.MinMaxCurve(0f);
    }

    // ======================================================
    // 2 切り替え
    // ======================================================
    [MenuItem("Tools/撃破エフェクト/2 全エネミーを新しい撃破エフェクトに切り替え（色テーマ・ボス連鎖も設定）")]
    private static void ApplyAll()
    {
        var newFx = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var oldFx = AssetDatabase.LoadAssetAtPath<GameObject>(OldPrefab);
        if (newFx == null) { EditorUtility.DisplayDialog("撃破エフェクト", "先に「1 新しい撃破エフェクトを作成」を実行してください。", "OK"); return; }
        if (!EditorUtility.DisplayDialog("撃破エフェクト：切り替え",
                "・全EnemyDataのDeath Effect Prefab Overrideを新しい撃破エフェクトにし、Death Vfx ConfigのThemeとBoss Chainを一覧どおりに設定\n" +
                "・エネミーPrefabのEnemyStatsで従来の撃破エフェクトを使っているものを新しい撃破エフェクトに差し替え\n" +
                "（火花の数・速さ・大きさ・Ring Scale等の従来の値はそのまま使う）\n続けますか？", "切り替える", "キャンセル"))
            return;

        var log = new System.Text.StringBuilder();
        foreach (var data in LoadAllEnemyData())
        {
            string key = data.name.StartsWith("EnemyData_") ? data.name.Substring("EnemyData_".Length) : data.name;
            Undo.RecordObject(data, "Enemy Death VFX");
            data.deathEffectPrefabOverride = newFx;
            if (data.deathVfxConfig == null) data.deathVfxConfig = new DeathVfxConfig();
            data.deathVfxConfig.theme = ThemeTable.TryGetValue(key, out var th) ? th : DeathVfxTheme.Fire;
            data.deathVfxConfig.bossChain = BossChain.Contains(key);
            EditorUtility.SetDirty(data);
            log.AppendLine($"{key}：{data.deathVfxConfig.theme}{(data.deathVfxConfig.bossChain ? "（ボス連鎖）" : "")}{(ThemeTable.ContainsKey(key) ? "" : " ※一覧に無いため炎")}{(data.useCustomDeathVfx ? "" : " ※Use Custom Death VfxがOFFのため色テーマ・大きさは使われない")}");
        }
        int prefabCount = ReplacePrefabDeathEffect(oldFx, newFx);
        AssetDatabase.SaveAssets();
        Debug.Log($"[EnemyDeathVFXSetupTool] 新しい撃破エフェクトに切り替えました（エネミーPrefab {prefabCount}件）\n" + log);
        EditorUtility.DisplayDialog("撃破エフェクト", $"切り替えました。\nエネミーPrefab {prefabCount}件を差し替え。\n各エネミーの色テーマはConsoleに出力しました。", "OK");
    }

    // ======================================================
    // 3 元に戻す
    // ======================================================
    [MenuItem("Tools/撃破エフェクト/3 全エネミーを元の撃破エフェクトに戻す")]
    private static void RevertAll()
    {
        var newFx = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var oldFx = AssetDatabase.LoadAssetAtPath<GameObject>(OldPrefab);
        if (oldFx == null) { EditorUtility.DisplayDialog("撃破エフェクト", $"{OldPrefab} が見つかりません。", "OK"); return; }
        if (!EditorUtility.DisplayDialog("撃破エフェクト：元に戻す",
                "全エネミーの撃破エフェクトを従来のVFX_Explosion_A_RingSparksに戻します（切り替え前と同じ状態）。\n続けますか？", "戻す", "キャンセル"))
            return;
        foreach (var data in LoadAllEnemyData())
        {
            string key = data.name.StartsWith("EnemyData_") ? data.name.Substring("EnemyData_".Length) : data.name;
            Undo.RecordObject(data, "Enemy Death VFX Revert");
            data.deathEffectPrefabOverride = HadOldOverride.Contains(key) ? oldFx : null;
            EditorUtility.SetDirty(data);
        }
        int prefabCount = newFx != null ? ReplacePrefabDeathEffect(newFx, oldFx) : 0;
        AssetDatabase.SaveAssets();
        Debug.Log($"[EnemyDeathVFXSetupTool] 元の撃破エフェクトに戻しました（エネミーPrefab {prefabCount}件）");
        EditorUtility.DisplayDialog("撃破エフェクト", "元に戻しました。", "OK");
    }

    private static List<EnemyData> LoadAllEnemyData()
    {
        var list = new List<EnemyData>();
        foreach (string guid in AssetDatabase.FindAssets("t:EnemyData", new[] { EnemyDataDir }))
        {
            var d = AssetDatabase.LoadAssetAtPath<EnemyData>(AssetDatabase.GUIDToAssetPath(guid));
            if (d != null) list.Add(d);
        }
        return list;
    }

    // エネミーPrefabのEnemyStats.deathEffectPrefabが from のものを to に差し替える
    private static int ReplacePrefabDeathEffect(GameObject from, GameObject to)
    {
        if (from == null || to == null) return 0;
        int count = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { EnemyPrefabDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            bool uses = false;
            foreach (var st in asset.GetComponentsInChildren<EnemyStats>(true))
                if (new SerializedObject(st).FindProperty("deathEffectPrefab").objectReferenceValue == from) { uses = true; break; }
            if (!uses) continue;

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var st in root.GetComponentsInChildren<EnemyStats>(true))
                {
                    var so = new SerializedObject(st);
                    var p = so.FindProperty("deathEffectPrefab");
                    if (p.objectReferenceValue != from) continue;
                    p.objectReferenceValue = to;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                PrefabUtility.SaveAsPrefabAsset(root, path);
                count++;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
        return count;
    }

    // ======================================================
    // テスト再生（Play中）
    // ======================================================
    [MenuItem("Tools/撃破エフェクト/テスト：10テーマを並べて再生（Play中）")]
    private static void PreviewThemes()
    {
        var prefab = PreviewPrefab();
        if (prefab == null) return;
        Vector3 c = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
        int n = System.Enum.GetValues(typeof(DeathVfxTheme)).Length;
        for (int i = 0; i < n; i++)
        {
            int col = i % 5, row = i / 5;
            var pos = new Vector3(c.x - 7f + col * 3.5f, c.y + 2f - row * 4f, 0f);
            EnemyDeathVFX.SpawnPreview(prefab, pos, (DeathVfxTheme)i, 6f, false);
        }
        Debug.Log("[EnemyDeathVFXSetupTool] 上段左から：炎/紅蓮/ネオン水色/ネオン紫/ネオン虹、下段左から：毒/機械/遺跡/闇/月光");
    }

    [MenuItem("Tools/撃破エフェクト/テスト：ボスの連鎖爆発（炎）とNeonDancer（ネオン虹）を再生（Play中）")]
    private static void PreviewBoss()
    {
        var prefab = PreviewPrefab();
        if (prefab == null) return;
        Vector3 c = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
        EnemyDeathVFX.SpawnPreview(prefab, new Vector3(c.x - 4f, c.y + 0.5f, 0f), DeathVfxTheme.Fire, 8f, true);
        EnemyDeathVFX.SpawnPreview(prefab, new Vector3(c.x + 4f, c.y + 0.5f, 0f), DeathVfxTheme.NeonRainbow, 9f, true);
    }

    private static GameObject PreviewPrefab()
    {
        if (!Application.isPlaying) { EditorUtility.DisplayDialog("撃破エフェクト", "Play中に実行してください。", "OK"); return null; }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) EditorUtility.DisplayDialog("撃破エフェクト", "先に「1 新しい撃破エフェクトを作成」を実行してください。", "OK");
        return prefab;
    }
}
