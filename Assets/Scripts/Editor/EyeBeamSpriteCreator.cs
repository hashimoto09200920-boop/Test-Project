using UnityEngine;
using UnityEditor;
using System.IO;

public static class EyeBeamSpriteCreator
{
    [MenuItem("Tools/Create Eye Beam Sprite")]
    public static void CreateEyeBeamSprite()
    {
        const int width = 512;
        const int height = 512;
        const string savePath = "Assets/Art/Background/Spotlight_EyeBeam.png";

        // 28°: 円弧カットオフ範囲内で収まる角度
        float halfAngleTan = Mathf.Tan(28f * Mathf.Deg2Rad);

        Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[width * height];

        Color coreColor  = new Color(1.00f, 0.97f, 0.75f); // 明るい黄白（中心軸）
        Color goldColor  = new Color(1.00f, 0.70f, 0.12f); // ウォームゴールド
        Color amberColor = new Color(0.85f, 0.38f, 0.00f); // ディープアンバー（端）

        float cx = (width - 1) * 0.5f;

        // 円弧カットオフ：光源からこの半径で先端を切る（スポットライト形状の肝）
        float arcMaxRadius  = height * 0.88f; // 451px：完全に透過
        float arcFadeStart  = arcMaxRadius * 0.72f; // 325px：フェード開始

        for (int y = 0; y < height; y++)
        {
            // Unity Texture2D: y=0=下端, y=height-1=上端（光源）
            float depthPx = (height - 1) - y;

            for (int x = 0; x < width; x++)
            {
                float dxPx = x - cx;
                float distFromAxis;
                float alpha;

                if (depthPx < 3f)
                {
                    // 光源点の輝点
                    distFromAxis = Mathf.Abs(dxPx) / (cx * 0.04f);
                    float dt = Mathf.Clamp01(distFromAxis);
                    alpha = 1f - dt * dt * (3f - 2f * dt);
                }
                else
                {
                    float coneHalfWidthPx = depthPx * halfAngleTan;
                    distFromAxis = Mathf.Abs(dxPx) / coneHalfWidthPx;

                    // コーン外周フォールオフ（distFromAxis≥1.2で完全透過）
                    float edgeT = Mathf.Clamp01((distFromAxis - 0.6f) / 0.6f);
                    float coneAlpha = 1f - edgeT * edgeT * (3f - 2f * edgeT);

                    // 中心軸の明るい光線
                    float centerRay = Mathf.Exp(-distFromAxis * distFromAxis * 6f) * 0.6f;

                    alpha = Mathf.Clamp01(coneAlpha + centerRay);
                }

                // 光源からの直線距離で円弧カットオフ（スポットライトの先端形状）
                // これにより底が横一線ではなく扇形の弧になる
                float radialDist = Mathf.Sqrt(dxPx * dxPx + depthPx * depthPx);
                float arcT = Mathf.Clamp01((radialDist - arcFadeStart) / (arcMaxRadius - arcFadeStart));
                float arcFalloff = 1f - arcT * arcT * (3f - 2f * arcT);
                alpha *= arcFalloff;

                // 中心軸からの距離でカラーグラデーション
                float t = Mathf.Clamp01(distFromAxis);
                Color beamColor = t < 0.4f
                    ? Color.Lerp(coreColor, goldColor, t / 0.4f)
                    : Color.Lerp(goldColor, amberColor, (t - 0.4f) / 0.6f);

                pixels[y * width + x] = new Color(beamColor.r, beamColor.g, beamColor.b, alpha);
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
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePivot = new Vector2(0.5f, 1f); // ピボット=上端中央（目の位置）
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        Debug.Log("Eye Beam Sprite 作成完了 (512×512, Golden Amber, Arc Cutoff): " + savePath);
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<Texture2D>(savePath);
    }
}
