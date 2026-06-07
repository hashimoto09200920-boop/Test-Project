using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 弾発射マズルフラッシュVFXのParticleSystemを自動設定するセットアップスクリプト
/// 手順: 1) Materialを設定 → 2) ContextMenu "Setup Muzzle Flash VFX" を実行
///       3) Prefabとして保存 → 4) EnemyData > Bullet Types > Fire Vfx Prefab にセット
/// </summary>
public class MuzzleFlashVFXSetup : MonoBehaviour
{
    [Header("Material")]
    [Tooltip("Flash・Sparks共通マテリアル（URP: Particles/Unlit、Blend=Additive推奨）")]
    [SerializeField] private Material particleMaterial;

    [Header("Rendering")]
    [Tooltip("ソートレイヤー名（弾や敵より手前にしたい場合は調整）")]
    [SerializeField] private string sortingLayerName = "Default";
    [Tooltip("ソートオーダー（大きいほど手前）")]
    [SerializeField] private int sortingOrder = 10;

    [Header("Flash（中央フラッシュ）")]
    [Tooltip("フラッシュの最大サイズ（ワールド単位）")]
    [SerializeField] private float flashSize = 0.8f;

    [Header("Sparks（放射スパーク）")]
    [Tooltip("スパークの本数")]
    [SerializeField] private int sparkCount = 7;
    [Tooltip("スパークの初速（Min〜Max）")]
    [SerializeField] private Vector2 sparkSpeed = new Vector2(2f, 5f);
    [Tooltip("スパークの寿命（Min〜Max）秒")]
    [SerializeField] private Vector2 sparkLifetime = new Vector2(0.35f, 0.55f);
    [Tooltip("スパークの幅サイズ（Min〜Max）")]
    [SerializeField] private Vector2 sparkSize = new Vector2(0.12f, 0.20f);
    [Tooltip("スパークの広がり角度（Coneの半角）。65=前方130°に広がる")]
    [SerializeField] private float sparkAngle = 65f;

