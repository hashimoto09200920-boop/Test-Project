using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// PS_ConvergeBurst を一括設定するセットアップスクリプト。
/// 円周上に生成した粒子が中心へ収束しながら発光する「エネルギー収束」演出。
/// 実行後はこのコンポーネントを Remove Component して削除してください。
/// </summary>
[DisallowMultipleComponent]
public class ConvergeBurstVfxSetup : MonoBehaviour
{
    [ContextMenu("Setup Converge Burst (このオブジェクトに実行)")]
    private void SetupConvergeBurst()
    {
        var ps = GetComponent<ParticleSystem>();
        if (ps == null)
        {
            Debug.LogError("[ConvergeBurstVfxSetup] ParticleSystem が見つかりません。VFX_ConvergeBurst にアタッチしてください。");
            return;
        }

        // ── Main ──────────────────────────────────────────
        var main = ps.main;
        main.duration          = 0.5f;
        main.loop              = false;
        main.prewarm           = false;
        main.startDelay        = new ParticleSystem.MinMaxCurve(0f);
        main.startLifetime     = new ParticleSystem.MinMaxCurve(0.45f, 0.55f);
        main.startSpeed        = new ParticleSystem.MinMaxCurve(0f); // 移動はVelocity over Lifetimeのみで行う
        main.startSize         = new ParticleSystem.MinMaxCurve(0.06f, 0.16f);
        main.startColor        = new ParticleSystem.MinMaxGradient(new Color(0.4f, 0.95f, 1f, 1f), new Color(1f, 0.55f, 0.9f, 1f));
        main.gravityModifier   = 0f;
        main.simulationSpace   = ParticleSystemSimulationSpace.Local;
        main.playOnAwake       = false;
        main.maxParticles      = 60;

        // ── Emission（発射時に1回だけバースト）─────────────
        var emission = ps.emission;
        emission.enabled       = true;
        emission.rateOverTime  = new ParticleSystem.MinMaxCurve(0f);
        emission.SetBursts(new ParticleSystem.Burst[]
        {
            new ParticleSystem.Burst(0f, 40)
        });

        // ── Shape（円周上のみから発生させ、収束の始点を揃える）──
        var shape = ps.shape;
        shape.enabled          = true;
        shape.shapeType        = ParticleSystemShapeType.Circle;
        shape.radius           = 1.5f;
        shape.radiusThickness  = 0f;

        // ── Velocity over Lifetime（中心へ収束する動きの本体）──
        var vol = ps.velocityOverLifetime;
        vol.enabled = true;
        vol.space   = ParticleSystemSimulationSpace.Local;
        // 半径1.5を寿命0.5秒で中心まで引き寄せる（-radius/lifetime）
        vol.radial  = new ParticleSystem.MinMaxCurve(-3f);

        // ── Color over Lifetime（収束するほど白く輝く）────
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.4f, 0.95f, 1f), 0f),
                new GradientColorKey(new Color(1f, 0.8f, 1f),    0.7f),
                new GradientColorKey(Color.white,                1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.85f, 0f),
                new GradientAlphaKey(1f,    0.8f),
                new GradientAlphaKey(1f,    1f)
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(gradient);

        // ── Size over Lifetime（収束の最後に一瞬だけ膨らんでフラッシュ風に）──
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        var sizeCurve = new AnimationCurve(
            new Keyframe(0f,   1f),
            new Keyframe(0.85f,0.8f),
            new Keyframe(0.95f,1.6f),
            new Keyframe(1f,   0f)
        );
        sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // ── Renderer ──────────────────────────────────────
        var ren = GetComponent<ParticleSystemRenderer>();
        ren.renderMode     = ParticleSystemRenderMode.Billboard;
        ren.sortingLayerID = 0;
        ren.sortingOrder   = 5;

#if UNITY_EDITOR
        AssignMaterial(ren, "M_OrbGlow_Additive");
        MarkDirty();
#endif
        Debug.Log("[ConvergeBurstVfxSetup] Converge Burst の設定が完了しました。");
    }

#if UNITY_EDITOR
    private void AssignMaterial(ParticleSystemRenderer ren, string matName)
    {
        var guids = AssetDatabase.FindAssets($"t:Material {matName}");
        if (guids.Length == 0)
        {
            Debug.LogWarning($"[ConvergeBurstVfxSetup] マテリアル '{matName}' が見つかりませんでした。Inspector から手動でアサインしてください。");
            return;
        }
        var path = AssetDatabase.GUIDToAssetPath(guids[0]);
        var mat  = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null)
        {
            ren.sharedMaterial = mat;
            Debug.Log($"[ConvergeBurstVfxSetup] マテリアル '{path}' を自動アサインしました。");
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
