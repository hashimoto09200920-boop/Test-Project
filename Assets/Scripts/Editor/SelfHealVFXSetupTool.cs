using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// スキルC3「セルフリストア」の新しい回復エフェクト（SelfHealVFX）のセットアップメニュー。
/// 05_Gameシーンのプレイヤー（PixelDancerController、タグPlayer）と床（FloorHealth）に、子として
/// 「SelfHealVFX」を作り、各コンポーネントのSelf Heal Vfxに設定する（SEは従来どおり）。
/// 元に戻す時は「元の回復エフェクトに戻す」でSelf Heal Vfxを空にする（従来のHeal Vfx Prefabが再生される）。
/// </summary>
public static class SelfHealVFXSetupTool
{
    private const string GenDir       = "Assets/Generated/VFX";
    private const string RingTex      = GenDir + "/HealRing.png";
    private const string CrossTex     = GenDir + "/HealCross.png";
    private const string PillarTex    = GenDir + "/HealPillar.png";
    private const string SoftGlowPath = "Assets/Generated/UI/SoftGlowCircle.png";
    private const string MatSrc       = "Assets/Art/Background/Mat_StardustParticle.mat"; // URP Particles/Unlit・加算
    private const string MatGlow      = GenDir + "/Mat_Heal_GlowAdd.mat";
    private const string MatRing      = GenDir + "/Mat_Heal_RingAdd.mat";
    private const string MatCross     = GenDir + "/Mat_Heal_CrossAdd.mat";
    private const string MatPillar    = GenDir + "/Mat_Heal_PillarAdd.mat";
    private const string SpriteAddMat = "Assets/Art/Enemy/S3_10_NeonDancer/ND_WormholeAdditive.mat"; // スプライト用の加算（体の発光）
    private const string ChildName    = "SelfHealVFX";

    // ======================================================
    // 作成・設定
    // ======================================================
    [MenuItem("Tools/回復エフェクト/1 新しい回復エフェクトを作成してプレイヤーと床に設定（05_Game）")]
    private static void Setup()
    {
        var player = FindPlayer();
        var floors = Object.FindObjectsByType<FloorHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        var softGlow = AssetDatabase.LoadAssetAtPath<Texture2D>(SoftGlowPath);
        var spriteAdd = AssetDatabase.LoadAssetAtPath<Material>(SpriteAddMat);
        if (player == null || floors.Length == 0 || softGlow == null || spriteAdd == null || AssetDatabase.LoadMainAssetAtPath(MatSrc) == null)
        {
            EditorUtility.DisplayDialog("回復エフェクト",
                "05_Gameシーンを開いてから実行してください。\n（プレイヤー（タグPlayerのPixelDancerController）・FloorHealth、または\n" +
                $"{SoftGlowPath} / {SpriteAddMat} / {MatSrc} が見つかりません）", "OK");
            return;
        }
        var playerSr = new SerializedObject(player).FindProperty("spriteRenderer").objectReferenceValue as SpriteRenderer;
        if (playerSr == null) playerSr = player.GetComponent<SpriteRenderer>();
        if (playerSr == null) { EditorUtility.DisplayDialog("回復エフェクト", "プレイヤーのSpriteRendererが見つかりません。", "OK"); return; }

        string floorNames = string.Join(" / ", System.Array.ConvertAll(floors, f => f.name));
        bool exists = playerSr.transform.Find(ChildName) != null;
        foreach (var f in floors) if (f.transform.Find(ChildName) != null) exists = true;
        if (!EditorUtility.DisplayDialog("回復エフェクト：作成・設定",
                $"次を作成・設定します：\n・画像3つ・マテリアル4つ（{GenDir}）\n" +
                $"・プレイヤー（{playerSr.name}）の子に「{ChildName}」（光の柱・光の粒・十字のきらめき・体の発光）\n" +
                $"・床（{floorNames}）の子に「{ChildName}」（光の輪・光の波・光の粒・十字のきらめき）\n" +
                "・PixelDancerController / FloorHealth の Self Heal Vfx に設定（SEは従来どおり）\n" +
                (exists ? "\n※既にある「SelfHealVFX」は作り直します（Inspectorで調整した値は初期値に戻ります）\n" : "") +
                "\n実行後はシーンを保存してください。続けますか？", "実行", "キャンセル"))
            return;

        if (!AssetDatabase.IsValidFolder("Assets/Generated")) AssetDatabase.CreateFolder("Assets", "Generated");
        if (!AssetDatabase.IsValidFolder(GenDir)) AssetDatabase.CreateFolder("Assets/Generated", "VFX");
        var ringTex = WriteTexture(RingTex, 256, 256, RingPixel);
        var crossTex = WriteTexture(CrossTex, 128, 128, CrossPixel);
        var pillarTex = WriteTexture(PillarTex, 64, 256, PillarPixel);
        var glowMat = MakeAdditive(MatGlow, softGlow);
        var ringMat = MakeAdditive(MatRing, ringTex);
        var crossMat = MakeAdditive(MatCross, crossTex);
        var pillarMat = MakeAdditive(MatPillar, pillarTex);

        Undo.SetCurrentGroupName("Setup Self Heal VFX");
        int group = Undo.GetCurrentGroup();

        // プレイヤー
        var pv = Build(playerSr.transform, SelfHealVFX.Target.Player, playerSr, playerSr.sortingLayerID, playerSr.sortingOrder,
                       glowMat, ringMat, crossMat, pillarMat, spriteAdd);
        var pso = new SerializedObject(player);
        pso.FindProperty("selfHealVfx").objectReferenceValue = pv;
        pso.ApplyModifiedProperties();

        // 床
        foreach (var floor in floors)
        {
            var fsr = new SerializedObject(floor).FindProperty("spriteRenderer").objectReferenceValue as SpriteRenderer;
            if (fsr == null) fsr = floor.GetComponentInChildren<SpriteRenderer>(true);
            int layerId = fsr != null ? fsr.sortingLayerID : playerSr.sortingLayerID;
            int order = fsr != null ? fsr.sortingOrder : playerSr.sortingOrder - 1;
            var fv = Build(floor.transform, SelfHealVFX.Target.Floor, fsr, layerId, order, glowMat, ringMat, crossMat, pillarMat, spriteAdd);
            var fso = new SerializedObject(floor);
            fso.FindProperty("selfHealVfx").objectReferenceValue = fv;
            fso.ApplyModifiedProperties();
        }
        Undo.CollapseUndoOperations(group);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        Debug.Log($"[SelfHealVFXSetupTool] 回復エフェクトを設定しました（プレイヤー：{playerSr.name}、床：{floorNames}）");
        EditorUtility.DisplayDialog("回復エフェクト", "設定しました。シーンを保存してください。", "OK");
    }

