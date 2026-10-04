using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// NeonDancerのワームホール画像（外側の渦／内側の渦／中心の穴）をプログラム生成するための設定。
/// Inspectorで数値を変えて「画像を生成」を押すと、同じファイル名で作り直す（Prefabからの参照はそのまま）。
/// 画像は白〜透明の無彩色（色はSpriteRendererで9色に着色する）。中心の穴だけは黒。
/// </summary>
[CreateAssetMenu(menuName = "NeonDancer/Wormhole Texture Settings")]
public class NeonDancerWormholeTextureSettings : ScriptableObject
{
    [System.Serializable]
    public class SwirlParams
    {
        [Tooltip("画像サイズ（px、正方形）")]
        public int size = 512;
        [Tooltip("渦の腕の本数")]
        [Range(1, 12)] public int arms = 5;
        [Tooltip("巻きの強さ（大きいほど中心へ強く巻き込む）")]
        [Range(0f, 8f)] public float twist = 3f;
        [Tooltip("腕の細さ（大きいほど細くくっきり）")]
        [Range(0.5f, 12f)] public float armSharpness = 2.5f;
        [Tooltip("腕の中の細い筋の本数（腕1本あたり）")]
        [Range(0, 40)] public int streaksPerArm = 9;
        [Tooltip("筋のくっきり具合")]
        [Range(0.5f, 20f)] public float streakSharpness = 4f;
        [Tooltip("筋の強さ（0=筋なしのなめらかな腕、1=筋だけ）")]
        [Range(0f, 1f)] public float streakAmount = 0.6f;
        [Tooltip("筋の揺らぎ（ゆらゆら感）")]
        [Range(0f, 3f)] public float streakWobble = 0.8f;
        [Tooltip("中心の暗い部分の半径（0〜1、画像の半径に対する割合）")]
        [Range(0f, 0.6f)] public float innerRadius = 0.12f;
        [Tooltip("中心から明るくなるまでの幅")]
        [Range(0.01f, 0.5f)] public float innerSoftness = 0.12f;
        [Tooltip("外側へ向かって暗くなる強さ（大きいほど外周が早く消える）")]
        [Range(0.2f, 6f)] public float outerFalloff = 1.6f;
        [Tooltip("全体の明るさ")]
        [Range(0.1f, 4f)] public float gain = 1.6f;
        [Tooltip("散らばる星の数（参考画像の小さな光点）")]
        [Range(0, 400)] public int stars = 120;
        [Tooltip("星の明るさ")]
        [Range(0f, 1f)] public float starBrightness = 0.7f;
        [Tooltip("乱数の種（変えると筋の揺らぎ・星の配置が変わる）")]
        public int seed = 1;
    }

    [System.Serializable]
    public class VoidParams
    {
        public int size = 256;
        [Tooltip("黒い穴の半径（0〜1）")]
        [Range(0.1f, 1f)] public float radius = 0.7f;
        [Tooltip("縁のぼかし幅（0〜1）")]
        [Range(0.01f, 0.8f)] public float softness = 0.35f;
    }

    [Header("外側の渦（参考画像のような細い筋の大きな渦）")]
    public SwirlParams outer = new SwirlParams();

    [Header("内側の渦（逆回転で重ねる小さく巻きの強い渦）")]
    public SwirlParams inner = new SwirlParams { arms = 3, twist = 4.5f, armSharpness = 3f, streaksPerArm = 6, innerRadius = 0.18f, outerFalloff = 2.2f, gain = 1.4f, stars = 0, seed = 2 };

    [Header("中心の穴（黒。着色しない）")]
    public VoidParams voidHole = new VoidParams();

    public const string Folder    = "Assets/Art/Enemy/S3_10_NeonDancer";
    public const string OuterPath = Folder + "/ND_Wormhole_Outer.png";
    public const string InnerPath = Folder + "/ND_Wormhole_Inner.png";
    public const string VoidPath  = Folder + "/ND_Wormhole_Void.png";

    public void GenerateAll()
    {
        WritePng(OuterPath, GenerateSwirl(outer));
        WritePng(InnerPath, GenerateSwirl(inner));
        WritePng(VoidPath, GenerateVoid(voidHole));
        AssetDatabase.Refresh();
        Debug.Log($"[NeonDancerWormhole] 画像を生成しました：{OuterPath} / {InnerPath} / {VoidPath}");
    }

