using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools > Enable Read-Write for Bullet Sprites
/// 全EnemyDataのBulletType.spriteOverrideテクスチャにRead/Writeを一括有効化する。
/// FirePixelVFXがランタイムでスプライトの色を取得するために必要。一度実行すれば以後不要。
/// </summary>
public static class EnableBulletSpriteReadWrite
{
    [MenuItem("Tools/Enable Read-Write for Bullet Sprites")]
    static void Enable()
    {
        string[] guids = AssetDatabase.FindAssets("t:EnemyData");
        int enabled = 0;
        int already = 0;

        foreach (string guid in guids)
        {
            string dataPath = AssetDatabase.GUIDToAssetPath(guid);
            EnemyData data = AssetDatabase.LoadAssetAtPath<EnemyData>(dataPath);
            if (data == null || data.bulletTypes == null) continue;

            foreach (var bt in data.bulletTypes)
            {
                if (bt?.spriteOverride == null) continue;

                string texPath = AssetDatabase.GetAssetPath(bt.spriteOverride.texture);
                if (string.IsNullOrEmpty(texPath)) continue;

                var importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
                if (importer == null) continue;

                if (importer.isReadable)
                {
                    already++;
                }
                else
                {
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                    enabled++;
                }
            }
        }

        EditorUtility.DisplayDialog(
            "Bullet Sprites - Read/Write 有効化完了",
            $"新たに有効化: {enabled} 件\nすでに有効: {already} 件",
            "OK"
        );
    }
}