    // ======================================================
    // 元に戻す
    // ======================================================
    [MenuItem("Tools/回復エフェクト/2 元の回復エフェクトに戻す（05_Game）")]
    private static void Revert()
    {
        var player = FindPlayer();
        var floors = Object.FindObjectsByType<FloorHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (player == null && floors.Length == 0) { EditorUtility.DisplayDialog("回復エフェクト", "05_Gameシーンを開いてから実行してください。", "OK"); return; }
        if (!EditorUtility.DisplayDialog("回復エフェクト：元に戻す",
                "PixelDancerController / FloorHealth の Self Heal Vfx を空にし、子の「SelfHealVFX」を削除します\n（従来のHeal Vfx Prefabが再生されるようになります）。\n実行後はシーンを保存してください。続けますか？", "戻す", "キャンセル"))
            return;
        Undo.SetCurrentGroupName("Revert Self Heal VFX");
        var targets = new System.Collections.Generic.List<Component>();
        if (player != null) targets.Add(player);
        targets.AddRange(floors);
        foreach (var c in targets)
        {
            var so = new SerializedObject(c);
            var p = so.FindProperty("selfHealVfx");
            var v = p.objectReferenceValue as SelfHealVFX;
            p.objectReferenceValue = null;
            so.ApplyModifiedProperties();
            if (v != null) Undo.DestroyObjectImmediate(v.gameObject);
        }
        if (player != null) EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
        EditorUtility.DisplayDialog("回復エフェクト", "元に戻しました。シーンを保存してください。", "OK");
    }

    // ======================================================
    // エネミーダンサーのフロア（ND_Floor）：破壊後に自動で全快した瞬間に、プレイヤーのフロアと同じ回復エフェクトを出す
    // ======================================================
    private const string NeonDancerPrefab = "Assets/Prefabs/Enemies/NeonDancer.prefab";

