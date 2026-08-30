using UnityEngine;
using UnityEditor;

/// <summary>
/// Stage09_BlockA〜Dが、既存(Area01〜08)のブロック画像と異なるインポート設定で
/// 取り込まれていたのを、既存と同じ設定に揃える。
/// ・spritePixelsToUnits: 100 → 120（既存はすべて120。100のままだと約20%大きく表示される）
/// ・spriteMode: Multiple → Single（既存はすべてSingle。Multipleのままだと自動スライスにより
///   プロンプトの「周囲のダスト粒子」が別の小さなスプライトとして誤認識され、6個の余計な
///   欠片スプライトが切り出されてしまっていた）
/// </summary>
public static class Stage09BlockImportFixMigrator
{
    private static readonly string[] TargetPaths =
    {
        "Assets/Art/Walls/Stage09_BlockA.png",
        "Assets/Art/Walls/Stage09_BlockB.png",
        "Assets/Art/Walls/Stage09_BlockC.png",
        "Assets/Art/Walls/Stage09_BlockD.png",
    };

    [MenuItem("Tools/Area09/Fix Stage09 Block Import Settings")]
    public static void Run()
    {
        foreach (var path in TargetPaths)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[Stage09BlockImportFixMigrator] TextureImporterが見つかりません: {path}");
                continue;
            }

            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 120f;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            Debug.Log($"[Stage09BlockImportFixMigrator] {path}: spriteMode=Single, PPU=120 に修正しました。");
        }
    }
}
