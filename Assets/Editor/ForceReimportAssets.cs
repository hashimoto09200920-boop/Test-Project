using UnityEditor;
using UnityEngine;

/// <summary>
/// アセットの強制再インポート用エディタツール
/// </summary>
public class ForceReimportAssets : MonoBehaviour
{
    [MenuItem("Tools/Force Reimport AreaDef Assets")]
    public static void ReimportAreaDefAssets()
    {
        Debug.Log("[ForceReimportAssets] Starting reimport of AreaDef assets...");

        // AreaDef_Area_01.asset を強制再インポート
        string area01Path = "Assets/Resources/GameData/AreaDef_Area_01.asset";
        AssetDatabase.ImportAsset(area01Path, ImportAssetOptions.ForceUpdate);
        Debug.Log($"[ForceReimportAssets] Reimported: {area01Path}");

        // AreaDef_Area_02.asset を強制再インポート
        string area02Path = "Assets/Resources/GameData/AreaDef_Area_02.asset";
        AssetDatabase.ImportAsset(area02Path, ImportAssetOptions.ForceUpdate);
        Debug.Log($"[ForceReimportAssets] Reimported: {area02Path}");

        // AreaCatalog.asset も念のため再インポート
        string catalogPath = "Assets/Resources/GameData/AreaCatalog.asset";
        AssetDatabase.ImportAsset(catalogPath, ImportAssetOptions.ForceUpdate);
        Debug.Log($"[ForceReimportAssets] Reimported: {catalogPath}");

        // アセットデータベース全体をリフレッシュ
        AssetDatabase.Refresh();
        Debug.Log("[ForceReimportAssets] AssetDatabase.Refresh() completed.");

        Debug.Log("[ForceReimportAssets] Reimport完了。AreaDBProbeで確認してください。");
    }
}
