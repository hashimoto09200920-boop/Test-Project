using System.Collections;
using UnityEngine;

// 尾の見た目・発射だけを担当（HP/破壊は ArcGuardTailHealth が別担当）。
// ArcGuardController から SetMode()/PlaySweepRoutine() で駆動される。
// ExecuteAlways: Play前のEditor PreviewでOnEnable/EditorApplication.updateが確実に動くようにする。
[ExecuteAlways]
public class ArcGuardTailAnimator : MonoBehaviour
{
    public enum TailMode
    {
        IdleSway, // しなり・伸縮ループ + MultiWarhead発射（本体Idle中・スライド移動中）
        Frozen,   // しなり・伸縮ループの見た目はそのまま、MultiWarhead発射だけ止める（ジャンプ/爪攻撃/咆哮中）
    }

    public enum ArcGuardTailPreviewState
    {
        IdleSwayAnimate, // idleSwayFrames全部をループ再生（発射あり相当の見た目）
        Frozen,          // idleSwayFrames全部をループ再生（発射だけ無い見た目。実際に発射はしないので見た目はIdleSwayAnimateと同じ）
        SweepAnimate,    // sweepFrames全部をループ再生
        // 以下、idleSwayFramesの該当インデックスだけを静止表示（位置確認用。Claw Mark Position Previewと同じ用途）
        Tail1, Tail2, Tail3, Tail4, Tail5, Tail6, Tail7, Tail8,
        Tail9, Tail10, Tail11, Tail12, Tail13, Tail14, Tail15, Tail16,
    }

    [System.Serializable]
    public class ArcGuardTailAnimFrame
    {
        public Sprite  sprite;
        public Vector2 offset;
        [Tooltip("表示秒数")]
        public float   duration;
        [Tooltip("マズル位置（尾の先端。ローカル座標）")]
        public Vector2 muzzleOffset;
        [Tooltip("スプライトのZ軸回転（度）")]
        public float   rotationZ;
    }

    [System.Serializable]
    public class ArcGuardTailIdleSwayFrame
    {
        public Sprite  sprite;
        public Vector2 offset;
        [Tooltip("表示秒数の最小値")]
        public float   durationMin;
        [Tooltip("表示秒数の最大値（Min以下ならMinを固定値として使う）")]
        public float   durationMax;
        [Tooltip("マズル位置（尾の先端。ローカル座標）")]
        public Vector2 muzzleOffset;
        [Tooltip("スプライトのZ軸回転（度）")]
        public float   rotationZ;

        /// <summary>Min〜Maxの範囲でランダムな表示秒数を1回分だけ決める</summary>
        public float RollDuration()
        {
            if (durationMax > durationMin) return Random.Range(durationMin, durationMax);
            if (durationMin > 0f) return durationMin;
            return 0.15f;
        }
    }

    // ======================================================
    // Inspector fields
    // ======================================================

    [Header("References")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Header("Editor Preview")]
    [SerializeField] private ArcGuardTailPreviewState previewState = ArcGuardTailPreviewState.IdleSwayAnimate;

    [Header("Sprites - Idle Sway（しなり・伸縮。最大16枚）")]
    [NonReorderable]
    [SerializeField] private ArcGuardTailIdleSwayFrame[] idleSwayFrames;

    [Header("Sprites - Sweep（薙ぎ払い、後半フェーズ限定）")]
    [NonReorderable]
    [SerializeField] private ArcGuardTailAnimFrame[] sweepFrames;

    [Header("MultiWarhead（Idle Sway中、先端から発射）")]
    [SerializeField] private float multiWarheadInterval = 1.2f;
    [SerializeField] private int   multiWarheadBulletTypeIndex = 0;

    [Header("Sweep Domino Fire（付け根→先端へ1フレームずつ発射）")]
    [Tooltip("フレームのDurationが未設定(0)の場合のフォールバック秒数")]
    [SerializeField] private float dominoFrameDuration = 0.05f;
    [SerializeField] private int   sweepBulletTypeIndex = 1;

    [Header("Debug")]
    [SerializeField] private bool logDebug = false;

    // ======================================================
    // Runtime
    // ======================================================

    private TailMode _mode = TailMode.IdleSway;
    private EnemyData _enemyData;
    private bool _isDead;
    private Coroutine _idleSwayCoroutine;

