using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class FireflyParticleSetup : MonoBehaviour
{
    [ContextMenu("Setup Firefly Particle")]
    private void Setup()
    {
        var ps = GetComponent<ParticleSystem>();
        if (ps == null)
        {
            Debug.LogError("[FireflyParticleSetup] ParticleSystem not found.");
            return;
        }

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // Main
        var main = ps.main;
        main.duration = 10f;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 8f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
        main.startColor = new ParticleSystem.MinMaxGradient(Color.white, Color.white);
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 50;

        // Emission
        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 5f;

        // Shape: プレイ画面全体をカバーする矩形（見た目に合わせてXY微調整）
        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Rectangle;
        shape.scale = new Vector3(20f, 12f, 1f);

        // Velocity over Lifetime: ホバリング中心、上昇は最小限
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
        vel.y = new ParticleSystem.MinMaxCurve(0.02f, 0.1f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        // Color over Lifetime: 蛍の点滅（オン→オフ→オン）
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.1f, 1f, 0.2f), 0f),
                new GradientColorKey(new Color(1f, 1f, 0.2f), 0.2f),
                new GradientColorKey(new Color(0.1f, 1f, 0.2f), 0.45f),
                new GradientColorKey(new Color(1f, 0.65f, 0.05f), 0.72f),
                new GradientColorKey(new Color(0.1f, 1f, 0.2f), 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.1f),
                new GradientAlphaKey(1f, 0.3f),
                new GradientAlphaKey(0f, 0.35f),
                new GradientAlphaKey(0f, 0.55f),
                new GradientAlphaKey(1f, 0.6f),
                new GradientAlphaKey(1f, 0.85f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(grad);

        // Size over Lifetime: 点灯時に大きく、消灯時に小さく
        var sizeOL = ps.sizeOverLifetime;
        sizeOL.enabled = true;
        var sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.3f),
            new Keyframe(0.1f, 1f),
            new Keyframe(0.3f, 1f),
            new Keyframe(0.35f, 0.3f),
            new Keyframe(0.55f, 0.3f),
            new Keyframe(0.6f, 1f),
            new Keyframe(0.85f, 1f),
            new Keyframe(1f, 0.3f)
        );
        sizeOL.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // Noise: 不規則な飛び方（蛍らしい動き）
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.5f;
        noise.frequency = 0.5f;
        noise.scrollSpeed = 0.15f;
        noise.quality = ParticleSystemNoiseQuality.Low;

#if UNITY_EDITOR
        EditorUtility.SetDirty(gameObject);
#endif
        Debug.Log("[FireflyParticleSetup] 完了。確認後このコンポーネントを削除してください。");
    }
}
