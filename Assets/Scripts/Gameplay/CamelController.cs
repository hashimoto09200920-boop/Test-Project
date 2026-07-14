using System.Collections;
using UnityEngine;

public class CamelController : MonoBehaviour
{
    public enum PreviewMode { Walk1, Walk2, Walk3, Walk4, Inhale, Exhale, Exhale2, Exhale3, Attack, Attack2, Attack3, Animate }

    [System.Serializable]
    public struct WalkFrame
    {
        [Tooltip("表示するスプライト")]
        public Sprite  sprite;
        [Tooltip("SpriteHolder位置オフセット")]
        public Vector2 offset;
        [Tooltip("表示秒数（0の場合はWalk Frame Intervalを使用）")]
        public float   duration;
        [Tooltip("SpriteHolder Z軸回転（度）")]
        public float   rotationZ;
    }

    [Header("Walk Animation")]
    [Tooltip("歩行フレーム設定。各フレームにスプライト・位置オフセット・表示秒数・Z回転をまとめて設定する")]
    [SerializeField] private WalkFrame[] walkFrames;
    [Tooltip("各フレームのDurationが0のときに使用するデフォルト表示秒数")]
    [Range(0.05f, 0.5f)]
    [SerializeField] private float walkFrameInterval = 0.15f;

    [Header("Sprites")]
    [Tooltip("砂スモーク前の吸い込みモーションスプライト")]
    [SerializeField] private Sprite   inhaleSprite;
    [Tooltip("砂スモーク吐き出しモーションスプライト（フレーム1）")]
    [SerializeField] private Sprite   exhaleSprite;
    [Tooltip("Exhaleアニメーション フレーム2（省略可）")]
    [SerializeField] private Sprite   exhaleSprite2;
    [Tooltip("Exhaleアニメーション フレーム3（省略可）")]
    [SerializeField] private Sprite   exhaleSprite3;
    [Tooltip("弾発射時に一時表示する攻撃スプライト（EnemyShooterのOnFiredで切り替え）")]
    [SerializeField] private Sprite   attackSprite;
    [Tooltip("攻撃スプライトのバリエーション2（省略可）")]
    [SerializeField] private Sprite   attackSprite2;
    [Tooltip("攻撃スプライトのバリエーション3（省略可）")]
    [SerializeField] private Sprite   attackSprite3;
    [Tooltip("攻撃スプライトを表示し続ける秒数")]
    [SerializeField] private float    attackSpriteDuration = 0.2f;

    [Header("Sprite Position Offsets")]
    [Tooltip("Inhaleスプライト時のSpriteHolder位置オフセット")]
    [SerializeField] private Vector2   inhaleOffset;
    [Tooltip("Exhaleスプライト時のSpriteHolder位置オフセット")]
    [SerializeField] private Vector2   exhaleOffset;
    [Tooltip("Attackスプライト時のSpriteHolder位置オフセット")]
    [SerializeField] private Vector2   attackOffset;
    [Tooltip("AttackSprite2のSpriteHolder位置オフセット")]
    [SerializeField] private Vector2   attackOffset2;
    [Tooltip("AttackSprite3のSpriteHolder位置オフセット")]
    [SerializeField] private Vector2   attackOffset3;
    [Tooltip("Hit（被弾）スプライト時のSpriteHolder位置オフセット")]
    [SerializeField] private Vector2   hitOffset;

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
    [Tooltip("本体の見た目の半幅（Units）。画面端の折り返しでこの分の余白も確保する")]
    [SerializeField] private float bodyHalfWidth      = 1.45f;

    [Header("Sand Smoke")]
    [Tooltip("スポーン後、最初のSmoke攻撃までの待機秒数")]
    [SerializeField] private float      smokeFirstDelay      = 2f;
    [Tooltip("2発目以降のSmoke攻撃間隔（秒）")]
    [SerializeField] private float      smokeInterval        = 5f;
    [Tooltip("吸い込みポーズを保持する秒数。経過後にExhaleへ移行")]
    [SerializeField] private float      inhaleDuration       = 0.8f;
    [Tooltip("Exhaleスプライトを表示し続ける秒数。煙はExhale開始直後に生成され、この秒数が経過すると歩行に戻る")]
    [SerializeField] private float      exhaleDuration       = 0.5f;
    [Tooltip("Exhaleアニメーション 1フレームの表示秒数（1→2→3→1 のループ速度）")]
    [SerializeField] private float      exhaleFrameInterval  = 0.12f;
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
    [Tooltip("展開した煙クラウドが円で消滅した時に鳴るSE")]
    [SerializeField] private AudioClip  smokeCloudDissolveSE;

