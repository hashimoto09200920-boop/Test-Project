using UnityEngine;

public class CamelController : MonoBehaviour
{
    public enum PreviewMode { Walk1, Walk2, Walk3, Walk4, Inhale, Exhale, Animate }

    [Header("Sprites")]
    [Tooltip("歩行アニメのスプライト。Walk01→Walk02→Walk03→Walk04の順にループ再生する（4枚推奨）")]
    [SerializeField] private Sprite[] walkSprites;
    [Tooltip("砂スモーク前の吸い込みモーションスプライト")]
    [SerializeField] private Sprite   inhaleSprite;
    [Tooltip("砂スモーク吐き出しモーションスプライト")]
    [SerializeField] private Sprite   exhaleSprite;
    [Tooltip("弾発射時に一時表示する攻撃スプライト（EnemyShooterのOnFiredで切り替え）")]
    [SerializeField] private Sprite   attackSprite;
    [Tooltip("攻撃スプライトを表示し続ける秒数")]
    [SerializeField] private float    attackSpriteDuration = 0.2f;

    [Header("Walk Animation")]
    [Tooltip("歩行フレームを1枚ずつ進める間隔（秒）。小さいほど速いアニメになる")]
    [Range(0.05f, 0.5f)]
    [SerializeField] private float walkFrameInterval = 0.15f;

    [Header("Movement")]
    [Tooltip("水平移動の基本速度（Units/秒）")]
    [SerializeField] private float moveSpeedBase      = 1.5f;
    [Tooltip("速度変化の振れ幅。base ± variation で速度が正弦波状に変動する")]
    [Range(0f, 3f)]
    [SerializeField] private float moveSpeedVariation = 0.8f;
    [Tooltip("速度変化の周波数（Hz）。大きいほど速く速度が変わる")]
    [SerializeField] private float speedVariationFreq = 0.8f;
    [Tooltip("Y軸正弦波の振れ幅（Units）。体が上下にゆれる量")]
    [SerializeField] private float sineAmplitude      = 0.3f;
    [Tooltip("Y軸正弦波の周波数（Hz）。大きいほど速く上下する")]
    [SerializeField] private float sineFrequency      = 1.0f;
    [Tooltip("一方向に進む最短距離（Units）。ここで方向転換が起きる最小値")]
    [SerializeField] private float travelDistanceMin  = 2f;
    [Tooltip("一方向に進む最長距離（Units）。ここで方向転換が起きる最大値")]
    [SerializeField] private float travelDistanceMax  = 5f;
    [Tooltip("画面左端からの折り返し余白（Units）")]
    [SerializeField] private float screenMarginLeft   = 0.5f;
    [Tooltip("画面右端からの折り返し余白（Units）")]
    [SerializeField] private float screenMarginRight  = 0.5f;

