using UnityEditor;
using UnityEngine;

/// <summary>
/// Area6ボス「Shaman」用のセットアップメニュー。
/// </summary>
public static class ShamanSetupTool
{
    private const string ShamanPrefab = "Assets/Prefabs/Enemies/Shaman.prefab";
    private const string ShamanData = "Assets/GameData/Enemies/EnemyData_Shaman.asset";
    private const string FlamePrefabPath = "Assets/Prefabs/Effects/Shaman_SpiritFlame.prefab";
    private const string PlaceholderSpritePath = "Assets/Art/EnemyBullet/01_Normal.png";   // 仮の見た目（炎の色に着色）
    private const string FireSEPath = "Assets/Audio/Enemy/火炎魔法1.mp3";
    private const string SpiritFlameTypeName = "SpiritFlame";
    private static readonly Color FlameColor = new Color(1f, 0.55f, 0.15f, 1f);

    // ======================================================
    // 精霊の炎：
    //  ・火の玉のPrefab（Shaman_SpiritFlame）を作成（無ければ。仮の見た目＝既存の弾画像を炎の色に着色）
    //    SpriteRenderer＋CircleCollider2D＋Rigidbody2D(Kinematic)＋WallHealth（ブロックHP）＋ShamanSpiritFlame（壊れたSE＝火炎魔法1）
    //  ・EnemyData_Shamanに飛ばした火の玉の弾「SpiritFlame」を追加（0番のコピー・特殊な動きOFF・炎の色。既にあれば追加しない）
    //  ・Shaman.prefab（ルート）にShamanSpiritFlamesを追加し、Prefab・弾の番号・召喚SE（火炎魔法1）を設定
    // ======================================================
    [MenuItem("Tools/Shaman/精霊の炎を追加")]
    private static void AddSpiritFlames()
    {
        var data = AssetDatabase.LoadAssetAtPath<EnemyData>(ShamanData);
        var fireSE = AssetDatabase.LoadAssetAtPath<AudioClip>(FireSEPath);
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderSpritePath);
        if (data == null || data.bulletTypes == null || data.bulletTypes.Length == 0 || fireSE == null || sprite == null)
        {
            EditorUtility.DisplayDialog("Shaman", $"次のどれかが見つかりません：\n{ShamanData}\n{FireSEPath}\n{PlaceholderSpritePath}", "OK");
            return;
        }
        bool prefabExists = AssetDatabase.LoadAssetAtPath<GameObject>(FlamePrefabPath) != null;
        int typeIdx = FindTypeIndex(data, SpiritFlameTypeName);
        if (!EditorUtility.DisplayDialog("Shaman：精霊の炎を追加",
                "次の設定を行います：\n" +
                $"・火の玉のPrefab {FlamePrefabPath}{(prefabExists ? "（既にあるので作り直さない）" : "を作成（仮の見た目）")}\n" +
                $"・EnemyData_Shaman の Bullet Types に「{SpiritFlameTypeName}」{(typeIdx >= 0 ? $"（既にあり：{typeIdx}番）" : "を追加")}\n" +
                "・Shaman.prefab（ルート）に ShamanSpiritFlames を追加し、Prefab・弾の番号・召喚SE（火炎魔法1）を設定\n続けますか？", "実行", "キャンセル"))
            return;

