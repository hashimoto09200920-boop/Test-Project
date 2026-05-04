using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class SoulWispParticleSetup : MonoBehaviour
{
    [ContextMenu("Setup Soul Wisp Particle")]
    private void Setup()
    {
        var ps = GetComponent<ParticleSystem>();
        if (ps == null)
        {
            Debug.LogError("[SoulWispParticleSetup] ParticleSystem not found.");
            return;
        }

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // 既存のSubEmitter子オブジェクトをクリーンアップ
        var existing = transform.Find("SoulFragment_SubEmitter");
        if (existing != null)
            DestroyImmediate(existing.gameObject);

        // ── メインPS：Soul Wisps ──────────────────────────────────

        var main = ps.main;
        main.duration = 10f;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 8f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.16f);
        main.startColor = new ParticleSystem.MinMaxGradient(Color.white, Color.white);
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 50;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 5f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Rectangle;
        shape.scale = new Vector3(20f, 12f, 1f);

        // A）上昇パルス：ゆっくり→速く→ゆっくり→速く、と波打ちながら上昇
        // 全軸をCurveモードで統一（x/zはフラット、横ゆらぎはNoiseモジュールが担う）
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;

        var xFlat = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 0f));
        vel.x = new ParticleSystem.MinMaxCurve(1f, xFlat);

        var yCurve = new AnimationCurve(
            new Keyframe(0f,   0.15f),
            new Keyframe(0.2f, 1.0f),
            new Keyframe(0.4f, 0.15f),
            new Keyframe(0.6f, 1.0f),
            new Keyframe(0.8f, 0.15f),
            new Keyframe(1f,   0.6f)
        );
        vel.y = new ParticleSystem.MinMaxCurve(0.5f, yCurve);

        var zFlat = new AnimationCurve(new Keyframe(0f, 0f), new Keyframe(1f, 0f));
        vel.z = new ParticleSystem.MinMaxCurve(1f, zFlat);

        // B）色温度シフト：シアン→アイスホワイト→シアン、速度パルスに同期
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.2f, 0.9f, 1.0f), 0f),
                new GradientColorKey(new Color(0.85f, 0.97f, 1.0f), 0.2f),
                new GradientColorKey(new Color(0.2f, 0.9f, 1.0f), 0.4f),
                new GradientColorKey(new Color(0.85f, 0.97f, 1.0f), 0.6f),
                new GradientColorKey(new Color(0.2f, 0.9f, 1.0f), 0.8f),
                new GradientColorKey(new Color(0.2f, 0.9f, 1.0f), 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0f,   0f),
                new GradientAlphaKey(1f,   0.08f),
                new GradientAlphaKey(1f,   0.85f),
                new GradientAlphaKey(0f,   1f)
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(grad);

        // サイズも速度パルスに同期して膨らむ
        var sizeOL = ps.sizeOverLifetime;
        sizeOL.enabled = true;
        var sizeCurve = new AnimationCurve(
            new Keyframe(0f,   0.5f),
            new Keyframe(0.2f, 1.0f),
            new Keyframe(0.4f, 0.6f),
            new Keyframe(0.6f, 1.0f),
            new Keyframe(0.8f, 0.6f),
            new Keyframe(1f,   0.7f)
        );
        sizeOL.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.15f;
        noise.frequency = 0.15f;
        noise.scrollSpeed = 0.1f;
        noise.quality = ParticleSystemNoiseQuality.Low;

        var mainRenderer = ps.GetComponent<ParticleSystemRenderer>();
        if (mainRenderer != null)
            mainRenderer.sortingOrder = -7;

        // C）分裂SubEmitter：Wisp消滅時にシアンの欠片が散る
        var subGO = new GameObject("SoulFragment_SubEmitter");
        subGO.transform.SetParent(transform);
        subGO.transform.localPosition = Vector3.zero;
        var subPS = subGO.AddComponent<ParticleSystem>();

        var subMain = subPS.main;
        subMain.loop = false;
        subMain.duration = 0.5f;
        subMain.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        subMain.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
        subMain.startSize = new ParticleSystem.MinMaxCurve(1.0f);
        subMain.startColor = new ParticleSystem.MinMaxGradient(Color.white, Color.white);
        subMain.gravityModifier = -0.1f;
        subMain.simulationSpace = ParticleSystemSimulationSpace.World;
        subMain.maxParticles = 200;

        var subEmission = subPS.emission;
        subEmission.enabled = true;
        subEmission.rateOverTime = 0f;
        subEmission.SetBursts(new ParticleSystem.Burst[]
        {
            new ParticleSystem.Burst(0f, 3, 5)
        });

        var subShape = subPS.shape;
        subShape.enabled = true;
        subShape.shapeType = ParticleSystemShapeType.Sphere;
        subShape.radius = 0.05f;

        // 欠片：シアンから透明にフェード
        var subCol = subPS.colorOverLifetime;
        subCol.enabled = true;
        var subGrad = new Gradient();
        subGrad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.8f, 0.97f, 1.0f), 0f),
                new GradientColorKey(new Color(0.2f, 0.9f, 1.0f), 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        subCol.color = new ParticleSystem.MinMaxGradient(subGrad);

        var subRenderer = subPS.GetComponent<ParticleSystemRenderer>();
        subRenderer.sortingOrder = -7;
        if (mainRenderer != null && mainRenderer.sharedMaterial != null)
            subRenderer.sharedMaterial = mainRenderer.sharedMaterial;

        var subEmitters = ps.subEmitters;
        subEmitters.enabled = true;
        subEmitters.AddSubEmitter(subPS, ParticleSystemSubEmitterType.Death, ParticleSystemSubEmitterProperties.InheritSize);

#if UNITY_EDITOR
        EditorUtility.SetDirty(gameObject);
        EditorUtility.SetDirty(subGO);
#endif
        Debug.Log("[SoulWispParticleSetup] 完了。上昇パルス(A) + 色シフト(B) + 分裂SubEmitter(C)を実装しました。確認後このコンポーネントを削除してください。");
    }
}