    [Header("Sand Smoke")]
    [Tooltip("スポーン後、最初のSmoke攻撃までの待機秒数")]
    [SerializeField] private float      smokeFirstDelay      = 2f;
    [Tooltip("2発目以降のSmoke攻撃間隔（秒）")]
    [SerializeField] private float      smokeInterval        = 5f;
    [Tooltip("吸い込みポーズを保持する秒数。経過後にExhaleへ移行")]
    [SerializeField] private float      inhaleDuration       = 0.8f;
    [Tooltip("吐き出しポーズを保持する秒数。経過後にSmokeCloudを生成して歩行に戻る")]
    [SerializeField] private float      exhaleDuration       = 0.5f;
    [Tooltip("生成するSmokeCloudのPrefab（SmokeCloudコンポーネントが必要）")]
    [SerializeField] private GameObject smokeParticlePrefab;
    [Tooltip("砂煙の色。Instantiate後のインスタンスにのみ適用されるため既存システムに影響しない")]
    [SerializeField] private Color      smokeColor           = new Color(0.9f, 0.75f, 0.3f, 0.8f);
    [Tooltip("砂煙パーティクルの重力係数。マイナスで上に漂う（-0.05程度が砂っぽい浮遊感）")]
    [SerializeField] private float      smokeGravityModifier = -0.05f;
    [Tooltip("SmokeCloudが消去判定に使う円の半径（Units）。パドルで円を描いたときに消える範囲")]
    [SerializeField] private float      smokeRadius          = 3f;
    [Tooltip("SmokeCloudが自動消滅するまでの秒数")]
    [SerializeField] private float      smokeDuration        = 5f;
    [Tooltip("SmokeCloudのCircleColliderが膨張するスピード（Units/秒）")]
    [SerializeField] private float      smokeExpansionSpeed  = 0.5f;
    [Tooltip("砂煙パーティクル1粒の最小サイズ（Units）")]
    [SerializeField] private float      smokeParticleSizeMin = 0.3f;
    [Tooltip("砂煙パーティクル1粒の最大サイズ（Units）。MinとMaxを同じにすると均一サイズになる")]
    [SerializeField] private float      smokeParticleSizeMax = 1.2f;
    [Tooltip("砂煙が出現するときのフェードイン秒数（0=即時表示）")]
    [SerializeField] private float      smokeFadeInDuration  = 0.5f;
    [Tooltip("砂煙が消えるときのフェードアウト秒数（0=即時消滅）")]
    [SerializeField] private float      smokeFadeOutDuration = 1f;
    [Tooltip("砂煙パーティクルの放出レート（個/秒）。0以下はPrefabのデフォルト値を使用")]
    [SerializeField] private float      smokeEmissionRate    = 0f;
    [Tooltip("吸い込み時に再生するSE")]
    [SerializeField] private AudioClip  inhaleClip;
    [Tooltip("吐き出し時に再生するSE")]
    [SerializeField] private AudioClip  exhaleClip;
    [Tooltip("吸い込み・吐き出しSEのボリューム")]
    [Range(0f, 1f)]
    [SerializeField] private float      smokeSeVolume        = 1f;

    [Header("Editor Preview")]
    [Tooltip("エディタ上でスプライトをプレビューするモード。Animateを選ぶと歩行をループ再生する")]
    [SerializeField] private PreviewMode previewMode = PreviewMode.Walk1;

    [Header("References")]
    [Tooltip("スプライトを表示するSpriteRenderer。未設定時はAwakeで子から自動取得する")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    // ─── state ───────────────────────────────────────────────────────────────
    private enum State { Walk, Inhale, Exhale }
    private State _state;

    private EnemyMover        _mover;
    private EnemyShooter      _shooter;
    private EnemySpriteSwapper _spriteSwapper;
    private AudioSource       _audioSource;
    private Camera            _cam;

    private float _walkTimer;
    private int   _walkFrameIdx;
    private float _smokeTimer;
    private bool  _isFirstSmoke = true;
    private float _stateTimer;
    private float _moveTime;
    private float _sineTime;
    private int   _moveDir = 1;
    private float _baseY;
    private float _travelDist;
    private float _travelTarget;

#if UNITY_EDITOR
    private bool   _editorAnimRunning;
    private double _editorAnimLastTime;
    private int    _editorAnimFrameIdx;
#endif

    // ─── Lifecycle ───────────────────────────────────────────────────────────

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
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        _mover         = GetComponent<EnemyMover>();
        _shooter       = GetComponent<EnemyShooter>();
        _spriteSwapper = GetComponent<EnemySpriteSwapper>();
        _audioSource   = GetComponent<AudioSource>();

        if (_mover != null)
            _mover.suppressMovement = true;

        if (_shooter != null)
            _shooter.OnFired += OnShooterFired;
    }

    private void OnDestroy()
    {
        if (_shooter != null)
            _shooter.OnFired -= OnShooterFired;
    }

    private void Start()
    {
        if (!Application.isPlaying) return;

        _cam          = Camera.main;
        _baseY        = transform.position.y;
        _moveDir      = Random.value < 0.5f ? 1 : -1;
        _travelTarget = Random.Range(travelDistanceMin, travelDistanceMax);
        _state        = State.Walk;

        SetWalkSprite();
    }

    // ─── Update ──────────────────────────────────────────────────────────────

    private void Update()
    {
        if (!Application.isPlaying) return;

        float dt = Time.deltaTime;
        switch (_state)
        {
            case State.Walk:   UpdateWalk(dt);   break;
            case State.Inhale: UpdateInhale(dt); break;
            case State.Exhale: UpdateExhale(dt); break;
        }
    }