        // ① 火の玉のPrefab
        if (!prefabExists) CreateFlamePrefab(sprite, fireSE);
        var flamePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FlamePrefabPath).GetComponent<ShamanSpiritFlame>();

        // ② 飛ばした火の玉の弾
        if (typeIdx < 0)
        {
            Undo.RecordObject(data, "Add Shaman SpiritFlame Bullet Type");
            var t = new EnemyData.BulletType();
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(data.bulletTypes[0]), t);
            t.name = SpiritFlameTypeName;
            t.useWaveMotion = false; t.useSpiralMotion = false; t.useMultiShot = false; t.useTelegraph = false;
            t.useMissileArc = false; t.useCountdownExplosion = false; t.useMultiWarhead = false; t.useSmokeGrenade = false;
            t.useWarp = false; t.useBeam = false; t.usePinnedReflect = false;
            t.useFireIntervalOverride = false; t.useFireIntervalRandom = false; t.useFirePauseCycle = false;
            t.useColorOverride = true;
            t.colorOverride = FlameColor;
            var list = new System.Collections.Generic.List<EnemyData.BulletType>(data.bulletTypes) { t };
            data.bulletTypes = list.ToArray();
            typeIdx = data.bulletTypes.Length - 1;
            EditorUtility.SetDirty(data);
        }

        // ③ Shaman.prefabに取り付け
        GameObject root = PrefabUtility.LoadPrefabContents(ShamanPrefab);
        try
        {
            if (root.GetComponent<ShamanController>() == null)
            {
                Debug.LogError("[ShamanSetupTool] Shaman.prefabのルートにShamanControllerがありません");
                return;
            }
            var sf = root.GetComponent<ShamanSpiritFlames>();
            if (sf == null) sf = root.AddComponent<ShamanSpiritFlames>();
            var so = new SerializedObject(sf);
            so.FindProperty("flamePrefab").objectReferenceValue = flamePrefab;
            so.FindProperty("bulletTypeIndex").intValue = typeIdx;
            so.FindProperty("summonSE").objectReferenceValue = fireSE;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, ShamanPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[ShamanSetupTool] 精霊の炎を設定しました（SpiritFlame={typeIdx}番）");
        EditorUtility.DisplayDialog("Shaman", $"設定しました。\nSpiritFlame＝{typeIdx}番", "OK");
    }

    // ======================================================
    // 精霊の炎の画像（火の玉精霊.png）を、回る火の玉（Shaman_SpiritFlame.prefab）と飛ばした弾（SpiritFlame）に設定する
    //  ・取り込み設定：1単位あたり700px（500pxの画像が約0.7の大きさ）
    //  ・回る火の玉：仮の着色・拡大をやめ、当たり判定は炎の玉の部分（半径0.25）
    //  ・飛ばした弾：画像を差し替え、仮の着色はOFF（当たり判定は通常の弾のまま）
    // ======================================================
    private const string FlameImagePath = "Assets/Art/Enemy/S3_06_Shaman/火の玉精霊.png";
    private const float FlameImagePixelsPerUnit = 700f;
    private const float FlameColliderRadius = 0.25f;

    [MenuItem("Tools/Shaman/精霊の炎の画像を設定")]
    private static void ApplyFlameImage()
    {
        var importer = AssetImporter.GetAtPath(FlameImagePath) as TextureImporter;
        var data = AssetDatabase.LoadAssetAtPath<EnemyData>(ShamanData);
        int typeIdx = data != null ? FindTypeIndex(data, SpiritFlameTypeName) : -1;
        if (importer == null || AssetDatabase.LoadAssetAtPath<GameObject>(FlamePrefabPath) == null || typeIdx < 0)
        {
            EditorUtility.DisplayDialog("Shaman", "火の玉精霊.png、Shaman_SpiritFlame.prefab、またはEnemyData_Shamanの「SpiritFlame」が見つかりません（先に「精霊の炎を追加」を実行してください）。", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("Shaman：精霊の炎の画像を設定",
                "次の設定を行います：\n" +
                $"・火の玉精霊.png：Pixels Per Unit {importer.spritePixelsToUnits} → {FlameImagePixelsPerUnit}（大きさ約0.7）\n" +
                $"・Shaman_SpiritFlame.prefab：画像を差し替え、仮の着色・拡大をやめる。当たり判定の半径{FlameColliderRadius}\n" +
                "・EnemyData_Shaman「SpiritFlame」：画像を差し替え、仮の着色をOFF\n続けますか？", "実行", "キャンセル"))
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsToUnits = FlameImagePixelsPerUnit;
        importer.SaveAndReimport();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(FlameImagePath);

        GameObject root = PrefabUtility.LoadPrefabContents(FlamePrefabPath);
        try
        {
            root.transform.localScale = Vector3.one;
            var sr = root.GetComponent<SpriteRenderer>();
            if (sr != null) { sr.sprite = sprite; sr.color = Color.white; }
            var col = root.GetComponent<CircleCollider2D>();
            if (col != null) { col.radius = FlameColliderRadius; col.offset = Vector2.zero; }
            PrefabUtility.SaveAsPrefabAsset(root, FlamePrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        Undo.RecordObject(data, "Shaman SpiritFlame Sprite");
        var t = data.bulletTypes[typeIdx];
        t.spriteOverride = sprite;
        t.useColorOverride = false;
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        Debug.Log("[ShamanSetupTool] 精霊の炎の画像を設定しました");
        EditorUtility.DisplayDialog("Shaman", "設定しました。", "OK");
    }

    // ======================================================
    // 精霊の炎をParticle Systemで作り直す：
    //  ・炎のParticleのPrefab（Shaman_FlameFX）を作成（無ければ）。マテリアルはMat_StardustParticle（加算）をコピーしてSoftGlowCircleを設定
    //  ・回る火の玉（Shaman_SpiritFlame.prefab）：画像（SpriteRenderer）を外し、子に炎のParticleを付ける。大きさ1・当たり判定の半径0.25
    //  ・Shaman.prefab > ShamanSpiritFlames：線で反射された火の玉の見た目（Flame Fx Prefab）を設定
    // ======================================================
    private const string FlameFxPrefabPath = "Assets/Prefabs/Effects/Shaman_FlameFX.prefab";
    private const string FlameMatSrc = "Assets/Art/Background/Mat_StardustParticle.mat"; // URP Particles/Unlit・加算（_Blend=2）
    private const string FlameMatDst = "Assets/Art/Enemy/S3_06_Shaman/Shaman_FlameParticle.mat";
    private const string SoftGlowPath = "Assets/Generated/UI/SoftGlowCircle.png";

    [MenuItem("Tools/Shaman/精霊の炎をParticleに作り直す")]
    private static void RebuildFlameAsParticle()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(FlamePrefabPath) == null)
        {
            EditorUtility.DisplayDialog("Shaman", "Shaman_SpiritFlame.prefabがありません（先に「精霊の炎を追加」を実行してください）。", "OK");
            return;
        }
        var softGlow = AssetDatabase.LoadAssetAtPath<Texture2D>(SoftGlowPath);
        if (AssetDatabase.LoadMainAssetAtPath(FlameMatSrc) == null || softGlow == null)
        {
            EditorUtility.DisplayDialog("Shaman", $"コピー元が見つかりません：\n{FlameMatSrc}\n{SoftGlowPath}", "OK");
            return;
        }
        bool fxExists = AssetDatabase.LoadAssetAtPath<GameObject>(FlameFxPrefabPath) != null;
        if (!EditorUtility.DisplayDialog("Shaman：精霊の炎をParticleに作り直す",
                "次の設定を行います：\n" +
                $"・炎のParticleのPrefab {FlameFxPrefabPath}{(fxExists ? "（既にあるので作り直さない）" : "を作成")}\n" +
                "・Shaman_SpiritFlame.prefab：画像を外して子に炎のParticleを付ける（大きさ1・当たり判定の半径0.25）\n" +
                "・Shaman.prefab > ShamanSpiritFlames：線で反射された火の玉の見た目に炎のParticleを設定\n続けますか？", "実行", "キャンセル"))
            return;

        // 描画順：Shaman本体より手前
        int sortingLayerId = 0, sortingOrder = 31;
        GameObject shamanTmp = PrefabUtility.LoadPrefabContents(ShamanPrefab);
        try
        {
            var sc = shamanTmp.GetComponent<ShamanController>();
            var body = sc != null ? new SerializedObject(sc).FindProperty("spriteRenderer").objectReferenceValue as SpriteRenderer : null;
            if (body != null) { sortingLayerId = body.sortingLayerID; sortingOrder = body.sortingOrder + 1; }
        }
        finally { PrefabUtility.UnloadPrefabContents(shamanTmp); }

        // ① マテリアル
        if (AssetDatabase.LoadMainAssetAtPath(FlameMatDst) == null) AssetDatabase.CopyAsset(FlameMatSrc, FlameMatDst);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(FlameMatDst);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", softGlow);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", softGlow);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(mat);

        // ② 炎のParticleのPrefab
        if (!fxExists) CreateFlameFxPrefab(mat, sortingLayerId, sortingOrder);
        var fxAsset = AssetDatabase.LoadAssetAtPath<GameObject>(FlameFxPrefabPath);

        // ③ 回る火の玉
        GameObject flame = PrefabUtility.LoadPrefabContents(FlamePrefabPath);
        try
        {
            flame.transform.localScale = Vector3.one;
            var sr = flame.GetComponent<SpriteRenderer>();
            if (sr != null) Object.DestroyImmediate(sr);
            var oldFx = flame.transform.Find("FX");
            if (oldFx != null) Object.DestroyImmediate(oldFx.gameObject);
            var fxGo = (GameObject)PrefabUtility.InstantiatePrefab(fxAsset, flame.transform);
            fxGo.name = "FX";
            fxGo.transform.localPosition = Vector3.zero;
            var col = flame.GetComponent<CircleCollider2D>();
            if (col != null) { col.radius = 0.25f; col.offset = Vector2.zero; }
            var so = new SerializedObject(flame.GetComponent<ShamanSpiritFlame>());
            so.FindProperty("fx").objectReferenceValue = fxGo.GetComponent<ParticleSystem>();
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(flame, FlamePrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(flame);
        }

        // ④ Shaman
        GameObject root = PrefabUtility.LoadPrefabContents(ShamanPrefab);
        try
        {
            var sf = root.GetComponent<ShamanSpiritFlames>();
            if (sf == null)
            {
                Debug.LogError("[ShamanSetupTool] Shaman.prefabにShamanSpiritFlamesがありません（先に「精霊の炎を追加」を実行してください）");
                return;
            }
            var so = new SerializedObject(sf);
            so.FindProperty("flameFxPrefab").objectReferenceValue = fxAsset.GetComponent<ParticleSystem>();
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, ShamanPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[ShamanSetupTool] 精霊の炎をParticleで作り直しました");
        EditorUtility.DisplayDialog("Shaman", "設定しました。", "OK");
    }

    // ======================================================
    // 火の玉に反射弾を当てた時のSE・エフェクトを、通常のステージブロック（Block_StageScatter）と同じにする
    //  WallHealthのHit Clips / Just Hit Clips / Hit Volume / Hit Vfx Prefab / Hit Vfx Destroy Secondsをコピー（コピー元は読むだけ）
    //  ※SEを鳴らすAudioSourceはWallHealthが起動時に自動で追加する
    // ======================================================
    private const string StageBlockPrefab = "Assets/Prefabs/Enemies/Block_StageScatter.prefab";

    [MenuItem("Tools/Shaman/火の玉に当てた時のSE・エフェクトをブロックと同じに")]
    private static void CopyBlockHitFx()
    {
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(StageBlockPrefab);
        var srcWh = src != null ? src.GetComponent<WallHealth>() : null;
        if (srcWh == null || AssetDatabase.LoadAssetAtPath<GameObject>(FlamePrefabPath) == null)
        {
            EditorUtility.DisplayDialog("Shaman", $"コピー元（{StageBlockPrefab}のWallHealth）または Shaman_SpiritFlame.prefab が見つかりません。", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("Shaman：火の玉に当てた時のSE・エフェクト",
                "Block_StageScatter の WallHealth から、当たった時のSE・エフェクトの設定\n" +
                "（Hit Clips / Just Hit Clips / Hit Volume / Hit Vfx Prefab / Hit Vfx Destroy Seconds）を\n" +
                "Shaman_SpiritFlame.prefab の WallHealth にコピーします（コピー元は変更しません）。\n続けますか？", "コピーする", "キャンセル"))
            return;

        var srcSo = new SerializedObject(srcWh);
        GameObject root = PrefabUtility.LoadPrefabContents(FlamePrefabPath);
        try
        {
            var dstSo = new SerializedObject(root.GetComponent<WallHealth>());
            foreach (string n in new[] { "hitClips", "justHitClips", "hitVolume", "hitVfxPrefab", "hitVfxDestroySeconds" })
            {
                var p = srcSo.FindProperty(n);
                if (p != null) dstSo.CopyFromSerializedProperty(p);
            }
            dstSo.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, FlamePrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[ShamanSetupTool] 火の玉に当てた時のSE・エフェクトをBlock_StageScatterと同じにしました");
        EditorUtility.DisplayDialog("Shaman", "コピーしました。", "OK");
    }

    // 炎：外側の炎（上へ立ちのぼる粒）＋内側の明るい芯（子のCore）
    private static void CreateFlameFxPrefab(Material mat, int sortingLayerId, int sortingOrder)
    {
        var go = new GameObject("Shaman_FlameFX");
        try
        {
            var ps = go.AddComponent<ParticleSystem>();
            SetupFlamePs(ps, mat, sortingLayerId, sortingOrder,
                lifetime: new Vector2(0.35f, 0.6f), size: new Vector2(0.18f, 0.38f), speed: new Vector2(0.1f, 0.4f),
                rate: 70f, shapeRadius: 0.12f, riseSpeed: 0.8f,
                colors: new[] { new Color(1f, 0.95f, 0.6f), new Color(1f, 0.55f, 0.1f), new Color(0.8f, 0.15f, 0.05f) });

            var core = new GameObject("Core");
            core.transform.SetParent(go.transform, false);
            var cps = core.AddComponent<ParticleSystem>();
            SetupFlamePs(cps, mat, sortingLayerId, sortingOrder + 1,
                lifetime: new Vector2(0.2f, 0.3f), size: new Vector2(0.4f, 0.5f), speed: new Vector2(0f, 0.05f),
                rate: 25f, shapeRadius: 0.03f, riseSpeed: 0f,
                colors: new[] { new Color(1f, 1f, 0.85f), new Color(1f, 0.85f, 0.4f), new Color(1f, 0.6f, 0.2f) });

            PrefabUtility.SaveAsPrefabAsset(go, FlameFxPrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    private static void SetupFlamePs(ParticleSystem ps, Material mat, int sortingLayerId, int sortingOrder,
                                     Vector2 lifetime, Vector2 size, Vector2 speed, float rate, float shapeRadius, float riseSpeed, Color[] colors)
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;  // ShamanSpiritFlame / ShamanFlameBulletが再生する
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime.x, lifetime.y);
        main.startSize = new ParticleSystem.MinMaxCurve(size.x, size.y);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed.x, speed.y);
        main.startColor = Color.white;
        main.simulationSpace = ParticleSystemSimulationSpace.World; // 動くと炎がなびく
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;      // 吸収で弾が大きくなると炎も大きくなる
        main.gravityModifier = 0f;
        main.maxParticles = 200;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(rate);

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = shapeRadius;
        shape.radiusThickness = 1f;

        // 上へ立ちのぼる：★x/y/zは未使用軸も含め全て同じモード（Constant）で明示設定する
        var vel = ps.velocityOverLifetime;
        vel.enabled = riseSpeed != 0f;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(0f);
        vel.y = new ParticleSystem.MinMaxCurve(riseSpeed);
        vel.z = new ParticleSystem.MinMaxCurve(0f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(colors[0], 0f), new GradientColorKey(colors[1], 0.5f), new GradientColorKey(colors[2], 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(g);

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.3f)));

        var r = ps.GetComponent<ParticleSystemRenderer>();
        r.sharedMaterial = mat;
        r.renderMode = ParticleSystemRenderMode.Billboard;
        r.sortingLayerID = sortingLayerId;
        r.sortingOrder = sortingOrder;
    }

    private static void CreateFlamePrefab(Sprite sprite, AudioClip fireSE)
    {
        // Shaman本体と同じ描画レイヤーで、本体より手前に描く
        int sortingLayerId = 0, sortingOrder = 31;
        GameObject shaman = PrefabUtility.LoadPrefabContents(ShamanPrefab);
        try
        {
            var sc = shaman.GetComponent<ShamanController>();
            var body = sc != null ? new SerializedObject(sc).FindProperty("spriteRenderer").objectReferenceValue as SpriteRenderer : null;
            if (body != null) { sortingLayerId = body.sortingLayerID; sortingOrder = body.sortingOrder + 1; }
        }
        finally { PrefabUtility.UnloadPrefabContents(shaman); }

        var go = new GameObject("Shaman_SpiritFlame");
        try
        {
            go.transform.localScale = Vector3.one * 1.7f; // 仮の見た目：弾画像（直径約0.36）を約0.6に

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = FlameColor;
            sr.sortingLayerID = sortingLayerId;
            sr.sortingOrder = sortingOrder;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = Mathf.Max(0.05f, sprite.bounds.extents.x);

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var wh = go.AddComponent<WallHealth>();
            var whSo = new SerializedObject(wh);
            whSo.FindProperty("maxHp").intValue = 5;
            whSo.FindProperty("damageUnreflected").intValue = 0;
            whSo.FindProperty("disableColliderOnBreak").boolValue = true;
            whSo.FindProperty("disableRendererOnBreak").boolValue = false; // フェードアウトはShamanSpiritFlameが行う
            var breakClips = whSo.FindProperty("breakClips");
            for (int i = 0; i < breakClips.arraySize; i++) breakClips.GetArrayElementAtIndex(i).objectReferenceValue = null; // 壊れたSEはShamanSpiritFlameが鳴らす
            whSo.FindProperty("dropItems").boolValue = false;
            whSo.FindProperty("countsAsScoreBlock").boolValue = false;
            whSo.ApplyModifiedPropertiesWithoutUndo();

            var flame = go.AddComponent<ShamanSpiritFlame>();
            var fSo = new SerializedObject(flame);
            // ※見た目は後から「精霊の炎をParticleに作り直す」で炎のParticleに置き換える
            fSo.FindProperty("breakSE").objectReferenceValue = fireSE;
            fSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(go, FlamePrefabPath);
        }
        finally
        {
            Object.DestroyImmediate(go);
        }
    }

    private static int FindTypeIndex(EnemyData data, string typeName)
    {
        for (int i = 0; i < data.bulletTypes.Length; i++)
            if (data.bulletTypes[i] != null && data.bulletTypes[i].name == typeName) return i;
        return -1;
    }
}
