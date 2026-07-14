using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GyroWard（Area07雑魚敵・三眼の羅針虫）専用コントローラー。
/// EnemyMoverのLissajousは使わず、独自にリサジュー曲線+周期的な速度緩急ループを計算し、
/// 進行方向にスプライト自体を回転させる（円形デザインのためflipXではなく回転で表現）。
///
/// 分裂時は Instantiate(gameObject) ではなく EnemySpawner.SpawnCloneGyroWard() を使う（SlimeEnemy方式）。
/// </summary>
[RequireComponent(typeof(EnemyStats))]
[RequireComponent(typeof(EnemyDamageReceiver))]
public class GyroWardController : MonoBehaviour
{
    // =========================================================
    // Editor Preview（GuardBeast/ShamanControllerと同じ構成パターン）
    // =========================================================

    public enum GyroWardPreviewSprite
    {
        Idle1, Idle2, Idle3, IdleAnimate
    }

    [System.Serializable]
    public class GyroWardFrame
    {
        public Sprite  sprite;
        public Vector2 offset;
        [Tooltip("表示秒数")]
        public float   duration;
        [Tooltip("未使用（他エネミーとのフレーム構造統一のため保持）")]
        public Vector2 muzzleOffset;
        [Tooltip("未使用（当たり判定はRoot固定のため）")]
        public Vector2 colliderSize;
        [Tooltip("未使用")]
        public Vector2 colliderOffset;
    }

    // =========================================================
    // References
    // =========================================================

    [Header("References")]
    [Tooltip("本体スプライト（子オブジェクトBody）。未設定なら子から自動取得する")]
    [SerializeField] private SpriteRenderer bodySpriteRenderer;

    [Header("Editor Preview")]
    [SerializeField] private GyroWardPreviewSprite previewSprite = GyroWardPreviewSprite.Idle1;

    [Header("Sprites")]
    [Tooltip("目の発光強度違いの3枚（Idle1=現行/Idle2=中間/Idle3=最大輝度）。この順でループ再生する")]
    [NonReorderable]
    [SerializeField] private GyroWardFrame[] idleFrames;

    // =========================================================
    // Fade In（GyroWard専用。EnemyStats.FadeIn()はRoot上にSpriteRendererが無く効かないため独自実装）
    // =========================================================

    [Header("Fade In")]
    [Tooltip("出現時のフェードイン時間（秒）。0以下で無効")]
    [SerializeField] private float fadeInDuration = 0.5f;

    // =========================================================
    // Lissajous Movement
    // =========================================================

    [Header("Lissajous Movement")]
    [SerializeField] private float lissajousAmplitudeX = 2.5f;
    [SerializeField] private float lissajousAmplitudeY = 1.5f;
    [SerializeField] private float lissajousFrequencyX = 0.5f;
    [SerializeField] private float lissajousFrequencyY = 0.7f;
    [Tooltip("初期位相（度）。分裂で増える個体はEnemySpawnerが自動でこれに+120度ずつ加算する")]
    [SerializeField] private float lissajousPhaseDeg = 0f;

    // =========================================================
    // Speed Loop（ジェットコースター型の緩急ループ）
    // =========================================================

    [Header("Speed Loop（遅→速→遅を周期的に繰り返す）")]
    [Tooltip("1周期の長さ（秒）")]
    [SerializeField] private float speedLoopDuration = 3f;
    [Tooltip("横軸=周期内の進行割合(0→1)、縦軸=速度倍率の重み(0→1)。既定は緩急が山なりに変化するイージング")]
    [SerializeField] private AnimationCurve speedLoopCurve = new AnimationCurve(
        new Keyframe(0f,   0f),
        new Keyframe(0.5f, 1f),
        new Keyframe(1f,   0f)
    );
    [SerializeField] private float speedLoopMinMultiplier = 0.35f;
    [SerializeField] private float speedLoopMaxMultiplier = 1.6f;

    // =========================================================
    // Rotation（進行方向追従・コンパス針型）
    // =========================================================

