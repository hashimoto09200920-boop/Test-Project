using UnityEngine;
using UnityEditor;
using Game.Progress;

/// <summary>
/// Area_03 から Area_09 までの AreaDef アセットを一括作成
/// </summary>
public class CreateAllAreaDefs
{
    [MenuItem("Tools/Progress/Create Area_03 to Area_09 Assets")]
    public static void CreateAreaDefs()
    {
        // AreaDef.cs の GUID を取得
        string areaDefScriptGuid = "046fac161021a254cab80bd27a9ec10c";

        // Area_03 から Area_09 まで作成
        for (int i = 3; i <= 9; i++)
        {
            string areaId = $"Area_{i:D2}";
            string fileName = $"AreaDef_{areaId}.asset";
            string assetPath = $"Assets/Resources/GameData/{fileName}";

            // 既に存在する場合はスキップ
            if (AssetDatabase.LoadAssetAtPath<AreaDef>(assetPath) != null)
            {
                Debug.Log($"[CreateAllAreaDefs] {fileName} already exists, skipping");
                continue;
            }

            // 前のエリアID（アンロック条件用）
            string previousAreaId = $"Area_{(i - 1):D2}";

            // YAML形式で直接作成
            string yamlContent = $@"%YAML 1.1
%TAG !u! tag:unity3d.com,2011:
--- !u!114 &11400000
MonoBehaviour:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: 0}}
  m_Enabled: 1
  m_EditorHideFlags: 0
  m_Script: {{fileID: 11500000, guid: {areaDefScriptGuid}, type: 3}}
  m_Name: AreaDef_{areaId}
  m_EditorClassIdentifier: Assembly-CSharp:Game.Progress:AreaDef
  areaId: {areaId}
  stages:
  - number: 1
    displayName: Stage 1
  - number: 2
    displayName: Stage 2
  - number: 3
    displayName: Stage 3
  unlockByStages:
  - areaId: {previousAreaId}
    stageNumber: 3
";

            // ファイルに書き込み
            string fullPath = System.IO.Path.GetFullPath(assetPath);
            System.IO.File.WriteAllText(fullPath, yamlContent);

            Debug.Log($"[CreateAllAreaDefs] Created {fileName}");
        }

        // アセットデータベースをリフレッシュ
        AssetDatabase.Refresh();

        Debug.Log("[CreateAllAreaDefs] All AreaDef assets created. Now updating AreaCatalog...");

        // AreaCatalog を自動更新
        RecreateAreaCatalog.RecreateAsset();
    }
}
