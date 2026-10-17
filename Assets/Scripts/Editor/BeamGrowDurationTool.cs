using UnityEditor;
using UnityEngine;

/// <summary>
/// Bit・Obelisk・NeonDancer・DragonのBeamの「Beam Grow Duration」を一括で設定する（2026/10/17：全て0.3）。
/// ・Bit：Assets/Prefabs/Enemies/Bit.prefab > BitController > Beam Bullet Type（Prefabはファイルを直接編集せず、Prefabを開いて保存する）
/// ・Obelisk：EnemyData_Obelisk > Bullet Types > Element 14「Breath」
/// ・NeonDancer：EnemyData_NeonDancer > Bullet Types > Element 6「⑦Beam」・Element 13「⑦P2 SweepBeam」
/// ・Dragon：EnemyData_Dragon > Bullet Types > Element 14「Breath」
/// 何度実行しても同じ結果。実行結果（変更前→変更後）はConsoleに出す。
/// </summary>
public static class BeamGrowDurationTool
{
    private const float Duration = 0.3f;
    private const string BitPrefabPath = "Assets/Prefabs/Enemies/Bit.prefab";

    private static readonly (string path, int index, string expectedName)[] DataTargets =
    {
        ("Assets/GameData/Enemies/EnemyData_Obelisk.asset", 14, "Breath"),
        ("Assets/GameData/Enemies/EnemyData_NeonDancer.asset", 6, "⑦Beam"),
        ("Assets/GameData/Enemies/EnemyData_NeonDancer.asset", 13, "⑦P2 SweepBeam"),
        ("Assets/GameData/Enemies/EnemyData_Dragon.asset", 14, "Breath"),
    };

    [MenuItem("Tools/Beam/Beamの伸びる時間を0.3にする（Bit・Obelisk・NeonDancer・Dragon）")]
    private static void Run()
    {
        int ok = 0;

        // ===== EnemyData（Obelisk・NeonDancer・Dragon） =====
        foreach (var t in DataTargets)
        {
            var data = AssetDatabase.LoadAssetAtPath<EnemyData>(t.path);
            if (data == null) { Debug.LogError($"[BeamGrowDurationTool] {t.path} が見つかりません"); continue; }
            if (data.bulletTypes == null || t.index >= data.bulletTypes.Length || data.bulletTypes[t.index] == null)
            {
                Debug.LogError($"[BeamGrowDurationTool] {t.path} に Element {t.index} がありません");
                continue;
            }
            var bt = data.bulletTypes[t.index];
            if (!bt.useBeam || bt.name != t.expectedName)
            {
                Debug.LogError($"[BeamGrowDurationTool] {t.path} の Element {t.index} が想定と違うため変更しません（名前={bt.name}、Use Beam={bt.useBeam}。想定＝{t.expectedName}・Use Beam ON）");
                continue;
            }
            float before = bt.beamGrowDuration;
            Undo.RecordObject(data, "Beam Grow Duration");
            bt.beamGrowDuration = Duration;
            EditorUtility.SetDirty(data);
            Debug.Log($"[BeamGrowDurationTool] {System.IO.Path.GetFileNameWithoutExtension(t.path)} Element {t.index}「{bt.name}」：Beam Grow Duration {before} → {Duration}");
            ok++;
        }
        AssetDatabase.SaveAssets();

        // ===== Bit（Prefabを開いて設定し、保存する） =====
        GameObject root = PrefabUtility.LoadPrefabContents(BitPrefabPath);
        if (root == null) Debug.LogError($"[BeamGrowDurationTool] {BitPrefabPath} が見つかりません");
        else
        {
            try
            {
                var bit = root.GetComponent<BitController>();
                if (bit == null) Debug.LogError("[BeamGrowDurationTool] Bit.prefab のルートに BitController がありません");
                else
                {
                    var so = new SerializedObject(bit);
                    var p = so.FindProperty("beamBulletType.beamGrowDuration");
                    if (p == null) Debug.LogError("[BeamGrowDurationTool] BitController の beamBulletType.beamGrowDuration が見つかりません");
                    else
                    {
                        float before = p.floatValue;
                        p.floatValue = Duration;
                        so.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(bit);
                        PrefabUtility.SaveAsPrefabAsset(root, BitPrefabPath);
                        Debug.Log($"[BeamGrowDurationTool] Bit.prefab > BitController > Beam Bullet Type：Beam Grow Duration {before} → {Duration}");
                        ok++;
                    }
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        Debug.Log($"[BeamGrowDurationTool] 完了：{ok}/5 か所を設定しました");
    }
}
