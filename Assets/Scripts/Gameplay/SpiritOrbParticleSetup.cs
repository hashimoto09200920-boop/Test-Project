using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class SpiritOrbParticleSetup : MonoBehaviour
{
    [ContextMenu("Setup Spirit Orb Particle")]
    private void Setup()
    {
        var ps = GetComponent<ParticleSystem>();
        if (ps == null)
        {
            Debug.LogError("[SpiritOrbParticleSetup] ParticleSystem not found.");
            return;
        }

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // 既存の子オブジェクトをクリーンアップ
        var existing = transform.Find("PulsingOrbs");
        if (existing != null)
            DestroyImmediate(existing.gameObject);

        // ── メインPS：Ghost Trail（横に漂い、光の尾を引く） ──────────

        var main = ps.main;
        main.duration = 10f;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 7f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
        main.startColor = new ParticleSystem.MinMaxGradient(Color.white, Color.white);
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 30;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 3f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Rectangle;
        shape.scale = new Vector3(20f, 12f, 1f);

        // 上昇せず横に漂う
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
        vel.y = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        // 紫 → 白ラベンダー → 紫と色が変化しながらフェード
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.6f, 0.3f, 1.0f), 0f),
                new GradientColorKey(new Color(0.95f, 0.85f, 1.0f), 0.4f),
                new GradientColorKey(new Color(0.6f, 0.3f, 1.0f), 0.7f),
                new GradientColorKey(new Color(0.6f, 0.3f, 1.0f), 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.1f),
                new GradientAlphaKey(1f, 0.8f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var sizeOL = ps.sizeOverLifetime;
        sizeOL.enabled = true;
        var sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.5f),
            new Keyframe(0.3f, 1f),
            new Keyframe(0.7f, 1f),
            new Keyframe(1f, 0.3f)
        );
        sizeOL.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.4f;
        noise.frequency = 0.2f;
        noise.scrollSpeed = 0.08f;
        noise.quality = ParticleSystemNoiseQuality.Low;

        // Trails：光の尾を引く
        var trails = ps.trails;
        trails.enabled = true;
        trails.ratio = 1f;
        trails.lifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        trails.minVertexDistance = 0.05f;
        trails.worldSpace = true;
        trails.dieWithParticles = false;
        trails.sizeAffectsWidth = true;
        trails.inheritParticleColor = false;
        trails.colorOverTrail = new ParticleSystem.MinMaxGradient(
            new Gradient()
            {
                alphaKeys = new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(1f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                },
                colorKeys = new GradientColorKey[]
                {
                    new GradientColorKey(new Color(1f, 0.9f, 1f), 0f),
                    new GradientColorKey(new Color(0.7f, 0.4f, 1f), 0.5f),
                    new GradientColorKey(new Color(0.4f, 0.1f, 0.8f), 1f)
                }
            }
        );

        var mainRenderer = ps.GetComponent<ParticleSystemRenderer>();
        if (mainRenderer != null)
            mainRenderer.sortingOrder = -7;

#if UNITY_EDITOR
        // Trail用マテリアルを自動設定
        var fogMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Background/Mat_FogAdditive.mat");
        if (fogMat != null && mainRenderer != null)
            mainRenderer.trailMaterial = fogMat;
#endif

        // ── 子PS：Pulsing Orbs（その場で呼吸するように明滅する大きなオーブ） ──

        var orbGO = new GameObject("PulsingOrbs");
        orbGO.transform.SetParent(transform);
        orbGO.transform.localPosition = Vector3.zero;
        var orbPS = orbGO.AddComponent<ParticleSystem>();

        var orbMain = orbPS.main;
        orbMain.duration = 10f;
        orbMain.loop = true;
        orbMain.prewarm = true;
        orbMain.startLifetime = new ParticleSystem.MinMaxCurve(8f, 14f);
        orbMain.startSpeed = 0f;
        orbMain.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.55f);
        orbMain.startColor = new ParticleSystem.MinMaxGradient(Color.white, Color.white);
        orbMain.gravityModifier = 0f;
        orbMain.simulationSpace = ParticleSystemSimulationSpace.World;
        orbMain.maxParticles = 8;

        var orbEmission = orbPS.emission;
        orbEmission.enabled = true;
        orbEmission.rateOverTime = 0.7f;

        var orbShape = orbPS.shape;
        orbShape.enabled = true;
        orbShape.shapeType = ParticleSystemShapeType.Rectangle;
        orbShape.scale = new Vector3(18f, 10f, 1f);

        // ほぼその場に留まる
        var orbVel = orbPS.velocityOverLifetime;
        orbVel.enabled = true;
        orbVel.space = ParticleSystemSimulationSpace.Local;
        orbVel.x = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);
        orbVel.y = new ParticleSystem.MinMaxCurve(-0.02f, 0.02f);
        orbVel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        // 暗い紫→白ラベンダー→暗い紫と脈動、Ghost flicker
        var orbCol = orbPS.colorOverLifetime;
        orbCol.enabled = true;
        var orbGrad = new Gradient();
        orbGrad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.4f, 0.1f, 0.8f), 0f),
                new GradientColorKey(new Color(0.95f, 0.9f, 1.0f), 0.25f),
                new GradientColorKey(new Color(0.4f, 0.1f, 0.8f), 0.5f),
                new GradientColorKey(new Color(0.95f, 0.9f, 1.0f), 0.75f),
                new GradientColorKey(new Color(0.4f, 0.1f, 0.8f), 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.9f, 0.25f),
                new GradientAlphaKey(0.1f, 0.5f),
                new GradientAlphaKey(0.9f, 0.75f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        orbCol.color = new ParticleSystem.MinMaxGradient(orbGrad);

        // 大きく呼吸するようなサイズ変化
        var orbSizeOL = orbPS.sizeOverLifetime;
        orbSizeOL.enabled = true;
        var orbSizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.3f),
            new Keyframe(0.25f, 1.0f),
            new Keyframe(0.5f, 0.4f),
            new Keyframe(0.75f, 1.0f),
            new Keyframe(1f, 0.3f)
        );
        orbSizeOL.size = new ParticleSystem.MinMaxCurve(1f, orbSizeCurve);

        var orbNoise = orbPS.noise;
        orbNoise.enabled = true;
        orbNoise.strength = 0.15f;
        orbNoise.frequency = 0.15f;
        orbNoise.scrollSpeed = 0.05f;
        orbNoise.quality = ParticleSystemNoiseQuality.Low;

        var orbRenderer = orbPS.GetComponent<ParticleSystemRenderer>();
        orbRenderer.sortingOrder = -7;
        if (mainRenderer != null && mainRenderer.sharedMaterial != null)
            orbRenderer.sharedMaterial = mainRenderer.sharedMaterial;

#if UNITY_EDITOR
        EditorUtility.SetDirty(gameObject);
        EditorUtility.SetDirty(orbGO);
#endif
        Debug.Log("[SpiritOrbParticleSetup] 完了。Ghost Trail + Pulsing Orbsの2層構成で生成しました。確認後このコンポーネントを削除してください。");
    }
}
