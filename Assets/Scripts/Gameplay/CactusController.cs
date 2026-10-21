using UnityEngine;

public class CactusController : MonoBehaviour
{
    [System.Serializable]
    public class CactusColliderData
    {
        public Vector2 size = new Vector2(1f, 1.5f);
        public Vector2 offset = Vector2.zero;
    }

    public enum EditorPreviewMode { Idle, Run1, Run2, Animate }

    [Header("Sprites")]
    [SerializeField] private Sprite idleSprite;
    [SerializeField] private Sprite[] runSprites;

    [Header("Animation")]
    [Range(0.01f, 1f)]
    [SerializeField] private float runFrameInterval = 0.12f;

    [Header("Colliders")]
    [SerializeField] private BoxCollider2D bodyCollider;
    [SerializeField] private CactusColliderData idleCollider = new CactusColliderData();
    [SerializeField] private CactusColliderData run1Collider = new CactusColliderData();
    [SerializeField] private CactusColliderData run2Collider = new CactusColliderData();

    [Header("Needle Burst（咆哮弾：針を全方向へ同時発射）")]
    [Tooltip("ONで、ダッシュが止まっている間に針を全方向へ同時に撃つ")]
    [SerializeField] private bool    enableNeedleBurst      = true;
    [Tooltip("針の本数（360度を等分）")]
    [SerializeField] private int     needleCount            = 12;
    [Tooltip("針の弾。EnemyData.bulletTypes のインデックス（★仮で0＝Normal。針用の弾を用意したら番号を合わせる）")]
    [SerializeField] private int     needleBulletTypeIndex  = 0;
    [Tooltip("止まってから撃つまでの秒数（この間ずっと止まっている時だけ撃つ）")]
    [SerializeField] private float   needleDelayAfterStop   = 0.3f;
    [Tooltip("前回撃ってから次に撃てるまでの最短秒数（止まるたびに毎回撃たないため）")]
    [SerializeField] private float   needleCooldown         = 4f;
    [Tooltip("スポーン後、最初に撃てるまでの秒数")]
    [SerializeField] private float   needleFirstDelay       = 2f;
    [Tooltip("ONで撃つたびに全体の向きをランダムに回す。OFFなら真右から等間隔")]
    [SerializeField] private bool    needleRandomRotation   = true;
    [Tooltip("針を並べる円の中心（本体の位置からのずれ・ワールド単位）")]
    [SerializeField] private Vector2 needleRingCenterOffset = Vector2.zero;
    [Tooltip("針を並べる円の半径（ワールド単位）。針はそれぞれ円の上の自分の位置から外向きに飛ぶ。0で中心の1点から撃つ。選択中はSceneに円と発射位置を表示")]
    [SerializeField] private float   needleRingRadius       = 0.6f;

    [Header("Editor Preview")]
    [Tooltip("Idle=待機, Run1/Run2=静止フレーム, Animate=ループ再生")]
    [SerializeField] private EditorPreviewMode previewMode = EditorPreviewMode.Idle;

    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Transform muzzlePoint;

    private EnemyMover _mover;
    private float _runTimer;
    private int _runFrameIdx;
    private bool _wasDashing;
    private Vector3 _prevPosition;

    // Needle Burst
    private EnemyShooter _shooter;
    private EnemyStats _stats;
    private Collider2D[] _ownerColliders = new Collider2D[0];
    private float _stoppedTime;
    private float _needleCooldownTimer;
    private bool _firedThisStop;

