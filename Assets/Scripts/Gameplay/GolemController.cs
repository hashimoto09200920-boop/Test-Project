using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GolemController : MonoBehaviour
{
    // ======================================================
    // Data types
    // ======================================================

    public enum PreviewMode
    {
        Idle, ArmUp, ArmMid1, ArmMid2, ArmDown, ArmDown2, ArmDown3, Animate
    }

    [System.Serializable]
    public struct BodyFrame
    {
        public Sprite  sprite;
        public Vector2 offset;
        [Tooltip("このフレームを表示する秒数")]
        public float   duration;
    }

    [System.Serializable]
    public class RockSlotData
    {
        [Tooltip("RockSlot_XX の EnemyPart（EnemyDamageReceiverと差し替え）")]
        public EnemyPart enemyPart;
        [Tooltip("子 RockSprite の SpriteRenderer")]
        public SpriteRenderer rockRenderer;
        [Tooltip("子 CoreSprite の SpriteRenderer")]
        public SpriteRenderer coreRenderer;

        [HideInInspector] public bool isCore;
        [HideInInspector] public bool isDestroyed;
        [HideInInspector] public int  currentHp;
        [HideInInspector] public bool isCoreDestroyed;
        [HideInInspector] public int  currentCoreHp;
        // OnCollisionEnter2D修正: RockSlot側に移植したBoxCollider2D
        [HideInInspector] public BoxCollider2D slotCollider;
        // 岩表示時のコライダーオフセット（コア←→岩切り替え時に復元用）
        [HideInInspector] public Vector2 rockColliderOffset;
    }

    // ======================================================
    // Inspector fields
    // ======================================================

    [Header("Body Sprite")]
    [Tooltip("Golem全体を表示するSpriteRenderer（Rootの直子 BodySprite に配置）")]
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private PreviewMode    previewMode;
    [Tooltip("ONで左腕プレビュー（スプライトをX反転）")]
    [SerializeField] private bool           previewFlipX;
    [Tooltip("待機フレーム（duration は未使用）")]
    [SerializeField] private BodyFrame      idleFrame;
    [SerializeField] private BodyFrame      armUpFrame;
    [SerializeField] private BodyFrame      armMid1Frame;
    [SerializeField] private BodyFrame      armMid2Frame;
    [Tooltip("ArmDown 各フレーム。順番に再生（ArmDown/ArmDown2/ArmDown3）")]
    [SerializeField] private BodyFrame[]    armDownFrames;

    [Header("Arm Timing")]
    [Tooltip("最初のアーム攻撃まで待機する秒数")]
    [SerializeField] private float firstAttackDelay  = 2f;
    [Tooltip("右腕叩きつけ後の待機秒数（最小）")]
    [SerializeField] private float rightArmIntervalMin = 3f;
    [Tooltip("右腕叩きつけ後の待機秒数（最大）")]
    [SerializeField] private float rightArmIntervalMax = 5f;
    [Tooltip("左腕叩きつけ後の待機秒数（最小）")]
    [SerializeField] private float leftArmIntervalMin  = 3f;
    [Tooltip("左腕叩きつけ後の待機秒数（最大）")]
    [SerializeField] private float leftArmIntervalMax  = 5f;
    [Tooltip("左右のアームを交互に使う")]
    [SerializeField] private bool  alternateArms     = true;

    [Header("Rock Slots")]
    [SerializeField] private RockSlotData[] rockSlots;
    [Tooltip("コアとなるスロット数（起動時・再生成時にランダム決定）")]
    [SerializeField] private int   coreCount     = 4;
    [Tooltip("岩1個あたりのHP（全スロット共通）")]
    [SerializeField] private int   rockMaxHp     = 3;
    [Tooltip("最初の岩破壊から全再生成までの秒数")]
    [SerializeField] private float rockRegenTime = 60f;
    [Tooltip("岩スプライトA（起動時・再生成時にA/B/C/Dからランダム割り当て）")]
    [SerializeField] private Sprite rockSpriteA;
    [Tooltip("岩スプライトB（未設定の場合はスキップ）")]
    [SerializeField] private Sprite rockSpriteB;
    [Tooltip("岩スプライトC（未設定の場合はスキップ）")]
    [SerializeField] private Sprite rockSpriteC;
    [Tooltip("岩スプライトD（未設定の場合はスキップ）")]
    [SerializeField] private Sprite rockSpriteD;
    [Tooltip("岩スプライトのランダム回転Z角度の最大値（0〜この値でランダム）")]
    [SerializeField] private float  rockRotationMax = 360f;
    [Header("Rock Fade")]
    [Tooltip("出現時のフェードイン秒数（0=即表示）")]
    [SerializeField] private float  rockFadeInDuration  = 0.5f;
    [Tooltip("消滅時のフェードアウト秒数（0=即消滅）")]
    [SerializeField] private float  rockFadeOutDuration = 0.3f;

    [Header("Sand Smoke")]
    [SerializeField] private GameObject smokeParticlePrefab;
    [SerializeField] private Color      smokeColor           = new Color(0.9f, 0.75f, 0.3f, 0.8f);
    [SerializeField] private float      smokeRadius          = 3f;
    [SerializeField] private float      smokeDuration        = 5f;
    [SerializeField] private float      smokeExpansionSpeed  = 0.5f;
    [SerializeField] private float      smokeFadeInDuration  = 0.3f;
    [SerializeField] private float      smokeFadeOutDuration = 1f;
    [SerializeField] private float      smokeGravityModifier = -0.05f;
    [SerializeField] private float      smokeParticleSizeMin = 0.3f;
    [SerializeField] private float      smokeParticleSizeMax = 1.2f;
    [SerializeField] private float      smokeEmissionRate    = 0f;
    [SerializeField] private AudioClip  smokeCloudDissolveSE;

    [Header("Sand Grain")]
    [SerializeField] private Color sandGrainColor           = new Color(1f, 0.82f, 0.2f, 1f);
    [SerializeField] private float sandGrainSizeMin         = 0.05f;
    [SerializeField] private float sandGrainSizeMax         = 0.15f;
    [SerializeField] private float sandGrainEmissionRate    = 80f;
    [SerializeField] private float sandGrainSpeedMin        = 0.05f;
    [SerializeField] private float sandGrainSpeedMax        = 0.3f;
    [SerializeField] private float sandGrainGravityModifier = -0.02f;
    [SerializeField] private float sandGrainLifetime        = 3f;
    [SerializeField] private float sandGrainSpreadX         = 1.5f;
    [SerializeField] private float sandGrainSpreadY         = 1f;

    [Header("References")]
    [Tooltip("腕叩きつけ時の砂煙スポーン位置（Golem中心からのオフセット。左腕はX自動反転）")]
    [SerializeField] private Vector2   slamSmokeOffset = new Vector2(3f, -1f);
    [SerializeField] private AudioClip slamClip;
    [Range(0f, 1f)]
    [SerializeField] private float     slamSeVolume = 1f;

    [Header("Core Break")]
    [Tooltip("コアの最大HP（0になると砕け散り追加ダメージが発生）")]
    [SerializeField] private int        coreMaxHp         = 5;
    [Tooltip("コア砕け散り時に敵本体に与える追加ダメージ（1回のみ）")]
    [SerializeField] private int        coreBonusDamage   = 30;
    [Tooltip("コア砕け散り時のVFXプレハブ（Shield破壊エフェクト）")]
    [SerializeField] private GameObject coreBreakVfxPrefab;
    [Tooltip("コア砕け散り時のSE（Shield破壊SE）")]
    [SerializeField] private AudioClip  coreBreakSeClip;
    [Range(0f, 1f)]
    [SerializeField] private float      coreBreakSeVolume = 1f;

    [Header("Rock Hit Effect")]
    [Tooltip("ヒット時（未破壊）のVFXプレハブ")]
    [SerializeField] private GameObject rockHitVfxPrefab;
    [Tooltip("破壊時のVFXプレハブ")]
    [SerializeField] private GameObject rockBreakVfxPrefab;

    [Header("Rock SE")]
    [Tooltip("ヒット時（未破壊）のSE（3種ランダム）")]
    [SerializeField] private AudioClip[] rockHitClips;
    [Tooltip("破壊時のSE（3種ランダム）")]
    [SerializeField] private AudioClip[] rockBreakClips;
    [Range(0f, 1f)]
    [SerializeField] private float       rockSeVolume = 1f;

    [Header("Death VFX")]
    [Tooltip("撃破VFX設定（EnemyDataのuseCustomDeathVfx=ONと同等の効果）")]
    [SerializeField] private DeathVfxConfig deathVfxConfig = new DeathVfxConfig();
    [Tooltip("撃破時のVFX生成位置（複数指定可）。未設定の場合はGolem中心のみ")]
    [SerializeField] private Transform[]   deathEffectPoints;


    // ======================================================
    // Runtime state
    // ======================================================

    private EnemyMover  _mover;
    private AudioSource _audioSource;
    private EnemyStats  _enemyStats;
    private bool        _rightArmNext = true;
    private bool        _isArmAttacking;
    private Coroutine   _regenCoroutine;
    private bool        _regenTimerRunning;
    private Vector3     _spawnLocalPosition;

    // ======================================================
    // Lifecycle
    // ======================================================

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
        _mover       = GetComponent<EnemyMover>();
        _enemyStats  = GetComponent<EnemyStats>();
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake  = false;
            _audioSource.loop         = false;
            _audioSource.spatialBlend = 0f;
        }

        if (_mover != null)
            _mover.suppressMovement = true;
    }

    private void Start()
    {
        if (!Application.isPlaying) return;
        _spawnLocalPosition = transform.localPosition;
        var hpDisplay = GetComponent<EnemyHealthDisplay>();
        if (hpDisplay != null) hpDisplay.SetFixedBasePosition(transform.position);
        InitRockSlots();
        SetBodySprite(idleFrame, flipX: false);
        ApplyDeathVfxToStats();
        StartCoroutine(AttackLoop());
    }

    private void ApplyDeathVfxToStats()
    {
        if (_enemyStats == null) return;
        _enemyStats.ApplyDeathVfxConfig(true, deathVfxConfig);
        if (deathEffectPoints != null && deathEffectPoints.Length > 0)
        {
            var positions = new Vector3[deathEffectPoints.Length];
            for (int i = 0; i < deathEffectPoints.Length; i++)
                positions[i] = deathEffectPoints[i] != null ? deathEffectPoints[i].position : transform.position;
            _enemyStats.deathEffectPositions = positions;
        }
    }

    // ======================================================
    // Rock Slots
    // ======================================================

    private void InitRockSlots()
    {
        if (rockSlots == null || rockSlots.Length == 0) return;

        AssignRandomCores();

        for (int i = 0; i < rockSlots.Length; i++)
        {
            var slot = rockSlots[i];
            slot.isDestroyed    = false;
            slot.currentHp      = Mathf.Max(1, rockMaxHp);
            slot.isCoreDestroyed = false;
            slot.currentCoreHp  = Mathf.Max(1, coreMaxHp);

            // BoxCollider2DがRockSprite(子)にある場合、RockSlot(EnemyPart側)へ転写する
            // Unity2D: OnCollisionEnter2DはCollider2Dと同じGOのスクリプトにのみ送られるため
            if (slot.enemyPart != null && slot.rockRenderer != null)
            {
                var spriteCol = slot.rockRenderer.GetComponent<BoxCollider2D>();
                if (spriteCol != null)
                {
                    var slotCol = slot.enemyPart.GetComponent<BoxCollider2D>();
                    if (slotCol == null)
                        slotCol = slot.enemyPart.gameObject.AddComponent<BoxCollider2D>();
                    slotCol.size   = spriteCol.size;
                    // RockSpriteの localPosition 分を加算（RockSlot基準のオフセットに変換）
                    slotCol.offset = spriteCol.offset + (Vector2)slot.rockRenderer.transform.localPosition;
                    spriteCol.enabled = false;
                    slot.slotCollider = slotCol;
                    slot.rockColliderOffset = slotCol.offset;
                }
            }

            SetRockVisual(slot, true, randomizeSprite: true);

            // 初期フェードイン
            if (rockFadeInDuration > 0f)
            {
                SetRockAlpha(slot, 0f);
                StartCoroutine(FadeRenderer(slot.rockRenderer, 0f, 1f, rockFadeInDuration));
            }

            if (slot.enemyPart == null) continue;
            slot.enemyPart.suppressDamage = true;
            slot.enemyPart.enableDamage   = false;

            int idx = i;
            slot.enemyPart.OnHitWhileSuppressed += (bullet)      => OnRockSlotHit(rockSlots[idx], bullet);
            slot.enemyPart.OnHitWithDamage       += (bullet, dmg) => OnCoreHitWithDamage(rockSlots[idx], bullet, dmg);
        }
    }

    private void AssignRandomCores()
    {
        if (rockSlots == null || rockSlots.Length == 0) return;
        int count = Mathf.Min(coreCount, rockSlots.Length);
        foreach (var s in rockSlots) s.isCore = false;

        int[] indices = new int[rockSlots.Length];
        for (int i = 0; i < indices.Length; i++) indices[i] = i;
        for (int i = indices.Length - 1; i > 0; i--)
        {
            int j   = Random.Range(0, i + 1);
            int tmp = indices[i]; indices[i] = indices[j]; indices[j] = tmp;
        }
        for (int i = 0; i < count; i++)
            rockSlots[indices[i]].isCore = true;
    }

    private void OnRockSlotHit(RockSlotData slot, EnemyBullet bullet)
    {
        if (slot.isDestroyed) return;

        // WallHealthと同じダメージ計算（Just反射 or 通常反射）
        int dmg = bullet.DamageMultiplier > 1.0001f
            ? Mathf.RoundToInt(bullet.BlockJustDamage)
            : Mathf.RoundToInt(bullet.BlockNormalDamage);

        if (dmg <= 0) return;

        slot.currentHp -= dmg;

        if (slot.currentHp <= 0)
        {
            // 破壊
            slot.currentHp = 0;
            SpawnHitVfx(rockBreakVfxPrefab, bullet.transform.position);
            bullet.RegisterEnemyHitAsBounce();
            PlayRockSe(rockBreakClips);
            DestroyRock(slot);
        }
        else
        {
            // ヒット（未破壊）
            SpawnHitVfx(rockHitVfxPrefab, bullet.transform.position);
            PlayRockSe(rockHitClips);
        }
    }

    private void OnCoreHitWithDamage(RockSlotData slot, EnemyBullet bullet, int damage)
    {
        if (slot.isCoreDestroyed) return;

        Vector3 pos = slot.coreRenderer != null
            ? slot.coreRenderer.transform.position
            : slot.enemyPart.transform.position;
        SpawnHitVfx(rockHitVfxPrefab, pos);

        slot.currentCoreHp -= damage;
        if (slot.currentCoreHp <= 0)
            ShatterCore(slot);
    }

    private void ShatterCore(RockSlotData slot)
    {
        if (slot.isCoreDestroyed) return;
        slot.isCoreDestroyed = true;

        Vector3 pos = slot.coreRenderer != null
            ? slot.coreRenderer.transform.position
            : slot.enemyPart.transform.position;

        // CoreSprite非表示・コライダー無効化
        if (slot.coreRenderer != null)
            slot.coreRenderer.gameObject.SetActive(false);
        if (slot.slotCollider != null)
            slot.slotCollider.enabled = false;
        if (slot.enemyPart != null)
        {
            slot.enemyPart.suppressDamage = true;
            slot.enemyPart.enableDamage   = false;
        }

        // 追加ダメージ（1回のみ）
        if (_enemyStats != null)
            _enemyStats.Damage(coreBonusDamage);

        // Shield破壊VFX
        if (coreBreakVfxPrefab != null)
            Instantiate(coreBreakVfxPrefab, pos, Quaternion.identity);

        // Shield破壊SE
        if (coreBreakSeClip != null && _audioSource != null)
        {
            float vol = coreBreakSeVolume *
                (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
            _audioSource.PlayOneShot(coreBreakSeClip, vol);
        }
    }

    private void SpawnHitVfx(GameObject prefab, Vector3 pos)
    {
        if (prefab != null) Instantiate(prefab, pos, Quaternion.identity);
    }

    private void PlayRockSe(AudioClip[] clips)
    {
        if (clips == null || _audioSource == null) return;
        var valid = System.Array.FindAll(clips, c => c != null);
        if (valid.Length == 0) return;
        float vol = rockSeVolume *
            (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
        _audioSource.PlayOneShot(valid[Random.Range(0, valid.Length)], vol);
    }

    private void DestroyRock(RockSlotData slot)
    {
        slot.isDestroyed = true;
        SetRockVisual(slot, false);

        if (slot.enemyPart != null && slot.isCore)
        {
            slot.enemyPart.suppressDamage   = false;
            slot.enemyPart.enableDamage     = true;
            slot.enemyPart.damageMultiplier = 1.0f;
        }

        if (!_regenTimerRunning)
        {
            _regenTimerRunning = true;
            if (_regenCoroutine != null) StopCoroutine(_regenCoroutine);
            _regenCoroutine = StartCoroutine(RegenRoutine());
        }
    }

    private void SetRockVisual(RockSlotData slot, bool intact, bool randomizeSprite = false)
    {
        if (slot.rockRenderer != null)
        {
            if (intact && randomizeSprite && rockSpriteA != null)
            {
                // 設定済みスプライトだけ候補に入れてランダム選択
                var candidates = new System.Collections.Generic.List<Sprite> { rockSpriteA };
                if (rockSpriteB != null) candidates.Add(rockSpriteB);
                if (rockSpriteC != null) candidates.Add(rockSpriteC);
                if (rockSpriteD != null) candidates.Add(rockSpriteD);
                slot.rockRenderer.sprite = candidates[Random.Range(0, candidates.Count)];
                float z = Random.Range(0f, rockRotationMax);
                slot.rockRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, z);
            }
            // SetActive(true) 前にアルファをリセット（フェード処理による汚染防止）
            if (intact)
            {
                var rc = slot.rockRenderer.color; rc.a = 1f; slot.rockRenderer.color = rc;
            }
            slot.rockRenderer.gameObject.SetActive(intact);
        }
        if (slot.coreRenderer != null)
        {
            bool coreActive = !intact && slot.isCore;
            if (coreActive)
            {
                float z = Random.Range(0f, rockRotationMax);
                slot.coreRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, z);
                // SetActive(true) 前にアルファをリセット（フェード処理による汚染防止）
                var cc = slot.coreRenderer.color; cc.a = 1f; slot.coreRenderer.color = cc;
            }
            slot.coreRenderer.gameObject.SetActive(coreActive);
        }
        // コアは岩破壊後も当たり判定を維持（enableDamage=trueで弱点になる）
        if (slot.slotCollider != null)
        {
            slot.slotCollider.enabled = intact || slot.isCore;
            // コライダー位置をアクティブなスプライトに合わせる
            // コア表示時: CoreSprite位置 (0,0) / 岩表示時: 岩のオフセット位置
            if (!intact && slot.isCore && slot.coreRenderer != null)
                slot.slotCollider.offset = (Vector2)slot.coreRenderer.transform.localPosition;
            else if (intact)
                slot.slotCollider.offset = slot.rockColliderOffset;
        }
    }

    private void SetRockAlpha(RockSlotData slot, float alpha)
    {
        if (slot.rockRenderer != null)
        {
            var c = slot.rockRenderer.color; c.a = alpha; slot.rockRenderer.color = c;
        }
        if (slot.coreRenderer != null)
        {
            var c = slot.coreRenderer.color; c.a = alpha; slot.coreRenderer.color = c;
        }
    }

    private IEnumerator FadeRenderer(SpriteRenderer sr, float from, float to, float duration)
    {
        if (sr == null || !sr.gameObject.activeSelf) yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var c = sr.color;
            c.a = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            sr.color = c;
            yield return null;
        }
        var fc = sr.color; fc.a = to; sr.color = fc;
    }

    private IEnumerator RegenRoutine()
    {
        yield return new WaitForSeconds(rockRegenTime);
        yield return StartCoroutine(RegenAllRocksRoutine());
    }

    private IEnumerator RegenAllRocksRoutine()
    {
        _regenTimerRunning = false;
        if (rockSlots == null) yield break;

        // ── フェードアウト: 現在表示中のレンダラーを対象 ──
        if (rockFadeOutDuration > 0f)
        {
            foreach (var slot in rockSlots)
            {
                if (slot.rockRenderer != null && slot.rockRenderer.gameObject.activeSelf)
                    StartCoroutine(FadeRenderer(slot.rockRenderer, 1f, 0f, rockFadeOutDuration));
                if (slot.coreRenderer != null && slot.coreRenderer.gameObject.activeSelf)
                    StartCoroutine(FadeRenderer(slot.coreRenderer, 1f, 0f, rockFadeOutDuration));
            }
            yield return new WaitForSeconds(rockFadeOutDuration);
        }

        // ── 再生成: コア再割り当て・ビジュアルリセット ──
        AssignRandomCores();
        foreach (var slot in rockSlots)
        {
            slot.isDestroyed     = false;
            slot.currentHp       = Mathf.Max(1, rockMaxHp);
            slot.isCoreDestroyed = false;
            slot.currentCoreHp   = Mathf.Max(1, coreMaxHp);
            SetRockVisual(slot, true, randomizeSprite: true);
            SetRockAlpha(slot, 0f);
            if (slot.enemyPart != null)
            {
                slot.enemyPart.suppressDamage = true;
                slot.enemyPart.enableDamage   = false;
            }
        }

        // ── フェードイン: 全スロット並列 ──
        if (rockFadeInDuration > 0f)
        {
            foreach (var slot in rockSlots)
                StartCoroutine(FadeRenderer(slot.rockRenderer, 0f, 1f, rockFadeInDuration));
        }
        else
        {
            foreach (var slot in rockSlots)
                SetRockAlpha(slot, 1f);
        }
    }

    // ======================================================
    // Attack Loop
    // ======================================================

    private IEnumerator AttackLoop()
    {
        yield return new WaitForSeconds(firstAttackDelay);
        while (true)
        {
            bool useRight = alternateArms ? _rightArmNext : Random.value < 0.5f;
            if (alternateArms) _rightArmNext = !_rightArmNext;

            yield return StartCoroutine(ArmAttackRoutine(useRight));
            float interval = useRight
                ? Random.Range(rightArmIntervalMin, rightArmIntervalMax)
                : Random.Range(leftArmIntervalMin,  leftArmIntervalMax);
            yield return new WaitForSeconds(interval);
        }
    }

    private IEnumerator ArmAttackRoutine(bool right)
    {
        _isArmAttacking = true;

        // 左腕攻撃はスプライトを水平ミラーして右腕→左腕に見せる
        bool flip = !right;

        SetBodySprite(armUpFrame,   flip);
        yield return new WaitForSeconds(armUpFrame.duration);

        SetBodySprite(armMid1Frame, flip);
        yield return new WaitForSeconds(armMid1Frame.duration);

        SetBodySprite(armMid2Frame, flip);
        yield return new WaitForSeconds(armMid2Frame.duration);

        // ArmDown 全フレームを順番に再生（SE・砂煙は1枚目のみ）
        if (armDownFrames != null && armDownFrames.Length > 0)
        {
            for (int i = 0; i < armDownFrames.Length; i++)
            {
                SetBodySprite(armDownFrames[i], flip);
                if (i == 0)
                {
                    PlaySe(slamClip);
                    SpawnSmoke(right);
                }
                yield return new WaitForSeconds(armDownFrames[i].duration);
            }
        }

        SetBodySprite(idleFrame, flipX: false);
        _isArmAttacking = false;
    }

    // ======================================================
    // Body sprite helpers
    // ======================================================

    private void SetBodySprite(BodyFrame frame, bool flipX = false)
    {
        if (bodyRenderer == null || frame.sprite == null) return;
        bodyRenderer.sprite = frame.sprite;
        bodyRenderer.flipX  = flipX;
        var t = bodyRenderer.transform;
        float x = flipX ? -frame.offset.x : frame.offset.x;
        t.localPosition = new Vector3(_spawnLocalPosition.x + x, _spawnLocalPosition.y + frame.offset.y, _spawnLocalPosition.z);
    }

    // ======================================================
    // Sand Smoke
    // ======================================================

    private void SpawnSmoke(bool right)
    {
        if (smokeParticlePrefab == null) return;
        float   offsetX = right ? slamSmokeOffset.x : -slamSmokeOffset.x;
        Vector3 pos     = transform.position + new Vector3(offsetX, slamSmokeOffset.y, 0f);

        var go    = Instantiate(smokeParticlePrefab, pos, Quaternion.identity);
        var smoke = go.GetComponent<SmokeCloud>();
        if (smoke == null) { Destroy(go); return; }

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
            var gm = grain.main;
            gm.startColor      = new ParticleSystem.MinMaxGradient(sandGrainColor);
            if (sandGrainLifetime > 0f)
                gm.startLifetime = new ParticleSystem.MinMaxCurve(sandGrainLifetime);
            gm.gravityModifier = sandGrainGravityModifier;
            gm.startSize       = new ParticleSystem.MinMaxCurve(sandGrainSizeMin, sandGrainSizeMax);
            gm.startSpeed      = new ParticleSystem.MinMaxCurve(sandGrainSpeedMin, sandGrainSpeedMax);
            var grainEmission  = grain.emission;
            grainEmission.rateOverTime = sandGrainEmissionRate;

            var vel  = grain.velocityOverLifetime;
            vel.enabled = true;
            vel.space   = ParticleSystemSimulationSpace.World;
            vel.y       = new ParticleSystem.MinMaxCurve(0.4f);

            if (sandGrainSpreadX > 0f)
            {
                var noise          = grain.noise;
                noise.enabled      = true;
                noise.separateAxes = true;
                noise.strengthX    = new ParticleSystem.MinMaxCurve(sandGrainSpreadX);
                noise.strengthY    = new ParticleSystem.MinMaxCurve(0f);
                noise.strengthZ    = new ParticleSystem.MinMaxCurve(0f);
                noise.frequency    = 0.5f;
                noise.scrollSpeed  = new ParticleSystem.MinMaxCurve(0.5f);
                noise.octaveCount  = 1;
                noise.quality      = ParticleSystemNoiseQuality.Low;
            }
        }

        smoke.SetSandGrainGravity(sandGrainGravityModifier);
        if (sandGrainLifetime > 0f)
            smoke.SetSandGrainLifetime(sandGrainLifetime);
        smoke.SetSandGrainSpreadX(sandGrainSpreadX);
        smoke.SetSandGrainSpreadY(sandGrainSpreadY);
        smoke.Initialize(smokeRadius, smokeDuration, smokeExpansionSpeed, smokeCloudDissolveSE);
    }

    private void PlaySe(AudioClip clip)
    {
        if (clip == null || _audioSource == null) return;
        float vol = slamSeVolume *
            (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
        _audioSource.PlayOneShot(clip, vol);
    }

    // ======================================================
    // Editor Preview
    // ======================================================

    private void OnValidate()
    {
        if (Application.isPlaying) return;
#if UNITY_EDITOR
        if (previewMode == PreviewMode.Animate)
        {
            StartEditorAnim();
            return;
        }
        StopEditorAnim();
        ApplyEditorFrame(GetPreviewFrame(previewMode), previewFlipX);
        UnityEditor.SceneView.RepaintAll();
#endif
    }

    private BodyFrame GetPreviewFrame(PreviewMode mode)
    {
        switch (mode)
        {
            case PreviewMode.ArmUp:    return armUpFrame;
            case PreviewMode.ArmMid1:  return armMid1Frame;
            case PreviewMode.ArmMid2:  return armMid2Frame;
            case PreviewMode.ArmDown:  return GetArmDownFrame(0);
            case PreviewMode.ArmDown2: return GetArmDownFrame(1);
            case PreviewMode.ArmDown3: return GetArmDownFrame(2);
            default:                   return idleFrame;
        }
    }

    private BodyFrame GetArmDownFrame(int index)
    {
        if (armDownFrames != null && index < armDownFrames.Length)
            return armDownFrames[index];
        return default;
    }

    private void ApplyEditorFrame(BodyFrame frame, bool flipX = false)
    {
        if (bodyRenderer == null || frame.sprite == null) return;
        bodyRenderer.sprite = frame.sprite;
        bodyRenderer.flipX  = flipX;
        var t = bodyRenderer.transform;
        float x = flipX ? -frame.offset.x : frame.offset.x;
        t.localPosition = new Vector3(x, frame.offset.y, t.localPosition.z);
    }

#if UNITY_EDITOR
    private bool   _editorAnimRunning;
    private double _editorAnimLastTime;
    private int    _editorAnimFrameIdx;

    private void StartEditorAnim()
    {
        _editorAnimFrameIdx = 0;
        _editorAnimLastTime = UnityEditor.EditorApplication.timeSinceStartup;
        if (!_editorAnimRunning)
        {
            _editorAnimRunning = true;
            UnityEditor.EditorApplication.update += OnEditorUpdate;
        }
        ApplyEditorFrame(GetEditorAnimFrame(0), previewFlipX);
    }

    private void StopEditorAnim()
    {
        if (!_editorAnimRunning) return;
        _editorAnimRunning = false;
        UnityEditor.EditorApplication.update -= OnEditorUpdate;
    }

    private void OnEditorUpdate()
    {
        if (this == null || !_editorAnimRunning) { StopEditorAnim(); return; }
        BodyFrame current = GetEditorAnimFrame(_editorAnimFrameIdx);
        double dur = current.duration > 0f ? current.duration : 0.1;
        double now = UnityEditor.EditorApplication.timeSinceStartup;
        if (now - _editorAnimLastTime >= dur)
        {
            _editorAnimLastTime = now;
            _editorAnimFrameIdx++;
            ApplyEditorFrame(GetEditorAnimFrame(_editorAnimFrameIdx), previewFlipX);
            UnityEditor.SceneView.RepaintAll();
        }
    }

    // Idle→ArmUp→ArmMid1→ArmMid2→ArmDown[0]→ArmDown[1]→... の順でループ
    private BodyFrame GetEditorAnimFrame(int idx)
    {
        int downCount = (armDownFrames != null) ? armDownFrames.Length : 0;
        int total     = 4 + downCount;
        int frame     = idx % Mathf.Max(total, 1);
        switch (frame)
        {
            case 0:  return idleFrame;
            case 1:  return armUpFrame;
            case 2:  return armMid1Frame;
            case 3:  return armMid2Frame;
            default:
                int di = frame - 4;
                return (di < downCount) ? armDownFrames[di] : idleFrame;
        }
    }

    private void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
    {
        if (state == UnityEditor.PlayModeStateChange.ExitingEditMode)
            StopEditorAnim();
    }
#endif

    // ======================================================
    // Context Menu
    // ======================================================

    [ContextMenu("Setup Sorting Orders")]
    private void SetupSortingOrders()
    {
#if UNITY_EDITOR
        if (bodyRenderer != null)
            bodyRenderer.sortingOrder = 0;

        if (rockSlots != null)
        {
            foreach (var slot in rockSlots)
            {
                if (slot.rockRenderer != null) slot.rockRenderer.sortingOrder = 1;
                if (slot.coreRenderer != null) slot.coreRenderer.sortingOrder = 2;
            }
        }

        UnityEditor.EditorUtility.SetDirty(gameObject);
        Debug.Log("[GolemController] Sorting orders applied.");
#endif
    }

    [ContextMenu("Apply Block Hit VFX and SE")]
    private void ApplyBlockHitVfxAndSe()
    {
#if UNITY_EDITOR
        // ヒット・破壊VFX: 両方ともVFX_EnemyHit_Normal（Block_Scatterと同じ設定）
        string enemyHitVfxPath = UnityEditor.AssetDatabase.GUIDToAssetPath("6a41cc92eb391dc44a0e4f0ef4f0470c");
        if (!string.IsNullOrEmpty(enemyHitVfxPath))
        {
            var vfx = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(enemyHitVfxPath);
            rockHitVfxPrefab   = vfx;
            rockBreakVfxPrefab = vfx;
        }

        // ヒット（未破壊）SE: 石が砕ける
        string hitSePath = UnityEditor.AssetDatabase.GUIDToAssetPath("81ad551a6b976c54b951d84412d5f550");
        if (!string.IsNullOrEmpty(hitSePath))
            rockHitClips = new[] { UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(hitSePath) };

        // 破壊SE: 打撃4・打撃5・打撃6
        string[] breakGuids = {
            "d852bae026ddd434d9d09ffaf755d40c",
            "7d5fb3f5dfe38e64a8500af473bef9d3",
            "5c227499ebf725645bb1aa69c86c7696"
        };
        var breakList = new System.Collections.Generic.List<AudioClip>();
        foreach (var guid in breakGuids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            if (!string.IsNullOrEmpty(path))
                breakList.Add(UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(path));
        }
        rockBreakClips = breakList.ToArray();

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[GolemController] Block hit VFX and SE applied.");
#endif
    }

    [ContextMenu("Fix VFX Sorting Orders")]
    private void FixVfxSortingOrders()
    {
#if UNITY_EDITOR
        // ─────────────────────────────────────────────────────────────────
        // Sorting Layers (背面→前面): Background < Wall < Gameplay < UI < Default
        //   敵/プレイヤースプライト : Default, Order 0-5
        //   ステージブロック         : Default, Order 2 (変更不要・既に正しい)
        //   中断画面 Canvas          : ScreenSpace Overlay (Sorting Layer外・常に最前面)
        //   Gameplay層VFX           : パドルトレイル/煙/JustStar → 変更不要（意図的に敵の背後）
        //   ★戦闘VFX               : Default, Order 10 (スプライト0-3より前面)
        // ─────────────────────────────────────────────────────────────────
        string[] vfxGuids =
        {
            // VFX_* シリーズ
            "898373de939c02f4a9f35a34631c3125", // VFX_ShieldBreak        (UIレイヤーバグ修正)
            "6a41cc92eb391dc44a0e4f0ef4f0470c", // VFX_EnemyHit_Normal
            "dc44c347783ba494c9e442f338f84c80", // VFX_BulletDestroy
            "a50ca36df7b88a0469637c81e2d8df48", // VFX_JustReflect
            "e4719a14ede75f544b25b3eee170dd52", // VFX_NormalReflect
            "fe73f864cdbba7148a08fd22fac2beef", // VFX_ShieldActive
            "3584cfbcb89e5304888e9e0acd083ab4", // VFX_Explosion_A_RingSparks
            "f9f66cc9118cf9049964e133c49269e4", // VFX_LongPressActivate
            "61e4e5d9238e1c74db4f662f32b191f1", // VFX_AttackBlock
            // ヒットスパーク
            "d057f80307efa494ca329edab1a483a5", // WallHitSpark_Orange
            "8185f20c7fa62d441999a2936715f417", // JustHitSpark_OrangeStrong
            "90d4613eeb75937458550d89108d71ae", // BurstEyeHitVFX
            // その他戦闘エフェクト
            "e6b73158af0b47b46972b3c5eb752e5b", // HealVFX
            "5708eba2c070bcc4f8e3909001b3173c", // JustBulletVFX
            "4854d1a6f2670734da7c54452474b28b", // CircleFormedVFX_White
            "f648b8b6427301a4b9c281f2b7c09046", // GemRevealParticle      (UIレイヤーバグ修正)
            "9666004178d3b5a48b8129825bc224d5", // FX_AmmoEject (Effects/)
            "bf5db335c0a1da646a99acbcb2d9c496", // FX_AmmoEject (Prefabs/)
        };

        int fixedCount = 0;
        foreach (var guid in vfxGuids)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) continue;

            var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            bool dirty = false;
            foreach (var psr in prefab.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                if (psr.sortingLayerID != 0 || psr.sortingOrder != 10)
                {
                    psr.sortingLayerID = 0;
                    psr.sortingOrder   = 10;
                    UnityEditor.EditorUtility.SetDirty(psr);
                    dirty = true;
                }
            }
            if (dirty)
            {
                UnityEditor.EditorUtility.SetDirty(prefab);
                fixedCount++;
            }
        }

        UnityEditor.AssetDatabase.SaveAssets();
        Debug.Log($"[GolemController] Fix VFX Sorting Orders: {fixedCount} prefab(s) updated. " +
                  "(Default layer / Order 10 — sprites 0-3 の前面、Canvas UIの背後)");