    // SetFlip直後にflip後の符号で即座に再適用するため、直近にApplyFrameした内容をキャッシュしておく
    private Vector2 _currentOffset;
    private float   _currentRotationZ;

    // Jump/爪攻撃/咆哮のたびにSetMode()でIdleSwayLoopが再起動されるため、
    // コルーチンのローカル変数にすると毎回0に戻ってしまう。インスタンスフィールドにして再起動をまたいで積算する。
    private float _warheadTimer;

    /// <summary>本体側のSpawnBullet相当を呼ぶコールバック（Owner Collision Ignore等を本体側の実装に一本化するため）。</summary>
    private System.Action<Vector3, Vector2, EnemyData.BulletType> _spawnBullet;

    // ======================================================
    // Public API（ArcGuardControllerから駆動）
    // ======================================================

    public void Initialize(EnemyData enemyData, System.Action<Vector3, Vector2, EnemyData.BulletType> spawnBulletCallback)
    {
        _enemyData = enemyData;
        _spawnBullet = spawnBulletCallback;

        // _modeの初期値は既にIdleSwayのため、SetMode(IdleSway)は早期returnで何もしない。
        // ここで明示的にループを開始する。
        if (_idleSwayCoroutine == null)
            _idleSwayCoroutine = StartCoroutine(IdleSwayLoop(true));
    }

    public void SetFlip(bool flipX)
    {
        if (spriteRenderer == null) return;
        if (spriteRenderer.flipX == flipX) return;
        spriteRenderer.flipX = flipX;

        // flipXだけ切り替えるとスプライトは即座に反転するが、位置/回転は次のApplyFrameまで古い符号のまま残り、
        // 体から浮いて見える一瞬のズレが発生する。直近のフレーム内容を新しいflipXで即座に再適用する。
        ApplyOffset(_currentOffset);
        ApplyRotation(_currentRotationZ);
    }

    public void SetMode(TailMode mode)
    {
        if (_mode == mode) return;
        _mode = mode;

        if (_idleSwayCoroutine != null) { StopCoroutine(_idleSwayCoroutine); _idleSwayCoroutine = null; }

        // 見た目（idleSwayFramesのループ）はIdleSway/Frozen共通。Frozenだけ発射（MultiWarhead）を止める。
        _idleSwayCoroutine = StartCoroutine(IdleSwayLoop(mode == TailMode.IdleSway));
    }

    /// <summary>薙ぎ払い1回分を再生（付け根→先端へドミノ式に発射）。完了までブロックする。</summary>
    public IEnumerator PlaySweepRoutine()
    {
        if (_idleSwayCoroutine != null) { StopCoroutine(_idleSwayCoroutine); _idleSwayCoroutine = null; }

        if (sweepFrames != null && sweepFrames.Length > 0)
        {
            foreach (var frame in sweepFrames)
            {
                if (_isDead) yield break;
                if (frame == null) continue;

                ApplyFrame(frame);
                FireFromMuzzle(frame.muzzleOffset, sweepBulletTypeIndex);

                float dur = frame.duration > 0f ? frame.duration : dominoFrameDuration;
                yield return WaitScaled(dur);
            }
        }

        // SetMode()は_modeが変化しないと早期returnで何もしないため、
        // ここでは直接コルーチンを再開する（_idleSwayCoroutineはこの関数の先頭で止めたまま）
        if (!_isDead)
            _idleSwayCoroutine = StartCoroutine(IdleSwayLoop(_mode == TailMode.IdleSway));
    }

    // ======================================================
    // Idle Sway
    // ======================================================

    // canFire=falseの場合、見た目（しなり・伸縮ループ）はそのままでMultiWarhead発射だけ止める（Frozen用）
    private IEnumerator IdleSwayLoop(bool canFire)
    {
        int idx = 0;

        while (true)
        {
            if (_isDead) yield break;

            if (idleSwayFrames != null && idleSwayFrames.Length > 0)
            {
                var f = idleSwayFrames[idx % idleSwayFrames.Length];
                ApplyFrame(f);
                float frameDur = (f != null) ? f.RollDuration() : 0.15f;

                float elapsed = 0f;
                while (elapsed < frameDur)
                {
                    if (_isDead) yield break;
                    float dt = Time.deltaTime * GetTimeScale();
                    elapsed += dt;

                    if (canFire)
                    {
                        _warheadTimer += dt;
                        if (_warheadTimer >= multiWarheadInterval)
                        {
                            _warheadTimer = 0f;
                            Vector2 muzzle = idleSwayFrames[idx % idleSwayFrames.Length].muzzleOffset;
                            FireFromMuzzle(muzzle, multiWarheadBulletTypeIndex);
                        }
                    }
                    yield return null;
                }
                idx++;
            }
            else
            {
                yield return null;
            }
        }
    }