    // ─── Walk ────────────────────────────────────────────────────────────────

    private void UpdateWalk(float dt)
    {
        _moveTime += dt;
        _sineTime += dt;

        // Pattern A: sinusoidal speed variation
        float speed = Mathf.Max(0.1f,
            moveSpeedBase + moveSpeedVariation * Mathf.Sin(_moveTime * speedVariationFreq * Mathf.PI * 2f));

        float dx = _moveDir * speed * dt;
        _travelDist += Mathf.Abs(dx);

        // Pattern C: Y-axis sine oscillation
        float newX = transform.position.x + dx;
        float newY = _baseY + sineAmplitude * Mathf.Sin(_sineTime * sineFrequency * Mathf.PI * 2f);

        bool reverse = _travelDist >= _travelTarget;

        if (_cam != null)
        {
            float halfW = _cam.orthographicSize * _cam.aspect;
            float minX  = -halfW + screenMarginLeft;
            float maxX  =  halfW - screenMarginRight;
            if (newX < minX) { newX = minX; reverse = true; }
            if (newX > maxX) { newX = maxX; reverse = true; }
        }

        if (reverse)
        {
            _moveDir      *= -1;
            _travelDist    = 0f;
            _travelTarget  = Random.Range(travelDistanceMin, travelDistanceMax);
        }

        transform.position = new Vector3(newX, newY, transform.position.z);

        if (spriteRenderer != null)
            spriteRenderer.flipX = (_moveDir < 0);

        // Walk frame animation
        _walkTimer += dt;
        if (_walkTimer >= walkFrameInterval && walkSprites != null && walkSprites.Length > 0)
        {
            _walkTimer -= walkFrameInterval;
            _walkFrameIdx = (_walkFrameIdx + 1) % walkSprites.Length;
            SetWalkSprite();
        }

        // Smoke attack timer（初回は smokeFirstDelay、以降は smokeInterval）
        _smokeTimer += dt;
        float threshold = _isFirstSmoke ? smokeFirstDelay : smokeInterval;
        if (_smokeTimer >= threshold)
        {
            _smokeTimer = 0f;
            _isFirstSmoke = false;
            EnterInhale();
        }
    }

    private void SetWalkSprite()
    {
        if (walkSprites == null || walkSprites.Length == 0) return;
        var sp = walkSprites[Mathf.Clamp(_walkFrameIdx, 0, walkSprites.Length - 1)];
        SetSprite(sp);
    }

    // ─── Inhale ──────────────────────────────────────────────────────────────

    private void EnterInhale()
    {
        _state      = State.Inhale;
        _stateTimer = 0f;
        if (_shooter != null) _shooter.enabled = false;
        SetSprite(inhaleSprite);
        PlaySe(inhaleClip);
    }

    private void UpdateInhale(float dt)
    {
        _stateTimer += dt;
        if (_stateTimer >= inhaleDuration)
            EnterExhale();
    }

    // ─── Exhale ──────────────────────────────────────────────────────────────

    private void EnterExhale()
    {
        _state      = State.Exhale;
        _stateTimer = 0f;
        SetSprite(exhaleSprite);
        PlaySe(exhaleClip);
    }

    private void UpdateExhale(float dt)
    {
        _stateTimer += dt;
        if (_stateTimer >= exhaleDuration)
        {
            SpawnSmoke();
            EnterWalk();
        }
    }

    // ─── Walk restore ─────────────────────────────────────────────────────────

    private void EnterWalk()
    {
        _state = State.Walk;
        if (_shooter != null) _shooter.enabled = true;
        SetWalkSprite();
    }

    // ─── Smoke spawn ──────────────────────────────────────────────────────────

