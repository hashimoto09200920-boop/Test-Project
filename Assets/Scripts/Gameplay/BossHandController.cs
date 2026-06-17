using System.Collections;
using UnityEngine;

public class BossHandController : MonoBehaviour
{
    [System.Serializable]
    public class BossHandFrame
    {
        public Sprite sprite;
        public float offsetX = 0f;
        public float offsetY = 0f;
        public float durationMin = 0.07f;
        public float durationMax = 0.13f;
        [Tooltip("このフレームでのFingerTip_02のローカル位置（DollA用・左②指先）")]
        public Vector2 fingerTipOffset;
        [Tooltip("このフレームでのFingerTip_06のローカル位置（DollB用・右⑥指先）")]
        public Vector2 fingerTip06Offset;
        [Tooltip("このフレームでのFingerTip_03のローカル位置（③Player糸用）")]
        public Vector2 fingerTip03Offset;
        [Tooltip("このフレームでのFingerTip_05のローカル位置（⑤Player糸用）")]
        public Vector2 fingerTip05Offset;
        [Tooltip("マスク当たり判定サイズ（Width, Height）")]
        public Vector2 colliderSize = new Vector2(3f, 4f);
        [Tooltip("マスク当たり判定の回転Z（度）")]
        public float colliderRotationZ = 0f;
        [Tooltip("マスク当たり判定のオフセット（BossHandローカル座標）")]
        public Vector2 colliderOffset = Vector2.zero;
        [Tooltip("目コライダーのサイズ（Width, Height）— BackJitterFrames用")]
        public Vector2 eyeColliderSize = new Vector2(0.5f, 0.5f);
        [Tooltip("目コライダーのオフセット（BossHandローカル座標）— BackJitterFrames用")]
        public Vector2 eyeColliderOffset = Vector2.zero;
    }

    [Header("Front Phase Sprites")]
    [NonReorderable]
    [SerializeField] private BossHandFrame[] frontJitterFrames;

    [Header("Back Phase Sprites")]
    [NonReorderable]
    [SerializeField] private BossHandFrame[] backIdleFrames;
    [NonReorderable]
    [SerializeField] private BossHandFrame[] backJitterFrames;

    [Header("Jitter Settings")]
    [Tooltip("Jitterのフレームレート（デフォルト14fps）")]
    [SerializeField] private float jitterFps = 14f;

    [Header("Phase Transition")]
    [Tooltip("表→裏フェーズ移行時のフェードアウト時間（秒）")]
    [SerializeField] private float phaseTransitionFadeDuration = 0.5f;
    [Tooltip("裏フェーズのフェードイン時間（秒）")]
    [SerializeField] private float phaseTransitionFadeInDuration = 3f;
    [Tooltip("表→裏フェーズ移行するHP閾値（%）")]
    [Range(1f, 99f)]
    [SerializeField] private float phaseTransitionHpThreshold = 70f;

    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [Tooltip("フレームごとにSize/RotationZ/Offsetを更新するコライダー（BossHand子のGameObjectに配置）")]
    [SerializeField] private Collider2D maskHitCollider;
    [Tooltip("初期位置スポーン点（SP_05）")]
    [SerializeField] private Transform spawnPoint;
    [Tooltip("糸接続点（FingerTip_02）— DollA用・左②指先・フレームごとにlocalPositionを更新")]
    [SerializeField] private Transform fingerTip02;
    [Tooltip("糸接続点（FingerTip_06）— DollB用・右⑥指先・フレームごとにlocalPositionを更新")]
    [SerializeField] private Transform fingerTip06;
    [Tooltip("糸接続点（FingerTip_03）— ③Player糸用・フレームごとにlocalPositionを更新")]
    [SerializeField] private Transform fingerTip03;
    [Tooltip("糸接続点（FingerTip_05）— ⑤Player糸用・フレームごとにlocalPositionを更新")]
    [SerializeField] private Transform fingerTip05;
    [Tooltip("PlayerString_03/05を管理するOrchestrator（裏フェーズ移行時に起動）")]
    [SerializeField] private PlayerStringOrchestrator playerStringOrchestrator;

    [Header("Editor Preview — Back Jitter (Play前確認用)")]
    [Tooltip("-1=オフ、0以上=backJitterFramesのインデックスを静止表示")]
    [SerializeField] private int previewFrame = -1;
    [Tooltip("チェックでJitterをEditorでループ再生")]
    [SerializeField] private bool previewAnimate = false;

    [Header("Editor Preview — Front Jitter (Play前確認用)")]
    [Tooltip("-1=オフ、0以上=frontJitterFramesのインデックスを静止表示")]
    [SerializeField] private int previewFrontFrame = -1;
    [Tooltip("チェックでFront JitterをEditorでループ再生")]
    [SerializeField] private bool previewFrontAnimate = false;

    [HideInInspector] [SerializeField] private Vector3 editorBasePos;
    [HideInInspector] [SerializeField] private bool editorBaseCaptured = false;
    [HideInInspector] [SerializeField] private int _prevPreviewFrame = -1;
    [HideInInspector] [SerializeField] private int _prevPreviewFrontFrame = -1;

#if UNITY_EDITOR
    private double _editorAnimLastTime;
    private int    _editorAnimFrame;
    private bool   _editorAnimRunning;
    private float  _editorCurrentInterval;
    private bool   _editorAnimIsFront;
#endif