    // ======================================================
    // Bullet
    // ======================================================

    private EnemyData.BulletType GetBulletType(int index)
    {
        if (_enemyData == null || _enemyData.bulletTypes == null) return null;
        if (index < 0 || index >= _enemyData.bulletTypes.Length) return null;
        return _enemyData.bulletTypes[index];
    }

    private void FireFromMuzzle(Vector2 localMuzzleOffset, int bulletTypeIndex)
    {
        EnemyData.BulletType bt = GetBulletType(bulletTypeIndex);
        if (bt == null || _spawnBullet == null) return;

        float x = (spriteRenderer != null && spriteRenderer.flipX) ? -localMuzzleOffset.x : localMuzzleOffset.x;
        Vector3 muzzleWorld = transform.position + new Vector3(x, localMuzzleOffset.y, 0f);

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        Vector2 dir = playerObj != null
            ? ((Vector2)(playerObj.transform.position - muzzleWorld)).normalized
            : Vector2.down;

        if (bt.useMultiShot && bt.shotsPerFire > 1)
        {
            int shots   = Mathf.Max(1, bt.shotsPerFire);
            float half  = Mathf.Clamp(bt.spreadAngleDeg, 0f, 180f) * 0.5f;
            float delay = bt.multiShotLaunchDelay;

            if (delay > 0.0001f)
                StartCoroutine(FireMultiDelayed(muzzleWorld, dir, shots, half, bt, bt.multiShotSpawnOffset, delay));
            else
                FireMulti(muzzleWorld, dir, shots, half, bt, bt.multiShotSpawnOffset);
        }
        else
        {
            _spawnBullet(muzzleWorld, dir, bt);
        }

        if (logDebug)
            Debug.Log($"[ArcGuardTailAnimator] Fire bulletTypeIndex={bulletTypeIndex} from {muzzleWorld}", this);
    }

    private void FireMulti(Vector3 firePos, Vector2 fireDir, int shots, float half, EnemyData.BulletType bt, float spawnOffset)
    {
        for (int i = 0; i < shots; i++)
        {
            float ang   = (half > 0.0001f) ? Random.Range(-half, half) : 0f;
            Vector2 dir = RotateVec(fireDir, ang);
            if (dir.sqrMagnitude <= 0.0001f) dir = fireDir;

            Vector3 pos = firePos;
            if (spawnOffset > 0.0001f && shots > 1)
            {
                Vector2 perp = new Vector2(-dir.y, dir.x);
                pos += (Vector3)(perp * ((i - (shots - 1) * 0.5f) * spawnOffset));
            }
            _spawnBullet(pos, dir, bt);
        }
    }

    private IEnumerator FireMultiDelayed(Vector3 firePos, Vector2 fireDir, int shots, float half, EnemyData.BulletType bt, float spawnOffset, float launchDelay)
    {
        for (int i = 0; i < shots; i++)
        {
            float ang   = (half > 0.0001f) ? Random.Range(-half, half) : 0f;
            Vector2 dir = RotateVec(fireDir, ang);
            if (dir.sqrMagnitude <= 0.0001f) dir = fireDir;

            Vector3 pos = firePos;
            if (spawnOffset > 0.0001f && shots > 1)
            {
                Vector2 perp = new Vector2(-dir.y, dir.x);
                pos += (Vector3)(perp * ((i - (shots - 1) * 0.5f) * spawnOffset));
            }
            _spawnBullet(pos, dir, bt);

            if (i < shots - 1)
                yield return WaitScaled(launchDelay);
        }
    }

