using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// Multiple で絵が離れた部品ごとに分けて切り出された画像（例：アクセラレーターの3つの矢印ときらめきが4つに分かれた）を、
/// 全部を囲む1枚の切り出しにまとめる（2026/11/11）。
/// ★Unity 6 では TextureImporter.spritesheet を書き換えても保存されないので、Sprite Editor のデータ（ISpriteEditorDataProvider）で書き換える。
/// 1枚目の切り出しのID（spriteID）を残すので、すでに1枚目を参照しているシーン・Prefabは、そのまま全体の絵に変わる
/// </summary>
[InitializeOnLoad]
public static class SpriteSliceMergeTool
{
    static SpriteSliceMergeTool()
    {
        ItemBuffBase.MergeSlicesHook = Merge;
    }

    /// <summary>まとめたらtrue（もともと1枚・Multipleでない時はfalse）</summary>
    public static bool Merge(string path)
    {
        var imp = AssetImporter.GetAtPath(path) as TextureImporter;
        if (imp == null || imp.spriteImportMode != SpriteImportMode.Multiple) return false;

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var dp = factory.GetSpriteEditorDataProviderFromObject(imp);
        if (dp == null) { Debug.LogError("[SpriteSliceMerge] " + path + "：Sprite Editor のデータが読めません"); return false; }
        dp.InitSpriteEditorDataProvider();

        var rects = dp.GetSpriteRects();
        if (rects == null || rects.Length <= 1) return false;

        Rect r = rects[0].rect;
        for (int i = 1; i < rects.Length; i++)
            r = Rect.MinMaxRect(Mathf.Min(r.xMin, rects[i].rect.xMin), Mathf.Min(r.yMin, rects[i].rect.yMin),
                                Mathf.Max(r.xMax, rects[i].rect.xMax), Mathf.Max(r.yMax, rects[i].rect.yMax));

        var one = rects[0];
        one.rect = r;
        one.alignment = SpriteAlignment.Center;
        one.pivot = new Vector2(0.5f, 0.5f);
        dp.SetSpriteRects(new[] { one });

        // 名前とIDの対応表も1枚分にする（残っていると消した切り出しの名前が残る）
        var names = dp.GetDataProvider<ISpriteNameFileIdDataProvider>();
        if (names != null) names.SetNameFileIdPairs(new[] { new SpriteNameFileIdPair(one.name, one.spriteID) });

        dp.Apply();
        imp.SaveAndReimport();
        Debug.Log($"[SpriteSliceMerge] {path}：{rects.Length} 個に分かれていた切り出しを、全部を囲む1枚（{r.width}×{r.height}・名前 {one.name}）にまとめました");
        return true;
    }

    [MenuItem("Tools/アイテム/アクセラレーターの画像の切り出しを1枚にまとめる")]
    private static void MergeAccelerator()
    {
        bool done = Merge(GoldBoltBuff.BoltSpritePath);
        string msg = done ? "1枚にまとめました。" : "まとめる必要はありませんでした（すでに1枚です）。";
        EditorUtility.DisplayDialog("アクセラレーターの画像", msg + "\n切り出しの数は Sprite Editor で確認できます。", "OK");
    }
}
