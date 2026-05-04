using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class EmberParticleSetup : MonoBehaviour
{
    [ContextMenu("Setup Ember Particle")]
    private void Setup()
    {
        var ps = GetComponent<ParticleSystem>();
        if (ps == null)
        {
            Debug.LogError("[EmberParticleSetup] ParticleSystem not found.");
            return;
        }

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // 既存のサブエミッター子オブジェクトをクリーンアップ
        var existing = transform.Find("EmberBurst_SubEmitter");
        if (existing != null)
            DestroyImmediate(existing.gameObject);

        // ── メインPS（漂う火の粉） ──────────────────────────

        var main = ps.main;
        main.duration = 10f;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(2f, 5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.10f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.45f, 0f),
            new Color(1f, 0.9f, 0.2f)
        );
        main.gravityModifier = 0.05f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 80;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 10f;
        emission.SetBursts(new ParticleSystem.Burst[0]);

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Rectangle;
        shape.scale = new Vector3(20f, 12f, 1f);

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);
        vel.y = new ParticleSystem.MinMaxCurve(0.3f, 1.0f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1f, 0.45f, 0f), 0f),
                new GradientColorKey(new Color(1f, 0.85f, 0.2f), 0.5f),
                new GradientColorKey(new Color(1f, 1f, 0.6f), 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.05f),
                new GradientAlphaKey(1f, 0.7f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var sizeOL = ps.sizeOverLifetime;
        sizeOL.enabled = true;
        var sizeCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.6f, 0.8f),
            new Keyframe(1f, 0.2f)
        );
        sizeOL.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.35f;
        noise.frequency = 0.6f;
        noise.scrollSpeed = 0.2f;
        noise.quality = ParticleSystemNoiseQuality.Low;

        var mainRenderer = ps.GetComponent<ParticleSystemRenderer>();
        if (mainRenderer != null)
            mainRenderer.sortingOrder = -7;

        // ── サブPS（火の粉が消えた瞬間に四方へ飛び散る火花） ──

        var subGO = new GameObject("EmberBurst_SubEmitter");
        subGO.transform.SetParent(transform);
        subGO.transform.localPosition = Vector3.zero;
        var subPS = subGO.AddComponent<ParticleSystem>();

        var subMain = subPS.main;
        subMain.loop = false;
        subMain.duration = 0.5f;
        subMain.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        subMain.startSpeed = new ParticleSystem.MinMaxCurve(2.0f, 5.0f);
        subMain.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.07f);
        subMain.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1f, 0.8f, 0.1f),
            new Color(1f, 0.4f, 0f)
        );
        subMain.gravityModifier = 0.4f;
        subMain.simulationSpace = ParticleSystemSimulationSpace.World;
        subMain.maxParticles = 300;

        var subEmission = subPS.emission;
        subEmission.enabled = true;
        subEmission.rateOverTime = 0f;
        subEmission.SetBursts(new ParticleSystem.Burst[]
        {
            new ParticleSystem.Burst(0f, 8, 12)
        });

        // 一点から全方向に飛び散る
        var subShape = subPS.shape;
        subShape.enabled = true;
        subShape.shapeType = ParticleSystemShapeType.Sphere;
        subShape.radius = 0.05f;

        var subRenderer = subPS.GetComponent<ParticleSystemRenderer>();
        subRenderer.sortingOrder = -7;
        if (mainRenderer != null && mainRenderer.sharedMaterial != null)
            subRenderer.sharedMaterial = mainRenderer.sharedMaterial;

        // メインPSの死亡時にサブPSを発火
        var subEmitters = ps.subEmitters;
        subEmitters.enabled = true;
        subEmitters.AddSubEmitter(subPS, ParticleSystemSubEmitterType.Death, ParticleSystemSubEmitterProperties.InheritNothing);

#if UNITY_EDITOR
        EditorUtility.SetDirty(gameObject);
        EditorUtility.SetDirty(subGO);
#endif
        Debug.Log("[EmberParticleSetup] 完了。サブエミッターのMaterialも自動コピーしています。確認後このコンポーネントを削除してください。");
    }
}
