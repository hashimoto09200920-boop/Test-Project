using UnityEngine;
using UnityEditor;

/// <summary>
/// AreaConfig作成を支援するエディタースクリプト
/// EnemySpawnerのWave Stagesを簡単にAreaConfigにコピーできる
/// </summary>
public class AreaConfigHelper : EditorWindow
{
    private EnemySpawner sourceSpawner;
    private AreaConfig targetAreaConfig;

    [MenuItem("Tools/Area Config Helper")]
    public static void ShowWindow()
    {
        GetWindow<AreaConfigHelper>("Area Config Helper");
    }

    private void OnGUI()
    {
        GUILayout.Label("Wave Stages Copy Tool", EditorStyles.boldLabel);
        GUILayout.Space(10);

        EditorGUILayout.HelpBox(
            "EnemySpawnerのWave Stages設定をAreaConfigにコピーします。\n" +
            "1. Source Spawner: 05_GameシーンのEnemySpawner\n" +
            "2. Target Area Config: 作成したAreaConfig\n" +
            "3. Copy Stages ボタンをクリック",
            MessageType.Info
        );

        GUILayout.Space(10);

        // Source Spawner
        sourceSpawner = (EnemySpawner)EditorGUILayout.ObjectField(
            "Source Spawner",
            sourceSpawner,
            typeof(EnemySpawner),
            true
        );

        // Target Area Config
        targetAreaConfig = (AreaConfig)EditorGUILayout.ObjectField(
            "Target Area Config",
            targetAreaConfig,
            typeof(AreaConfig),
            false
        );

        GUILayout.Space(10);

        GUI.enabled = sourceSpawner != null && targetAreaConfig != null;

        if (GUILayout.Button("Copy Wave Stages", GUILayout.Height(40)))
        {
            CopyWaveStages();
        }

        GUI.enabled = true;

        GUILayout.Space(10);

        if (sourceSpawner != null)
        {
            EditorGUILayout.HelpBox(
                $"Source: {sourceSpawner.name}\n" +
                $"Wave Stages: {GetWaveStageCount()} stages",
                MessageType.None
            );
        }
    }

    private void CopyWaveStages()
    {
        if (sourceSpawner == null || targetAreaConfig == null)
        {
            EditorUtility.DisplayDialog("エラー", "Source SpawnerとTarget Area Configの両方を設定してください。", "OK");
            return;
        }

        // Reflection を使用してWave Stagesを取得
        var spawnerType = typeof(EnemySpawner);
        var waveStagesField = spawnerType.GetField("waveStages",
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);

        if (waveStagesField == null)
        {
            EditorUtility.DisplayDialog("エラー", "Wave Stagesフィールドが見つかりませんでした。", "OK");
            return;
        }

        var waveStages = waveStagesField.GetValue(sourceSpawner) as EnemySpawner.WaveStage[];

        if (waveStages == null || waveStages.Length == 0)
        {
            EditorUtility.DisplayDialog("警告", "Source SpawnerにWave Stagesが設定されていません。", "OK");
            return;
        }

        // AreaConfigにコピー
        Undo.RecordObject(targetAreaConfig, "Copy Wave Stages");
        targetAreaConfig.waveStages = waveStages;
        EditorUtility.SetDirty(targetAreaConfig);

        EditorUtility.DisplayDialog(
            "完了",
            $"{waveStages.Length}個のWave Stagesをコピーしました。\n\n" +
            $"Source: {sourceSpawner.name}\n" +
            $"Target: {targetAreaConfig.name}",
            "OK"
        );

        Debug.Log($"[AreaConfigHelper] Copied {waveStages.Length} wave stages from {sourceSpawner.name} to {targetAreaConfig.name}");
    }

    private int GetWaveStageCount()
    {
        if (sourceSpawner == null) return 0;

        var spawnerType = typeof(EnemySpawner);
        var waveStagesField = spawnerType.GetField("waveStages",
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);

        if (waveStagesField == null) return 0;

        var waveStages = waveStagesField.GetValue(sourceSpawner) as EnemySpawner.WaveStage[];
        return waveStages?.Length ?? 0;
    }
}
