using UnityEngine;

// タイル張り(Tiled)されたシームレスな雲テクスチャの上を、表示位置（視点）自体が
// 上方向へ上昇し続けながら左右にWave（波打つ）軌道を描くようにパンする演出。
// 速度は一定ではなく、ランダムな速さをランダムな時間だけ維持し続けるのを
// 繰り返すことで、緩急のある不規則な上昇気流の動きにする。
// テクスチャそのものは回転・拡大せず、SpriteRenderer.drawMode=Tiled（+テクスチャの
// Wrap Mode=Repeat）で継ぎ目なく敷き詰めた上で、transform位置を動かすことで
// 「雲が渦を巻いて上昇していく」ように見せる。
//
// 前提（Unity Editor側の設定）:
// - 対象Spriteのimport設定: Mesh Type = Full Rect, Wrap Mode = Repeat
// - テクスチャ自体が上下左右シームレスに繋がるタイル画像であること
// - このコンポーネントが付くGameObjectのtransform.localScaleは(1,1,1)のままにする
//   （タイル範囲のサイズはSpriteRenderer.sizeで直接計算するため、scaleを掛けると二重スケールになる）
//
// 注意: Background_MidはAll Area共通のSpriteRendererで、既定のMaterialは
// Mat_FogAdditive（加算合成、Fog/Rain/Steam等の薄い靄演出向け）。不透明な一枚絵の
// 雲を表示するこのモードでは加算合成だと白飛び・霞んで見えるため、overrideMaterial
// に通常のアルファブレンド用Material（Sprites-Default等）を割り当てて上書きする。
[RequireComponent(typeof(SpriteRenderer))]
public class VortexScroll : MonoBehaviour
{
    [Header("Material")]
    [Tooltip("既定のMat_FogAdditive（加算合成）を上書きする通常のアルファブレンドMaterial。未設定なら上書きしない")]
    [SerializeField] private Material overrideMaterial;

    [Header("Wave")]
    [Tooltip("左右Waveの振幅（ワールド単位）。大きいほど横揺れが大きくなる")]
    [Range(0.5f, 20f)]
    [SerializeField] private float orbitRadius = 12f;

    [Tooltip("左右Waveの周波数。大きいほど細かく素早く揺れる")]
    [Range(0.1f, 10f)]
    [SerializeField] private float waveFrequency = 1f;

    [Header("Speed Variation")]
    [Tooltip("上昇速度の最小値（ワールド単位/秒）")]
    [Range(1f, 240f)]
    [SerializeField] private float minAngularSpeed = 30f;

    [Tooltip("上昇速度の最大値（ワールド単位/秒）")]
    [Range(1f, 360f)]
    [SerializeField] private float maxAngularSpeed = 180f;

    [Tooltip("1つの速度を維持する時間の最小値（秒）")]
    [Range(0.2f, 10f)]
    [SerializeField] private float minHoldDuration = 1f;

    [Tooltip("1つの速度を維持する時間の最大値（秒）")]
    [Range(0.2f, 10f)]
    [SerializeField] private float maxHoldDuration = 4f;

    [Tooltip("速度が切り替わる際の変化率（ワールド単位/秒^2）。大きいほど素早く目標速度に切り替わる")]
    [Range(10f, 1000f)]
    [SerializeField] private float speedTransitionRate = 200f;

    [Header("Tile Coverage")]
    [Tooltip("画面サイズ＋Wave振幅・タイル高さぶんの余白に、さらに追加で確保する安全マージン（ワールド単位）")]
    [Range(0f, 5f)]
    [SerializeField] private float extraMargin = 2f;

    private SpriteRenderer sr;
    private Material originalMaterial;
    private Vector3 basePosition;
    private float tileHeight;
    private float risenDistance;
    private float wavePhase;
    private float currentSpeed;
    private float targetSpeed;
    private float holdTimer;
    private bool initialized;

    private float TimeScale =>
        SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if (!initialized)
        {
            if (sr.sprite == null) return;
            Initialize();
        }

        float dt = Time.deltaTime * TimeScale;

        // 維持時間が切れたら、新しい目標速度と維持時間を抽選し直す
        holdTimer -= dt;
        if (holdTimer <= 0f)
        {
            targetSpeed = Random.Range(minAngularSpeed, maxAngularSpeed);
            holdTimer = Random.Range(minHoldDuration, maxHoldDuration);
        }

        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, speedTransitionRate * dt);

        // 上昇距離を蓄積し、タイル高さでラップして無限に上昇し続けるように見せる
        // （テクスチャが上下シームレスなタイルである前提）
        risenDistance += currentSpeed * dt;
        float wrappedY = tileHeight > 0f ? risenDistance % tileHeight : 0f;

        // 左右のWaveは上昇速度に連動させ、速い時ほど揺れも大きく感じるようにする
        wavePhase += currentSpeed * dt * waveFrequency;
        float waveX = Mathf.Sin(wavePhase * Mathf.Deg2Rad) * orbitRadius;

        // 視点が上昇して見えるようにするには、背景の絵自体は逆に下方向へ動かす
        transform.position = basePosition + new Vector3(waveX, -wrappedY, 0f);
    }

    private void Initialize()
    {
        basePosition = transform.position;
        sr.drawMode = SpriteDrawMode.Tiled;

        if (overrideMaterial != null)
        {
            originalMaterial = sr.sharedMaterial;
            sr.material = overrideMaterial;
        }

        tileHeight = sr.sprite.bounds.size.y * transform.lossyScale.y;

        Camera cam = Camera.main;
        if (cam != null)
        {
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;
            // transformが動いてもカメラの表示範囲を常に覆えるよう、
            // 画面サイズ＋Wave振幅・タイル高さぶんの余白＋追加マージンでタイル範囲を確保する
            sr.size = new Vector2(
                halfWidth * 2f + orbitRadius * 2f + extraMargin,
                halfHeight * 2f + tileHeight * 2f + extraMargin);
        }

        currentSpeed = minAngularSpeed;
        targetSpeed = Random.Range(minAngularSpeed, maxAngularSpeed);
        holdTimer = Random.Range(minHoldDuration, maxHoldDuration);

        initialized = true;
    }

    private void OnDisable()
    {
        // Background_Midは他Areaと共用のため、無効化時は元のMaterialに戻しておく
        if (originalMaterial != null && sr != null)
            sr.material = originalMaterial;
    }
}
