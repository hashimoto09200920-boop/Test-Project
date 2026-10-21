using UnityEditor;
using UnityEngine;

/// <summary>
/// Assets/Audio/BGM の全BGMの取り込み設定（Default）の音質（Vorbis Quality）を一括で下げる（2026/10/20）。
/// Google Play のアプリ本体（base）の上限200MBに対し、BGM 32曲が音質100%で約128MBあったため。
/// 音質を TargetQuality と違うものだけを変える（再実行しても同じ結果。値を変えたい時は TargetQuality を直して再実行）。
/// 圧縮形式・Load Type などほかの設定は変えない。
/// </summary>
public static class BgmCompressionQualityTool
{
    private const string BgmFolder = "Assets/Audio/BGM";
    private const float TargetQuality = 1.0f; // 100%（2026/10/20 60%→80%→100%に戻した。80%と100%の差は約18MBしかなく、Split Application Binaryでbaseは200MB未満のため音質を優先）

    [MenuItem("Tools/ビルド/BGMの音質を100%に戻す")]
    private static void Apply()
    {
        string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] { BgmFolder });
        var targets = new System.Collections.Generic.List<AudioImporter>();
        foreach (string g in guids)
        {
            var imp = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(g)) as AudioImporter;
            if (imp == null) continue;
            var s = imp.defaultSampleSettings;
            if (s.compressionFormat != AudioCompressionFormat.Vorbis) continue;
            if (Mathf.Approximately(s.quality, TargetQuality)) continue;
            targets.Add(imp);
        }
        if (targets.Count == 0)
        {
            EditorUtility.DisplayDialog("BGMの音質", $"変更が必要なBGMはありません（すべて{TargetQuality * 100:0}%です）。", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("BGMの音質",
                $"{BgmFolder} のBGM {targets.Count}曲の音質（Quality）を {TargetQuality * 100:0}% にします。\n取り込み直しに数分かかります。\n（一覧はConsoleに出力します）\n続けますか？",
                "変更する", "キャンセル"))
            return;

        var list = new System.Text.StringBuilder();
        try
        {
            AssetDatabase.StartAssetEditing();
            for (int i = 0; i < targets.Count; i++)
            {
                var imp = targets[i];
                EditorUtility.DisplayProgressBar("BGMの音質", imp.assetPath, (float)i / targets.Count);
                var s = imp.defaultSampleSettings;
                list.AppendLine($"{imp.assetPath}：{s.quality * 100:0}% → {TargetQuality * 100:0}%");
                s.quality = TargetQuality;
                imp.defaultSampleSettings = s;
                imp.SaveAndReimport();
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            EditorUtility.ClearProgressBar();
        }
        Debug.Log($"[BgmCompressionQualityTool] BGM {targets.Count}曲の音質を{TargetQuality * 100:0}%にしました\n{list}");
        EditorUtility.DisplayDialog("BGMの音質", $"{targets.Count}曲を変更しました。", "OK");
    }
}