    // ======================================================
    // 渦：対数らせんの腕 × 腕の中の細い筋 × 中心〜外周の明るさの包絡線 ＋ 星
    // ======================================================
    private static Texture2D GenerateSwirl(SwirlParams p)
    {
        int n = Mathf.Max(32, p.size);
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var px = new Color[n * n];
        var rng = new System.Random(p.seed);
        float wobblePhase1 = (float)rng.NextDouble() * Mathf.PI * 2f;
        float wobblePhase2 = (float)rng.NextDouble() * Mathf.PI * 2f;

        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f;
                float v = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                if (r >= 1f) { px[y * n + x] = new Color(1f, 1f, 1f, 0f); continue; }

                float theta = Mathf.Atan2(v, u);
                // 対数らせん：中心へ向かうほど角度が進む（巻き込み）
                float phase = theta + p.twist * Mathf.Log(Mathf.Max(r, 0.0005f));

                float arm = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(phase * p.arms), p.armSharpness);

                float streak = 1f;
                if (p.streaksPerArm > 0)
                {
                    float wob = p.streakWobble * (Mathf.Sin(r * 11f + wobblePhase1) + 0.5f * Mathf.Sin(r * 23f + theta * 2f + wobblePhase2));
                    streak = Mathf.Pow(0.5f + 0.5f * Mathf.Cos(phase * p.arms * p.streaksPerArm + wob), p.streakSharpness);
                }
                float intensity = arm * Mathf.Lerp(1f, streak, p.streakAmount);

                // 中心は暗く（穴に吸い込まれる）、少し外で最も明るく、外周へ溶けて消える
                float innerRamp = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(p.innerRadius, p.innerRadius + p.innerSoftness, r));
                float outerFade = Mathf.Pow(Mathf.Clamp01(1f - r), p.outerFalloff);
                float a = Mathf.Clamp01(intensity * innerRamp * outerFade * p.gain * 1.6f);

                px[y * n + x] = new Color(1f, 1f, 1f, a);
            }
        }

        // 星（小さな光点）
        for (int i = 0; i < p.stars; i++)
        {
            float rr = Mathf.Lerp(p.innerRadius + 0.05f, 0.95f, (float)rng.NextDouble());
            float th = (float)rng.NextDouble() * Mathf.PI * 2f;
            int cx = Mathf.RoundToInt((Mathf.Cos(th) * rr * 0.5f + 0.5f) * n);
            int cy = Mathf.RoundToInt((Mathf.Sin(th) * rr * 0.5f + 0.5f) * n);
            float b = p.starBrightness * Mathf.Lerp(0.4f, 1f, (float)rng.NextDouble()) * Mathf.Pow(1f - rr, 0.5f);
            int rad = Mathf.Max(1, n / 256);
            for (int dy = -rad; dy <= rad; dy++)
                for (int dx = -rad; dx <= rad; dx++)
                {
                    int xx = cx + dx, yy = cy + dy;
                    if (xx < 0 || yy < 0 || xx >= n || yy >= n) continue;
                    float fall = 1f - Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / (rad + 0.5f));
                    var c = px[yy * n + xx];
                    c.a = Mathf.Clamp01(c.a + b * fall);
                    px[yy * n + xx] = c;
                }
        }

        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    private static Texture2D GenerateVoid(VoidParams p)
    {
        int n = Mathf.Max(32, p.size);
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        var px = new Color[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f;
                float v = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float a = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(p.radius - p.softness, p.radius, r));
                px[y * n + x] = new Color(0f, 0f, 0f, Mathf.Clamp01(a));
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    private static void WritePng(string path, Texture2D tex)
    {
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets/Art/Enemy", "S3_10_NeonDancer");

        File.WriteAllBytes(path, tex.EncodeToPNG());
        int size = tex.width;
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = size; // 1ユニット四方（ワームホールの大きさ＝NeonDancerTurretのWormhole Sizeそのまま）
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }
}

[CustomEditor(typeof(NeonDancerWormholeTextureSettings))]
public class NeonDancerWormholeTextureSettingsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.Space();
        if (GUILayout.Button("画像を生成（上書き）", GUILayout.Height(32)))
        {
            ((NeonDancerWormholeTextureSettings)target).GenerateAll();
            SceneView.RepaintAll();
        }
        EditorGUILayout.HelpBox("生成先：" + NeonDancerWormholeTextureSettings.Folder + "\nND_Wormhole_Outer / Inner / Void.png（同じファイル名で上書きするため、Prefabの参照はそのまま）", MessageType.Info);
    }
}
