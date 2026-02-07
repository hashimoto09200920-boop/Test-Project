using UnityEngine;
using UnityEditor;
using Game.UI;

/// <summary>
/// StageButton の Inspector カスタマイズ
/// Context Menu の代わりにボタンを表示
/// </summary>
[CustomEditor(typeof(StageButton))]
public class StageButtonEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // 通常のInspectorを描画
        DrawDefaultInspector();

        StageButton button = (StageButton)target;

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Setup", EditorStyles.boldLabel);

        if (GUILayout.Button("Auto Setup (Find All)"))
        {
            // private メソッドなので Reflection で呼び出す
            var method = typeof(StageButton).GetMethod("AutoSetup",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (method != null)
            {
                method.Invoke(button, null);
            }
        }

        if (GUILayout.Button("Create Debug Text"))
        {
            var method = typeof(StageButton).GetMethod("CreateDebugText",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (method != null)
            {
                method.Invoke(button, null);
            }
        }

        if (GUILayout.Button("Find LockOverlay"))
        {
            var method = typeof(StageButton).GetMethod("FindLockOverlay",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (method != null)
            {
                method.Invoke(button, null);
            }
        }

        // Play モード中のみ有効なボタン
        EditorGUI.BeginDisabledGroup(!Application.isPlaying);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Runtime Controls (Play Mode Only)", EditorStyles.boldLabel);

        if (GUILayout.Button("Refresh Lock Status"))
        {
            button.RefreshLockStatus();
        }

        if (GUILayout.Button("Toggle Test Mode"))
        {
            var method = typeof(StageButton).GetMethod("ToggleTestMode",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (method != null)
            {
                method.Invoke(button, null);
            }
        }

        if (GUILayout.Button("Toggle Force Unlocked"))
        {
            var method = typeof(StageButton).GetMethod("ToggleForceUnlocked",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (method != null)
            {
                method.Invoke(button, null);
            }
        }

        EditorGUI.EndDisabledGroup();
    }
}