    private static Vector2 RotateVec(Vector2 v, float angleDeg)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }

    private float GetTimeScale() => SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;

    private IEnumerator WaitScaled(float seconds)
    {
        float t = 0f;
        while (t < seconds)
        {
            if (_isDead) yield break;
            t += Time.deltaTime * GetTimeScale();
            yield return null;
        }
    }

    private void OnDestroy()
    {
        _isDead = true;
    }

    // ======================================================
    // Frame apply
    // ======================================================

    private void ApplyFrame(ArcGuardTailAnimFrame frame)
    {
        if (frame == null || spriteRenderer == null) return;
        if (frame.sprite != null) spriteRenderer.sprite = frame.sprite;
        _currentOffset = frame.offset;
        _currentRotationZ = frame.rotationZ;
        ApplyOffset(frame.offset);
        ApplyRotation(frame.rotationZ);
    }

    private void ApplyFrame(ArcGuardTailIdleSwayFrame frame)
    {
        if (frame == null || spriteRenderer == null) return;
        if (frame.sprite != null) spriteRenderer.sprite = frame.sprite;
        _currentOffset = frame.offset;
        _currentRotationZ = frame.rotationZ;
        ApplyOffset(frame.offset);
        ApplyRotation(frame.rotationZ);
    }

    private void ApplyOffset(Vector2 offset)
    {
        if (spriteRenderer == null) return;
        var t = spriteRenderer.transform;
        float x = spriteRenderer.flipX ? -offset.x : offset.x;
        Vector3 local = t.localPosition;
        t.localPosition = new Vector3(x, offset.y, local.z);
    }

    private void ApplyRotation(float rotationZ)
    {
        if (spriteRenderer == null) return;
        float z = spriteRenderer.flipX ? -rotationZ : rotationZ;
        spriteRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, z);
    }

    // ======================================================
    // Editor Preview
    // ======================================================

    private void OnEnable()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.update += OnEditorTickRefresh;
#endif
    }

    private void OnDisable()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.update -= OnEditorTickRefresh;
        StopEditorAnim();
#endif
    }

