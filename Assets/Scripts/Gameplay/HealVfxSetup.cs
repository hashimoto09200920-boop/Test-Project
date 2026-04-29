using UnityEngine;

/// <summary>
/// HealVFX Prefab のParticleSystemを自動設定するセットアップスクリプト。
/// ContextMenuで一度だけ実行してPrefab化する。実行後このコンポーネントは削除してよい。
/// </summary>
[RequireComponent(typeof(ParticleSystem))]
public class HealVfxSetup : MonoBehaviour
{
    [ContextMenu("Setup Heal VFX")]
    private void Setup()
    {
        var ps = GetComponent<ParticleSystem>();

        // Main
        var main = ps.main;
        main.duration           = 0.5f;
        main.loop               = false;
        main.startLifetime      = new ParticleSystem.MinMaxCurve(0.6f);
        main.startSpeed         = new ParticleSystem.MinMaxCurve(1.5f);
        main.startSize          = new ParticleSystem.MinMaxCurve(0.06f);
        // startColorはHealVfxColorizerが各粒子に設定するため未指定（白をデフォルト）
        main.startColor         = new ParticleSystem.MinMaxGradient(Color.white);
        main.gravityModifierMultiplier = -0.3f;
        main.stopAction         = ParticleSystemStopAction.Destroy;
        main.playOnAwake        = true;
        main.simulationSpace    = ParticleSystemSimulationSpace.World;

        // Emission: HealVfxColorizerが手動発射するためバーストは無効
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[0]);

        // Shape: Circle
        var shape = ps.shape;
        shape.enabled       = true;
        shape.shapeType     = ParticleSystemShapeType.Circle;
        shape.radius        = 0.15f;

        // Color over Lifetime: alpha フェードアウト
        // Color over Lifetime: 白固定でアルファだけフェードアウト（色はStart Colorで制御）
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var fadeGrad = new Gradient();
        fadeGrad.SetKeys(
            new GradientColorKey[] {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new GradientAlphaKey[] {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(fadeGrad);

        // Size over Lifetime: 1 → 0（右下がり）
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        var sizeCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(1f, 0f)
        );
        sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        Debug.Log("[HealVfxSetup] Setup完了。Prefab化してこのコンポーネントを削除してください。");
    }
}
