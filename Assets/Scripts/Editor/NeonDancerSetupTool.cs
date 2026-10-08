using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// NeonDancer（Area10最終ボス）のPrefab・EnemyData・ワームホールPrefabを初回だけ作成するツール。
/// ArcGuard.prefab / EnemyData_ArcGuard.asset をコピーし（既存エネミーからコピーして作るルール）、
/// ArcGuard固有の部品を外してNeonDancerの階層を組み立てる。
/// ★出力先が1つでも既に存在する場合は何もしない（再実行で調整済みの設定が消える事故を防ぐ）。
/// </summary>
public static class NeonDancerSetupTool
{
    private const string SrcPrefab   = "Assets/Prefabs/Enemies/ArcGuard.prefab";
    private const string SrcData     = "Assets/GameData/Enemies/EnemyData_ArcGuard.asset";
    private const string DstPrefab   = "Assets/Prefabs/Enemies/NeonDancer.prefab";
    private const string DstData     = "Assets/GameData/Enemies/EnemyData_NeonDancer.asset";
    private const string WormholePrefabPath = "Assets/Prefabs/Effects/NeonDancer_Wormhole.prefab";
    private const string ArtFolder   = "Assets/Art/Enemy/S3_10_NeonDancer";
    private const string WhitePngPath = ArtFolder + "/ND_White.png";

    // PixelDancerAnimControllerのanimNamesと同じ順
    private static readonly string[] DanceClipPaths =
    {
        "Assets/Animations/PixelDancer_パルクール.anim",
        "Assets/Animations/PixelDancer_スピン.anim",
        "Assets/Animations/PixelDancer_キック.anim",
        "Assets/Animations/PixelDancer_新技.anim",
    };

    // ArcGuardのBodyと同じソートレイヤーで、奥→手前の順
    private const int OrderFloor = 6, OrderFloorSpot = 7, OrderBeam = 8, OrderLight = 9, OrderWormhole = 11, OrderFlash = 100;

