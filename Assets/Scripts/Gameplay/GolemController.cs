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
        Head_Idle, Head_Attack,
        Arm_Idle, Arm_Windup, Arm_PreSlam, Arm_Slam,
        Animate
    }

    public enum PreviewArmSide { Both, RightOnly, LeftOnly }

    [System.Serializable]
    public struct ArmFrame
    {
        [Tooltip("腕スプライト（左腕スプライト。ArmRightはX反転）")]
        public Sprite sprite;
        [Tooltip("ArmRightの位置オフセット（ArmLeftはXミラー）")]
        public Vector2 rightArmOffset;
        [Tooltip("ArmRightのZ軸回転（度）。ArmLeftは符号反転）")]
        public float rotationZ;
        [Tooltip("このフレームを維持する秒数（0でデフォルト値を使用）")]
        public float duration;
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
    }

    // ======================================================
    // Inspector fields
    // ======================================================

    [Header("Head")]
    [SerializeField] private SpriteRenderer headRenderer;
    [SerializeField] private Sprite         headIdleSprite;
    [SerializeField] private Vector2        headIdleOffset;
    [SerializeField] private Sprite         headAttackSprite;
    [SerializeField] private Vector2        headAttackOffset;
    [Tooltip("EnemyShooterのOnFired後、顔攻撃スプライトを維持する秒数")]
    [SerializeField] private float          headAttackDuration = 0.4f;

    [Header("Arm Animation")]
    [SerializeField] private SpriteRenderer armRightRenderer;
    [SerializeField] private SpriteRenderer armLeftRenderer;
    [SerializeField] private ArmFrame       armIdleFrame;
    [SerializeField] private ArmFrame       armWindupFrame;
    [SerializeField] private ArmFrame       armPreSlamFrame;
    [SerializeField] private ArmFrame       armSlamFrame;

    [Header("Arm Timing")]
    [Tooltip("最初のアーム攻撃まで待機する秒数")]
    [SerializeField] private float firstAttackDelay   = 2f;
    [Tooltip("各アーム攻撃の間隔（秒）")]
    [SerializeField] private float attackInterval     = 4f;
    [Tooltip("左右のアームを交互に使う")]
    [SerializeField] private bool  alternateArms      = true;
    [Tooltip("ArmFrame.durationが0のときのWindupデフォルト秒数")]
    [SerializeField] private float defaultWindupDur   = 0.5f;
    [Tooltip("ArmFrame.durationが0のときのPreSlamデフォルト秒数")]
    [SerializeField] private float defaultPreSlamDur  = 0.2f;
    [Tooltip("ArmFrame.durationが0のときのSlamデフォルト秒数")]
    [SerializeField] private float defaultSlamDur     = 0.3f;

    [Header("Rock Slots")]
    [SerializeField] private RockSlotData[] rockSlots;
    [Tooltip("コアとなるスロット数（起動時・再生成時にランダム決定）")]
    [SerializeField] private int  coreCount    = 4;
    [Tooltip("最初の岩破壊から全再生成までの秒数")]
    [SerializeField] private float rockRegenTime = 60f;

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
    [SerializeField] private Transform firePointArmRight;
    [SerializeField] private Transform firePointArmLeft;
    [SerializeField] private AudioClip slamClip;
    [Range(0f, 1f)]
    [SerializeField] private float     slamSeVolume = 1f;

    [Header("Rock SE")]
    [Tooltip("岩が壊れたとき（ブロックヒット）のSE")]
    [SerializeField] private AudioClip rockBreakClip;
    [Tooltip("Justヒットで岩が壊れなかったときのSE（省略可）")]
    [SerializeField] private AudioClip rockBlockedClip;
    [Range(0f, 1f)]
    [SerializeField] private float     rockSeVolume = 1f;

    [Header("Editor Preview")]
    [SerializeField] private PreviewMode    previewMode;
    [SerializeField] private PreviewArmSide previewArmSide = PreviewArmSide.Both;

    // ======================================================
    // Runtime state
    // ======================================================

    private EnemyMover    _mover;
    private EnemyShooter  _shooter;
    private AudioSource   _audioSource;
    private bool          _rightArmNext     = true;
    private bool          _isArmAttacking;
    private Coroutine     _headAttackCoroutine;
    private Coroutine     _regenCoroutine;
    private bool          _regenTimerRunning;

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
        _shooter     = GetComponent<EnemyShooter>();
        _audioSource = GetComponent<AudioSource>();

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
        InitRockSlots();
        SetHeadSprite(false);
        ApplyBothArms(armIdleFrame);
        StartCoroutine(AttackLoop());
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
            slot.isDestroyed = false;
            SetRockVisual(slot, true);

            if (slot.enemyPart == null) continue;
            slot.enemyPart.suppressDamage = true;
            slot.enemyPart.enableDamage   = false;

            int idx = i;
            slot.enemyPart.OnHitWhileSuppressed += (bullet) => OnRockSlotHit(rockSlots[idx], bullet);
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

        // Justヒット（isPowered）は岩を壊さない
        if (bullet.DamageMultiplier > 1.0001f)
        {
            PlayRockSe(rockBlockedClip);
            return;
        }

        bullet.RegisterEnemyHitAsBounce();
        PlayRockSe(rockBreakClip);
        DestroyRock(slot);
    }

    private void PlayRockSe(AudioClip clip)
    {
        if (clip == null || _audioSource == null) return;
        float vol = rockSeVolume *
            (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
        _audioSource.PlayOneShot(clip, vol);
    }

    private void DestroyRock(RockSlotData slot)
    {
        slot.isDestroyed = true;
        SetRockVisual(slot, false);

        if (slot.enemyPart != null)
        {
            if (slot.isCore)
            {
                // コア露出: 以降のヒットは直接ボスHPにダメージ
                slot.enemyPart.suppressDamage   = false;
                slot.enemyPart.enableDamage     = true;
                slot.enemyPart.damageMultiplier = 1.0f;
            }
            // ダミー: suppressDamage=trueのまま（OnHitWhileSuppressedが来てもisDestroyedでスキップ）
        }

        // 最初の破壊でリジェネタイマー開始
        if (!_regenTimerRunning)
        {
            _regenTimerRunning = true;
            if (_regenCoroutine != null) StopCoroutine(_regenCoroutine);
            _regenCoroutine = StartCoroutine(RegenRoutine());
        }
    }

    private void SetRockVisual(RockSlotData slot, bool intact)
    {
        if (slot.rockRenderer != null) slot.rockRenderer.gameObject.SetActive(intact);
        if (slot.coreRenderer != null) slot.coreRenderer.gameObject.SetActive(!intact && slot.isCore);
    }

    private IEnumerator RegenRoutine()
    {
        yield return new WaitForSeconds(rockRegenTime);
        RegenAllRocks();
    }

    private void RegenAllRocks()
    {
        _regenTimerRunning = false;
        AssignRandomCores();
        if (rockSlots == null) return;
        foreach (var slot in rockSlots)
        {
            slot.isDestroyed = false;
            SetRockVisual(slot, true);
            if (slot.enemyPart != null)
            {
                slot.enemyPart.suppressDamage = true;
                slot.enemyPart.enableDamage   = false;
            }
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
            yield return new WaitForSeconds(attackInterval);
        }
    }

    private IEnumerator ArmAttackRoutine(bool right)
    {
        _isArmAttacking = true;
        SetHeadSprite(true);

        ApplySingleArm(armWindupFrame, right);
        yield return new WaitForSeconds(armWindupFrame.duration > 0f ? armWindupFrame.duration : defaultWindupDur);

        ApplySingleArm(armPreSlamFrame, right);
        yield return new WaitForSeconds(armPreSlamFrame.duration > 0f ? armPreSlamFrame.duration : defaultPreSlamDur);

        ApplySingleArm(armSlamFrame, right);
        PlaySe(slamClip);
        SpawnSmoke(right);
        yield return new WaitForSeconds(armSlamFrame.duration > 0f ? armSlamFrame.duration : defaultSlamDur);

        ApplyBothArms(armIdleFrame);
        _isArmAttacking = false;
        if (_headAttackCoroutine == null)
            SetHeadSprite(false);
    }

    // ======================================================
    // Arm / Head helpers
    // ======================================================

    private void ApplyBothArms(ArmFrame frame)
    {
        ApplyToArm(armRightRenderer, frame, mirrorX: false);
        ApplyToArm(armLeftRenderer,  frame, mirrorX: true);
    }

    private void ApplySingleArm(ArmFrame frame, bool right)
    {
        if (right)
            ApplyToArm(armRightRenderer, frame, mirrorX: false);
        else
            ApplyToArm(armLeftRenderer,  frame, mirrorX: true);
    }

    private void ApplyToArm(SpriteRenderer sr, ArmFrame frame, bool mirrorX)
    {
        if (sr == null || frame.sprite == null) return;
        sr.sprite = frame.sprite;
        float x  = mirrorX ? -frame.rightArmOffset.x : frame.rightArmOffset.x;
        float rz = mirrorX ? -frame.rotationZ         : frame.rotationZ;
        var t = sr.transform;
        t.localPosition = new Vector3(x, frame.rightArmOffset.y, t.localPosition.z);
        t.localRotation = Quaternion.Euler(0f, 0f, rz);
    }

    private void SetHeadSprite(bool attack)
    {
        if (headRenderer == null) return;
        Sprite  sp     = attack ? headAttackSprite : headIdleSprite;
        Vector2 offset = attack ? headAttackOffset  : headIdleOffset;
        if (sp != null) headRenderer.sprite = sp;
        var t = headRenderer.transform;
        t.localPosition = new Vector3(offset.x, offset.y, t.localPosition.z);
    }

    private void OnShooterFired()
    {
        if (_headAttackCoroutine != null) StopCoroutine(_headAttackCoroutine);
        _headAttackCoroutine = StartCoroutine(HeadAttackCoroutine());
    }

    private IEnumerator HeadAttackCoroutine()
    {
        SetHeadSprite(true);
        yield return new WaitForSeconds(headAttackDuration);
        _headAttackCoroutine = null;
        if (!_isArmAttacking)
            SetHeadSprite(false);
    }

    // ======================================================
    // Sand Smoke
    // ======================================================

    private void SpawnSmoke(bool right)
    {
        if (smokeParticlePrefab == null) return;
        Transform fp  = right ? firePointArmRight : firePointArmLeft;
        Vector3   pos = fp != null ? fp.position : transform.position;

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

        // Head は常に適用（モードで Idle/Attack を切り替え）
        bool showAttackHead = (previewMode == PreviewMode.Head_Attack);
        ApplyEditorHead(
            showAttackHead ? headAttackSprite : headIdleSprite,
            showAttackHead ? headAttackOffset  : headIdleOffset);

        // Arms は常に適用（モードでフレーム、previewArmSideで左右を切り替え）
        ArmFrame af = GetEditorFrame(previewMode);
        switch (previewArmSide)
        {
            case PreviewArmSide.RightOnly:
                ApplyEditorArm(armRightRenderer, af, mirrorX: false);
                if (armLeftRenderer  != null) armLeftRenderer.sprite  = null;
                break;
            case PreviewArmSide.LeftOnly:
                ApplyEditorArm(armLeftRenderer,  af, mirrorX: true);
                if (armRightRenderer != null) armRightRenderer.sprite = null;
                break;
            default:
                ApplyEditorBothArms(af);
                break;
        }
        UnityEditor.SceneView.RepaintAll();
#endif
    }

    private void ApplyEditorHead(Sprite sprite, Vector2 offset)
    {
        if (headRenderer == null || sprite == null) return;
        headRenderer.sprite = sprite;
        var t = headRenderer.transform;
        t.localPosition = new Vector3(offset.x, offset.y, t.localPosition.z);
    }

    private void ApplyEditorArm(SpriteRenderer sr, ArmFrame frame, bool mirrorX)
    {
        if (sr == null || frame.sprite == null) return;
        sr.sprite = frame.sprite;
        float x  = mirrorX ? -frame.rightArmOffset.x : frame.rightArmOffset.x;
        float rz = mirrorX ? -frame.rotationZ         : frame.rotationZ;
        var t = sr.transform;
        t.localPosition = new Vector3(x, frame.rightArmOffset.y, t.localPosition.z);
        t.localRotation = Quaternion.Euler(0f, 0f, rz);
    }

    private void ApplyEditorBothArms(ArmFrame frame)
    {
        ApplyEditorArm(armRightRenderer, frame, mirrorX: false);
        ApplyEditorArm(armLeftRenderer,  frame, mirrorX: true);
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
        ApplyEditorArmsBySide(GetEditorFrame(0));
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
        ArmFrame frame = GetEditorFrame(_editorAnimFrameIdx);
        float    dur   = frame.duration > 0f ? frame.duration : 0.3f;
        double   now   = UnityEditor.EditorApplication.timeSinceStartup;
        if (now - _editorAnimLastTime >= dur)
        {
            _editorAnimLastTime = now;
            _editorAnimFrameIdx = (_editorAnimFrameIdx + 1) % 4;
            ApplyEditorArmsBySide(GetEditorFrame(_editorAnimFrameIdx));
        }
    }

    private void ApplyEditorArmsBySide(ArmFrame frame)
    {
        switch (previewArmSide)
        {
            case PreviewArmSide.RightOnly:
                ApplyEditorArm(armRightRenderer, frame, mirrorX: false);
                if (armLeftRenderer  != null) armLeftRenderer.sprite  = null;
                break;
            case PreviewArmSide.LeftOnly:
                ApplyEditorArm(armLeftRenderer,  frame, mirrorX: true);
                if (armRightRenderer != null) armRightRenderer.sprite = null;
                break;
            default:
                ApplyEditorBothArms(frame);
                break;
        }
        UnityEditor.SceneView.RepaintAll();
    }

    private ArmFrame GetEditorFrame(int idx)
    {
        switch (idx)
        {
            case 0:  return armIdleFrame;
            case 1:  return armWindupFrame;
            case 2:  return armPreSlamFrame;
            case 3:  return armSlamFrame;
            default: return armIdleFrame;
        }
    }

    private ArmFrame GetEditorFrame(PreviewMode mode)
    {
        switch (mode)
        {
            case PreviewMode.Arm_Windup:  return armWindupFrame;
            case PreviewMode.Arm_PreSlam: return armPreSlamFrame;
            case PreviewMode.Arm_Slam:    return armSlamFrame;
            default:                      return armIdleFrame;
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

    [ContextMenu("Setup Default Offsets")]
    private void SetupDefaultOffsets()
    {
#if UNITY_EDITOR
        headIdleOffset   = new Vector2(0f, 10.0f);
        headAttackOffset = new Vector2(0f, 10.0f);

        armIdleFrame.rightArmOffset    = new Vector2(9.5f, -5.0f);
        armWindupFrame.rightArmOffset  = new Vector2(3.0f,  7.0f);
        armPreSlamFrame.rightArmOffset = new Vector2(2.0f,  6.5f);
        armSlamFrame.rightArmOffset    = new Vector2(8.8f, -5.0f);

        UnityEditor.EditorUtility.SetDirty(this);
        ApplyEditorHead(headIdleSprite, headIdleOffset);
        ApplyEditorBothArms(armIdleFrame);
        UnityEditor.SceneView.RepaintAll();
        Debug.Log("[GolemController] Default offsets applied.");
#endif
    }

    [ContextMenu("Setup Sorting Orders")]
    private void SetupSortingOrders()
    {
#if UNITY_EDITOR
        if (headRenderer != null)
            headRenderer.sortingOrder = 2;

        if (rockSlots != null)
        {
            foreach (var slot in rockSlots)
            {
                if (slot.rockRenderer != null) slot.rockRenderer.sortingOrder = 2;
                if (slot.coreRenderer != null) slot.coreRenderer.sortingOrder = 3;
            }
        }

        UnityEditor.EditorUtility.SetDirty(gameObject);
        Debug.Log("[GolemController] Sorting orders applied.");
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
