#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Assets/Prefabs/Enemies配下の全Prefabを走査し、EnemyStatsを持つものの
/// fadeOutDurationが現在2秒になっているものだけを1秒に一括変更するメニュー。
/// 2秒以外の値（0や意図的な別値）は変更しない。Play前のEditor拡張のみで完結する。
/// </summary>
public static class EnemyFadeOutDurationRevertTo1s
{
    private const string PrefabFolder = "Assets/Prefabs/Enemies";
    private const float FromDuration = 2f;
    private const float ToDuration = 1f;

    [MenuItem("Tools/NEON DANCER/全エネミーのFadeOutDurationを2秒→1秒に一括変更")]
    private static void RevertFadeOutDurationOnAll()
    {
        string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { PrefabFolder });

        int changedCount = 0;
        int skippedNoComponentCount = 0;
        int skippedDifferentValueCount = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string enemyName = Path.GetFileNameWithoutExtension(path);

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);
            try
            {
                EnemyStats stats = prefabRoot.GetComponent<EnemyStats>();
                if (stats == null)
                {
                    skippedNoComponentCount++;
                    continue;
                }

                SerializedObject so = new SerializedObject(stats);
                SerializedProperty prop = so.FindProperty("fadeOutDuration");
                if (prop == null)
                {
                    skippedNoComponentCount++;
                    continue;
                }

                // ★2秒以外（0や意図的な個別値）はそのまま維持し、触らない
                if (!Mathf.Approximately(prop.floatValue, FromDuration))
                {
                    skippedDifferentValueCount++;
                    continue;
                }

                prop.floatValue = ToDuration;
                so.ApplyModifiedProperties();

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                Debug.Log($"[EnemyFadeOutDurationRevertTo1s] {enemyName}: fadeOutDuration {FromDuration} → {ToDuration}");
                changedCount++;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[EnemyFadeOutDurationRevertTo1s] 完了：{changedCount}件変更、"
            + $"{skippedDifferentValueCount}件は2秒以外のためスキップ、{skippedNoComponentCount}件はEnemyStats/フィールド無しでスキップ");
    }
}
#endif
