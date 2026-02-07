using UnityEngine;
using UnityEditor;
using Game.UI;

/// <summary>
/// StageButtonManager の Inspector カスタマイズ
/// Context Menu の代わりにボタンを表示
/// </summary>
[CustomEditor(typeof(StageButtonManager))]
public class StageButtonManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // 通常のInspectorを描画
        DrawDefaultInspector();

        StageButtonManager manager = (StageButtonManager)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Setup", EditorStyles.boldLabel);

        // 強制再インポートボタン（重要）
        GUI.backgroundColor = Color.yellow;
        if (GUILayout.Button("⚠ Force Reimport AreaDef Assets ⚠", GUILayout.Height(40)))
        {
            ForceReimportAssets.ReimportAreaDefAssets();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(5);

        // AreaCatalog 再作成ボタン（重要）
        GUI.backgroundColor = Color.cyan;
        if (GUILayout.Button("🔄 Recreate AreaCatalog Asset 🔄", GUILayout.Height(40)))
        {
            RecreateAreaCatalog.RecreateAsset();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(5);

        if (GUILayout.Button("Find All Stage Buttons"))
        {
            manager.FindAllStageButtons();
        }

        if (GUILayout.Button("Auto Set Area IDs from Button Names"))
        {
            manager.AutoSetAreaIdsFromButtonNames();
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Refresh & Status", EditorStyles.boldLabel);

        if (GUILayout.Button("Refresh All Buttons"))
        {
            manager.RefreshAllButtons();
        }

        if (GUILayout.Button("Log All Button Status"))
        {
            manager.LogAllButtonStatus();
        }

        if (GUILayout.Button("Log Detailed Status"))
        {
            manager.LogDetailedStatus();
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Test Mode", EditorStyles.boldLabel);

        if (GUILayout.Button("Apply Test Mode To All"))
        {
            manager.ApplyTestModeToAll();
        }

        if (GUILayout.Button("Test: Unlock All"))
        {
            manager.TestUnlockAll();
        }

        if (GUILayout.Button("Test: Lock All"))
        {
            manager.TestLockAll();
        }

        if (GUILayout.Button("Test: Use Real Unlock Status"))
        {
            manager.UseRealUnlockStatus();
        }

        if (GUILayout.Button("Toggle Debug Text"))
        {
            manager.ToggleDebugText();
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Debug: Progress Control", EditorStyles.boldLabel);

        if (GUILayout.Button("Reset Progress"))
        {
            manager.DebugResetProgress();
        }

        if (GUILayout.Button("Clear Area_01 Stage 1"))
        {
            manager.DebugClearArea01Stage1();
        }

        EditorGUILayout.Space(5);
        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Clear Area_01 Stage 3 (Unlock Area_02)", GUILayout.Height(30)))
        {
            manager.DebugClearArea01Stage3();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Unclear Area_01 Stage 3 (Lock Area_02)", GUILayout.Height(30)))
        {
            manager.DebugUnclearArea01Stage3();
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Debug: Area Stage 3 Clear/Unclear", EditorStyles.boldLabel);

        // 一括 Unclear ボタン
        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("Unclear All Areas (01-09) Stage 3", GUILayout.Height(35)))
        {
            for (int i = 1; i <= 9; i++)
            {
                string areaId = $"Area_{i:D2}";
                manager.UnclearAreaStage(areaId, 3);
            }
            Debug.Log("[StageButtonManagerEditor] Uncleared all areas (01-09) Stage 3");
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(5);

        // Area_01 の Stage 3 をクリア/クリア解除
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Clear Area_01 Stage 3 (Unlock Area_02)"))
        {
            manager.ClearAreaStage("Area_01", 3);
        }
        if (GUILayout.Button("Unclear Area_01 Stage 3 (Lock Area_02)"))
        {
            manager.UnclearAreaStage("Area_01", 3);
        }
        EditorGUILayout.EndHorizontal();

        // Area_02 から Area_09 まで各エリアの Stage 3 をクリア/クリア解除
        for (int i = 2; i <= 9; i++)
        {
            string areaId = $"Area_{i:D2}";
            string nextAreaId = i < 9 ? $"Area_{(i + 1):D2}" : "none";
            string unlockText = i < 9 ? $" (Unlock {nextAreaId})" : "";

            EditorGUILayout.BeginHorizontal();

            if (GUILayout.Button($"Clear {areaId} Stage 3{unlockText}"))
            {
                manager.ClearAreaStage(areaId, 3);
            }

            if (GUILayout.Button($"Unclear {areaId} Stage 3"))
            {
                manager.UnclearAreaStage(areaId, 3);
            }

            EditorGUILayout.EndHorizontal();
        }
    }
}
