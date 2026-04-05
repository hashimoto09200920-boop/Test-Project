using UnityEngine;

/// <summary>
/// シールド有効中エフェクト（VFX_ShieldActive）のパラメータをInspectorで管理する。
///
/// 使い方:
///   1. 空のGameObjectを作り、子に PS_Orbs・PS_Ring を Unity Editor で手動追加する
///      （ParticleSystem コンポーネントを Add Component → Unity が正しいマテリアルを自動設定する）
///   2. 本コンポーネントをルートに追加し、PS_Orbs・PS_Ring を Inspector にアサインする
///   3. Inspector でパラメータを調整する
///   4. 右クリック →「Apply VFX Settings」で ParticleSystem に焼き込む
///   5. Prefab として保存し、EnemyData の shieldActiveEffectPrefab に設定する
/// </summary>
[DisallowMultipleComponent]
public class ShieldActiveVfxController : MonoBehaviour
{
    // =============================================
    // References
    // =============================================
    [Header("References")]
    [SerializeField] private GameObject psOrbsObject;
    [SerializeField] private GameObject psRingObject;

    // =============================================
    // Orbs（周回する光の粒）
    // =============================================
    [Header("Orbs - 周回する光の粒")]
    [Tooltip("敵を周回する円の半径。敵のサイズに合わせて調整する。")]
    [SerializeField] private float orbRadius = 0.55f;

    [Tooltip("1秒あたりの放出数。多いほど粒が密になる。")]
    [SerializeField] private float orbEmitRate = 5f;

    [Tooltip("粒の寿命（秒）。半径と周回速度に合わせて調整する。")]
    [SerializeField] private float orbLifetime = 4f;

    [Tooltip("粒のサイズ。")]
    [SerializeField] private float orbSize = 0.07f;

    [Tooltip("Z軸周りの周回速度（度/秒）。正=反時計回り。")]
    [SerializeField] private float orbOrbitalSpeed = 90f;

    [Tooltip("粒の色グラデーション（生成→消滅）。最後はアルファ0にするとフェードアウトする。")]
    [SerializeField] private Gradient orbColorGradient;

    // =============================================
    // Ring（拡散するパルスリング）
    // =============================================
    [Header("Ring - 拡散するパルスリング")]
    [Tooltip("パルスリングの放出半径（敵中心から）。小さいほど1点から広がるように見える。")]
    [SerializeField] private float ringRadius = 0.05f;

    [Tooltip("バーストの間隔（秒）。短いほど頻繁にリングが広がる。")]
    [SerializeField] private float ringInterval = 2.5f;

    [Tooltip("1バーストで出るパーティクル数。多いほどリングが密になる。")]
    [SerializeField] private int ringBurstCount = 40;

    [Tooltip("パーティクルの放出速度（外向き）。大きいほど素早く広がる。")]
    [SerializeField] private float ringSpeed = 2.0f;

    [Tooltip("パーティクルの寿命（秒）。速度と合わせてリングの広がり具合が決まる。")]
    [SerializeField] private float ringLifetime = 0.8f;

    [Tooltip("パーティクルのサイズ。")]
    [SerializeField] private float ringParticleSize = 0.06f;

    [Tooltip("リングの色グラデーション（生成→消滅）。最後はアルファ0にするとフェードアウトする。")]
    [SerializeField] private Gradient ringColorGradient;

    // =============================================
    // Unity Callbacks
    // =============================================
    private void Reset()
    {
        orbColorGradient  = CreateDefaultOrbGradient();
        ringColorGradient = CreateDefaultRingGradient();
    }

    private void Awake()
    {
        ApplySettings();
    }

    // =============================================
    // ContextMenu
    // =============================================

    /// <summary>
    /// Inspector のパラメータを ParticleSystem に適用する。
    /// Play前に右クリック → Apply VFX Settings で Prefab に焼き込む。
    /// </summary>
    [ContextMenu("Apply VFX Settings")]
    public void ApplySettings()
    {
        if (psOrbsObject == null) psOrbsObject = FindChildByNameTrimmed("PS_Orbs");
        if (psRingObject == null) psRingObject = FindChildByNameTrimmed("PS_Ring");

        ApplyOrbs();
        ApplyRing();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.EditorUtility.SetDirty(this);
            if (psOrbsObject != null)
            {
                var ps = psOrbsObject.GetComponent<ParticleSystem>();
                if (ps != null) UnityEditor.EditorUtility.SetDirty(ps);
            }
            if (psRingObject != null)
            {
                var ps = psRingObject.GetComponent<ParticleSystem>();
                if (ps != null) UnityEditor.EditorUtility.SetDirty(ps);
            }
        }
#endif
    }

    // =============================================
    // Apply methods
    // =============================================
    private void ApplyOrbs()
    {
        if (psOrbsObject == null) return;
        ParticleSystem psOrbs = psOrbsObject.GetComponent<ParticleSystem>();
        if (psOrbs == null) return;
        if (orbColorGradient == null) orbColorGradient = CreateDefaultOrbGradient();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.Undo.RecordObject(psOrbs, "Apply Shield Orbs VFX");
            AssignDefaultMaterial(psOrbsObject);
        }