#if UNITY_EDITOR
    private bool   _editorAnimRunning;
    private double _editorAnimLastTime;
    private int    _editorAnimFrameIdx;
    private double _editorAnimCurrentDuration = 0.15;

    private void OnEditorTickRefresh()
    {
        if (this == null || Application.isPlaying) return;
        if (_editorAnimRunning) return;
        if (spriteRenderer == null) return;

        // Frozen/IdleSwayAnimate/SweepAnimateは_editorAnimRunning中はStartEditorAnim/OnEditorUpdateが処理するため、
        // ここに来るのはTail1〜16の静止プレビューのみ。
        int tailIdx = TailPreviewIndex(previewState);
        if (tailIdx >= 0)
        {
            if (idleSwayFrames != null && tailIdx < idleSwayFrames.Length)
                ApplyFrame(idleSwayFrames[tailIdx]);
            UnityEditor.SceneView.RepaintAll();
        }
    }

    // Tail1〜Tail16はidleSwayFramesの何番目かを返す（対象外ならー1）
    private static int TailPreviewIndex(ArcGuardTailPreviewState state)
    {
        switch (state)
        {
            case ArcGuardTailPreviewState.Tail1:  return 0;
            case ArcGuardTailPreviewState.Tail2:  return 1;
            case ArcGuardTailPreviewState.Tail3:  return 2;
            case ArcGuardTailPreviewState.Tail4:  return 3;
            case ArcGuardTailPreviewState.Tail5:  return 4;
            case ArcGuardTailPreviewState.Tail6:  return 5;
            case ArcGuardTailPreviewState.Tail7:  return 6;
            case ArcGuardTailPreviewState.Tail8:  return 7;
            case ArcGuardTailPreviewState.Tail9:  return 8;
            case ArcGuardTailPreviewState.Tail10: return 9;
            case ArcGuardTailPreviewState.Tail11: return 10;
            case ArcGuardTailPreviewState.Tail12: return 11;
            case ArcGuardTailPreviewState.Tail13: return 12;
            case ArcGuardTailPreviewState.Tail14: return 13;
            case ArcGuardTailPreviewState.Tail15: return 14;
            case ArcGuardTailPreviewState.Tail16: return 15;
            default: return -1;
        }
    }

    private void StartEditorAnim()
    {
        _editorAnimFrameIdx = 0;
        _editorAnimLastTime = UnityEditor.EditorApplication.timeSinceStartup;
        if (!_editorAnimRunning)
        {
            _editorAnimRunning = true;
            UnityEditor.EditorApplication.update += OnEditorUpdate;
        }

        if (previewState == ArcGuardTailPreviewState.SweepAnimate)
        {
            if (sweepFrames != null && sweepFrames.Length > 0)
            {
                var f0 = sweepFrames[0];
                _editorAnimCurrentDuration = (f0 != null && f0.duration > 0f) ? f0.duration : 0.15;
                if (f0 != null && spriteRenderer != null) ApplyFrame(f0);
            }
        }
        else
        {
            if (idleSwayFrames != null && idleSwayFrames.Length > 0)
            {
                var f0 = idleSwayFrames[0];
                _editorAnimCurrentDuration = f0 != null ? f0.RollDuration() : 0.15;
                if (f0 != null && spriteRenderer != null) ApplyFrame(f0);
            }
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
        if (this == null || !_editorAnimRunning) { StopEditorAnim(); return; }

        double now = UnityEditor.EditorApplication.timeSinceStartup;
        if (now - _editorAnimLastTime < _editorAnimCurrentDuration) return;
        _editorAnimLastTime = now;

        if (previewState == ArcGuardTailPreviewState.SweepAnimate)
        {
            if (sweepFrames == null || sweepFrames.Length == 0) { StopEditorAnim(); return; }
            _editorAnimFrameIdx = (_editorAnimFrameIdx + 1) % sweepFrames.Length;
            var next = sweepFrames[_editorAnimFrameIdx];
            _editorAnimCurrentDuration = (next != null && next.duration > 0f) ? next.duration : 0.15;
            if (next != null && next.sprite != null && spriteRenderer != null) ApplyFrame(next);
        }
        else
        {
            if (idleSwayFrames == null || idleSwayFrames.Length == 0) { StopEditorAnim(); return; }
            _editorAnimFrameIdx = (_editorAnimFrameIdx + 1) % idleSwayFrames.Length;
            var next = idleSwayFrames[_editorAnimFrameIdx];
            _editorAnimCurrentDuration = next != null ? next.RollDuration() : 0.15;
            if (next != null && next.sprite != null && spriteRenderer != null) ApplyFrame(next);
        }

        UnityEditor.SceneView.RepaintAll();
    }
#endif

    private void OnValidate()
    {
        if (Application.isPlaying) return;
#if UNITY_EDITOR
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) return;

        // FrozenはidleSwayFramesをそのままループ再生する（発射が無いだけで見た目はIdleSwayAnimateと同じ）
        bool isAnim = previewState == ArcGuardTailPreviewState.IdleSwayAnimate ||
                      previewState == ArcGuardTailPreviewState.Frozen ||
                      previewState == ArcGuardTailPreviewState.SweepAnimate;

        if (isAnim)
        {
            StartEditorAnim();
        }
        else
        {
            // Tail1〜16の静止表示は、OnEditorTickRefresh（次ティック）に反映を任せる。
            // ここで直接spriteRenderer.spriteを書き換えると「SendMessage cannot be called during ... OnValidate」警告が出るため。
            StopEditorAnim();
        }

        UnityEditor.SceneView.RepaintAll();
#endif
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (Application.isPlaying) return;

        Vector2 offset, muzzleOffset;

        int tailIdx = TailPreviewIndex(previewState);
        if (tailIdx >= 0 && idleSwayFrames != null && tailIdx < idleSwayFrames.Length && idleSwayFrames[tailIdx] != null)
        {
            offset = idleSwayFrames[tailIdx].offset;
            muzzleOffset = idleSwayFrames[tailIdx].muzzleOffset;
        }
        else if (previewState == ArcGuardTailPreviewState.SweepAnimate)
        {
            if (sweepFrames == null || sweepFrames.Length == 0) return;
            var f = sweepFrames[_editorAnimFrameIdx % sweepFrames.Length];
            if (f == null) return;
            offset = f.offset;
            muzzleOffset = f.muzzleOffset;
        }
        else // IdleSwayAnimate / Frozen（どちらもidleSwayFramesを参照）
        {
            if (idleSwayFrames == null || idleSwayFrames.Length == 0) return;
            var f = idleSwayFrames[_editorAnimFrameIdx % idleSwayFrames.Length];
            if (f == null) return;
            offset = f.offset;
            muzzleOffset = f.muzzleOffset;
        }

        Vector3 muzzleWorld = transform.position + new Vector3(offset.x + muzzleOffset.x, offset.y + muzzleOffset.y, 0f);
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.9f);
        Gizmos.DrawSphere(muzzleWorld, 0.06f);
    }
#endif
}