    [Header("Rotation")]
    [Tooltip("進行方向への回転の追従速度（度/秒の上限）。0で即座にスナップ")]
    [SerializeField] private float rotationFollowSpeed = 720f;
    [Tooltip("この距離未満の移動量では回転角を更新しない（曲線の頂点付近での震え防止）")]
    [SerializeField] private float minMoveDeltaForRotation = 0.0005f;

    // =========================================================
    // Split Settings
    // =========================================================

    [Header("Split Settings")]
    [Tooltip("画面上に存在できるGyroWardの最大数（分裂後の総数）")]
    [SerializeField] private int maxOnScreen = 3;
    [Tooltip("分裂の位相オフセット（度）。分裂のたびにこの角度をずらして重なりを防ぐ")]
    [SerializeField] private float splitPhaseOffsetDeg = 120f;
    [Tooltip("同一個体が連続して分裂できない最短間隔（秒）")]
    [SerializeField] private float splitCooldown = 0.5f;

    [Header("Split SE")]
    [SerializeField] private AudioClip splitClip;
    [SerializeField] private AudioSource splitAudioSource;
    [Range(0f, 1f)] [SerializeField] private float splitSeVolume = 1f;

    [Header("Split Spawn Settings")]
    [SerializeField] private float spawnMinDistance = 2f;
    [SerializeField] private float spawnMaxDistance = 0f;
    [Range(0f, 1f)] [SerializeField] private float spawnYMinPercent = 0.4f;
    [Range(0f, 1f)] [SerializeField] private float spawnYMaxPercent = 0.9f;
    [SerializeField] private int maxSpawnAttempts = 15;
    [SerializeField] private float skillHudPixelWidth = 280f;

    // =========================================================
    // Screen Bounds（スポーン時に軌道を画面内へ自動補正）
    // =========================================================

    [Header("Screen Bounds")]
    [Tooltip("画面端からの安全マージン（ワールド単位）")]
    [SerializeField] private float screenMargin = 0.5f;
    [Tooltip("本体の見た目の半径（ワールド単位）。画面端クランプでこの分の余白も確保する")]
    [SerializeField] private float bodyRadius = 0.55f;

    [Header("Floor Avoidance")]
    [Tooltip("Floorオブジェクトへの参照。未設定の場合はシーンからFloorHealthコンポーネントで自動検索する")]
    [SerializeField] private FloorHealth floorObject;
    [Tooltip("Floorの上端からこの距離以内に軌道が入らないようにする（ワールド単位）")]
    [SerializeField] private float floorAvoidDistance = 1.5f;

    // =========================================================
    // 定数・スタティック
    // =========================================================

    private static readonly List<GyroWardController> activeGyroWards = new List<GyroWardController>();

    // =========================================================
    // インスタンス変数
    // =========================================================

    private EnemyStats stats;
    private EnemyDamageReceiver damageReceiver;
    private EnemyMover enemyMover;

    private float SlowMultiplier => (enemyMover != null) ? enemyMover.SpeedMultiplier : 1f;
    private static float MasterSEVolume => SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f;

    private Vector3 lissajousCenter;
    private float effectiveAmplitudeX;
    private float effectiveAmplitudeY;
    private float lissajousTime;
    private float speedLoopTimer;
    private bool isFirstFrame;
    private float currentZAngle;

    private float lastSplitTime = -999f;
    private float floorAvoidY = float.MinValue;
    private Coroutine fadeInCoroutine;

    private int idleFrameIndex = 0;
    private float idleFrameTimer = 0f;

    [HideInInspector] public bool phaseInitializedBySpawner = false;

    // =========================================================
    // Unity ライフサイクル
    // =========================================================

    private void Awake()
    {
        stats = GetComponent<EnemyStats>();
        damageReceiver = GetComponent<EnemyDamageReceiver>();
        enemyMover = GetComponentInParent<EnemyMover>();

        if (bodySpriteRenderer == null) bodySpriteRenderer = GetComponentInChildren<SpriteRenderer>();

        // 独自移動のため、EnemyMover自体の移動処理は止める（B4デバフ/スロー効果の受け口としてのみ利用）
        if (enemyMover != null) enemyMover.suppressMovement = true;
    }