#endif

        // Main
        var main = psOrbs.main;
        main.loop          = true;
        main.playOnAwake   = true;
        main.startLifetime = orbLifetime;
        main.startSpeed    = 0f;
        main.startSize     = orbSize;
        main.startColor    = Color.white;
        main.scalingMode   = ParticleSystemScalingMode.Hierarchy;

        // Emission
        var emission = psOrbs.emission;
        emission.enabled      = true;
        emission.rateOverTime = orbEmitRate;

        // Shape: 円の縁から放出
        var shape = psOrbs.shape;
        shape.enabled         = true;
        shape.shapeType       = ParticleSystemShapeType.Circle;
        shape.radius          = orbRadius;
        shape.radiusThickness = 0f;

        // Velocity over Lifetime: Z軸周回
        var vel = psOrbs.velocityOverLifetime;
        vel.enabled  = true;
        vel.space    = ParticleSystemSimulationSpace.Local;
        vel.orbitalZ = orbOrbitalSpeed;

        // Color over Lifetime
        var col = psOrbs.colorOverLifetime;
        col.enabled = true;
        col.color   = new ParticleSystem.MinMaxGradient(orbColorGradient);
    }

    private void ApplyRing()
    {
        if (psRingObject == null) return;
        ParticleSystem psRing = psRingObject.GetComponent<ParticleSystem>();
        if (psRing == null) return;
        if (ringColorGradient == null) ringColorGradient = CreateDefaultRingGradient();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.Undo.RecordObject(psRing, "Apply Shield Ring VFX");
            AssignDefaultMaterial(psRingObject);
        }
#endif

        // Main
        var main = psRing.main;
        main.loop          = true;
        main.playOnAwake   = true;
        main.startLifetime = ringLifetime;
        main.startSpeed    = ringSpeed;
        main.startSize     = ringParticleSize;
        main.startColor    = Color.white;
        main.scalingMode   = ParticleSystemScalingMode.Hierarchy;

        // Emission: rateOverTime=0、バーストのみ
        var emission = psRing.emission;
        emission.enabled      = true;
        emission.rateOverTime = 0f;

        var burst = new ParticleSystem.Burst(0f, (short)ringBurstCount);
        burst.cycleCount     = 0;
        burst.repeatInterval = ringInterval;
        emission.SetBursts(new[] { burst });

        // Shape: 小さい円の縁から外向きに放出
        var shape = psRing.shape;
        shape.enabled         = true;
        shape.shapeType       = ParticleSystemShapeType.Circle;
        shape.radius          = ringRadius;
        shape.radiusThickness = 0f;

        // Color over Lifetime
        var col = psRing.colorOverLifetime;
        col.enabled = true;
        col.color   = new ParticleSystem.MinMaxGradient(ringColorGradient);

        // Size over Lifetime: 少し膨張しながら消える
        var sizeOL = psRing.sizeOverLifetime;
        sizeOL.enabled = true;
        sizeOL.size    = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 1.8f));
    }

    /// <summary>
    /// VFX_Explosion_A_RingSparks と同じデフォルトマテリアル（GUID固定）を割り当てる。
    /// マテリアルが null のままだと紫（エラーカラー）になるため必ず実行する。
    /// </summary>
    private static void AssignDefaultMaterial(GameObject go)
    {
#if UNITY_EDITOR
        if (go == null) return;
        ParticleSystemRenderer r = go.GetComponent<ParticleSystemRenderer>();
        if (r == null || r.sharedMaterial != null) return;

        const string guid = "a97c105638bdf8b4a8650670310a4cd3";
        string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path)) return;

        Material mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) return;

        UnityEditor.Undo.RecordObject(r, "Assign Particle Material");
        r.sharedMaterial = mat;
        UnityEditor.EditorUtility.SetDirty(r);
#endif
    }

    /// <summary>子オブジェクトを名前（前後スペース無視）で検索する</summary>
    private GameObject FindChildByNameTrimmed(string name)
    {
        foreach (Transform child in transform)
        {
            if (child.name.Trim() == name.Trim())
                return child.gameObject;
        }
        return null;
    }

    // =============================================
    // Default gradients
    // =============================================
    private static Gradient CreateDefaultOrbGradient()
    {
        var g = new Gradient();
        g.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.4f, 0.8f, 1.0f), 0.0f),
                new GradientColorKey(new Color(0.7f, 0.95f, 1.0f), 0.5f),
                new GradientColorKey(new Color(1.0f, 1.0f, 1.0f), 1.0f),
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.0f, 0.0f),
                new GradientAlphaKey(0.9f, 0.2f),
                new GradientAlphaKey(0.9f, 0.7f),
                new GradientAlphaKey(0.0f, 1.0f),
            }
        );
        return g;
    }

    private static Gradient CreateDefaultRingGradient()
    {
        var g = new Gradient();
        g.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(new Color(0.6f, 0.95f, 1.0f), 0.0f),
                new GradientColorKey(new Color(1.0f, 1.0f, 1.0f), 0.3f),
                new GradientColorKey(new Color(0.4f, 0.7f, 1.0f), 1.0f),
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1.0f, 0.0f),
                new GradientAlphaKey(0.6f, 0.5f),
                new GradientAlphaKey(0.0f, 1.0f),
            }
        );
        return g;
    }
}