    private static float SlowTimeScale => SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;
    private static bool IsGameOver => FloorHealth.IsBrokenGlobal || PixelDancerController.IsPlayerDeadGlobal || PixelDancerController.IsDownGlobal;

#if UNITY_EDITOR
    private bool _editorAnimRunning;
    private double _editorAnimLastTime;
    private int _editorAnimFrameIdx;
#endif

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
        if (bodyCollider == null)
            bodyCollider = GetComponentInChildren<BoxCollider2D>();
        _mover = GetComponent<EnemyMover>();
        _shooter = GetComponent<EnemyShooter>();
        _stats = GetComponent<EnemyStats>();
        _prevPosition = transform.position;
    }

    private void Start()
    {
        if (!Application.isPlaying) return;
        _ownerColliders = GetComponentsInChildren<Collider2D>(true);
        _needleCooldownTimer = needleFirstDelay;
    }

    private void ApplyCollider(CactusColliderData data)
    {
        if (bodyCollider == null || data == null) return;
        bodyCollider.size = data.size;
        bodyCollider.offset = data.offset;
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
        if (spriteRenderer == null || _mover == null) return;

        bool dashing = _mover.IsCactusDashing;

        if (dashing)
        {
            float dx = transform.position.x - _prevPosition.x;
            if (dx > 0.001f)       spriteRenderer.flipX = false;
            else if (dx < -0.001f) spriteRenderer.flipX = true;
        }

        if (dashing && runSprites != null && runSprites.Length > 0)
        {
            if (!_wasDashing)
            {
                spriteRenderer.sprite = runSprites[0];
                _runFrameIdx = 0;
                _runTimer = 0f;
                ApplyCollider(run1Collider);
            }

            _runTimer += Time.deltaTime;
            if (_runTimer >= runFrameInterval)
            {
                _runTimer -= runFrameInterval;
                _runFrameIdx = (_runFrameIdx + 1) % runSprites.Length;
                spriteRenderer.sprite = runSprites[_runFrameIdx];
                ApplyCollider(_runFrameIdx == 0 ? run1Collider : run2Collider);
            }
        }
        else if (!dashing)
        {
            if (_wasDashing)
            {
                if (idleSprite != null)
                    spriteRenderer.sprite = idleSprite;
                ApplyCollider(idleCollider);
                _runTimer = 0f;
                _runFrameIdx = 0;
            }
        }

        UpdateNeedleBurst(dashing);

        _prevPosition = transform.position;
        _wasDashing = dashing;
    }

    // ─── Needle Burst ─────────────────────────────────────────────────────────
    // ダッシュが止まってから needleDelayAfterStop 秒ずっと止まっていたら、needleCount 本の針を全方向へ同時に撃つ（1回の停止で1回まで・needleCooldown 秒は撃たない）

    private void UpdateNeedleBurst(bool dashing)
    {
        float dt = Time.deltaTime * SlowTimeScale;
        if (_needleCooldownTimer > 0f) _needleCooldownTimer -= dt;

        if (dashing) { _stoppedTime = 0f; _firedThisStop = false; return; }
        _stoppedTime += dt;

        if (!enableNeedleBurst || _firedThisStop || _needleCooldownTimer > 0f || _stoppedTime < needleDelayAfterStop) return;
        if (IsGameOver || (_stats != null && _stats.HP <= 0)) return;

        if (FireNeedleBurst())
        {
            _firedThisStop = true;
            _needleCooldownTimer = needleCooldown;
        }
    }

    private bool FireNeedleBurst()
    {
        if (_shooter == null || needleCount <= 0) return false;
        EnemyData data = _shooter.GetEnemyData();
        EnemyBullet prefab = _shooter.GetBulletPrefab();
        Transform projRoot = _shooter.GetProjectileRoot();
        if (data == null || prefab == null || projRoot == null) return false;
        if (data.bulletTypes == null || needleBulletTypeIndex < 0 || needleBulletTypeIndex >= data.bulletTypes.Length)
        {
            Debug.LogWarning($"[CactusController] {name}: Needle Bullet Type Index {needleBulletTypeIndex} がEnemyDataにありません", this);
            return false;
        }
        EnemyData.BulletType bt = data.bulletTypes[needleBulletTypeIndex];

        float offset = needleRandomRotation ? Random.Range(0f, 360f / needleCount) : 0f;
        for (int i = 0; i < needleCount; i++)
        {
            float a = ((360f / needleCount) * i + offset) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            EnemyBullet bullet = EnemyBulletPool.Get(prefab, NeedleSpawnPos(dir), Quaternion.identity, projRoot);
            bullet.SetDirection(dir);
            EnemyShooter.ApplyBulletTypeToEnemyBullet(bullet, bt, data.bulletSpeed, data.bulletLifeTime, data.bulletSpriteOverride, prefab, projRoot);
            foreach (Collider2D col in _ownerColliders)
                if (col != null) bullet.SetOwnerCollisionIgnore(col, 0.15f);
            if (data.unreflectedBulletCollisionDisableTime > 0f)
                bullet.SetUnreflectedCollisionDisable(data.unreflectedBulletCollisionDisableTime);
        }

        // 発射SEは1回だけ
        AudioClip se; float vol;
        if (bt.fireSEOverride != null) { se = bt.fireSEOverride; vol = bt.fireSEOverrideVolume; }
        else { se = data.fireSE; vol = data.fireSEVolume; }
        if (se != null && vol > 0f && SeSimultaneousGuard.TryAllow("CactusNeedleBurst"))
            AudioOneShotPool.Play(se, vol * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f), NeedleRingCenter(), null, 0.1f);
        return true;
    }

    private Vector3 NeedleRingCenter() => transform.position + (Vector3)needleRingCenterOffset;
    private Vector3 NeedleSpawnPos(Vector2 dir) => NeedleRingCenter() + (Vector3)(dir * Mathf.Max(0f, needleRingRadius));

    // Play前のSceneで、針の円と発射位置を表示
    private void OnDrawGizmosSelected()
    {
        if (!enableNeedleBurst || needleCount <= 0) return;
        Vector3 c = NeedleRingCenter();
        float r = Mathf.Max(0f, needleRingRadius);
        Gizmos.color = new Color(1f, 0.35f, 0.35f, 0.9f);
        const int seg = 48;
        for (int k = 0; k < seg; k++)
        {
            float a0 = k * Mathf.PI * 2f / seg, a1 = (k + 1) * Mathf.PI * 2f / seg;
            Gizmos.DrawLine(c + new Vector3(Mathf.Cos(a0), Mathf.Sin(a0)) * r, c + new Vector3(Mathf.Cos(a1), Mathf.Sin(a1)) * r);
        }
        for (int i = 0; i < needleCount; i++)
        {
            float a = (360f / needleCount) * i * Mathf.Deg2Rad;
            Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            Vector3 p = c + (Vector3)(d * r);
            Gizmos.DrawSphere(p, 0.05f);
            Gizmos.DrawLine(p, p + (Vector3)(d * 0.3f));
        }
    }

    private void OnDrawGizmos()
    {
        if (muzzlePoint == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(muzzlePoint.position, 0.08f);
        float arm = 0.14f;
        Gizmos.DrawLine(muzzlePoint.position - Vector3.right * arm, muzzlePoint.position + Vector3.right * arm);
        Gizmos.DrawLine(muzzlePoint.position - Vector3.up    * arm, muzzlePoint.position + Vector3.up    * arm);
#if UNITY_EDITOR
        UnityEditor.Handles.Label(muzzlePoint.position + Vector3.up * 0.18f, "Muzzle");
#endif
    }

    private void OnValidate()
    {
        if (Application.isPlaying) return;

#if UNITY_EDITOR
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (bodyCollider == null)
            bodyCollider = GetComponentInChildren<BoxCollider2D>();

        if (previewMode == EditorPreviewMode.Animate)
        {
            StartEditorAnim();
        }
        else
        {
            StopEditorAnim();

            if (spriteRenderer == null) return;

            switch (previewMode)
            {
                case EditorPreviewMode.Run1:
                    if (runSprites != null && runSprites.Length > 0 && runSprites[0] != null)
                        spriteRenderer.sprite = runSprites[0];
                    ApplyCollider(run1Collider);
                    break;
                case EditorPreviewMode.Run2:
                    if (runSprites != null && runSprites.Length > 1 && runSprites[1] != null)
                        spriteRenderer.sprite = runSprites[1];
                    ApplyCollider(run2Collider);
                    break;
                default:
                    if (idleSprite != null)
                        spriteRenderer.sprite = idleSprite;
                    ApplyCollider(idleCollider);
                    break;
            }

            UnityEditor.SceneView.RepaintAll();
        }
#endif
    }

#if UNITY_EDITOR
    private void StartEditorAnim()
    {
        if (runSprites == null || runSprites.Length == 0) return;

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
        if (this == null || !_editorAnimRunning || runSprites == null || runSprites.Length == 0)
        {
            StopEditorAnim();
            return;
        }

        double now = UnityEditor.EditorApplication.timeSinceStartup;
        if (now - _editorAnimLastTime >= runFrameInterval)
        {
            _editorAnimLastTime = now;
            _editorAnimFrameIdx = (_editorAnimFrameIdx + 1) % runSprites.Length;
            ApplyEditorFrame(_editorAnimFrameIdx);
        }
    }

    private void ApplyEditorFrame(int index)
    {
        if (spriteRenderer == null || runSprites == null || index >= runSprites.Length) return;
        if (runSprites[index] != null)
            spriteRenderer.sprite = runSprites[index];
        ApplyCollider(index == 0 ? run1Collider : run2Collider);
        if (bodyCollider != null)
            UnityEditor.EditorUtility.SetDirty(bodyCollider);
        UnityEditor.SceneView.RepaintAll();
    }

    private void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
    {
        if (state == UnityEditor.PlayModeStateChange.ExitingEditMode)
            StopEditorAnim();
    }
#endif
}