    private void OnEnable()
    {
        if (!activeGyroWards.Contains(this))
            activeGyroWards.Add(this);

        if (damageReceiver != null)
            damageReceiver.OnReflectedBulletHit += HandleBulletHit;

        lissajousCenter = transform.position;
        CacheFloorY();
        ClampOrbitToScreen();
        lissajousTime = 0f;
        speedLoopTimer = 0f;
        currentZAngle = transform.eulerAngles.z;
        isFirstFrame = true;

        idleFrameIndex = 0;
        idleFrameTimer = 0f;
        ApplyIdleFrame(0);

        if (fadeInCoroutine != null) StopCoroutine(fadeInCoroutine);
        fadeInCoroutine = StartCoroutine(FadeInBody());
    }

    /// <summary>
    /// GyroWard専用フェードイン。本体スプライトのアルファを0→1にする（Body子オブジェクト対応）。
    /// </summary>
    private IEnumerator FadeInBody()
    {
        if (bodySpriteRenderer == null || fadeInDuration <= 0f) yield break;

        Color original = bodySpriteRenderer.color;
        bodySpriteRenderer.color = new Color(original.r, original.g, original.b, 0f);

        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime * GetTimeScale();
            float alpha = Mathf.Clamp01(elapsed / fadeInDuration);
            bodySpriteRenderer.color = new Color(original.r, original.g, original.b, alpha);
            yield return null;
        }