    [ContextMenu("Setup Muzzle Flash VFX")]
    private void Setup()
    {
        // Root ParticleSystem（Flash）の取得 or 追加
        var ps = GetComponent<ParticleSystem>();
        if (ps == null)
            ps = gameObject.AddComponent<ParticleSystem>();

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        // 既存のSparks子を再生成
        var existingSparks = transform.Find("Sparks");
        if (existingSparks != null)
            DestroyImmediate(existingSparks.gameObject);

        // ─── Flash（中央フラッシュ: 1パーティクル、ポップして消える）───

        var main = ps.main;
        // duration + startLifetime = 0.9 + 0.12 = 1.02s → AutoDestroyVfx がスパーク(0.8s)消滅前に GO を破棄しない
        main.duration          = 0.9f;
        main.loop              = false;
        main.startLifetime     = 0.12f;
        main.startSpeed        = 0f;
        main.startSize         = flashSize;
        main.startColor        = new Color(2f, 2f, 2f, 1f); // HDR白（眩しく光る）
        main.maxParticles      = 1;
        main.playOnAwake       = true;
        main.simulationSpace   = ParticleSystemSimulationSpace.World;

        // Emission: t=0 に1発バースト
        var emission = ps.emission;
        emission.enabled       = true;
        emission.rateOverTime  = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 1) });

        // Shape: 無効（発生点から動かない）
        var shape = ps.shape;
        shape.enabled = false;

        // Size over Lifetime: 0 → 1 → 0（ポン、と膨らんで消える）
        var sizeOL = ps.sizeOverLifetime;
        sizeOL.enabled = true;
        var sizeCurve = new AnimationCurve(
            new Keyframe(0f,   0f),
            new Keyframe(0.3f, 1f),
            new Keyframe(1f,   0f)
        );
        sizeOL.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // Color over Lifetime: 白 → シアン、フェードアウト
        var colorOL = ps.colorOverLifetime;
        colorOL.enabled = true;
        var flashGrad = new Gradient();
        flashGrad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(Color.white,               0f),
                new GradientColorKey(new Color(0.4f, 1f, 1f),  0.5f),
                new GradientColorKey(new Color(0f,  0.8f, 1f), 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 0.4f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOL.color = new ParticleSystem.MinMaxGradient(flashGrad);

        var flashRenderer = ps.GetComponent<ParticleSystemRenderer>();
        flashRenderer.sortingLayerName = sortingLayerName;
        flashRenderer.sortingOrder     = sortingOrder;
        if (particleMaterial != null)
            flashRenderer.sharedMaterial = particleMaterial;

        // ─── Sparks（放射スパーク: 数本が四方に飛び散る）─────────────

        var sparksGO = new GameObject("Sparks");
        sparksGO.transform.SetParent(transform);
        sparksGO.transform.localPosition = Vector3.zero;
        var sparksPS = sparksGO.AddComponent<ParticleSystem>();

        var sMain = sparksPS.main;
        sMain.duration        = 1.0f;
        sMain.loop            = false;
        sMain.startLifetime   = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
        sMain.startSpeed      = 0f; // 速度はMuzzleFlash2DEmitterがEmitParamsで設定
        sMain.startSize       = new ParticleSystem.MinMaxCurve(0.06f, 0.10f);
        sMain.startColor      = new ParticleSystem.MinMaxGradient(
            new Color(0.6f, 1f, 1f), // シアン
            Color.white
        );
        sMain.maxParticles    = sparkCount + 3;
        sMain.playOnAwake     = true;
        sMain.simulationSpace = ParticleSystemSimulationSpace.World;

        // Emission: 自動放射なし。MuzzleFlash2DEmitter.Emit() で手動放射する
        var sEmission = sparksPS.emission;
        sEmission.enabled      = true;
        sEmission.rateOverTime = 0f;
        sEmission.SetBursts(new ParticleSystem.Burst[0]);

        // Shape: 無効。速度はMuzzleFlash2DEmitterがEmitParamsで直接設定する
        // （Shapeモジュールを有効にするとZ方向にも放射され、2Dカメラで360°に見えてしまう）
        var sShape = sparksPS.shape;
        sShape.enabled = false;

        // Size over Lifetime: 直線フェードアウト
        var sSizeOL = sparksPS.sizeOverLifetime;
        sSizeOL.enabled = true;
        var sSizeCurve = new AnimationCurve(
            new Keyframe(0f, 1f),
            new Keyframe(1f, 0f)
        );
        sSizeOL.size = new ParticleSystem.MinMaxCurve(1f, sSizeCurve);

        // Color over Lifetime: 白 → シアン、フェードアウト
        var sColorOL = sparksPS.colorOverLifetime;
        sColorOL.enabled = true;
        var sparksGrad = new Gradient();
        sparksGrad.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(Color.white,              0f),
                new GradientColorKey(new Color(0f, 0.9f, 1f), 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        sColorOL.color = new ParticleSystem.MinMaxGradient(sparksGrad);

        var sparksRenderer = sparksPS.GetComponent<ParticleSystemRenderer>();
        sparksRenderer.sortingLayerName = sortingLayerName;
        sparksRenderer.sortingOrder     = sortingOrder;
        sparksRenderer.renderMode       = ParticleSystemRenderMode.Stretch;
        sparksRenderer.velocityScale    = 0.4f;
        sparksRenderer.lengthScale      = 6f;
        // pivot.y = -1: クワッドが粒子位置から前方（進行方向）に伸びる。後ろ方向には伸びない
        sparksRenderer.pivot            = new Vector3(0f, -1f, 0f);
        // Flash と同じマテリアルを流用
        sparksRenderer.sharedMaterial   = flashRenderer.sharedMaterial;

        // ─── MuzzleFlash2DEmitter（2D専用スパーク放射コンポーネント）─────
        // Shapeモジュールの代わりにコードで XY 平面内の速度を設定する
        var existing2D = GetComponent<MuzzleFlash2DEmitter>();
        if (existing2D != null) DestroyImmediate(existing2D);
        var mf2d = gameObject.AddComponent<MuzzleFlash2DEmitter>();
        mf2d.sparkCount   = 8;
        mf2d.halfAngleDeg = 70f;   // 140° total
        mf2d.speedMin     = 3f;
        mf2d.speedMax     = 6f;
        mf2d.lifetimeMin  = 0.5f;
        mf2d.lifetimeMax  = 0.8f;
        mf2d.sizeMin      = 0.06f;
        mf2d.sizeMax      = 0.10f;

#if UNITY_EDITOR
        EditorUtility.SetDirty(gameObject);
        EditorUtility.SetDirty(sparksGO);
#endif
        Debug.Log("[MuzzleFlashVFXSetup] 完了。Prefabとして保存して EnemyData > Bullet Types > Fire Vfx Prefab にセットしてください。");
    }
}
