using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class SandDustParticleSetup : MonoBehaviour
{
    [ContextMenu("Setup Sand Dust Particle")]
    private void Setup()
    {
        var ps = GetComponent<ParticleSystem>();
        if (ps == null)
        {
            Debug.LogError("[SandDustParticleSetup] ParticleSystem not found.");
            return;
        }

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

#if UNITY_EDITOR
        // Mat_SandDust.mat を新規作成（Alpha Blend）
        const string matPath = "Assets/Art/Background/Mat_SandDust.mat";
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null)
            shader = Shader.Find("Particles/Standard Unlit");
        var mat = new Material(shader);
        mat.SetFloat("_Surface",   1f);   // Transparent
        mat.SetFloat("_Blend",     0f);   // Alpha blend
        mat.SetFloat("_SrcBlend",  5f);   // SrcAlpha
        mat.SetFloat("_DstBlend", 10f);   // OneMinusSrcAlpha
        mat.SetFloat("_ZWrite",    0f);
        mat.renderQueue = 3000;
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        AssetDatabase.CreateAsset(mat, matPath);
        AssetDatabase.SaveAssets();
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
            renderer.sharedMaterial = mat;
        Debug.Log($"[SandDustParticleSetup] {matPath} を作成しました。");
#endif

        var main = ps.main;
        main.duration = 10f;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0f, 0f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.831f, 0.647f, 0.329f),   // #D4A554
            new Color(0.627f, 0.471f, 0.188f)    // #A07830
        );
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 120;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 12f;
        emission.SetBursts(new ParticleSystem.Burst[0]);

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Rectangle;
        shape.scale = new Vector3(22f, 14f, 1f);

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(-3.5f, -1.5f);
        vel.y = new ParticleSystem.MinMaxCurve(-0.3f, -0.05f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.831f, 0.647f, 0.329f), 0f),
                new GradientColorKey(new Color(0.750f, 0.565f, 0.251f), 0.5f),
                new GradientColorKey(new Color(0.627f, 0.471f, 0.188f), 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0f,   0f),
                new GradientAlphaKey(0.8f, 0.1f),
                new GradientAlphaKey(0.8f, 0.8f),
                new GradientAlphaKey(0f,   1f)
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var sizeOL = ps.sizeOverLifetime;
        sizeOL.enabled = true;
        var sizeCurve = new AnimationCurve(
            new Keyframe(0f,   0.3f),
            new Keyframe(0.2f, 1.0f),
            new Keyframe(0.8f, 0.8f),
            new Keyframe(1f,   0.1f)
        );
        sizeOL.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.15f;
        noise.frequency = 0.4f;
        noise.scrollSpeed = 0.1f;
        noise.quality = ParticleSystemNoiseQuality.Low;

        var r = ps.GetComponent<ParticleSystemRenderer>();
        if (r != null)
            r.sortingOrder = -7;

#if UNITY_EDITOR
        EditorUtility.SetDirty(gameObject);
#endif
        Debug.Log("[SandDustParticleSetup] 完了。確認後このコンポーネントを削除してください。");
    }
}
