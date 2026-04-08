using UnityEngine;
using UnityEditor;

/// <summary>
/// 長押し赤線切り替えエフェクト（リング拡散パーティクル）を生成するEditorスクリプト。
/// メニュー: Tools → Create VFX → LongPressActivate
/// </summary>
public class CreateLongPressActivateVFX
{
    [MenuItem("Tools/Create VFX/LongPressActivate")]
    public static void Create()
    {
        // ルートGameObject
        GameObject root = new GameObject("VFX_LongPressActivate");

        // ParticleSystem追加
        ParticleSystem ps = root.AddComponent<ParticleSystem>();
        ParticleSystemRenderer psr = root.GetComponent<ParticleSystemRenderer>();

        // ----- Main -----
        var main = ps.main;
        main.duration          = 0.5f;
        main.loop              = false;
        main.startLifetime     = 0.4f;
        main.startSpeed        = 0f;
        main.startSize         = 0.25f;
        main.startColor        = new Color(1f, 0.25f, 0.25f, 1f);
        main.gravityModifier   = 0f;
        main.stopAction        = ParticleSystemStopAction.Destroy;
        main.simulationSpace   = ParticleSystemSimulationSpace.World;

        // ----- Emission -----
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        var burst = new ParticleSystem.Burst(0f, 30);
        emission.SetBursts(new ParticleSystem.Burst[] { burst });

        // ----- Shape -----
        var shape = ps.shape;
        shape.enabled      = true;
        shape.shapeType    = ParticleSystemShapeType.Circle;
        shape.radius       = 0.05f;
        shape.radiusThickness = 0f; // エッジから放出

        // ----- Velocity over Lifetime -----
        var vol = ps.velocityOverLifetime;
        vol.enabled = true;
        vol.radial  = new ParticleSystem.MinMaxCurve(4f);
        vol.space   = ParticleSystemSimulationSpace.Local;

        // ----- Size over Lifetime -----
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(0.6f, 1.2f),
            new Keyframe(1f, 0f)
        );
        sol.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // ----- Color over Lifetime -----
        var col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(1f, 0.25f, 0.25f), 0f),
                new GradientColorKey(new Color(1f, 0.6f, 0.6f),  0.5f),
                new GradientColorKey(new Color(1f, 1f, 1f),       1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 0.5f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        col.color = new ParticleSystem.MinMaxGradient(grad);

        // ----- Renderer -----
        psr.renderMode = ParticleSystemRenderMode.Billboard;
        // デフォルトのParticle/Standardマテリアルを使用
        psr.material = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Particle.mat");

        // ----- Prefab保存 -----
        string savePath = "Assets/Prefabs/Effects/VFX_LongPressActivate.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, savePath);
        Object.DestroyImmediate(root);

        if (prefab != null)
        {
            Debug.Log($"[CreateLongPressActivateVFX] 作成成功: {savePath}");
            Selection.activeObject = prefab;
            EditorGUIUtility.PingObject(prefab);
        }
        else
        {
            Debug.LogError("[CreateLongPressActivateVFX] Prefab作成失敗");
        }
    }
}
