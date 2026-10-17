using UnityEditor;
using UnityEngine;

/// <summary>
/// Area4ボス（Balloon.prefab）の後半、神経衰弱をミスした時の攻撃（分裂体が黒くなる）で、
/// 全分裂体が同時に弾を撃たないよう、分裂体ごとの発射の時間差（ランダム）を設定する。
/// 設定先：Assets/Prefabs/Enemies/Balloon.prefab > Balloon > HalloweenBossController > Fragment Fire Lag Min / Max
/// （後から直接Inspectorで変更してもよい。この2つの値はミス時の攻撃にしか使われない）
/// </summary>
public static class BalloonMismatchFireLagTool
{
    private const string PrefabPath = "Assets/Prefabs/Enemies/Balloon.prefab";
    private const float LagMin = 0f;
    private const float LagMax = 0.6f;

    [MenuItem("Tools/Balloon/ミス時の攻撃に発射の時間差をつける（0〜0.6秒）")]
    private static void Apply()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null)
        {
            Debug.LogWarning("[BalloonMismatchFireLagTool] " + PrefabPath + " が見つかりません");
            return;
        }
        try
        {
            var boss = root.GetComponent<HalloweenBossController>();
            if (boss == null)
            {
                Debug.LogWarning("[BalloonMismatchFireLagTool] Balloon（ルート）にHalloweenBossControllerがありません");
                return;
            }
            var so = new SerializedObject(boss);
            var min = so.FindProperty("fragmentFireLagMin");
            var max = so.FindProperty("fragmentFireLagMax");
            float beforeMin = min.floatValue, beforeMax = max.floatValue;
            min.floatValue = LagMin;
            max.floatValue = LagMax;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log($"[BalloonMismatchFireLagTool] Fragment Fire Lag Min {beforeMin}→{LagMin}、Max {beforeMax}→{LagMax} に変更して保存しました（{PrefabPath}）");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