        bodySpriteRenderer.color = new Color(original.r, original.g, original.b, 1f);
    }

    /// <summary>
    /// Floorオブジェクトの上端Y座標をキャッシュする（SlimeEnemy.CacheFloorY()と同じ考え方）。
    /// </summary>
    private void CacheFloorY()
    {
        if (floorObject == null)
            floorObject = FindObjectOfType<FloorHealth>();

        if (floorObject != null)
        {
            Collider2D col = floorObject.GetComponent<Collider2D>();
            float floorTopY = col != null
                ? col.bounds.max.y
                : floorObject.transform.position.y;

            floorAvoidY = floorTopY + floorAvoidDistance;
        }

        Debug.Log($"[GyroWardController] CacheFloorY: enemy={gameObject.name}, floorObjectFound={floorObject != null}, floorAvoidY={floorAvoidY}, spawnY={transform.position.y}");
    }

    /// <summary>
    /// スポーン時に1回だけ実行。軌道（中心+振幅）が画面内・Floor回避距離内に収まるよう、
    /// まず中心を内側へシフトし、それでも収まらない場合のみ振幅を縮める。
    /// </summary>
    private void ClampOrbitToScreen()
    {
        effectiveAmplitudeX = lissajousAmplitudeX;
        effectiveAmplitudeY = lissajousAmplitudeY;

        if (Camera.main == null) return;

        float halfH = Camera.main.orthographicSize;
        float halfW = halfH * Camera.main.aspect;
        Vector3 camPos = Camera.main.transform.position;

        float skillHudWorldOffset = 0f;
        if (skillHudPixelWidth > 0f && Screen.width > 0)
        {
            float worldUnitsPerPixel = (halfW * 2f) / Screen.width;
            skillHudWorldOffset = skillHudPixelWidth * worldUnitsPerPixel;
        }

        // screenMarginは画面端からの余白。本体の見た目の端（bodyRadius）が
        // その余白の内側に収まるよう、中心座標側にさらにbodyRadius分を足し込む
        float screenXMin = camPos.x - halfW + screenMargin + bodyRadius + skillHudWorldOffset;
        float screenXMax = camPos.x + halfW - screenMargin - bodyRadius;
        float screenYMin = camPos.y - halfH + screenMargin + bodyRadius;
        float screenYMax = camPos.y + halfH - screenMargin - bodyRadius;

        // Floor回避距離とスクリーン下端の大きい方をY下限として使用
        if (floorAvoidY > float.MinValue)
            screenYMin = Mathf.Max(screenYMin, floorAvoidY);

        // X軸：振幅が画面内側の幅より広ければ縮小し、中心を画面内に収まる位置へシフト
        float availableHalfWidth = Mathf.Max(0f, (screenXMax - screenXMin) * 0.5f);
        if (effectiveAmplitudeX > availableHalfWidth) effectiveAmplitudeX = availableHalfWidth;

        float minCx = screenXMin + effectiveAmplitudeX;
        float maxCx = screenXMax - effectiveAmplitudeX;
        lissajousCenter.x = (minCx <= maxCx)
            ? Mathf.Clamp(lissajousCenter.x, minCx, maxCx)
            : (screenXMin + screenXMax) * 0.5f;

        // Y軸も同様
        float availableHalfHeight = Mathf.Max(0f, (screenYMax - screenYMin) * 0.5f);
        if (effectiveAmplitudeY > availableHalfHeight) effectiveAmplitudeY = availableHalfHeight;

        float minCy = screenYMin + effectiveAmplitudeY;
        float maxCy = screenYMax - effectiveAmplitudeY;
        lissajousCenter.y = (minCy <= maxCy)
            ? Mathf.Clamp(lissajousCenter.y, minCy, maxCy)
            : (screenYMin + screenYMax) * 0.5f;

        Debug.Log($"[GyroWardController] ClampOrbitToScreen: enemy={gameObject.name}, screenYMin={screenYMin}, screenYMax={screenYMax}, effectiveAmplitudeY={effectiveAmplitudeY}, centerY={lissajousCenter.y}, lowestOrbitY={lissajousCenter.y - effectiveAmplitudeY}");
    }

    private void OnDisable()
    {
        activeGyroWards.Remove(this);

        if (damageReceiver != null)
            damageReceiver.OnReflectedBulletHit -= HandleBulletHit;
    }

    private void OnDestroy()
    {
        activeGyroWards.Remove(this);
    }

    private float GetTimeScale() =>
        SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;

    private void Update()
    {
        float dt = Time.deltaTime * GetTimeScale() * SlowMultiplier;

        ApplyLissajousMoveAndRotate(dt);
        TickIdleFrames(dt);
    }

    // =========================================================
    // 移動 + 回転
    // =========================================================

    private void ApplyLissajousMoveAndRotate(float dt)
    {
        if (isFirstFrame)
        {
            isFirstFrame = false;
            return; // 初回フレームは位置を変えない（生成直後のちらつき防止）
        }

        // Speed Loop: 遅→速→遅を周期的に繰り返す倍率を求める
        speedLoopTimer += dt;
        float loopT = (speedLoopDuration > 0.0001f)
            ? Mathf.Repeat(speedLoopTimer / speedLoopDuration, 1f)
            : 0f;
        float loopWeight = speedLoopCurve.Evaluate(loopT);
        float speedFactor = Mathf.Lerp(speedLoopMinMultiplier, speedLoopMaxMultiplier, loopWeight);

        lissajousTime += dt * speedFactor;

        float phaseRad = lissajousPhaseDeg * Mathf.Deg2Rad;
        float x = lissajousCenter.x + Mathf.Sin(lissajousTime * lissajousFrequencyX * Mathf.PI * 2f + phaseRad) * effectiveAmplitudeX;
        float y = lissajousCenter.y + Mathf.Sin(lissajousTime * lissajousFrequencyY * Mathf.PI * 2f) * effectiveAmplitudeY;

        Vector3 prevPos = transform.position;
        Vector3 newPos = new Vector3(x, y, prevPos.z);
        Vector3 delta = newPos - prevPos;

        transform.position = newPos;

        // Rotation(B): 進行方向に追従（コンパス針型）。Speed Loopで移動量が変わるため回転の緩急も自然に連動する
        if (delta.sqrMagnitude >= minMoveDeltaForRotation * minMoveDeltaForRotation)
        {
            float targetAngle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            currentZAngle = (rotationFollowSpeed > 0f)
                ? Mathf.MoveTowardsAngle(currentZAngle, targetAngle, rotationFollowSpeed * dt)
                : targetAngle;
            transform.rotation = Quaternion.Euler(0f, 0f, currentZAngle);
        }
    }

    // =========================================================
    // Idle発光ループ（本体スプライトを3枚差し替えて目の発光強度を変える）
    // =========================================================

    private void TickIdleFrames(float dt)
    {
        if (idleFrames == null || idleFrames.Length == 0) return;

        idleFrameTimer += dt;
        GyroWardFrame current = idleFrames[idleFrameIndex % idleFrames.Length];
        float dur = (current != null && current.duration > 0f) ? current.duration : 0.3f;

        if (idleFrameTimer >= dur)
        {
            idleFrameTimer -= dur;
            idleFrameIndex++;
            ApplyIdleFrame(idleFrameIndex);
        }
    }

    private void ApplyIdleFrame(int index)
    {
        GyroWardFrame f = GetIdleFrame(((index % SafeIdleLength()) + SafeIdleLength()) % SafeIdleLength());
        if (f == null) return;

        if (f.sprite != null && bodySpriteRenderer != null) bodySpriteRenderer.sprite = f.sprite;
        ApplyOffset(f.offset);
    }

    private int SafeIdleLength() => (idleFrames != null && idleFrames.Length > 0) ? idleFrames.Length : 1;

    private void ApplyOffset(Vector2 offset)
    {
        if (bodySpriteRenderer == null) return;
        Vector3 local = bodySpriteRenderer.transform.localPosition;
        bodySpriteRenderer.transform.localPosition = new Vector3(offset.x, offset.y, local.z);
    }

    private GyroWardFrame GetIdleFrame(int index)
    {
        if (idleFrames == null || index < 0 || index >= idleFrames.Length) return null;
        return idleFrames[index];
    }

    private GyroWardFrame GetPreviewFrame(GyroWardPreviewSprite ps)
    {
        switch (ps)
        {
            case GyroWardPreviewSprite.Idle1: return GetIdleFrame(0);
            case GyroWardPreviewSprite.Idle2: return GetIdleFrame(1);
            case GyroWardPreviewSprite.Idle3: return GetIdleFrame(2);
            default: return null;
        }
    }

    private void OnValidate()
    {
        if (Application.isPlaying) return;
#if UNITY_EDITOR
        if (bodySpriteRenderer == null)
            bodySpriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (bodySpriteRenderer == null) return;

        if (previewSprite == GyroWardPreviewSprite.IdleAnimate)
        {
            StartEditorAnim();
        }
        else
        {
            StopEditorAnim();
            var f = GetPreviewFrame(previewSprite);
            if (f != null)
            {
                if (f.sprite != null) bodySpriteRenderer.sprite = f.sprite;
                ApplyOffset(f.offset);
            }
        }
        UnityEditor.SceneView.RepaintAll();
#endif
    }

