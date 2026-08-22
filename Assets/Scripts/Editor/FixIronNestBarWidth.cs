using UnityEngine;
using UnityEditor;

/// <summary>
/// IronNest(Area3ボス)・CondorのEnemyHealthDisplayは autoBarWidth=true になっており、
/// 自動検出される最初の子SpriteRenderer（Animator付き）の見た目サイズを毎フレーム参照するため、
/// アニメーションの姿勢によってバー幅が変動してしまう（HPバーが異常に長く見える原因）。
/// 他37体と同じ「固定値の手動指定」に統一する。
/// </summary>
public class FixIronNestBarWidth : MonoBehaviour
{
    private const string IronNestPath = "Assets/Prefabs/Enemies/IronNest.prefab";
    private const string CondorPath = "Assets/Prefabs/Enemies/Condor.prefab";

    [MenuItem("Tools/Enemies/Fix IronNest Bar Width (Auto->Manual)")]
    public static void FixIronNest()
    {
        FixOne(IronNestPath);
    }

    [MenuItem("Tools/Enemies/Fix Condor Bar Width (Auto->Manual)")]
    public static void FixCondor()
    {
        FixOne(CondorPath);
    }

    private static void FixOne(string prefabPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[FixIronNestBarWidth] Prefabが見つかりません: {prefabPath}");
            return;
        }

        EnemyHealthDisplay display = prefab.GetComponent<EnemyHealthDisplay>();
        if (display == null)
        {
            Debug.LogError($"[FixIronNestBarWidth] EnemyHealthDisplayが見つかりません: {prefabPath}");
            return;
        }

        SerializedObject so = new SerializedObject(display);
        SerializedProperty autoWidthProp = so.FindProperty("autoBarWidth");
        SerializedProperty barWidthProp = so.FindProperty("barWidth");

        if (autoWidthProp == null || barWidthProp == null)
        {
            Debug.LogError($"[FixIronNestBarWidth] プロパティが見つかりません: {prefabPath}");
            return;
        }

        Debug.Log($"[FixIronNestBarWidth] {prefabPath} 現在値: autoBarWidth={autoWidthProp.boolValue}, barWidth={barWidthProp.floatValue}");

        autoWidthProp.boolValue = false;
        // barWidthは既存の保存値をそのまま使う。見た目に合わなければInspectorで手動調整する。

        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SavePrefabAsset(prefab);
        AssetDatabase.SaveAssets();

        Debug.Log($"[FixIronNestBarWidth] {prefabPath}: autoBarWidthをfalseにしました。見た目に合わなければEnemyHealthDisplayのbarWidthを手動調整してください。");
    }
}
