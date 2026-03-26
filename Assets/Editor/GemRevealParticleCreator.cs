using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

public static class GemRevealParticleCreator
{
    private const string PrefabPath   = "Assets/Prefabs/Effects/GemRevealParticle.prefab";
    private const string MaterialPath = "Assets/Materials/GemRevealParticleMat.mat";

    [MenuItem("Tools/Create Gem Reveal Particle Prefab")]
    public static void CreateGemRevealParticle()
    {
        // ===== マテリアル（既存があれば上書き、なければ新規）=====
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
        {
            Debug.LogError("[GemRevealParticleCreator] シェーダーが見つかりません。");
            return;
        }

        var existingMat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        Material mat = existingMat != null ? existingMat : new Material(shader);
        mat.shader = shader;

        mat.SetFloat("_Surface",  1f);
        mat.SetFloat("_Blend",    0f);
        mat.SetInt("_SrcBlend",   (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend",   (int)BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite",     0);
        mat.SetColor("_BaseColor", Color.white);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = 3000;

        if (existingMat == null)
            AssetDatabase.CreateAsset(mat, MaterialPath);
        else
            EditorUtility.SetDirty(mat);

        AssetDatabase.SaveAssets();
        Debug.Log($"[GemRevealParticleCreator] マテリアル: {MaterialPath}");

        // ===== Particle System =====
        var go = new GameObject("GemRevealParticle");
        var ps = go.AddComponent<ParticleSystem>();

        var main             = ps.main;
        main.duration        = 1.5f;
        main.loop            = false;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.8f, 3.0f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(1f, 4f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
        main.startRotation   = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        main.gravityModifier = 0.05f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 80;
        main.startColor      = Color.white;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 60) });

        var shape             = ps.shape;
        shape.enabled         = true;
        shape.shapeType       = ParticleSystemShapeType.Sphere;
        shape.radius          = 0.5f;
        shape.radiusThickness = 0f;

        // Color over Lifetime: 虹色を2周させてばらつきを最大化
        // 8キー制限内で 4色×2周 = 8キー
        var col     = ps.colorOverLifetime;
        col.enabled = true;
        var g       = new Gradient();
        g.mode      = GradientMode.Blend;
        g.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1f, 0f,   0f),   0.00f), // 赤   (1周目)
                new GradientColorKey(new Color(1f, 1f,   0f),   0.12f), // 黄
                new GradientColorKey(new Color(0f, 1f,   0f),   0.25f), // 緑
                new GradientColorKey(new Color(0f, 0.4f, 1f),   0.37f), // 青
                new GradientColorKey(new Color(1f, 0f,   0f),   0.50f), // 赤   (2周目)
                new GradientColorKey(new Color(1f, 1f,   0f),   0.62f), // 黄
                new GradientColorKey(new Color(0f, 1f,   0f),   0.75f), // 緑
                new GradientColorKey(new Color(0f, 0.4f, 1f),   0.87f), // 青
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f, 0.00f),
                new GradientAlphaKey(1f, 0.75f),
                new GradientAlphaKey(0f, 1.00f),
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(g);

        var sol     = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size    = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(1f, 0.2f)));

        var ren              = go.GetComponent<ParticleSystemRenderer>();
        ren.material         = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        ren.sortingLayerName = "UI";
        ren.sortingOrder     = 100;
        ren.renderMode       = ParticleSystemRenderMode.Billboard;

        // 既存Prefabを上書き（GUIDを保持するため上書き保存）
        var existingPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        bool success;
        if (existingPrefab != null)
            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath, out success);
        else
            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath, out success);

        Object.DestroyImmediate(go);

        if (success)
        {
            Debug.Log($"[GemRevealParticleCreator] 完了: {PrefabPath}");
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        }
        else
            Debug.LogError("[GemRevealParticleCreator] Prefab の保存に失敗しました。");
    }
}
