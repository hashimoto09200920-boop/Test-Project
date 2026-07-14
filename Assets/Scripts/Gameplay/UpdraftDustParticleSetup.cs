using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// Area08用のパーティクル。デザイン（色・粒のサイズ・瞬き）はArea07のStardustParticleSetupを
// そのまま流用し、動きだけ反転させたもの。Stardustは「白→シアンが降り注ぐ」だったのに対し、
// こちらは「白→シアンの塵が下から上昇気流に吹き上げられる」動きにする。
public class UpdraftDustParticleSetup : MonoBehaviour
{
    [ContextMenu("Setup Updraft Dust Particle")]
    private void Setup()
    {
        var ps = GetComponent<ParticleSystem>();
        if (ps == null)
        {
            Debug.LogError("[UpdraftDustParticleSetup] ParticleSystem not found.");
            return;
        }

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var existing = transform.Find("UpdraftDust_Twinkle");
        if (existing != null)
            DestroyImmediate(existing.gameObject);

#if UNITY_EDITOR
        // マテリアルはArea07のStardustと同じデザインなので、既存のMat_StardustParticleをそのまま流用する
        const string matPath = "Assets/Art/Background/Mat_StardustParticle.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        var mainRenderer = ps.GetComponent<ParticleSystemRenderer>();
        if (mat != null && mainRenderer != null)
        {
            mainRenderer.sharedMaterial = mat;
            Debug.Log($"[UpdraftDustParticleSetup] 既存の{matPath}を流用しました。");
        }
        else
        {
            Debug.LogWarning($"[UpdraftDustParticleSetup] {matPath}が見つかりませんでした。手動でアサインしてください。");
        }
#endif

        // ── メインPS：上昇気流に吹き上げられる塵 ──────────────────────────

        var main = ps.main;
        main.duration       = 10f;
        main.loop           = true;
        main.prewarm        = true;
        main.startLifetime  = new ParticleSystem.MinMaxCurve(4f, 8f);
        main.startSpeed     = 0f;
        main.startSize      = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
        main.startColor     = new ParticleSystem.MinMaxGradient(Color.white, Color.white);
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles   = 80;

        var emission = ps.emission;
        emission.enabled       = true;
        emission.rateOverTime  = 15f;

        var shape = ps.shape;
        shape.enabled    = true;
        shape.shapeType  = ParticleSystemShapeType.Rectangle;
        shape.scale      = new Vector3(22f, 14f, 1f);

        // 上昇＋気流らしい横ゆれ（Stardustの降下(-0.6〜-0.2)を反転し、少し勢いを強めにする）
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space   = ParticleSystemSimulationSpace.World;
        vel.x       = new ParticleSystem.MinMaxCurve(-0.15f, 0.15f);
        vel.y       = new ParticleSystem.MinMaxCurve(0.3f, 0.9f);
        vel.z       = new ParticleSystem.MinMaxCurve(0f, 0f);

        // 白 → シアン → 透明（Stardustと同じ配色）
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1.0f,  1.0f,  1.0f),  0f),
                new GradientColorKey(new Color(0.85f, 0.97f, 1.0f),  0.3f),
                new GradientColorKey(new Color(0.2f,  0.9f,  1.0f),  0.7f),
                new GradientColorKey(new Color(0.2f,  0.9f,  1.0f),  1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0f,   0f),
                new GradientAlphaKey(1f,   0.08f),
                new GradientAlphaKey(1f,   0.75f),
                new GradientAlphaKey(0f,   1f)
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(grad);

        // フェードイン→ピーク→フェードアウトのサイズ変化
        var sizeOL = ps.sizeOverLifetime;
        sizeOL.enabled = true;
        var sizeCurve = new AnimationCurve(
            new Keyframe(0f,   0.3f),
            new Keyframe(0.15f, 1.0f),
            new Keyframe(0.7f,  0.8f),
            new Keyframe(1f,   0.0f)
        );
        sizeOL.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // 気流の乱れを表現するため、Stardust(0.1)より少し強めのNoiseで揺らす
        var noise = ps.noise;
        noise.enabled     = true;
        noise.strength    = 0.18f;
        noise.frequency   = 0.25f;
        noise.scrollSpeed = 0.08f;
        noise.quality     = ParticleSystemNoiseQuality.Low;

        var mainRenderer2 = ps.GetComponent<ParticleSystemRenderer>();
        if (mainRenderer2 != null)
            mainRenderer2.sortingOrder = -7;

        // ── 子PS：Twinkle（瞬く光の明滅、Stardustと同じ） ──────────────────────────

        var twinkleGO = new GameObject("UpdraftDust_Twinkle");
        twinkleGO.transform.SetParent(transform);
        twinkleGO.transform.localPosition = Vector3.zero;
        var twinklePS = twinkleGO.AddComponent<ParticleSystem>();

        var twMain = twinklePS.main;
        twMain.duration        = 10f;
        twMain.loop            = true;
        twMain.prewarm         = true;
        twMain.startLifetime   = new ParticleSystem.MinMaxCurve(0.5f, 1.5f);
        twMain.startSpeed      = 0f;
        twMain.startSize       = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
        twMain.startColor      = new ParticleSystem.MinMaxGradient(Color.white, Color.white);
        twMain.gravityModifier = 0f;
        twMain.simulationSpace = ParticleSystemSimulationSpace.World;
        twMain.maxParticles    = 20;

        var twEmission = twinklePS.emission;
        twEmission.enabled      = true;
        twEmission.rateOverTime = 3f;

        var twShape = twinklePS.shape;
        twShape.enabled   = true;
        twShape.shapeType = ParticleSystemShapeType.Rectangle;
        twShape.scale     = new Vector3(20f, 12f, 1f);

        // 瞬く：白→シアン白→透明、急速に明滅（Stardustと同じ）
        var twCol = twinklePS.colorOverLifetime;
        twCol.enabled = true;
        var twGrad = new Gradient();
        twGrad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1.0f, 1.0f, 1.0f), 0f),
                new GradientColorKey(new Color(0.8f, 0.97f, 1.0f), 0.5f),
                new GradientColorKey(new Color(0.5f, 0.95f, 1.0f), 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0f,   0f),
                new GradientAlphaKey(1f,   0.2f),
                new GradientAlphaKey(0.8f, 0.5f),
                new GradientAlphaKey(0f,   1f)
            }
        );
        twCol.color = new ParticleSystem.MinMaxGradient(twGrad);

        // サイズが急速に膨らんで消える（瞬き感、Stardustと同じ）
        var twSizeOL = twinklePS.sizeOverLifetime;
        twSizeOL.enabled = true;
        var twSizeCurve = new AnimationCurve(
            new Keyframe(0f,    0f),
            new Keyframe(0.2f,  1.0f),
            new Keyframe(0.5f,  0.7f),
            new Keyframe(1f,    0f)
        );
        twSizeOL.size = new ParticleSystem.MinMaxCurve(1f, twSizeCurve);

        // Twinkle側も上昇方向に合わせて少しだけ上に流す
        // ※x/y/zは全て同じcurve modeでないとUnityがエラーを出すため、全てTwoConstantsで統一する
        var twVel = twinklePS.velocityOverLifetime;
        twVel.enabled = true;
        twVel.space   = ParticleSystemSimulationSpace.World;
        twVel.x       = new ParticleSystem.MinMaxCurve(0f, 0f);
        twVel.y       = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        twVel.z       = new ParticleSystem.MinMaxCurve(0f, 0f);

        var twRenderer = twinklePS.GetComponent<ParticleSystemRenderer>();
        twRenderer.sortingOrder = -7;
#if UNITY_EDITOR
        if (mainRenderer != null && mainRenderer.sharedMaterial != null)
            twRenderer.sharedMaterial = mainRenderer.sharedMaterial;
#endif

#if UNITY_EDITOR
        EditorUtility.SetDirty(gameObject);
        EditorUtility.SetDirty(twinkleGO);
#endif
        Debug.Log("[UpdraftDustParticleSetup] 完了。上昇する塵(Main) + 瞬く光(Twinkle)の2層構成で生成しました。確認後このコンポーネントを削除してください。");
    }
}