    [MenuItem("Tools/回復エフェクト/3 エネミーダンサーのフロア（ND_Floor）の全快時にも出す")]
    private static void SetupNeonDancerFloor()
    {
        var softGlow = AssetDatabase.LoadAssetAtPath<Texture2D>(SoftGlowPath);
        var spriteAdd = AssetDatabase.LoadAssetAtPath<Material>(SpriteAddMat);
        if (softGlow == null || spriteAdd == null || AssetDatabase.LoadMainAssetAtPath(MatSrc) == null
            || AssetDatabase.LoadAssetAtPath<GameObject>(NeonDancerPrefab) == null)
        {
            EditorUtility.DisplayDialog("回復エフェクト", $"必要なファイルが見つかりません：\n{SoftGlowPath}\n{SpriteAddMat}\n{MatSrc}\n{NeonDancerPrefab}", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("回復エフェクト：エネミーダンサーのフロア",
                "NeonDancer.prefab の ND_Floor の子に「SelfHealVFX」（光の輪・光の波・光の粒・十字のきらめき）を作り、\n" +
                "ND_Floor > NeonDancerBarrier の Restore Vfx に設定します。\n" +
                "破壊後30秒で自動的に全快した瞬間に再生します（後半移行時の全快では再生しません）。\n" +
                "※既にある場合は作り直します。続けますか？", "実行", "キャンセル"))
            return;

        if (!AssetDatabase.IsValidFolder("Assets/Generated")) AssetDatabase.CreateFolder("Assets", "Generated");
        if (!AssetDatabase.IsValidFolder(GenDir)) AssetDatabase.CreateFolder("Assets/Generated", "VFX");
        var ringTex = AssetDatabase.LoadAssetAtPath<Texture2D>(RingTex) ?? WriteTexture(RingTex, 256, 256, RingPixel);
        var crossTex = AssetDatabase.LoadAssetAtPath<Texture2D>(CrossTex) ?? WriteTexture(CrossTex, 128, 128, CrossPixel);
        var pillarTex = AssetDatabase.LoadAssetAtPath<Texture2D>(PillarTex) ?? WriteTexture(PillarTex, 64, 256, PillarPixel);
        var glowMat = MakeAdditive(MatGlow, softGlow);
        var ringMat = MakeAdditive(MatRing, ringTex);
        var crossMat = MakeAdditive(MatCross, crossTex);
        var pillarMat = MakeAdditive(MatPillar, pillarTex);

        string result;
        GameObject root = PrefabUtility.LoadPrefabContents(NeonDancerPrefab);
        try
        {
            Transform floor = null;
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) if (t.name == "ND_Floor") { floor = t; break; }
            var barrier = floor != null ? floor.GetComponent<NeonDancerBarrier>() : null;
            if (barrier == null)
            {
                EditorUtility.DisplayDialog("回復エフェクト", "NeonDancer.prefab に ND_Floor（NeonDancerBarrier付き）が見つかりません。", "OK");
                return;
            }
            var floorSr = floor.GetComponent<SpriteRenderer>();
            int layerId = floorSr != null ? floorSr.sortingLayerID : 0;
            int order = floorSr != null ? floorSr.sortingOrder : 0;
            var vfx = Build(floor, SelfHealVFX.Target.Floor, floorSr, layerId, order, glowMat, ringMat, crossMat, pillarMat, spriteAdd);
            var so = new SerializedObject(barrier);
            so.FindProperty("restoreVfx").objectReferenceValue = vfx;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, NeonDancerPrefab);
            result = $"ND_Floor の子に SelfHealVFX を作成し、NeonDancerBarrier の Restore Vfx に設定しました（基準の絵：{(floorSr != null ? floorSr.name : "なし")}）";
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[SelfHealVFXSetupTool] " + result);
        EditorUtility.DisplayDialog("回復エフェクト", result, "OK");
    }

    [MenuItem("Tools/回復エフェクト/テスト：プレイヤーと床で再生（Play中）")]
    private static void TestPlay()
    {
        if (!Application.isPlaying) { EditorUtility.DisplayDialog("回復エフェクト", "Play中に実行してください。", "OK"); return; }
        foreach (var v in Object.FindObjectsByType<SelfHealVFX>(FindObjectsSortMode.None)) v.Play();
    }

