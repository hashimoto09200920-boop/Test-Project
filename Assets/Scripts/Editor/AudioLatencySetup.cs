using UnityEditor;
using UnityEngine;

/// <summary>
/// Project Settings > Audio の「DSP Buffer Size」を「Best latency（一番遅れが少ない）」にする（2026/10/25・ユーザー指示）。
/// スマホで音を鳴らす命令から実際に音が出るまでの遅れを小さくするため。
/// コンパイル後に1回だけ自動で設定する（その後は Project Settings > Audio で自由に変えられる）。エディタ専用でゲームには含まれない。
/// </summary>
public static class AudioLatencySetup
{
    private const string DoneKey = "NeonDancer.AudioLatencySetup.BestLatency.Done";
    private const int BestLatency = 256;

    [InitializeOnLoadMethod]
    private static void ApplyOnce()
    {
        if (EditorPrefs.GetBool(DoneKey, false)) return;
        EditorApplication.delayCall += () =>
        {
            if (EditorPrefs.GetBool(DoneKey, false)) return;
            if (Apply()) EditorPrefs.SetBool(DoneKey, true);
        };
    }

    [MenuItem("Tools/サウンド/DSP Buffer Size を Best latency にする")]
    private static void ApplyFromMenu() => Apply();

    private static bool Apply()
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/AudioManager.asset");
        if (assets == null || assets.Length == 0) { Debug.LogWarning("[AudioLatencySetup] AudioManager の設定が見つかりません"); return false; }

        var so = new SerializedObject(assets[0]);
        var requested = so.FindProperty("m_RequestedDSPBufferSize");
        if (requested == null) { Debug.LogWarning("[AudioLatencySetup] DSP Buffer Size の項目が見つかりません"); return false; }

        if (requested.intValue != BestLatency)
        {
            int before = requested.intValue;
            requested.intValue = BestLatency;
            so.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log($"[AudioLatencySetup] Project Settings > Audio の DSP Buffer Size を {before}（0=Default）→ {BestLatency}（Best latency）に変更しました");
        }
        else Debug.Log("[AudioLatencySetup] DSP Buffer Size は既に Best latency です");
        return true;
    }
}
