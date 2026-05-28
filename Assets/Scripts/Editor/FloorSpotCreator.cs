using UnityEngine;
using UnityEditor;
using System.IO;

public static class FloorSpotCreator
{
    [MenuItem("Tools/Create Floor Spot Sprite")]
    public static void CreateFloorSpotSprite()
    {
        const int width  = 256;
        const int height = 128; // 2:1 楕円（床面の遠近感）
        const string savePath = "Assets/Art/Background/Spotlight_FloorSpot.png";

        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[width * height];

        // EyeBeamと統一したゴールデン/アンバーカラー
        Color coreColor  = new Color(1.00f, 0.97f, 0.75f); // 明るい黄白（中心）
        Color goldColor  = new Color(1.00f, 0.70f, 0.12f); // ウォームゴールド
        Color amberColor = new Color(0.85f, 0.38f, 0.00f); // ディープアンバー（端）

        float cx = (width  - 1) * 0.5f;
        float cy = (height - 1) * 0.5f;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                // 楕円正規化距離（0=中心, 1=楕円端, >1=外側）
                float dx = (x - cx) / cx;
                float dy = (y - cy) / cy;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                if (dist >= 1f)
                {
                    pixels[y * width + x] = new Color(0f, 0f, 0f, 0f);
                    continue;
                }

                // フォールオフ（dist=0.25から端まで滑らかに減衰）
                float edgeT = Mathf.Clamp01((dist - 0.25f) / 0.75f);
                float alpha = 1f - edgeT * edgeT * (3f - 2f * edgeT);

                // 中心の輝点を強調
                float coreGlow = Mathf.Exp(-dist * dist * 5f) * 0.5f;
                alpha = Mathf.Min(1f, alpha + coreGlow);

                // カラーグラデーション（中心→端）
                float t = Mathf.Clamp01(dist);
                Color spotColor = t < 0.4f
                    ? Color.Lerp(coreColor, goldColor,  t / 0.4f)
                    : Color.Lerp(goldColor, amberColor, (t - 0.4f) / 0.6f);

                pixels[y * width + x] = new Color(spotColor.r, spotColor.g, spotColor.b, alpha);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();

        string fullPath = Path.Combine(Application.dataPath,
            savePath.Substring("Assets/".Length));
        File.WriteAllBytes(fullPath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.Refresh();

        TextureImporter importer = AssetImporter.GetAtPath(savePath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType        = TextureImporterType.Sprite;
            importer.spriteImportMode   = SpriteImportMode.Single;
            importer.spritePivot        = new Vector2(0.5f, 0.5f); // 中央ピボット
            importer.mipmapEnabled      = false;
            importer.alphaIsTransparency = true;
            importer.filterMode         = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        Debug.Log("Floor Spot Sprite 作成完了: " + savePath);
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<Texture2D>(savePath);
    }
}