    private static PixelDancerController FindPlayer()
    {
        foreach (var p in Object.FindObjectsByType<PixelDancerController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (p.CompareTag("Player")) return p;
        return null;
    }

    // ---------- 組み立て ----------
    private static SelfHealVFX Build(Transform parent, SelfHealVFX.Target target, SpriteRenderer source, int layerId, int baseOrder,
                                     Material glow, Material ring, Material cross, Material pillar, Material spriteAdd)
    {
        var old = parent.Find(ChildName);
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        var root = new GameObject(ChildName);
        Undo.RegisterCreatedObjectUndo(root, "Create SelfHealVFX");
        root.transform.SetParent(parent, false);
        root.layer = parent.gameObject.layer;
        var v = root.AddComponent<SelfHealVFX>();
        bool isPlayer = target == SelfHealVFX.Target.Player;

        ParticleSystem pPillar = null, pRing = null, pWave = null;
        SpriteRenderer bodyGlow = null;
        if (isPlayer)
        {
            // ② 光の柱（体の後ろ）
            pPillar = Layer(root, "Pillar", pillar, layerId, baseOrder - 1, ps =>
            {
                Main(ps, new Vector2(0.5f, 0.5f), new Vector2(1f, 1f), Vector2.zero);
                var main = ps.main; main.startSize3D = true;
                NoShape(ps);
                SizeOverLife(ps, AnimationCurve.Linear(0f, 0.7f, 1f, 1.05f));
                Alpha(ps, new[] { 0f, 0.9f, 0.5f, 0f }, new[] { 0f, 0.15f, 0.5f, 1f });
            });
            // ⑤ 体の発光（同じ絵を加算で重ねる）
            var g = new GameObject("BodyGlow");
            g.transform.SetParent(root.transform, false);
            g.layer = root.layer;
            bodyGlow = g.AddComponent<SpriteRenderer>();
            bodyGlow.sharedMaterial = spriteAdd;
            bodyGlow.sortingLayerID = layerId;
            bodyGlow.sortingOrder = baseOrder + 1;
            bodyGlow.color = new Color(1f, 1f, 1f, 0f);
            bodyGlow.enabled = false;
        }
        else
        {
            // ① 床の楕円に沿った光の輪
            pRing = Layer(root, "FloorRing", ring, layerId, baseOrder + 1, ps =>
            {
                Main(ps, new Vector2(0.55f, 0.55f), new Vector2(1f, 1f), Vector2.zero);
                var main = ps.main; main.startSize3D = true;
                NoShape(ps);
                SizeOverLife(ps, new AnimationCurve(new Keyframe(0f, 0.4f, 0f, 1.8f), new Keyframe(1f, 1f, 0f, 0f)));
                Alpha(ps, new[] { 1f, 0.6f, 0f }, new[] { 0f, 0.5f, 1f });
            });
            // ⑥ 中央から左右へ走る光の波
            pWave = Layer(root, "Wave", glow, layerId, baseOrder + 2, ps =>
            {
                Main(ps, new Vector2(0.35f, 0.5f), new Vector2(0.12f, 0.22f), new Vector2(0.2f, 0.6f));
                Circle(ps, 0.05f);
                SizeOverLife(ps, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));
                Alpha(ps, new[] { 1f, 0f }, new[] { 0f, 1f });
                Velocity(ps, new Vector2(0f, 0f), new Vector2(0.3f, 0.5f));
            });
        }
        // ③ 立ちのぼる光の粒
        var pMotes = Layer(root, "Motes", glow, layerId, baseOrder + 2, ps =>
        {
            Main(ps, new Vector2(0.7f, 1.1f), new Vector2(0.06f, 0.14f), Vector2.zero);
            NoShape(ps);
            Velocity(ps, new Vector2(-0.2f, 0.2f), new Vector2(0.8f, 1.8f));
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = new ParticleSystem.MinMaxCurve(0.4f);
            noise.frequency = 1.2f;
            noise.scrollSpeed = new ParticleSystem.MinMaxCurve(0.5f);
            noise.quality = ParticleSystemNoiseQuality.Low;
            SizeOverLife(ps, AnimationCurve.Linear(0f, 1f, 1f, 0.2f));
            Alpha(ps, new[] { 0f, 1f, 1f, 0f }, new[] { 0f, 0.1f, 0.6f, 1f });
        });
        // ④ 十字のきらめき
        var pCross = Layer(root, "Crosses", cross, layerId, baseOrder + 3, ps =>
        {
            Main(ps, new Vector2(0.45f, 0.6f), new Vector2(0.18f, 0.32f), Vector2.zero);
            NoShape(ps);
            SizeOverLife(ps, new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.35f, 1f), new Keyframe(1f, 0f)));
            Alpha(ps, new[] { 1f, 1f, 0f }, new[] { 0f, 0.6f, 1f });
        });

        var so = new SerializedObject(v);
        so.FindProperty("target").enumValueIndex = (int)target;
        so.FindProperty("source").objectReferenceValue = source;
        so.FindProperty("pillar").objectReferenceValue = pPillar;
        so.FindProperty("motes").objectReferenceValue = pMotes;
        so.FindProperty("crosses").objectReferenceValue = pCross;
        so.FindProperty("floorRing").objectReferenceValue = pRing;
        so.FindProperty("wave").objectReferenceValue = pWave;
        so.FindProperty("bodyGlow").objectReferenceValue = bodyGlow;
        if (!isPlayer) so.FindProperty("moteCount").intValue = 24;
        so.ApplyModifiedPropertiesWithoutUndo();
        return v;
    }

    private static ParticleSystem Layer(GameObject root, string name, Material mat, int layerId, int order, System.Action<ParticleSystem> setup)
    {
        var go = new GameObject(name);
        go.transform.SetParent(root.transform, false);
        go.layer = root.layer;
        var ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false; // SelfHealVFXがEmitで出す
        main.loop = true;
        main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.scalingMode = ParticleSystemScalingMode.Local; // 親（プレイヤー・床）の拡大率の影響を受けない
        main.maxParticles = 200;
        main.startColor = Color.white;
        var em = ps.emission;
        em.enabled = false;
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.sortingLayerID = layerId;
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

    private static void NoShape(ParticleSystem ps) { var s = ps.shape; s.enabled = false; }

    private static void Circle(ParticleSystem ps, float r)
    {
        var s = ps.shape;
        s.enabled = true;
        s.shapeType = ParticleSystemShapeType.Circle;
        s.radius = r;
    }

    private static void SizeOverLife(ParticleSystem ps, AnimationCurve c)
    {
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, c);
    }

    private static void Alpha(ParticleSystem ps, float[] a, float[] t)
    {
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        var ak = new GradientAlphaKey[a.Length];
        for (int i = 0; i < a.Length; i++) ak[i] = new GradientAlphaKey(a[i], t[i]);
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) }, ak);
        col.color = new ParticleSystem.MinMaxGradient(g);
    }

    // ★x/y/zは未使用軸も含め全て同じモード（TwoConstants）で明示設定する
    private static void Velocity(ParticleSystem ps, Vector2 xRange, Vector2 yRange)
    {
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(xRange.x, xRange.y);
        vel.y = new ParticleSystem.MinMaxCurve(yRange.x, yRange.y);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
    }

    // ---------- マテリアル（Mat_StardustParticle＝加算をコピーして画像だけ変える） ----------
    private static Material MakeAdditive(string path, Texture2D tex)
    {
        if (AssetDatabase.LoadMainAssetAtPath(path) == null) AssetDatabase.CopyAsset(MatSrc, path);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(mat);
        return mat;
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

    // 細い光の輪
    private static float RingPixel(float u, float v)
    {
        float r = Mathf.Sqrt(u * u + v * v);
        float core = Mathf.Exp(-Mathf.Pow((r - 0.85f) / 0.045f, 2f));
        float glow = Mathf.Exp(-Mathf.Pow((r - 0.82f) / 0.14f, 2f)) * 0.35f;
        return (core + glow) * Mathf.Clamp01((1f - r) / 0.05f);
    }

    // 十字のきらめき（細い縦横の光＋中心の光）
    private static float CrossPixel(float u, float v)
    {
        float au = Mathf.Abs(u), av = Mathf.Abs(v);
        float h = Mathf.Exp(-Mathf.Pow(av / 0.07f, 2f)) * Mathf.Clamp01(1f - au);
        float vv = Mathf.Exp(-Mathf.Pow(au / 0.07f, 2f)) * Mathf.Clamp01(1f - av);
        float c = Mathf.Exp(-(u * u + v * v) / 0.05f);
        return Mathf.Max(h, vv) + c * 0.8f;
    }

    // 光の柱（横は柔らかく、上に行くほど薄くなる）
    private static float PillarPixel(float u, float v)
    {
        float across = Mathf.Exp(-Mathf.Pow(u / 0.45f, 2f));
        float up = Mathf.Clamp01(1f - (v + 1f) * 0.5f);          // 下(足元)が濃く、上が薄い
        float bottomFade = Mathf.Clamp01((v + 1f) / 0.15f);       // 足元の端だけ少し柔らかく
        return across * Mathf.Lerp(0.25f, 1f, up) * bottomFade;
    }
}