    [Header("Curse System")]
    [Tooltip("CurseLevel最大値（達した後にヒットで強化フェーズ）")]
    [SerializeField] private int maxCurseLevel = 5;
    [Tooltip("反射弾1ヒットで溜まるCurse量（0.067 = 15ヒットでLv1、30ヒットで強化フェーズ）")]
    [SerializeField] private float cursePerHit = 0.067f;
    [Tooltip("各CurseLevelのパーティクル（maxCurseLevel個分）")]
    [SerializeField] private ParticleSystem[] curseParticles = new ParticleSystem[5];
    [Tooltip("強化フェーズ開始パーティクル")]
    [SerializeField] private ParticleSystem enhancementParticle;
    [Tooltip("強化フェーズ持続時間（秒）")]
    [SerializeField] private float enhancementDuration = 8f;
    [Tooltip("CurseLevelアップ瞬間のバーストパーティクル（未設定なら省略）")]
    [SerializeField] private ParticleSystem curseLevelUpParticle;

    [Header("Enhancement Tint")]
    [Tooltip("強化フェーズ中のスプライトtint色（BossHand・全Doll共通）")]
    [SerializeField] private Color enhancementTintColor = new Color(1f, 0.45f, 0.45f, 1f);

    [Header("Doll References")]
    [Tooltip("強化フェーズtintを適用するDollController一覧（DollA/DollB）")]
    [SerializeField] private DollController[] dolls;

    [Header("Enhancement Attack Speed")]
    [Tooltip("バースト中の攻撃間隔倍率（0.7 = 30%速い・小さいほど速い）")]
    [SerializeField] private float burstFireIntervalMultiplier = 0.7f;
    [Tooltip("ボス本体のEnemyShooter（Fingersルート）")]
    [SerializeField] private EnemyShooter bossShooter;

    [Header("Enhancement Eye Zone")]
    [Tooltip("バースト中にダメージ無効化する目パーツ（EnemyPartコンポーネント）")]
    [SerializeField] private EnemyPart eyePart;
    [Tooltip("バースト中に弾が目に当たった時のボスHP回復量")]
    [SerializeField] private float burstEyeHealAmount = 20f;
    [Tooltip("バースト中に弾が目に当たった時の消滅SE")]
    [SerializeField] private AudioClip burstEyeHitSe;
    [Range(0f, 1f)]
    [SerializeField] private float burstEyeHitSeVolume = 1f;
    [Tooltip("バースト中に弾が目に当たった時のVFXパーティクル（ContextMenu「Setup: Burst Eye Hit Particle」で自動生成）")]
    [SerializeField] private ParticleSystem burstEyeHitParticle;
    [Tooltip("VFX発生位置のeyePart基準ローカルオフセット。「Apply: Eye Hit VFX Position」で反映、Previewで確認")]
    [SerializeField] private Vector3 burstEyeHitVfxLocalOffset = Vector3.zero;

    private int _curseLevel = 0;
    private float _curseAccumulation = 0f;
    private bool _isEnhanced = false;
    private Coroutine _enhancementCo;

    public bool IsEnhanced => _isEnhanced;

    private Coroutine jitterCo;
    private bool isBackPhase = false;
    private bool _phaseTransitionTriggered = false;
    private BossHandFrame _currentFrame;

