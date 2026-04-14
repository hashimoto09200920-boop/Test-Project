using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// PS_CoreAura / PS_InnerSparks を一括設定するセットアップスクリプト。
/// 実行後はこのコンポーネントを Remove Component して削除してください。
/// </summary>
[DisallowMultipleComponent]
public class OrbGlowSetup : MonoBehaviour
{
    [ContextMenu("01 - Setup PS_CoreAura (このオブジェクトに実行)")]
    private void SetupCoreAura()
    {
        var ps = GetComponent<ParticleSystem>();
        if (ps == null)
        {
            Debug.LogError("[OrbGlowSetup] ParticleSystem が見つかりません。PS_CoreAura にアタッチしてください。");
            return;
        }

        // ── Main ──────────────────────────────────────────
        var main = ps.main;
        main.duration          = 2f;
        main.loop              = true;
        main.prewarm           = true;
        main.startDelay        = new ParticleSystem.MinMaxCurve(0f);
        main.startLifetime     = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
        main.startSpeed        = new ParticleSystem.MinMaxCurve(0f);
        main.startSize         = new ParticleSystem.MinMaxCurve(1.0f, 1.8f);  // オーブを覆う大きさ
        main.startColor        = new ParticleSystem.MinMaxGradient(new Color(1f, 0.85f, 0.1f, 0.65f));
        main.gravityModifier   = 0f;
        main.simulationSpace   = ParticleSystemSimulationSpace.World;
        main.playOnAwake       = true;
        main.maxParticles      = 30;

        // ── Emission ──────────────────────────────────────
        var emission = ps.emission;
        emission.enabled       = true;
        emission.rateOverTime  = new ParticleSystem.MinMaxCurve(5f);

        // ── Shape ─────────────────────────────────────────
        var shape = ps.shape;
        shape.enabled          = true;
        shape.shapeType        = ParticleSystemShapeType.Circle;
        shape.radius           = 0.01f;
        shape.radiusThickness  = 1f;

        // ── Size over Lifetime（山型カーブ）──────────────
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        var sizeCurve = new AnimationCurve(
            new Keyframe(0f,   0f,  0f,  3f),
            new Keyframe(0.4f, 1f,  0f,  0f),
            new Keyframe(1f,   0f, -3f,  0f)
        );
        sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // ── Color over Lifetime（α: 0→高→高→0）─────────
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0f,    0f),
                new GradientAlphaKey(0.85f, 0.2f),
                new GradientAlphaKey(0.85f, 0.8f),
                new GradientAlphaKey(0f,    1f)
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(gradient);

