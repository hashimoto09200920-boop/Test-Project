using UnityEditor;
using UnityEngine;

/// <summary>
/// Area8ボス「Obelisk」とBit用のセットアップメニュー。
/// </summary>
public static class ObeliskSetupTool
{
    private const string BitPrefab = "Assets/Prefabs/Enemies/Bit.prefab";
    private const string ObeliskPrefab = "Assets/Prefabs/Enemies/Obelisk.prefab";

    // 攻撃頻度を20%上げる（同じ時間で1.2倍撃つ＝間隔を1/1.2に）。2026/10/8時点の値（Bit 2.5〜9秒、Obelisk 15秒）を基準にした固定値にし、
    // 何度実行しても同じ値になるようにする
    private const float BitIntervalMin = 2.08f;      // 2.5 / 1.2
    private const float BitIntervalMax = 7.5f;       // 9 / 1.2
    private const float CentralBeamInterval = 12.5f; // 15 / 1.2

    [MenuItem("Tools/Obelisk/BitとObeliskの攻撃頻度を20%上げる")]
    private static void SpeedUpAttacks()
    {
        var bit = AssetDatabase.LoadAssetAtPath<GameObject>(BitPrefab);
        var obe = AssetDatabase.LoadAssetAtPath<GameObject>(ObeliskPrefab);
        var bitCtrl = bit != null ? bit.GetComponentInChildren<BitController>(true) : null;
        var obeCtrl = obe != null ? obe.GetComponentInChildren<ObeliskController>(true) : null;
        if (bitCtrl == null || obeCtrl == null)
        {
            EditorUtility.DisplayDialog("Obelisk", "Bit.prefab の BitController または Obelisk.prefab の ObeliskController が見つかりません。", "OK");
            return;
        }
        var bso = new SerializedObject(bitCtrl);
        var oso = new SerializedObject(obeCtrl);
        float bMin = bso.FindProperty("fireIntervalMin").floatValue;
        float bMax = bso.FindProperty("fireIntervalMax").floatValue;
        float oInt = oso.FindProperty("centralBeamInterval").floatValue;
        if (!EditorUtility.DisplayDialog("Obelisk：攻撃頻度を20%上げる",
                "次の値を変更します（同じ時間で約1.2倍撃つ）：\n" +
                $"・Bit.prefab > BitController > Fire Interval Min / Max：{bMin} / {bMax} → {BitIntervalMin} / {BitIntervalMax}\n" +
                $"・Obelisk.prefab > ObeliskController > Central Beam Interval：{oInt} → {CentralBeamInterval}\n続けますか？", "変更する", "キャンセル"))
            return;

        SetFloats(BitPrefab, typeof(BitController), ("fireIntervalMin", BitIntervalMin), ("fireIntervalMax", BitIntervalMax));
        SetFloats(ObeliskPrefab, typeof(ObeliskController), ("centralBeamInterval", CentralBeamInterval));
        AssetDatabase.SaveAssets();
        Debug.Log($"[ObeliskSetupTool] Bit Fire Interval {bMin}/{bMax} → {BitIntervalMin}/{BitIntervalMax}、Obelisk Central Beam Interval {oInt} → {CentralBeamInterval}");
        EditorUtility.DisplayDialog("Obelisk", "変更しました。", "OK");
    }

    // Bitのビームが痛すぎるため、ダンサー・フロアへのダメージ頻度だけを1秒4回→2回に下げる
    //   （Beam Damage Tick Rateはプレイヤーの線の検出間隔も兼ねるので4のまま。反射のしやすさは変わらない）
    private const float BitUnreflectedDamageTickRate = 2f;

    [MenuItem("Tools/Obelisk/Bitのビームのダンサー・フロアへのダメージ頻度を1秒2回に下げる")]
    private static void LowerBitBeamDamageRate()
    {
        var bit = AssetDatabase.LoadAssetAtPath<GameObject>(BitPrefab);
        var bitCtrl = bit != null ? bit.GetComponentInChildren<BitController>(true) : null;
        if (bitCtrl == null)
        {
            EditorUtility.DisplayDialog("Obelisk", "Bit.prefab の BitController が見つかりません。", "OK");
            return;
        }
        var so = new SerializedObject(bitCtrl);
        float tick = so.FindProperty("beamBulletType.beamDamageTickRate").floatValue;
        float cur = so.FindProperty("beamBulletType.beamUnreflectedDamageTickRate").floatValue;
        if (!EditorUtility.DisplayDialog("Bit：ビームのダメージ頻度",
                "Bit.prefab > BitController > Beam Bullet Type を変更します：\n" +
                $"・Beam Unreflected Damage Tick Rate（ダンサー・フロアへのダメージ頻度）：{(cur > 0f ? cur.ToString() : $"未設定（{tick}回/秒）")} → {BitUnreflectedDamageTickRate}回/秒\n" +
                $"・Beam Damage Tick Rate（線の検出・反射後のダメージ）：{tick}回/秒のまま\n続けますか？", "変更する", "キャンセル"))
            return;
        SetFloats(BitPrefab, typeof(BitController), ("beamBulletType.beamUnreflectedDamageTickRate", BitUnreflectedDamageTickRate));
        AssetDatabase.SaveAssets();
        Debug.Log($"[ObeliskSetupTool] Bit Beam Unreflected Damage Tick Rate → {BitUnreflectedDamageTickRate}");
        EditorUtility.DisplayDialog("Obelisk", "変更しました。", "OK");
    }

    private static void SetFloats(string path, System.Type type, params (string name, float value)[] values)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var comp = root.GetComponentInChildren(type, true);
            var so = new SerializedObject(comp);
            foreach (var (name, value) in values) so.FindProperty(name).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
