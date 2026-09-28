using UnityEditor;
using UnityEngine;
using UnityEngine.U2D;

/// <summary>
/// エネミー弾の負荷軽減②：Assets/Art/EnemyBullet（全エネミー共通で使われる14種の弾スプライト）を
/// 1枚のSprite Atlasにまとめる。同時に多数の弾が画面上に出る際、別々のテクスチャによる描画バッチの
/// 分裂を防ぐのが目的。
/// ★SpriteAtlasExtensions等のEditor専用APIが本プロジェクトの環境で解決できなかったため、
///   CreateShopIconAtlasToolと同じくSerializedObjectで直接シリアライズ済みフィールドを操作する。
/// ★ShopIconAtlasで実際に発生したタイトパッキング起因のアイコン滲みを踏まえ、
///   最初からTight Packing/Rotationを無効にして作成する。
/// </summary>
public static class CreateEnemyBulletAtlasTool
{
    private const string AtlasPath = "Assets/Art/EnemyBullet/EnemyBulletAtlas.spriteatlas";
    private const string BulletFolder = "Assets/Art/EnemyBullet";

    [MenuItem("Tools/★エネミー弾のSprite Atlasを作成")]
    public static void CreateAtlas()
    {
        if (AssetDatabase.LoadAssetAtPath<SpriteAtlas>(AtlasPath) != null)
        {
            Debug.LogWarning($"[CreateEnemyBulletAtlasTool] {AtlasPath} は既に存在します。");
            return;
        }

        var folderAsset = AssetDatabase.LoadAssetAtPath<DefaultAsset>(BulletFolder);
        if (folderAsset == null)
        {
            Debug.LogError($"[CreateEnemyBulletAtlasTool] {BulletFolder} フォルダが見つかりません。");
            return;
        }

        var atlas = new SpriteAtlas();
        AssetDatabase.CreateAsset(atlas, AtlasPath);

        var so = new SerializedObject(atlas);

        var packables = so.FindProperty("m_EditorData.packables");
        packables.arraySize = 1;
        packables.GetArrayElementAtIndex(0).objectReferenceValue = folderAsset;

        // ★ShopIconAtlasの滲み不具合の原因になった設定を、最初からOFFにしておく
        so.FindProperty("m_EditorData.packingSettings.enableRotation").boolValue = false;
        so.FindProperty("m_EditorData.packingSettings.enableTightPacking").boolValue = false;

        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(atlas);
        AssetDatabase.SaveAssets();

        Debug.Log($"[CreateEnemyBulletAtlasTool] {AtlasPath} を作成し、{BulletFolder} 配下のスプライトを登録しました。Inspectorで内容を確認し、必要なら「Pack Preview」を押してください。");
    }
}
