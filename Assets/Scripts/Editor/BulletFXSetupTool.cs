using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 未反射弾の演出（BulletFXManager）のセットアップメニュー。
/// 1 作成：画像・マテリアル・共有パーティクルと Assets/Resources/BulletFX.prefab を作る（既にあれば作り直さない）。
///   プレハブがあると、撃たれた弾が自動で登録されて演出が付く。
/// 2 ON/OFF：BulletFX.prefab の Fx Enabled を切り替える（OFFで従来の見た目）。
/// 3 作成：反射弾の演出 Assets/Resources/ReflectFX.prefab を作る（既にあれば作り直さない）。4 ON/OFF：その Fx Enabled を切り替える。
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

    // ---------- 反射弾（ReflectedBulletFXManager） ----------
    private const string ReflectPrefabPath = "Assets/Resources/ReflectFX.prefab";

    [MenuItem("Tools/弾の見た目/3 反射弾を派手にする（作成）")]
    private static void CreateReflect()
    {
        var softGlow = AssetDatabase.LoadAssetAtPath<Texture2D>(SoftGlow);
        var spriteAdd = AssetDatabase.LoadAssetAtPath<Material>(SpriteAdd);
        if (softGlow == null || spriteAdd == null || AssetDatabase.LoadMainAssetAtPath(MatSrc) == null)
        {
            EditorUtility.DisplayDialog("弾の見た目", $"コピー元が見つかりません：\n{SoftGlow}\n{SpriteAdd}\n{MatSrc}", "OK");
            return;
        }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ReflectPrefabPath) != null)
        {
            EditorUtility.DisplayDialog("弾の見た目", $"{ReflectPrefabPath} は既にあります（作り直しません）。\n設定はこのプレハブのInspectorで調整してください。", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("弾の見た目：反射弾を作成",
                $"次を作成します：\n・{ReflectPrefabPath}（管理役と共有パーティクル1つ）\n・画像・マテリアル（{GenDir}、「1」で作成済みなら使い回し）\n" +
                "作成後、プレイヤーが反射した弾（ドリル・ビーム以外）に自動で演出が付きます。弾の動き・当たり判定は変わりません。続けますか？", "作成", "キャンセル"))
            return;

        if (!AssetDatabase.IsValidFolder("Assets/Generated")) AssetDatabase.CreateFolder("Assets", "Generated");
        if (!AssetDatabase.IsValidFolder(GenDir)) AssetDatabase.CreateFolder("Assets/Generated", "VFX");
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");

        if (AssetDatabase.LoadMainAssetAtPath(TexGlow) == null)
            WriteTex(TexGlow, 128, 128, (x, y) => { float r2 = x * x + y * y; return Mathf.Exp(-r2 / 0.18f) * Mathf.Clamp01((1f - Mathf.Sqrt(r2)) / 0.15f); }, true);
        if (AssetDatabase.LoadMainAssetAtPath(TexRing) == null)
            WriteTex(TexRing, 128, 128, (x, y) => { float r = Mathf.Sqrt(x * x + y * y); return (Mathf.Exp(-Mathf.Pow((r - 0.8f) / 0.06f, 2f)) + Mathf.Exp(-Mathf.Pow((r - 0.78f) / 0.18f, 2f)) * 0.3f) * Mathf.Clamp01((1f - r) / 0.06f); }, true);
        var lineTex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexLine);
        if (lineTex == null) lineTex = WriteTex(TexLine, 32, 32, (x, y) => Mathf.Exp(-y * y / 0.15f), false);
        var matDot = AssetDatabase.LoadAssetAtPath<Material>(MatDot);
        if (matDot == null) matDot = MakeMat(MatDot, softGlow, true);
        var matLine = AssetDatabase.LoadAssetAtPath<Material>(MatLine);
        if (matLine == null) matLine = MakeMat(MatLine, lineTex, true);

        var root = new GameObject("ReflectFX");
        try
        {
            var m = root.AddComponent<ReflectedBulletFXManager>();
            m.sparkle = Layer(root, "Sparkle", matDot, 20, ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
                Alpha(ps, new[] { 1f, 0f });
                Size(ps, 1f, 0.2f);
            });
            m.debris = Layer(root, "Debris", matDot, 21, ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
                main.gravityModifier = 0.6f;
                Alpha(ps, new[] { 1f, 1f, 0f });
                Size(ps, 1f, 0.4f);
            });
            m.glowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexGlow);
            m.ringSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexRing);
            m.additiveSpriteMaterial = spriteAdd;
            m.lineMaterial = matLine;
            PrefabUtility.SaveAsPrefabAsset(root, ReflectPrefabPath);
        }
        finally { Object.DestroyImmediate(root); }
        AssetDatabase.SaveAssets();
        Debug.Log($"[BulletFXSetupTool] {ReflectPrefabPath} を作成しました");
        EditorUtility.DisplayDialog("弾の見た目", $"作成しました。\n{ReflectPrefabPath}\n全体のON/OFFや①〜⑩の各演出は、このプレハブのReflectedBulletFXManagerで調整できます。", "OK");
    }

    [MenuItem("Tools/弾の見た目/5 線の破壊演出用の破片を追加")]
    private static void AddDebris()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ReflectPrefabPath) == null) { EditorUtility.DisplayDialog("弾の見た目", ReflectPrefabPath + " がありません（先に「3 作成」を実行してください）。", "OK"); return; }
        var matDot = AssetDatabase.LoadAssetAtPath<Material>(MatDot);
        if (matDot == null) { EditorUtility.DisplayDialog("弾の見た目", MatDot + " がありません（先に「1」か「3」を実行してください）。", "OK"); return; }
        GameObject root = PrefabUtility.LoadPrefabContents(ReflectPrefabPath);
        bool added = false;
        try
        {
            var m = root.GetComponent<ReflectedBulletFXManager>();
            if (m.debris == null)
            {
                var old = root.transform.Find("Debris");
                if (old != null) Object.DestroyImmediate(old.gameObject);
                m.debris = Layer(root, "Debris", matDot, 21, ps =>
                {
                    var main = ps.main;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
                    main.gravityModifier = 0.6f;
                    Alpha(ps, new[] { 1f, 1f, 0f });
                    Size(ps, 1f, 0.4f);
                });
                PrefabUtility.SaveAsPrefabAsset(root, ReflectPrefabPath);
                added = true;
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("弾の見た目", added ? "ReflectFX.prefab に Debris（重力で落ちる破片）を追加しました。" : "Debris は既に設定されています（変更なし）。", "OK");
    }

    // ---------- シールド破壊（ShieldBreakFXManager） ----------
    private const string ShieldPrefabPath = "Assets/Resources/ShieldFX.prefab";
    private const string TexShard = GenDir + "/ShieldFX_Shard.png";
    // ---------- シールドの泡（VFX_ShieldActive）を派手にする ----------
    private const string ShieldBubblePrefab = "Assets/Prefabs/Effects/VFX_ShieldActive.prefab";
    private const string MatShieldDot = GenDir + "/Mat_ShieldDot.mat";
    private const string SpriteLitDefault = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Lit-Default.mat";

    private const string TexHex = GenDir + "/ShieldFX_Hex.png";

    [MenuItem("Tools/弾の見た目/13 シールドの泡を派手にする（ドットの粒・標準）")]
    private static void ShieldBubbleSquare() { ApplyShieldBubble(false); }

    [MenuItem("Tools/弾の見た目/14 シールドの泡の粒を丸いぼかしに切り替える（試す用）")]
    private static void ShieldBubbleRound() { ApplyShieldBubble(true); }

    // 六角形の網目（円の中に六角形の線。縁に近いほど明るく、円の外は透明）
    private static float HexPixel(float x, float y)
    {
        float r = Mathf.Sqrt(x * x + y * y);
        if (r >= 1f) return 0f;
        const float cells = 5f;                           // 直径あたりの六角形の数
        float px = x * cells, py = y * cells;
        const float rx = 1f, ry = 1.7320508f;             // 六角形の並び（横1・縦√3）
        float ax = Mod(px, rx) - rx * 0.5f, ay = Mod(py, ry) - ry * 0.5f;
        float bx = Mod(px - rx * 0.5f, rx) - rx * 0.5f, by = Mod(py - ry * 0.5f, ry) - ry * 0.5f;
        float gx, gy;
        if (ax * ax + ay * ay < bx * bx + by * by) { gx = ax; gy = ay; } else { gx = bx; gy = by; }
        gx = Mathf.Abs(gx); gy = Mathf.Abs(gy);
        float hexDist = Mathf.Max(gx * 0.5f + gy * 0.8660254f, gx); // 中心から六角形の辺までの距離（辺で0.5）
        float edge = 0.5f - hexDist;
        float line = Mathf.Clamp01(1f - edge / 0.05f);    // 辺の近くだけ明るい線
        float rim = 0.35f + 0.65f * r * r;                // 縁に近いほど明るい
        float fade = Mathf.Clamp01((1f - r) / 0.06f);      // 円の外側の端はなめらかに0
        return line * rim * fade;
    }
    private static float Mod(float a, float b) => a - b * Mathf.Floor(a / b);

    // 最初のデザイン（PS_Orbs＝粒の集まりの円、PS_Ring＝中央から広がる泡）を残したまま、
    // ① 円の粒を回す ③ 内側の輪（PS_InnerRing）を追加 ・追加の粒用（PS_Fx）を追加 ・ShieldActiveFX を付ける、を行う。
    // round=true で丸いぼかしの粒、false で元の四角い粒（Sprite-Lit-Default）。何度実行しても部品は増えない
    private static void ApplyShieldBubble(bool round)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ShieldBubblePrefab) == null)
        {
            EditorUtility.DisplayDialog("弾の見た目", ShieldBubblePrefab + " が見つかりません。", "OK");
            return;
        }
        Material mat;
        if (round)
        {
            var softGlow = AssetDatabase.LoadAssetAtPath<Texture2D>(SoftGlow);
            if (softGlow == null || AssetDatabase.LoadMainAssetAtPath(MatSrc) == null)
            {
                EditorUtility.DisplayDialog("弾の見た目", "コピー元が見つかりません：" + SoftGlow + " / " + MatSrc, "OK");
                return;
            }
            if (!AssetDatabase.IsValidFolder("Assets/Generated")) AssetDatabase.CreateFolder("Assets", "Generated");
            if (!AssetDatabase.IsValidFolder(GenDir)) AssetDatabase.CreateFolder("Assets/Generated", "VFX");
            mat = AssetDatabase.LoadAssetAtPath<Material>(MatShieldDot);
            if (mat == null) mat = MakeMat(MatShieldDot, softGlow, true);
        }
        else
        {
            mat = AssetDatabase.LoadAssetAtPath<Material>(SpriteLitDefault);
            if (mat == null) { EditorUtility.DisplayDialog("弾の見た目", "元のマテリアルが見つかりません：" + SpriteLitDefault, "OK"); return; }
        }
        if (!EditorUtility.DisplayDialog("弾の見た目：シールドの泡",
                ShieldBubblePrefab + " を派手にします（最初のデザインはそのまま）。\n" +
                "・バリアの円（PS_Orbs）の粒を円周に沿って回す\n・六角形の網目（Hex・HexRipple）と追加の粒用（PS_Fx）を追加（既にあれば作り直さない）\n" +
                "・内側の輪（PS_InnerRing）があれば削除\n" +
                "・構成は「外の円＋中央からの泡＋六角形の網目」＋シールド量に合わせた色の変化（他の追加演出はOFF。ShieldActiveFXで個別にONにできる）\n" +
                "・粒の形：" + (round ? "丸いぼかし（" + MatShieldDot + "）" : "四角（元のSprite-Lit-Default）") + "\n続けますか？", "実行", "キャンセル"))
            return;

        GameObject root = PrefabUtility.LoadPrefabContents(ShieldBubblePrefab);
        string log;
        try
        {
            var ringT = root.transform.Find("PS_Orbs");
            var waveT = root.transform.Find("PS_Ring");
            if (ringT == null || waveT == null) { EditorUtility.DisplayDialog("弾の見た目", "PS_Orbs / PS_Ring が見つかりません。", "OK"); return; }
            var ring = ringT.GetComponent<ParticleSystem>();
            var wave = waveT.GetComponent<ParticleSystem>();
            // ① バリアの円の粒を円周に沿って回す
            SetOrbitalZ(ring, 0.5f);

            // ---- 外の円（PS_Orbs） ----
            // A1 うねる円：ノイズで粒をふわふわ揺らす（品質は低＝軽い）
            {
                var nz = ring.noise;
                nz.enabled = true;
                nz.quality = ParticleSystemNoiseQuality.Low;
                nz.strength = new ParticleSystem.MinMaxCurve(0.12f);
                nz.frequency = 1.2f;
                nz.scrollSpeed = new ParticleSystem.MinMaxCurve(0.4f);
                nz.damping = true;
                nz.octaveCount = 1;
            }
            // A5 粒の大きさのばらつき（元の大きさの0.6〜1.6倍。何度実行しても元の大きさから計算する）
            SetSizeSpread(ring, 0.6f, 1.6f);

            // ---- 中央の泡（PS_Ring） ----
            // B1 渦を巻いて広がる
            SetOrbitalZ(wave, 1.5f);
            // B2 中心は白、外ほど青 ／ B3 外の円に届く直前に薄れて溶け込む
            {
                var col = wave.colorOverLifetime;
                col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0.55f, 0.88f, 1f), 0.55f), new GradientColorKey(new Color(0.3f, 0.55f, 1f), 1f) },
                          new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
                col.color = new ParticleSystem.MinMaxGradient(g);
                // B3 届く直前に少し大きくなる
                var sz = wave.sizeOverLifetime;
                sz.enabled = true;
                sz.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.75f, 1f), new Keyframe(1f, 1.8f)));
            }
            // B4 泡の粒の大きさのばらつき（元の大きさの0.5〜1.7倍）
            SetSizeSpread(wave, 0.5f, 1.7f);

            // 追加の粒用（PS_Orbsを複製し、自分では出さず、ShieldActiveFXがEmitで出す。泡と同じ大きさ・位置の基準になる）
            var fxT = root.transform.Find("PS_Fx");
            if (fxT == null)
            {
                var go = Object.Instantiate(ring.gameObject, root.transform);
                go.name = "PS_Fx";
                fxT = go.transform;
            }
            var fx = fxT.GetComponent<ParticleSystem>();
            {
                var em = fx.emission; em.enabled = false;
                var sh = fx.shape; sh.enabled = false;
                var vel = fx.velocityOverLifetime; vel.enabled = false;
                var mm = fx.main; mm.maxParticles = 1500; mm.loop = true; mm.playOnAwake = true;
                var col = fx.colorOverLifetime; col.enabled = true;
                var g = new Gradient();
                g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                          new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.3f), new GradientAlphaKey(0f, 1f) });
                col.color = new ParticleSystem.MinMaxGradient(g);
                var sz = fx.sizeOverLifetime; sz.enabled = false;
            }

            // 内側の輪は不要（ユーザー指示）。あれば削除
            var oldInner = root.transform.Find("PS_InnerRing");
            if (oldInner != null) Object.DestroyImmediate(oldInner.gameObject);

            // ③ 六角形の網目と、⑧ 当たった所の六角形の波紋（加算の画像。並び順はバリアの円の手前・奥）
            if (AssetDatabase.LoadMainAssetAtPath(TexHex) == null) WriteTex(TexHex, 256, 256, HexPixel, true);
            var hexSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexHex);
            var spriteAddMat = AssetDatabase.LoadAssetAtPath<Material>(SpriteAdd);
            var ringR = ring.GetComponent<ParticleSystemRenderer>();
            SpriteRenderer MakeHex(string name, int orderOffset)
            {
                var tr = root.transform.Find(name);
                if (tr == null) { var go = new GameObject(name); go.layer = root.layer; tr = go.transform; tr.SetParent(root.transform, false); }
                var sr = tr.GetComponent<SpriteRenderer>();
                if (sr == null) sr = tr.gameObject.AddComponent<SpriteRenderer>();
                sr.sprite = hexSprite;
                if (spriteAddMat != null) sr.sharedMaterial = spriteAddMat;
                sr.sortingLayerID = ringR != null ? ringR.sortingLayerID : 0;
                sr.sortingOrder = (ringR != null ? ringR.sortingOrder : 10) + orderOffset;
                return sr;
            }
            var hexSr = MakeHex("Hex", -1);
            var rippleSr = MakeHex("HexRipple", 1);
            rippleSr.enabled = false;

            // 部品
            var comp = root.GetComponent<ShieldActiveFX>();
            if (comp == null) comp = root.AddComponent<ShieldActiveFX>();
            comp.ringPS = ring; comp.wavePS = wave; comp.fxPS = fx;
            comp.hex = hexSr; comp.hexRipple = rippleSr;
            // 構成は「外の円（PS_Orbs）＋中央からの泡（PS_Ring）＋六角形の網目（Hex）」だけ（ユーザー指示）。
            // それ以外の追加演出（きらめき・残量の色・脈打つ波紋・輪の光・中心の光・当たった時・全快・回復中）はOFFにする
            comp.hexEnabled = true;
            comp.hexAlpha = 0.2f;               // 六角形の網目の濃さ（0.32は目立ちすぎ、0.12はほぼ見えない：ユーザー確認）
            comp.twinkleEnabled = false;
            comp.ratioLookEnabled = true;       // シールド量に合わせた色の変化（ユーザー指示で追加）
            comp.ratioDensityEnabled = false;   // 粒がまばらになる方は使わない
            comp.waveReachRing = true;          // 中央の泡を外の円の位置まで届かせる
            comp.twoToneEnabled = true;         // A3 2色のドット
            comp.spillEnabled = true;           // A4 エネルギーのこぼれ
            comp.pulseEnabled = false;
            comp.ringFlashEnabled = false;
            comp.coreEnabled = false;
            comp.hitEnabled = false;
            comp.hexRippleEnabled = false;
            comp.restoreEnabled = false;
            comp.recoverEnabled = false;
            comp.ringRadius = ring.shape.radius;
            comp.dotSizeMul = round ? 3f : 1f;

            // 粒の形（全てのパーティクル）
            int n = 0;
            foreach (var r in root.GetComponentsInChildren<ParticleSystemRenderer>(true)) { r.sharedMaterial = mat; n++; }
            EditorUtility.SetDirty(comp);
            PrefabUtility.SaveAsPrefabAsset(root, ShieldBubblePrefab);
            log = "粒の形：" + (round ? "丸いぼかし" : "四角") + "（パーティクル" + n + "個）、粒の大きさの倍率 " + comp.dotSizeMul + "、六角形の網目あり";
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Debug.Log("[BulletFXSetupTool] シールドの泡：" + log);
        EditorUtility.DisplayDialog("弾の見た目", "完了しました。\n" + log + "\n次にエネミーが出てきた時から反映されます。", "OK");
    }

    // 粒の大きさにばらつきを付ける（元の大きさ×min〜max）。既にばらつきがある時は、前回の設定から元の大きさを戻して計算する
    private static void SetSizeSpread(ParticleSystem ps, float min, float max)
    {
        var m = ps.main;
        var cur = m.startSize;
        float baseSize;
        if (cur.mode == ParticleSystemCurveMode.TwoConstants && cur.constantMin > 0f && cur.constantMax > cur.constantMin)
            baseSize = (cur.constantMin + cur.constantMax) / (min + max); // 前回もこの関数で設定した時の元の大きさ
        else baseSize = cur.constant;
        m.startSize = new ParticleSystem.MinMaxCurve(baseSize * min, baseSize * max);
    }

    // 円周に沿って回す（x/y/z と orbital を全て同じ「定数」の形で明示的に設定する）
    private static void SetOrbitalZ(ParticleSystem ps, float radPerSec)
    {
        var v = ps.velocityOverLifetime;
        v.enabled = true;
        v.space = ParticleSystemSimulationSpace.Local;
        v.x = new ParticleSystem.MinMaxCurve(0f);
        v.y = new ParticleSystem.MinMaxCurve(0f);
        v.z = new ParticleSystem.MinMaxCurve(0f);
        v.orbitalX = new ParticleSystem.MinMaxCurve(0f);
        v.orbitalY = new ParticleSystem.MinMaxCurve(0f);
        v.orbitalZ = new ParticleSystem.MinMaxCurve(radPerSec);
        v.radial = new ParticleSystem.MinMaxCurve(0f);
        v.speedModifier = new ParticleSystem.MinMaxCurve(1f);
    }

    // ---------- エネミーの予告線（TelegraphFXManager） ----------
    private const string TelegraphPrefabPath = "Assets/Resources/TelegraphFX.prefab";

    [MenuItem("Tools/弾の見た目/15 エネミーの予告線を派手にする（作成）")]
    private static void CreateTelegraphFx()
    {
        var softGlow = AssetDatabase.LoadAssetAtPath<Texture2D>(SoftGlow);
        var spriteAdd = AssetDatabase.LoadAssetAtPath<Material>(SpriteAdd);
        if (softGlow == null || spriteAdd == null || AssetDatabase.LoadMainAssetAtPath(MatSrc) == null)
        {
            EditorUtility.DisplayDialog("弾の見た目", "コピー元が見つかりません：" + SoftGlow + " / " + SpriteAdd + " / " + MatSrc, "OK");
            return;
        }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(TelegraphPrefabPath) != null)
        {
            EditorUtility.DisplayDialog("弾の見た目", TelegraphPrefabPath + " は既にあります（作り直しません）。設定はこのプレハブのInspectorで調整してください。", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("弾の見た目：エネミーの予告線",
                "次を作成します：" + TelegraphPrefabPath + "（予告線の管理役）。作成後、Zephyr・WalkerMech・NeonMonster03・IronNest_NM02・雷雲（Shaman・GuardBeast）の予告線に、芯と光・流れる光・チャージ光・照準リング等が付きます。続けますか？", "作成", "キャンセル"))
            return;

        if (!AssetDatabase.IsValidFolder("Assets/Generated")) AssetDatabase.CreateFolder("Assets", "Generated");
        if (!AssetDatabase.IsValidFolder(GenDir)) AssetDatabase.CreateFolder("Assets/Generated", "VFX");
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        if (AssetDatabase.LoadMainAssetAtPath(TexGlow) == null)
            WriteTex(TexGlow, 128, 128, (x, y) => { float r2 = x * x + y * y; return Mathf.Exp(-r2 / 0.18f) * Mathf.Clamp01((1f - Mathf.Sqrt(r2)) / 0.15f); }, true);
        if (AssetDatabase.LoadMainAssetAtPath(TexRing) == null)
            WriteTex(TexRing, 128, 128, (x, y) => { float r = Mathf.Sqrt(x * x + y * y); return (Mathf.Exp(-Mathf.Pow((r - 0.8f) / 0.06f, 2f)) + Mathf.Exp(-Mathf.Pow((r - 0.78f) / 0.18f, 2f)) * 0.3f) * Mathf.Clamp01((1f - r) / 0.06f); }, true);
        var lineTex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexLine);
        if (lineTex == null) lineTex = WriteTex(TexLine, 32, 32, (x, y) => Mathf.Exp(-y * y / 0.15f), false);
        var matLine = AssetDatabase.LoadAssetAtPath<Material>(MatLine);
        if (matLine == null) matLine = MakeMat(MatLine, lineTex, true);

        var root = new GameObject("TelegraphFX");
        try
        {
            var m = root.AddComponent<TelegraphFXManager>();
            m.glowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexGlow);
            m.ringSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexRing);
            m.additiveSpriteMaterial = spriteAdd;
            m.lineMaterial = matLine;
            PrefabUtility.SaveAsPrefabAsset(root, TelegraphPrefabPath);
        }
        finally { Object.DestroyImmediate(root); }
        AssetDatabase.SaveAssets();
        Debug.Log("[BulletFXSetupTool] " + TelegraphPrefabPath + " を作成しました");
        EditorUtility.DisplayDialog("弾の見た目", "作成しました。" + TelegraphPrefabPath + "　全体のON/OFFや①〜⑦の各演出は、このプレハブのTelegraphFXManagerで調整できます。", "OK");
    }

    [MenuItem("Tools/弾の見た目/6 シールド破壊を派手にする（作成）")]
    private static void CreateShield()
    {
        var softGlow = AssetDatabase.LoadAssetAtPath<Texture2D>(SoftGlow);
        var spriteAdd = AssetDatabase.LoadAssetAtPath<Material>(SpriteAdd);
        if (softGlow == null || spriteAdd == null || AssetDatabase.LoadMainAssetAtPath(MatSrc) == null)
        {
            EditorUtility.DisplayDialog("弾の見た目", "コピー元が見つかりません：" + SoftGlow + " / " + SpriteAdd + " / " + MatSrc, "OK");
            return;
        }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ShieldPrefabPath) != null)
        {
            EditorUtility.DisplayDialog("弾の見た目", ShieldPrefabPath + " は既にあります（作り直しません）。設定はこのプレハブのInspectorで調整してください。", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("弾の見た目：シールド破壊を作成",
                "次を作成します：" + ShieldPrefabPath + "（管理役・共有パーティクル）と破片の画像（" + TexShard + "）。" +
                "作成後、シールドが壊れた時は旧VFX（VFX_ShieldBreak）の代わりに新しい演出が出ます。続けますか？", "作成", "キャンセル"))
            return;

        if (!AssetDatabase.IsValidFolder("Assets/Generated")) AssetDatabase.CreateFolder("Assets", "Generated");
        if (!AssetDatabase.IsValidFolder(GenDir)) AssetDatabase.CreateFolder("Assets/Generated", "VFX");
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");

        if (AssetDatabase.LoadMainAssetAtPath(TexGlow) == null)
            WriteTex(TexGlow, 128, 128, (x, y) => { float r2 = x * x + y * y; return Mathf.Exp(-r2 / 0.18f) * Mathf.Clamp01((1f - Mathf.Sqrt(r2)) / 0.15f); }, true);
        if (AssetDatabase.LoadMainAssetAtPath(TexRing) == null)
            WriteTex(TexRing, 128, 128, (x, y) => { float r = Mathf.Sqrt(x * x + y * y); return (Mathf.Exp(-Mathf.Pow((r - 0.8f) / 0.06f, 2f)) + Mathf.Exp(-Mathf.Pow((r - 0.78f) / 0.18f, 2f)) * 0.3f) * Mathf.Clamp01((1f - r) / 0.06f); }, true);
        // ガラス片：先の尖った不揃いな三角形（縁を少しぼかし、中央を明るく）
        if (AssetDatabase.LoadMainAssetAtPath(TexShard) == null)
            WriteTex(TexShard, 64, 64, (x, y) =>
            {
                float t = (y + 0.7f) / 1.65f; // 0＝底辺 1＝先端
                if (t < 0f || t > 1f) return 0f;
                float half = (1f - t) * 0.6f;
                float cx = 0.15f * t;
                float edge = Mathf.Clamp01((half - Mathf.Abs(x - cx)) / 0.06f) * Mathf.Clamp01(t / 0.05f);
                return edge * (0.55f + 0.45f * (1f - Mathf.Abs(x - cx) / Mathf.Max(0.01f, half)));
            }, true);
        var lineTex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexLine);
        if (lineTex == null) lineTex = WriteTex(TexLine, 32, 32, (x, y) => Mathf.Exp(-y * y / 0.15f), false);
        var matDot = AssetDatabase.LoadAssetAtPath<Material>(MatDot);
        if (matDot == null) matDot = MakeMat(MatDot, softGlow, true);
        var matLine = AssetDatabase.LoadAssetAtPath<Material>(MatLine);
        if (matLine == null) matLine = MakeMat(MatLine, lineTex, true);

        var root = new GameObject("ShieldFX");
        try
        {
            var m = root.AddComponent<ShieldBreakFXManager>();
            m.sparkle = Layer(root, "Sparkle", matDot, 30, ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
                Alpha(ps, new[] { 1f, 0f });
                Size(ps, 1f, 0.2f);
            });
            m.glowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexGlow);
            m.ringSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexRing);
            m.shardSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexShard);
            m.additiveSpriteMaterial = spriteAdd;
            m.lineMaterial = matLine;

            PrefabUtility.SaveAsPrefabAsset(root, ShieldPrefabPath);
        }
        finally { Object.DestroyImmediate(root); }
        AssetDatabase.SaveAssets();
        Debug.Log("[BulletFXSetupTool] " + ShieldPrefabPath + " を作成しました");
        EditorUtility.DisplayDialog("弾の見た目", "作成しました。" + ShieldPrefabPath + "　全体のON/OFFや各演出は、このプレハブのShieldBreakFXManagerで調整できます。", "OK");
    }

    // ---------- アイテム取得（ItemFXManager） ----------
    private const string ItemPrefabPath = "Assets/Resources/ItemFX.prefab";
    private const string TexStar = GenDir + "/ItemFX_Star.png";

    [MenuItem("Tools/弾の見た目/7 アイテム取得を派手にする（作成）")]
    private static void CreateItemFx()
    {
        var softGlow = AssetDatabase.LoadAssetAtPath<Texture2D>(SoftGlow);
        var spriteAdd = AssetDatabase.LoadAssetAtPath<Material>(SpriteAdd);
        if (softGlow == null || spriteAdd == null || AssetDatabase.LoadMainAssetAtPath(MatSrc) == null)
        {
            EditorUtility.DisplayDialog("弾の見た目", "コピー元が見つかりません：" + SoftGlow + " / " + SpriteAdd + " / " + MatSrc, "OK");
            return;
        }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ItemPrefabPath) != null)
        {
            EditorUtility.DisplayDialog("弾の見た目", ItemPrefabPath + " は既にあります（作り直しません）。設定はこのプレハブのInspectorで調整してください。", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("弾の見た目：アイテム取得を作成",
                "次を作成します：" + ItemPrefabPath + "（管理役・共有パーティクル）と星の画像（" + TexStar + "）。" +
                "作成後、アイテム（Gold・Life）を取った時に新しい演出が重なって出ます。続けますか？", "作成", "キャンセル"))
            return;

        if (!AssetDatabase.IsValidFolder("Assets/Generated")) AssetDatabase.CreateFolder("Assets", "Generated");
        if (!AssetDatabase.IsValidFolder(GenDir)) AssetDatabase.CreateFolder("Assets/Generated", "VFX");
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");

        if (AssetDatabase.LoadMainAssetAtPath(TexGlow) == null)
            WriteTex(TexGlow, 128, 128, (x, y) => { float r2 = x * x + y * y; return Mathf.Exp(-r2 / 0.18f) * Mathf.Clamp01((1f - Mathf.Sqrt(r2)) / 0.15f); }, true);
        if (AssetDatabase.LoadMainAssetAtPath(TexRing) == null)
            WriteTex(TexRing, 128, 128, (x, y) => { float r = Mathf.Sqrt(x * x + y * y); return (Mathf.Exp(-Mathf.Pow((r - 0.8f) / 0.06f, 2f)) + Mathf.Exp(-Mathf.Pow((r - 0.78f) / 0.18f, 2f)) * 0.3f) * Mathf.Clamp01((1f - r) / 0.06f); }, true);
        // 4方向に光る星（縦横の細い光の筋＋中心の光）
        if (AssetDatabase.LoadMainAssetAtPath(TexStar) == null)
            WriteTex(TexStar, 128, 128, (x, y) =>
            {
                float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
                float armH = Mathf.Exp(-ay * ay / 0.0015f) * Mathf.Clamp01(1f - ax);
                float armV = Mathf.Exp(-ax * ax / 0.0015f) * Mathf.Clamp01(1f - ay);
                float core = Mathf.Exp(-(x * x + y * y) / 0.02f);
                return Mathf.Clamp01(armH + armV + core);
            }, true);
        var matDot = AssetDatabase.LoadAssetAtPath<Material>(MatDot);
        if (matDot == null) matDot = MakeMat(MatDot, softGlow, true);

        var root = new GameObject("ItemFX");
        try
        {
            var m = root.AddComponent<ItemFXManager>();
            m.sparkle = Layer(root, "Sparkle", matDot, 41, ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
                Alpha(ps, new[] { 1f, 0f });
                Size(ps, 1f, 0.3f);
            });
            m.glowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexGlow);
            m.ringSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexRing);
            m.starSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexStar);
            m.additiveSpriteMaterial = spriteAdd;
            PrefabUtility.SaveAsPrefabAsset(root, ItemPrefabPath);
        }
        finally { Object.DestroyImmediate(root); }
        AssetDatabase.SaveAssets();
        Debug.Log("[BulletFXSetupTool] " + ItemPrefabPath + " を作成しました");
        EditorUtility.DisplayDialog("弾の見た目", "作成しました。" + ItemPrefabPath + "　全体のON/OFFや各演出は、このプレハブのItemFXManagerで調整できます。", "OK");
    }

    // ---------- ダンサー・フロアの被弾（PlayerHitFXManager） ----------
    private const string PlayerHitPrefabPath = "Assets/Resources/PlayerHitFX.prefab";

    [MenuItem("Tools/弾の見た目/11 ダンサー・フロアの被弾を派手にする（作成）")]
    private static void CreatePlayerHitFx()
    {
        var softGlow = AssetDatabase.LoadAssetAtPath<Texture2D>(SoftGlow);
        var spriteAdd = AssetDatabase.LoadAssetAtPath<Material>(SpriteAdd);
        if (softGlow == null || spriteAdd == null || AssetDatabase.LoadMainAssetAtPath(MatSrc) == null)
        {
            EditorUtility.DisplayDialog("弾の見た目", "コピー元が見つかりません：" + SoftGlow + " / " + SpriteAdd + " / " + MatSrc, "OK");
            return;
        }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PlayerHitPrefabPath) != null)
        {
            EditorUtility.DisplayDialog("弾の見た目", PlayerHitPrefabPath + " は既にあります（作り直しません）。設定はこのプレハブのInspectorで調整してください。", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("弾の見た目：ダンサー・フロアの被弾",
                "次を作成します：" + PlayerHitPrefabPath + "（管理役・共有パーティクル3つ）。作成後、ダンサー・フロアが被弾した時に新しい演出が出ます（旧VFX_BulletDestroyの代わり）。続けますか？", "作成", "キャンセル"))
            return;

        if (!AssetDatabase.IsValidFolder("Assets/Generated")) AssetDatabase.CreateFolder("Assets", "Generated");
        if (!AssetDatabase.IsValidFolder(GenDir)) AssetDatabase.CreateFolder("Assets/Generated", "VFX");
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");

        if (AssetDatabase.LoadMainAssetAtPath(TexGlow) == null)
            WriteTex(TexGlow, 128, 128, (x, y) => { float r2 = x * x + y * y; return Mathf.Exp(-r2 / 0.18f) * Mathf.Clamp01((1f - Mathf.Sqrt(r2)) / 0.15f); }, true);
        if (AssetDatabase.LoadMainAssetAtPath(TexRing) == null)
            WriteTex(TexRing, 128, 128, (x, y) => { float r = Mathf.Sqrt(x * x + y * y); return (Mathf.Exp(-Mathf.Pow((r - 0.8f) / 0.06f, 2f)) + Mathf.Exp(-Mathf.Pow((r - 0.78f) / 0.18f, 2f)) * 0.3f) * Mathf.Clamp01((1f - r) / 0.06f); }, true);
        var lineTex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexLine);
        if (lineTex == null) lineTex = WriteTex(TexLine, 32, 32, (x, y) => Mathf.Exp(-y * y / 0.15f), false);
        var matDot = AssetDatabase.LoadAssetAtPath<Material>(MatDot);
        if (matDot == null) matDot = MakeMat(MatDot, softGlow, true);
        var matSmoke = AssetDatabase.LoadAssetAtPath<Material>(MatSmoke);
        if (matSmoke == null) matSmoke = MakeMat(MatSmoke, softGlow, false);
        var matLine = AssetDatabase.LoadAssetAtPath<Material>(MatLine);
        if (matLine == null) matLine = MakeMat(MatLine, lineTex, true);

        var root = new GameObject("PlayerHitFX");
        try
        {
            var m = root.AddComponent<PlayerHitFXManager>();
            m.sparkle = Layer(root, "Sparkle", matDot, 36, ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
                Alpha(ps, new[] { 1f, 0f });
                Size(ps, 1f, 0.3f);
            });
            m.debris = Layer(root, "Debris", matSmoke, 35, ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.75f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
                main.gravityModifier = 1.4f;
                Alpha(ps, new[] { 1f, 1f, 0f });
                Size(ps, 1f, 0.8f);
            });
            m.smoke = Layer(root, "Smoke", matSmoke, 34, ps =>
            {
                var main = ps.main;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 0.9f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
                Alpha(ps, new[] { 0f, 1f, 0f });
                Size(ps, 0.6f, 1.5f);
            });
            m.glowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexGlow);
            m.ringSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexRing);
            m.additiveSpriteMaterial = spriteAdd;
            m.lineMaterial = matLine;
            PrefabUtility.SaveAsPrefabAsset(root, PlayerHitPrefabPath);
        }
        finally { Object.DestroyImmediate(root); }
        AssetDatabase.SaveAssets();
        Debug.Log("[BulletFXSetupTool] " + PlayerHitPrefabPath + " を作成しました");
        EditorUtility.DisplayDialog("弾の見た目", "作成しました。" + PlayerHitPrefabPath + "　全体のON/OFFや各演出は、このプレハブのPlayerHitFXManagerで調整できます。", "OK");
    }

    // ---------- 生成した光の画像の形の修正 ----------
    [MenuItem("Tools/弾の見た目/10 光の画像が角ばって切れるのを直す")]
    private static void FixGeneratedSpriteMesh()
    {
        const string dir = GenDir;
        var targets = new System.Collections.Generic.List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { dir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp == null || imp.textureType != TextureImporterType.Sprite) continue;
            var ts = new TextureImporterSettings();
            imp.ReadTextureSettings(ts);
            if (ts.spriteMeshType != SpriteMeshType.FullRect) targets.Add(path);
        }
        if (targets.Count == 0) { EditorUtility.DisplayDialog("弾の見た目", dir + " のスプライトはすべて Full Rect です（変更なし）。", "OK"); return; }
        if (!EditorUtility.DisplayDialog("弾の見た目：光の画像の形",
                dir + " のスプライト " + targets.Count + " 枚の Mesh Type を Tight → Full Rect に変えます" + BSN + string.Join(BSN, targets) + BSN + "画像そのもの・大きさは変わりません。続けますか？", "実行", "キャンセル"))
            return;
        foreach (string path in targets)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            var ts = new TextureImporterSettings();
            imp.ReadTextureSettings(ts);
            ts.spriteMeshType = SpriteMeshType.FullRect;
            imp.SetTextureSettings(ts);
            imp.SaveAndReimport();
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[BulletFXSetupTool] Full Rect に変更：" + string.Join(", ", targets));
        EditorUtility.DisplayDialog("弾の見た目", targets.Count + " 枚を Full Rect に変更しました。", "OK");
    }
    private const string BSN = "\n";

    // ---------- ジャスト反射弾の先端の粒（JustBulletVFX） ----------
    private const string JustVfxPrefabPath = "Assets/Prefabs/Effects/JustBulletVFX.prefab";
    private const string MatJustDot = GenDir + "/Mat_JustVFX_Dot.mat";

    // JustBulletVFX（矢じり・リング・螺旋・衝撃波）のパーティクルは画像なしのマテリアル（Sprite-Lit-Default）だったため粒が四角く描かれていた。
    // 丸くぼかした画像のマテリアル（通常の重ね方＝従来と同じ明るさの付き方）に差し替える
    [MenuItem("Tools/弾の見た目/9 ジャスト反射弾の先端の粒を丸くする")]
    private static void FixJustVfxDots()
    {
        var softGlow = AssetDatabase.LoadAssetAtPath<Texture2D>(SoftGlow);
        if (softGlow == null || AssetDatabase.LoadMainAssetAtPath(MatSrc) == null || AssetDatabase.LoadAssetAtPath<GameObject>(JustVfxPrefabPath) == null)
        {
            EditorUtility.DisplayDialog("弾の見た目", "必要なファイルが見つかりません：" + SoftGlow + " / " + MatSrc + " / " + JustVfxPrefabPath, "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("弾の見た目：ジャスト反射弾の先端の粒",
                JustVfxPrefabPath + " の4つのパーティクル（Arrowhead_PS / FlashRing_PS / Drill_PS / Shockwave_PS）のマテリアルを、丸い粒のマテリアル（" + MatJustDot + "）に差し替えます。粒の大きさ・数・色・動きは変わりません。続けますか？", "実行", "キャンセル"))
            return;

        if (!AssetDatabase.IsValidFolder("Assets/Generated")) AssetDatabase.CreateFolder("Assets", "Generated");
        if (!AssetDatabase.IsValidFolder(GenDir)) AssetDatabase.CreateFolder("Assets/Generated", "VFX");
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatJustDot);
        if (mat == null) mat = MakeMat(MatJustDot, softGlow, false);

        GameObject root = PrefabUtility.LoadPrefabContents(JustVfxPrefabPath);
        int n = 0;
        try
        {
            foreach (var r in root.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                r.sharedMaterial = mat;
                n++;
            }
            PrefabUtility.SaveAsPrefabAsset(root, JustVfxPrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Debug.Log("[BulletFXSetupTool] JustBulletVFX のパーティクル " + n + " 個のマテリアルを " + MatJustDot + " に差し替えました");
        EditorUtility.DisplayDialog("弾の見た目", "差し替えました（" + n + "個）。", "OK");
    }

    // ---------- ブロック等の破壊（BreakFXManager） ----------
    private const string BreakPrefabPath = "Assets/Resources/BreakFX.prefab";
    private const string TexMetal = GenDir + "/BreakFX_Metal.png";

    // 機械・霊火の種類と色を設定するプレハブ（ここに無いWallHealthは「Block＝本物のブロック」のまま）
    private static readonly (string path, int style, Color color, string label)[] BreakStyleTargets =
    {
        ("Assets/Prefabs/Enemies/Bit.prefab",        1, new Color(0.9f, 0.75f, 1f, 1f),  "ObeliskのBit：機械（紫）"),
        ("Assets/Prefabs/Enemies/Obelisk.prefab",    1, new Color(0.9f, 0.75f, 1f, 1f),  "ObeliskのWeakPoint：機械（紫）"),
        ("Assets/Prefabs/Enemies/Zephyr.prefab",     1, new Color(0.45f, 1f, 0.85f, 1f), "ZephyrのBitL/BitR：機械（青緑）"),
        ("Assets/Prefabs/Enemies/NeonDancer.prefab", 1, new Color(1f, 0.45f, 0.85f, 1f), "NeonDancerのND_Floor/ND_LightLeft/ND_LightRight：機械（ネオンピンク）"),
        ("Assets/Prefabs/Effects/Shaman_SpiritFlame.prefab", 2, new Color(0.55f, 0.85f, 1f, 1f), "Shamanの霊火：霊火（青白）"),
    };

    [MenuItem("Tools/弾の見た目/8 ブロック等の破壊を派手にする（作成・設定）")]
    private static void CreateBreakFx()
    {
        var softGlow = AssetDatabase.LoadAssetAtPath<Texture2D>(SoftGlow);
        var spriteAdd = AssetDatabase.LoadAssetAtPath<Material>(SpriteAdd);
        if (softGlow == null || spriteAdd == null || AssetDatabase.LoadMainAssetAtPath(MatSrc) == null)
        {
            EditorUtility.DisplayDialog("弾の見た目", "コピー元が見つかりません：" + SoftGlow + " / " + SpriteAdd + " / " + MatSrc, "OK");
            return;
        }
        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(BreakPrefabPath) != null;
        var sb = new System.Text.StringBuilder();
        foreach (var t in BreakStyleTargets) sb.Append("・").Append(t.label).Append("\n");
        if (!EditorUtility.DisplayDialog("弾の見た目：ブロック等の破壊",
                (exists ? BreakPrefabPath + " は既にあるので作り直しません。\n" : "次を作成します：" + BreakPrefabPath + "（管理役・共有パーティクル4つ）と金属片の画像（" + TexMetal + "）。\n") +
                "次のプレハブの WallHealth > Break Fx Style / Break Fx Color を設定します（それ以外は「本物のブロック」のまま）：\n" + sb +
                "作成後、壊れた時は旧VFXの代わりに新しい演出が出ます。続けますか？", "実行", "キャンセル"))
            return;

        if (!AssetDatabase.IsValidFolder("Assets/Generated")) AssetDatabase.CreateFolder("Assets", "Generated");
        if (!AssetDatabase.IsValidFolder(GenDir)) AssetDatabase.CreateFolder("Assets/Generated", "VFX");
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");

        if (!exists)
        {
            if (AssetDatabase.LoadMainAssetAtPath(TexGlow) == null)
                WriteTex(TexGlow, 128, 128, (x, y) => { float r2 = x * x + y * y; return Mathf.Exp(-r2 / 0.18f) * Mathf.Clamp01((1f - Mathf.Sqrt(r2)) / 0.15f); }, true);
            if (AssetDatabase.LoadMainAssetAtPath(TexRing) == null)
                WriteTex(TexRing, 128, 128, (x, y) => { float r = Mathf.Sqrt(x * x + y * y); return (Mathf.Exp(-Mathf.Pow((r - 0.8f) / 0.06f, 2f)) + Mathf.Exp(-Mathf.Pow((r - 0.78f) / 0.18f, 2f)) * 0.3f) * Mathf.Clamp01((1f - r) / 0.06f); }, true);
            // 金属片：細長い板（縁は少し暗く、中央に光沢）
            if (AssetDatabase.LoadMainAssetAtPath(TexMetal) == null)
                WriteTex(TexMetal, 64, 64, (x, y) =>
                {
                    float ax = Mathf.Abs(x), ay = Mathf.Abs(y + x * 0.25f);
                    if (ax > 0.9f || ay > 0.28f) return 0f;
                    float edge = Mathf.Clamp01((0.9f - ax) / 0.08f) * Mathf.Clamp01((0.28f - ay) / 0.06f);
                    return edge;
                }, true);
            var lineTex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexLine);
            if (lineTex == null) lineTex = WriteTex(TexLine, 32, 32, (x, y) => Mathf.Exp(-y * y / 0.15f), false);
            var matDot = AssetDatabase.LoadAssetAtPath<Material>(MatDot);
            if (matDot == null) matDot = MakeMat(MatDot, softGlow, true);
            var matSmoke = AssetDatabase.LoadAssetAtPath<Material>(MatSmoke);
            if (matSmoke == null) matSmoke = MakeMat(MatSmoke, softGlow, false);
            var matLine = AssetDatabase.LoadAssetAtPath<Material>(MatLine);
            if (matLine == null) matLine = MakeMat(MatLine, lineTex, true);

            var root = new GameObject("BreakFX");
            try
            {
                var m = root.AddComponent<BreakFXManager>();
                m.sparkle = Layer(root, "Sparkle", matDot, 32, ps =>
                {
                    var main = ps.main;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.1f);
                    Alpha(ps, new[] { 1f, 0f });
                    Size(ps, 1f, 0.3f);
                });
                m.smoke = Layer(root, "Smoke", matSmoke, 29, ps =>
                {
                    var main = ps.main;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 0.9f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
                    Alpha(ps, new[] { 0f, 1f, 0f });
                    Size(ps, 0.6f, 1.6f);
                });
                m.pebbles = Layer(root, "Pebbles", matSmoke, 30, ps =>
                {
                    var main = ps.main;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
                    main.gravityModifier = 1.5f;
                    Alpha(ps, new[] { 1f, 1f, 0f });
                    Size(ps, 1f, 0.8f);
                });
                m.sparkRain = Layer(root, "SparkRain", matDot, 33, ps =>
                {
                    var main = ps.main;
                    main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
                    main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
                    main.gravityModifier = 1f;
                    Alpha(ps, new[] { 1f, 1f, 0f });
                    Size(ps, 1f, 0.4f);
                });
                m.glowSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexGlow);
                m.ringSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexRing);
                m.metalSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TexMetal);
                m.additiveSpriteMaterial = spriteAdd;
                m.lineMaterial = matLine;
                PrefabUtility.SaveAsPrefabAsset(root, BreakPrefabPath);
            }
            finally { Object.DestroyImmediate(root); }
        }

        // 各プレハブの WallHealth に種類と色を設定
        var log = new System.Text.StringBuilder();
        foreach (var t in BreakStyleTargets)
        {
            if (AssetDatabase.LoadMainAssetAtPath(t.path) == null) { log.Append("見つからない：").Append(t.path).Append("\n"); continue; }
            GameObject pr = PrefabUtility.LoadPrefabContents(t.path);
            try
            {
                int n = 0;
                foreach (var wh in pr.GetComponentsInChildren<WallHealth>(true))
                {
                    var so = new SerializedObject(wh);
                    so.FindProperty("breakFxStyle").enumValueIndex = t.style;
                    so.FindProperty("breakFxColor").colorValue = t.color;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    n++;
                }
                PrefabUtility.SaveAsPrefabAsset(pr, t.path);
                log.Append(t.label).Append("（").Append(n).Append("個）\n");
            }
            finally { PrefabUtility.UnloadPrefabContents(pr); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[BulletFXSetupTool] ブロック等の破壊演出：" + log);
        EditorUtility.DisplayDialog("弾の見た目", "完了しました。\n" + log + "調整は " + BreakPrefabPath + " の BreakFXManager と、各プレハブの WallHealth > Break Fx Style / Color で行えます。", "OK");
    }

    [MenuItem("Tools/弾の見た目/4 反射弾の演出のON・OFFを切り替える")]
    private static void ToggleReflect()
    {
        var m = AssetDatabase.LoadAssetAtPath<ReflectedBulletFXManager>(ReflectPrefabPath);
        if (m == null) { EditorUtility.DisplayDialog("弾の見た目", $"{ReflectPrefabPath} がありません（先に「3 作成」を実行してください）。", "OK"); return; }
        GameObject root = PrefabUtility.LoadPrefabContents(ReflectPrefabPath);
        bool now;
        try
        {
            var mm = root.GetComponent<ReflectedBulletFXManager>();
            mm.fxEnabled = !mm.fxEnabled;
            now = mm.fxEnabled;
            PrefabUtility.SaveAsPrefabAsset(root, ReflectPrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("弾の見た目", now ? "反射弾の演出をONにしました。" : "反射弾の演出をOFFにしました（従来の見た目）。", "OK");
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
}
