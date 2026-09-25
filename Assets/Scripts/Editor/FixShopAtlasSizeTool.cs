using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;

/// <summary>
/// ★一時的な修復ツール（原因特定でき次第削除）。ShopIconAtlasのMax Texture Sizeを
/// 切り替えて検証するためのツール。
/// </summary>
public static class FixShopAtlasSizeTool
{
    private const string AtlasPath = "Assets/Art/AreaSelect/Shop/ShopIconAtlas.spriteatlas";

    [MenuItem("Tools/★ShopIconAtlasのMaxTextureSizeを2048に戻す")]
    public static void RevertTo2048() => SetMaxSize(2048);

    [MenuItem("Tools/★ShopIconAtlasのMaxTextureSizeを4096にする")]
    public static void SetTo4096() => SetMaxSize(4096);

    private static void SetMaxSize(int size)
    {
        var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AtlasPath);
        if (atlas == null)
        {
            Debug.LogError($"[FixShopAtlasSizeTool] {AtlasPath} が見つかりません。");
            return;
        }

        var so = new SerializedObject(atlas);
        var prop = so.FindProperty("m_EditorData.textureSettings.maxTextureSize");
        if (prop == null)
        {
            Debug.LogError("[FixShopAtlasSizeTool] maxTextureSizeプロパティが見つかりません。");
            return;
        }

        Debug.Log($"[FixShopAtlasSizeTool] maxTextureSize: {prop.intValue} -> {size}");
        prop.intValue = size;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(atlas);
        AssetDatabase.SaveAssets();
        Debug.Log("[FixShopAtlasSizeTool] 完了。Inspectorで「Pack Preview」を押して反映してください。");
    }
}
