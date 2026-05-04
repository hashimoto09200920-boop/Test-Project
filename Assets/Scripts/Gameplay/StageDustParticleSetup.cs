using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class StageDustParticleSetup : MonoBehaviour
{
    [ContextMenu("Setup Stage Dust Particle")]
    private void Setup()
    {
        var ps = GetComponent<ParticleSystem>();
        if (ps == null)
        {
            Debug.LogError("[StageDustParticleSetup] ParticleSystem not found.");
            return;
        }

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var existing = transform.Find("SpotlightSparkle");
        if (existing != null)
            DestroyImmediate(existing.gameObject);

        // ── メインPS：落下する舞台埃 ──────────────────────────────

        var main = ps.main;
        main.duration = 10f;
        main.loop = true;
        main.prewarm = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 14f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.07f);
        main.startColor = new ParticleSystem.MinMaxGradient(Color.white, Color.white);
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 80;

        var emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 8f;

        var shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Rectangle;
        shape.scale = new Vector3(20f, 12f, 1f);

        // 全軸TwoConstantsで統一（モード混在エラー回避）
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.x = new ParticleSystem.MinMaxCurve(-0.03f, 0.03f);
        vel.y = new ParticleSystem.MinMaxCurve(-0.5f, -0.15f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        // 金色〜琥珀〜明るい白金でスポットライトに照らされた埃を表現
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1.0f, 0.75f, 0.2f), 0f),
                new GradientColorKey(new Color(1.0f, 0.97f, 0.75f), 0.25f),
                new GradientColorKey(new Color(1.0f, 0.75f, 0.2f), 0.5f),
                new GradientColorKey(new Color(1.0f, 0.97f, 0.75f), 0.75f),
                new GradientColorKey(new Color(1.0f, 0.75f, 0.2f), 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0f,   0f),
                new GradientAlphaKey(1f,   0.06f),
                new GradientAlphaKey(1f,   0.88f),
                new GradientAlphaKey(0f,   1f)
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(grad);

        // キラッと光を受けるサイズ変化
        var sizeOL = ps.sizeOverLifetime;
        sizeOL.enabled = true;
        var sizeCurve = new AnimationCurve(
            new Keyframe(0f,    0.4f),
            new Keyframe(0.25f, 1.0f),
            new Keyframe(0.5f,  0.5f),
            new Keyframe(0.75f, 1.0f),
            new Keyframe(1f,    0.3f)
        );
        sizeOL.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        var noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.08f;
        noise.frequency = 0.3f;
        noise.scrollSpeed = 0.08f;
        noise.quality = ParticleSystemNoiseQuality.Low;

        var mainRenderer = ps.GetComponent<ParticleSystemRenderer>();
        if (mainRenderer != null)
            mainRenderer.sortingOrder = -7;

        // ── 子PS：スポットライトに瞬く大粒スパークル ──────────────

        var sparkGO = new GameObject("SpotlightSparkle");
        sparkGO.transform.SetParent(transform);
        sparkGO.transform.localPosition = Vector3.zero;
        var sparkPS = sparkGO.AddComponent<ParticleSystem>();

        var sparkMain = sparkPS.main;
        sparkMain.duration = 10f;
        sparkMain.loop = true;
        sparkMain.prewarm = true;
        sparkMain.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 3.0f);
        sparkMain.startSpeed = 0f;
        sparkMain.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.35f);
        sparkMain.startColor = new ParticleSystem.MinMaxGradient(Color.white, Color.white);
        sparkMain.gravityModifier = 0f;
        sparkMain.simulationSpace = ParticleSystemSimulationSpace.World;
        sparkMain.maxParticles = 20;

        var sparkEmission = sparkPS.emission;
        sparkEmission.enabled = true;
        sparkEmission.rateOverTime = 5f;

        var sparkShape = sparkPS.shape;
        sparkShape.enabled = true;
        sparkShape.shapeType = ParticleSystemShapeType.Rectangle;
        sparkShape.scale = new Vector3(20f, 12f, 1f);

        var sparkVel = sparkPS.velocityOverLifetime;
        sparkVel.enabled = true;
        sparkVel.space = ParticleSystemSimulationSpace.Local;
        sparkVel.x = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
        sparkVel.y = new ParticleSystem.MinMaxCurve(-0.1f, -0.02f);
        sparkVel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        // 瞬間的に白く輝いてすぐ消える
        var sparkCol = sparkPS.colorOverLifetime;
        sparkCol.enabled = true;
        var sparkGrad = new Gradient();
        sparkGrad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1.0f, 0.97f, 0.8f), 0f),
                new GradientColorKey(new Color(1.0f, 1.0f, 1.0f), 0.2f),
                new GradientColorKey(new Color(1.0f, 0.85f, 0.4f), 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.1f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        sparkCol.color = new ParticleSystem.MinMaxGradient(sparkGrad);

        var sparkRenderer = sparkPS.GetComponent<ParticleSystemRenderer>();
        sparkRenderer.sortingOrder = -7;

#if UNITY_EDITOR
        var dustMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Background/Mat_StageDust.mat");
        if (dustMat != null && mainRenderer != null)
            mainRenderer.sharedMaterial = dustMat;

        var sparkleMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Background/Mat_SpotlightSparkle.mat");
        if (sparkleMat != null)
            sparkRenderer.sharedMaterial = sparkleMat;

        EditorUtility.SetDirty(gameObject);
        EditorUtility.SetDirty(sparkGO);
#endif
        Debug.Log("[StageDustParticleSetup] 完了。落下埃 + スポットライトスパークルの2層構成で生成しました。確認後このコンポーネントを削除してください。");
    }
}
