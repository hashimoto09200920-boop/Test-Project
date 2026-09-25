using UnityEditor;
using UnityEngine;

/// <summary>
/// ★一時的な修復ツール（実行後は削除してよい）。保留中のアセット再インポートを
/// Play前に強制的に完了させ、Play中にインポート処理が割り込んでPlayが強制終了する
/// 事象を防ぐ。
/// </summary>
public static class FlushAssetImportsTool
{
    [MenuItem("Tools/★保留中のアセットインポートを今すぐ完了させる")]
    public static void Flush()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log("[FlushAssetImportsTool] 完了。保留中のインポートを同期的に処理しました。");
    }
}