    private void SpawnSmoke()
    {
        if (smokeParticlePrefab == null) return;

        var go    = Instantiate(smokeParticlePrefab, transform.position, Quaternion.identity);
        var smoke = go.GetComponent<SmokeCloud>();
        if (smoke == null) { Destroy(go); return; }

        // Set color and gravity BEFORE Initialize() so they are not overwritten
        var ps = go.GetComponent<ParticleSystem>();
        if (ps != null)
        {
            var main = ps.main;
            main.startColor      = new ParticleSystem.MinMaxGradient(smokeColor);
            main.gravityModifier = smokeGravityModifier;
            main.startSize       = new ParticleSystem.MinMaxCurve(smokeParticleSizeMin, smokeParticleSizeMax);
        }

        smoke.SetFadeDurations(smokeFadeInDuration, smokeFadeOutDuration);
        smoke.SetEmissionRate(smokeEmissionRate);
        smoke.Initialize(smokeRadius, smokeDuration, smokeExpansionSpeed);
    }

    // ─── Attack sprite ────────────────────────────────────────────────────────

    private void OnShooterFired()
    {
        if (attackSprite == null || _spriteSwapper == null) return;
        _spriteSwapper.TriggerAttack(attackSprite, attackSpriteDuration);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private void SetSprite(Sprite sp)
    {
        if (sp == null) return;
        if (_spriteSwapper != null) _spriteSwapper.SetBaseSprite(sp);
        else if (spriteRenderer != null) spriteRenderer.sprite = sp;
    }

    private void PlaySe(AudioClip clip)
    {
        if (clip == null || _audioSource == null) return;
        float vol = smokeSeVolume *
            (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
        _audioSource.PlayOneShot(clip, vol);
    }

    // ─── Editor Preview ───────────────────────────────────────────────────────

    private void OnValidate()
    {
        if (Application.isPlaying) return;
#if UNITY_EDITOR
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        if (previewMode == PreviewMode.Animate)
        {
            StartEditorAnim();
        }
        else
        {
            StopEditorAnim();
            if (spriteRenderer == null) return;

            switch (previewMode)
            {
                case PreviewMode.Inhale:
                    if (inhaleSprite != null) spriteRenderer.sprite = inhaleSprite;
                    break;
                case PreviewMode.Exhale:
                    if (exhaleSprite != null) spriteRenderer.sprite = exhaleSprite;
                    break;
                default:
                    int idx = (int)previewMode; // Walk1=0, Walk2=1, Walk3=2, Walk4=3
                    if (walkSprites != null && idx < walkSprites.Length && walkSprites[idx] != null)
                        spriteRenderer.sprite = walkSprites[idx];
                    break;
            }
            UnityEditor.SceneView.RepaintAll();
        }
#endif
    }

#if UNITY_EDITOR
    private void StartEditorAnim()
    {
        if (walkSprites == null || walkSprites.Length == 0) return;
        _editorAnimFrameIdx = 0;
        _editorAnimLastTime = UnityEditor.EditorApplication.timeSinceStartup;
        if (!_editorAnimRunning)
        {
            _editorAnimRunning = true;
            UnityEditor.EditorApplication.update += OnEditorUpdate;
        }
        ApplyEditorFrame(_editorAnimFrameIdx);
    }

    private void StopEditorAnim()
    {
        if (!_editorAnimRunning) return;
        _editorAnimRunning = false;
        UnityEditor.EditorApplication.update -= OnEditorUpdate;
    }

    private void OnEditorUpdate()
    {
        if (this == null || !_editorAnimRunning || walkSprites == null || walkSprites.Length == 0)
        {
            StopEditorAnim();
            return;
        }
        double now = UnityEditor.EditorApplication.timeSinceStartup;
        if (now - _editorAnimLastTime >= walkFrameInterval)
        {
            _editorAnimLastTime = now;
            _editorAnimFrameIdx = (_editorAnimFrameIdx + 1) % walkSprites.Length;
            ApplyEditorFrame(_editorAnimFrameIdx);
        }
    }

    private void ApplyEditorFrame(int idx)
    {
        if (spriteRenderer == null || walkSprites == null || idx >= walkSprites.Length) return;
        if (walkSprites[idx] != null)
            spriteRenderer.sprite = walkSprites[idx];
        UnityEditor.SceneView.RepaintAll();
    }

    private void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
    {
        if (state == UnityEditor.PlayModeStateChange.ExitingEditMode)
            StopEditorAnim();
    }
#endif
}
