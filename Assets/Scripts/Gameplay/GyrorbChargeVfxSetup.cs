using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

/// <summary>
/// PS_GyrorbCharge を一括設定するセットアップスクリプト。
/// Gyrorb自身の周囲に円周上で粒子を生成し、中心（Gyrorb本体）へ収束しながら
/// 緑に発光する「エネルギー充填」演出。VFX_ConvergeBurstと同じ収束構造を
/// Gyrorbの体格に合わせて縮小し、色を緑（体表の宝石色）に変更したもの。
/// 実行後はこのコンポーネントを Remove Component して削除してください。
/// </summary>
[DisallowMultipleComponent]
public class GyrorbChargeVfxSetup : MonoBehaviour
{
    [ContextMenu("Setup Gyrorb Charge (このオブジェクトに実行)")]
    private void SetupGyrorbCharge()
    {
        var ps = GetComponent<ParticleSystem>();
        if (ps == null)
        {
            Debug.LogError("[GyrorbChargeVfxSetup] ParticleSystem が見つかりません。VFX_GyrorbCharge にアタッチしてください。");
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
        main.startSize         = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
        main.startColor        = new ParticleSystem.MinMaxGradient(new Color(0.2f, 0.85f, 0.35f, 1f), new Color(0.6f, 1f, 0.5f, 1f));
        main.gravityModifier   = 0f;
        main.simulationSpace   = ParticleSystemSimulationSpace.Local;
        main.playOnAwake       = false;
        main.maxParticles      = 40;

        // ── Emission（発射時に1回だけバースト）─────────────
        var emission = ps.emission;
        emission.enabled       = true;
        emission.rateOverTime  = new ParticleSystem.MinMaxCurve(0f);
        emission.SetBursts(new ParticleSystem.Burst[]
        {
            new ParticleSystem.Burst(0f, 28)
        });

        // ── Shape（Gyrorb本体を囲む円周上のみから発生させ、収束の始点を揃える）──
        var shape = ps.shape;
        shape.enabled          = true;
        shape.shapeType        = ParticleSystemShapeType.Circle;
        shape.radius           = 1.2f;
        shape.radiusThickness  = 0f;

        // ── Velocity over Lifetime（中心＝Gyrorb本体へ収束する動きの本体）──
        var vol = ps.velocityOverLifetime;
        vol.enabled = true;
        vol.space   = ParticleSystemSimulationSpace.Local;
        // 半径1.2を寿命0.5秒で中心まで引き寄せる（-radius/lifetime）
        vol.radial  = new ParticleSystem.MinMaxCurve(-2.4f);

        // ── Color over Lifetime（収束するほど明るい緑〜白に輝く）────
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.15f, 0.7f, 0.3f), 0f),
                new GradientColorKey(new Color(0.6f, 1f, 0.5f),    0.7f),
                new GradientColorKey(Color.white,                  1f)
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
        ren.renderMode       = ParticleSystemRenderMode.Billboard;
        ren.sortingLayerID   = 0;
        ren.sortingOrder     = 10;
        ren.maxParticleSize  = 2f;

#if UNITY_EDITOR
        AssignMaterial(ren, "M_OrbGlow_Additive");
        MarkDirty();
#endif
        Debug.Log("[GyrorbChargeVfxSetup] Gyrorb Charge の設定が完了しました。");
    }

#if UNITY_EDITOR
    private void AssignMaterial(ParticleSystemRenderer ren, string matName)
    {
        var guids = AssetDatabase.FindAssets($"t:Material {matName}");
        if (guids.Length == 0)
        {
            Debug.LogWarning($"[GyrorbChargeVfxSetup] マテリアル '{matName}' が見つかりませんでした。Inspector から手動でアサインしてください。");
            return;
        }
        var path = AssetDatabase.GUIDToAssetPath(guids[0]);
        var mat  = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null)
        {
            ren.sharedMaterial = mat;
            Debug.Log($"[GyrorbChargeVfxSetup] マテリアル '{path}' を自動アサインしました。");
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
