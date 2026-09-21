#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// EnemyStats.FadeOutAndDestroy()が子オブジェクトのSpriteRendererまで見るように修正したことに伴い、
/// 見た目が子オブジェクトにある（Marshal/Dragon等の複合コントローラー系）敵19体のfadeOutDurationを
/// まとめて2秒に変更するための一括実行メニュー。Play前のEditor拡張のみで完結し、実行中の
/// 生成・変更は行わない。
/// </summary>
public static class EnemyFadeOutDurationBulkFix
{
    private const string PrefabFolder = "Assets/Prefabs/Enemies";
    private const float TargetFadeOutDuration = 2f;

    private static readonly string[] TargetEnemyNames =
    {
        "ArcGuard", "Cactus", "Camel", "Condor", "Fingers", "GuardBeast",
        "GyroWard", "Gyrorb", "IronNest", "Marshal", "Mask", "Obelisk",
        "Phantom", "PuppetHead", "Shaman", "Susanoo", "Tsukuyomi",
        "WalkerMech", "Zephyr",
    };

    [MenuItem("Tools/NEON DANCER/敵19体のFadeOutDurationを2秒に一括設定")]
    private static void SetFadeOutDurationOnAllTargets()
    {
        int changedCount = 0;
        int skippedCount = 0;

        foreach (string enemyName in TargetEnemyNames)
        {
            string path = $"{PrefabFolder}/{enemyName}.prefab";

            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                Debug.LogWarning($"[EnemyFadeOutDurationBulkFix] Prefabが見つかりません: {path}");
                skippedCount++;
                continue;
            }

            GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);
            try
            {
                EnemyStats stats = prefabRoot.GetComponent<EnemyStats>();
                if (stats == null)
                {
                    Debug.LogWarning($"[EnemyFadeOutDurationBulkFix] EnemyStatsが見つかりません: {path}");
                    skippedCount++;
                    continue;
                }

                SerializedObject so = new SerializedObject(stats);
                SerializedProperty prop = so.FindProperty("fadeOutDuration");
                if (prop == null)
                {
                    Debug.LogWarning($"[EnemyFadeOutDurationBulkFix] fadeOutDurationフィールドが見つかりません: {path}");
                    skippedCount++;
                    continue;
                }

                float before = prop.floatValue;
                prop.floatValue = TargetFadeOutDuration;
                so.ApplyModifiedProperties();

                PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
                Debug.Log($"[EnemyFadeOutDurationBulkFix] {enemyName}: fadeOutDuration {before} → {TargetFadeOutDuration}");
                changedCount++;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[EnemyFadeOutDurationBulkFix] 完了：{changedCount}件変更、{skippedCount}件スキップ");
    }
}
#endif