        // ── Renderer ──────────────────────────────────────
        var ren = GetComponent<ParticleSystemRenderer>();
        ren.renderMode     = ParticleSystemRenderMode.Billboard;
        ren.sortingLayerID = 0;  // Default
        ren.sortingOrder   = 1;  // Orb (Order:0) より手前

#if UNITY_EDITOR
        AssignMaterial(ren, "M_OrbGlow_Additive");
        MarkDirty();
#endif
        Debug.Log("[OrbGlowSetup] PS_CoreAura の設定が完了しました。");
    }

    [ContextMenu("03 - Setup PS_Rays (このオブジェクトに実行)")]
    private void SetupRays()
    {
        var ps = GetComponent<ParticleSystem>();
        if (ps == null)
        {
            Debug.LogError("[OrbGlowSetup] ParticleSystem が見つかりません。PS_Rays にアタッチしてください。");
            return;
        }

        // ── Main ──────────────────────────────────────────
        var main = ps.main;
        main.duration          = 1f;
        main.loop              = true;
        main.prewarm           = true;
        main.startLifetime     = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
        main.startSpeed        = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);  // 外に向かって速く飛ぶ
        main.startSize         = new ParticleSystem.MinMaxCurve(0.06f, 0.14f);
        main.startColor        = new ParticleSystem.MinMaxGradient(new Color(1f, 0.97f, 0.7f, 1f));  // 明るい黄白
        main.gravityModifier   = 0f;
        main.simulationSpace   = ParticleSystemSimulationSpace.World;
        main.playOnAwake       = true;
        main.maxParticles      = 50;

        // ── Emission ──────────────────────────────────────
        var emission = ps.emission;
        emission.enabled       = true;
        emission.rateOverTime  = new ParticleSystem.MinMaxCurve(15f);

        // ── Shape（全方向に放射）──────────────────────────
        var shape = ps.shape;
        shape.enabled          = true;
        shape.shapeType        = ParticleSystemShapeType.Circle;
        shape.radius           = 0.02f;
        shape.radiusThickness  = 1f;

        // ── Color over Lifetime（明→透明）────────────────
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(Color.white,                    0f),
                new GradientColorKey(new Color(1f, 0.85f, 0.1f, 1f), 0.5f),
                new GradientColorKey(new Color(1f, 0.4f,  0f,   1f), 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(gradient);

        // ── Renderer（Stretched Billboard で光線状に）────
        var ren = GetComponent<ParticleSystemRenderer>();
        ren.renderMode      = ParticleSystemRenderMode.Stretch;
        ren.velocityScale   = 0.6f;   // 速度に比例して伸びる
        ren.lengthScale     = 0.5f;
        ren.sortingLayerID  = 0;
        ren.sortingOrder    = 2;      // CoreAura(1)よりさらに手前

#if UNITY_EDITOR
        AssignMaterial(ren, "M_OrbGlow_Additive");
        MarkDirty();
#endif
        Debug.Log("[OrbGlowSetup] PS_Rays の設定が完了しました。");
    }

    [ContextMenu("02 - Setup PS_InnerSparks (このオブジェクトに実行)")]
    private void SetupInnerSparks()
    {
        var ps = GetComponent<ParticleSystem>();
        if (ps == null)
        {
            Debug.LogError("[OrbGlowSetup] ParticleSystem が見つかりません。PS_InnerSparks にアタッチしてください。");
            return;
        }

        // ── Main ──────────────────────────────────────────
        var main = ps.main;
        main.duration          = 1f;
        main.loop              = true;
        main.prewarm           = true;
        main.startLifetime     = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        main.startSpeed        = new ParticleSystem.MinMaxCurve(0.03f, 0.10f);
        main.startSize         = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
        main.startColor        = new ParticleSystem.MinMaxGradient(new Color(1f, 0.94f, 0.59f, 0.78f));
        main.gravityModifier   = 0f;
        main.simulationSpace   = ParticleSystemSimulationSpace.World;
        main.playOnAwake       = true;
        main.maxParticles      = 30;

        // ── Emission ──────────────────────────────────────
        var emission = ps.emission;
        emission.enabled       = true;
        emission.rateOverTime  = new ParticleSystem.MinMaxCurve(6f);

        // ── Shape ─────────────────────────────────────────
        var shape = ps.shape;
        shape.enabled          = true;
        shape.shapeType        = ParticleSystemShapeType.Circle;
        shape.radius           = 0.08f;
        shape.radiusThickness  = 1f;

        // ── Color over Lifetime（フェードアウト）─────────
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.86f, 0f),
                new GradientAlphaKey(0f,    1f)
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(gradient);

        // ── Renderer ──────────────────────────────────────
        var ren = GetComponent<ParticleSystemRenderer>();
        ren.renderMode     = ParticleSystemRenderMode.Billboard;
        ren.sortingLayerID = 0;
        ren.sortingOrder   = 1;

#if UNITY_EDITOR
        AssignMaterial(ren, "M_OrbGlow_Additive");
        MarkDirty();
#endif
        Debug.Log("[OrbGlowSetup] PS_InnerSparks の設定が完了しました。");
    }

#if UNITY_EDITOR
    private void AssignMaterial(ParticleSystemRenderer ren, string matName)
    {
        // プロジェクト内を名前で検索
        var guids = AssetDatabase.FindAssets($"t:Material {matName}");
        if (guids.Length == 0)
        {
            Debug.LogWarning($"[OrbGlowSetup] マテリアル '{matName}' が見つかりませんでした。Inspector から手動でアサインしてください。");
            return;
        }
        var path = AssetDatabase.GUIDToAssetPath(guids[0]);
        var mat  = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null)
        {
            ren.sharedMaterial = mat;
            Debug.Log($"[OrbGlowSetup] マテリアル '{path}' を自動アサインしました。");
        }
    }

    private void MarkDirty()
    {
        EditorUtility.SetDirty(gameObject);
        if (gameObject.scene.IsValid())
            EditorSceneManager.MarkSceneDirty(gameObject.scene);
    }
#endif
}
