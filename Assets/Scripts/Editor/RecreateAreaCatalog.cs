using UnityEngine;
using UnityEditor;
using Game.Progress;

public class RecreateAreaCatalog
{
    [MenuItem("Tools/Progress/Recreate AreaCatalog Asset")]
    public static void RecreateAsset()
    {
        string assetPath = "Assets/Resources/GameData/AreaCatalog.asset";

        // 既存のアセットを削除
        if (AssetDatabase.LoadAssetAtPath<AreaCatalog>(assetPath) != null)
        {
            AssetDatabase.DeleteAsset(assetPath);
            Debug.Log($"[RecreateAreaCatalog] Deleted existing asset at {assetPath}");
        }

        // 新しい AreaCatalog を作成
        var catalog = ScriptableObject.CreateInstance<AreaCatalog>();

        // Area_01 から Area_09 まで自動的に読み込んで追加
        for (int i = 1; i <= 9; i++)
        {
            string areaId = $"Area_{i:D2}";
            string path = $"Assets/Resources/GameData/AreaDef_{areaId}.asset";
            var areaDef = AssetDatabase.LoadAssetAtPath<AreaDef>(path);

            if (areaDef != null)
            {
                catalog.areas.Add(areaDef);
                Debug.Log($"[RecreateAreaCatalog] Added AreaDef_{areaId}");
            }
            else
            {
                Debug.LogWarning($"[RecreateAreaCatalog] AreaDef_{areaId}.asset not found at {path}");
            }
        }

        // アセットとして保存
        AssetDatabase.CreateAsset(catalog, assetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[RecreateAreaCatalog] Created new AreaCatalog at {assetPath} with {catalog.areas.Count} areas");

        // 選択して Inspector に表示
        Selection.activeObject = catalog;
        EditorGUIUtility.PingObject(catalog);
    }
}
