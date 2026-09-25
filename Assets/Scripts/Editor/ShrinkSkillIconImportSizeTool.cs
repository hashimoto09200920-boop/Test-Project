using UnityEditor;
using UnityEditor.U2D;
using UnityEngine;
using UnityEngine.U2D;

/// <summary>
/// ★一時的な修復ツール（原因特定でき次第削除）。Assets/Art/Skill配下のアイコン画像は
/// 実際には40〜80px程度でしか表示されないのに、元画像は500〜1024pxと過大なため、
/// SkillIconAtlasが2048x2048に収まりきらず2ページに分裂していた。
/// 元のPNGファイルはそのままに、TextureImporterのMax Sizeだけを下げてAtlasを1枚に収める。
/// </summary>
public static class ShrinkSkillIconImportSizeTool
{
    private const string SkillFolder = "Assets/Art/Skill";
    private const int NewMaxSize = 256;

    [MenuItem("Tools/★SkillアイコンのMaxSizeを256に下げる")]
    public static void ShrinkMaxSize()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { SkillFolder });
        int changed = 0;
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;
            if (importer.maxTextureSize == NewMaxSize) continue;

            importer.maxTextureSize = NewMaxSize;
            importer.SaveAndReimport();
            Debug.Log($"[ShrinkSkillIconImportSizeTool] {path}: maxTextureSize -> {NewMaxSize}");
            changed++;
        }

        Debug.Log($"[ShrinkSkillIconImportSizeTool] 完了。{changed}件のMax Sizeを{NewMaxSize}に変更しました。");

        var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>("Assets/Art/Skill/SkillIconAtlas.spriteatlas");
        if (atlas != null)
            Debug.Log("[ShrinkSkillIconImportSizeTool] SkillIconAtlasを選択してInspectorで「Pack Preview」を押し、1ページに収まったか確認してください。");
    }
}