#endif
    }

    [ContextMenu("Apply Core Break Effect")]
    private void ApplyCoreBreakEffect()
    {
#if UNITY_EDITOR
        string vfxPath = UnityEditor.AssetDatabase.GUIDToAssetPath("a8d563d479ad4f7584989dd31e7cc956"); // VFX_CoreBreak
        if (!string.IsNullOrEmpty(vfxPath))
            coreBreakVfxPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(vfxPath);

        string sePath = UnityEditor.AssetDatabase.GUIDToAssetPath("0bf9f0bd2bc1a154a9133f61afc30f71");
        if (!string.IsNullOrEmpty(sePath))
            coreBreakSeClip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(sePath);

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[GolemController] Core break effect applied.");
#endif
    }

    [ContextMenu("Apply Camel Smoke Settings")]
    private void ApplyCamelSmokeSettings()
    {
#if UNITY_EDITOR
        string prefabPath = UnityEditor.AssetDatabase.GUIDToAssetPath("de70ff092b954e14590948e0660c15de");
        if (!string.IsNullOrEmpty(prefabPath))
            smokeParticlePrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);

        smokeColor           = new Color(0.9f, 0.75f, 0.3f, 0.8f);
        smokeGravityModifier = -0.008f;
        smokeRadius          = 3f;
        smokeDuration        = 10f;
        smokeExpansionSpeed  = 0.07f;
        smokeParticleSizeMin = 3.5f;
        smokeParticleSizeMax = 3.5f;
        smokeFadeInDuration  = 2.5f;
        smokeFadeOutDuration = 3.5f;
        smokeEmissionRate    = 3f;

        string sePath = UnityEditor.AssetDatabase.GUIDToAssetPath("ff905b34adca9c7469577b7fa9f06e43");
        if (!string.IsNullOrEmpty(sePath))
            smokeCloudDissolveSE = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(sePath);

        sandGrainColor           = new Color(0.509434f, 0.40485677f, 0.045656808f, 1f);
        sandGrainSizeMin         = 0.05f;
        sandGrainSizeMax         = 0.1f;
        sandGrainEmissionRate    = 300f;
        sandGrainSpeedMin        = 0.1f;
        sandGrainSpeedMax        = 0.6f;
        sandGrainGravityModifier = -0.2f;
        sandGrainLifetime        = 1.5f;
        sandGrainSpreadX         = 1.5f;
        sandGrainSpreadY         = 1.5f;

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[GolemController] Camel smoke settings applied.");
#endif
    }

    [ContextMenu("Setup Death VFX (Golem Size)")]
    private void SetupDeathVfxGolemSize()
    {
#if UNITY_EDITOR
        deathVfxConfig.burstCount     = 200;
        deathVfxConfig.shapeAngle     = 30f;
        deathVfxConfig.startSpeedMin  = 4f;
        deathVfxConfig.startSpeedMax  = 20f;
        deathVfxConfig.startSizeMin   = 0.08f;
        deathVfxConfig.startSizeMax   = 0.6f;
        deathVfxConfig.lifetimeMin    = 0.5f;
        deathVfxConfig.lifetimeMax    = 1.2f;
        deathVfxConfig.gravity        = 0.4f;
        deathVfxConfig.startColorMin  = new Color(1f, 0.25f, 0f, 1f);
        deathVfxConfig.startColorMax  = new Color(1f, 0.95f, 0.6f, 1f);
        deathVfxConfig.ringScale      = 18f;
        deathVfxConfig.ringMinHue     = 0f;
        deathVfxConfig.ringMaxHue     = 0.14f;
        deathVfxConfig.ringCycleSpeed = 4f;
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[GolemController] Death VFX (Golem Size) applied.");
#endif
    }

    [ContextMenu("Setup Rock Slots From Hierarchy")]
    private void SetupRockSlotsFromHierarchy()
    {
#if UNITY_EDITOR
        var list = new List<RockSlotData>();
        for (int i = 1; i <= 10; i++)
        {
            Transform slot = transform.Find($"RockSlot_{i:D2}");
            if (slot == null) continue;

            list.Add(new RockSlotData
            {
                enemyPart    = slot.GetComponent<EnemyPart>(),
                rockRenderer = slot.Find("RockSprite")?.GetComponent<SpriteRenderer>(),
                coreRenderer = slot.Find("CoreSprite")?.GetComponent<SpriteRenderer>()
            });
        }
        rockSlots = list.ToArray();
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[GolemController] {rockSlots.Length} rock slot(s) configured.");
#endif
    }
}