    [MenuItem("Tools/NeonDancer/1 セットアップ/プレハブとEnemyDataを作成（初回のみ）")]
    private static void Create()
    {
        string[] outputs = { DstPrefab, DstData, WormholePrefabPath };
        foreach (string p in outputs)
        {
            if (AssetDatabase.LoadMainAssetAtPath(p) != null)
            {
                EditorUtility.DisplayDialog("NeonDancer作成", $"既に存在するため何もしません：\n{p}\n\n（作り直す場合は手動で削除してから実行してください）", "OK");
                return;
            }
        }
        if (AssetDatabase.LoadMainAssetAtPath(SrcPrefab) == null || AssetDatabase.LoadMainAssetAtPath(SrcData) == null)
        {
            EditorUtility.DisplayDialog("NeonDancer作成", $"コピー元が見つかりません：\n{SrcPrefab}\n{SrcData}", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("NeonDancer作成",
                "ArcGuardのPrefab/EnemyDataをコピーして、NeonDancerのPrefab・EnemyData・ワームホールPrefabを作成します。\n（ArcGuard本体は変更しません）",
                "作成する", "キャンセル"))
            return;

        Sprite white = CreateWhiteSprite();
        NeonDancerWormhole wormholePrefab = CreateWormholePrefab(white);

        if (!AssetDatabase.CopyAsset(SrcPrefab, DstPrefab)) { Debug.LogError("[NeonDancerSetupTool] Prefabのコピーに失敗"); return; }
        if (!AssetDatabase.CopyAsset(SrcData, DstData))     { Debug.LogError("[NeonDancerSetupTool] EnemyDataのコピーに失敗"); return; }
        AssetDatabase.Refresh();

        BuildPrefab(wormholePrefab, white);
        SetupEnemyData();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Selection.activeObject = AssetDatabase.LoadMainAssetAtPath(DstPrefab);
        Debug.Log($"[NeonDancerSetupTool] 作成完了：{DstPrefab} / {DstData} / {WormholePrefabPath}");
        EditorUtility.DisplayDialog("NeonDancer作成", "作成しました。\nスプライト・位置・HP・Bullet Typesの数値はInspectorで設定してください。", "OK");
    }

    // ======================================================
    // 白スプライト（画面フラッシュ・ワームホール仮表示用）
    // ======================================================
    private static Sprite CreateWhiteSprite()
    {
        if (!AssetDatabase.IsValidFolder(ArtFolder))
            AssetDatabase.CreateFolder("Assets/Art/Enemy", "S3_10_NeonDancer");

        if (AssetDatabase.LoadAssetAtPath<Sprite>(WhitePngPath) == null)
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color[16];
            for (int i = 0; i < px.Length; i++) px[i] = Color.white;
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(WhitePngPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(WhitePngPath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(WhitePngPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 4f; // 1ユニット四方
            importer.filterMode = FilterMode.Point;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(WhitePngPath);
    }

    // ======================================================
    // ワームホールPrefab
    // ======================================================
    private static NeonDancerWormhole CreateWormholePrefab(Sprite placeholder)
    {
        var go = new GameObject("NeonDancer_Wormhole");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = placeholder; // ★渦巻き画像ができたら差し替える
        sr.sortingLayerName = GetBodySortingLayerName();
        sr.sortingOrder = OrderWormhole;
        var wh = go.AddComponent<NeonDancerWormhole>();
        SetRef(wh, "spriteRenderer", sr);

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(go, WormholePrefabPath);
        Object.DestroyImmediate(go);
        return saved.GetComponent<NeonDancerWormhole>();
    }

    private static string GetBodySortingLayerName()
    {
        // ArcGuardのBody（SortingLayerID 1170256609）と同じレイヤー名を使う
        var src = AssetDatabase.LoadAssetAtPath<GameObject>(SrcPrefab);
        var body = src != null ? src.transform.Find("Body") : null;
        var sr = body != null ? body.GetComponent<SpriteRenderer>() : null;
        return sr != null ? sr.sortingLayerName : "Default";
    }

    // ======================================================
    // Prefab組み立て
    // ======================================================
    private static void BuildPrefab(NeonDancerWormhole wormholePrefab, Sprite white)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(DstPrefab);
        try
        {
            root.name = "NeonDancer";

            // --- ArcGuard固有の部品を除去 ---
            var arc = root.GetComponent<ArcGuardController>();
            if (arc != null) Object.DestroyImmediate(arc, true);
            Transform trail = root.transform.Find("Trail");
            if (trail != null) Object.DestroyImmediate(trail.gameObject, true);

            // --- HP表示：EnemyHealthDisplay → NeonDancerHealthDisplay（フォント・デバフアイコン等の設定値を引き継ぐ） ---
            var oldDisplay = root.GetComponent<EnemyHealthDisplay>();
            var newDisplay = root.AddComponent<NeonDancerHealthDisplay>();
            if (oldDisplay != null)
            {
                CopyMatchingProperties(oldDisplay, newDisplay);
                Object.DestroyImmediate(oldDisplay, true);
            }

            // --- 本体（ArcGuardのBodyを再利用：WeakPoint/ダメージ有効/ソートレイヤー設定済み） ---
            Transform body = root.transform.Find("Body");
            body.name = "ND_Body";
            body.localPosition = Vector3.zero;
            var bodySr = body.GetComponent<SpriteRenderer>();
            bodySr.sprite = null;
            var bodyCol = body.GetComponent<BoxCollider2D>();
            bodyCol.offset = Vector2.zero;
            bodyCol.size = new Vector2(0.6f, 1.0f);
            var bodyPart = body.GetComponent<EnemyPart>();
            string layerName = bodySr.sortingLayerName;

            Transform muzzle = root.transform.Find("FirePoint_Face");
            if (muzzle != null) { muzzle.name = "ND_MuzzlePoint"; muzzle.localPosition = Vector3.zero; }

            // --- 発射台P2（Dancerの左右：Dancerと一緒に動く） ---
            var turretP2 = CreateTurret(root.transform, "ND_Turret_P2", Vector3.zero, Vector2.zero, new Vector2(3.6f, 0.8f),
                new[] { 2, 3, 6 }, "P2 Dancer左右 ③④⑦", new Color(0.2f, 1f, 1f, 0.9f));

            // --- ND_Stage（Start時に切り離して固定） ---
            var stage = new GameObject("ND_Stage").transform;
            stage.SetParent(root.transform, false);

            // Floor（プレイヤー側と同じくDancerの足元0.5下）
            var floorGo = new GameObject("ND_Floor");
            floorGo.transform.SetParent(stage, false);
            floorGo.transform.localPosition = new Vector3(0f, -0.6f, 0f);
            var floorSr = floorGo.AddComponent<SpriteRenderer>();
            floorSr.sortingLayerName = layerName; floorSr.sortingOrder = OrderFloor;
            var floorCol = floorGo.AddComponent<BoxCollider2D>();
            floorCol.size = new Vector2(4.2f, 0.3f);
            var floorWall = floorGo.AddComponent<WallHealth>();
            SetupWallHealth(floorWall, 30);
            var floorBarrier = floorGo.AddComponent<NeonDancerBarrier>();
            SetBool(floorBarrier, "darkenOnBreak", true);
            SetRef(floorBarrier, "targetRenderer", floorSr);

            // Light×2（プレイヤー側Partnerと同じくDancerの左上・右上）
            var lightL = CreateLight(stage, "ND_LightLeft",  new Vector3(-2.1f, 1.6f, 0f), layerName);
            var lightR = CreateLight(stage, "ND_LightRight", new Vector3( 2.1f, 1.6f, 0f), layerName);

            // 足元スポット
            var spotGo = new GameObject("ND_FloorSpot");
            spotGo.transform.SetParent(stage, false);
            spotGo.transform.localPosition = new Vector3(0f, -0.5f, 0f);
            var spotSr = spotGo.AddComponent<SpriteRenderer>();
            spotSr.sortingLayerName = layerName; spotSr.sortingOrder = OrderFloorSpot;

            // 発射台P1（Light後方）・P3（Floor前方）
            var turretP1 = CreateTurret(stage, "ND_Turret_P1", new Vector3(0f, 2.3f, 0f), Vector2.zero, new Vector2(5.0f, 0.6f),
                new[] { 1, 5, 8 }, "P1 Light後方 ②⑥⑨", new Color(1f, 0.3f, 1f, 0.9f));
            var turretP3 = CreateTurret(stage, "ND_Turret_P3", new Vector3(0f, -1.3f, 0f), Vector2.zero, new Vector2(4.2f, 0.5f),
                new[] { 0, 4, 7 }, "P3 Floor前方 ①⑤⑧", new Color(1f, 0.9f, 0.2f, 0.9f));

            // 画面フラッシュ（後半移行演出。通常は非表示）
            var flashGo = new GameObject("ND_TransitionFlash");
            flashGo.transform.SetParent(stage, false);
            flashGo.transform.localScale = new Vector3(60f, 30f, 1f);
            var flashSr = flashGo.AddComponent<SpriteRenderer>();
            flashSr.sprite = white;
            flashSr.sortingLayerName = layerName; flashSr.sortingOrder = OrderFlash;
            flashSr.color = new Color(1f, 1f, 1f, 0f);
            flashSr.enabled = false;

            // --- コントローラー ---
            var ctrl = root.AddComponent<NeonDancerController>();
            var so = new SerializedObject(ctrl);
            so.FindProperty("spriteRenderer").objectReferenceValue = bodySr;
            so.FindProperty("bodyCollider").objectReferenceValue = bodyCol;
            so.FindProperty("bodyPart").objectReferenceValue = bodyPart;
            so.FindProperty("enemyMover").objectReferenceValue = root.GetComponent<EnemyMover>();
            so.FindProperty("enemyStats").objectReferenceValue = root.GetComponent<EnemyStats>();
            so.FindProperty("enemyShooter").objectReferenceValue = root.GetComponent<EnemyShooter>();
            so.FindProperty("spriteSwapper").objectReferenceValue = root.GetComponent<EnemySpriteSwapper>();
            so.FindProperty("muzzlePoint").objectReferenceValue = muzzle;
            so.FindProperty("stageRoot").objectReferenceValue = stage;
            so.FindProperty("floorCollider").objectReferenceValue = floorCol;
            so.FindProperty("floorBarrier").objectReferenceValue = floorBarrier;
            SetArray(so.FindProperty("lights"), lightL, lightR);
            so.FindProperty("floorSpotRenderer").objectReferenceValue = spotSr;
            so.FindProperty("transitionFlashRenderer").objectReferenceValue = flashSr;
            SetArray(so.FindProperty("turrets"), turretP1, turretP2, turretP3);
            so.FindProperty("wormholePrefab").objectReferenceValue = wormholePrefab;
            var clips = so.FindProperty("sourceDanceClips");
            clips.arraySize = DanceClipPaths.Length;
            for (int i = 0; i < DanceClipPaths.Length; i++)
                clips.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AnimationClip>(DanceClipPaths[i]);
            so.ApplyModifiedPropertiesWithoutUndo();

            SetRef(lightL, "controller", ctrl);
            SetRef(lightR, "controller", ctrl);

            // コピー元（ArcGuard）のEnemyData参照を新しいEnemyDataへ付け替える（実行時はスポナーが上書きするが、Inspector/プレビューの混乱防止）
            var newData = AssetDatabase.LoadAssetAtPath<EnemyData>(DstData);
            var shooter = root.GetComponent<EnemyShooter>();
            if (shooter != null) SetRef(shooter, "enemyData", newData);
            var mover = root.GetComponent<EnemyMover>();
            if (mover != null) SetRef(mover, "enemyData", newData);
            SetRef(newDisplay, "enemyData", newData);

            PrefabUtility.SaveAsPrefabAsset(root, DstPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static NeonDancerLight CreateLight(Transform parent, string name, Vector3 localPos, string layerName)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = layerName; sr.sortingOrder = OrderLight;
        var col = go.AddComponent<BoxCollider2D>();
        col.size = new Vector2(0.8f, 0.8f);
        // ★回転して追尾するためKinematic Rigidbody2D（ArcGuardの尻尾と同じ）。回転はMoveRotationで行う
        var rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;

        var wall = go.AddComponent<WallHealth>();
        SetupWallHealth(wall, 15);
        go.AddComponent<NeonDancerBarrier>();
        var light = go.AddComponent<NeonDancerLight>();

        var beamRoot = new GameObject("BeamRoot").transform;
        beamRoot.SetParent(go.transform, false);
        var beamSrGo = new GameObject("BeamSr");
        beamSrGo.transform.SetParent(beamRoot, false);
        var beamSr = beamSrGo.AddComponent<SpriteRenderer>();
        beamSr.sortingLayerName = layerName; beamSr.sortingOrder = OrderBeam;

        SetRef(light, "beamRoot", beamRoot);
        SetRef(light, "beamSr", beamSr);
        return light;
    }

    private static NeonDancerTurret CreateTurret(Transform parent, string name, Vector3 localPos, Vector2 areaCenter, Vector2 areaSize,
                                                 int[] indices, string label, Color gizmoColor)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var t = go.AddComponent<NeonDancerTurret>();

        var so = new SerializedObject(t);
        SetBulletChoices(so, indices, null);
        so.FindProperty("spawnAreaCenter").vector2Value = areaCenter;
        so.FindProperty("spawnAreaSize").vector2Value = areaSize;
        so.FindProperty("gizmoLabel").stringValue = label;
        so.FindProperty("gizmoColor").colorValue = gizmoColor;
        so.ApplyModifiedPropertiesWithoutUndo();
        return t;
    }

    private static void SetupWallHealth(WallHealth wall, int maxHp)
    {
        var so = new SerializedObject(wall);
        so.FindProperty("maxHp").intValue = maxHp;
        so.FindProperty("disableColliderOnBreak").boolValue = true;  // 破壊中はすり抜け
        so.FindProperty("disableRendererOnBreak").boolValue = false; // 見た目は消さない（暗色化/消灯はNeonDancer側）
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ======================================================
    // EnemyData
    // ======================================================
    private static void SetupEnemyData()
    {
        var data = AssetDatabase.LoadAssetAtPath<EnemyData>(DstData);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DstPrefab);

        data.prefabOverride = prefab;
        data.sprite = null;                 // ArcGuardの絵を残さない
        data.spriteScale = Vector2.one;     // スポナーがルートのlocalScaleをこの値で上書きするため(1,1)必須
        data.useWeakPointSystem = true;     // ルートのEnemyDamageReceiver経由の爆発/Beamダメージを無効化（本体はND_BodyのEnemyPartで受ける）

        // ①〜⑨（NeonDancerTurretのbulletTypeIndicesと対応）。数値はInspectorで調整する
        string[] names = { "①Straight", "②SpeedCurve", "③Missile", "④Spiral", "⑤Smoke", "⑥Warhead", "⑦Beam", "⑧Warp", "⑨Drill" };
        var types = new EnemyData.BulletType[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            var bt = new EnemyData.BulletType();
            bt.name = names[i];
            bt.aimMode = EnemyData.BulletType.AimMode.TowardPlayer;
            types[i] = bt;
        }
        types[1].useSpeedCurve    = true;
        types[2].useMissileArc    = true;
        types[3].useSpiralMotion  = true;
        types[4].useSmokeGrenade  = true;
        types[5].useMultiWarhead  = true;
        types[6].useBeam          = true;
        types[7].useWarp          = true;
        types[8].usePinnedReflect = true;
        data.bulletTypes = types;

        EditorUtility.SetDirty(data);
    }

    // ======================================================
    // プレイヤー側（05_Game）と同じ画像・設定をNeonDancerのFloor/Light/ビーム/足元スポットへ割り当てる
    // （値は05_Game.unityの実データから確認済み）
    // ======================================================
    private const string FloorSpritePath   = "Assets/Art/Floor/FloorNeon.png";          // FloorVisual（FloorNeon_0）
    private const string PartnerSpritePath = "Assets/Art/Partner/PD_Idle_F1.png";       // PartnerLeft/Right
    private const string BeamSpritePath    = "Assets/Art/Background/Spotlight_EyeBeam.png";
    private const string SpotSpritePath    = "Assets/Art/Background/Spotlight_FloorSpot.png";
    private const string NeonMaterialGuid  = "a97c105638bdf8b4a8650670310a4cd3";         // Partner/FloorVisualと同じマテリアル
    private const int    FloorSortingLayerId = 1694564541;                              // FloorVisualと同じソートレイヤー

    [MenuItem("Tools/NeonDancer/1 セットアップ/プレイヤーの画像を割り当て")]
    private static void AssignPlayerVisuals()
    {
        if (AssetDatabase.LoadMainAssetAtPath(DstPrefab) == null)
        {
            EditorUtility.DisplayDialog("NeonDancer", "先に「プレハブとEnemyDataを作成（初回のみ）」を実行してください。", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("プレイヤーの画像を割り当て",
                "NeonDancer.prefabのFloor・Light・ビーム・足元スポットに、プレイヤー側（05_Game）と同じ画像・マテリアル・ソート順・縮尺・位置関係を設定します。\n" +
                "これらの位置・大きさ・当たり判定・ビーム/スポットの不透明度は上書きされます（Dancerのダンス・当たり判定は変更しません）。続けますか？",
                "割り当てる", "キャンセル"))
            return;

        Sprite floorSprite = null;
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(FloorSpritePath))
            if (o is Sprite s && s.name == "FloorNeon_0") { floorSprite = s; break; }
        Sprite partner = AssetDatabase.LoadAssetAtPath<Sprite>(PartnerSpritePath);
        Sprite beam    = AssetDatabase.LoadAssetAtPath<Sprite>(BeamSpritePath);
        Sprite spot    = AssetDatabase.LoadAssetAtPath<Sprite>(SpotSpritePath);
        Material neonMat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(NeonMaterialGuid));
        if (floorSprite == null || partner == null || beam == null || spot == null || neonMat == null)
        {
            EditorUtility.DisplayDialog("NeonDancer", "プレイヤー側の画像/マテリアルが見つかりません。Consoleを確認してください。", "OK");
            Debug.LogError($"[NeonDancerSetupTool] floor={floorSprite} partner={partner} beam={beam} spot={spot} mat={neonMat}");
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(DstPrefab);
        try
        {
            Transform stage = root.transform.Find("ND_Stage");
            var bodySr = root.transform.Find("ND_Body").GetComponent<SpriteRenderer>();

            // --- Floor：プレイヤーはFloor(scale 0.4,0.1)×FloorVisual(scale 1,7)＝見た目0.4×0.7、当たり判定はワールド4.12×0.4 ---
            Transform floor = stage.Find("ND_Floor");
            floor.localPosition = new Vector3(0f, -0.5f, 0f);              // プレイヤーはダンサーの0.5下
            floor.localScale = new Vector3(0.4f, 0.7f, 1f);
            var floorSr = floor.GetComponent<SpriteRenderer>();
            floorSr.sprite = floorSprite;
            floorSr.sharedMaterial = neonMat;
            floorSr.sortingLayerID = FloorSortingLayerId;
            floorSr.sortingOrder = 0;
            floor.GetComponent<BoxCollider2D>().size = new Vector2(4.12f / 0.4f, 0.4f / 0.7f);

            // --- Light：プレイヤーのPartnerはダンサーから左右±2.1・上に1.8 ---
            SetupLightVisual(stage.Find("ND_LightLeft"),  new Vector3(-2.1f, 1.8f, 0f), partner, beam, neonMat, bodySr.sortingLayerID);
            SetupLightVisual(stage.Find("ND_LightRight"), new Vector3( 2.1f, 1.8f, 0f), partner, beam, neonMat, bodySr.sortingLayerID);

            // --- 足元スポット：StageIntroController.InitFloorSpotと同じ計算（足元＝ダンサー下端、直径＝平均照射距離×sin28°×2） ---
            var ctrl = root.GetComponent<NeonDancerController>();
            var ctrlSo = new SerializedObject(ctrl);
            Sprite firstDance = null;
            var d1 = ctrlSo.FindProperty("dance1Frames");
            if (d1 != null && d1.arraySize > 0) firstDance = d1.GetArrayElementAtIndex(0).FindPropertyRelative("sprite").objectReferenceValue as Sprite;
            // ★足元＝絵が描かれている範囲（不透明部分＝Tightメッシュ頂点）の下端。
            //   sprite.bounds は透明な余白を含む枠全体のため使わない（PD_02_D_0は枠2.72・ピボット55%で、枠基準だと1.36下にずれた）
            float feetY = -0.5f;
            if (firstDance != null && firstDance.vertices != null && firstDance.vertices.Length > 0)
            {
                feetY = float.MaxValue;
                foreach (Vector2 v in firstDance.vertices) feetY = Mathf.Min(feetY, v.y);
            }

            Transform spotT = stage.Find("ND_FloorSpot");
            spotT.localPosition = new Vector3(0f, feetY, 0f);
            float avgDist = new Vector2(2.1f, 1.8f).magnitude;
            float diameter = avgDist * Mathf.Sin(28f * Mathf.Deg2Rad) * 2f;
            float spotScale = diameter / spot.bounds.size.x;
            spotT.localScale = new Vector3(spotScale, spotScale, 1f);
            var spotSr = spotT.GetComponent<SpriteRenderer>();
            spotSr.sprite = spot;
            spotSr.sortingLayerID = bodySr.sortingLayerID;
            spotSr.sortingOrder = bodySr.sortingOrder - 1; // ダンサーの足元（ダンサーより奥）

            ctrlSo.FindProperty("floorSpotAlpha").floatValue = 0.3f; // プレイヤーのfloorSpotMaxAlpha
            ctrlSo.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, DstPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[NeonDancerSetupTool] プレイヤーの画像を割り当てました");
        EditorUtility.DisplayDialog("NeonDancer", "割り当てました。", "OK");
    }

    private static void SetupLightVisual(Transform light, Vector3 localPos, Sprite partner, Sprite beam, Material neonMat, int bodyLayerId)
    {
        light.localPosition = localPos;
        var sr = light.GetComponent<SpriteRenderer>();
        sr.sprite = partner;
        sr.sharedMaterial = neonMat;
        sr.sortingLayerID = bodyLayerId;
        sr.sortingOrder = OrderLight;
        light.GetComponent<BoxCollider2D>().size = new Vector2(0.6f, 0.6f); // 画像は500px/700PPU＝約0.71四方

        var beamSr = light.Find("BeamRoot/BeamSr").GetComponent<SpriteRenderer>();
        beamSr.sprite = beam;
        beamSr.sortingLayerID = 0;      // プレイヤーのBeamSrと同じDefaultレイヤー
        beamSr.sortingOrder = 103;

        var so = new SerializedObject(light.GetComponent<NeonDancerLight>());
        so.FindProperty("beamOriginOffset").vector2Value = Vector2.zero; // プレイヤーのbeamFaceLocalY=0
        so.FindProperty("beamReachRatio").floatValue = 0.7f;             // プレイヤーのbeamReachRatio
        so.FindProperty("beamAlpha").floatValue = 0.1f;                  // プレイヤーのintroBeamAlpha
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ======================================================
    // 修正の適用（2026/10/3 指摘分）
    //  ① Bullet Types①〜⑨を既存エネミーの設定からコピー（画像・速度・貫通力・SE込み）
    //     ①②③④⑤⑥⑧：EnemyData_Tsukuyomiの標準弾セット／⑦：Bit.prefabのBitController.beamBulletType／⑨：TsukuyomiのDrill「Straight」
    //  ② Dancerの移動をプレイヤー（05_GameのPlayerタグのPixelDancer：速度1・範囲±1.5・待ち0.1〜0.4秒）に合わせる
    //  ③ パターン1（Light後方）の範囲を画面内（Lightの高さ）へ下げる
    //  ④ Floor/Lightの被弾・破壊エフェクト/SEをArcGuardの尻尾（ArcGuardTailHealth）と同じにする
    // ======================================================
    private const string TsukuyomiDataPath = "Assets/GameData/Enemies/EnemyData_Tsukuyomi.asset";
    private const string BitPrefabPath     = "Assets/Prefabs/Enemies/Bit.prefab";

    [MenuItem("Tools/NeonDancer/2 前半の攻撃・ステージ/弾設定コピー・移動速度・P1位置・被弾エフェクト")]
    private static void ApplyFixes20261003()
    {
        if (AssetDatabase.LoadMainAssetAtPath(DstPrefab) == null || AssetDatabase.LoadMainAssetAtPath(DstData) == null)
        {
            EditorUtility.DisplayDialog("NeonDancer", "先に「プレハブとEnemyDataを作成（初回のみ）」を実行してください。", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("NeonDancer 修正を適用",
                "以下を上書きします（Tsukuyomi・Bit・ArcGuardは読むだけで変更しません）：\n" +
                "・EnemyData_NeonDancerのBullet Types①〜⑨（既存エネミーの設定をコピー）\n" +
                "・Dancerの移動速度/範囲/待ち時間（プレイヤーと同じ値）\n" +
                "・ND_Turret_P1の位置\n" +
                "・ND_Floor/ND_LightLeft/ND_LightRightのWallHealthの被弾・破壊エフェクト/SE\n続けますか？",
                "適用する", "キャンセル"))
            return;

        if (!CopyBulletTypes()) return;
        if (!ApplyPrefabFixes()) return;

        AssetDatabase.SaveAssets();
        Debug.Log("[NeonDancerSetupTool] 修正を適用しました（弾設定コピー・移動速度・P1位置・被弾エフェクト）");
        EditorUtility.DisplayDialog("NeonDancer", "適用しました。", "OK");
    }

    private static bool CopyBulletTypes()
    {
        var data = AssetDatabase.LoadAssetAtPath<EnemyData>(DstData);
        var tsukuyomi = AssetDatabase.LoadAssetAtPath<EnemyData>(TsukuyomiDataPath);
        var bitPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BitPrefabPath);
        BitController bit = bitPrefab != null ? bitPrefab.GetComponentInChildren<BitController>(true) : null;
        var beamField = typeof(BitController).GetField("beamBulletType", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var bitBeam = (bit != null && beamField != null) ? beamField.GetValue(bit) as EnemyData.BulletType : null;
        if (tsukuyomi == null || tsukuyomi.bulletTypes == null || bitBeam == null)
        {
            EditorUtility.DisplayDialog("NeonDancer", "コピー元（EnemyData_Tsukuyomi / Bit.prefabのBeam設定）が見つかりません。", "OK");
            return false;
        }

        EnemyData.BulletType FindTsukuyomi(string name, bool drill)
        {
            foreach (var bt in tsukuyomi.bulletTypes)
                if (bt != null && bt.name == name && bt.usePinnedReflect == drill) return bt;
            return null;
        }

        string[] newNames = { "①Straight", "②SpeedCurve", "③Missile", "④Spiral", "⑤Smoke", "⑥Warhead", "⑦Beam", "⑧Warp", "⑨Drill" };
        EnemyData.BulletType[] sources =
        {
            FindTsukuyomi("Normal", false),
            FindTsukuyomi("Speed Curve", false),
            FindTsukuyomi("Missile", false),
            FindTsukuyomi("Spiral", false),
            FindTsukuyomi("Smoke", false),
            FindTsukuyomi("MultiWarhead", false),
            bitBeam,
            FindTsukuyomi("Warp", false),
            FindTsukuyomi("Straight", true),
        };
        for (int i = 0; i < sources.Length; i++)
        {
            if (sources[i] == null)
            {
                EditorUtility.DisplayDialog("NeonDancer", $"コピー元が見つかりません：{newNames[i]}", "OK");
                return false;
            }
        }

        Undo.RecordObject(data, "NeonDancer Copy Bullet Types");
        var types = new EnemyData.BulletType[sources.Length];
        for (int i = 0; i < sources.Length; i++)
        {
            var copy = new EnemyData.BulletType();
            // 画像・SE等のアセット参照も含めて丸ごと複製する
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(sources[i]), copy);
            copy.name = newNames[i];
            copy.aimMode = EnemyData.BulletType.AimMode.TowardPlayer; // 仕様：自機狙い
            types[i] = copy;
            Debug.Log($"[NeonDancerSetupTool] {newNames[i]} ← {(i == 6 ? "Bit.prefab beamBulletType" : "Tsukuyomi「" + sources[i].name + "」")} " +
                      $"(speed={copy.speed}, penetration={copy.penetration}, sprite={(copy.spriteOverride != null ? copy.spriteOverride.name : "なし")}, fireSE={(copy.fireSEOverride != null ? copy.fireSEOverride.name : "なし")})");
        }
        data.bulletTypes = types;
        EditorUtility.SetDirty(data);
        return true;
    }

    private static bool ApplyPrefabFixes()
    {
        var arcPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SrcPrefab);
        var tail = arcPrefab != null ? arcPrefab.GetComponentInChildren<ArcGuardTailHealth>(true) : null;
        if (tail == null)
        {
            EditorUtility.DisplayDialog("NeonDancer", "コピー元（ArcGuard.prefabのArcGuardTailHealth）が見つかりません。", "OK");
            return false;
        }
        var tailSo = new SerializedObject(tail);

        GameObject root = PrefabUtility.LoadPrefabContents(DstPrefab);
        try
        {
            // ② 移動：Floorの当たり判定幅4.12（半分2.06）− マージン0.56 ＝ プレイヤーと同じ±1.5
            var ctrlSo = new SerializedObject(root.GetComponent<NeonDancerController>());
            ctrlSo.FindProperty("moveSpeed").floatValue = 1f;
            ctrlSo.FindProperty("moveEdgeMargin").floatValue = 0.56f;
            ctrlSo.FindProperty("moveArriveThreshold").floatValue = 0.05f;
            ctrlSo.FindProperty("moveWaitMin").floatValue = 0.1f;
            ctrlSo.FindProperty("moveWaitMax").floatValue = 0.4f;
            ctrlSo.ApplyModifiedPropertiesWithoutUndo();

            Transform stage = root.transform.Find("ND_Stage");

            // ③ パターン1：画面上端はy=5（カメラ中心0・縦サイズ5）。出現位置2.8＋1.5＝4.3（範囲4.0〜4.6）でLightと同じ高さ
            stage.Find("ND_Turret_P1").localPosition = new Vector3(0f, 1.5f, 0f);

            // ④ 被弾・破壊エフェクト/SE（ArcGuardTailHealthの項目 → WallHealthの対応項目）
            foreach (string n in new[] { "ND_Floor", "ND_LightLeft", "ND_LightRight" })
            {
                var wallSo = new SerializedObject(stage.Find(n).GetComponent<WallHealth>());
                wallSo.FindProperty("breakVfxPrefab").objectReferenceValue = tailSo.FindProperty("breakVfxPrefab").objectReferenceValue;
                wallSo.FindProperty("breakVfxDestroySeconds").floatValue   = tailSo.FindProperty("breakVfxDestroySeconds").floatValue;
                wallSo.FindProperty("hitVfxPrefab").objectReferenceValue   = tailSo.FindProperty("hitVfxPrefab").objectReferenceValue;
                wallSo.FindProperty("hitVfxDestroySeconds").floatValue     = tailSo.FindProperty("hitVfxDestroySeconds").floatValue;
                CopyClipArray(tailSo.FindProperty("hitClipsNormal"), wallSo.FindProperty("hitClips"));
                CopyClipArray(tailSo.FindProperty("hitClipsJust"),   wallSo.FindProperty("justHitClips"));
                CopyClipArray(tailSo.FindProperty("breakClips"),     wallSo.FindProperty("breakClips"));
                wallSo.FindProperty("hitVolume").floatValue   = tailSo.FindProperty("hitVolume").floatValue;
                wallSo.FindProperty("breakVolume").floatValue = tailSo.FindProperty("breakVolume").floatValue;
                wallSo.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, DstPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        return true;
    }

    // ======================================================
    // 修正の適用（2026/10/3 指摘分その2）
    //  ① ⑨Drillの回転コマ（Tsukuyomi_Bullet_DrillSpin1〜8）をTsukuyomi.prefabのTsukuyomiControllerからコピー
    //  ② 発射台を「順番」から「確率」に変更（暫定値：ユーザー指定）
    // ======================================================
    private const string TsukuyomiPrefabPath = "Assets/Prefabs/Enemies/Tsukuyomi.prefab";

    [MenuItem("Tools/NeonDancer/2 前半の攻撃・ステージ/Drill画像・割合発射")]
    private static void ApplyFixes20261003b()
    {
        if (AssetDatabase.LoadMainAssetAtPath(DstPrefab) == null)
        {
            EditorUtility.DisplayDialog("NeonDancer", "先に「プレハブとEnemyDataを作成（初回のみ）」を実行してください。", "OK");
            return;
        }
        var tsuPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TsukuyomiPrefabPath);
        var tsu = tsuPrefab != null ? tsuPrefab.GetComponentInChildren<TsukuyomiController>(true) : null;
        if (tsu == null)
        {
            EditorUtility.DisplayDialog("NeonDancer", "コピー元（Tsukuyomi.prefabのTsukuyomiController）が見つかりません。", "OK");
            return;
        }
        var tsuSo = new SerializedObject(tsu);
        var srcFrames = tsuSo.FindProperty("drillSpinFrames");
        float srcRps = tsuSo.FindProperty("drillSpinRotationsPerSecond").floatValue;

        if (!EditorUtility.DisplayDialog("NeonDancer 修正を適用",
                $"以下を上書きします（Tsukuyomiは読むだけで変更しません）：\n" +
                $"・⑨Drillの回転コマ {srcFrames.arraySize}枚（1秒に{srcRps}周）\n" +
                "・発射台の抽選（パターン1：②50% ⑥30% ⑨20%／パターン2：③40% ④40% ⑦20%／パターン3：①50% ⑤10% ⑧40%）\n続けますか？",
                "適用する", "キャンセル"))
            return;

        GameObject root = PrefabUtility.LoadPrefabContents(DstPrefab);
        try
        {
            var ctrlSo = new SerializedObject(root.GetComponent<NeonDancerController>());
            CopyObjectArray(srcFrames, ctrlSo.FindProperty("drillSpinFrames"));
            ctrlSo.FindProperty("drillSpinRotationsPerSecond").floatValue = srcRps;
            ctrlSo.ApplyModifiedPropertiesWithoutUndo();

            Transform stage = root.transform.Find("ND_Stage");
            SetTurretChoices(stage.Find("ND_Turret_P1"),      new[] { 1, 5, 8 }, new[] { 50f, 30f, 20f }); // ②⑥⑨
            SetTurretChoices(root.transform.Find("ND_Turret_P2"), new[] { 2, 3, 6 }, new[] { 40f, 40f, 20f }); // ③④⑦
            SetTurretChoices(stage.Find("ND_Turret_P3"),      new[] { 0, 4, 7 }, new[] { 50f, 10f, 40f }); // ①⑤⑧

            PrefabUtility.SaveAsPrefabAsset(root, DstPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[NeonDancerSetupTool] 修正を適用しました（Drill回転コマ{srcFrames.arraySize}枚・割合発射）");
        EditorUtility.DisplayDialog("NeonDancer", "適用しました。", "OK");
    }

    // ======================================================
    // ⑤Smoke専用エフェクト（AreaSelectのノード色の粒子）
    //  ・煙幕の効果（SmokeCloudの範囲内の敵弾/敵を非表示）はそのまま。見た目のパーティクルだけ専用にする
    //  ・既存のSmokeParticle.prefab / Mat_StardustParticle.matはコピー元として読むだけ
    //  ・Gradientの色キーは最大8個のため、10色をSmokeCloudが管理する2系統（smokeParticle：Area1〜5／sandGrainParticle：Area6〜10）に分ける
    // ======================================================
    private const string SmokeSrcPrefab = "Assets/Prefabs/SmokePart​icle.prefab";              // 既存（ファイル名にゼロ幅スペースを含む）
    private const string SmokeDstPrefab = "Assets/Prefabs/Effects/NeonDancer_SmokeParticle.prefab";
    private const string SparkleMatSrc  = "Assets/Art/Background/Mat_StardustParticle.mat";      // URP Particles/Unlit・加算（_Blend=2）
    private const string SparkleMatDst  = ArtFolder + "/ND_SmokeSparkle.mat";
    private const string SoftGlowPath   = "Assets/Generated/UI/SoftGlowCircle.png";              // AreaSelectの粒子と同じ素材
    private const string SmokeSePath    = "Assets/Audio/Enemy/ステータス上昇魔法1.mp3";

    // 03_AreaSelectのAreaConstellationFX.nodesの色（Area1〜9）＋Area10収束先の金色
    private static readonly Color[] AreaNodeColors =
    {
        new Color32(0x9B, 0x8F, 0xC7, 0xFF), new Color32(0x4C, 0xAF, 0x7D, 0xFF), new Color32(0x8D, 0x99, 0xAE, 0xFF),
        new Color32(0xE0, 0x7A, 0x3F, 0xFF), new Color32(0xB2, 0x3A, 0x52, 0xFF), new Color32(0xE0, 0xB0, 0x4F, 0xFF),
        new Color32(0x4F, 0x8F, 0xE0, 0xFF), new Color32(0x5F, 0xD6, 0xD6, 0xFF), new Color32(0xA3, 0xAE, 0xE0, 0xFF),
        new Color32(0xE8, 0xC9, 0x6B, 0xFF),
    };

    [MenuItem("Tools/NeonDancer/2 前半の攻撃・ステージ/⑤Smoke専用エフェクトを作成して適用")]
    private static void CreateSmokeEffect()
    {
        if (AssetDatabase.LoadMainAssetAtPath(DstData) == null)
        {
            EditorUtility.DisplayDialog("NeonDancer", "先に「プレハブとEnemyDataを作成（初回のみ）」を実行してください。", "OK");
            return;
        }
        if (AssetDatabase.LoadMainAssetAtPath(SmokeDstPrefab) != null || AssetDatabase.LoadMainAssetAtPath(SparkleMatDst) != null)
        {
            EditorUtility.DisplayDialog("NeonDancer", $"既に存在するため作成しません：\n{SmokeDstPrefab}\n{SparkleMatDst}\n\n（作り直す場合は手動で削除してから実行してください）", "OK");
            return;
        }
        var softGlow = AssetDatabase.LoadAssetAtPath<Texture2D>(SoftGlowPath);
        var smokeSe  = AssetDatabase.LoadAssetAtPath<AudioClip>(SmokeSePath);
        if (AssetDatabase.LoadMainAssetAtPath(SmokeSrcPrefab) == null || AssetDatabase.LoadMainAssetAtPath(SparkleMatSrc) == null || softGlow == null || smokeSe == null)
        {
            EditorUtility.DisplayDialog("NeonDancer", "コピー元の素材が見つかりません。Consoleを確認してください。", "OK");
            Debug.LogError($"[NeonDancerSetupTool] smokePrefab={AssetDatabase.LoadMainAssetAtPath(SmokeSrcPrefab)} mat={AssetDatabase.LoadMainAssetAtPath(SparkleMatSrc)} softGlow={softGlow} se={smokeSe}");
            return;
        }
        if (!EditorUtility.DisplayDialog("⑤Smoke専用エフェクト",
                "既存の煙プレハブ・マテリアルをコピーして、NeonDancer専用の粒子の煙幕（Area1〜10の色・瞬き・渦・中心ほど濃く）を作り、\n" +
                "EnemyData_NeonDancerの⑤Smokeに設定します（煙が出る瞬間のSE＝ステータス上昇魔法1。円で消した時のSEはそのまま）。\n続けますか？",
                "作成する", "キャンセル"))
            return;

        // --- マテリアル（加算・SoftGlowCircle） ---
        AssetDatabase.CopyAsset(SparkleMatSrc, SparkleMatDst);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(SparkleMatDst);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", softGlow);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", softGlow);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(mat);

        // --- プレハブ ---
        AssetDatabase.CopyAsset(SmokeSrcPrefab, SmokeDstPrefab);
        AssetDatabase.Refresh();
        GameObject root = PrefabUtility.LoadPrefabContents(SmokeDstPrefab);
        try
        {
            root.name = "NeonDancer_SmokeParticle";
            var cloud = root.GetComponent<SmokeCloud>();
            var cloudSo = new SerializedObject(cloud);
            var mainPs = cloudSo.FindProperty("smokeParticle").objectReferenceValue as ParticleSystem;
            var subPs  = cloudSo.FindProperty("sandGrainParticle").objectReferenceValue as ParticleSystem;
            // ★元の煙プレハブはsmokeParticleが未設定で、実行時にSmokeCloud.Awake()がGetComponentInChildren<ParticleSystem>()で
            //   ルートのパーティクルを自動取得している。同じ方法で取得し、新しいプレハブには明示的に設定しておく
            if (mainPs == null)
            {
                mainPs = root.GetComponentInChildren<ParticleSystem>();
                cloudSo.FindProperty("smokeParticle").objectReferenceValue = mainPs;
                cloudSo.ApplyModifiedPropertiesWithoutUndo();
            }
            if (mainPs == null || subPs == null || mainPs == subPs)
            {
                Debug.LogError($"[NeonDancerSetupTool] コピーした煙プレハブのパーティクルが特定できません smoke={mainPs} sand={subPs}");
                return;
            }

            // 系統1：Area1〜5（初速はSmokeCloudが拡散速度で上書き＝外へ広がる）
            SetupSparklePs(mainPs, mat, new[] { AreaNodeColors[0], AreaNodeColors[1], AreaNodeColors[2], AreaNodeColors[3], AreaNodeColors[4] }, 0.5f, 0.8f);
            // 系統2：Area6〜10（初速を遅くして中心付近に溜める＝中心ほど濃く）
            SetupSparklePs(subPs,  mat, new[] { AreaNodeColors[5], AreaNodeColors[6], AreaNodeColors[7], AreaNodeColors[8], AreaNodeColors[9] }, 0.08f, -0.6f);

            PrefabUtility.SaveAsPrefabAsset(root, SmokeDstPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }

        // --- EnemyDataの⑤Smokeに設定 ---
        var data = AssetDatabase.LoadAssetAtPath<EnemyData>(DstData);
        EnemyData.BulletType smoke = null;
        foreach (var bt in data.bulletTypes) if (bt != null && bt.useSmokeGrenade) { smoke = bt; break; }
        if (smoke != null)
        {
            Undo.RecordObject(data, "NeonDancer Smoke Effect");
            smoke.smokeParticlePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SmokeDstPrefab);
            smoke.smokeReflectSE = smokeSe; // 煙が出る瞬間のSEのみ変更（円で消した時のSEはそのまま）
            EditorUtility.SetDirty(data);
        }
        else Debug.LogWarning("[NeonDancerSetupTool] EnemyData_NeonDancerにSmokeのBullet Typeが見つかりません");

        AssetDatabase.SaveAssets();
        Debug.Log($"[NeonDancerSetupTool] ⑤Smoke専用エフェクトを作成しました：{SmokeDstPrefab} / {SparkleMatDst}");
        EditorUtility.DisplayDialog("NeonDancer", "作成・適用しました。", "OK");
    }

    // ======================================================
    // ③MissileをGolemと同じ大きなカーブ軌道にする（軌道の数値だけコピー。自機狙い・画像・SEは変更しない）
    //  Golemは標準EnemyShooterでEnemyData_GolemのBullet Types[8]「Missile」（Use Missile Arc=ON）を撃っている
    // ======================================================
    private const string GolemDataPath = "Assets/GameData/Enemies/EnemyData_Golem.asset";

    [MenuItem("Tools/NeonDancer/2 前半の攻撃・ステージ/③MissileをGolemの軌道に")]
    private static void CopyGolemMissileArc()
    {
        var golem = AssetDatabase.LoadAssetAtPath<EnemyData>(GolemDataPath);
        var data = AssetDatabase.LoadAssetAtPath<EnemyData>(DstData);
        EnemyData.BulletType src = null, dst = null;
        if (golem != null && golem.bulletTypes != null)
            foreach (var bt in golem.bulletTypes) if (bt != null && bt.name == "Missile" && bt.useMissileArc) { src = bt; break; }
        if (data != null && data.bulletTypes != null)
            foreach (var bt in data.bulletTypes) if (bt != null && bt.useMissileArc) { dst = bt; break; }
        if (src == null || dst == null)
        {
            EditorUtility.DisplayDialog("NeonDancer", $"コピー元/先が見つかりません（GolemのMissile={src != null} / NeonDancerの③={dst != null}）", "OK");
            return;
        }

        // 名前がmissileで始まる軌道の項目すべて＋Speed（カーブは複製してコピー）
        var fields = new System.Collections.Generic.List<System.Reflection.FieldInfo>();
        foreach (var f in typeof(EnemyData.BulletType).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
            if (f.Name.StartsWith("missile") || f.Name == "speed") fields.Add(f);

        var sb = new System.Text.StringBuilder();
        foreach (var f in fields)
        {
            object a = f.GetValue(dst), b = f.GetValue(src);
            string sa = a is AnimationCurve ca ? $"カーブ({ca.length}キー)" : a?.ToString();
            string sbv = b is AnimationCurve cb ? $"カーブ({cb.length}キー)" : b?.ToString();
            sb.AppendLine($"{f.Name}: {sa} → {sbv}");
        }
        if (!EditorUtility.DisplayDialog("③MissileをGolemの軌道に",
                "EnemyData_NeonDancerの③Missileの軌道の数値をGolemのMissileと同じにします（自機狙い・画像・SEは変更しません。Golemは読むだけ）：\n\n" + sb,
                "適用する", "キャンセル"))
            return;

        Undo.RecordObject(data, "NeonDancer Missile Arc");
        foreach (var f in fields)
        {
            object v = f.GetValue(src);
            if (v is AnimationCurve curve) v = new AnimationCurve(curve.keys) { preWrapMode = curve.preWrapMode, postWrapMode = curve.postWrapMode };
            f.SetValue(dst, v);
        }
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        Debug.Log("[NeonDancerSetupTool] ③MissileをGolemの軌道にしました\n" + sb);
        EditorUtility.DisplayDialog("NeonDancer", "適用しました。", "OK");
    }

    [MenuItem("Tools/NeonDancer/2 前半の攻撃・ステージ/⑤煙幕の範囲・Just反射")]
    private static void ApplySmokeRangeFix()
    {
        if (AssetDatabase.LoadMainAssetAtPath(SmokeDstPrefab) == null)
        {
            EditorUtility.DisplayDialog("NeonDancer", "先に「⑤Smoke専用エフェクトを作成して適用」を実行してください。", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("NeonDancer 修正を適用",
                "以下を上書きします：\n" +
                "・EnemyData_NeonDancerの⑤：Smoke Radius 4.5／Smoke Expansion Speed 1.5\n" +
                "・NeonDancer_SmokeParticle：SandGrainPS（Area6〜10）の初速0.3／生成位置の記録用にNeonDancerSmokeMarkerを追加\n続けますか？",
                "適用する", "キャンセル"))
            return;

        var data = AssetDatabase.LoadAssetAtPath<EnemyData>(DstData);
        foreach (var bt in data.bulletTypes)
        {
            if (bt == null || !bt.useSmokeGrenade) continue;
            Undo.RecordObject(data, "NeonDancer Smoke Range");
            bt.smokeRadius = 4.5f;
            bt.smokeExpansionSpeed = 1.5f;
            EditorUtility.SetDirty(data);
        }

        GameObject root = PrefabUtility.LoadPrefabContents(SmokeDstPrefab);
        try
        {
            var cloudSo = new SerializedObject(root.GetComponent<SmokeCloud>());
            var subPs = cloudSo.FindProperty("sandGrainParticle").objectReferenceValue as ParticleSystem;
            if (subPs != null) { var main = subPs.main; main.startSpeed = 0.3f; }
            if (root.GetComponent<NeonDancerSmokeMarker>() == null) root.AddComponent<NeonDancerSmokeMarker>();
            PrefabUtility.SaveAsPrefabAsset(root, SmokeDstPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[NeonDancerSetupTool] 修正を適用しました（⑤煙幕の範囲・Just反射）");
        EditorUtility.DisplayDialog("NeonDancer", "適用しました。", "OK");
    }

    // ======================================================
    // ⑤煙幕に「ネオンの霧」を追加（通常の半透明合成の大きくぼやけた塊で後ろを覆い隠す）
    // ======================================================
    private const string HazeMatSrc = "Assets/Materials/Mat_SmokeAlpha.mat";   // URP Particles/Unlit・半透明合成（SrcBlend=5/DstBlend=10）
    private const string HazeMatDst = ArtFolder + "/ND_SmokeHaze.mat";

    [MenuItem("Tools/NeonDancer/2 前半の攻撃・ステージ/⑤煙幕にネオンの霧を追加")]
    private static void AddSmokeHaze()
    {
        if (AssetDatabase.LoadMainAssetAtPath(SmokeDstPrefab) == null)
        {
            EditorUtility.DisplayDialog("NeonDancer", "先に「⑤Smoke専用エフェクトを作成して適用」を実行してください。", "OK");
            return;
        }
        var softGlow = AssetDatabase.LoadAssetAtPath<Texture2D>(SoftGlowPath);
        if (AssetDatabase.LoadMainAssetAtPath(HazeMatSrc) == null || softGlow == null)
        {
            EditorUtility.DisplayDialog("NeonDancer", $"コピー元が見つかりません：\n{HazeMatSrc}\n{SoftGlowPath}", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("⑤煙幕にネオンの霧を追加",
                "NeonDancer_SmokeParticleに霧（ND_HazeA：Area1〜5色／ND_HazeB：Area6〜10色）とNeonDancerSmokeHazeを追加します。\n" +
                "既に霧がある場合は設定を上書きします。マテリアルND_SmokeHaze.matはMat_SmokeAlpha.matのコピーです（元は変更しません）。\n続けますか？",
                "追加する", "キャンセル"))
            return;

        if (AssetDatabase.LoadMainAssetAtPath(HazeMatDst) == null) AssetDatabase.CopyAsset(HazeMatSrc, HazeMatDst);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(HazeMatDst);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", softGlow);
        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", softGlow);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
        EditorUtility.SetDirty(mat);

        GameObject root = PrefabUtility.LoadPrefabContents(SmokeDstPrefab);
        try
        {
            var smokePs = root.GetComponent<ParticleSystem>();
            var a = GetOrCreateChildPs(root.transform, "ND_HazeA");
            var b = GetOrCreateChildPs(root.transform, "ND_HazeB");
            SetupHazePs(a, mat, new[] { AreaNodeColors[0], AreaNodeColors[1], AreaNodeColors[2], AreaNodeColors[3], AreaNodeColors[4] }, 0.5f);
            SetupHazePs(b, mat, new[] { AreaNodeColors[5], AreaNodeColors[6], AreaNodeColors[7], AreaNodeColors[8], AreaNodeColors[9] }, -0.4f);

            var haze = root.GetComponent<NeonDancerSmokeHaze>();
            if (haze == null) haze = root.AddComponent<NeonDancerSmokeHaze>();
            var so = new SerializedObject(haze);
            SetArray(so.FindProperty("hazeSystems"), a, b);
            so.FindProperty("smokeParticle").objectReferenceValue = smokePs;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, SmokeDstPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[NeonDancerSetupTool] ⑤煙幕にネオンの霧を追加しました");
        EditorUtility.DisplayDialog("NeonDancer", "追加しました。", "OK");
    }

    // ======================================================
    // ⑤煙幕の負荷軽減：霧（ND_HazeA/B）の1秒あたりの発生数を半分にし、代わりに煙幕に重なった弾を隠す部品を追加する
    //  ・発生数の半減は初回（NeonDancerSmokeBulletHiderがまだ無い時）だけ行う（再実行でさらに半分にならないように）
    //  ・SmokeCloud（共有）は変更しない
    // ======================================================
    [MenuItem("Tools/NeonDancer/2 前半の攻撃・ステージ/⑤煙幕の霧を減らして弾を隠す（負荷軽減）")]
    private static void ReduceSmokeHazeAndHideBullets()
    {
        if (AssetDatabase.LoadMainAssetAtPath(SmokeDstPrefab) == null)
        {
            EditorUtility.DisplayDialog("NeonDancer", "NeonDancer_SmokeParticle.prefabが見つかりません。", "OK");
            return;
        }
        GameObject root = PrefabUtility.LoadPrefabContents(SmokeDstPrefab);
        try
        {
            bool firstTime = root.GetComponent<NeonDancerSmokeBulletHider>() == null;
            var hazes = new System.Collections.Generic.List<ParticleSystem>();
            foreach (string n in new[] { "ND_HazeA", "ND_HazeB" })
            {
                Transform t = root.transform.Find(n);
                var ps = t != null ? t.GetComponent<ParticleSystem>() : null;
                if (ps != null) hazes.Add(ps);
            }
            var before = new System.Text.StringBuilder();
            foreach (var ps in hazes) before.AppendLine($"{ps.name}：{DescribeRate(ps.emission.rateOverTime)}/秒");

            string msg = firstTime
                ? "NeonDancer_SmokeParticleを次のように変更します：\n" +
                  "・霧の1秒あたりの発生数を半分にする\n" + before +
                  "・煙幕に重なった弾を隠す部品（NeonDancerSmokeBulletHider）を追加\n続けますか？"
                : "弾を隠す部品は追加済みのため、霧の発生数は変更しません（再実行でさらに半分になるのを防ぐため）。\n現在の霧：\n" + before;
            if (!firstTime)
            {
                EditorUtility.DisplayDialog("⑤煙幕の負荷軽減", msg, "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("⑤煙幕の負荷軽減", msg, "変更する", "キャンセル")) return;

            var after = new System.Text.StringBuilder();
            foreach (var ps in hazes)
            {
                var em = ps.emission;
                em.rateOverTime = ScaleCurve(em.rateOverTime, 0.5f);
                after.AppendLine($"{ps.name}：{DescribeRate(em.rateOverTime)}/秒");
            }
            root.AddComponent<NeonDancerSmokeBulletHider>();
            PrefabUtility.SaveAsPrefabAsset(root, SmokeDstPrefab);
            Debug.Log("[NeonDancerSetupTool] ⑤煙幕の負荷軽減：霧の発生数を半分（変更前\n" + before + "変更後\n" + after + "）＋NeonDancerSmokeBulletHider追加");
            EditorUtility.DisplayDialog("⑤煙幕の負荷軽減", "変更しました。\n変更後：\n" + after, "OK");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
    }

    private static ParticleSystem.MinMaxCurve ScaleCurve(ParticleSystem.MinMaxCurve c, float k)
    {
        switch (c.mode)
        {
            case ParticleSystemCurveMode.Constant:
                return new ParticleSystem.MinMaxCurve(c.constant * k);
            case ParticleSystemCurveMode.TwoConstants:
                return new ParticleSystem.MinMaxCurve(c.constantMin * k, c.constantMax * k);
            default:
                c.curveMultiplier *= k;
                return c;
        }
    }

    private static string DescribeRate(ParticleSystem.MinMaxCurve c)
    {
        switch (c.mode)
        {
            case ParticleSystemCurveMode.Constant: return c.constant.ToString("0.##");
            case ParticleSystemCurveMode.TwoConstants: return $"{c.constantMin:0.##}〜{c.constantMax:0.##}";
            default: return $"カーブ×{c.curveMultiplier:0.##}";
        }
    }

    private static ParticleSystem GetOrCreateChildPs(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        if (t == null)
        {
            t = new GameObject(name).transform;
            t.SetParent(parent, false);
        }
        var ps = t.GetComponent<ParticleSystem>();
        if (ps == null) ps = t.gameObject.AddComponent<ParticleSystem>();
        return ps;
    }

    private static void SetupHazePs(ParticleSystem ps, Material mat, Color[] colors, float orbitalZ)
    {
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = mat;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sortingLayerName = "Default";
        renderer.sortingOrder = 999; // 光る粒子(1000)の奥

        var g = new Gradient { mode = GradientMode.Fixed };
        var ck = new GradientColorKey[colors.Length];
        for (int i = 0; i < colors.Length; i++) ck[i] = new GradientColorKey(colors[i], (i + 1) / (float)colors.Length);
        g.SetKeys(ck, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });

        var main = ps.main;
        main.playOnAwake = false;           // NeonDancerSmokeHazeがSmokeCloudの初期化後に再生する
        main.loop = true;
        main.duration = 5f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 3f);
        main.startSize = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = new ParticleSystem.MinMaxGradient(g) { mode = ParticleSystemGradientMode.RandomColor };
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.maxParticles = 200;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(14f);

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere; // 半径はNeonDancerSmokeHazeが煙幕の範囲判定に合わせて毎フレーム更新
        shape.radius = 0.1f;
        shape.radiusThickness = 1f;

        // ゆっくり自転
        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.separateAxes = false;
        rot.z = new ParticleSystem.MinMaxCurve(-0.35f, 0.35f);

        // 渦：★x/y/z・orbitalX/Y/Z・radialは未使用軸も含め全て同じモード（Constant）で明示設定する
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(0f);
        vel.y = new ParticleSystem.MinMaxCurve(0f);
        vel.z = new ParticleSystem.MinMaxCurve(0f);
        vel.orbitalX = new ParticleSystem.MinMaxCurve(0f);
        vel.orbitalY = new ParticleSystem.MinMaxCurve(0f);
        vel.orbitalZ = new ParticleSystem.MinMaxCurve(orbitalZ);
        vel.radial = new ParticleSystem.MinMaxCurve(0f);

        // アルファはNeonDancerSmokeHazeが毎フレーム設定（煙幕全体の透明度と同期）
        var col = ps.colorOverLifetime;
        col.enabled = true;
    }

    private static void SetupSparklePs(ParticleSystem ps, Material mat, Color[] colors, float startSpeed, float orbitalZ)
    {
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = mat;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        // 色：5色を等確率で（Fixedモードのグラデーションから「ランダムな色」）
        var g = new Gradient { mode = GradientMode.Fixed };
        var ck = new GradientColorKey[colors.Length];
        for (int i = 0; i < colors.Length; i++) ck[i] = new GradientColorKey(colors[i], (i + 1) / (float)colors.Length);
        g.SetKeys(ck, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });

        var main = ps.main;
        main.startColor = new ParticleSystem.MinMaxGradient(g) { mode = ParticleSystemGradientMode.RandomColor };
        main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
        main.startSpeed = startSpeed;
        main.gravityModifier = 0f;
        main.maxParticles = 1000;

        var emission = ps.emission;
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(40f);

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere; // 半径はSmokeCloudが拡散に合わせて毎フレーム更新する
        shape.radiusThickness = 1f;

        // 瞬き：大きさを寿命の中で何度か脈打たせる（透明度はSmokeCloudが毎フレーム上書きするため大きさで表現）
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        var twinkle = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.06f, 1f), new Keyframe(0.18f, 0.35f), new Keyframe(0.30f, 1f),
            new Keyframe(0.42f, 0.35f), new Keyframe(0.54f, 1f), new Keyframe(0.66f, 0.35f), new Keyframe(0.78f, 1f),
            new Keyframe(0.90f, 0.35f), new Keyframe(1f, 0f));
        sol.size = new ParticleSystem.MinMaxCurve(1f, twinkle);

        // 渦：中心の周りを回る。★x/y/z・orbitalX/Y/Zは未使用軸も含め全て同じモード（Constant）で明示設定する
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(0f);
        vel.y = new ParticleSystem.MinMaxCurve(0f);
        vel.z = new ParticleSystem.MinMaxCurve(0f);
        vel.orbitalX = new ParticleSystem.MinMaxCurve(0f);
        vel.orbitalY = new ParticleSystem.MinMaxCurve(0f);
        vel.orbitalZ = new ParticleSystem.MinMaxCurve(orbitalZ);
        vel.radial = new ParticleSystem.MinMaxCurve(0f);
    }

    // ======================================================
    // ワームホール画像（外側の渦／内側の渦［逆回転］／中心の穴）をプログラム生成してPrefabを3層にする
    // ======================================================
    private const string WormholeSettingsPath = ArtFolder + "/ND_WormholeTextureSettings.asset";
    private const string WormholeAdditiveMat  = ArtFolder + "/ND_WormholeAdditive.mat";

    [MenuItem("Tools/NeonDancer/1 セットアップ/ワームホール画像を生成してPrefabに適用")]
    private static void BuildWormholeVisual()
    {
        if (AssetDatabase.LoadMainAssetAtPath(WormholePrefabPath) == null)
        {
            EditorUtility.DisplayDialog("NeonDancer", "先に「プレハブとEnemyDataを作成（初回のみ）」を実行してください。", "OK");
            return;
        }
        var shader = Shader.Find("Sprites/Additive");
        if (shader == null)
        {
            EditorUtility.DisplayDialog("NeonDancer", "シェーダー「Sprites/Additive」（Assets/Shaders/Sprite_Additive.shader）が見つかりません。", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("ワームホール画像",
                "ワームホール画像3枚（外側の渦・内側の渦・中心の穴）を生成し、NeonDancer_Wormhole.prefabを3層（内側は逆回転）にします。\n" +
                "生成の数値は ND_WormholeTextureSettings（S3_10_NeonDancerフォルダ）のInspectorで調整し、「画像を生成」で作り直せます。\n続けますか？",
                "実行する", "キャンセル"))
            return;

        // 設定アセット（既にあれば調整済みの値をそのまま使う）
        var settings = AssetDatabase.LoadAssetAtPath<NeonDancerWormholeTextureSettings>(WormholeSettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<NeonDancerWormholeTextureSettings>();
            AssetDatabase.CreateAsset(settings, WormholeSettingsPath);
        }
        settings.GenerateAll();

        // 加算合成マテリアル（2D Renderer対応のSprites/Additive）
        var mat = AssetDatabase.LoadAssetAtPath<Material>(WormholeAdditiveMat);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, WormholeAdditiveMat);
        }

        var outerSp = AssetDatabase.LoadAssetAtPath<Sprite>(NeonDancerWormholeTextureSettings.OuterPath);
        var innerSp = AssetDatabase.LoadAssetAtPath<Sprite>(NeonDancerWormholeTextureSettings.InnerPath);
        var voidSp  = AssetDatabase.LoadAssetAtPath<Sprite>(NeonDancerWormholeTextureSettings.VoidPath);

        GameObject root = PrefabUtility.LoadPrefabContents(WormholePrefabPath);
        try
        {
            var outerSr = root.GetComponent<SpriteRenderer>();
            outerSr.sprite = outerSp;
            outerSr.sharedMaterial = mat;
            int layerId = outerSr.sortingLayerID;
            int order = outerSr.sortingOrder;

            SpriteRenderer innerSr = GetOrCreateChildSr(root.transform, "Inner");
            innerSr.transform.localScale = new Vector3(0.55f, 0.55f, 1f);
            innerSr.sprite = innerSp;
            innerSr.sharedMaterial = mat;
            innerSr.sortingLayerID = layerId;
            innerSr.sortingOrder = order + 1;

            SpriteRenderer voidSr = GetOrCreateChildSr(root.transform, "Void");
            voidSr.transform.localScale = new Vector3(0.3f, 0.3f, 1f);
            voidSr.sprite = voidSp;
            voidSr.color = Color.black;
            voidSr.sortingLayerID = layerId;
            voidSr.sortingOrder = order + 2; // 中心の黒い穴は一番手前

            var so = new SerializedObject(root.GetComponent<NeonDancerWormhole>());
            so.FindProperty("spriteRenderer").objectReferenceValue = outerSr;
            SetArray(so.FindProperty("extraTintedRenderers"), innerSr);
            SetArray(so.FindProperty("untintedRenderers"), voidSr);
            SetArray(so.FindProperty("counterSpinLayers"), innerSr.transform);
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, WormholePrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        Selection.activeObject = settings;
        Debug.Log("[NeonDancerSetupTool] ワームホール画像を生成し、NeonDancer_Wormhole.prefabを3層にしました");
        EditorUtility.DisplayDialog("NeonDancer", "適用しました。\n見た目の調整はND_WormholeTextureSettingsのInspectorで行えます（今選択しています）。", "OK");
    }

    // ======================================================
    // ⑦Beamの予告：予兆線（Bullet TypeのTelegraph）・警告SE（Bitと同じ）・溜めエフェクト（BitのVFX_GyrorbCharge）
    // ======================================================
    private const string TelegraphSePath   = "Assets/Audio/Bullet/ロボットの目が光る.mp3";        // Bit.prefab BitController.chargeStartSE
    private const string ChargeVfxPath     = "Assets/Prefabs/Effects/VFX_GyrorbCharge.prefab";      // Bit.prefab BeamChargeGlowの元プレハブ

    [MenuItem("Tools/NeonDancer/2 前半の攻撃・ステージ/⑦Beamの予兆線・警告SE・溜めエフェクト")]
    private static void ApplyBeamTelegraph()
    {
        var se = AssetDatabase.LoadAssetAtPath<AudioClip>(TelegraphSePath);
        var vfx = AssetDatabase.LoadAssetAtPath<GameObject>(ChargeVfxPath);
        if (se == null || vfx == null || AssetDatabase.LoadMainAssetAtPath(WormholePrefabPath) == null)
        {
            EditorUtility.DisplayDialog("NeonDancer", $"素材が見つかりません：\n{TelegraphSePath}\n{ChargeVfxPath}\n{WormholePrefabPath}", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("⑦Beamの予告",
                "以下を設定します（Bit・VFX_GyrorbChargeは読むだけで変更しません）：\n" +
                "・EnemyData_NeonDancerの⑦：Use Telegraph ON／Telegraph Seconds 0.6／点滅3回\n" +
                "・NeonDancerControllerのTelegraph SE：ロボットの目が光る\n" +
                "・NeonDancer_Wormholeに溜めエフェクト（ChargeGlow＝VFX_GyrorbCharge）を追加\n続けますか？",
                "設定する", "キャンセル"))
            return;

        // ⑦Beam：予兆線
        var data = AssetDatabase.LoadAssetAtPath<EnemyData>(DstData);
        foreach (var bt in data.bulletTypes)
        {
            if (bt == null || !bt.useBeam) continue;
            Undo.RecordObject(data, "NeonDancer Beam Telegraph");
            bt.useTelegraph = true;
            bt.telegraphSeconds = 0.6f;
            bt.telegraphUseBlink = true;
            bt.telegraphBlinkCount = 3;
            bt.telegraphBlinkMinAlphaMul = 0.2f;
            EditorUtility.SetDirty(data);
        }

        // 警告SE
        GameObject nd = PrefabUtility.LoadPrefabContents(DstPrefab);
        try
        {
            var so = new SerializedObject(nd.GetComponent<NeonDancerController>());
            so.FindProperty("telegraphSE").objectReferenceValue = se;
            so.FindProperty("telegraphSEVolume").floatValue = 1f; // BitのchargeStartSEVolumeと同じ
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(nd, DstPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(nd);
        }

        // ワームホール：溜めエフェクト
        GameObject wh = PrefabUtility.LoadPrefabContents(WormholePrefabPath);
        try
        {
            Transform glow = wh.transform.Find("ChargeGlow");
            if (glow == null)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(vfx, wh.transform);
                inst.name = "ChargeGlow";
                glow = inst.transform;
            }
            glow.localPosition = Vector3.zero;
            glow.localRotation = Quaternion.identity;
            glow.localScale = Vector3.one;
            foreach (var ps in glow.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.playOnAwake = false;  // 溜めの間だけコードから再生する
                main.loop = false;         // Bitと同じく1回だけ再生（ループさせると溜めの間に何度も繰り返す）
                main.scalingMode = ParticleSystemScalingMode.Local; // ワームホールの拡大縮小（0→Size）の影響を受けない
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            var so = new SerializedObject(wh.GetComponent<NeonDancerWormhole>());
            so.FindProperty("chargeEffect").objectReferenceValue = glow.GetComponent<ParticleSystem>();
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(wh, WormholePrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(wh);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[NeonDancerSetupTool] ⑦Beamの予兆線・警告SE・溜めエフェクトを設定しました");
        EditorUtility.DisplayDialog("NeonDancer", "設定しました。", "OK");
    }

    [MenuItem("Tools/NeonDancer/2 前半の攻撃・ステージ/⑦予兆線なし・吸い込み1回")]
    private static void ApplyBeamNoTelegraphSingleCharge()
    {
        if (!EditorUtility.DisplayDialog("⑦Beamの予告",
                "以下を設定します：\n・EnemyData_NeonDancerの⑦：Use Telegraph OFF（予兆線なし）\n" +
                "・NeonDancer_WormholeのChargeGlow：ループをやめ1回だけ再生（BitのVFX_GyrorbChargeの元設定と同じ）\n" +
                "警告SEと吸い込みエフェクトはBeamの時に出ます。続けますか？",
                "設定する", "キャンセル"))
            return;

        var data = AssetDatabase.LoadAssetAtPath<EnemyData>(DstData);
        foreach (var bt in data.bulletTypes)
        {
            if (bt == null || !bt.useBeam) continue;
            Undo.RecordObject(data, "NeonDancer Beam No Telegraph");
            bt.useTelegraph = false;
            EditorUtility.SetDirty(data);
        }

        GameObject wh = PrefabUtility.LoadPrefabContents(WormholePrefabPath);
        try
        {
            Transform glow = wh.transform.Find("ChargeGlow");
            if (glow != null)
            {
                foreach (var ps in glow.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = ps.main;
                    main.loop = false;
                }
                PrefabUtility.SaveAsPrefabAsset(wh, WormholePrefabPath);
            }
            else Debug.LogWarning("[NeonDancerSetupTool] NeonDancer_WormholeにChargeGlowがありません（先に⑦Beamの予兆線・警告SE・溜めエフェクトを実行）");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(wh);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[NeonDancerSetupTool] ⑦予兆線なし・吸い込み1回にしました");
        EditorUtility.DisplayDialog("NeonDancer", "設定しました。", "OK");
    }

    // ======================================================
    // ⑨Drillの線ヒット中間エフェクト：Tsukuyomiと同じ弾プレハブを使う
    //  （中間ヒットのエフェクトは弾プレハブのEnemyBulletFeedback.Paddle Hit Vfx Prefabから出る。共通のEnemyBullet.prefabは空）
    // ======================================================
    private const string TsukuyomiBulletPrefabPath = "Assets/Prefabs/EnemyBullet_Tsukuyomi.prefab";        // TsukuyomiController.bulletPrefab

    [MenuItem("Tools/NeonDancer/2 前半の攻撃・ステージ/⑨Drillの線ヒットエフェクト")]
    private static void ApplyLineHitTickVfx()
    {
        var drillPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TsukuyomiBulletPrefabPath);
        if (drillPrefab == null || drillPrefab.GetComponent<EnemyBullet>() == null)
        {
            EditorUtility.DisplayDialog("NeonDancer", $"素材が見つかりません：{TsukuyomiBulletPrefabPath}", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("⑨Drillの線ヒットエフェクト",
                "以下を設定します（Tsukuyomiの素材は読むだけで変更しません）：\n" +
                "・NeonDancerControllerのDrill Bullet Prefab：EnemyBullet_Tsukuyomi（⑨Drillだけに使用）\n続けますか？",
                "設定する", "キャンセル"))
            return;

        GameObject nd = PrefabUtility.LoadPrefabContents(DstPrefab);
        try
        {
            var so = new SerializedObject(nd.GetComponent<NeonDancerController>());
            so.FindProperty("drillBulletPrefab").objectReferenceValue = drillPrefab.GetComponent<EnemyBullet>();
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(nd, DstPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(nd);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("[NeonDancerSetupTool] ⑨Drillの線ヒットエフェクト（Tsukuyomiと同じ弾プレハブ）を設定しました");
        EditorUtility.DisplayDialog("NeonDancer", "設定しました。", "OK");
    }

    // ======================================================
    // ⑦Beam／BitのBeam：反射ごとのヒットエフェクトをDragonの「Breath」と同じにする
    //  （DragonはPaddle Hit Vfx Prefab／Floor Hit Vfx PrefabにVFX_EnemyHit_Normalを設定済み。Bitは未設定だった）
    // ======================================================
    private const string BeamHitVfxPath = "Assets/Prefabs/Effects/VFX_EnemyHit_Normal.prefab";   // EnemyData_Dragon「Breath」と同じ

    [MenuItem("Tools/NeonDancer/2 前半の攻撃・ステージ/⑦とBitのBeam反射エフェクトをDragonと同じに")]
    private static void ApplyBeamHitVfxLikeDragon()
    {
        var vfx = AssetDatabase.LoadAssetAtPath<GameObject>(BeamHitVfxPath);
        if (vfx == null)
        {
            EditorUtility.DisplayDialog("NeonDancer", $"素材が見つかりません：{BeamHitVfxPath}", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("Beamの反射エフェクト",
                "以下を設定します（DragonのBreathと同じ）：\n" +
                "・EnemyData_NeonDancerの⑦（Use BeamのBulletType）：Paddle Hit Vfx Prefab／Floor Hit Vfx Prefab＝VFX_EnemyHit_Normal\n" +
                "・Bit.prefab > BitController > Beam Bullet Type：同上（ObeliskのBitにも反映されます）\n" +
                "※Bit.prefabをPrefabモードで開いて未保存の変更がある場合は、先に保存してから実行してください。\n続けますか？",
                "設定する", "キャンセル"))
            return;

        // NeonDancer ⑦
        var data = AssetDatabase.LoadAssetAtPath<EnemyData>(DstData);
        if (data != null)
        {
            Undo.RecordObject(data, "NeonDancer Beam Hit Vfx");
            foreach (var bt in data.bulletTypes)
            {
                if (bt == null || !bt.useBeam) continue;
                bt.paddleHitVfxPrefab = vfx;
                bt.floorHitVfxPrefab = vfx;
                Debug.Log($"[NeonDancerSetupTool] EnemyData_NeonDancer「{bt.name}」にBeam反射エフェクトを設定");
            }
            EditorUtility.SetDirty(data);
        }

        // Bit（beamBulletType以外の値・未コミットの変更には触れない）
        GameObject bit = PrefabUtility.LoadPrefabContents(BitPrefabPath);
        try
        {
            var bc = bit.GetComponent<BitController>();
            if (bc != null)
            {
                var so = new SerializedObject(bc);
                so.FindProperty("beamBulletType.paddleHitVfxPrefab").objectReferenceValue = vfx;
                so.FindProperty("beamBulletType.floorHitVfxPrefab").objectReferenceValue = vfx;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(bit, BitPrefabPath);
                Debug.Log("[NeonDancerSetupTool] Bit.prefab BitController.beamBulletTypeにBeam反射エフェクトを設定");
            }
            else Debug.LogWarning("[NeonDancerSetupTool] Bit.prefabにBitControllerがありません");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(bit);
        }

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("NeonDancer", "設定しました。", "OK");
    }

    // ======================================================
    // Area10 Final Stage開始演出（次元移動→登場→カットイン→VS）をシーンに配置する
    //  ★既に配置済みの場合は何もしない（調整済みの値が消える事故を防ぐ）
    // ======================================================
    private const string DimensionWarpMatPath = ArtFolder + "/ND_DimensionWarp.mat";

    [MenuItem("Tools/NeonDancer/1 セットアップ/Final Stage開始演出をシーンに配置")]
    private static void PlaceFinalIntro()
    {
        var bossRush = Object.FindFirstObjectByType<Area10BossRushController>(FindObjectsInactive.Include);
        if (bossRush == null)
        {
            EditorUtility.DisplayDialog("Final Stage開始演出", "Area10BossRushControllerが見つかりません。05_Gameシーンを開いてから実行してください。", "OK");
            return;
        }
        if (Object.FindFirstObjectByType<Area10FinalIntroController>(FindObjectsInactive.Include) != null)
        {
            EditorUtility.DisplayDialog("Final Stage開始演出", "既にシーンに配置されています（作り直す場合は手動で削除してから実行してください）。", "OK");
            return;
        }

        var shader    = Shader.Find("Sprites/Area10DimensionWarp");
        var white     = AssetDatabase.LoadAssetAtPath<Sprite>(WhitePngPath);
        var outerSp   = AssetDatabase.LoadAssetAtPath<Sprite>(NeonDancerWormholeTextureSettings.OuterPath);
        var innerSp   = AssetDatabase.LoadAssetAtPath<Sprite>(NeonDancerWormholeTextureSettings.InnerPath);
        var voidSp    = AssetDatabase.LoadAssetAtPath<Sprite>(NeonDancerWormholeTextureSettings.VoidPath);
        var addMat    = AssetDatabase.LoadAssetAtPath<Material>(WormholeAdditiveMat);
        var sparkMat  = AssetDatabase.LoadAssetAtPath<Material>(SparkleMatDst);
        if (shader == null || white == null || outerSp == null || innerSp == null || voidSp == null || addMat == null || sparkMat == null)
        {
            EditorUtility.DisplayDialog("Final Stage開始演出", "素材が見つかりません。Consoleを確認してください。", "OK");
            Debug.LogError($"[NeonDancerSetupTool] shader={shader} white={white} outer={outerSp} inner={innerSp} void={voidSp} additiveMat={addMat} sparkleMat={sparkMat}");
            return;
        }

        var bossRushSo = new SerializedObject(bossRush);
        var stageIntro = bossRushSo.FindProperty("stageIntroController").objectReferenceValue as StageIntroController;
        if (stageIntro == null)
        {
            EditorUtility.DisplayDialog("Final Stage開始演出", "Area10BossRushControllerのStage Intro Controllerが未設定です。", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("Final Stage開始演出",
                "シーンに「Area10FinalIntro」を作成し、Area10BossRushControllerのFinal Introに設定します。\n" +
                "（作成後はシーンを保存してください）\n続けますか？",
                "配置する", "キャンセル"))
            return;

        // --- 次元移動用マテリアル ---
        var warpMat = AssetDatabase.LoadAssetAtPath<Material>(DimensionWarpMatPath);
        if (warpMat == null)
        {
            warpMat = new Material(shader);
            AssetDatabase.CreateAsset(warpMat, DimensionWarpMatPath);
        }

        var scene = bossRush.gameObject.scene;
        var root = new GameObject("Area10FinalIntro");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
        Undo.RegisterCreatedObjectUndo(root, "Place Area10 Final Intro");
        var intro = root.AddComponent<Area10FinalIntroController>();

        // 取り込み画面
        var warpSr = new GameObject("DimensionWarp").AddComponent<SpriteRenderer>();
        warpSr.transform.SetParent(root.transform, false);
        warpSr.sprite = white;
        warpSr.sharedMaterial = warpMat;
        warpSr.sortingLayerName = "Default";
        warpSr.sortingOrder = 1100;

        // 巨大ワームホール（外側の渦／内側の渦［逆回転］／中心の穴）
        var wormRoot = new GameObject("DimensionWormhole").transform;
        wormRoot.SetParent(root.transform, false);
        var outerSr = new GameObject("Outer").AddComponent<SpriteRenderer>();
        outerSr.transform.SetParent(wormRoot, false);
        outerSr.sprite = outerSp;
        outerSr.sharedMaterial = addMat;
        var innerSr = new GameObject("Inner").AddComponent<SpriteRenderer>();
        innerSr.transform.SetParent(wormRoot, false);
        innerSr.transform.localScale = new Vector3(0.55f, 0.55f, 1f);
        innerSr.sprite = innerSp;
        innerSr.sharedMaterial = addMat;
        var voidSr = new GameObject("Void").AddComponent<SpriteRenderer>();
        voidSr.transform.SetParent(wormRoot, false);
        voidSr.sprite = voidSp;
        voidSr.color = Color.black;
        foreach (var sr in new[] { outerSr, innerSr, voidSr }) sr.sortingLayerName = "Default";
        outerSr.sortingOrder = 1101; innerSr.sortingOrder = 1102; voidSr.sortingOrder = 1103;

        // 9色の光の粒（スクリプトから1粒ずつ発生させる。色・速度・寿命はArea10FinalIntroController側で指定）
        var ps = new GameObject("DimensionSparkles").AddComponent<ParticleSystem>();
        ps.transform.SetParent(root.transform, false);
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = 0f;
        main.startLifetime = 1f;
        main.gravityModifier = 0f;
        main.maxParticles = 2000;
        var emission = ps.emission;
        emission.enabled = false;
        var shape = ps.shape;
        shape.enabled = false;
        var colorOl = ps.colorOverLifetime;
        colorOl.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
        colorOl.color = new ParticleSystem.MinMaxGradient(g);
        var sizeOl = ps.sizeOverLifetime;
        sizeOl.enabled = true;
        sizeOl.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.3f)));
        var psr = ps.GetComponent<ParticleSystemRenderer>();
        psr.sharedMaterial = sparkMat;
        psr.renderMode = ParticleSystemRenderMode.Billboard;
        psr.sortingLayerName = "Default";
        psr.sortingOrder = 1104;

        // 参照
        var so = new SerializedObject(intro);
        so.FindProperty("bossRushController").objectReferenceValue = bossRush;
        so.FindProperty("stageIntroController").objectReferenceValue = stageIntro;
        so.FindProperty("targetCamera").objectReferenceValue = Camera.main;
        so.FindProperty("warpRenderer").objectReferenceValue = warpSr;
        so.FindProperty("wormholeRoot").objectReferenceValue = wormRoot;
        SetArray(so.FindProperty("wormholeTintedLayers"), outerSr, innerSr);
        SetArray(so.FindProperty("wormholeCounterSpinLayers"), innerSr.transform);
        so.FindProperty("wormholeVoid").objectReferenceValue = voidSr;
        so.FindProperty("sparkleParticles").objectReferenceValue = ps;
        so.ApplyModifiedPropertiesWithoutUndo();

        Undo.RecordObject(bossRush, "Set Final Intro");
        bossRushSo.Update();
        bossRushSo.FindProperty("finalIntro").objectReferenceValue = intro;
        bossRushSo.ApplyModifiedProperties();

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = root;
        Debug.Log("[NeonDancerSetupTool] Area10FinalIntroをシーンに配置し、Area10BossRushControllerのFinal Introに設定しました（シーンの保存が必要）");
        EditorUtility.DisplayDialog("Final Stage開始演出", "配置しました。シーンを保存してください（Ctrl+S）。", "OK");
    }

    // ======================================================
    // 後半移行（ダウン・魂・Finish）：プレイヤーの画像・数値をNeonDancerへコピーし、魂と発光用のオブジェクトを作る
    //  コピー元（読むだけで変更しない）：05_Gameシーンのプレイヤー（StageIntroController.Pixel Dancer Renderer）の
    //  PixelDancerController（Collapse Frames / Soul Frames / 落下の数値 / Soul）と StageIntroController（Finish Frames）
    // ======================================================
    [MenuItem("Tools/NeonDancer/3 後半移行/ダウン・魂・Finishをプレイヤーからコピー")]
    private static void CopyPhaseTransitionFromPlayer()
    {
        var stageIntro = Object.FindFirstObjectByType<StageIntroController>(FindObjectsInactive.Include);
        var dancerSr = stageIntro != null ? stageIntro.PixelDancerRenderer : null;
        var pdc = dancerSr != null ? dancerSr.GetComponent<PixelDancerController>() : null;
        var addMat = AssetDatabase.LoadAssetAtPath<Material>(WormholeAdditiveMat);
        if (pdc == null || addMat == null)
        {
            EditorUtility.DisplayDialog("後半移行", "05_Gameシーンを開いてから実行してください（プレイヤーのPixelDancerController、または加算マテリアルが見つかりません）。", "OK");
            return;
        }
        var pdcSo = new SerializedObject(pdc);
        var introSo = new SerializedObject(stageIntro);
        var srcCollapse = pdcSo.FindProperty("collapseFrames");
        var srcSoulFrames = pdcSo.FindProperty("soulFrames");
        var srcFinish = introSo.FindProperty("finishFrames");
        var soulT = pdcSo.FindProperty("soulTransform").objectReferenceValue as Transform;
        var soulSr = soulT != null ? soulT.GetComponent<SpriteRenderer>() : null;
        if (srcCollapse == null || srcSoulFrames == null || srcFinish == null || soulSr == null)
        {
            EditorUtility.DisplayDialog("後半移行", "プレイヤーのCollapse Frames / Soul Frames / Soul、またはFinish Framesが見つかりません。", "OK");
            return;
        }
        int finishCount = Mathf.Min(10, srcFinish.arraySize);
        if (!EditorUtility.DisplayDialog("後半移行",
                "プレイヤーの以下をNeonDancer.prefab > NeonDancerControllerにコピーします（プレイヤー側は変更しません）：\n" +
                $"・倒れるアニメ（Collapse Frames {srcCollapse.arraySize}コマ）→ Down Frames\n" +
                $"・魂のアニメ（Soul Frames {srcSoulFrames.arraySize}コマ）・落下の数値 → Soul\n" +
                $"・Finishアニメ（Finish_1〜{finishCount}）→ Finish Frames\n" +
                "・ND_Stage > ND_Soul（魂の画像）と ND_Body > ND_BodyGlow（虹色の発光）を作成\n" +
                "・HP Refill Duration＝2秒\n続けますか？",
                "コピーする", "キャンセル"))
            return;

        GameObject root = PrefabUtility.LoadPrefabContents(DstPrefab);
        try
        {
            var ctrl = root.GetComponent<NeonDancerController>();
            var so = new SerializedObject(ctrl);
            var bodySr = so.FindProperty("spriteRenderer").objectReferenceValue as SpriteRenderer;
            Transform stage = root.transform.Find("ND_Stage");
            if (bodySr == null || stage == null)
            {
                Debug.LogError("[NeonDancerSetupTool] ND_Body（Sprite Renderer）またはND_Stageが見つかりません");
                return;
            }

            CopyPoseFrames(srcCollapse, so.FindProperty("downFrames"), srcCollapse.arraySize);
            CopyPoseFrames(srcSoulFrames, so.FindProperty("soulFrames"), srcSoulFrames.arraySize);
            CopyPoseFrames(srcFinish, so.FindProperty("finishFrames"), finishCount);

            // ※落下速度（Soul Fall Speed）はユーザー調整値（1.5）を保つため、再実行してもコピーしない
            so.FindProperty("soulFallAngleMin").floatValue    = pdcSo.FindProperty("fallAngleMinDegrees").floatValue;
            so.FindProperty("soulFallAngleMax").floatValue    = pdcSo.FindProperty("fallAngleMaxDegrees").floatValue;
            so.FindProperty("soulHopHeight").floatValue       = pdcSo.FindProperty("hopHeight").floatValue;
            so.FindProperty("soulHopDuration").floatValue     = pdcSo.FindProperty("hopDuration").floatValue;
            so.FindProperty("soulHopHorizontal").floatValue   = pdcSo.FindProperty("hopHorizontal").floatValue;
            so.FindProperty("soulHopArcHeight").floatValue    = pdcSo.FindProperty("hopArcHeight").floatValue;
            so.FindProperty("soulOffscreenMargin").floatValue = pdcSo.FindProperty("gameOverYMargin").floatValue;
            so.FindProperty("hpRefillDuration").floatValue    = 2f;

            // プレイヤーの画像に対するボスの大きさ（オフセットに掛ける）
            float playerScale = Mathf.Abs(dancerSr.transform.lossyScale.y);
            float bossScale = Mathf.Abs(bodySr.transform.lossyScale.y);
            so.FindProperty("poseOffsetScale").floatValue = playerScale > 0.0001f ? bossScale / playerScale : 1f;

            // 魂（ND_Stage > ND_Soul）：プレイヤーのSoulと同じ画像・大きさ・描画順
            SpriteRenderer ndSoul = GetOrCreateChildSr(stage, "ND_Soul");
            ndSoul.sprite = soulSr.sprite;
            ndSoul.sharedMaterial = soulSr.sharedMaterial;
            ndSoul.color = soulSr.color;
            ndSoul.sortingLayerID = soulSr.sortingLayerID;
            ndSoul.sortingOrder = soulSr.sortingOrder;
            ndSoul.transform.localScale = soulSr.transform.lossyScale * (playerScale > 0.0001f ? bossScale / playerScale : 1f);
            ndSoul.gameObject.SetActive(false);
            so.FindProperty("soulRenderer").objectReferenceValue = ndSoul;

            // 虹色の発光（ND_Body > ND_BodyGlow）：体と同じ画像を加算合成で重ねる
            SpriteRenderer glow = GetOrCreateChildSr(bodySr.transform, "ND_BodyGlow");
            glow.sharedMaterial = addMat;
            glow.sortingLayerID = bodySr.sortingLayerID;
            glow.sortingOrder = bodySr.sortingOrder + 1;
            glow.color = new Color(1f, 1f, 1f, 0f);
            glow.transform.localPosition = Vector3.zero;
            so.FindProperty("bodyGlowRenderer").objectReferenceValue = glow;

            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, DstPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[NeonDancerSetupTool] 後半移行（ダウン{srcCollapse.arraySize}コマ・魂{srcSoulFrames.arraySize}コマ・Finish{finishCount}コマ）をプレイヤーからコピーしました");
        EditorUtility.DisplayDialog("後半移行", "コピーしました。", "OK");
    }

    // ======================================================
    // 後半移行の追加演出：魂のガイドリング（画像を生成）・虹色の光の粒の尾・救出SE/VFX（プレイヤーからコピー）
    // ======================================================
    private const string SoulGuideRingPath = ArtFolder + "/ND_SoulGuideRing.png";

    [MenuItem("Tools/NeonDancer/3 後半移行/追加演出（魂のガイド・救出SE・虹の尾）")]
    private static void ApplyPhaseTransitionExtras()
    {
        var stageIntro = Object.FindFirstObjectByType<StageIntroController>(FindObjectsInactive.Include);
        var dancerSr = stageIntro != null ? stageIntro.PixelDancerRenderer : null;
        var pdc = dancerSr != null ? dancerSr.GetComponent<PixelDancerController>() : null;
        var addMat = AssetDatabase.LoadAssetAtPath<Material>(WormholeAdditiveMat);
        var sparkMat = AssetDatabase.LoadAssetAtPath<Material>(SparkleMatDst);
        if (pdc == null || addMat == null || sparkMat == null)
        {
            EditorUtility.DisplayDialog("後半移行の追加演出", "05_Gameシーンを開いてから実行してください（プレイヤーのPixelDancerController、またはマテリアルが見つかりません）。", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("後半移行の追加演出",
                "以下を設定します（プレイヤー側は変更しません）：\n" +
                "・魂のガイドリング画像（ND_SoulGuideRing.png）を生成し、ND_Stage > ND_Soul > ND_SoulGuide を作成\n" +
                "・ND_Stage > ND_SoulTrail（虹色の光の粒）を作成\n" +
                "・救出SE/VFX：プレイヤーのRescue Se Clip / Volume / Rescue Vfx Prefabをコピー\n" +
                "・⑤HPバー満タンのSE（Phase2 Ready SE）：プレイヤーのRescue Se Clipと同じ\n" +
                "※先に「後半移行：ダウン・魂・Finishをプレイヤーからコピー」を実行してND_Soulを作っておくこと\n続けますか？",
                "設定する", "キャンセル"))
            return;

        // --- ガイドリング画像（白いリング・外側へなめらかに消える） ---
        const int size = 256;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size * 2f - 1f, dy = (y + 0.5f) / size * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float ring = Mathf.Exp(-Mathf.Pow((r - 0.8f) / 0.06f, 2f));
                float glow = Mathf.Exp(-Mathf.Pow((r - 0.8f) / 0.18f, 2f)) * 0.35f;
                float a = Mathf.Clamp01(ring + glow) * (r < 1f ? 1f : 0f);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(px);
        tex.Apply();
        File.WriteAllBytes(SoulGuideRingPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(SoulGuideRingPath, ImportAssetOptions.ForceUpdate);
        var ringImporter = (TextureImporter)AssetImporter.GetAtPath(SoulGuideRingPath);
        ringImporter.textureType = TextureImporterType.Sprite;
        ringImporter.spriteImportMode = SpriteImportMode.Single;
        ringImporter.alphaIsTransparency = true;
        ringImporter.mipmapEnabled = false;
        ringImporter.textureCompression = TextureImporterCompression.Uncompressed;
        ringImporter.SaveAndReimport();
        var ringSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SoulGuideRingPath);

        var pdcSo = new SerializedObject(pdc);
        GameObject root = PrefabUtility.LoadPrefabContents(DstPrefab);
        try
        {
            var ctrl = root.GetComponent<NeonDancerController>();
            var so = new SerializedObject(ctrl);
            var soulSr = so.FindProperty("soulRenderer").objectReferenceValue as SpriteRenderer;
            Transform stage = root.transform.Find("ND_Stage");
            if (soulSr == null || stage == null)
            {
                Debug.LogError("[NeonDancerSetupTool] ND_Soul（Soul Renderer）またはND_Stageがありません。先に「後半移行：ダウン・魂・Finishをプレイヤーからコピー」を実行してください");
                return;
            }

            // ガイドリング（魂の子・魂より奥に描く）
            SpriteRenderer guide = GetOrCreateChildSr(soulSr.transform, "ND_SoulGuide");
            guide.sprite = ringSprite;
            guide.sharedMaterial = addMat;
            guide.sortingLayerID = soulSr.sortingLayerID;
            guide.sortingOrder = soulSr.sortingOrder - 1;
            guide.color = new Color(1f, 1f, 1f, 0f);
            guide.transform.localPosition = Vector3.zero;
            so.FindProperty("soulGuideRenderer").objectReferenceValue = guide;

            // 虹色の光の粒（ND_Stageの子：魂が消えても粒は自然に消えるまで残る）
            Transform trailT = stage.Find("ND_SoulTrail");
            ParticleSystem ps = trailT != null ? trailT.GetComponent<ParticleSystem>() : null;
            if (ps == null)
            {
                var go = new GameObject("ND_SoulTrail");
                go.transform.SetParent(stage, false);
                ps = go.AddComponent<ParticleSystem>();
            }
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.startLifetime = 1f;
            main.gravityModifier = 0f;
            main.maxParticles = 1000;
            var emission = ps.emission;
            emission.enabled = false;
            var shape = ps.shape;
            shape.enabled = false;
            var colorOl = ps.colorOverLifetime;
            colorOl.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                      new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            colorOl.color = new ParticleSystem.MinMaxGradient(g);
            var sizeOl = ps.sizeOverLifetime;
            sizeOl.enabled = true;
            sizeOl.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.2f)));
            var psr = ps.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = sparkMat;
            psr.renderMode = ParticleSystemRenderMode.Billboard;
            psr.sortingLayerID = soulSr.sortingLayerID;
            psr.sortingOrder = soulSr.sortingOrder - 1;
            so.FindProperty("soulTrailParticles").objectReferenceValue = ps;

            // 救出SE/VFX（プレイヤーと同じ）
            so.FindProperty("rescueSE").objectReferenceValue = pdcSo.FindProperty("rescueSeClip").objectReferenceValue;
            so.FindProperty("rescueSEVolume").floatValue = Mathf.Clamp01(pdcSo.FindProperty("rescueSeVolume").floatValue);
            so.FindProperty("rescueVfxPrefab").objectReferenceValue = pdcSo.FindProperty("rescueVfxPrefab").objectReferenceValue;
            so.FindProperty("rescueVfxSeconds").floatValue = pdcSo.FindProperty("rescueVfxDestroySeconds").floatValue;
            // ⑤HPバー満タン（Floor/Light全快）のSEも、プレイヤーの魂を救出した時と同じSE
            so.FindProperty("phase2ReadySE").objectReferenceValue = pdcSo.FindProperty("rescueSeClip").objectReferenceValue;
            so.FindProperty("phase2ReadySEVolume").floatValue = Mathf.Clamp01(pdcSo.FindProperty("rescueSeVolume").floatValue);

            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, DstPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[NeonDancerSetupTool] 後半移行の追加演出（魂のガイド・虹の尾・救出SE/VFX）を設定しました");
        EditorUtility.DisplayDialog("後半移行の追加演出", "設定しました。", "OK");
    }

    // ======================================================
    // プレイヤーの魂の救出にも、NeonDancerと同じ「魂のガイド」「虹色の尾」を追加する（05_Gameシーンのプレイヤーに配置）
    //  PixelDancerControllerは変更しない（PixelDancerSoulRescueFXが公開状態を見て動く）
    // ======================================================
    [MenuItem("Tools/NeonDancer/6 プレイヤー/魂救出にガイド・虹の尾を追加")]
    private static void AddPlayerSoulRescueFX()
    {
        var stageIntro = Object.FindFirstObjectByType<StageIntroController>(FindObjectsInactive.Include);
        var dancerSr = stageIntro != null ? stageIntro.PixelDancerRenderer : null;
        var pdc = dancerSr != null ? dancerSr.GetComponent<PixelDancerController>() : null;
        var ringSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SoulGuideRingPath);
        var addMat = AssetDatabase.LoadAssetAtPath<Material>(WormholeAdditiveMat);
        var sparkMat = AssetDatabase.LoadAssetAtPath<Material>(SparkleMatDst);
        if (pdc == null || ringSprite == null || addMat == null || sparkMat == null)
        {
            EditorUtility.DisplayDialog("プレイヤーの魂救出", "05_Gameシーンを開いてから実行してください（プレイヤー、ガイドリング画像ND_SoulGuideRing.png、またはマテリアルが見つかりません）。", "OK");
            return;
        }
        var soulT = pdc.SoulTransform;
        var soulSr = soulT != null ? soulT.GetComponent<SpriteRenderer>() : null;
        if (soulSr == null)
        {
            EditorUtility.DisplayDialog("プレイヤーの魂救出", "プレイヤーのSoul（Sprite Renderer）が見つかりません。", "OK");
            return;
        }
        if (pdc.GetComponent<PixelDancerSoulRescueFX>() != null)
        {
            EditorUtility.DisplayDialog("プレイヤーの魂救出", "既に追加されています（作り直す場合は手動で削除してから実行してください）。", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("プレイヤーの魂救出",
                $"シーンのプレイヤー（{pdc.name}）に以下を追加します（PixelDancerControllerは変更しません）：\n" +
                "・PixelDancerSoulRescueFX コンポーネント\n・Soul > SoulGuide（ガイドのリング）\n・SoulRescueTrail（虹色の光の粒）\n" +
                "追加後はシーンを保存してください。続けますか？",
                "追加する", "キャンセル"))
            return;

        Undo.SetCurrentGroupName("Add Player Soul Rescue FX");
        var fx = Undo.AddComponent<PixelDancerSoulRescueFX>(pdc.gameObject);

        var guideGo = new GameObject("SoulGuide");
        Undo.RegisterCreatedObjectUndo(guideGo, "Add SoulGuide");
        guideGo.transform.SetParent(soulT, false);
        guideGo.layer = soulT.gameObject.layer;
        var guide = guideGo.AddComponent<SpriteRenderer>();
        guide.sprite = ringSprite;
        guide.sharedMaterial = addMat;
        guide.sortingLayerID = soulSr.sortingLayerID;
        guide.sortingOrder = soulSr.sortingOrder - 1;
        guide.color = new Color(1f, 1f, 1f, 0f);
        guide.enabled = false;

        var trailGo = new GameObject("SoulRescueTrail");
        Undo.RegisterCreatedObjectUndo(trailGo, "Add SoulRescueTrail");
        trailGo.transform.SetParent(pdc.transform, false);
        trailGo.layer = pdc.gameObject.layer;
        var ps = trailGo.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = 0f;
        main.startLifetime = 1f;
        main.gravityModifier = 0f;
        main.maxParticles = 1000;
        var emission = ps.emission;
        emission.enabled = false;
        var shape = ps.shape;
        shape.enabled = false;
        var colorOl = ps.colorOverLifetime;
        colorOl.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
        colorOl.color = new ParticleSystem.MinMaxGradient(g);
        var sizeOl = ps.sizeOverLifetime;
        sizeOl.enabled = true;
        sizeOl.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.2f)));
        var psr = ps.GetComponent<ParticleSystemRenderer>();
        psr.sharedMaterial = sparkMat;
        psr.renderMode = ParticleSystemRenderMode.Billboard;
        psr.sortingLayerID = soulSr.sortingLayerID;
        psr.sortingOrder = soulSr.sortingOrder - 1;

        var so = new SerializedObject(fx);
        so.FindProperty("pixelDancer").objectReferenceValue = pdc;
        so.FindProperty("soulGuideRenderer").objectReferenceValue = guide;
        so.FindProperty("soulTrailParticles").objectReferenceValue = ps;
        so.ApplyModifiedProperties();

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(pdc.gameObject.scene);
        Selection.activeGameObject = pdc.gameObject;
        Debug.Log($"[NeonDancerSetupTool] プレイヤー（{pdc.name}）に魂のガイド・虹の尾を追加しました（シーンの保存が必要）");
        EditorUtility.DisplayDialog("プレイヤーの魂救出", "追加しました。シーンを保存してください（Ctrl+S）。", "OK");
    }

    // ======================================================
    // Area10クリア時だけ、プレイヤーのFinishポーズ（Finish_1〜10）にNeonDancerと同じ虹色演出一式を出す（05_Gameシーンのプレイヤーに配置）
    //  数値はNeonDancer.prefab > NeonDancerControllerの現在値をコピー（以後はプレイヤー側で個別に調整できる）
    //  EnemySpawnerのArea10 Finish Rainbow欄にもアサインする（EnemySpawnerはArea10の時だけ使う）
    // ======================================================
    [MenuItem("Tools/NeonDancer/6 プレイヤー/Area10クリア時の虹色Finishを追加")]
    private static void AddPlayerFinishRainbowFX()
    {
        var stageIntro = Object.FindFirstObjectByType<StageIntroController>(FindObjectsInactive.Include);
        var dancerSr = stageIntro != null ? stageIntro.PixelDancerRenderer : null;
        var spawner = Object.FindFirstObjectByType<EnemySpawner>(FindObjectsInactive.Include);
        var ringSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SoulGuideRingPath);
        var addMat = AssetDatabase.LoadAssetAtPath<Material>(WormholeAdditiveMat);
        var sparkMat = AssetDatabase.LoadAssetAtPath<Material>(SparkleMatDst);
        if (dancerSr == null || spawner == null || ringSprite == null || addMat == null || sparkMat == null)
        {
            EditorUtility.DisplayDialog("Area10の虹色Finish", "05_Gameシーンを開いてから実行してください（プレイヤー・EnemySpawner、リング画像ND_SoulGuideRing.png、またはマテリアルが見つかりません）。", "OK");
            return;
        }
        if (dancerSr.GetComponent<PlayerFinishRainbowFX>() != null)
        {
            EditorUtility.DisplayDialog("Area10の虹色Finish", "既に追加されています（作り直す場合は手動で削除してから実行してください）。", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("Area10の虹色Finish",
                $"シーンのプレイヤー（{dancerSr.name}）に以下を追加します：\n" +
                "・PlayerFinishRainbowFX コンポーネント（数値はNeonDancerControllerからコピー）\n" +
                "・FinishRainbowGlow（虹色の発光）\n・FinishRainbowSparks（9色の光の粒）\n" +
                $"・EnemySpawner（{spawner.name}）の Area10 Finish Rainbow 欄にアサイン\n" +
                "追加後はシーンを保存してください。続けますか？",
                "追加する", "キャンセル"))
            return;

        Undo.SetCurrentGroupName("Add Player Finish Rainbow FX");
        var fx = Undo.AddComponent<PlayerFinishRainbowFX>(dancerSr.gameObject);

        var glowGo = new GameObject("FinishRainbowGlow");
        Undo.RegisterCreatedObjectUndo(glowGo, "Add FinishRainbowGlow");
        glowGo.transform.SetParent(dancerSr.transform, false);
        glowGo.layer = dancerSr.gameObject.layer;
        var glow = glowGo.AddComponent<SpriteRenderer>();
        glow.sharedMaterial = addMat;
        glow.sortingLayerID = dancerSr.sortingLayerID;
        glow.sortingOrder = dancerSr.sortingOrder + 1;
        glow.color = new Color(1f, 1f, 1f, 0f);
        glow.enabled = false;

        var sparkGo = new GameObject("FinishRainbowSparks");
        Undo.RegisterCreatedObjectUndo(sparkGo, "Add FinishRainbowSparks");
        sparkGo.transform.SetParent(dancerSr.transform, false);
        sparkGo.layer = dancerSr.gameObject.layer;
        var ps = sparkGo.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 1f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startSpeed = 0f;
        main.startLifetime = 1f;
        main.gravityModifier = 0f;
        main.maxParticles = 1000;
        var emission = ps.emission;
        emission.enabled = false;
        var shape = ps.shape;
        shape.enabled = false;
        var colorOl = ps.colorOverLifetime;
        colorOl.enabled = true;
        var g = new Gradient();
        g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                  new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
        colorOl.color = new ParticleSystem.MinMaxGradient(g);
        var sizeOl = ps.sizeOverLifetime;
        sizeOl.enabled = true;
        sizeOl.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.2f)));
        var psr = ps.GetComponent<ParticleSystemRenderer>();
        psr.sharedMaterial = sparkMat;
        psr.renderMode = ParticleSystemRenderMode.Billboard;
        psr.sortingLayerID = dancerSr.sortingLayerID;
        psr.sortingOrder = dancerSr.sortingOrder + 2;

        var so = new SerializedObject(fx);
        so.FindProperty("bodyGlowRenderer").objectReferenceValue = glow;
        so.FindProperty("sparkParticles").objectReferenceValue = ps;
        so.FindProperty("rainbowRingSprite").objectReferenceValue = ringSprite;

        // 数値はNeonDancerControllerの現在値をコピー（同名フィールド。読むだけ）
        GameObject ndRoot = PrefabUtility.LoadPrefabContents(DstPrefab);
        try
        {
            var ndSo = new SerializedObject(ndRoot.GetComponent<NeonDancerController>());
            string[] names =
            {
                "rainbowGlowDuration", "rainbowCycleSpeed", "rainbowPulseFrequency", "rainbowGlowMaxAlpha", "rainbowGlowScale",
                "rainbowGlowExtraLayerScales", "rainbowGlowExtraLayerAlphas",
                "rainbowRingInterval", "rainbowRingSize", "rainbowRingDuration", "rainbowRingMaxAlpha",
                "rainbowSparkRate", "rainbowSparkRiseSpeed", "rainbowSparkLifetime", "rainbowSparkSize",
                "rainbowFinalRingSize", "rainbowFinalRingDuration", "rainbowFinalSparkCount", "rainbowFinalSparkSpeed",
            };
            foreach (string n in names)
            {
                var src = ndSo.FindProperty(n);
                if (src != null) so.CopyFromSerializedProperty(src);
                else Debug.LogWarning($"[NeonDancerSetupTool] NeonDancerController.{n} が見つかりません（初期値のまま）");
            }
            var srcRing = ndSo.FindProperty("rainbowRingSprite");
            if (srcRing != null && srcRing.objectReferenceValue != null) so.FindProperty("rainbowRingSprite").objectReferenceValue = srcRing.objectReferenceValue;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(ndRoot);
        }
        so.ApplyModifiedProperties();

        var spSo = new SerializedObject(spawner);
        Undo.RecordObject(spawner, "Assign Area10 Finish Rainbow");
        spSo.FindProperty("area10FinishRainbow").objectReferenceValue = fx;
        spSo.ApplyModifiedProperties();

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(dancerSr.gameObject.scene);
        Selection.activeGameObject = dancerSr.gameObject;
        Debug.Log($"[NeonDancerSetupTool] プレイヤー（{dancerSr.name}）にArea10クリア時の虹色Finishを追加しました（シーンの保存が必要）");
        EditorUtility.DisplayDialog("Area10の虹色Finish", "追加しました。シーンを保存してください（Ctrl+S）。", "OK");
    }

    // ======================================================
    // 後半フェーズの攻撃：元のボスの弾の設定（Bullet Type）と攻撃の数値をNeonDancerへコピーする（コピー元は読むだけ）
    //  ①Susanooのスパイラル弾 ②ArcGuardのTrail Sweep ④ArcGuardのClaw1H ⑤ShamanのTornado
    //  ⑦Obeliskの中央ビーム ⑧Susanoo後半のワープ弾 ⑨Tsukuyomi後半の強化ドリル（③⑥はそのまま）
    //  発射台の後半用の抽選（Phase2 Bullet Choices）は前半と同じ割合で作る
    // ======================================================
    private const string SusanooPrefabPath  = "Assets/Prefabs/Enemies/Susanoo.prefab";
    private const string SusanooDataPath    = "Assets/GameData/Enemies/EnemyData_Susanoo.asset";
    private const string ArcGuardPrefabPath = "Assets/Prefabs/Enemies/ArcGuard.prefab";
    private const string ShamanPrefabPath   = "Assets/Prefabs/Enemies/Shaman.prefab";
    private const string ShamanDataPath     = "Assets/GameData/Enemies/EnemyData_Shaman.asset";
    private const string ObeliskPrefabPath  = "Assets/Prefabs/Enemies/Obelisk.prefab";
    private const string ObeliskDataPath    = "Assets/GameData/Enemies/EnemyData_Obelisk.asset";

    [MenuItem("Tools/NeonDancer/4 後半の攻撃/後半の攻撃を元のボスからコピー")]
    private static void CopyPhase2Attacks()
    {
        var data      = AssetDatabase.LoadAssetAtPath<EnemyData>(DstData);
        var susData   = AssetDatabase.LoadAssetAtPath<EnemyData>(SusanooDataPath);
        var arcData   = AssetDatabase.LoadAssetAtPath<EnemyData>(SrcData);
        var shaData   = AssetDatabase.LoadAssetAtPath<EnemyData>(ShamanDataPath);
        var obeData   = AssetDatabase.LoadAssetAtPath<EnemyData>(ObeliskDataPath);
        var susCtrl   = LoadComp<SusanooController>(SusanooPrefabPath);
        var arcCtrl   = LoadComp<ArcGuardController>(ArcGuardPrefabPath);
        var arcTail   = LoadComp<ArcGuardTailAnimator>(ArcGuardPrefabPath);
        var shaCtrl   = LoadComp<ShamanController>(ShamanPrefabPath);
        var obeCtrl   = LoadComp<ObeliskController>(ObeliskPrefabPath);
        var tsuCtrl   = LoadComp<TsukuyomiController>(TsukuyomiPrefabPath);
        if (data == null || susData == null || arcData == null || shaData == null || obeData == null ||
            susCtrl == null || arcCtrl == null || arcTail == null || shaCtrl == null || obeCtrl == null || tsuCtrl == null)
        {
            EditorUtility.DisplayDialog("後半の攻撃", "コピー元・コピー先のアセットが見つかりません。Consoleを確認してください。", "OK");
            Debug.LogError($"[NeonDancerSetupTool] data={data} susData={susData} arcData={arcData} shaData={shaData} obeData={obeData} " +
                           $"susCtrl={susCtrl} arcCtrl={arcCtrl} arcTail={arcTail} shaCtrl={shaCtrl} obeCtrl={obeCtrl} tsuCtrl={tsuCtrl}");
            return;
        }
        var sus = new SerializedObject(susCtrl);
        var arc = new SerializedObject(arcCtrl);
        var tail = new SerializedObject(arcTail);
        var sha = new SerializedObject(shaCtrl);
        var obe = new SerializedObject(obeCtrl);
        var tsu = new SerializedObject(tsuCtrl);

        // --- コピー元の弾（index＋名前で確認） ---
        EnemyData.BulletType Src(EnemyData d, int idx, string expectName)
        {
            if (d.bulletTypes == null || idx < 0 || idx >= d.bulletTypes.Length || d.bulletTypes[idx] == null) return null;
            var bt = d.bulletTypes[idx];
            if (bt.name != expectName) { Debug.LogError($"[NeonDancerSetupTool] {d.name}[{idx}]の名前が「{bt.name}」で、想定の「{expectName}」と違います"); return null; }
            return bt;
        }
        int spiralIdx = sus.FindProperty("spiralBulletTypeIndex").intValue;
        int sweepIdx  = tail.FindProperty("sweepBulletTypeIndex").intValue;
        int clawIdx   = arc.FindProperty("claw1HBulletTypeIndex").intValue;
        int tornIdx   = sha.FindProperty("tornadoBulletTypeIndex").intValue;
        int beamIdx   = obe.FindProperty("centralBeamBulletTypeIndex").intValue;
        var srcSpiral = Src(susData, spiralIdx, "Normal");
        var srcSweep  = Src(arcData, sweepIdx, "Speed Curve");
        var srcClaw   = Src(arcData, clawIdx, "Speed Curve");
        var srcTorn   = Src(shaData, tornIdx, "Missile");
        var srcBeam   = Src(obeData, beamIdx, "Breath");
        var srcWarp   = Src(susData, 12, "Warp");
        if (srcSpiral == null || srcSweep == null || srcClaw == null || srcTorn == null || srcBeam == null || srcWarp == null)
        {
            EditorUtility.DisplayDialog("後半の攻撃", "コピー元の弾の設定が想定と違います。Consoleを確認してください。", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("後半の攻撃",
                "元のボスの設定をNeonDancerへコピーします（コピー元は変更しません）：\n" +
                "・EnemyData_NeonDancerのBullet Typesに後半用の弾6種を追加（同名があれば上書き）\n" +
                "  ①P2 Spiral ← Susanoo「Normal」／②P2 TrailSweep・④P2 Claw1H ← ArcGuard「Speed Curve」\n" +
                "  ⑤P2 TornadoMissile ← Shaman「Missile」／⑦P2 SweepBeam ← Obelisk「Breath」／⑧P2 Warp ← Susanoo「Warp」\n" +
                "・NeonDancerControllerの後半の攻撃の数値（各ボスのPrefabの保存値）\n" +
                "・各発射台のPhase2 Bullet Choices（割合は前半と同じ）\n続けますか？",
                "コピーする", "キャンセル"))
            return;

        // --- EnemyData_NeonDancer：後半用の弾を追加/上書き ---
        Undo.RecordObject(data, "NeonDancer Phase2 Bullet Types");
        var list = new System.Collections.Generic.List<EnemyData.BulletType>(data.bulletTypes ?? new EnemyData.BulletType[0]);
        int Upsert(string name, EnemyData.BulletType src, System.Action<EnemyData.BulletType> tweak = null)
        {
            var copy = new EnemyData.BulletType();
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(src), copy);
            copy.name = name;
            tweak?.Invoke(copy);
            int found = list.FindIndex(b => b != null && b.name == name);
            if (found >= 0) list[found] = copy; else { list.Add(copy); found = list.Count - 1; }
            Debug.Log($"[NeonDancerSetupTool] Bullet Types[{found}] {name} ← {src.name}（speed={copy.speed}, penetration={copy.penetration}, lifeTime={copy.lifeTime}）");
            return found;
        }
        int iSpiral = Upsert("①P2 Spiral", srcSpiral);
        int iSweep  = Upsert("②P2 TrailSweep", srcSweep);
        int iClaw   = Upsert("④P2 Claw1H", srcClaw);
        int iTorn   = Upsert("⑤P2 TornadoMissile", srcTorn);
        var obeFireSE = obe.FindProperty("centralBeamFireSE").objectReferenceValue as AudioClip;
        float obeFireVol = obe.FindProperty("centralBeamFireSEVolume").floatValue;
        int iBeam   = Upsert("⑦P2 SweepBeam", srcBeam, c => { c.fireSEOverride = obeFireSE; c.fireSEOverrideVolume = obeFireVol; }); // Obeliskの発射SE
        int iWarp   = Upsert("⑧P2 Warp", srcWarp);
        data.bulletTypes = list.ToArray();
        EditorUtility.SetDirty(data);

        // --- NeonDancer.prefab：攻撃の数値・発射台の後半用の抽選 ---
        GameObject root = PrefabUtility.LoadPrefabContents(DstPrefab);
        try
        {
            var ctrl = root.GetComponent<NeonDancerController>();
            var so = new SerializedObject(ctrl);

            // ①スパイラル弾
            so.FindProperty("spiralBulletCount").intValue      = sus.FindProperty("spiralBulletCount").intValue;
            so.FindProperty("spiralAngleStepDeg").floatValue   = sus.FindProperty("spiralAngleStepDeg").floatValue;
            so.FindProperty("spiralFireInterval").floatValue   = sus.FindProperty("spiralFireInterval").floatValue;
            so.FindProperty("spiralBurstSE").objectReferenceValue = sus.FindProperty("spiralBulletSpawnSe").objectReferenceValue;
            so.FindProperty("spiralBurstSEVolume").floatValue  = sus.FindProperty("spiralBulletSpawnSeVolume").floatValue;

            // ②Trail Sweep：尾の各コマの「尾のOffset＋Muzzle Offset」と表示秒数
            var sweepFrames = tail.FindProperty("sweepFrames");
            float domino = tail.FindProperty("dominoFrameDuration").floatValue;
            var sweepOffsets = so.FindProperty("trailSweepShotOffsets");
            var sweepDurs = so.FindProperty("trailSweepShotDurations");
            sweepOffsets.arraySize = sweepFrames.arraySize;
            sweepDurs.arraySize = sweepFrames.arraySize;
            for (int i = 0; i < sweepFrames.arraySize; i++)
            {
                var f = sweepFrames.GetArrayElementAtIndex(i);
                sweepOffsets.GetArrayElementAtIndex(i).vector2Value = f.FindPropertyRelative("offset").vector2Value + f.FindPropertyRelative("muzzleOffset").vector2Value;
                float d = f.FindPropertyRelative("duration").floatValue;
                sweepDurs.GetArrayElementAtIndex(i).floatValue = d > 0f ? d : domino;
            }

            // ④Claw1H：予備動作（発射開始コマより前のDuration合計）と、爪痕の各コマのMuzzle Offset・表示秒数
            var clawPose = arc.FindProperty("claw1HLeftFrames");
            int trigger = arc.FindProperty("claw1HFireTriggerFrame").intValue;
            float poseDef = arc.FindProperty("clawPoseFrameDuration").floatValue;
            float windup = 0f;
            for (int i = 0; i < Mathf.Min(trigger, clawPose.arraySize); i++)
            {
                float d = clawPose.GetArrayElementAtIndex(i).FindPropertyRelative("duration").floatValue;
                windup += d > 0f ? d : poseDef;
            }
            so.FindProperty("claw1HWindupSeconds").floatValue = windup;
            var marks = arc.FindProperty("clawMarkFrames");
            float markDef = arc.FindProperty("clawMarkFrameDuration").floatValue;
            var clawOffsets = so.FindProperty("claw1HShotOffsets");
            var clawDurs = so.FindProperty("claw1HShotDurations");
            clawOffsets.arraySize = marks.arraySize;
            clawDurs.arraySize = marks.arraySize;
            for (int i = 0; i < marks.arraySize; i++)
            {
                var f = marks.GetArrayElementAtIndex(i);
                clawOffsets.GetArrayElementAtIndex(i).vector2Value = f.FindPropertyRelative("muzzleOffset").vector2Value;
                float d = f.FindPropertyRelative("duration").floatValue;
                clawDurs.GetArrayElementAtIndex(i).floatValue = d > 0f ? d : markDef;
            }

            // ⑤Tornado
            var tornGo = sha.FindProperty("tornadoPrefab").objectReferenceValue as GameObject;
            so.FindProperty("tornadoPrefab").objectReferenceValue = tornGo != null ? tornGo.GetComponent<TornadoCloud>() : null;
            so.FindProperty("tornadoDuration").floatValue          = sha.FindProperty("tornadoDuration").floatValue;
            so.FindProperty("tornadoMoveSpeed").floatValue         = sha.FindProperty("tornadoMoveSpeed").floatValue;
            so.FindProperty("tornadoFadeIn").floatValue            = sha.FindProperty("tornadoFadeIn").floatValue;
            so.FindProperty("tornadoFadeOut").floatValue           = sha.FindProperty("tornadoFadeOut").floatValue;
            so.FindProperty("tornadoEmissionRate").floatValue      = sha.FindProperty("tornadoEmissionRate").floatValue;
            so.FindProperty("tornadoParticleSizeMin").floatValue   = sha.FindProperty("tornadoParticleSizeMin").floatValue;
            so.FindProperty("tornadoParticleSizeMax").floatValue   = sha.FindProperty("tornadoParticleSizeMax").floatValue;
            so.FindProperty("tornadoParticleLifetime").floatValue  = sha.FindProperty("tornadoParticleLifetime").floatValue;
            so.FindProperty("tornadoBulletFireInterval").floatValue = sha.FindProperty("tornadoBulletFireInterval").floatValue;
            so.FindProperty("tornadoColorAlpha").floatValue        = sha.FindProperty("tornadoSmokeColor").colorValue.a;

            // ⑦薙ぎ払いビーム
            so.FindProperty("sweepBeamAngleRangeDeg").floatValue = obe.FindProperty("centralBeamSweepAngleRangeDeg").floatValue;
            so.FindProperty("sweepBeamDuration").floatValue      = obe.FindProperty("centralBeamSweepDuration").floatValue;

            // ⑧ワープ弾の複数発射（Susanoo後半の値）
            so.FindProperty("warpShotCountMin").intValue               = sus.FindProperty("warpShotCountBackMin").intValue;
            so.FindProperty("warpShotCountMax").intValue               = sus.FindProperty("warpShotCountBackMax").intValue;
            so.FindProperty("warpShotSpreadAngle").floatValue          = sus.FindProperty("warpShotSpreadAngle").floatValue;
            so.FindProperty("warpTimingSimultaneousWeight").floatValue = sus.FindProperty("warpTimingSimultaneousWeight").floatValue;
            so.FindProperty("warpTimingStaggeredWeight").floatValue    = sus.FindProperty("warpTimingStaggeredWeight").floatValue;
            so.FindProperty("warpStaggerDelayMin").floatValue          = sus.FindProperty("warpStaggerDelayMin").floatValue;
            so.FindProperty("warpStaggerDelayMax").floatValue          = sus.FindProperty("warpStaggerDelayMax").floatValue;
            so.FindProperty("warpSpawnSE").objectReferenceValue        = sus.FindProperty("warpBulletSpawnSe").objectReferenceValue;
            so.FindProperty("warpSpawnSEVolume").floatValue            = sus.FindProperty("warpBulletSpawnSeVolume").floatValue;

            // ⑨強化ドリル弾
            so.FindProperty("enhancedDrillChance").floatValue        = tsu.FindProperty("enhancedBulletChance").floatValue;
            so.FindProperty("enhancedRequiredHitsBonus").intValue    = tsu.FindProperty("enhancedRequiredHitsBonus").intValue;
            so.FindProperty("enhancedPenetrationOverride").intValue  = tsu.FindProperty("enhancedPenetrationOverride").intValue;
            so.FindProperty("enhancedScaleMultiplier").floatValue    = tsu.FindProperty("enhancedScaleMultiplier").floatValue;
            so.FindProperty("enhancedTintColor").colorValue          = tsu.FindProperty("enhancedTintColor").colorValue;
            so.FindProperty("enhancedTrailColor").colorValue         = tsu.FindProperty("enhancedTrailColor").colorValue;
            so.FindProperty("enhancedTrailTime").floatValue          = tsu.FindProperty("enhancedTrailTime").floatValue;
            so.FindProperty("enhancedTrailWidth").floatValue         = tsu.FindProperty("enhancedTrailWidth").floatValue;
            so.ApplyModifiedPropertiesWithoutUndo();

            // 発射台：前半の抽選（①〜⑨の番号＋割合）から、後半用の抽選（後半の弾の番号＋攻撃の種類、割合は同じ）を作る
            var map = new System.Collections.Generic.Dictionary<int, (int idx, NeonDancerTurret.Phase2Attack atk)>
            {
                { 0, (iSpiral, NeonDancerTurret.Phase2Attack.SpiralBurst) },
                { 1, (iSweep,  NeonDancerTurret.Phase2Attack.TrailSweep) },
                { 2, (2,       NeonDancerTurret.Phase2Attack.Normal) },
                { 3, (iClaw,   NeonDancerTurret.Phase2Attack.Claw1H) },
                { 4, (iTorn,   NeonDancerTurret.Phase2Attack.Tornado) },
                { 5, (5,       NeonDancerTurret.Phase2Attack.Normal) },
                { 6, (iBeam,   NeonDancerTurret.Phase2Attack.SweepBeam) },
                { 7, (iWarp,   NeonDancerTurret.Phase2Attack.WarpMulti) },
                { 8, (8,       NeonDancerTurret.Phase2Attack.EnhancedDrill) },
            };
            var turretsProp = so.FindProperty("turrets");
            for (int ti = 0; ti < turretsProp.arraySize; ti++)
            {
                var turret = turretsProp.GetArrayElementAtIndex(ti).objectReferenceValue as NeonDancerTurret;
                if (turret == null) continue;
                var tso = new SerializedObject(turret);
                var p1 = tso.FindProperty("bulletChoices");
                var p2 = tso.FindProperty("phase2BulletChoices");
                p2.arraySize = p1.arraySize;
                for (int i = 0; i < p1.arraySize; i++)
                {
                    var c1 = p1.GetArrayElementAtIndex(i);
                    var c2 = p2.GetArrayElementAtIndex(i);
                    int idx1 = c1.FindPropertyRelative("bulletTypeIndex").intValue;
                    var m = map.TryGetValue(idx1, out var v) ? v : (idx1, NeonDancerTurret.Phase2Attack.Normal);
                    c2.FindPropertyRelative("bulletTypeIndex").intValue = m.Item1;
                    c2.FindPropertyRelative("probabilityPercent").floatValue = c1.FindPropertyRelative("probabilityPercent").floatValue;
                    c2.FindPropertyRelative("attack").enumValueIndex = (int)m.Item2;
                    Debug.Log($"[NeonDancerSetupTool] {turret.name} 後半：{data.bulletTypes[m.Item1].name}（{m.Item2}）{c1.FindPropertyRelative("probabilityPercent").floatValue}%");
                }
                tso.ApplyModifiedPropertiesWithoutUndo();
            }

            PrefabUtility.SaveAsPrefabAsset(root, DstPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[NeonDancerSetupTool] 後半の攻撃を元のボスからコピーしました");
        EditorUtility.DisplayDialog("後半の攻撃", "コピーしました。", "OK");
    }

    // ======================================================
    // 全ビーム（Obelisk / Dragon / Bit / NeonDancer）の未反射区間がプレイヤーのダンサーにも当たるようにする
    //  （Beam Ignore Player＝OFF。ONだとダンサーを素通りしてFloorにだけ当たる）
    // ======================================================
    private const string DragonDataPath = "Assets/GameData/Enemies/EnemyData_Dragon.asset";

    [MenuItem("Tools/NeonDancer/2 前半の攻撃・ステージ/全ビームをダンサーにも当てる（Obelisk・Dragon・Bit含む）")]
    private static void BeamsHitPlayer()
    {
        if (!EditorUtility.DisplayDialog("全ビームをダンサーにも当てる",
                "以下のビームの「Beam Ignore Player」をOFFにします（未反射のビームがダンサーにも当たり、そこで止まる）：\n" +
                "・EnemyData_Obelisk / EnemyData_Dragon / EnemyData_NeonDancer のBeam Ignore PlayerがONの弾すべて\n" +
                "・Bit.prefab > BitController > Beam Bullet Type（ObeliskのBitにも反映）\n" +
                "※Bit.prefabをPrefabモードで開いて未保存の変更がある場合は、先に保存してください。\n続けますか？",
                "OFFにする", "キャンセル"))
            return;

        int count = 0;
        foreach (string path in new[] { ObeliskDataPath, DragonDataPath, DstData })
        {
            var d = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
            if (d == null || d.bulletTypes == null) { Debug.LogWarning($"[NeonDancerSetupTool] {path} が見つかりません"); continue; }
            Undo.RecordObject(d, "Beams Hit Player");
            foreach (var bt in d.bulletTypes)
            {
                if (bt == null || !bt.beamIgnorePlayer) continue;
                bt.beamIgnorePlayer = false;
                count++;
                Debug.Log($"[NeonDancerSetupTool] {d.name}「{bt.name}」Beam Ignore Player → OFF");
            }
            EditorUtility.SetDirty(d);
        }

        GameObject bit = PrefabUtility.LoadPrefabContents(BitPrefabPath);
        try
        {
            var bc = bit.GetComponent<BitController>();
            if (bc != null)
            {
                var so = new SerializedObject(bc);
                var p = so.FindProperty("beamBulletType.beamIgnorePlayer");
                if (p != null && p.boolValue)
                {
                    p.boolValue = false;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.SaveAsPrefabAsset(bit, BitPrefabPath);
                    count++;
                    Debug.Log("[NeonDancerSetupTool] Bit.prefab BitController.beamBulletType Beam Ignore Player → OFF");
                }
            }
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(bit);
        }

        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("全ビームをダンサーにも当てる", $"{count}個のビームのBeam Ignore PlayerをOFFにしました。", "OK");
    }

    // ======================================================
    // ⑨Drill：Tsukuyomiと同じく直進とカーブを混ぜる（TsukuyomiのCurveドリル弾をコピーし、NeonDancerControllerに設定）
    // ======================================================
    [MenuItem("Tools/NeonDancer/2 前半の攻撃・ステージ/⑨Drillにカーブを混ぜる")]
    private static void AddDrillCurve()
    {
        var data = AssetDatabase.LoadAssetAtPath<EnemyData>(DstData);
        var tsuData = AssetDatabase.LoadAssetAtPath<EnemyData>(TsukuyomiDataPath);
        var tsuCtrl = LoadComp<TsukuyomiController>(TsukuyomiPrefabPath);
        if (data == null || tsuData == null || tsuCtrl == null)
        {
            EditorUtility.DisplayDialog("⑨Drillのカーブ", "EnemyData_NeonDancer / EnemyData_Tsukuyomi / Tsukuyomi.prefabが見つかりません。", "OK");
            return;
        }
        int curveIdx = new SerializedObject(tsuCtrl).FindProperty("curveBulletTypeIndex").intValue;
        var src = (tsuData.bulletTypes != null && curveIdx >= 0 && curveIdx < tsuData.bulletTypes.Length) ? tsuData.bulletTypes[curveIdx] : null;
        if (src == null || src.name != "Curve" || !src.usePinnedReflect || !src.useMissileArc)
        {
            EditorUtility.DisplayDialog("⑨Drillのカーブ", $"EnemyData_Tsukuyomi[{curveIdx}]が想定（Curve・ドリル・ミサイル軌道）と違います。", "OK");
            return;
        }
        // 前半の⑨Drill（直進）を探す（狙い方を同じにするため）
        EnemyData.BulletType straight = null;
        foreach (var bt in data.bulletTypes) if (bt != null && bt.usePinnedReflect && !bt.useMissileArc && bt.name == "⑨Drill") { straight = bt; break; }
        if (straight == null)
        {
            EditorUtility.DisplayDialog("⑨Drillのカーブ", "EnemyData_NeonDancerに「⑨Drill」が見つかりません。", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("⑨Drillのカーブ",
                $"・TsukuyomiのCurveドリル弾（EnemyData_Tsukuyomi[{curveIdx}]）を、EnemyData_NeonDancerに「⑨Drill Curve」としてコピー（同名があれば上書き。狙い方は⑨Drillと同じ）\n" +
                "・NeonDancerControllerのDrill Curve Bullet Type Indexに設定、Drill Curve Chance Percent＝50（TsukuyomiのStraight 50%／Curve 50%と同じ）\n" +
                "Tsukuyomiは変更しません。続けますか？",
                "設定する", "キャンセル"))
            return;

        Undo.RecordObject(data, "NeonDancer Drill Curve");
        var list = new System.Collections.Generic.List<EnemyData.BulletType>(data.bulletTypes);
        var copy = new EnemyData.BulletType();
        EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(src), copy);
        copy.name = "⑨Drill Curve";
        copy.aimMode = straight.aimMode;
        int idx = list.FindIndex(b => b != null && b.name == copy.name);
        if (idx >= 0) list[idx] = copy; else { list.Add(copy); idx = list.Count - 1; }
        data.bulletTypes = list.ToArray();
        EditorUtility.SetDirty(data);

        GameObject root = PrefabUtility.LoadPrefabContents(DstPrefab);
        try
        {
            var so = new SerializedObject(root.GetComponent<NeonDancerController>());
            so.FindProperty("drillCurveBulletTypeIndex").intValue = idx;
            so.FindProperty("drillCurveChancePercent").floatValue = 50f;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, DstPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[NeonDancerSetupTool] Bullet Types[{idx}] ⑨Drill Curve ← Tsukuyomi「Curve」（曲がり角{copy.missileCurveAngle}°）を設定しました");
        EditorUtility.DisplayDialog("⑨Drillのカーブ", "設定しました。", "OK");
    }

    // ======================================================
    // 敵の線が壊れた時のSE：プレイヤーの線が壊れた時と同じSE（05_GameシーンのPaddleDrawer > Line Break Clip）をコピー
    // ======================================================
    [MenuItem("Tools/NeonDancer/4 後半の攻撃/敵の線の破壊SEをプレイヤーの線と同じに")]
    private static void CopyEnemyLineBreakSE()
    {
        var drawer = Object.FindFirstObjectByType<PaddleDrawer>(FindObjectsInactive.Include);
        if (drawer == null)
        {
            EditorUtility.DisplayDialog("敵の線の破壊SE", "PaddleDrawerが見つかりません。05_Gameシーンを開いてから実行してください。", "OK");
            return;
        }
        var dso = new SerializedObject(drawer);
        var clip = dso.FindProperty("lineBreakClip").objectReferenceValue as AudioClip;
        float vol = dso.FindProperty("lineBreakVolume").floatValue;
        if (clip == null)
        {
            EditorUtility.DisplayDialog("敵の線の破壊SE", "PaddleDrawerのLine Break Clipが未設定です。", "OK");
            return;
        }
        GameObject root = PrefabUtility.LoadPrefabContents(DstPrefab);
        try
        {
            var so = new SerializedObject(root.GetComponent<NeonDancerController>());
            so.FindProperty("enemyLineBreakSE").objectReferenceValue = clip;
            so.FindProperty("enemyLineBreakSEVolume").floatValue = vol;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, DstPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[NeonDancerSetupTool] 敵の線の破壊SE ← PaddleDrawer.lineBreakClip（{clip.name}、音量{vol}）");
        EditorUtility.DisplayDialog("敵の線の破壊SE", $"設定しました（{clip.name}）。", "OK");
    }

    // ======================================================
    // ドリル弾：反射して敵に当てた時だけ規定ヒット数を2倍（強化弾は強化後の回数×2）
    //  対象：EnemyData_Tsukuyomi / EnemyData_NeonDancer の、ドリル反射（Use Pinned Reflect）がONの弾すべて
    // ======================================================
    [MenuItem("Tools/NeonDancer/4 後半の攻撃/ドリルの敵へのヒット数を2倍に（Tsukuyomi・NeonDancer）")]
    private static void SetDrillEnemyHitMultiplier()
    {
        string[] paths = { "Assets/GameData/Enemies/EnemyData_Tsukuyomi.asset", DstData };
        if (!EditorUtility.DisplayDialog("ドリルの敵へのヒット数",
            "EnemyData_Tsukuyomi / EnemyData_NeonDancer のドリル弾（Use Pinned Reflect=ON）の\n" +
            "Pinned Reflect Enemy Hit Multiplier を 2 にします。よろしいですか？", "実行", "キャンセル")) return;

        var log = new System.Text.StringBuilder();
        foreach (string path in paths)
        {
            var data = AssetDatabase.LoadAssetAtPath<EnemyData>(path);
            if (data == null) { log.AppendLine($"見つかりません: {path}"); continue; }
            var so = new SerializedObject(data);
            var arr = so.FindProperty("bulletTypes");
            for (int i = 0; i < arr.arraySize; i++)
            {
                var e = arr.GetArrayElementAtIndex(i);
                if (!e.FindPropertyRelative("usePinnedReflect").boolValue) continue;
                e.FindPropertyRelative("pinnedReflectEnemyHitMultiplier").intValue = 2;
                log.AppendLine($"{data.name} [{i}] {e.FindPropertyRelative("name").stringValue}");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[NeonDancerSetupTool] ドリルの敵へのヒット数×2:\n" + log);
        EditorUtility.DisplayDialog("ドリルの敵へのヒット数", "設定しました。\n" + log, "OK");
    }

    // ======================================================
    // Area10のStage2→3でNeonDancerのVSが出ないようにする：Area10ConfigのVs Boss Spriteを空に戻す
    //  （EnemySpawnerは「Stage3かつVs Boss Spriteあり」でVSを出す。Final StageのVSはArea10FinalIntroControllerのVs Boss Posesから出す）
    // ======================================================
    [MenuItem("Tools/NeonDancer/5 Final Stage開始演出/Area10のStage3でVSを出さない")]
    private static void ClearArea10StageVs()
    {
        var cfg = AssetDatabase.LoadAssetAtPath<AreaConfig>(Area10VsImageSettings.Area10ConfigPath);
        if (cfg == null)
        {
            EditorUtility.DisplayDialog("Area10のVS", $"{Area10VsImageSettings.Area10ConfigPath} が見つかりません。", "OK");
            return;
        }
        var so = new SerializedObject(cfg);
        so.FindProperty("vsBossSprite").objectReferenceValue = null;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(cfg);
        AssetDatabase.SaveAssets();
        Debug.Log("[NeonDancerSetupTool] Area10ConfigのVs Boss Spriteを空にしました（Vs Boss Name Sprite・テーマ色はFinal StageのVSで使うため残す）");
        EditorUtility.DisplayDialog("Area10のVS", "Area10ConfigのVs Boss Spriteを空にしました。", "OK");
    }

    private static T LoadComp<T>(string prefabPath) where T : Component
    {
        var go = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        return go != null ? go.GetComponentInChildren<T>(true) : null;
    }

    // sprite / offsetX / offsetY / duration を持つ配列同士をコピーする
    private static void CopyPoseFrames(SerializedProperty src, SerializedProperty dst, int count)
    {
        dst.arraySize = count;
        for (int i = 0; i < count; i++)
        {
            var s = src.GetArrayElementAtIndex(i);
            var d = dst.GetArrayElementAtIndex(i);
            d.FindPropertyRelative("sprite").objectReferenceValue = s.FindPropertyRelative("sprite").objectReferenceValue;
            d.FindPropertyRelative("offsetX").floatValue  = s.FindPropertyRelative("offsetX").floatValue;
            d.FindPropertyRelative("offsetY").floatValue  = s.FindPropertyRelative("offsetY").floatValue;
            d.FindPropertyRelative("duration").floatValue = s.FindPropertyRelative("duration").floatValue;
        }
    }

    [MenuItem("Tools/NeonDancer/2 前半の攻撃・ステージ/Light破壊中は暗くする")]
    private static void DarkenLightsOnBreak()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(DstPrefab);
        int count = 0;
        try
        {
            var ctrl = root.GetComponent<NeonDancerController>();
            var lightsProp = new SerializedObject(ctrl).FindProperty("lights");
            for (int i = 0; i < lightsProp.arraySize; i++)
            {
                var light = lightsProp.GetArrayElementAtIndex(i).objectReferenceValue as NeonDancerLight;
                if (light == null) continue;
                var barrier = light.GetComponent<NeonDancerBarrier>();
                var sr = light.GetComponent<SpriteRenderer>();
                if (barrier == null || sr == null) continue;
                var bso = new SerializedObject(barrier);
                bso.FindProperty("darkenOnBreak").boolValue = true;
                bso.FindProperty("targetRenderer").objectReferenceValue = sr;
                bso.ApplyModifiedPropertiesWithoutUndo();
                count++;
            }
            PrefabUtility.SaveAsPrefabAsset(root, DstPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[NeonDancerSetupTool] Light {count}機のDarken On BreakをONにしました（暗さはFloorと同じBroken Color Multiplier）");
        EditorUtility.DisplayDialog("Light破壊中の暗色化", $"Light {count}機のDarken On BreakをONにしました。", "OK");
    }

    [MenuItem("Tools/NeonDancer/5 Final Stage開始演出/次元移動のブロックノイズをなくす")]
    private static void DisableDimensionBlockNoise()
    {
        var intro = Object.FindFirstObjectByType<Area10FinalIntroController>(FindObjectsInactive.Include);
        if (intro == null)
        {
            EditorUtility.DisplayDialog("次元移動", "シーンにArea10FinalIntroControllerがありません。05_Gameシーンを開いてから実行してください。", "OK");
            return;
        }
        var so = new SerializedObject(intro);
        so.FindProperty("blockNoiseCurve").animationCurveValue = AnimationCurve.Constant(0f, 1f, 0f);
        so.ApplyModifiedProperties();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(intro.gameObject.scene);
        Debug.Log("[NeonDancerSetupTool] Area10FinalIntroController.Block Noise Curveを0にしました（シーンの保存が必要）");
        EditorUtility.DisplayDialog("次元移動", "Block Noise Curveを0にしました。シーンを保存してください（Ctrl+S）。", "OK");
    }

    [MenuItem("Tools/NeonDancer/5 Final Stage開始演出/次元移動のブロックノイズを小さく・9色に")]
    private static void SmallAreaColorBlockNoise()
    {
        var intro = Object.FindFirstObjectByType<Area10FinalIntroController>(FindObjectsInactive.Include);
        if (intro == null)
        {
            EditorUtility.DisplayDialog("次元移動", "シーンにArea10FinalIntroControllerがありません。05_Gameシーンを開いてから実行してください。", "OK");
            return;
        }
        var so = new SerializedObject(intro);
        so.FindProperty("blockNoiseCurve").animationCurveValue =
            new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(0.3f, 0.2f), new Keyframe(0.8f, 0.7f), new Keyframe(1f, 1f));
        so.FindProperty("blockNoiseRows").intValue = 30;
        so.FindProperty("blockNoiseMaxCoverage").floatValue = 0.08f;
        so.FindProperty("blockNoiseOpacity").floatValue = 0.85f;
        so.ApplyModifiedProperties();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(intro.gameObject.scene);
        Debug.Log("[NeonDancerSetupTool] 次元移動のブロックノイズを小さく・9色にしました（シーンの保存が必要）");
        EditorUtility.DisplayDialog("次元移動", "ブロックノイズを設定しました（縦30マス・最大8%・濃さ0.85・Area1〜9の色）。\nシーンを保存してください（Ctrl+S）。", "OK");
    }

    // ======================================================
    // Area10 VS画像（ボス画像・ネームプレート）の生成設定を作成して選択する
    // ======================================================
    private const string VsImageSettingsPath = ArtFolder + "/ND_VsImageSettings.asset";

    [MenuItem("Tools/NeonDancer/1 セットアップ/VS画像の生成設定を開く")]
    private static void OpenVsImageSettings()
    {
        var settings = AssetDatabase.LoadAssetAtPath<Area10VsImageSettings>(VsImageSettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<Area10VsImageSettings>();
            settings.sourceNamePlate = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/AreaSelect/VS/NeonDancerネームプレート.png");
            AssetDatabase.CreateAsset(settings, VsImageSettingsPath);
            AssetDatabase.SaveAssets();
        }
        Selection.activeObject = settings;
        EditorGUIUtility.PingObject(settings);
    }

    private static SpriteRenderer GetOrCreateChildSr(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        if (t == null)
        {
            t = new GameObject(name).transform;
            t.SetParent(parent, false);
        }
        var sr = t.GetComponent<SpriteRenderer>();
        if (sr == null) sr = t.gameObject.AddComponent<SpriteRenderer>();
        return sr;
    }

    private static void SetTurretChoices(Transform turret, int[] indices, float[] percents)
    {
        var so = new SerializedObject(turret.GetComponent<NeonDancerTurret>());
        SetBulletChoices(so, indices, percents);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetBulletChoices(SerializedObject turretSo, int[] indices, float[] percents)
    {
        var arr = turretSo.FindProperty("bulletChoices");
        arr.arraySize = indices.Length;
        for (int i = 0; i < indices.Length; i++)
        {
            var e = arr.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("bulletTypeIndex").intValue = indices[i];
            e.FindPropertyRelative("probabilityPercent").floatValue = percents != null ? percents[i] : 100f / indices.Length;
        }
    }

    private static void CopyObjectArray(SerializedProperty src, SerializedProperty dst)
    {
        dst.arraySize = src.arraySize;
        for (int i = 0; i < src.arraySize; i++)
            dst.GetArrayElementAtIndex(i).objectReferenceValue = src.GetArrayElementAtIndex(i).objectReferenceValue;
    }

    private static void CopyClipArray(SerializedProperty src, SerializedProperty dst)
    {
        dst.arraySize = src.arraySize;
        for (int i = 0; i < src.arraySize; i++)
            dst.GetArrayElementAtIndex(i).objectReferenceValue = src.GetArrayElementAtIndex(i).objectReferenceValue;
    }

    // ======================================================
    // Helpers
    // ======================================================
    private static void SetRef(Object target, string prop, Object value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(prop).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetBool(Object target, string prop, bool value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(prop).boolValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetArray(SerializedProperty arr, params Object[] values)
    {
        arr.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    // 同名のシリアライズ項目を丸ごとコピーする（EnemyHealthDisplay → NeonDancerHealthDisplay）
    private static void CopyMatchingProperties(Object src, Object dst)
    {
        var srcSo = new SerializedObject(src);
        var dstSo = new SerializedObject(dst);
        var it = srcSo.GetIterator();
        if (it.NextVisible(true))
        {
            do
            {
                if (it.propertyPath == "m_Script") continue;
                if (dstSo.FindProperty(it.propertyPath) != null) dstSo.CopyFromSerializedProperty(it);
            } while (it.NextVisible(false));
        }
        dstSo.ApplyModifiedPropertiesWithoutUndo();
    }

    // ======================================================
    // 後半：ND_Turret_P2の③Missileを、Condor（Area2ボス）の雷のジグザグ弾に変更（前半の③Missileは変更しない）
    //  ・EnemyData_CondorのBullet Types「Thunder」をそのまま「③P2 Thunder」としてコピー（同名があれば上書き）
    //  ・CondorはBullet TypeのFire SE Overrideが空の時EnemyData_CondorのFire SEを鳴らすため、空ならそれを入れて同じ音にする
    //  ・NeonDancerControllerの③雷の数値（数・広がり・ジグザグの角度/距離）はCondor.prefabの保存値をコピー
    // ======================================================
    private const string CondorDataPath   = "Assets/GameData/Enemies/EnemyData_Condor.asset";
    private const string CondorPrefabPath = "Assets/Prefabs/Enemies/Condor.prefab";

    [MenuItem("Tools/NeonDancer/4 後半の攻撃/P2の③Missileを雷のジグザグ弾に（Condorからコピー）")]
    private static void P2MissileToThunder()
    {
        var data   = AssetDatabase.LoadAssetAtPath<EnemyData>(DstData);
        var cData  = AssetDatabase.LoadAssetAtPath<EnemyData>(CondorDataPath);
        var cAtk   = LoadComp<CondorSpecialAttack>(CondorPrefabPath);
        if (data == null || cData == null || cAtk == null)
        {
            EditorUtility.DisplayDialog("雷のジグザグ弾", "EnemyData_NeonDancer / EnemyData_Condor / Condor.prefabのCondorSpecialAttack のいずれかが見つかりません。", "OK");
            return;
        }
        var cso = new SerializedObject(cAtk);
        int srcIdx = cso.FindProperty("thunderBulletTypeIndex").intValue;
        if (cData.bulletTypes == null || srcIdx < 0 || srcIdx >= cData.bulletTypes.Length || cData.bulletTypes[srcIdx] == null)
        {
            EditorUtility.DisplayDialog("雷のジグザグ弾", $"EnemyData_CondorのBullet Types {srcIdx}番がありません。", "OK");
            return;
        }
        var src = cData.bulletTypes[srcIdx];
        int perWing = cso.FindProperty("thunderCountPerWing").intValue;
        float spread = cso.FindProperty("thunderSpreadDeg").floatValue;
        float zAngle = cso.FindProperty("thunderZigzagAngleDeg").floatValue;
        float zSeg   = cso.FindProperty("thunderZigzagSegment").floatValue;

        if (!EditorUtility.DisplayDialog("雷のジグザグ弾",
                $"・EnemyData_NeonDancerのBullet Typesに「③P2 Thunder」を追加（← EnemyData_Condor {srcIdx}番「{src.name}」のコピー）\n" +
                $"・ND_Turret_P2の後半（Phase2 Bullet Choices）の③Missileを「③P2 Thunder（Thunder）」に変更（割合はそのまま）\n" +
                $"・NeonDancerControllerの③雷：扇あたり{perWing}発 × 2扇、広がり{spread}°、ジグザグ{zAngle}°／{zSeg}\n" +
                "前半の③Missileは変更しません。続けますか？", "変更する", "キャンセル"))
            return;

        // --- EnemyData_NeonDancer：③P2 Thunderを追加/上書き ---
        Undo.RecordObject(data, "NeonDancer P2 Thunder");
        var list = new System.Collections.Generic.List<EnemyData.BulletType>(data.bulletTypes ?? new EnemyData.BulletType[0]);
        var copy = new EnemyData.BulletType();
        EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(src), copy);
        copy.name = "③P2 Thunder";
        if (copy.fireSEOverride == null) { copy.fireSEOverride = cData.fireSE; copy.fireSEOverrideVolume = cData.fireSEVolume; }
        if (copy.spriteOverride == null && cData.bulletSpriteOverride != null) copy.spriteOverride = cData.bulletSpriteOverride;
        int idx = list.FindIndex(b => b != null && b.name == copy.name);
        if (idx >= 0) list[idx] = copy; else { list.Add(copy); idx = list.Count - 1; }
        data.bulletTypes = list.ToArray();
        EditorUtility.SetDirty(data);

        // --- NeonDancer.prefab：③雷の数値と、ND_Turret_P2の後半の抽選 ---
        var log = new System.Text.StringBuilder();
        GameObject root = PrefabUtility.LoadPrefabContents(DstPrefab);
        try
        {
            var so = new SerializedObject(root.GetComponent<NeonDancerController>());
            so.FindProperty("thunderCountPerFan").intValue = perWing;
            so.FindProperty("thunderFanCount").intValue = 2;
            so.FindProperty("thunderSpreadDeg").floatValue = spread;
            so.FindProperty("thunderZigzagAngleDeg").floatValue = zAngle;
            so.FindProperty("thunderZigzagSegment").floatValue = zSeg;
            so.ApplyModifiedPropertiesWithoutUndo();

            NeonDancerTurret p2 = null;
            foreach (var t in root.GetComponentsInChildren<NeonDancerTurret>(true)) if (t.name == "ND_Turret_P2") { p2 = t; break; }
            if (p2 == null) log.AppendLine("ND_Turret_P2 が見つかりません");
            else
            {
                var tso = new SerializedObject(p2);
                var choices = tso.FindProperty("phase2BulletChoices");
                int changed = 0;
                for (int i = 0; i < choices.arraySize; i++)
                {
                    var c = choices.GetArrayElementAtIndex(i);
                    var bi = c.FindPropertyRelative("bulletTypeIndex");
                    if (bi.intValue != 2 && bi.intValue != idx) continue; // ③Missile（前半と共通の2番）か、再実行時の③P2 Thunder
                    bi.intValue = idx;
                    c.FindPropertyRelative("attack").enumValueIndex = (int)NeonDancerTurret.Phase2Attack.Thunder;
                    changed++;
                    log.AppendLine($"ND_Turret_P2 後半[{i}]：③P2 Thunder（{idx}番・Thunder）{c.FindPropertyRelative("probabilityPercent").floatValue}%");
                }
                if (changed == 0) log.AppendLine("ND_Turret_P2 の後半に③Missile（2番）が見つかりません");
                tso.ApplyModifiedPropertiesWithoutUndo();
            }
            PrefabUtility.SaveAsPrefabAsset(root, DstPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[NeonDancerSetupTool] Bullet Types[{idx}] ③P2 Thunder ← EnemyData_Condor[{srcIdx}] {src.name}（speed={copy.speed}, lifeTime={copy.lifeTime}）\n" + log);
        EditorUtility.DisplayDialog("雷のジグザグ弾", $"変更しました。\nBullet Types {idx}番「③P2 Thunder」\n" + log, "OK");
    }

    // ======================================================
    // 前半：IronNestのNMの撃ち方を追加（ND_Turret_P3にSweepRapid、ND_Turret_P2にTelegraph3Way）
    //  ・弾はIronNest.prefabのNM01/NM02に付いたIronNestNMPhase2AttackのBullet Type（各NMのEnemyData）をそのままコピー
    //    （Fire SE Overrideが空ならそのNMのEnemyDataのFire SEを入れて、IronNestと同じ音にする）
    //  ・撃ち方の数値（角度・秒数・間隔）もIronNestの保存値をコピー
    //  ・確率：追加する弾を20%、既存の弾は今の比率のまま合計80%に縮める（再実行時は縮めない）
    //  ・後半（Phase2 Bullet Choices）は変更しない
    // ======================================================
    private const string IronNestPrefabPath = "Assets/Prefabs/Enemies/IronNest.prefab";

    [MenuItem("Tools/NeonDancer/2 前半の攻撃・ステージ/P3にSweepRapid・P2にTelegraph3Wayを追加（IronNestからコピー）")]
    private static void AddIronNestAttacksToPhase1()
    {
        var data = AssetDatabase.LoadAssetAtPath<EnemyData>(DstData);
        var ironGo = AssetDatabase.LoadAssetAtPath<GameObject>(IronNestPrefabPath);
        if (data == null || ironGo == null)
        {
            EditorUtility.DisplayDialog("IronNestの撃ち方", "EnemyData_NeonDancer または IronNest.prefab が見つかりません。", "OK");
            return;
        }
        IronNestNMPhase2Attack FindAtk(string nm)
        {
            foreach (var t in ironGo.GetComponentsInChildren<Transform>(true))
                if (t.name.Trim() == nm) return t.GetComponent<IronNestNMPhase2Attack>();
            return null;
        }
        var atk1 = FindAtk("NM01");
        var atk2 = FindAtk("NM02");
        if (atk1 == null || atk2 == null)
        {
            EditorUtility.DisplayDialog("IronNestの撃ち方", "IronNest.prefabのNM01/NM02にIronNestNMPhase2Attackが見つかりません。", "OK");
            return;
        }
        var so1 = new SerializedObject(atk1);
        var so2 = new SerializedObject(atk2);
        EnemyData d1 = atk1.GetComponent<EnemyShooter>() != null ? new SerializedObject(atk1.GetComponent<EnemyShooter>()).FindProperty("enemyData").objectReferenceValue as EnemyData : null;
        EnemyData d2 = atk2.GetComponent<EnemyShooter>() != null ? new SerializedObject(atk2.GetComponent<EnemyShooter>()).FindProperty("enemyData").objectReferenceValue as EnemyData : null;
        int i1 = so1.FindProperty("bulletTypeIndex").intValue;
        int i2 = so2.FindProperty("bulletTypeIndex").intValue;
        EnemyData.BulletType src1 = (d1 != null && d1.bulletTypes != null && i1 >= 0 && i1 < d1.bulletTypes.Length) ? d1.bulletTypes[i1] : null;
        EnemyData.BulletType src2 = (d2 != null && d2.bulletTypes != null && i2 >= 0 && i2 < d2.bulletTypes.Length) ? d2.bulletTypes[i2] : null;
        if (src1 == null || src2 == null)
        {
            EditorUtility.DisplayDialog("IronNestの撃ち方", $"コピー元の弾が見つかりません（NM01：{(d1 != null ? d1.name : "EnemyData未設定")} {i1}番／NM02：{(d2 != null ? d2.name : "EnemyData未設定")} {i2}番）。", "OK");
            return;
        }

        if (!EditorUtility.DisplayDialog("IronNestの撃ち方",
                $"・EnemyData_NeonDancerのBullet Typesに追加（同名があれば上書き）\n" +
                $"  「P1 SweepRapid」← {d1.name} {i1}番「{src1.name}」\n" +
                $"  「P1 Telegraph3Way」← {d2.name} {i2}番「{src2.name}」\n" +
                "・ND_Turret_P3の前半にSweepRapid（20%）、ND_Turret_P2の前半にTelegraph3Way（20%）を追加。既存の弾は今の比率のまま合計80%に\n" +
                "・NeonDancerControllerの前半 SweepRapid／Telegraph3Wayの数値をIronNestからコピー\n" +
                "後半は変更しません。続けますか？", "追加する", "キャンセル"))
            return;

        Undo.RecordObject(data, "NeonDancer Phase1 IronNest Attacks");
        var list = new System.Collections.Generic.List<EnemyData.BulletType>(data.bulletTypes ?? new EnemyData.BulletType[0]);
        int Upsert(string name, EnemyData.BulletType src, EnemyData srcData)
        {
            var copy = new EnemyData.BulletType();
            EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(src), copy);
            copy.name = name;
            if (copy.fireSEOverride == null) { copy.fireSEOverride = srcData.fireSE; copy.fireSEOverrideVolume = srcData.fireSEVolume; }
            if (copy.spriteOverride == null && srcData.bulletSpriteOverride != null) copy.spriteOverride = srcData.bulletSpriteOverride;
            int found = list.FindIndex(b => b != null && b.name == name);
            if (found >= 0) list[found] = copy; else { list.Add(copy); found = list.Count - 1; }
            Debug.Log($"[NeonDancerSetupTool] Bullet Types[{found}] {name} ← {srcData.name}[{src.name}]（speed={copy.speed}, lifeTime={copy.lifeTime}, telegraph={copy.useTelegraph}）");
            return found;
        }
        int idxSweep = Upsert("P1 SweepRapid", src1, d1);
        int idx3Way  = Upsert("P1 Telegraph3Way", src2, d2);
        data.bulletTypes = list.ToArray();
        EditorUtility.SetDirty(data);

        var log = new System.Text.StringBuilder();
        GameObject root = PrefabUtility.LoadPrefabContents(DstPrefab);
        try
        {
            var so = new SerializedObject(root.GetComponent<NeonDancerController>());
            so.FindProperty("sweepRapidHalfAngle").floatValue      = so1.FindProperty("sweepHalfAngle").floatValue;
            so.FindProperty("sweepRapidLegSeconds").floatValue     = so1.FindProperty("sweepLegSeconds").floatValue;
            so.FindProperty("sweepRapidShotInterval").floatValue   = so1.FindProperty("sweepShotInterval").floatValue;
            so.FindProperty("telegraph3WaySpreadDeg").floatValue   = so2.FindProperty("telegraphSpreadDeg").floatValue;
            so.FindProperty("telegraph3WayShotStagger").floatValue = so2.FindProperty("telegraphShotStagger").floatValue;
            so.ApplyModifiedPropertiesWithoutUndo();

            void AddChoice(string turretName, int idx, NeonDancerTurret.Phase2Attack atk)
            {
                NeonDancerTurret turret = null;
                foreach (var t in root.GetComponentsInChildren<NeonDancerTurret>(true)) if (t.name == turretName) { turret = t; break; }
                if (turret == null) { log.AppendLine($"{turretName} が見つかりません"); return; }
                var tso = new SerializedObject(turret);
                var choices = tso.FindProperty("bulletChoices");
                int existing = -1;
                for (int i = 0; i < choices.arraySize; i++)
                    if (choices.GetArrayElementAtIndex(i).FindPropertyRelative("bulletTypeIndex").intValue == idx) { existing = i; break; }
                if (existing < 0)
                {
                    // 既存の弾を今の比率のまま合計80%に縮めて、新しい弾を20%で追加
                    float total = 0f;
                    for (int i = 0; i < choices.arraySize; i++) total += Mathf.Max(0f, choices.GetArrayElementAtIndex(i).FindPropertyRelative("probabilityPercent").floatValue);
                    for (int i = 0; i < choices.arraySize; i++)
                    {
                        var p = choices.GetArrayElementAtIndex(i).FindPropertyRelative("probabilityPercent");
                        p.floatValue = total > 0f ? Mathf.Round(Mathf.Max(0f, p.floatValue) / total * 80f * 100f) / 100f : p.floatValue;
                    }
                    choices.arraySize++;
                    existing = choices.arraySize - 1;
                    choices.GetArrayElementAtIndex(existing).FindPropertyRelative("probabilityPercent").floatValue = 20f;
                }
                var c = choices.GetArrayElementAtIndex(existing);
                c.FindPropertyRelative("bulletTypeIndex").intValue = idx;
                c.FindPropertyRelative("attack").enumValueIndex = (int)atk;
                tso.ApplyModifiedPropertiesWithoutUndo();
                for (int i = 0; i < choices.arraySize; i++)
                {
                    var e = choices.GetArrayElementAtIndex(i);
                    int bi = e.FindPropertyRelative("bulletTypeIndex").intValue;
                    log.AppendLine($"{turretName} 前半[{i}]：{(bi >= 0 && bi < data.bulletTypes.Length ? data.bulletTypes[bi].name : "?")} {e.FindPropertyRelative("probabilityPercent").floatValue}%（{(NeonDancerTurret.Phase2Attack)e.FindPropertyRelative("attack").enumValueIndex}）");
                }
            }
            AddChoice("ND_Turret_P3", idxSweep, NeonDancerTurret.Phase2Attack.SweepRapid);
            AddChoice("ND_Turret_P2", idx3Way, NeonDancerTurret.Phase2Attack.Telegraph3Way);
            PrefabUtility.SaveAsPrefabAsset(root, DstPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[NeonDancerSetupTool] 前半にIronNestの撃ち方を追加しました\n" + log);
        EditorUtility.DisplayDialog("IronNestの撃ち方", "追加しました。\n" + log, "OK");
    }
}
