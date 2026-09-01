using UnityEditor;
using UnityEngine;

/// <summary>
/// 「新ネオン枠.png」に9-slice境界(spriteBorder)を設定する。
/// これまで境界が未設定(0,0,0,0)だったため、Image.Type.Slicedで使っても実質Simpleと同じく
/// 画像全体が単純に引き伸ばされるだけで、ボタンサイズ(160x60)より大きいジェムカード(366x315)に
/// 使うと管の太さまで比例して太くなってしまっていた。境界を設定することで、角の丸み・管の太さは
/// 一定のまま、直線部分だけが伸縮するようになる。
/// </summary>
public static class NeonFrameBorderFixMigrator
{
    private const string TargetPath = "Assets/Art/AreaSelect/Shop/新ネオン枠.png";

    [MenuItem("Tools/AreaSelect/Fix Neon Frame Sprite Border")]
    public static void Run()
    {
        var importer = AssetImporter.GetAtPath(TargetPath) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError($"[NeonFrameBorderFixMigrator] TextureImporterが見つかりません: {TargetPath}");
            return;
        }

        // 500x500の画像に対し、角の丸み+管の太さを保持する境界として150pxを設定(left,bottom,right,top)
        importer.spriteBorder = new Vector4(150, 150, 150, 150);
        EditorUtility.SetDirty(importer);
        importer.SaveAndReimport();
        Debug.Log("[NeonFrameBorderFixMigrator] 新ネオン枠.pngにspriteBorder(150,150,150,150)を設定しました。");
    }
}