    [Header("Sand Grain")]
    [Tooltip("砂粒パーティクルの色")]
    [SerializeField] private Color      sandGrainColor           = new Color(1f, 0.82f, 0.2f, 1f);
    [Tooltip("砂粒1粒の最小サイズ（Units）")]
    [SerializeField] private float      sandGrainSizeMin         = 0.05f;
    [Tooltip("砂粒1粒の最大サイズ（Units）")]
    [SerializeField] private float      sandGrainSizeMax         = 0.15f;
    [Tooltip("砂粒の放出レート（個/秒）")]
    [SerializeField] private float      sandGrainEmissionRate    = 80f;
    [Tooltip("砂粒の初速 最小値（Units/秒）")]
    [SerializeField] private float      sandGrainSpeedMin        = 0.05f;
    [Tooltip("砂粒の初速 最大値（Units/秒）")]
    [SerializeField] private float      sandGrainSpeedMax        = 0.3f;
    [Tooltip("砂粒の重力係数（マイナスで上に舞う）")]
    [SerializeField] private float      sandGrainGravityModifier = -0.02f;
    [Tooltip("竜巻回転速度（ラジアン/秒。0で無効。1=緩やか・3=速い）")]
    [SerializeField] private float      sandGrainOrbitalSpeed    = 0f;
    [Tooltip("X軸Wave振幅（Units/秒）。左右交互のサイン波軌道。0で無効")]
    [SerializeField] private float      sandGrainWaveAmplitude   = 0.4f;
    [Tooltip("砂粒の上昇速度（Units/秒）。orbitalの横流れに打ち勝つ上向き力")]
    [SerializeField] private float      sandGrainRiseSpeed       = 0.4f;
    [Tooltip("砂粒1粒の寿命（秒）。0以下のとき煙のdurationと同じになる")]
    [SerializeField] private float      sandGrainLifetime        = 3f;
    [Tooltip("砂粒スタート位置のX方向広がり（Units）。中心から±この値の範囲でランダム")]
    [SerializeField] private float      sandGrainSpreadX         = 1.5f;
    [Tooltip("砂粒スタート位置のY方向広がり（Units）。スポーン点から下にこの値分の範囲でランダム")]
    [SerializeField] private float      sandGrainSpreadY         = 1f;

    [Header("Editor Preview")]
    [Tooltip("エディタ上でスプライトをプレビューするモード。Animateを選ぶとWalkアニメをループ再生する")]
    [SerializeField] private PreviewMode previewMode = PreviewMode.Walk1;
    [Tooltip("ONにすると左向き反転状態をプレビュー。コライダーも反転するので左向き時の当たり判定を確認できる")]
    [SerializeField] private bool previewFlipX;

    [Header("References")]
    [Tooltip("スプライトを表示するSpriteRenderer。未設定時はAwakeで子から自動取得する")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [Tooltip("コライダーを持つBodyオブジェクト。未設定時はAwakeで\"Body\"という名前の子から自動取得する")]
    [SerializeField] private Transform bodyTransform;

    // ─── state ───────────────────────────────────────────────────────────────
    private enum State { Walk, Inhale, Exhale }
    private State _state;

    private EnemyMover         _mover;
    private EnemyShooter       _shooter;
    private EnemySpriteSwapper _spriteSwapper;
    private AudioSource        _audioSource;
    private Camera             _cam;

    private float   _walkTimer;
    private int     _walkFrameIdx;
    private float   _smokeTimer;
    private bool    _isFirstSmoke = true;
    private float   _stateTimer;
    private float   _moveTime;
    private float   _sineTime;
    private int     _moveDir = 1;
    private float   _baseY;
    private float   _travelDist;
    private float   _travelTarget;
    private Vector2 _currentBaseOffset;
    private bool    _wasHitActive;
    private bool    _isAttacking;
    private Vector2 _pickedAttackOffset;
    private int     _exhaleFrameIdx;
    private float   _exhaleFrameTimer;

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
        if (bodyTransform == null)
            bodyTransform = transform.Find("Body");

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

        // Hit offset: IsHitActive が変化したタイミングで切り替える
        if (_spriteSwapper != null)
        {
            bool isHitNow = _spriteSwapper.IsHitActive;
            if (isHitNow != _wasHitActive)
            {
                _wasHitActive = isHitNow;
                ApplyOffset(isHitNow ? hitOffset : _currentBaseOffset);
            }
        }
    }

