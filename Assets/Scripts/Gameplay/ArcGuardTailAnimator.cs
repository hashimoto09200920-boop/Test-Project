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
        // 以下、sweepFramesの該当インデックスだけを静止表示
        Sweep1, Sweep2, Sweep3, Sweep4, Sweep5, Sweep6, Sweep7, Sweep8, Sweep9, Sweep10,
    }

    // 尾は細長く曲がっているため、Box1個では追従しきれない。1コマにつき最大3個の
    // 独立した（個別に回転できる）Box当たり判定セグメントを割り当てられるようにする。
    [System.Serializable]
    public class ArcGuardTailColliderSegment
    {
        public Vector2 offset;
        public Vector2 size;
        [Tooltip("このセグメント単体のZ軸回転（度）")]
        public float   rotationZ;
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
        [Tooltip("当たり判定セグメント（最大3個。要素数が3未満なら残りは無効化される）")]
        [NonReorderable]
        public ArcGuardTailColliderSegment[] colliderSegments;
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
        [Tooltip("当たり判定セグメント（最大3個。要素数が3未満なら残りは無効化される）")]
        [NonReorderable]
        public ArcGuardTailColliderSegment[] colliderSegments;

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
    [Tooltip("本体側のSpriteRenderer。未設定なら親の直下から\"Body\"という名前の子を自動検索する（尾を常に本体より背面に表示するため）")]
    [SerializeField] private SpriteRenderer bodySpriteRenderer;
    [Tooltip("当たり判定セグメント用の子オブジェクト（最大3個、それぞれにBoxCollider2Dをアタッチ）。各フレームのColliderSegmentsに毎回位置/サイズ/回転が同期される。TrailにKinematic Rigidbody2Dが必要（子Colliderのヒットを親のArcGuardTailHealthへ届けるため）")]
    [SerializeField] private BoxCollider2D[] tailColliderSegments = new BoxCollider2D[3];

    [Header("Editor Preview")]
    [SerializeField] private ArcGuardTailPreviewState previewState = ArcGuardTailPreviewState.IdleSwayAnimate;
    [Tooltip("ONの間はCollider Segmentsの自動同期を止める。Scene viewでMove/Rotateツールを使って直接ドラッグ配置したい時だけ一時的にチェックし、配置後はCapture Collider Segments From Sceneで保存してからOFFに戻す")]
    [SerializeField] private bool suspendColliderSync = false;

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
        SyncSortingOrder();
        ApplyColliderSegments(frame.colliderSegments);
    }

    private void ApplyFrame(ArcGuardTailIdleSwayFrame frame)
    {
        if (frame == null || spriteRenderer == null) return;
        if (frame.sprite != null) spriteRenderer.sprite = frame.sprite;
        _currentOffset = frame.offset;
        _currentRotationZ = frame.rotationZ;
        ApplyOffset(frame.offset);
        ApplyRotation(frame.rotationZ);
        SyncSortingOrder();
        ApplyColliderSegments(frame.colliderSegments);
    }

    // 最大3個の当たり判定セグメントをフレームごとの位置/サイズ/回転に同期する。
    // 対応するセグメントデータが無い（配列が短い/サイズ0）ものは無効化する。
    // セグメントは子オブジェクトのColliderなので、localPosition/localRotationはTrail基準でそのまま設定すればよい
    // （Trail自体の位置はApplyOffset/ApplyRotationで既に動いているため、親子関係で自動的に追従する）。
    private void ApplyColliderSegments(ArcGuardTailColliderSegment[] segments)
    {
        // Scene viewで手動ドラッグ中は自動同期を止める（毎ティック上書きされるとMove/Rotateツールが効かなくなるため）
        if (suspendColliderSync) return;
        if (tailColliderSegments == null) return;

        for (int i = 0; i < tailColliderSegments.Length; i++)
        {
            BoxCollider2D col = tailColliderSegments[i];
            if (col == null) continue;

            ArcGuardTailColliderSegment seg = (segments != null && i < segments.Length) ? segments[i] : null;
            if (seg == null || seg.size.sqrMagnitude <= 0.0001f)
            {
                col.enabled = false;
                continue;
            }

            col.enabled = true;
            float x = (spriteRenderer != null && spriteRenderer.flipX) ? -seg.offset.x : seg.offset.x;
            float rot = (spriteRenderer != null && spriteRenderer.flipX) ? -seg.rotationZ : seg.rotationZ;
            Transform t = col.transform;
            Vector3 local = t.localPosition;
            t.localPosition = new Vector3(x, seg.offset.y, local.z);
            t.localRotation = Quaternion.Euler(0f, 0f, rot);
            col.size = seg.size;
        }
    }

    // 尾は常に本体より1つ奥のSorting Orderに揃える（同値だと前後関係が不安定になるため）
    private void SyncSortingOrder()
    {
        if (spriteRenderer == null) return;
        if (bodySpriteRenderer == null && transform.parent != null)
        {
            Transform body = transform.parent.Find("Body");
            if (body != null) bodySpriteRenderer = body.GetComponent<SpriteRenderer>();
        }
        if (bodySpriteRenderer == null) return;

        spriteRenderer.sortingLayerID = bodySpriteRenderer.sortingLayerID;
        spriteRenderer.sortingOrder   = bodySpriteRenderer.sortingOrder - 1;
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
        // ここに来るのはTail1〜16/Sweep1〜7の静止プレビューのみ。
        int tailIdx = TailPreviewIndex(previewState);
        if (tailIdx >= 0)
        {
            if (idleSwayFrames != null && tailIdx < idleSwayFrames.Length)
                ApplyFrame(idleSwayFrames[tailIdx]);
            UnityEditor.SceneView.RepaintAll();
            return;
        }

        int sweepIdx = SweepPreviewIndex(previewState);
        if (sweepIdx >= 0)
        {
            if (sweepFrames != null && sweepIdx < sweepFrames.Length)
                ApplyFrame(sweepFrames[sweepIdx]);
            UnityEditor.SceneView.RepaintAll();
        }
    }

    // Scene view上で手で掴んで配置したColliderSegmentの実際のTransform/Sizeを、
    // 今Preview Stateで選択中のコマ（Tail1〜16/Sweep1〜10）のColliderSegmentsへ書き戻す。
    // 通常時はOnEditorTickRefreshが毎ティックデータ→実物へ同期し続けるため、
    // 逆方向（実物→データ）のこの操作をしないと手動ドラッグがすぐ元に戻ってしまう。
    [ContextMenu("Capture Collider Segments From Scene")]
    private void CaptureColliderSegmentsFromScene()
    {
        if (tailColliderSegments == null || tailColliderSegments.Length == 0)
        {
            Debug.LogWarning("[ArcGuardTailAnimator] Tail Collider Segmentsが未設定です。", this);
            return;
        }

        ArcGuardTailColliderSegment[] target = GetCurrentPreviewColliderSegments(out string frameLabel);
        if (target == null)
        {
            Debug.LogWarning("[ArcGuardTailAnimator] Preview StateがTail1〜16またはSweep1〜10の時だけキャプチャできます。", this);
            return;
        }

        for (int i = 0; i < tailColliderSegments.Length && i < target.Length; i++)
        {
            BoxCollider2D col = tailColliderSegments[i];
            if (col == null || target[i] == null) continue;

            Vector3 local = col.transform.localPosition;
            float rotZ = col.transform.localEulerAngles.z;
            if (rotZ > 180f) rotZ -= 360f;

            bool flip = spriteRenderer != null && spriteRenderer.flipX;
            float x = flip ? -local.x : local.x;
            float rot = flip ? -rotZ : rotZ;

            target[i].offset = new Vector2(x, local.y);
            target[i].rotationZ = rot;
            target[i].size = col.size;
        }

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log($"[ArcGuardTailAnimator] {frameLabel} にCollider Segmentsをキャプチャしました。", this);
    }

    // tailColliderSegmentsのうち、実際にColliderが割り当てられている（Noneではない）要素数を返す。
    // 配列のSizeだけ3にしていても中身がNoneのままなら数えない。
    private int AssignedColliderSegmentCount()
    {
        if (tailColliderSegments == null) return 0;
        int count = 0;
        foreach (var c in tailColliderSegments)
            if (c != null) count++;
        return count;
    }

    // 今のPreview Stateが指すidleSwayFrames/sweepFramesのcolliderSegments配列を返す
    // （実際に割り当て済みのtailColliderSegmentsの個数と過不足があれば自動でちょうどの数に揃える）。対象外ならnull。
    private ArcGuardTailColliderSegment[] GetCurrentPreviewColliderSegments(out string frameLabel)
    {
        int need = AssignedColliderSegmentCount();

        int tailIdx = TailPreviewIndex(previewState);
        if (tailIdx >= 0 && idleSwayFrames != null && tailIdx < idleSwayFrames.Length && idleSwayFrames[tailIdx] != null)
        {
            var f = idleSwayFrames[tailIdx];
            if (f.colliderSegments == null || f.colliderSegments.Length != need)
                f.colliderSegments = ResizeSegments(f.colliderSegments, need);
            frameLabel = $"Idle Sway Frames[{tailIdx}]";
            return f.colliderSegments;
        }

        int sweepIdx = SweepPreviewIndex(previewState);
        if (sweepIdx >= 0 && sweepFrames != null && sweepIdx < sweepFrames.Length && sweepFrames[sweepIdx] != null)
        {
            var f = sweepFrames[sweepIdx];
            if (f.colliderSegments == null || f.colliderSegments.Length != need)
                f.colliderSegments = ResizeSegments(f.colliderSegments, need);
            frameLabel = $"Sweep Frames[{sweepIdx}]";
            return f.colliderSegments;
        }

        frameLabel = null;
        return null;
    }

    private static ArcGuardTailColliderSegment[] ResizeSegments(ArcGuardTailColliderSegment[] src, int newSize)
    {
        var result = new ArcGuardTailColliderSegment[newSize];
        for (int i = 0; i < newSize; i++)
            result[i] = (src != null && i < src.Length && src[i] != null) ? src[i] : new ArcGuardTailColliderSegment();
        return result;
    }

    // Sweep1〜Sweep7はsweepFramesの何番目かを返す（対象外ならー1）
    private static int SweepPreviewIndex(ArcGuardTailPreviewState state)
    {
        switch (state)
        {
            case ArcGuardTailPreviewState.Sweep1: return 0;
            case ArcGuardTailPreviewState.Sweep2: return 1;
            case ArcGuardTailPreviewState.Sweep3: return 2;
            case ArcGuardTailPreviewState.Sweep4: return 3;
            case ArcGuardTailPreviewState.Sweep5: return 4;
            case ArcGuardTailPreviewState.Sweep6: return 5;
            case ArcGuardTailPreviewState.Sweep7: return 6;
            case ArcGuardTailPreviewState.Sweep8: return 7;
            case ArcGuardTailPreviewState.Sweep9: return 8;
            case ArcGuardTailPreviewState.Sweep10: return 9;
            default: return -1;
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

        // Muzzle位置は実体を持たない値なので確認用の球を描く。実際の発射処理（FireFromMuzzle）と同じ式（flipXのみ考慮、
        // transform.positionにそのまま加算）に合わせる。transform.positionは直前のApplyFrameで既にそのフレームの
        // offset込みの位置になっているため、ここでoffsetを重ねて足すと二重加算になる点に注意。
        //
        // Collider Segmentsは実物の子Colliderとして存在するため、Trailの子オブジェクト自体を選択していればUnity標準の
        // ギズモがそのまま出るが、Trail選択中は出ないため、ここでも同じ内容を描画する。こちらは実物の子Transform
        // （localPosition/localRotation）と同じ計算になるよう、TransformPoint/回転合成を使って親の回転も考慮する。
        Vector2 muzzleOffset;
        ArcGuardTailColliderSegment[] segments;

        int tailIdx = TailPreviewIndex(previewState);
        int sweepIdx = SweepPreviewIndex(previewState);
        if (tailIdx >= 0 && idleSwayFrames != null && tailIdx < idleSwayFrames.Length && idleSwayFrames[tailIdx] != null)
        {
            muzzleOffset = idleSwayFrames[tailIdx].muzzleOffset;
            segments = idleSwayFrames[tailIdx].colliderSegments;
        }
        else if (sweepIdx >= 0 && sweepFrames != null && sweepIdx < sweepFrames.Length && sweepFrames[sweepIdx] != null)
        {
            muzzleOffset = sweepFrames[sweepIdx].muzzleOffset;
            segments = sweepFrames[sweepIdx].colliderSegments;
        }
        else if (previewState == ArcGuardTailPreviewState.SweepAnimate)
        {
            if (sweepFrames == null || sweepFrames.Length == 0) return;
            var f = sweepFrames[_editorAnimFrameIdx % sweepFrames.Length];
            if (f == null) return;
            muzzleOffset = f.muzzleOffset;
            segments = f.colliderSegments;
        }
        else // IdleSwayAnimate / Frozen（どちらもidleSwayFramesを参照）
        {
            if (idleSwayFrames == null || idleSwayFrames.Length == 0) return;
            var f = idleSwayFrames[_editorAnimFrameIdx % idleSwayFrames.Length];
            if (f == null) return;
            muzzleOffset = f.muzzleOffset;
            segments = f.colliderSegments;
        }

        float mx = (spriteRenderer != null && spriteRenderer.flipX) ? -muzzleOffset.x : muzzleOffset.x;
        Vector3 muzzleWorld = transform.position + new Vector3(mx, muzzleOffset.y, 0f);
        Gizmos.color = new Color(1f, 0.9f, 0.2f, 0.9f);
        Gizmos.DrawSphere(muzzleWorld, 0.06f);

        if (segments == null || tailColliderSegments == null) return;

        // 実物のColliderが無いインデックス分は、データが残っていても描かない（ApplyColliderSegmentsと同じ範囲に揃える）
        int segCount = Mathf.Min(segments.Length, tailColliderSegments.Length);

        Gizmos.color = new Color(0.2f, 1f, 0.4f, 0.9f);
        Matrix4x4 oldMatrix = Gizmos.matrix;
        for (int i = 0; i < segCount; i++)
        {
            var seg = segments[i];
            if (seg == null || seg.size.sqrMagnitude <= 0.0001f) continue;
            bool flip = spriteRenderer != null && spriteRenderer.flipX;
            float sx = flip ? -seg.offset.x : seg.offset.x;
            float srot = flip ? -seg.rotationZ : seg.rotationZ;
            Vector3 worldPos = transform.TransformPoint(new Vector3(sx, seg.offset.y, 0f));
            Quaternion worldRot = transform.rotation * Quaternion.Euler(0f, 0f, srot);
            Gizmos.matrix = Matrix4x4.TRS(worldPos, worldRot, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(seg.size.x, seg.size.y, 0.05f));
        }
        Gizmos.matrix = oldMatrix;
    }
#endif
}
