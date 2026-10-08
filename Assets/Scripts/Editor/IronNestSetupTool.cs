using UnityEditor;
using UnityEngine;

/// <summary>
/// Area3ボス「IronNest」用のセットアップメニュー。
/// </summary>
public static class IronNestSetupTool
{
    private const string IronNestPrefab = "Assets/Prefabs/Enemies/IronNest.prefab";

    // 後半のNMの撃ち方：NM01/02/03にIronNestNMPhase2Attackを追加（既にあれば設定し直す）。
    //  弾の番号は各NMのEnemyData（EnemyShooterに設定されているもの）のBullet Typesから名前で探す
    [MenuItem("Tools/IronNest/後半のNMの撃ち方を設定")]
    private static void SetupPhase2Attacks()
    {
        var specs = new (string nm, IronNestNMPhase2Attack.Kind kind, string typeName)[]
        {
            ("NM01", IronNestNMPhase2Attack.Kind.SweepRapid, "Rapid"),
            ("NM02", IronNestNMPhase2Attack.Kind.Telegraph3Way, "Telegraph"),
            ("NM03", IronNestNMPhase2Attack.Kind.Countdown3WayCurve, "Countdown"),
        };
        if (!EditorUtility.DisplayDialog("IronNest：後半のNMの撃ち方",
                "IronNest.prefabのNM01/02/03に IronNestNMPhase2Attack を追加し、次の撃ち方を設定します：\n" +
                "・NM01：Rapidを旋回しながら掃射\n・NM02：3wayのTelegraph\n・NM03：3wayのCountdown（左右は弧を描いてプレイヤーの位置へ）\n" +
                "（本体のHPが各NMのEnemyDataのHp Threshold Percentage未満で切り替わる）\n続けますか？", "実行", "キャンセル"))
            return;

        var log = new System.Text.StringBuilder();
        GameObject root = PrefabUtility.LoadPrefabContents(IronNestPrefab);
        try
        {
            foreach (var s in specs)
            {
                Transform nm = FindDeep(root.transform, s.nm);
                var shooter = nm != null ? nm.GetComponent<EnemyShooter>() : null;
                if (nm == null || shooter == null || nm.GetComponent<IronNestNM>() == null)
                {
                    log.AppendLine($"{s.nm}：見つかりません（NM・EnemyShooter・IronNestNMのいずれか）");
                    continue;
                }
                var data = new SerializedObject(shooter).FindProperty("enemyData").objectReferenceValue as EnemyData;
                int idx = -1;
                if (data != null && data.bulletTypes != null)
                    for (int i = 0; i < data.bulletTypes.Length; i++)
                        if (data.bulletTypes[i] != null && data.bulletTypes[i].name == s.typeName) { idx = i; break; }
                if (idx < 0)
                {
                    log.AppendLine($"{s.nm}：EnemyData（{(data != null ? data.name : "未設定")}）に「{s.typeName}」が見つかりません");
                    continue;
                }
                Transform muzzle = FindDeep(nm, "Muzzle");

                var atk = nm.GetComponent<IronNestNMPhase2Attack>();
                if (atk == null) atk = nm.gameObject.AddComponent<IronNestNMPhase2Attack>();
                var so = new SerializedObject(atk);
                so.FindProperty("kind").enumValueIndex = (int)s.kind;
                so.FindProperty("bulletTypeIndex").intValue = idx;
                so.FindProperty("muzzle").objectReferenceValue = muzzle;
                so.ApplyModifiedPropertiesWithoutUndo();
                log.AppendLine($"{s.nm}：{s.kind}（{data.name} の {s.typeName}＝{idx}番、Muzzle＝{(muzzle != null ? "あり" : "なし（NMの位置から撃つ）")}）");
            }
            PrefabUtility.SaveAsPrefabAsset(root, IronNestPrefab);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("[IronNestSetupTool] 後半のNMの撃ち方を設定しました\n" + log);
        EditorUtility.DisplayDialog("IronNest", "設定しました。\n" + log, "OK");
    }

    // 後半の横移動（Move Types「Horizontal2」）が画面外まで行かないようにする：
    //  これまでは出現位置から±Range（9）まで動けたため、戦車が画面から見切れていた。
    //  画面端で折り返す設定（Use Screen Bounds）をONにし、戦車の半分の幅＋壁の分の余白と、左のスキルHUDの幅を入れる
    private const string IronNestData = "Assets/GameData/Enemies/EnemyData_IronNest.asset";
    private const string Horizontal2Name = "Horizontal2";
    private const float ScreenMargin = 3f;        // 戦車の見えている横幅の半分（約2.4）＋左右の壁の分
    private const float SkillHudPixelWidth = 280f; // Fortress等と同じ

    [MenuItem("Tools/IronNest/後半の横移動を画面内に収める")]
    private static void FitPhase2HorizontalInScreen()
    {
        var data = AssetDatabase.LoadAssetAtPath<EnemyData>(IronNestData);
        EnemyData.MoveType mt = null;
        if (data != null && data.moveTypes != null)
            foreach (var m in data.moveTypes) if (m != null && m.name == Horizontal2Name) { mt = m; break; }
        if (mt == null)
        {
            EditorUtility.DisplayDialog("IronNest", $"EnemyData_IronNest の Move Types に「{Horizontal2Name}」が見つかりません。", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("IronNest：後半の横移動を画面内に収める",
                $"EnemyData_IronNest の Move Types「{Horizontal2Name}」を変更します：\n" +
                $"・Use Screen Bounds：{mt.useScreenBounds} → ON（画面端で折り返す）\n" +
                $"・Screen Bounds Margin Left / Right：{mt.screenBoundsMarginLeft} / {mt.screenBoundsMarginRight} → {ScreenMargin} / {ScreenMargin}\n" +
                $"・Screen Bounds Skill Hud Pixel Width：{mt.screenBoundsSkillHudPixelWidth} → {SkillHudPixelWidth}\n続けますか？", "変更する", "キャンセル"))
            return;
        Undo.RecordObject(data, "IronNest Horizontal2 Screen Bounds");
        mt.useScreenBounds = true;
        mt.screenBoundsMarginLeft = ScreenMargin;
        mt.screenBoundsMarginRight = ScreenMargin;
        mt.screenBoundsSkillHudPixelWidth = SkillHudPixelWidth;
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();
        Debug.Log("[IronNestSetupTool] Horizontal2を画面端で折り返すように設定しました");
        EditorUtility.DisplayDialog("IronNest", "設定しました。", "OK");
    }

    private static Transform FindDeep(Transform parent, string name)
    {
        foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
            if (t.name.Trim() == name) return t;
        return null;
    }
}
