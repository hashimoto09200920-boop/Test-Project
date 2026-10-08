using UnityEditor;
using UnityEngine;

/// <summary>
/// Area2ボス「Condor」用のセットアップメニュー。
/// </summary>
public static class CondorSetupTool
{
    private const string CondorPrefab = "Assets/Prefabs/Enemies/Condor.prefab";

    // 左右の翼の羽ばたきを同時に再生させる部品（CondorWingSync）をCondor.prefabのルートに追加し、翼のAnimatorをアサインする
    [MenuItem("Tools/Condor/翼の羽ばたきを左右同期")]
    private static void AddWingSync()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(CondorPrefab);
        try
        {
            Transform right = root.transform.Find("WingRight");
            Transform left = root.transform.Find("WingLeft");
            Animator ra = right != null ? right.GetComponent<Animator>() : null;
            Animator la = left != null ? left.GetComponent<Animator>() : null;
            if (ra == null || la == null)
            {
                EditorUtility.DisplayDialog("Condor", "Condor.prefabのWingRight / WingLeft（Animator）が見つかりません。", "OK");
                return;
            }
            bool exists = root.GetComponent<CondorWingSync>() != null;
            if (!EditorUtility.DisplayDialog("Condor：翼の羽ばたきを左右同期",
                    (exists ? "CondorWingSyncは追加済みです。翼のアサインを設定し直します。\n" : "Condor.prefab（ルート）にCondorWingSyncを追加します。\n") +
                    "・基準の翼：WingRight\n・合わせる翼：WingLeft\n続けますか？", "実行", "キャンセル"))
                return;

            var sync = root.GetComponent<CondorWingSync>();
            if (sync == null) sync = root.AddComponent<CondorWingSync>();
            var so = new SerializedObject(sync);
            so.FindProperty("leaderWing").objectReferenceValue = ra;
            var arr = so.FindProperty("followerWings");
            arr.arraySize = 1;
            arr.GetArrayElementAtIndex(0).objectReferenceValue = la;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, CondorPrefab);
            Debug.Log("[CondorSetupTool] Condor.prefabにCondorWingSyncを設定しました（基準:WingRight / 合わせる:WingLeft）");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Condor", "設定しました。", "OK");
    }

    // ======================================================
    // 特殊攻撃（羽根の一斉射撃・雷の羽ばたき）：
    //  ・EnemyData_Condorに弾の設定「Feather」「Thunder」を追加（Normal＝0番をコピーし、特殊な動きは全てOFF。既にあれば追加しない）
    //  ・Condor.prefab（ルート）にCondorSpecialAttackを追加し、翼・羽ばたき同期・弾の番号を設定
    // ======================================================
    private const string CondorData = "Assets/GameData/Enemies/EnemyData_Condor.asset";
    private const string FeatherTypeName = "Feather";
    private const string ThunderTypeName = "Thunder";

    [MenuItem("Tools/Condor/特殊攻撃（羽根の一斉射撃・雷の羽ばたき）を追加")]
    private static void AddSpecialAttack()
    {
        var data = AssetDatabase.LoadAssetAtPath<EnemyData>(CondorData);
        if (data == null || data.bulletTypes == null || data.bulletTypes.Length == 0)
        {
            EditorUtility.DisplayDialog("Condor", "EnemyData_Condor（Bullet Types）が見つかりません。", "OK");
            return;
        }
        int featherIdx = FindTypeIndex(data, FeatherTypeName);
        int thunderIdx = FindTypeIndex(data, ThunderTypeName);
        if (!EditorUtility.DisplayDialog("Condor：特殊攻撃を追加",
                "次の設定を行います：\n" +
                $"・EnemyData_Condor の Bullet Types に「{FeatherTypeName}」{(featherIdx >= 0 ? $"（既にあり：{featherIdx}番）" : "を追加")}・" +
                $"「{ThunderTypeName}」{(thunderIdx >= 0 ? $"（既にあり：{thunderIdx}番）" : "を追加")}\n" +
                "　（追加分は0番「" + data.bulletTypes[0].name + "」をコピーし、特殊な動きはOFF・色と大きさを設定）\n" +
                "・Condor.prefab（ルート）に CondorSpecialAttack を追加し、翼と弾の番号を設定\n続けますか？", "実行", "キャンセル"))
            return;

        Undo.RecordObject(data, "Add Condor Special Bullet Types");
        var list = new System.Collections.Generic.List<EnemyData.BulletType>(data.bulletTypes);
        if (featherIdx < 0)
        {
            var t = CloneType(data.bulletTypes[0], FeatherTypeName);
            t.useColorOverride = true;
            t.colorOverride = new Color(0.78f, 0.88f, 0.95f, 1f); // 翼の羽根の色（灰青）
            t.useScaleOverride = true;
            t.scaleOverride = new Vector2(0.8f, 1.3f);            // 仮画像：少し細長く
            list.Add(t);
            featherIdx = list.Count - 1;
        }
        if (thunderIdx < 0)
        {
            var t = CloneType(data.bulletTypes[0], ThunderTypeName);
            t.useColorOverride = true;
            t.colorOverride = new Color(0.35f, 0.95f, 1f, 1f);    // 翼の縁・体の模様のシアン
            t.useUnreflectedTrail = true;
            t.unreflectedTrailColor = new Color(0.35f, 0.95f, 1f, 1f);
            list.Add(t);
            thunderIdx = list.Count - 1;
        }
        data.bulletTypes = list.ToArray();
        EditorUtility.SetDirty(data);

        GameObject root = PrefabUtility.LoadPrefabContents(CondorPrefab);
        try
        {
            Transform right = root.transform.Find("WingRight");
            Transform left = root.transform.Find("WingLeft");
            if (right == null || left == null)
            {
                Debug.LogError("[CondorSetupTool] Condor.prefabのWingRight / WingLeftが見つかりません");
                return;
            }
            var sp = root.GetComponent<CondorSpecialAttack>();
            if (sp == null) sp = root.AddComponent<CondorSpecialAttack>();
            var so = new SerializedObject(sp);
            so.FindProperty("wingRight").objectReferenceValue = right;
            so.FindProperty("wingLeft").objectReferenceValue = left;
            so.FindProperty("wingSync").objectReferenceValue = root.GetComponent<CondorWingSync>();
            so.FindProperty("featherBulletTypeIndex").intValue = featherIdx;
            so.FindProperty("thunderBulletTypeIndex").intValue = thunderIdx;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, CondorPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        bool hasSync = AssetDatabase.LoadAssetAtPath<GameObject>(CondorPrefab).GetComponent<CondorWingSync>() != null;
        Debug.Log($"[CondorSetupTool] 特殊攻撃を設定しました（Feather={featherIdx}番 / Thunder={thunderIdx}番 / 羽ばたき同期={(hasSync ? "あり" : "なし")}）");
        EditorUtility.DisplayDialog("Condor", $"設定しました。\nFeather＝{featherIdx}番、Thunder＝{thunderIdx}番" +
            (hasSync ? "" : "\n※CondorWingSyncが無いため、溜め中の速い羽ばたきは無効です（「翼の羽ばたきを左右同期」を先に実行してから、このメニューを再実行してください）"), "OK");
    }

    // ======================================================
    // 羽根の画像（Feather.png）と雷の羽ばたきの数値をまとめて設定する
    //  ・Feather.png：1単位あたりのピクセル数を700に（羽根の長さ≒0.67。通常の弾は直径≒0.36）
    //  ・EnemyData_Condorの「Feather」：画像をFeather.pngに、仮の色替え・細長い拡大はOFF（当たり判定は通常の弾と同じ大きさのまま）
    //  ・Condor.prefabのCondorSpecialAttack：雷の確率85%（羽根15%）、ジグザグ60°・1回曲がる距離1.4、後半の間隔5〜7秒
    // ======================================================
    private const string FeatherPngPath = "Assets/Art/Enemy/S3_02_Condor/Feather.png";
    private const float FeatherPixelsPerUnit = 700f;

    [MenuItem("Tools/Condor/羽根の画像と雷の数値を設定")]
    private static void ApplyFeatherImageAndThunderValues()
    {
        var importer = AssetImporter.GetAtPath(FeatherPngPath) as TextureImporter;
        var data = AssetDatabase.LoadAssetAtPath<EnemyData>(CondorData);
        int featherIdx = data != null ? FindTypeIndex(data, FeatherTypeName) : -1;
        if (importer == null || data == null || featherIdx < 0)
        {
            EditorUtility.DisplayDialog("Condor", "Feather.png、またはEnemyData_Condorの「Feather」が見つかりません（先に「特殊攻撃…を追加」を実行してください）。", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("Condor：羽根の画像と雷の数値を設定",
                "次の設定を行います：\n" +
                $"・Feather.png：Pixels Per Unit {importer.spritePixelsToUnits} → {FeatherPixelsPerUnit}（羽根の長さ約0.67）\n" +
                "・EnemyData_Condor「Feather」：画像＝Feather.png、色替え・拡大はOFF\n" +
                "・Condor.prefab > CondorSpecialAttack：後半の羽根の確率15%（雷85%）、ジグザグ60°・1回曲がる距離1.4、後半の間隔5〜7秒\n続けますか？",
                "実行", "キャンセル"))
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsToUnits = FeatherPixelsPerUnit;
        importer.SaveAndReimport();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(FeatherPngPath);

        Undo.RecordObject(data, "Condor Feather Sprite");
        var t = data.bulletTypes[featherIdx];
        t.spriteOverride = sprite;
        t.useColorOverride = false;
        t.useScaleOverride = false;
        EditorUtility.SetDirty(data);

        GameObject root = PrefabUtility.LoadPrefabContents(CondorPrefab);
        try
        {
            var sp = root.GetComponent<CondorSpecialAttack>();
            if (sp == null)
            {
                Debug.LogError("[CondorSetupTool] Condor.prefabにCondorSpecialAttackがありません（先に「特殊攻撃…を追加」を実行してください）");
                return;
            }
            var so = new SerializedObject(sp);
            so.FindProperty("phase2FeatherChancePercent").floatValue = 15f;
            so.FindProperty("thunderZigzagAngleDeg").floatValue = 60f;
            so.FindProperty("thunderZigzagSegment").floatValue = 1.4f;
            so.FindProperty("phase2IntervalMin").floatValue = 5f;
            so.FindProperty("phase2IntervalMax").floatValue = 7f;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, CondorPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[CondorSetupTool] 羽根の画像（PPU {FeatherPixelsPerUnit}）と雷の数値を設定しました（Feather={featherIdx}番）");
        EditorUtility.DisplayDialog("Condor", "設定しました。", "OK");
    }

    // ======================================================
    // 通常攻撃を減らして羽根・雷を中心にする
    //  ・EnemyData_Condorの通常の弾（Feather/Thunder以外）：撃つ間隔（Fire Interval Override・Fire Interval Min Seconds）を2倍
    //    （1回だけ。CondorSpecialAttackに適用済みの印を残し、再実行で4倍にならないようにする）
    //  ・CondorSpecialAttack：特殊攻撃の間隔 前半4〜6秒・後半3〜4秒
    // ======================================================
    private const float NormalFireIntervalScale = 2f;

    [MenuItem("Tools/Condor/通常攻撃を減らして羽根・雷を中心にする")]
    private static void ReduceNormalFireFavorSpecials()
    {
        var data = AssetDatabase.LoadAssetAtPath<EnemyData>(CondorData);
        if (data == null || data.bulletTypes == null)
        {
            EditorUtility.DisplayDialog("Condor", "EnemyData_Condorが見つかりません。", "OK");
            return;
        }
        GameObject root = PrefabUtility.LoadPrefabContents(CondorPrefab);
        try
        {
            var sp = root.GetComponent<CondorSpecialAttack>();
            if (sp == null)
            {
                EditorUtility.DisplayDialog("Condor", "Condor.prefabにCondorSpecialAttackがありません（先に「特殊攻撃…を追加」を実行してください）。", "OK");
                return;
            }
            var so = new SerializedObject(sp);
            var applied = so.FindProperty("normalFireReducedApplied");
            bool scaleNormal = !applied.boolValue;

            var before = new System.Text.StringBuilder();
            foreach (var t in data.bulletTypes)
            {
                if (t == null || t.name == FeatherTypeName || t.name == ThunderTypeName) continue;
                before.AppendLine($"{t.name}：{t.fireIntervalOverride:0.##}秒（最短{t.fireIntervalMinSeconds:0.##}）");
            }
            string msg = "次の設定を行います：\n" +
                (scaleNormal
                    ? $"・通常の弾（Feather/Thunder以外）の撃つ間隔を{NormalFireIntervalScale}倍：\n" + before
                    : "・通常の弾の間隔は2倍に変更済みのため変更しません\n") +
                "・特殊攻撃（羽根・雷）の間隔：前半4〜6秒、後半3〜4秒\n続けますか？";
            if (!EditorUtility.DisplayDialog("Condor：通常攻撃を減らして羽根・雷を中心に", msg, "実行", "キャンセル")) return;

            if (scaleNormal)
            {
                Undo.RecordObject(data, "Condor Reduce Normal Fire");
                foreach (var t in data.bulletTypes)
                {
                    if (t == null || t.name == FeatherTypeName || t.name == ThunderTypeName) continue;
                    t.fireIntervalOverride *= NormalFireIntervalScale;
                    t.fireIntervalMinSeconds *= NormalFireIntervalScale;
                }
                EditorUtility.SetDirty(data);
                applied.boolValue = true;
            }
            so.FindProperty("intervalMin").floatValue = 4f;
            so.FindProperty("intervalMax").floatValue = 6f;
            so.FindProperty("phase2IntervalMin").floatValue = 3f;
            so.FindProperty("phase2IntervalMax").floatValue = 4f;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, CondorPrefab);
            Debug.Log("[CondorSetupTool] 通常攻撃を減らして羽根・雷を中心に設定しました" + (scaleNormal ? $"（通常の弾の間隔×{NormalFireIntervalScale}。変更前\n{before}）" : "（通常の弾の間隔は変更済みのため据え置き）"));
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Condor", "設定しました。", "OK");
    }

    // ======================================================
    // 羽根と雷の弾速：EnemyData_Condorの「Feather」4.5（元3の1.5倍）・「Thunder」6（元3の2倍）。値を直接設定するので再実行しても同じ
    // ======================================================
    private const float FeatherSpeed = 4.5f;
    private const float ThunderSpeed = 6f;

    [MenuItem("Tools/Condor/羽根と雷の弾速を設定（羽根4.5・雷6）")]
    private static void ApplySpecialBulletSpeeds()
    {
        var data = AssetDatabase.LoadAssetAtPath<EnemyData>(CondorData);
        int fi = data != null ? FindTypeIndex(data, FeatherTypeName) : -1;
        int ti = data != null ? FindTypeIndex(data, ThunderTypeName) : -1;
        if (fi < 0 || ti < 0)
        {
            EditorUtility.DisplayDialog("Condor", "EnemyData_Condorの「Feather」「Thunder」が見つかりません（先に「特殊攻撃…を追加」を実行してください）。", "OK");
            return;
        }
        var f = data.bulletTypes[fi];
        var t = data.bulletTypes[ti];
        if (!EditorUtility.DisplayDialog("Condor：羽根と雷の弾速",
                $"EnemyData_Condorの弾速を設定します：\n・Feather：{f.speed:0.##} → {FeatherSpeed}\n・Thunder：{t.speed:0.##} → {ThunderSpeed}\n続けますか？", "実行", "キャンセル"))
            return;
        Undo.RecordObject(data, "Condor Special Bullet Speeds");
        f.speed = FeatherSpeed;
        t.speed = ThunderSpeed;
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        Debug.Log($"[CondorSetupTool] 弾速を設定しました（Feather={FeatherSpeed} / Thunder={ThunderSpeed}）");
        EditorUtility.DisplayDialog("Condor", "設定しました。", "OK");
    }

    private static int FindTypeIndex(EnemyData data, string typeName)
    {
        for (int i = 0; i < data.bulletTypes.Length; i++)
            if (data.bulletTypes[i] != null && data.bulletTypes[i].name == typeName) return i;
        return -1;
    }

    // 弾の設定をコピーし、特殊な動き（波・螺旋・同時発射・予兆線・ミサイル・時限爆発・分裂・煙幕・ワープ・ビーム・ドリル等）はすべてOFFにする
    private static EnemyData.BulletType CloneType(EnemyData.BulletType src, string newName)
    {
        var t = new EnemyData.BulletType();
        EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(src), t);
        t.name = newName;
        t.useWaveMotion = false;
        t.useSpiralMotion = false;
        t.useMultiShot = false;
        t.useTelegraph = false;
        t.useMissileArc = false;
        t.useCountdownExplosion = false;
        t.useMultiWarhead = false;
        t.useSmokeGrenade = false;
        t.useWarp = false;
        t.useBeam = false;
        t.usePinnedReflect = false;
        t.useFireIntervalOverride = false;
        t.useFireIntervalRandom = false;
        t.useFirePauseCycle = false;
        return t;
    }

    // 8の字移動（Figure8）の開始方向を左右ランダムにする部品（CondorRandomFigure8Start）をCondor.prefabのルートに追加する
    [MenuItem("Tools/Condor/8の字移動の開始方向を左右ランダムにする")]
    private static void AddRandomFigure8Start()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(CondorPrefab);
        try
        {
            if (root.GetComponent<EnemyMover>() == null)
            {
                EditorUtility.DisplayDialog("Condor", "Condor.prefabのルートにEnemyMoverが見つかりません。", "OK");
                return;
            }
            if (root.GetComponent<CondorRandomFigure8Start>() != null)
            {
                EditorUtility.DisplayDialog("Condor", "CondorRandomFigure8Startは追加済みです。", "OK");
                return;
            }
            if (!EditorUtility.DisplayDialog("Condor：8の字の開始方向を左右ランダム",
                    "Condor.prefab（ルート）にCondorRandomFigure8Startを追加します（左から始める確率50%）。\n続けますか？", "追加する", "キャンセル"))
                return;
            root.AddComponent<CondorRandomFigure8Start>();
            PrefabUtility.SaveAsPrefabAsset(root, CondorPrefab);
            Debug.Log("[CondorSetupTool] Condor.prefabにCondorRandomFigure8Startを追加しました");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("Condor", "追加しました。", "OK");
    }
}