    // ─── Walk ────────────────────────────────────────────────────────────────

    private void UpdateWalk(float dt)
    {
        // Smoke attack timer（初回は smokeFirstDelay、以降は smokeInterval）
        _smokeTimer += dt;
        float threshold = _isFirstSmoke ? smokeFirstDelay : smokeInterval;
        if (_smokeTimer >= threshold)
        {
            _smokeTimer   = 0f;
            _isFirstSmoke = false;
            EnterInhale();
            return;
        }

        if (_isAttacking) return;

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
            // screenMarginは画面端からの余白。本体の見た目の端（bodyHalfWidth）が
            // その余白の内側に収まるよう、折り返し位置側にさらにbodyHalfWidth分を足し込む
            float minX  = -halfW + screenMarginLeft + bodyHalfWidth;
            float maxX  =  halfW - screenMarginRight - bodyHalfWidth;
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
        if (bodyTransform != null)
            bodyTransform.localScale = new Vector3(_moveDir, 1f, 1f);

        // Walk frame animation
        _walkTimer += dt;
        if (_walkTimer >= GetWalkDuration(_walkFrameIdx) && walkFrames != null && walkFrames.Length > 0)
        {
            _walkTimer    -= GetWalkDuration(_walkFrameIdx);
            _walkFrameIdx  = (_walkFrameIdx + 1) % walkFrames.Length;
            SetWalkSprite();
        }
    }

    private void SetWalkSprite()
    {
        if (walkFrames == null || walkFrames.Length == 0) return;
        int       idx   = Mathf.Clamp(_walkFrameIdx, 0, walkFrames.Length - 1);
        WalkFrame frame = walkFrames[idx];
        SetSprite(frame.sprite, frame.offset);
        ApplyRotation(frame.rotationZ);
    }

    // ─── Inhale ──────────────────────────────────────────────────────────────

    private void EnterInhale()
    {
        _state      = State.Inhale;
        _stateTimer = 0f;
        if (_shooter != null) _shooter.enabled = false;
        SetSprite(inhaleSprite, inhaleOffset);
        ApplyRotation(0f);
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
        _state            = State.Exhale;
        _stateTimer       = 0f;
        _exhaleFrameIdx   = 0;
        _exhaleFrameTimer = 0f;
        SetSprite(exhaleSprite, exhaleOffset);
        ApplyRotation(0f);
        PlaySe(exhaleClip);
        SpawnSmoke();
    }

    private void UpdateExhale(float dt)
    {
        _stateTimer       += dt;
        _exhaleFrameTimer += dt;

        if (_exhaleFrameTimer >= exhaleFrameInterval)
        {
            _exhaleFrameTimer -= exhaleFrameInterval;
            _exhaleFrameIdx    = (_exhaleFrameIdx + 1) % 3;
            ApplyExhaleFrame(_exhaleFrameIdx);
        }

        if (_stateTimer >= exhaleDuration)
            EnterWalk();
    }

