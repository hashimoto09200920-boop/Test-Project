using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;
using System.IO;

/// <summary>
/// ★一時的な修復ツール（実行後は削除してよい）。SkillHUDCardUIのタイル形状を、実行時に
/// コード生成していたSpriteから、SkillIconAtlasに含まれる実ファイルのSpriteへ差し替える。
/// これによりカード内で「アイコン(Atlas内)→タイル(コード生成、Atlas外)」という
/// テクスチャ切り替えが1回無くなる（アイコンとタイルが完全に同じAtlasテクスチャになる）。
/// SkillHUDCardUI.CreateParallelogramSprite()と全く同じアルゴリズムでPNGを生成するため、
/// 見た目は既存のタイルと完全に一致する。
/// </summary>
public static class BakeTileShapeIntoSkillAtlasTool
{
    private const string TileShapePngPath = "Assets/Art/Skill/TileShape.png";
    private static readonly string[] TargetScenePaths =
    {
        "Assets/Scenes/03_AreaSelect.unity",
        "Assets/Scenes/02_Gem.unity",
        "Assets/Scenes/04_Drink.unity",
        "Assets/Scenes/05_Game.unity",
    };

    [MenuItem("Tools/★スキルタイル形状をSkillIconAtlasに焼き込む(1.PNG生成)")]
    public static void GenerateTileShapePng()
    {
        // ★SkillHUDCardUI.CreateParallelogramSprite()と完全に同じアルゴリズム
        int size = 32;
        float skew = 0.3f;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float normalizedY = y / (float)size;
                float skewOffset = normalizedY * skew * size;

                float left = skewOffset;
                float right = size - (size * skew - skewOffset);

                Color color = (x >= left && x < right) ? Color.white : Color.clear;
                texture.SetPixel(x, y, color);
            }
        }
        texture.Apply();

        byte[] png = texture.EncodeToPNG();
        Object.DestroyImmediate(texture);

        string fullPath = Path.Combine(Application.dataPath, "Art/Skill/TileShape.png");
        File.WriteAllBytes(fullPath, png);

        AssetDatabase.ImportAsset(TileShapePngPath, ImportAssetOptions.ForceUpdate);

        // 他のスキルアイコンと同じSprite Import設定に揃える
        TextureImporter importer = AssetImporter.GetAtPath(TileShapePngPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePivot = new Vector2(0.5f, 0.5f);
            importer.spritePixelsPerUnit = 100f;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 256;
            importer.SaveAndReimport();
        }

        Debug.Log($"[BakeTileShapeIntoSkillAtlasTool] {TileShapePngPath} を生成しました。次に「2.SkillIconAtlasを再パック」→「3.全SkillHUDCardUIに配線」の順で実行してください。");
    }

    [MenuItem("Tools/★スキルタイル形状をSkillIconAtlasに焼き込む(2.SkillIconAtlasを再パック)")]
    public static void RepackAtlas()
    {
        var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>("Assets/Art/Skill/SkillIconAtlas.spriteatlas");
        if (atlas == null)
        {
            Debug.LogError("[BakeTileShapeIntoSkillAtlasTool] SkillIconAtlasが見つかりません。");
            return;
        }

        SpriteAtlasUtility.PackAtlases(new[] { atlas }, EditorUserBuildSettings.activeBuildTarget);
        Debug.Log("[BakeTileShapeIntoSkillAtlasTool] SkillIconAtlasを再パックしました。");
    }

    [MenuItem("Tools/★スキルタイル形状をSkillIconAtlasに焼き込む(3.全SkillHUDCardUIに配線)")]
    public static void WireUpAllCards()
    {
        Sprite tileSprite = AssetDatabase.LoadAssetAtPath<Sprite>(TileShapePngPath);
        if (tileSprite == null)
        {
            Debug.LogError($"[BakeTileShapeIntoSkillAtlasTool] {TileShapePngPath} がSpriteとして読み込めません。先に手順1を実行してください。");
            return;
        }

        var cardType = System.Type.GetType("SkillHUDCardUI, Assembly-CSharp");
        if (cardType == null)
        {
            Debug.LogError("[BakeTileShapeIntoSkillAtlasTool] SkillHUDCardUI型が見つかりません。");
            return;
        }

        foreach (string scenePath in TargetScenePaths)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            int count = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var comp in root.GetComponentsInChildren(cardType, true))
                {
                    var so = new SerializedObject(comp);
                    var prop = so.FindProperty("tileShapeSpriteAsset");
                    if (prop == null) continue;
                    prop.objectReferenceValue = tileSprite;
                    so.ApplyModifiedProperties();
                    count++;
                }
            }

            if (count > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            Debug.Log($"[BakeTileShapeIntoSkillAtlasTool] {scenePath}: {count}件のSkillHUDCardUIにtileShapeSpriteAssetを設定しました。");
        }

        Debug.Log("[BakeTileShapeIntoSkillAtlasTool] 完了。");
    }
}
