using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(ParticleSystem))]
public class AmmoEjectSetup : MonoBehaviour
{
    [Header("Size")]
    [SerializeField] private float shellSize = 0.15f;

    [Header("Direction (behind enemy)")]
    [Tooltip("排出方向の中心角度（度）。X=-90で画面上方向。調整してマズルの後ろ側に合わせる")]
    [SerializeField] private Vector3 shapeRotation = new Vector3(90f, 0f, 0f);
    [Tooltip("広がりの角度（度）。大きいほどランダム感が増す")]
    [SerializeField] private float spreadAngle = 50f;

    [Header("Rendering")]
    [Tooltip("NMスプライト(Order 2)より大きい値にする。3以上推奨")]
    [SerializeField] private int sortingOrder = 3;

    [Header("Physics")]
    [SerializeField] private float startSpeed    = 3f;
    [Tooltip("ライフタイム終了時の速度倍率（0=完全停止、0.5=半分）")]
    [SerializeField] private float speedEndMultiplier = 0.1f;
    [SerializeField] private float lifetime      = 1.0f;

    [ContextMenu("Setup Ammo Eject Particle")]
    private void Setup()
    {
        ParticleSystem ps = GetComponent<ParticleSystem>();

        // ---- Main ----
        var main = ps.main;
        main.duration          = 0.1f;
        main.loop              = false;
        main.playOnAwake       = true;
        main.startLifetime     = new ParticleSystem.MinMaxCurve(lifetime);
        main.startSpeed        = new ParticleSystem.MinMaxCurve(startSpeed);
        main.startSize         = new ParticleSystem.MinMaxCurve(shellSize);
        main.startRotation     = new ParticleSystem.MinMaxCurve(-Mathf.PI, Mathf.PI);
        main.gravityModifier   = new ParticleSystem.MinMaxCurve(0f);
        main.stopAction        = ParticleSystemStopAction.Destroy;
        main.maxParticles      = 10;
        main.simulationSpace   = ParticleSystemSimulationSpace.World;

        // ---- Emission ----
        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

        // ---- Shape ----
        var shape = ps.shape;
        shape.enabled   = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle     = spreadAngle;
        shape.radius    = 0.01f;
        shape.rotation  = shapeRotation;

        // ---- Velocity over Lifetime (deceleration) ----
        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        var speedCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(1f, speedEndMultiplier)
        );
        vel.speedModifier = new ParticleSystem.MinMaxCurve(1f, speedCurve);
        vel.space = ParticleSystemSimulationSpace.Local;

        // ---- Rotation over Lifetime ----
        var rot = ps.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(
            -360f * Mathf.Deg2Rad,
            -180f * Mathf.Deg2Rad
        );

        // ---- Renderer + Material ----
#if UNITY_EDITOR
        var psr = GetComponent<ParticleSystemRenderer>();
        psr.renderMode = ParticleSystemRenderMode.Billboard;

        psr.sortingLayerID = 0; // Default layer
        psr.sortingOrder   = sortingOrder;

        const string texPath = "Assets/Art/Enemy/S3_03_IronNest/Ammo.png";
        const string matPath = "Assets/Art/Enemy/S3_03_IronNest/Mat_Ammo.mat";

        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        if (tex == null)
        {
            Debug.LogWarning($"[AmmoEjectSetup] Ammo.png が見つかりません: {texPath}");
        }
        else
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Sprites/Default"));
                mat.mainTexture = tex;
                AssetDatabase.CreateAsset(mat, matPath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[AmmoEjectSetup] Mat_Ammo.mat を作成しました: {matPath}");
            }
            psr.material = mat;
        }

        EditorUtility.SetDirty(gameObject);
        Debug.Log("[AmmoEjectSetup] セットアップ完了");
#endif
    }
}
