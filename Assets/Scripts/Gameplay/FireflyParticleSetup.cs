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

        // Main
        var main = ps.main;
        main.duration = 10f;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 8f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0f, 1f, 0.3f),
            new Color(0.3f, 1f, 0.6f)
        );
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

        // Velocity over Lifetime: ゆるやかに漂う
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
        vel.y = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        // Color over Lifetime: フェードイン→維持→フェードアウト
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.2f, 1f, 0.4f), 0f),
                new GradientColorKey(new Color(0.2f, 1f, 0.4f), 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.2f),
                new GradientAlphaKey(1f, 0.8f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(grad);

        // Size over Lifetime: ゆるやかな点滅
        var sizeOL = ps.sizeOverLifetime;
        sizeOL.enabled = true;
        var sizeCurve = new AnimationCurve(
            new Keyframe(0f, 0.5f),
            new Keyframe(0.3f, 1f),
            new Keyframe(0.7f, 1f),
            new Keyframe(1f, 0.5f)
        );
        sizeOL.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // Noise: 有機的なふわふわ動き
        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.25f;
        noise.frequency = 0.3f;
        noise.scrollSpeed = 0.15f;
        noise.quality = ParticleSystemNoiseQuality.Low;

#if UNITY_EDITOR
        EditorUtility.SetDirty(gameObject);
#endif
        Debug.Log("[FireflyParticleSetup] 完了。確認後このコンポーネントを削除してください。");
    }
}
