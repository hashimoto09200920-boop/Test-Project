using UnityEditor;
using UnityEngine;

/// <summary>
/// Area1ボス「GravePole」用のセットアップメニュー。
/// </summary>
public static class GravePoleSetupTool
{
    private const string GravePolePrefab = "Assets/Prefabs/Enemies/GravePole.prefab";
    private const string GravePoleData = "Assets/GameData/Enemies/EnemyData_GravePole.asset";
    private const string EyeShotName = "EyeShot";
    private const string GazeShotName = "GazeShot";
    private const string ReleaseName = "ReleaseRing";

    // ======================================================
    // 特殊攻撃（①四つ目の連続射撃・②凝視・③瞬き→解放）：
    //  ・EnemyData_GravePoleに弾の設定「EyeShot」「GazeShot」「ReleaseRing」を追加（0番をコピーし、特殊な動きは全てOFF。既にあれば追加しない）
    //  ・GravePole.prefab（ルート）にGravePoleSpecialAttackを追加し、4つの目（GravePoleEyeの付いた子を上から順）と弾の番号を設定
    // ======================================================
    [MenuItem("Tools/GravePole/特殊攻撃（四つ目・凝視・瞬き）を追加")]
    private static void AddSpecialAttack()
    {
        var data = AssetDatabase.LoadAssetAtPath<EnemyData>(GravePoleData);
        if (data == null || data.bulletTypes == null || data.bulletTypes.Length == 0)
        {
            EditorUtility.DisplayDialog("GravePole", "EnemyData_GravePole（Bullet Types）が見つかりません。", "OK");
            return;
        }
        int eyeIdx = FindTypeIndex(data, EyeShotName);
        int gazeIdx = FindTypeIndex(data, GazeShotName);
        int relIdx = FindTypeIndex(data, ReleaseName);
        if (!EditorUtility.DisplayDialog("GravePole：特殊攻撃を追加",
                "次の設定を行います：\n" +
                $"・EnemyData_GravePole の Bullet Types に「{EyeShotName}」「{GazeShotName}」「{ReleaseName}」を追加（既にあるものは追加しない）\n" +
                $"　（0番「{data.bulletTypes[0].name}」をコピーし、特殊な動きはOFF・色を設定）\n" +
                "・GravePole.prefab（ルート）に GravePoleSpecialAttack を追加し、4つの目と弾の番号を設定\n続けますか？", "実行", "キャンセル"))
            return;

        Undo.RecordObject(data, "Add GravePole Special Bullet Types");
        var list = new System.Collections.Generic.List<EnemyData.BulletType>(data.bulletTypes);
        if (eyeIdx < 0) { list.Add(CloneType(data.bulletTypes[0], EyeShotName, new Color(1f, 0.85f, 0.5f, 1f))); eyeIdx = list.Count - 1; }   // 光った目の色
        if (gazeIdx < 0) { list.Add(CloneType(data.bulletTypes[0], GazeShotName, new Color(1f, 0.3f, 0.3f, 1f))); gazeIdx = list.Count - 1; } // 照準の赤
        if (relIdx < 0) { list.Add(CloneType(data.bulletTypes[0], ReleaseName, new Color(0.75f, 0.4f, 1f, 1f))); relIdx = list.Count - 1; }   // 瞳の紫
        data.bulletTypes = list.ToArray();
        EditorUtility.SetDirty(data);

        GameObject root = PrefabUtility.LoadPrefabContents(GravePolePrefab);
        try
        {
            var eyeComps = root.GetComponentsInChildren<GravePoleEye>(true);
            if (eyeComps.Length == 0)
            {
                Debug.LogError("[GravePoleSetupTool] GravePole.prefabに目（GravePoleEye）が見つかりません");
                return;
            }
            // 上から順（ルートから見た高さの大きい順）に並べる
            System.Array.Sort(eyeComps, (a, b) =>
                root.transform.InverseTransformPoint(b.transform.position).y.CompareTo(root.transform.InverseTransformPoint(a.transform.position).y));

            var sp = root.GetComponent<GravePoleSpecialAttack>();
            if (sp == null) sp = root.AddComponent<GravePoleSpecialAttack>();
            var so = new SerializedObject(sp);
            var arr = so.FindProperty("eyes");
            arr.arraySize = eyeComps.Length;
            for (int i = 0; i < eyeComps.Length; i++) arr.GetArrayElementAtIndex(i).objectReferenceValue = eyeComps[i].transform;
            so.FindProperty("eyeShotBulletTypeIndex").intValue = eyeIdx;
            so.FindProperty("gazeShotBulletTypeIndex").intValue = gazeIdx;
            so.FindProperty("releaseBulletTypeIndex").intValue = relIdx;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, GravePolePrefab);

            var names = new System.Text.StringBuilder();
            foreach (var e in eyeComps) names.Append(e.name.Trim()).Append(" ");
            Debug.Log($"[GravePoleSetupTool] 特殊攻撃を設定しました（目：{names}/ EyeShot={eyeIdx} GazeShot={gazeIdx} ReleaseRing={relIdx}）");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
        AssetDatabase.SaveAssets();
        EditorUtility.DisplayDialog("GravePole", $"設定しました。\nEyeShot＝{eyeIdx}番、GazeShot＝{gazeIdx}番、ReleaseRing＝{relIdx}番", "OK");
    }

    private static int FindTypeIndex(EnemyData data, string typeName)
    {
        for (int i = 0; i < data.bulletTypes.Length; i++)
            if (data.bulletTypes[i] != null && data.bulletTypes[i].name == typeName) return i;
        return -1;
    }

    // 弾の設定をコピーし、特殊な動き（波・螺旋・同時発射・予兆線・ミサイル・時限爆発・分裂・煙幕・ワープ・ビーム・ドリル等）はすべてOFFにして色を設定する
    private static EnemyData.BulletType CloneType(EnemyData.BulletType src, string newName, Color color)
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
        t.useColorOverride = true;
        t.colorOverride = color;
        return t;
    }
}