    private void ApplyExhaleFrame(int idx)
    {
        switch (idx)
        {
            case 0: SetSprite(exhaleSprite,  exhaleOffset); break;
            case 1: if (exhaleSprite2 != null) SetSprite(exhaleSprite2, exhaleOffset); break;
            case 2: if (exhaleSprite3 != null) SetSprite(exhaleSprite3, exhaleOffset); break;
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

        var grain = smoke.SandGrainParticle;
        if (grain != null)
        {
            var grainMain = grain.main;
            grainMain.startColor      = new ParticleSystem.MinMaxGradient(sandGrainColor);
            if (sandGrainLifetime > 0f)
                grainMain.startLifetime = new ParticleSystem.MinMaxCurve(sandGrainLifetime);
            grainMain.gravityModifier = sandGrainGravityModifier;
            grainMain.startSize       = new ParticleSystem.MinMaxCurve(sandGrainSizeMin, sandGrainSizeMax);
            grainMain.startSpeed      = new ParticleSystem.MinMaxCurve(sandGrainSpeedMin, sandGrainSpeedMax);
            var grainEmission = grain.emission;
            grainEmission.rateOverTime = sandGrainEmissionRate;

            {
                var vel = grain.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.World;
                vel.y = new ParticleSystem.MinMaxCurve(sandGrainRiseSpeed);

                if (sandGrainOrbitalSpeed != 0f)
                    vel.orbitalZ = new ParticleSystem.MinMaxCurve(sandGrainOrbitalSpeed);

                // vel.x への AnimationCurve 設定は実行時に適用されない（Unity の制限）
                // Noise モジュールで X 軸のみに Perlin ノイズを与えて Wave 状の左右揺れを実現する
                if (sandGrainWaveAmplitude > 0f)
                {
                    var noiseModule = grain.noise;
                    noiseModule.enabled       = true;
                    noiseModule.separateAxes  = true;
                    noiseModule.strengthX     = new ParticleSystem.MinMaxCurve(sandGrainWaveAmplitude);
                    noiseModule.strengthY     = new ParticleSystem.MinMaxCurve(0f);
                    noiseModule.strengthZ     = new ParticleSystem.MinMaxCurve(0f);
                    noiseModule.frequency     = 0.5f;
                    noiseModule.scrollSpeed   = new ParticleSystem.MinMaxCurve(0.5f);
                    noiseModule.octaveCount   = 1;
                    noiseModule.quality       = ParticleSystemNoiseQuality.Low;
                }
            }
        }

        // Initialize()内でも確実にgravityを適用するため値を渡す
        smoke.SetSandGrainGravity(sandGrainGravityModifier);
        if (sandGrainLifetime > 0f)
            smoke.SetSandGrainLifetime(sandGrainLifetime);
        smoke.SetSandGrainSpreadX(sandGrainSpreadX);
        smoke.SetSandGrainSpreadY(sandGrainSpreadY);
        smoke.Initialize(smokeRadius, smokeDuration, smokeExpansionSpeed, smokeCloudDissolveSE);
    }

    // ─── Attack sprite ────────────────────────────────────────────────────────

    private void OnShooterFired()
    {
        if (attackSprite == null || _spriteSwapper == null) return;
        _spriteSwapper.TriggerAttack(PickAttackSprite(), attackSpriteDuration);
        StartCoroutine(AttackOffsetCoroutine());
    }

    private Sprite PickAttackSprite()
    {
        var sprites = new System.Collections.Generic.List<Sprite>(3) { attackSprite };
        var offsets  = new System.Collections.Generic.List<Vector2>(3) { attackOffset };
        if (attackSprite2 != null) { sprites.Add(attackSprite2); offsets.Add(attackOffset2); }
        if (attackSprite3 != null) { sprites.Add(attackSprite3); offsets.Add(attackOffset3); }
        int idx = Random.Range(0, sprites.Count);
        _pickedAttackOffset = offsets[idx];
        return sprites[idx];
    }

    private IEnumerator AttackOffsetCoroutine()
    {
        _isAttacking = true;
        ApplyOffset(_pickedAttackOffset);
        yield return new WaitForSeconds(attackSpriteDuration);
        _isAttacking = false;
        // Hit が優先されている場合は上書きしない
        if (_spriteSwapper == null || !_spriteSwapper.IsHitActive)
            ApplyOffset(_currentBaseOffset);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private void SetSprite(Sprite sp, Vector2 offset = default)
    {
        if (sp == null) return;
        _currentBaseOffset = offset;
        if (_spriteSwapper != null) _spriteSwapper.SetBaseSprite(sp);
        else if (spriteRenderer != null) spriteRenderer.sprite = sp;
        ApplyOffset(offset);
    }

    private void ApplyOffset(Vector2 offset)
    {
        if (spriteRenderer == null) return;
        var t = spriteRenderer.transform;
        t.localPosition = new Vector3(offset.x, offset.y, t.localPosition.z);
    }

    private void ApplyRotation(float rotZ)
    {
        if (spriteRenderer == null) return;
        var t = spriteRenderer.transform;
        t.localEulerAngles = new Vector3(t.localEulerAngles.x, t.localEulerAngles.y, rotZ);
    }

    private float GetWalkDuration(int idx)
    {
        if (walkFrames != null && idx < walkFrames.Length && walkFrames[idx].duration > 0f)
            return walkFrames[idx].duration;
        return walkFrameInterval;
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
        if (bodyTransform == null)
            bodyTransform = transform.Find("Body");

        if (spriteRenderer != null)
            spriteRenderer.flipX = previewFlipX;
        if (bodyTransform != null)
            bodyTransform.localScale = new Vector3(previewFlipX ? -1f : 1f, 1f, 1f);

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
                    if (inhaleSprite != null) { spriteRenderer.sprite = inhaleSprite; ApplyOffset(inhaleOffset); ApplyRotation(0f); }
                    break;
                case PreviewMode.Exhale:
                    if (exhaleSprite  != null) { spriteRenderer.sprite = exhaleSprite;  ApplyOffset(exhaleOffset); ApplyRotation(0f); }
                    break;
                case PreviewMode.Exhale2:
                    if (exhaleSprite2 != null) { spriteRenderer.sprite = exhaleSprite2; ApplyOffset(exhaleOffset); ApplyRotation(0f); }
                    break;
                case PreviewMode.Exhale3:
                    if (exhaleSprite3 != null) { spriteRenderer.sprite = exhaleSprite3; ApplyOffset(exhaleOffset); ApplyRotation(0f); }
                    break;
                case PreviewMode.Attack:
                    if (attackSprite != null) { spriteRenderer.sprite = attackSprite; ApplyOffset(attackOffset); ApplyRotation(0f); }
                    break;
                case PreviewMode.Attack2:
                    if (attackSprite2 != null) { spriteRenderer.sprite = attackSprite2; ApplyOffset(attackOffset2); ApplyRotation(0f); }
                    break;
                case PreviewMode.Attack3:
                    if (attackSprite3 != null) { spriteRenderer.sprite = attackSprite3; ApplyOffset(attackOffset3); ApplyRotation(0f); }
                    break;
                default:
                {
                    int idx = (int)previewMode; // Walk1=0, Walk2=1, Walk3=2, Walk4=3
                    if (walkFrames != null && idx < walkFrames.Length && walkFrames[idx].sprite != null)
                    {
                        spriteRenderer.sprite = walkFrames[idx].sprite;
                        ApplyOffset(walkFrames[idx].offset);
                        ApplyRotation(walkFrames[idx].rotationZ);
                    }
                    break;
                }
            }
            UnityEditor.SceneView.RepaintAll();
        }
#endif
    }

#if UNITY_EDITOR
    private void StartEditorAnim()
    {
        if (walkFrames == null || walkFrames.Length == 0) return;
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
        if (this == null || !_editorAnimRunning || walkFrames == null || walkFrames.Length == 0)
        {
            StopEditorAnim();
            return;
        }
        double now = UnityEditor.EditorApplication.timeSinceStartup;
        if (now - _editorAnimLastTime >= GetWalkDuration(_editorAnimFrameIdx))
        {
            _editorAnimLastTime = now;
            _editorAnimFrameIdx = (_editorAnimFrameIdx + 1) % walkFrames.Length;
            ApplyEditorFrame(_editorAnimFrameIdx);
        }
    }

    private void ApplyEditorFrame(int idx)
    {
        if (spriteRenderer == null || walkFrames == null || idx >= walkFrames.Length) return;
        if (walkFrames[idx].sprite != null)
        {
            spriteRenderer.sprite = walkFrames[idx].sprite;
            ApplyOffset(walkFrames[idx].offset);
            ApplyRotation(walkFrames[idx].rotationZ);
        }
        UnityEditor.SceneView.RepaintAll();
    }

    private void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
    {
        if (state == UnityEditor.PlayModeStateChange.ExitingEditMode)
            StopEditorAnim();
    }
#endif
}
