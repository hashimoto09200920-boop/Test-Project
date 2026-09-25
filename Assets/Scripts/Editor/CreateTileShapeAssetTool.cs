using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// ★一時的な調査用ツール（原因特定でき次第削除）。SkillHUDCardUI.CreateParallelogramSprite()と
/// 全く同じ形状(平行四辺形、skew=0.3)のPNGアセットをAssets/Art/Skill/に書き出す。
/// アイコンと同じフォルダに置くことでSkillIconAtlasへ自動的に含める狙い。
/// </summary>
public static class CreateTileShapeAssetTool
{
    private const string OutputPath = "Assets/Art/Skill/TileShape.png";

    [MenuItem("Tools/★タイル形状PNGをAssets/Art/Skillに書き出す")]
    public static void CreateTileShapeAsset()
    {
        int size = 32;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float skew = 0.3f;

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

        File.WriteAllBytes(OutputPath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(OutputPath, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(OutputPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();

        Debug.Log($"[CreateTileShapeAssetTool] {OutputPath} を作成しました。SkillIconAtlasのPackablesに'Skill'フォルダが含まれていれば自動的にAtlasへ入ります。");
    }

    [MenuItem("Tools/★02_Gemの全カードにタイル形状Spriteを割り当てる")]
    public static void AssignTileShapeToGemCards() => AssignTileShapeToCards("Assets/Scenes/02_Gem.unity");

    [MenuItem("Tools/★03_AreaSelectの全カードにタイル形状Spriteを割り当てる")]
    public static void AssignTileShapeToAreaSelectCards() => AssignTileShapeToCards("Assets/Scenes/03_AreaSelect.unity");

    private static void AssignTileShapeToCards(string scenePath)
    {
        var tileSprite = AssetDatabase.LoadAssetAtPath<Sprite>(OutputPath);
        if (tileSprite == null)
        {
            Debug.LogError($"[CreateTileShapeAssetTool] {OutputPath} が見つかりません。先に「★タイル形状PNGをAssets/Art/Skillに書き出す」を実行してください。");
            return;
        }

        var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        var cardType = System.Type.GetType("SkillHUDCardUI, Assembly-CSharp");
        if (cardType == null)
        {
            Debug.LogError("[CreateTileShapeAssetTool] SkillHUDCardUI型が見つかりません。");
            return;
        }

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

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[CreateTileShapeAssetTool] {scenePath}: {count}件のSkillHUDCardUIにtileShapeSpriteAssetを設定し、シーンを保存しました。");
    }
}