#if UNITY_EDITOR
    private bool _editorAnimRunning;
    private double _editorAnimLastTime;
    private int _editorAnimFrameIdx;

    private void StartEditorAnim()
    {
        _editorAnimFrameIdx = 0;
        _editorAnimLastTime = UnityEditor.EditorApplication.timeSinceStartup;
        if (!_editorAnimRunning)
        {
            _editorAnimRunning = true;
            UnityEditor.EditorApplication.update += OnEditorUpdate;
        }
        var f0 = GetIdleFrame(0);
        if (f0 != null && f0.sprite != null && bodySpriteRenderer != null)
        {
            bodySpriteRenderer.sprite = f0.sprite;
            ApplyOffset(f0.offset);
        }
    }

    private void StopEditorAnim()
    {
        if (!_editorAnimRunning) return;
        _editorAnimRunning = false;
        UnityEditor.EditorApplication.update -= OnEditorUpdate;
    }

    private void OnEditorUpdate()
    {
        if (this == null) { StopEditorAnim(); return; }
        if (idleFrames == null || idleFrames.Length == 0) return;

        double now = UnityEditor.EditorApplication.timeSinceStartup;
        GyroWardFrame current = idleFrames[_editorAnimFrameIdx % idleFrames.Length];
        float dur = (current != null && current.duration > 0f) ? current.duration : 0.3f;

        if (now - _editorAnimLastTime >= dur)
        {
            _editorAnimLastTime = now;
            _editorAnimFrameIdx++;
            GyroWardFrame nf = idleFrames[_editorAnimFrameIdx % idleFrames.Length];
            if (nf != null && nf.sprite != null && bodySpriteRenderer != null)
            {
                bodySpriteRenderer.sprite = nf.sprite;
                ApplyOffset(nf.offset);
            }
            UnityEditor.SceneView.RepaintAll();
        }
    }