    private void OnEnable()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        StopEditorAnim();
#endif
    }

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
        if (spawnPoint != null)
            transform.position = spawnPoint.position;
        var mover = GetComponentInParent<EnemyMover>();
        if (mover != null) mover.suppressMovement = true;

        var stats = GetComponentInParent<EnemyStats>();
        if (stats != null) stats.onDamageTaken += OnDamageTaken;
    }

    private void OnDestroy()
    {
        var stats = GetComponentInParent<EnemyStats>();
        if (stats != null) stats.onDamageTaken -= OnDamageTaken;
    }

    private void Start()
    {
        if (frontJitterFrames != null && frontJitterFrames.Length > 0)
            EnterFrontPhase();
        else
            EnterBackPhase();
    }

    // ==============================
    // Front Phase
    // ==============================

    public void EnterFrontPhase()
    {
        isBackPhase = false;

        if (eyePart != null)
        {
            var col = eyePart.GetComponent<Collider2D>();
            if (col != null) col.enabled = false;
        }

        if (frontJitterFrames != null && frontJitterFrames.Length > 0)
            ApplyFrame(frontJitterFrames[0]);

        if (jitterCo != null) StopCoroutine(jitterCo);
        jitterCo = StartCoroutine(FrontJitterLoop());
    }

    private IEnumerator FrontJitterLoop()
    {
        if (frontJitterFrames == null || frontJitterFrames.Length == 0) yield break;

        float defaultInterval = 1f / Mathf.Max(1f, jitterFps);
        Vector3 basePos = transform.position;
        int lastIdx = -1;

        while (!isBackPhase)
        {
            int idx;
            do { idx = Random.Range(0, frontJitterFrames.Length); }
            while (frontJitterFrames.Length > 1 && idx == lastIdx);
            lastIdx = idx;

            var frame = frontJitterFrames[idx];
            ApplyFrame(frame, basePos);
            float wait = (frame != null && frame.durationMax > 0f)
                ? Random.Range(frame.durationMin, frame.durationMax)
                : defaultInterval;
            yield return new WaitForSeconds(wait);
        }
    }

    private void OnDamageTaken()
    {
        if (_phaseTransitionTriggered || isBackPhase) return;
        var stats = GetComponentInParent<EnemyStats>();
        if (stats != null && stats.GetHpPercentage() <= phaseTransitionHpThreshold)
        {
            _phaseTransitionTriggered = true;
            if (jitterCo != null) StopCoroutine(jitterCo);
            jitterCo = StartCoroutine(PhaseTransitionRoutine());
        }
    }

    private IEnumerator PhaseTransitionRoutine()
    {
        // 移行中: ボス当たり判定・攻撃を無効化
        if (maskHitCollider != null) maskHitCollider.enabled = false;
        if (bossShooter != null) bossShooter.enabled = false;
        Collider2D eyeCol = eyePart != null ? eyePart.GetComponent<Collider2D>() : null;
        if (eyeCol != null) eyeCol.enabled = false;

        float elapsed = 0f;
        Color c = spriteRenderer.color;
        while (elapsed < phaseTransitionFadeDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / phaseTransitionFadeDuration);
            spriteRenderer.color = new Color(c.r, c.g, c.b, alpha);
            yield return null;
        }
        spriteRenderer.color = new Color(c.r, c.g, c.b, 0f);
        jitterCo = null; // EnterBackPhase の StopCoroutine(jitterCo) が自分自身を止めないようにクリア
        EnterBackPhase(); // 内部で eyePart collider を有効化するが、フェードイン中は引き続き無効のまま
        if (eyeCol != null) eyeCol.enabled = false;

        elapsed = 0f;
        c = spriteRenderer.color;
        while (elapsed < phaseTransitionFadeInDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, 1f, elapsed / phaseTransitionFadeInDuration);
            spriteRenderer.color = new Color(c.r, c.g, c.b, alpha);
            yield return null;
        }
        spriteRenderer.color = new Color(c.r, c.g, c.b, 1f);

        // 移行完了: 当たり判定・攻撃を再有効化
        if (maskHitCollider != null) maskHitCollider.enabled = true;
        if (eyeCol != null) eyeCol.enabled = true;
        if (bossShooter != null) bossShooter.enabled = true;
    }

    // ==============================
    // Back Phase
    // ==============================

    public void EnterBackPhase()
    {
        isBackPhase = true;

        if (eyePart != null)
        {
            var col = eyePart.GetComponent<Collider2D>();
            if (col != null) col.enabled = true;
        }

        playerStringOrchestrator?.StartStrings();

        if (backIdleFrames != null && backIdleFrames.Length > 0)
            ApplyFrame(backIdleFrames[0]);

        if (jitterCo != null) StopCoroutine(jitterCo);
        jitterCo = StartCoroutine(BackJitterLoop());
    }

    private IEnumerator BackJitterLoop()
    {
        if (backJitterFrames == null || backJitterFrames.Length == 0) yield break;

        float defaultInterval = 1f / Mathf.Max(1f, jitterFps);
        Vector3 basePos = transform.position;
        int lastIdx = -1;

        while (isBackPhase)
        {
            int idx;
            do { idx = Random.Range(0, backJitterFrames.Length); }
            while (backJitterFrames.Length > 1 && idx == lastIdx);
            lastIdx = idx;

            var frame = backJitterFrames[idx];
            ApplyFrame(frame, basePos);
            float wait = (frame != null && frame.durationMax > 0f)
                ? Random.Range(frame.durationMin, frame.durationMax)
                : defaultInterval;
            yield return new WaitForSeconds(wait);
        }
    }

    private void ApplyMaskCollider(BossHandFrame frame)
    {
        if (maskHitCollider == null) return;
        maskHitCollider.transform.localPosition    = new Vector3(frame.colliderOffset.x, frame.colliderOffset.y, 0f);
        maskHitCollider.transform.localEulerAngles = new Vector3(0f, 0f, frame.colliderRotationZ);
        if (maskHitCollider is BoxCollider2D box)
            box.size = frame.colliderSize;
        else if (maskHitCollider is CapsuleCollider2D cap)
            cap.size = frame.colliderSize;
    }

    private void ApplyEyeCollider(BossHandFrame frame)
    {
        if (eyePart == null) return;
        var col = eyePart.GetComponent<Collider2D>();
        if (col == null) return;
        eyePart.transform.localPosition = new Vector3(frame.eyeColliderOffset.x, frame.eyeColliderOffset.y, 0f);
        if (col is BoxCollider2D box)
            box.size = frame.eyeColliderSize;
        else if (col is CapsuleCollider2D cap)
            cap.size = frame.eyeColliderSize;
        else if (col is CircleCollider2D circle)
            circle.radius = frame.eyeColliderSize.x * 0.5f;
    }

    private void ApplyFrame(BossHandFrame frame, Vector3? basePos = null)
    {
        if (frame == null || spriteRenderer == null) return;
        if (frame.sprite != null) spriteRenderer.sprite = frame.sprite;
        if (basePos.HasValue)
            transform.position = basePos.Value + new Vector3(frame.offsetX, frame.offsetY, 0f);
        if (fingerTip02 != null)
            fingerTip02.localPosition = new Vector3(frame.fingerTipOffset.x, frame.fingerTipOffset.y, 0f);
        if (fingerTip06 != null)
            fingerTip06.localPosition = new Vector3(frame.fingerTip06Offset.x, frame.fingerTip06Offset.y, 0f);
        if (fingerTip03 != null)
            fingerTip03.localPosition = new Vector3(frame.fingerTip03Offset.x, frame.fingerTip03Offset.y, 0f);
        if (fingerTip05 != null)
            fingerTip05.localPosition = new Vector3(frame.fingerTip05Offset.x, frame.fingerTip05Offset.y, 0f);
        ApplyMaskCollider(frame);
        ApplyEyeCollider(frame);
        _currentFrame = frame;
    }

    /// <summary>現在表示中フレームのfingerTipOffsetからワールド座標を直接計算して返す</summary>
    public Vector3 GetCurrentFingerTipWorldPos()
    {
        if (_currentFrame == null)
            return fingerTip02 != null ? fingerTip02.position : transform.position;
        return transform.position + new Vector3(
            _currentFrame.fingerTipOffset.x,
            _currentFrame.fingerTipOffset.y, 0f);
    }

    // ==============================
    // Curse System
    // ==============================

    /// <summary>反射弾が糸にヒットするたびDollControllerから呼ぶ</summary>
    public void AddCurse()
    {
        if (!Application.isPlaying) return;
        if (_isEnhanced) return;

        _curseAccumulation += cursePerHit;
        while (_curseAccumulation >= 1f)
        {
            _curseAccumulation -= 1f;
            if (_curseLevel >= maxCurseLevel)
            {
                TriggerEnhancement();
                return;
            }
            _curseLevel++;
            UpdateCurseEffects();
        }
    }

    private void UpdateCurseEffects()
    {
        for (int i = 0; i < curseParticles.Length; i++)
        {
            if (curseParticles[i] == null) continue;
            bool active = i < _curseLevel;
            if (active && !curseParticles[i].isPlaying)
                curseParticles[i].Play();
            else if (!active && curseParticles[i].isPlaying)
                curseParticles[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        if (curseLevelUpParticle != null && _curseLevel > 0)
            curseLevelUpParticle.Play();
    }

    private void TriggerEnhancement()
    {
        _isEnhanced = true;
        ApplyTintAll(enhancementTintColor);
        ApplyBurstAttackSpeed(true);
        SetEyeBurstMode(true);
        foreach (var ps in curseParticles)
            if (ps != null && !ps.isPlaying) ps.Play();
        if (enhancementParticle != null) enhancementParticle.Play();
        if (_enhancementCo != null) StopCoroutine(_enhancementCo);
        _enhancementCo = StartCoroutine(EnhancementRoutine());
    }

    private IEnumerator EnhancementRoutine()
    {
        yield return new WaitForSeconds(enhancementDuration);
        _isEnhanced = false;
        _curseLevel = 0;
        _curseAccumulation = 0f;
        ApplyTintAll(Color.white);
        ApplyBurstAttackSpeed(false);
        SetEyeBurstMode(false);
        UpdateCurseEffects();
        if (enhancementParticle != null)
            enhancementParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        _enhancementCo = null;
    }

    private void ApplyBurstAttackSpeed(bool active)
    {
        System.Action<EnemyShooter> apply = s =>
        {
            if (s == null) return;
            if (active) s.SetFireIntervalMultiplier(burstFireIntervalMultiplier);
            else s.ResetFireIntervalMultiplier();
        };
        apply(bossShooter);
        if (dolls != null)
            foreach (var doll in dolls)
                apply(doll?.GetComponent<EnemyShooter>());
    }

    private void SetEyeBurstMode(bool active)
    {
        if (eyePart == null) return;
        if (active)
        {
            eyePart.suppressDamage = true;
            eyePart.OnHitWhileSuppressed += OnEyeHitDuringBurst;
        }
        else
        {
            eyePart.suppressDamage = false;
            eyePart.OnHitWhileSuppressed -= OnEyeHitDuringBurst;
        }
    }

    private void OnEyeHitDuringBurst(EnemyBullet bullet)
    {
        GetComponentInParent<EnemyStats>()?.Heal(Mathf.RoundToInt(burstEyeHealAmount));

        if (burstEyeHitParticle != null)
            burstEyeHitParticle.Play(true);

        if (bullet == null) return;
        if (burstEyeHitSe != null)
        {
            float vol = burstEyeHitSeVolume * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
            AudioSource.PlayClipAtPoint(burstEyeHitSe, bullet.transform.position, vol);
        }
        Destroy(bullet.gameObject);
    }

    private void ApplyTintAll(Color rgb)
    {
        if (spriteRenderer != null)
        {
            Color c = spriteRenderer.color;
            spriteRenderer.color = new Color(rgb.r, rgb.g, rgb.b, c.a);
        }
        if (dolls != null)
            foreach (var doll in dolls)
                doll?.ApplyEnhancementTint(rgb);
    }

#if UNITY_EDITOR
    [ContextMenu("Setup Curse Particles")]
    private void SetupCurseParticles()
    {
        if (Application.isPlaying) { Debug.LogWarning("Play中は実行不可"); return; }
        int count = Mathf.Max(1, maxCurseLevel);
        curseParticles = new ParticleSystem[count];
        var wispMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Background/Mat_WispParticle.mat");

        for (int i = 0; i < count; i++)
        {
            string goName = $"CurseParticle_{i}";
            var existing = transform.Find(goName);
            if (existing != null) DestroyImmediate(existing.gameObject);

            var go = new GameObject(goName);
            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;

            var ps = go.AddComponent<ParticleSystem>();
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sortingLayerID = spriteRenderer != null ? spriteRenderer.sortingLayerID : 0;
            r.sortingOrder = (spriteRenderer != null ? spriteRenderer.sortingOrder : 6) + 1;
            if (wispMat != null) r.material = wispMat;

            float t = count > 1 ? (float)i / (count - 1) : 1.0f;
            var main = ps.main;
            main.loop = true;
            main.duration = 2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(
                Mathf.Lerp(0.08f, 0.22f, t),
                Mathf.Lerp(0.16f, 0.40f, t));
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1.0f, 0.0f, 0.0f, Mathf.Lerp(0.4f, 0.9f, t)),
                new Color(0.4f, 0.0f, 0.0f, Mathf.Lerp(0.3f, 0.7f, t)));
            main.gravityModifier = -0.02f;
            main.maxParticles = 80;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = Mathf.Lerp(3f, 12f, t);

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = Mathf.Lerp(0.3f, 1.2f, t);

            curseParticles[i] = ps;
        }
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[BossHandController] CurseParticles x{count} 作成完了");
    }

    [ContextMenu("Setup Curse LevelUp Particle")]
    private void SetupCurseLevelUpParticle()
    {
        if (Application.isPlaying) { Debug.LogWarning("Play中は実行不可"); return; }
        var existing = transform.Find("CurseLevelUpParticle");
        if (existing != null) DestroyImmediate(existing.gameObject);

        var go = new GameObject("CurseLevelUpParticle");
        go.transform.SetParent(transform);
        go.transform.localPosition = Vector3.zero;

        var ps = go.AddComponent<ParticleSystem>();
        var r = go.GetComponent<ParticleSystemRenderer>();
        r.sortingLayerID = spriteRenderer != null ? spriteRenderer.sortingLayerID : 0;
        r.sortingOrder = (spriteRenderer != null ? spriteRenderer.sortingOrder : 6) + 2;
        var wispMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Background/Mat_WispParticle.mat");
        if (wispMat != null) r.material = wispMat;

        var main = ps.main;
        main.loop = false;
        main.duration = 0.6f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2.0f, 4.5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.35f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(1.0f, 0.0f, 0.0f, 1f),
            new Color(0.4f, 0.0f, 0.0f, 1f));
        main.maxParticles = 150;
        main.stopAction = ParticleSystemStopAction.None;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 100) });

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.2f;

        curseLevelUpParticle = ps;
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[BossHandController] CurseLevelUpParticle 作成完了");
    }

    [ContextMenu("Preview: Curse 靄 全ON")]
    private void PreviewCurseParticlesOn()
    {
        foreach (var ps in curseParticles)
        {
            if (ps == null) continue;
            ps.Simulate(1.5f, true, true);
            ps.Play();
        }
        UnityEditor.SceneView.RepaintAll();
    }

    [ContextMenu("Preview: CurseLevelUp バースト再生")]
    private void PreviewCurseLevelUpBurst()
    {
        if (curseLevelUpParticle == null) return;
        curseLevelUpParticle.Simulate(0.3f, true, true);
        curseLevelUpParticle.Play();
        UnityEditor.SceneView.RepaintAll();
    }

    [ContextMenu("Fix: SpriteColor を白にリセット (Prefab保存)")]
    private void FixResetSpriteColorsInPrefab()
    {
        if (spriteRenderer != null)
        {
            UnityEditor.Undo.RecordObject(spriteRenderer, "Reset Color");
            spriteRenderer.color = Color.white;
            Debug.Log("[Fix] BossHand SpriteRenderer → white");
        }
        var statsRoot = GetComponentInParent<EnemyStats>();
        if (statsRoot != null)
        {
            foreach (var doll in statsRoot.GetComponentsInChildren<DollController>())
            {
                var sr = doll.GetComponent<SpriteRenderer>();
                if (sr == null) continue;
                UnityEditor.Undo.RecordObject(sr, "Reset Color");
                sr.color = Color.white;
                Debug.Log($"[Fix] {doll.name} SpriteRenderer → white");
            }
        }

        string prefabPath = UnityEditor.PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(gameObject);
        Debug.Log($"[Fix] prefabPath = {prefabPath}");
        if (!string.IsNullOrEmpty(prefabPath))
        {
            var prefabGO = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefabGO != null)
            {
                foreach (var bhc in prefabGO.GetComponentsInChildren<BossHandController>(true))
                {
                    var sr = bhc.GetComponent<SpriteRenderer>();
                    if (sr == null) continue;
                    sr.color = Color.white;
                    UnityEditor.EditorUtility.SetDirty(sr);
                    Debug.Log("[Fix] Prefab BossHand → white");
                }
                foreach (var doll in prefabGO.GetComponentsInChildren<DollController>(true))
                {
                    var sr = doll.GetComponent<SpriteRenderer>();
                    if (sr == null) continue;
                    sr.color = Color.white;
                    UnityEditor.EditorUtility.SetDirty(sr);
                    Debug.Log($"[Fix] Prefab {doll.name} → white");
                }
                UnityEditor.AssetDatabase.SaveAssets();
                Debug.Log("[Fix] Prefab保存完了");
            }
            else
            {
                Debug.LogError("[Fix] Prefabのロードに失敗");
            }
        }
        else
        {
            Debug.LogWarning("[Fix] prefabPathが空。SceneにPrefabインスタンスとして配置されているか確認");
        }
        UnityEditor.SceneView.RepaintAll();
    }

    [ContextMenu("Preview: Enhancement Tint ON")]
    private void PreviewEnhancementTintOn()
    {
        RecordTintUndo();
        ApplyTintAll(enhancementTintColor);
        UnityEditor.SceneView.RepaintAll();
    }

    [ContextMenu("Preview: Enhancement Tint OFF (リセット)")]
    private void PreviewEnhancementTintOff()
    {
        RecordTintUndo();
        ApplyTintAll(Color.white);
        UnityEditor.SceneView.RepaintAll();
    }

    private void RecordTintUndo()
    {
        if (spriteRenderer != null)
            UnityEditor.Undo.RecordObject(spriteRenderer, "Enhancement Tint");
        if (dolls != null)
            foreach (var doll in dolls)
            {
                if (doll == null) continue;
                var sr = doll.GetComponent<SpriteRenderer>();
                if (sr != null) UnityEditor.Undo.RecordObject(sr, "Enhancement Tint");
            }
    }

    [ContextMenu("Setup: Burst Eye Hit Particle")]
    private void SetupBurstEyeHitParticle()
    {
        if (Application.isPlaying) { Debug.LogWarning("Play中は実行不可"); return; }
        if (eyePart == null)
        {
            Debug.LogError("[BossHand] eyePart が未設定です。先に eyePart をアサインしてください。");
            return;
        }

        var wispMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Background/Mat_WispParticle.mat");
        int baseLayer = spriteRenderer != null ? spriteRenderer.sortingLayerID : 0;
        int baseOrder = spriteRenderer != null ? spriteRenderer.sortingOrder : 6;

        Transform existing = eyePart.transform.Find("BurstEyeHitVFX");
        if (existing != null) DestroyImmediate(existing.gameObject);

        var rootGo = new GameObject("BurstEyeHitVFX");
        rootGo.transform.SetParent(eyePart.transform, false);
        rootGo.transform.localPosition = burstEyeHitVfxLocalOffset;

        var psA = rootGo.AddComponent<ParticleSystem>();
        {
            var main = psA.main;
            main.loop = false;
            main.duration = 0.5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 10f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.13f, 0.26f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.8f, 0.2f, 1f, 1f),
                new Color(0.5f, 0f, 0.8f, 1f));
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 120;
            main.playOnAwake = false;
            main.stopAction = ParticleSystemStopAction.None;

            var emission = psA.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 40) });

            var shape = psA.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.08f;

            var col = psA.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(0.9f, 0.5f, 1f), 0f),
                    new GradientColorKey(new Color(0.7f, 0.1f, 1f), 0.5f),
                    new GradientColorKey(new Color(0.3f, 0f, 0.6f), 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.6f, 0.4f),
                    new GradientAlphaKey(0f, 1f)
                });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var rA = psA.GetComponent<ParticleSystemRenderer>();
            rA.sortingLayerID = baseLayer;
            rA.sortingOrder = baseOrder + 2;
            if (wispMat != null) rA.material = wispMat;
        }

        var sparkGo = new GameObject("Spark");
        sparkGo.transform.SetParent(rootGo.transform, false);
        sparkGo.transform.localPosition = Vector3.zero;

        var psB = sparkGo.AddComponent<ParticleSystem>();
        {
            var main = psB.main;
            main.loop = false;
            main.duration = 0.3f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(8f, 20f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 1f, 1f, 1f),
                new Color(1f, 0.8f, 1f, 1f));
            main.gravityModifier = 0.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 120;
            main.playOnAwake = false;
            main.stopAction = ParticleSystemStopAction.None;

            var emission = psB.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 60) });

            var shape = psB.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;

            var col = psB.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(1f, 1f, 1f), 0f),
                    new GradientColorKey(new Color(1f, 0.6f, 1f), 0.4f),
                    new GradientColorKey(new Color(0.8f, 0.2f, 0.9f), 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f)
                });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var rB = psB.GetComponent<ParticleSystemRenderer>();
            rB.renderMode = ParticleSystemRenderMode.Stretch;
            rB.velocityScale = 0.08f;
            rB.lengthScale = 2.5f;
            rB.sortingLayerID = baseLayer;
            rB.sortingOrder = baseOrder + 3;
            if (wispMat != null) rB.material = wispMat;
        }

        burstEyeHitParticle = psA;
        UnityEditor.Undo.RegisterCreatedObjectUndo(rootGo, "Setup Burst Eye Hit Particle");
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[BossHandController] BurstEyeHitVFX 作成完了（案A吸収 + 案Bスパーク）");
    }

    [ContextMenu("Apply: Eye Hit VFX Position")]
    private void ApplyEyeHitVfxPosition()
    {
        if (burstEyeHitParticle == null) { Debug.LogWarning("burstEyeHitParticle 未設定"); return; }
        UnityEditor.Undo.RecordObject(burstEyeHitParticle.transform, "Eye Hit VFX Position");
        burstEyeHitParticle.transform.localPosition = burstEyeHitVfxLocalOffset;
        UnityEditor.EditorUtility.SetDirty(burstEyeHitParticle.transform);
        UnityEditor.SceneView.RepaintAll();
        Debug.Log($"[BossHandController] VFX localPosition → {burstEyeHitVfxLocalOffset}");
    }

    [ContextMenu("Preview: Burst Eye Hit VFX")]
    private void PreviewBurstEyeHitVfx()
    {
        if (burstEyeHitParticle == null) { Debug.LogWarning("burstEyeHitParticle 未設定"); return; }
        burstEyeHitParticle.Simulate(0.15f, true, true);
        burstEyeHitParticle.Play(true);
        UnityEditor.SceneView.RepaintAll();
    }

    [ContextMenu("Preview: 全パーティクル停止")]
    private void PreviewStopAll()
    {
        foreach (var ps in curseParticles)
            if (ps != null) ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (curseLevelUpParticle != null)
            curseLevelUpParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    [ContextMenu("Move to Spawn Point")]
    private void MoveToSpawnPoint()
    {
        if (spawnPoint == null) { Debug.LogWarning("Spawn Point未設定"); return; }
        transform.position = spawnPoint.position;
        UnityEditor.EditorUtility.SetDirty(gameObject);
    }
#endif

    private void OnValidate()
    {
        if (Application.isPlaying) return;

#if UNITY_EDITOR
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();

        if (spawnPoint != null)
        {
            editorBasePos = spawnPoint.position;
            editorBaseCaptured = true;
        }

        // -----------------------------------------------
        // Back Jitter: フレーム切り替え時にfingerTipを自動保存
        // -----------------------------------------------
        if (!previewAnimate
            && fingerTip02 != null
            && _prevPreviewFrame >= 0
            && _prevPreviewFrame != previewFrame
            && backJitterFrames != null
            && _prevPreviewFrame < backJitterFrames.Length)
        {
            var prev = backJitterFrames[_prevPreviewFrame];
            if (prev != null)
            {
                prev.fingerTipOffset   = new Vector2(fingerTip02.localPosition.x, fingerTip02.localPosition.y);
                if (fingerTip06 != null) prev.fingerTip06Offset = new Vector2(fingerTip06.localPosition.x, fingerTip06.localPosition.y);
                if (fingerTip03 != null) prev.fingerTip03Offset = new Vector2(fingerTip03.localPosition.x, fingerTip03.localPosition.y);
                if (fingerTip05 != null) prev.fingerTip05Offset = new Vector2(fingerTip05.localPosition.x, fingerTip05.localPosition.y);
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }
        _prevPreviewFrame = previewFrame;

        // -----------------------------------------------
        // Front Jitter: フレーム切り替え時にfingerTipを自動保存
        // -----------------------------------------------
        if (!previewFrontAnimate
            && fingerTip02 != null
            && _prevPreviewFrontFrame >= 0
            && _prevPreviewFrontFrame != previewFrontFrame
            && frontJitterFrames != null
            && _prevPreviewFrontFrame < frontJitterFrames.Length)
        {
            var prev = frontJitterFrames[_prevPreviewFrontFrame];
            if (prev != null)
            {
                prev.fingerTipOffset   = new Vector2(fingerTip02.localPosition.x, fingerTip02.localPosition.y);
                if (fingerTip06 != null) prev.fingerTip06Offset = new Vector2(fingerTip06.localPosition.x, fingerTip06.localPosition.y);
                if (fingerTip03 != null) prev.fingerTip03Offset = new Vector2(fingerTip03.localPosition.x, fingerTip03.localPosition.y);
                if (fingerTip05 != null) prev.fingerTip05Offset = new Vector2(fingerTip05.localPosition.x, fingerTip05.localPosition.y);
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }
        _prevPreviewFrontFrame = previewFrontFrame;

        // -----------------------------------------------
        // アニメーション開始/停止の優先順位
        // Back → Front の順で排他制御
        // -----------------------------------------------
        if (previewAnimate)
        {
            if (_editorAnimRunning && _editorAnimIsFront) StopEditorAnim();
            StartEditorAnim(isFront: false);
        }
        else if (previewFrontAnimate)
        {
            if (_editorAnimRunning && !_editorAnimIsFront) StopEditorAnim();
            StartEditorAnim(isFront: true);
        }
        else
        {
            StopEditorAnim();

            // Front Jitter 静止表示（previewFrontFrame 0=Idle, 1-4=Jitter）
            if (previewFrontFrame >= 0 && frontJitterFrames != null && previewFrontFrame < frontJitterFrames.Length)
            {
                var f = frontJitterFrames[previewFrontFrame];
                if (f != null)
                {
                    if (f.sprite != null) spriteRenderer.sprite = f.sprite;
                    transform.localPosition = new Vector3(f.offsetX, f.offsetY, 0f);
                    if (fingerTip02 != null) fingerTip02.localPosition = new Vector3(f.fingerTipOffset.x,   f.fingerTipOffset.y,   0f);
                    if (fingerTip06 != null) fingerTip06.localPosition = new Vector3(f.fingerTip06Offset.x, f.fingerTip06Offset.y, 0f);
                    if (fingerTip03 != null) fingerTip03.localPosition = new Vector3(f.fingerTip03Offset.x, f.fingerTip03Offset.y, 0f);
                    if (fingerTip05 != null) fingerTip05.localPosition = new Vector3(f.fingerTip05Offset.x, f.fingerTip05Offset.y, 0f);
                    ApplyMaskCollider(f);
                    ApplyEyeCollider(f);
                }
            }
            // Back Jitter 静止表示
            else if (previewFrame >= 0 && backJitterFrames != null && previewFrame < backJitterFrames.Length)
            {
                var f = backJitterFrames[previewFrame];
                if (f != null)
                {
                    if (f.sprite != null) spriteRenderer.sprite = f.sprite;
                    transform.localPosition = new Vector3(f.offsetX, f.offsetY, 0f);
                    if (fingerTip02 != null) fingerTip02.localPosition = new Vector3(f.fingerTipOffset.x,   f.fingerTipOffset.y,   0f);
                    if (fingerTip06 != null) fingerTip06.localPosition = new Vector3(f.fingerTip06Offset.x, f.fingerTip06Offset.y, 0f);
                    if (fingerTip03 != null) fingerTip03.localPosition = new Vector3(f.fingerTip03Offset.x, f.fingerTip03Offset.y, 0f);
                    if (fingerTip05 != null) fingerTip05.localPosition = new Vector3(f.fingerTip05Offset.x, f.fingerTip05Offset.y, 0f);
                    ApplyMaskCollider(f);
                    ApplyEyeCollider(f);
                }
            }
            // デフォルト: frontJitterFrames[0]（Idle）を表示
            else if (frontJitterFrames != null && frontJitterFrames.Length > 0 && frontJitterFrames[0] != null && frontJitterFrames[0].sprite != null)
            {
                var f = frontJitterFrames[0];
                spriteRenderer.sprite = f.sprite;
                transform.localPosition = new Vector3(f.offsetX, f.offsetY, 0f);
                if (fingerTip02 != null) fingerTip02.localPosition = new Vector3(f.fingerTipOffset.x,   f.fingerTipOffset.y,   0f);
                if (fingerTip06 != null) fingerTip06.localPosition = new Vector3(f.fingerTip06Offset.x, f.fingerTip06Offset.y, 0f);
                if (fingerTip03 != null) fingerTip03.localPosition = new Vector3(f.fingerTip03Offset.x, f.fingerTip03Offset.y, 0f);
                if (fingerTip05 != null) fingerTip05.localPosition = new Vector3(f.fingerTip05Offset.x, f.fingerTip05Offset.y, 0f);
                ApplyMaskCollider(f);
                ApplyEyeCollider(f);
            }
            else
            {
                transform.localPosition = Vector3.zero;
            }
        }
#endif
    }

#if UNITY_EDITOR
    private float CalcEditorInterval(BossHandFrame frame)
    {
        return (frame != null && frame.durationMax > 0f)
            ? Random.Range(frame.durationMin, frame.durationMax)
            : 1f / Mathf.Max(1f, jitterFps);
    }

    private void StartEditorAnim(bool isFront = false)
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        var frames = isFront ? frontJitterFrames : backJitterFrames;
        if (frames == null || frames.Length == 0) return;

        _editorAnimIsFront = isFront;
        if (!_editorAnimRunning)
        {
            _editorAnimFrame = 0;
            _editorAnimLastTime = UnityEditor.EditorApplication.timeSinceStartup;
            _editorAnimRunning = true;
            _editorCurrentInterval = CalcEditorInterval(frames[0]);
            UnityEditor.EditorApplication.update += OnEditorUpdate;
        }

        ApplyEditorFrame(_editorAnimFrame);
    }

    private void StopEditorAnim()
    {
        if (!_editorAnimRunning) return;
        _editorAnimRunning = false;
        UnityEditor.EditorApplication.update -= OnEditorUpdate;
    }

    private void OnEditorUpdate()
    {
        var frames = _editorAnimIsFront ? frontJitterFrames : backJitterFrames;
        if (this == null || !_editorAnimRunning || frames == null || frames.Length == 0)
        {
            StopEditorAnim();
            return;
        }

        double now = UnityEditor.EditorApplication.timeSinceStartup;

        if (now - _editorAnimLastTime >= _editorCurrentInterval)
        {
            _editorAnimLastTime = now;
            _editorAnimFrame = (_editorAnimFrame + 1) % frames.Length;
            ApplyEditorFrame(_editorAnimFrame);
            _editorCurrentInterval = CalcEditorInterval(frames[_editorAnimFrame]);
        }
    }

    private void ApplyEditorFrame(int index)
    {
        var frames = _editorAnimIsFront ? frontJitterFrames : backJitterFrames;
        if (frames == null || index >= frames.Length) return;
        var frame = frames[index];
        if (frame == null) return;

        if (spriteRenderer != null && frame.sprite != null)
            spriteRenderer.sprite = frame.sprite;

        transform.localPosition = new Vector3(frame.offsetX, frame.offsetY, 0f);

        if (fingerTip02 != null) fingerTip02.localPosition = new Vector3(frame.fingerTipOffset.x,   frame.fingerTipOffset.y,   0f);
        if (fingerTip06 != null) fingerTip06.localPosition = new Vector3(frame.fingerTip06Offset.x, frame.fingerTip06Offset.y, 0f);
        if (fingerTip03 != null) fingerTip03.localPosition = new Vector3(frame.fingerTip03Offset.x, frame.fingerTip03Offset.y, 0f);
        if (fingerTip05 != null) fingerTip05.localPosition = new Vector3(frame.fingerTip05Offset.x, frame.fingerTip05Offset.y, 0f);
        ApplyMaskCollider(frame);
        ApplyEyeCollider(frame);

        UnityEditor.SceneView.RepaintAll();
    }

    private void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
    {
        if (state == UnityEditor.PlayModeStateChange.ExitingEditMode)
            StopEditorAnim();
    }
#endif
}
