using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;

/// <summary>
/// ★一時的な調査用ツール（原因特定でき次第削除）。ドリンク画面のアイコン類(Assets/Art/AreaSelect/Shop)を
/// 1枚のSprite Atlasにまとめる。スキルアイコンと同じ理屈で、別々のテクスチャが同時に大量表示される
/// ことによる描画バッチの分裂→実機チラつきの原因になっている疑いを検証する。
/// ★SpriteAtlasExtensions等のEditor専用APIが本プロジェクトの環境で解決できなかったため、
///   SerializedObjectで直接シリアライズ済みフィールドを操作する（型に依存しない安全な方法）。
/// </summary>
public static class CreateShopIconAtlasTool
{
    private const string AtlasPath = "Assets/Art/AreaSelect/Shop/ShopIconAtlas.spriteatlas";
    private const string ShopFolder = "Assets/Art/AreaSelect/Shop";

    [MenuItem("Tools/★ドリンクアイコンのSprite Atlasを作成")]
    public static void CreateAtlas()
    {
        if (AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AtlasPath) != null)
        {
            Debug.LogWarning($"[CreateShopIconAtlasTool] {AtlasPath} は既に存在します。");
            return;
        }

        var folderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(ShopFolder);
        if (folderAsset == null)
        {
            Debug.LogError($"[CreateShopIconAtlasTool] {ShopFolder} フォルダが見つかりません。");
            return;
        }

        var atlas = new SpriteAtlas();
        AssetDatabase.CreateAsset(atlas, AtlasPath);

        var so = new SerializedObject(atlas);
        var packables = so.FindProperty("m_EditorData.packables");
        packables.arraySize = 1;
        packables.GetArrayElementAtIndex(0).objectReferenceValue = folderAsset;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(atlas);
        AssetDatabase.SaveAssets();

        Debug.Log($"[CreateShopIconAtlasTool] {AtlasPath} を作成し、{ShopFolder} 配下のスプライトを登録しました。Inspectorで内容を確認し、必要なら「Pack Preview」を押してください。");
    }

    private const string GoldSpritePath = "Assets/Art/Gold/gold.png";

    [MenuItem("Tools/★ShopIconAtlasにゴールドアイコンを追加する")]
    public static void AddGoldIconToAtlas()
    {
        var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AtlasPath);
        if (atlas == null)
        {
            Debug.LogError($"[CreateShopIconAtlasTool] {AtlasPath} が見つかりません。先にAtlasを作成してください。");
            return;
        }

        var goldAsset = AssetDatabase.LoadAssetAtPath<Object>(GoldSpritePath);
        if (goldAsset == null)
        {
            Debug.LogError($"[CreateShopIconAtlasTool] {GoldSpritePath} が見つかりません。");
            return;
        }

        var so = new SerializedObject(atlas);
        var packables = so.FindProperty("m_EditorData.packables");

        for (int i = 0; i < packables.arraySize; i++)
        {
            if (packables.GetArrayElementAtIndex(i).objectReferenceValue == goldAsset)
            {
                Debug.LogWarning("[CreateShopIconAtlasTool] 既に登録済みです。");
                return;
            }
        }

        packables.arraySize++;
        packables.GetArrayElementAtIndex(packables.arraySize - 1).objectReferenceValue = goldAsset;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(atlas);
        AssetDatabase.SaveAssets();

        Debug.Log($"[CreateShopIconAtlasTool] {GoldSpritePath} をShopIconAtlasに追加しました。Inspectorで「Pack Preview」を押して反映してください。");
    }
}