#endif

    // =========================================================
    // 分裂ロジック（SlimeEnemy方式を移植）
    // =========================================================

    private void HandleBulletHit(int damage, bool isJust)
    {
        if (isJust) return;

        if (Time.time - lastSplitTime < splitCooldown) return;

        for (int i = activeGyroWards.Count - 1; i >= 0; i--)
        {
            if (activeGyroWards[i] == null) activeGyroWards.RemoveAt(i);
        }
        if (activeGyroWards.Count >= maxOnScreen) return;

        lastSplitTime = Time.time;
        TrySpawnSplit();
    }

    private void TrySpawnSplit()
    {
        EnemySpawner spawner = stats.GetSpawner();
        if (spawner == null) return;

        EnemyShooter shooter = GetComponent<EnemyShooter>();
        if (shooter == null) return;

        EnemyData data = shooter.GetEnemyData();
        if (data == null) return;

        Vector3? spawnPos = FindSpawnPosition();
        if (!spawnPos.HasValue) return;

        float newPhase = lissajousPhaseDeg + splitPhaseOffsetDeg;

        spawner.SpawnCloneGyroWard(data, spawnPos.Value, stats.HP, newPhase);
        PlaySplitSE();
    }

    private void PlaySplitSE()
    {
        if (splitClip == null) return;
        float volume = splitSeVolume * MasterSEVolume;
        if (splitAudioSource != null)
            splitAudioSource.PlayOneShot(splitClip, volume);
        else
            AudioSource.PlayClipAtPoint(splitClip, transform.position, volume);
    }

    /// <summary>
    /// EnemySpawner.SpawnCloneGyroWard() から呼ばれる。位相をずらして重なりを防ぐ。
    /// </summary>
    public void SetLissajousPhaseOffsetDeg(float phaseDeg)
    {
        lissajousPhaseDeg = phaseDeg;
        phaseInitializedBySpawner = true;
    }

    private Vector3? FindSpawnPosition()
    {
        if (Camera.main == null) return null;

        float halfH = Camera.main.orthographicSize;
        float halfW = halfH * Camera.main.aspect;
        Vector3 camPos = Camera.main.transform.position;

        float skillHudWorldOffset = 0f;
        if (skillHudPixelWidth > 0f && Screen.width > 0)
        {
            float worldUnitsPerPixel = (halfW * 2f) / Screen.width;
            skillHudWorldOffset = skillHudPixelWidth * worldUnitsPerPixel;
        }

        float screenHeight = halfH * 2f;
        float yMin = camPos.y - halfH + screenHeight * spawnYMinPercent;
        float yMax = camPos.y - halfH + screenHeight * spawnYMaxPercent;
        float xMin = camPos.x - halfW + skillHudWorldOffset;
        float xMax = camPos.x + halfW;

        for (int i = 0; i < maxSpawnAttempts; i++)
        {
            float x = Random.Range(xMin, xMax);
            float y = Random.Range(yMin, yMax);
            Vector3 candidate = new Vector3(x, y, transform.position.z);

            float dist = Vector3.Distance(candidate, transform.position);
            if (dist >= spawnMinDistance && (spawnMaxDistance <= 0f || dist <= spawnMaxDistance))
                return candidate;
        }

        return null;
    }
}
