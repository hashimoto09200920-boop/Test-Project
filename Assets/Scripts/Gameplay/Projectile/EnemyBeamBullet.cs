using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Skills;

/// <summary>
/// レーザービーム弾。既存のEnemyBullet（Rigidbody2D駆動）とは完全に別のクラス。
/// 発射すると先端が一定の速さで伸びていき、先端が届いた瞬間に反射・貫通・ダメージを起こす（見た目と当たるタイミングが一致。2026/10/17〜）。
/// さらに、存在している間は一定間隔で全セグメントを再チェックし、パドルラインが新しく現れて
/// セグメントの経路と交差したら、その場で反射させて新しいセグメントを生やす（都度判定）。
/// パドル反射の判定・Just判定・線の破断は PaddleDot.EvaluateExternalHit() を通じて既存仕様と同じロジックを使う。
/// </summary>
public class EnemyBeamBullet : MonoBehaviour
{
    [Header("Hit Detection")]
    [Tooltip("Raycast対象レイヤー。Paddleライン・ブロック(WallHealth)・Player・Floor・Enemyは全てDefaultレイヤーのため、通常はDefault＋Wallの2つを選択すればよい")]
    [SerializeField] private LayerMask hitLayers;

    [Tooltip("画面境界の壁（HPを持たず跳ね返るだけの壁。既存弾のwallLayersToCountと同じWallレイヤー）。ここに当たったらダメージ無しでそのまま終端する")]
    [SerializeField] private LayerMask boundaryWallLayers;

    [Tooltip("何にも当たらなかった場合にビームを伸ばす最大距離")]
    [SerializeField] private float maxSegmentDistance = 30f;

    [Tooltip("反射の連鎖が異常に増えないための安全上限（Paddle Bounce Limitが無制限[-1]の場合の保険）")]
    [SerializeField] private int maxSegmentsSafety = 32;

    [Tooltip("ビームの先端が Bullet Type の Beam Grow Duration 秒で進む距離（ワールド単位。初期値10＝画面の縦の長さ）。\n" +
             "先端は一定の速さで伸び、先端が届いた瞬間に反射・ダメージが起きる")]
    [SerializeField] private float growReferenceDistance = 10f;

    [Header("Reflect（プレイヤーの線での反射）")]
    [Tooltip("ON：線そのものの向き（当たった付近の前後の点を結んだ向き）を鏡にして反射する。\n" +
             "OFF：当たった円（PaddleDot）の表面の向きで反射する（線は円を並べて作るため、当たる位置で角度がぶれる）")]
    [SerializeField] private bool reflectByLineDirection = true;
    [Tooltip("線の向きを求める時に使う、当たった点の前後の点の数（大きいほど線の細かいガタつきをならす）")]
    [Min(1)] [SerializeField] private int lineDirectionSampleRange = 2;

    [Header("Segment Visual")]
    [Tooltip("ビームの線を描画するMaterial（LineRendererに使用。未設定だとUnityのデフォルトマテリアルになる）")]
    [SerializeField] private Material beamMaterial;
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 10;
    [Tooltip("火花パーティクルの、ビーム長1単位あたりの発生量")]
    [SerializeField] private float sparkDensityPerUnit = 20f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLog = false;

    private class BeamSegment
    {
        public Vector3 start;
        public Vector3 end;
        public bool isReflected;
        public LineRenderer line;
        public ParticleSystem spark;
        public GameObject visualRoot; // Destroy用（line/sparkの親）
        public BeamSegment next; // このセグメントの続き（反射で生まれた次のセグメント）。無ければnull
        public WallHealth terminalWall; // このセグメントがBlockに当たって終端した場合、そのBlock（移動追従に使う）。無ければnull
        public bool hadTerminalWall; // terminalWallに一度でも値が入ったか（Block破壊による自然消滅と、元々追従対象が無いケースを区別するため）
        public EnemyPart terminalEnemyPart; // このセグメントがEnemy(WeakPoint)に当たって終端した場合のEnemyPart（移動追従に使う）。無ければnull
        public EnemyDamageReceiver terminalEnemyReceiver; // このセグメントがEnemy(本体)に当たって終端した場合のEnemyDamageReceiver。無ければnull
        public bool hadTerminalEnemy; // terminalEnemyPart/Receiverに一度でも値が入ったか（Enemy撃破による自然消滅の判定用）
        public PaddleDot reflectionSourceDot; // このセグメントが反射するきっかけとなった線（次のセグメントを生んだ線）。無ければnull
        public bool hadReflectionSourceDot; // reflectionSourceDotに一度でも値が入ったか（線が消えて反射前の状態に戻す判定用）
        public int reflectTickCount; // 線に当たり続けている間のヒット回数（反射演出を回数ごとに大きくするのに使う。ReflectedBulletFXManager）
        public PixelDancerController terminalPlayer; // このセグメントがPlayerに当たって終端した場合のPixelDancerController（移動追従に使う）。無ければnull
        public bool hadTerminalPlayer; // terminalPlayerに一度でも値が入ったか
        public bool isOpenEnd; // 何にも当たらず最大距離まで伸びきったセグメントか（後からEnemyが移動してきた時に打ち切るための判定用）
        public bool growing; // 先端が伸びている途中のセグメントか（end＝今の先端。届くまでは当たり・継続ダメージ・追従の対象にしない）
        public Vector2 segDir; // このセグメントが進んでいた方向（Block/Enemyが消えた時・反射前の線が消えた時、そこから先へ延長するのに使う）
        public GradientColorKey[] baseColorKeys; // フェードアウト計算のベースになる、元のグラデーション色
        public GradientAlphaKey[] baseAlphaKeys; // フェードアウト計算のベースになる、元のグラデーション不透明度
        public PaddleDot originDot; // このセグメントが直前に反射して生まれた場合、その反射元の線（CheckForNewReflectionsでの誤検出防止用）
        // ★PaddleDotプーリング対応：線が消えてもDotはDestroyされずプールへ戻る（参照がnullにならない）ため、
        //   参照を記録した時点のPoolGenerationを覚えておき、ClearStaleDotRefs()で「消えた線」の参照をnullに戻す
        public int reflectionSourceDotGeneration = -1;
        public int originDotGeneration = -1;
        public EnemyLineReflector reflectionSourceEnemyLine; // このセグメントが敵の線（NeonDancer後半）で反射して終端した場合、その線。線が消えたら反射前の方向(segDir)に戻す
        public bool hadReflectionSourceEnemyLine; // reflectionSourceEnemyLineに一度でも値が入ったか
        public BeamReflector reflectedOffReflector; // このセグメントがBeamReflectorに反射して終端した場合、その相手（当たり続けている間VFXを繰り返すのに使う）。無ければnull
        public float fadeAlphaMul = 1f; // LifeRoutineのフェードアウトで更新される不透明度倍率。beamPulseEnabled時はLateUpdateがこれを読んでcolorGradientに反映する
        public GradientAlphaKey[] pulseAlphaKeysScratch; // 明滅計算用の使い回し配列（毎フレームnewしない。初回LateUpdateで確保）
        public Gradient pulseGradient; // 明滅計算用の使い回しGradient（同上）
        public GradientAlphaKey[] fadeAlphaKeysScratch; // フェードアウト計算用の使い回し配列（毎フレームnewしない）
        public Gradient fadeGradient; // フェードアウト計算用の使い回しGradient（同上）
    }

    private readonly List<BeamSegment> segments = new List<BeamSegment>();

    // ★負荷軽減用の使い回し（毎フレーム・判定ごとのnewをやめる。中身の扱いは従来と同じ）
    private static readonly HashSet<Collider2D> s_emptyIgnored = new HashSet<Collider2D>(); // 読むだけの空集合（中身を追加しないこと）
    private readonly HashSet<Collider2D> _buildChainIgnoredScratch = new HashSet<Collider2D>();
    private readonly HashSet<Object> _hitThisTickScratch = new HashSet<Object>();
    private readonly Stack<List<BeamSegment>> _snapshotPool = new Stack<List<BeamSegment>>();

    /// <summary>
    /// segmentsの今の中身を写したListを返す（従来の new List&lt;BeamSegment&gt;(segments) と同じ内容）。
    /// 使い回しのListを借りるので、関数の最後でReturnSegmentSnapshotで返す（返し忘れても次回newされるだけで動作は同じ）
    /// </summary>
    private List<BeamSegment> RentSegmentSnapshot()
    {
        List<BeamSegment> list = _snapshotPool.Count > 0 ? _snapshotPool.Pop() : new List<BeamSegment>(segments.Count);
        list.Clear();
        list.AddRange(segments);
        return list;
    }

    private void ReturnSegmentSnapshot(List<BeamSegment> list)
    {
        if (list == null) return;
        list.Clear();
        _snapshotPool.Push(list);
    }
    private EnemyData.BulletType bulletType;
    private float damageMultiplier = 1f; // Just反射で更新され、以降の全セグメントに引き継がれる（既存EnemyBulletのDamageMultiplierと同じ「一度Justになったら持続」仕様）
    private Collider2D[] ownerColliders;
    private int penetration;
    private int reflectCount; // ビーム1本の生涯を通じた反射回数（発射時＋都度判定の反射を全部合算してPaddle Bounce Limitと比較する）

    // A8スキル「反射弾が敵に当たるたびダメージが増加」をBeamにも適用するための状態
    // （EnemyBullet.RegisterEnemyHitAsBounceと同じ考え方。ビーム1本の生涯を通じて持続する）
    private float currentDamage; // bulletType.damageを起点に、A8ヒットのたびに更新される実際の基礎ダメージ
    private int a8MaxAdditions = 0;
    private float a8DamagePerHit = 1f;
    private int a8EnemyHitCount = 0;
    private int lastA8HitFrame = -999;

    private static float GetTimeScale() => SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;

    // 発射後に延ばした寿命（秒）。ExtendLife で足す（呼ばれなければ0＝Bullet TypeのLife Timeどおり）
    private float extraLifeSeconds;

    /// <summary>このビームの寿命を seconds 秒だけ延ばす（Obeliskの往復掃射など、Bullet TypeのLife Timeより長く照射したい時に発射元が呼ぶ）</summary>
    public void ExtendLife(float seconds)
    {
        if (seconds > 0f) extraLifeSeconds += seconds;
    }

    // ---------- 見た目用の読み取り窓口（BeamStyleFXが使う。ビームの判定・状態は変更しない） ----------
    /// <summary>今あるセグメント（反射で折れ曲がる区間）の数</summary>
    public int VisualSegmentCount => segments.Count;

    /// <summary>ジャスト反射されたビームか（反射後の見た目を弾のジャスト反射と同じにするのに使う。ReflectedBulletFXManager）</summary>
    public bool IsJustReflected => damageMultiplier > 1.0001f;

    /// <summary>
    /// i番目のセグメントの見た目の情報。line＝本体の線（位置はline.GetPosition(0/1)）、isReflected＝プレイヤーが反射させた後の区間か、
    /// isOpenEnd＝何にも当たらず伸びきった区間か、hasNext＝続き（反射で生まれた次の区間）があるか、fade＝照射終了時のフェード（1→0）
    /// </summary>
    public bool GetVisualSegment(int i, out LineRenderer line, out bool isReflected, out bool isOpenEnd, out bool hasNext, out float fade)
    {
        line = null; isReflected = false; isOpenEnd = false; hasNext = false; fade = 1f;
        if (i < 0 || i >= segments.Count || segments[i] == null) return false;
        BeamSegment s = segments[i];
        line = s.line;
        isReflected = s.isReflected;
        isOpenEnd = s.isOpenEnd || s.growing;
        hasNext = s.next != null;
        fade = s.fadeAlphaMul;
        return line != null;
    }

    /// <summary>i番目のセグメントの続き（反射で生まれた次の区間）の番号。無ければ-1。0番が発射口から始まる区間</summary>
    public int GetNextVisualSegmentIndex(int i)
    {
        if (i < 0 || i >= segments.Count || segments[i] == null || segments[i].next == null) return -1;
        return segments.IndexOf(segments[i].next);
    }

    /// <summary>
    /// ビームを発射する。EnemyShooter.SpawnBeamBullet（または各コントローラーの独自実装）から呼ばれる想定。
    /// direction・originはBullet Typeの Aim Mode を反映済みの最終方向を渡すこと（本クラス自体はAim Modeを解決しない）。
    /// </summary>
    public void Fire(Vector3 origin, Vector2 direction, EnemyData.BulletType bt, Collider2D[] owner)
    {
        bulletType = bt;
        ownerColliders = owner;
        damageMultiplier = 1f;
        reflectCount = 0;
        transform.position = origin;

        currentDamage = (bt != null) ? bt.damage : 0;
        if (SkillManager.Instance != null)
        {
            a8MaxAdditions = SkillManager.Instance.GetA8MaxAdditions();
            a8DamagePerHit = SkillManager.Instance.GetA8DamagePerHit();
        }
        else
        {
            a8MaxAdditions = 0;
            a8DamagePerHit = 1f;
        }
        a8EnemyHitCount = 0;
        lastA8HitFrame = -999;

        if (direction.sqrMagnitude < 0.0001f)
        {
            Debug.LogWarning($"[EnemyBeamBullet] Fire()に渡された発射方向がほぼゼロベクトルです（direction={direction}）。呼び出し元のAim Mode解決を確認してください。");
        }

        int areaNumber = GameSession.HasValidArea() ? GameSession.GetEffectiveAreaNumber() : 0;
        penetration = (bt != null) ? bt.GetPenetration(areaNumber) : -1;

        // ★先端方式：発射口から長さ0のセグメントが伸び始める（当たりは先端が届いた時。Update→AdvanceFront）
        StartFront(origin, direction.normalized, false, null, null);

        // ★再入対策：Beam Grow Durationが0以下（その場で伸びきる）の時、最初の判定で反射して発射元自身のWallHealthに
        // 命中し、それが発射元の同期的な自己破壊（Destroy）を引き起こしていることがある。この場合、以降のコルーチン開始を
        // 破棄予約済みの自分自身の上で行わないよう、ここで打ち切る
        if (this == null) return;

        if (segments.Count == 0)
        {
            Destroy(gameObject);
            return;
        }

        // ★ビームの見た目のスタイル（BeamStyle）：見た目だけを上乗せする部品を付ける（当たり判定・ダメージには影響しない）
        if (bt != null && bt.beamStyle != null)
        {
            var styleFx = gameObject.AddComponent<BeamStyleFX>();
            styleFx.Init(this, bt.beamStyle, bt);
        }

        if (showDebugLog)
        {
            Debug.Log($"[EnemyBeamBullet] Fired: origin={origin}, direction={direction}, segments={segments.Count}, damageMultiplier={damageMultiplier}");
            for (int i = 0; i < segments.Count; i++)
            {
                var s = segments[i];
                Debug.Log($"[EnemyBeamBullet]   segment[{i}]: start={s.start}, end={s.end}, length={Vector3.Distance(s.start, s.end):F2}, isReflected={s.isReflected}");
            }
        }

        float life = (bt != null && bt.lifeTime > 0f) ? bt.lifeTime : 5f;
        StartCoroutine(LifeRoutine(life));

        float tickRate = (bt != null && bt.beamDamageTickRate > 0f) ? bt.beamDamageTickRate : 4f;
        StartCoroutine(DamageTickRoutine(1f / tickRate));
        StartCoroutine(LiveReflectionCheckRoutine(1f / tickRate));
    }

    /// <summary>
    /// 発射済みのビームの方向を差し替える（掃射ブレスなど、旋回させながら撃つ攻撃用）。
    /// Lifetime・damageMultiplierなど、このビーム全体の状態は維持したまま、向きだけを更新する。
    /// このメソッドはBuildChainFrom自体には一切手を加えず、独自にRaycastで下見をしてから
    /// 判断するだけなので、Fire()/CheckForNewReflections/ContinueSegmentPastGoneなど、
    /// このメソッドを呼ばない既存の全ての発射経路（Single/Doubleブレス・他の全弾種）には
    /// 一切影響しない。
    /// 直前と同じPaddleライン（同じ反射対象）に当たり続けている場合は、EvaluateExternalHitを
    /// 再度呼ばずに（VFX・SE・統計の毎フレーム重複発生と、Just判定の誤った再評価を防ぐ）、
    /// 既存セグメントの位置だけを更新し、反射方向から先だけを組み直す。
    /// 対象が変わった場合（反射していなかった／別の対象になった）は、これまで通り丸ごと組み直す。
    /// </summary>
    public void UpdateOriginDirection(Vector2 newDirection)
    {
        if (segments.Count == 0) return;
        if (newDirection.sqrMagnitude < 0.0001f) return;

        ClearStaleDotRefs();
        BeamSegment origin = segments[0];
        Vector2 dir = newDirection.normalized;

        // ★先端方式：発射口からの最初のセグメントがまだ伸びている途中なら、向きだけ回す（伸びた長さはそのまま）。
        //   当たりは次のフレームの先端の判定（AdvanceFront）で、新しい向きの上で調べる
        if (origin.growing)
        {
            BeamFront of = FindFront(origin);
            if (of != null)
            {
                of.ctx.segStart = transform.position;
                of.ctx.dir = dir;
                origin.start = transform.position;
                origin.segDir = dir;
                origin.end = origin.start + (Vector3)(dir * of.tip);
                if (origin.line != null)
                {
                    origin.line.SetPosition(0, origin.start);
                    origin.line.SetPosition(1, origin.end);
                }
                UpdateSparkShape(origin, origin.start, origin.end);
            }
            return;
        }

        if (origin.hadReflectionSourceDot && origin.reflectionSourceDot != null)
        {
            RaycastHit2D peek = FindNextRelevantHit(transform.position, dir, s_emptyIgnored, false, maxSegmentDistance);
            PaddleDot peekDot = (peek.collider != null) ? peek.collider.GetComponent<PaddleDot>() : null;

            // 1本の線は多数の個別PaddleDotの集まりのため、掃射で角度が変わると隣接する別Dotに
            // 当たり先が移ることが多い。個々のDotではなく、同じStroke（同じ線）かどうかで比較する
            bool sameLine = peekDot != null
                && peekDot.ParentStroke != null
                && peekDot.ParentStroke == origin.reflectionSourceDot.ParentStroke;

            if (sameLine)
            {
                origin.reflectionSourceDot = peekDot; // 隣接Dotに移った分、参照を最新化しておく
                origin.reflectionSourceDotGeneration = peekDot.PoolGeneration;
                origin.start = transform.position;
                origin.end = peek.point;
                origin.segDir = dir;
                if (origin.line != null)
                {
                    origin.line.SetPosition(0, origin.start);
                    origin.line.SetPosition(1, origin.end);
                }
                UpdateSparkShape(origin, origin.start, origin.end);

                // ★先端方式：反射した先がまだ伸びている途中なら、伸びた長さを引き継いで組み直す（掃射中も先端が伸び続ける）
                bool contGrowing = MeasureChain(origin.next, out float contLength);
                if (origin.next != null)
                {
                    RemoveChainFrom(origin.next);
                    origin.next = null;
                }

                Vector2 reflectDir = Vector2.Reflect(dir, PaddleReflectNormal(peekDot, peek.normal, dir)).normalized;
                if (contGrowing)
                {
                    BeamFront cf = StartFront(peek.point, reflectDir, true, peek.collider, origin);
                    if (this == null) return;
                    if (fronts.Contains(cf)) AdvanceFront(cf, contLength);
                    return;
                }
                List<BeamSegment> continuation = BuildChainFrom(peek.point, reflectDir, true, peek.collider);
                origin.next = continuation.Count > 0 ? continuation[0] : null;

                foreach (var seg in continuation)
                {
                    CreateSegmentVisual(seg);
                    if (seg.line != null) seg.line.SetPosition(1, seg.end);
                    UpdateSparkShape(seg, seg.start, seg.end);
                }
                return;
            }
        }

        // 対象が変わった場合は、これまで通り未反射区間から先を丸ごと組み直す
        // ★先端方式：まだ伸びている途中なら、伸びた長さを引き継いで組み直す
        bool chainGrowing = MeasureChain(origin, out float chainLength);
        RemoveChainFrom(origin);
        if (chainGrowing)
        {
            BeamFront rf = StartFront(transform.position, dir, false, null, null);
            if (this == null) return;
            if (fronts.Contains(rf)) AdvanceFront(rf, chainLength);
            return;
        }

        List<BeamSegment> rebuilt = BuildChainFrom(transform.position, dir, false);
        foreach (var seg in rebuilt)
        {
            CreateSegmentVisual(seg);
            if (seg.line != null) seg.line.SetPosition(1, seg.end);
            UpdateSparkShape(seg, seg.start, seg.end);
        }
    }

    // =========================================================
    // セグメント連鎖の構築
    // ★2026/10/17「先端で当てる」方式：ビームは先端（BeamFront）が一定の速さで伸びていき、先端が届いた瞬間に
    //   反射・貫通・ダメージ・演出を起こす（以前は発射の瞬間に連鎖の最後まで判定し、見た目だけ後から伸ばしていた）。
    //   1つの当たりの処理（ProcessHit）は、伸びる先端（AdvanceFront）と、一瞬で組み直す処理（BuildChainFrom。
    //   伸びきった掃射ビームの向き変更で使う）の両方で共通に使う。
    // =========================================================
    private enum HitOutcome { PassThrough, Reflected, Terminated }

    // 連鎖を組み立てている途中の状態（従来のBuildChainFromのローカル変数をまとめたもの）
    private class ChainCtx
    {
        public Vector3 segStart;              // 今のセグメントの始点
        public Vector2 dir;                   // 今の進行方向
        public bool hasReflected;             // プレイヤーが反射させた後の区間か
        public HashSet<Collider2D> ignored;   // この連鎖で貫通・反射済みのコライダー（同じ物に二度当たらない）
        public PaddleDot pendingOriginDot;    // 次に作られるセグメントが直前に反射した線
        public BeamSegment growing;           // 先端方式で今伸びているセグメント（一瞬で組み直す時はnull）
        public BeamSegment prevSeg;           // 直前のセグメント（next連結用）
        public List<BeamSegment> newSegs;     // 一瞬で組み直す時に作ったセグメント（先端方式ではnull）
        public int segCount;                  // この連鎖で作ったセグメント数（安全上限用）
    }

    // 伸びている先端。ctx.growing が伸びているセグメント、tip がその始点からの長さ
    private class BeamFront
    {
        public ChainCtx ctx;
        public float tip;
    }

    private readonly List<BeamFront> fronts = new List<BeamFront>();
    private readonly List<BeamFront> _frontScratch = new List<BeamFront>();

    /// <summary>先端が1秒に進む距離（Beam Grow Duration 秒で Grow Reference Distance を進む）。0以下＝一瞬で伸びきる</summary>
    private float FrontSpeed
    {
        get
        {
            float d = bulletType != null ? bulletType.beamGrowDuration : 0.1f;
            return d > 0f ? Mathf.Max(0.01f, growReferenceDistance) / d : 0f;
        }
    }

    // セグメントを作成し、リスト登録・連結(next)まで行う（従来のBuildChainFrom内のAddSegと同じ）
    private BeamSegment AddSegInstant(ChainCtx c, Vector3 s, Vector3 e, bool reflected)
    {
        var seg = new BeamSegment { start = s, end = e, isReflected = reflected, segDir = c.dir };
        // 反射して生まれたセグメントには、その反射元の線を覚えさせておく。この場で無視するだけでなく、
        // 後々（別のTickの）CheckForNewReflectionsが同じ線を誤検出しないようにするため
        seg.originDot = c.pendingOriginDot;
        seg.originDotGeneration = c.pendingOriginDot != null ? c.pendingOriginDot.PoolGeneration : -1;
        c.pendingOriginDot = null; // 使ったらクリア（次の反射が起きるまでは持ち越さない）
        segments.Add(seg);
        c.newSegs?.Add(seg);
        if (c.prevSeg != null) c.prevSeg.next = seg;
        c.prevSeg = seg;
        c.segCount++;
        return seg;
    }

    // 今のセグメントを end で確定する。先端方式なら伸びていたセグメントを確定、一瞬で組み直す時は新しく作る
    private BeamSegment FinishSeg(ChainCtx c, Vector3 end, bool reflected)
    {
        if (c.growing == null) return AddSegInstant(c, c.segStart, end, reflected);
        BeamSegment seg = c.growing;
        c.growing = null;
        seg.end = end;
        seg.isReflected = reflected;
        seg.segDir = c.dir;
        seg.growing = false;
        if (seg.line != null) seg.line.SetPosition(1, end);
        UpdateSparkShape(seg, seg.start, end);
        c.prevSeg = seg;
        return seg;
    }

    // 先端方式：今の始点・方向で、長さ0から伸びるセグメントを作る
    private void BeginGrowingSeg(ChainCtx c)
    {
        BeamSegment seg = AddSegInstant(c, c.segStart, c.segStart, c.hasReflected);
        seg.growing = true;
        CreateSegmentVisual(seg);
        c.growing = seg;
    }

    /// <summary>
    /// 1つの当たりを処理する（従来のBuildChainFromのループ1回分と同じ内容・同じ順番）。
    /// PassThrough＝素通りして同じ始点から探し直す、Reflected＝反射して ctx の始点・方向が変わった、Terminated＝ここで止まった
    /// </summary>
    private HitOutcome ProcessHit(ChainCtx c, RaycastHit2D hit)
    {
        // Block（WallHealth。ダメージあり）
        WallHealth wall = GetWallHealth(hit.collider);
        if (wall != null)
        {
            if (showDebugLog)
            {
                Debug.Log($"[EnemyBeamBullet] ProcessHit: WallHealth検出。collider={hit.collider.gameObject.name}, layer={hit.collider.gameObject.layer}, hasReflected={c.hasReflected}, hitPoint={hit.point}");
            }
            BeamSegment wallSeg = FinishSeg(c, hit.point, c.hasReflected);
            wallSeg.terminalWall = wall; // Blockが移動した時に追従させるため覚えておく
            wallSeg.hadTerminalWall = true;
            wall.ApplyBeamDamage(!c.hasReflected, damageMultiplier > 1.0001f, hit.point);
            return HitOutcome.Terminated;
        }

        // 敵の線（EnemyLineReflector。Area10最終ボスNeonDancerの後半のみ）：
        //   未反射のビームは素通り、プレイヤーが反射させたビームは反射して未反射に戻る（弾と同じ仕様）
        EnemyLineReflector enemyLine = hit.collider.GetComponent<EnemyLineReflector>();
        if (enemyLine != null)
        {
            c.ignored.Add(hit.collider);
            if (!c.hasReflected || !enemyLine.IsSolid) return HitOutcome.PassThrough;

            BeamSegment lineSeg = FinishSeg(c, hit.point, c.hasReflected);
            lineSeg.reflectionSourceEnemyLine = enemyLine;
            lineSeg.hadReflectionSourceEnemyLine = true;
            damageMultiplier = 1f; // Just反射で上がったダメージ倍率は戻す（弾と同じ仕様）
            ResetA8ForEnemyLine();
            c.dir = Vector2.Reflect(c.dir, hit.normal).normalized;
            enemyLine.NotifyBeamReflected(hit.point, c.dir); // 反射のSE・エフェクト（NeonDancer側で再生）
            c.segStart = hit.point;
            c.hasReflected = false;
            return HitOutcome.Reflected;
        }

        // Beam Reflector（ダメージを与えず・受けず、物理的な壁として反射する。Obelisk本体等）
        BeamReflector reflector = hit.collider.GetComponent<BeamReflector>();
        if (reflector != null)
        {
            BeamSegment reflectorSeg = FinishSeg(c, hit.point, c.hasReflected);
            reflectorSeg.reflectedOffReflector = reflector; // 当たり続けている間、TickBeamReflectorVfxでVFXを繰り返す
            reflector.PlayReflectVfx(hit.point);

            // プレイヤーが反射させたことにはならないため、hasReflectedは変更しない
            // （Obelisk自身の壁に当たっただけで他の敵にダメージが通るようになるのを防ぐ）
            c.ignored.Add(hit.collider);
            c.dir = Vector2.Reflect(c.dir, hit.normal).normalized;
            c.segStart = hit.point;
            return HitOutcome.Reflected;
        }

        // Enemy（反射後のみ。ここで停止する。以降はTickReflectedSegmentが同じ終端位置で継続ダメージを入れる）
        if (c.hasReflected)
        {
            EnemyPart hitPart = hit.collider.GetComponent<EnemyPart>();
            EnemyDamageReceiver hitReceiver = hitPart == null ? GetEnemyDamageReceiver(hit.collider) : null;
            if (hitPart != null || hitReceiver != null)
            {
                BeamSegment enemySeg = FinishSeg(c, hit.point, c.hasReflected);
                enemySeg.terminalEnemyPart = hitPart; // 移動追従に使う（どちらか一方のみ入る）
                enemySeg.terminalEnemyReceiver = hitReceiver;
                enemySeg.hadTerminalEnemy = true;
                int baseDamage = Mathf.RoundToInt(currentDamage);
                bool newHitFx = ReflectedBulletFXManager.BeginExternalEnemyHit(); // 旧の敵ヒットVFXを止め、新しい演出を出す
                bool hitApplied = hitPart != null
                    ? hitPart.TryApplyExternalReflectedDamage(baseDamage, damageMultiplier, hit.point)
                    : hitReceiver.TryApplyExternalReflectedDamage(baseDamage, damageMultiplier, hit.point);
                if (newHitFx) { if (hitApplied) PlayBeamEnemyHitFx(enemySeg, hit.point, true); }
                else TrySpawnEnemyHitVfx(hit.point);
                if (hitApplied) RegisterA8EnemyHit();
                return HitOutcome.Terminated;
            }
        }

        // 画面境界の壁（HP無し、跳ね返らずダメージも無し。そのまま終端する）
        if (IsBoundaryWallCollider(hit.collider))
        {
            FinishSeg(c, hit.point, c.hasReflected);
            return HitOutcome.Terminated;
        }

        // Player / Floor（未反射区間のみ該当。ここで終端する）
        if (!c.hasReflected)
        {
            PixelDancerController player = hit.collider.GetComponent<PixelDancerController>();

            if (player != null && bulletType != null && bulletType.beamIgnorePlayer)
            {
                // Playerを無視する設定：当たり判定を持たず貫通してFloorまで直進する。
                // 始点は動かさない（動かすと、発射地点からPlayerまでの区間が描画されなくなる）。
                // ignoredに入れるだけで、次の探索が同じ始点から再探索してこのコライダーを飛ばしてくれる
                c.ignored.Add(hit.collider);
                return HitOutcome.PassThrough;
            }

            FloorHealth floor = hit.collider.GetComponent<FloorHealth>();
            if (player != null || floor != null)
            {
                BeamSegment targetSeg = FinishSeg(c, hit.point, false);
                if (player != null)
                {
                    // FloorはPixelDancerと違って動かないので追従不要。Playerだけ移動追従させる
                    targetSeg.terminalPlayer = player;
                    targetSeg.hadTerminalPlayer = true;
                }
                return HitOutcome.Terminated;
            }
        }

        // Paddleライン
        PaddleDot dot = hit.collider.GetComponent<PaddleDot>();
        if (dot != null)
        {
            bool penetrated = dot.EvaluateExternalHit(penetration, hit.point, hit.normal, out float justMulOut);
            if (penetrated)
            {
                // 貫通：このコライダーは無視済みとして記録するだけで、始点は動かさない（beamIgnorePlayerと同じ理由）
                c.ignored.Add(hit.collider);
                return HitOutcome.PassThrough;
            }

            // 反射：ここでセグメントを確定し、新しいセグメントを反射方向へ開始する
            BeamSegment preReflectSeg = FinishSeg(c, hit.point, c.hasReflected);
            preReflectSeg.reflectionSourceDot = dot; // この線が消えたら反射前の方向(segDir)に戻すために覚えておく
            preReflectSeg.reflectionSourceDotGeneration = dot.PoolGeneration;
            preReflectSeg.hadReflectionSourceDot = true;
            TrySpawnPaddleHitVfx(hit.point);

            damageMultiplier = Mathf.Max(damageMultiplier, justMulOut);
            c.hasReflected = true;
            reflectCount++;

            if (bulletType != null && bulletType.paddleBounceLimit >= 0 && reflectCount >= bulletType.paddleBounceLimit)
            {
                return HitOutcome.Terminated;
            }

            // このコライダーは反射済みとして記録する。反射方向によっては次のRayが同じコライダーに
            // 再ヒットし得るため（浅い角度・真後ろ付近の反射など）、同一チェーン内での二重反射を防ぐ
            c.ignored.Add(hit.collider);
            c.pendingOriginDot = dot; // 次に生まれるセグメントは、この線から反射して生まれる

            Vector2 incoming = c.dir;
            c.dir = Vector2.Reflect(incoming, PaddleReflectNormal(dot, hit.normal, incoming)).normalized;
            ReflectedBulletFXManager.NotifyBeamReflect(hit.point, dot, justMulOut > 1.0001f, c.dir); // 弾の反射と同じ演出
            c.segStart = hit.point;
            return HitOutcome.Reflected;
        }

        // 未分類のコライダー：安全側として無視し直進を継続（同じものに何度も当たらないよう記録だけしておく）
        c.ignored.Add(hit.collider);
        return HitOutcome.PassThrough;
    }

    /// <summary>
    /// 一瞬で連鎖を最後まで組み立てる（先端方式ではなく、伸びきった後の掃射ビームの向き変更で使う）。
    /// 呼び出しごとに、新しく追加されたセグメントのリストを返す
    /// </summary>
    private List<BeamSegment> BuildChainFrom(Vector3 startOrigin, Vector2 startDir, bool startHasReflected, Collider2D preIgnore = null, BeamSegment linkFrom = null)
    {
        // ★負荷軽減：無視リストは毎回newせず使い回す（この関数は入れ子で呼ばれないため1つで足りる）
        var ignored = _buildChainIgnoredScratch;
        ignored.Clear();
        var c = new ChainCtx
        {
            segStart = startOrigin, dir = startDir, hasReflected = startHasReflected,
            ignored = ignored, prevSeg = linkFrom, newSegs = new List<BeamSegment>(),
        };
        // 呼び出し元ですでに反射済みのコライダーを、この場でもう一度検出しないよう先に除外しておく
        if (preIgnore != null)
        {
            ignored.Add(preIgnore);
            c.pendingOriginDot = preIgnore.GetComponent<PaddleDot>();
        }

        for (int guard = 0; guard < maxSegmentsSafety * 4 && c.segCount < maxSegmentsSafety; guard++)
        {
            RaycastHit2D hit = FindNextRelevantHit(c.segStart, c.dir, ignored, c.hasReflected, maxSegmentDistance);

            if (!hit.collider)
            {
                if (showDebugLog && c.hasReflected)
                {
                    Debug.Log($"[EnemyBeamBullet] BuildChainFrom: 何にもヒットせず最大距離まで直進。segStart={c.segStart}, dir={c.dir}, hasReflected={c.hasReflected}");
                    LogAllWallHealthColliderInfo(c.segStart, c.dir);
                }
                AddSegInstant(c, c.segStart, c.segStart + (Vector3)(c.dir * maxSegmentDistance), c.hasReflected).isOpenEnd = true;
                return c.newSegs;
            }

            HitOutcome o = ProcessHit(c, hit);
            if (this == null || o == HitOutcome.Terminated) return c.newSegs;
        }

        if (showDebugLog)
        {
            Debug.LogWarning("[EnemyBeamBullet] BuildChainFrom: 上限に到達しました。安全のため打ち切ります。");
        }

        return c.newSegs;
    }

    /// <summary>
    /// 先端を作る：start から dir へ、長さ0のセグメントが伸び始める。linkFrom はこのセグメントの手前のセグメント（next連結用。発射時はnull）。
    /// Beam Grow Duration が0以下なら、その場で最後まで伸びきる
    /// </summary>
    private BeamFront StartFront(Vector3 start, Vector2 dir, bool hasReflected, Collider2D preIgnore, BeamSegment linkFrom)
    {
        var c = new ChainCtx
        {
            segStart = start, dir = dir, hasReflected = hasReflected,
            ignored = new HashSet<Collider2D>(), prevSeg = linkFrom,
        };
        if (preIgnore != null)
        {
            c.ignored.Add(preIgnore);
            c.pendingOriginDot = preIgnore.GetComponent<PaddleDot>();
        }
        BeginGrowingSeg(c);
        var f = new BeamFront { ctx = c, tip = 0f };
        fronts.Add(f);
        if (FrontSpeed <= 0f) AdvanceFront(f, maxSegmentDistance * maxSegmentsSafety);
        return f;
    }

    /// <summary>
    /// 先端を distance だけ進める。始点から新しい先端までの間に当たる物があれば、そこで ProcessHit を起こす
    /// （先端より手前に後から線が引かれた・プレイヤーが入ってきた場合も、ここで当たる）。
    /// 反射したら、残りの距離で次のセグメントを伸ばす
    /// </summary>
    private void AdvanceFront(BeamFront f, float distance)
    {
        ChainCtx c = f.ctx;
        float remaining = distance;
        for (int guard = 0; guard < maxSegmentsSafety * 4; guard++)
        {
            if (c.growing == null) { fronts.Remove(f); return; }

            float target = f.tip + remaining;
            float limit = Mathf.Min(target, maxSegmentDistance);
            RaycastHit2D hit = FindNextRelevantHit(c.segStart, c.dir, c.ignored, c.hasReflected, limit);

            if (hit.collider)
            {
                float hitDist = hit.distance;
                HitOutcome o = ProcessHit(c, hit);
                if (this == null) return;
                if (o == HitOutcome.PassThrough) continue; // 素通り：同じ始点から探し直す
                if (o == HitOutcome.Terminated) { fronts.Remove(f); return; }

                // 反射：残りの距離で次のセグメントを伸ばす
                remaining = Mathf.Max(0f, target - hitDist);
                if (c.segCount >= maxSegmentsSafety) { fronts.Remove(f); return; }
                BeginGrowingSeg(c);
                f.tip = 0f;
                continue;
            }

            // 何にも当たらない：先端を進める。最大距離まで伸びきったら確定（何にも当たらず伸びきったセグメント）
            f.tip = limit;
            BeamSegment seg = c.growing;
            seg.end = c.segStart + (Vector3)(c.dir * f.tip);
            if (seg.line != null) seg.line.SetPosition(1, seg.end);
            UpdateSparkShape(seg, seg.start, seg.end);
            if (f.tip >= maxSegmentDistance - 0.0001f)
            {
                FinishSeg(c, seg.end, c.hasReflected).isOpenEnd = true;
                fronts.Remove(f);
            }
            return;
        }
        fronts.Remove(f);
    }

    private BeamFront FindFront(BeamSegment growingSeg)
    {
        for (int i = 0; i < fronts.Count; i++)
            if (fronts[i].ctx.growing == growingSeg) return fronts[i];
        return null;
    }

    /// <summary>s から先（next連鎖）の、今の長さの合計。伸びている途中のセグメントがあれば true（掃射の組み直しで、伸びた分を引き継ぐのに使う）</summary>
    private bool MeasureChain(BeamSegment s, out float length)
    {
        length = 0f;
        bool growingFound = false;
        for (BeamSegment cur = s; cur != null; cur = cur.next)
        {
            if (cur.growing)
            {
                BeamFront f = FindFront(cur);
                length += f != null ? f.tip : Vector3.Distance(cur.start, cur.end);
                growingFound = true;
            }
            else length += Vector3.Distance(cur.start, cur.end);
        }
        return growingFound;
    }

    // 先端を毎フレーム進める（スローモーションの速さに合わせる。一時停止中は進まない）
    private void Update()
    {
        if (fronts.Count == 0) return;
        float dt = Time.deltaTime * GetTimeScale();
        if (dt <= 0f) return;
        float speed = FrontSpeed;
        float dist = speed > 0f ? speed * dt : maxSegmentDistance * maxSegmentsSafety;

        _frontScratch.Clear();
        _frontScratch.AddRange(fronts);
        for (int i = 0; i < _frontScratch.Count; i++)
        {
            BeamFront f = _frontScratch[i];
            if (!fronts.Contains(f)) continue;
            AdvanceFront(f, dist);
            if (this == null) return;
        }
    }

    /// <summary>
    /// Raycastの結果から、無視すべきコライダー（貫通済み・未反射区間のオーナー/敵など）を飛ばして
    /// 次に意味のあるヒットを1つ返す。同一Rayの結果内で判定するため、コライダーのサイズに関係なく正確。
    /// </summary>
    private RaycastHit2D FindNextRelevantHit(Vector3 origin, Vector2 dir, HashSet<Collider2D> ignored, bool hasReflected, float maxDistance)
    {
        if (maxDistance <= 0f) return default;
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, dir, maxDistance, hitLayers | boundaryWallLayers);
        if (hits.Length > 1) System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i].collider;
            if (col == null || ignored.Contains(col)) continue;

            bool isOwner = IsOwnerCollider(col);
            bool isEnemy = IsEnemyCollider(col);

            // 未反射区間はオーナー（発射元エネミー）・敵コライダーを無視する（既存弾の未反射弾も敵に一切干渉しない仕様と同じ）
            if (!hasReflected && (isOwner || isEnemy)) continue;

            // 未反射区間はBlock（WallHealth）とも当たり判定を持たない（既存弾の未反射弾と同じ仕様）
            if (!hasReflected && GetWallHealth(col) != null) continue;

            return hits[i];
        }

        return default;
    }

    private bool IsOwnerCollider(Collider2D col)
    {
        if (ownerColliders == null) return false;
        for (int i = 0; i < ownerColliders.Length; i++)
        {
            if (ownerColliders[i] == col) return true;
        }
        return false;
    }

    private static bool IsEnemyCollider(Collider2D col)
    {
        if (col == null) return false;
        if (col.GetComponent<EnemyDamageReceiver>() != null) return true;
        Transform t = col.transform.parent;
        for (int i = 0; i < 4 && t != null; i++)
        {
            if (t.GetComponent<EnemyDamageReceiver>() != null) return true;
            t = t.parent;
        }
        return false;
    }

    private bool IsBoundaryWallCollider(Collider2D col)
    {
        if (col == null) return false;
        if (boundaryWallLayers.value == 0) return false;

        Transform t = col.transform;
        for (int i = 0; i < 8 && t != null; i++)
        {
            int layer = t.gameObject.layer;
            if ((boundaryWallLayers.value & (1 << layer)) != 0) return true;
            t = t.parent;
        }
        return false;
    }

    private static WallHealth GetWallHealth(Collider2D col)
    {
        WallHealth wh = col.GetComponent<WallHealth>();
        if (wh == null) wh = col.GetComponentInParent<WallHealth>();
        return wh;
    }

    // ★追加：1本の線（Stroke）は多数の個別PaddleDotインスタンスの集まりのため、「同じ線かどうか」は
    // インスタンス比較ではなくParentStroke単位で判定する。CheckForNewReflections/UpdateWallTrackedSegments/
    // UpdateEnemyTrackedSegments（＝「後から現れた別の線」を検出する目的の関数）だけに使う。
    // BuildChainFrom（発射時に円の中を何度も反射する仕様を1回の呼び出しで完結させている箇所）には
    // 絶対に適用しないこと（適用すると、同じ線に何度も反射する円の仕様ごと壊れて貫通する）
    // ★プレイヤーの線で反射する時の鏡の向き：線は円（PaddleDot）を並べて作るため、当たった円の表面の向き（hitNormal）は
    //   当たる位置でぶれ、Bit等のビームを狙った角度に曲げにくかった。当たった付近の線そのものの向きから求めた法線を使う
    //   （求められない時・OFFの時は従来どおりhitNormal）。向きはビームが来た側に向ける
    private Vector2 PaddleReflectNormal(PaddleDot dot, Vector2 hitNormal, Vector2 incomingDir)
    {
        if (!reflectByLineDirection || dot == null || dot.ParentStroke == null) return hitNormal;
        if (!dot.ParentStroke.TryGetLocalDirection(dot, lineDirectionSampleRange, out Vector2 tangent)) return hitNormal;
        Vector2 n = new Vector2(-tangent.y, tangent.x);
        if (Vector2.Dot(n, incomingDir) > 0f) n = -n;
        return n;
    }

    private static bool IsSameStroke(PaddleDot a, PaddleDot b)
    {
        if (a == null || b == null) return false;
        Stroke sa = a.ParentStroke;
        Stroke sb = b.ParentStroke;
        return sa != null && sa == sb;
    }

    private static EnemyDamageReceiver GetEnemyDamageReceiver(Collider2D col)
    {
        EnemyDamageReceiver r = col.GetComponent<EnemyDamageReceiver>();
        if (r != null) return r;
        Transform t = col.transform.parent;
        for (int i = 0; i < 4 && t != null; i++)
        {
            r = t.GetComponent<EnemyDamageReceiver>();
            if (r != null) return r;
            t = t.parent;
        }
        return null;
    }

    // =========================================================
    // 都度判定：存在している間、全セグメントに新しいパドルラインが交差していないか再チェックする
    // =========================================================
    private IEnumerator LiveReflectionCheckRoutine(float interval)
    {
        while (true)
        {
            float t = 0f;
            while (t < interval)
            {
                // プレイヤーの線描画（PaddleDrawer）はスローモーションの影響を受けず常に実時間で動くため、
                // このチェックも実時間（unscaledDeltaTime）で回す。GetTimeScale()で遅らせると、
                // スロー中に新しく描いた線への反射検出だけ最大5倍遅れてしまう
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            CheckForNewReflections();
        }
    }

    private void CheckForNewReflections()
    {
        ClearStaleDotRefs();

        // segmentsは判定中に増減し得るため、対象は先にスナップショットしておく
        var snapshot = RentSegmentSnapshot();

        foreach (BeamSegment seg in snapshot)
        {
            // ★再入対策：このループの前の回でBuildChainFromが自分自身（発射元）のWallHealthを破壊させ、
            // それが自分自身のDestroy()を引き起こしていた場合、それ以上ゾンビ状態で処理を続けない
            if (this == null) return;

            // 同じTick内で、より手前のセグメントの打ち切りによってすでに削除された可能性がある
            if (!segments.Contains(seg)) continue;
            // 伸びている途中のセグメントは、先端の判定（AdvanceFront）が始点〜先端の間の線を毎フレーム調べている
            if (seg.growing) continue;

            Vector2 full = seg.end - seg.start;
            float len = full.magnitude;
            if (len < 0.01f) continue;
            Vector2 dir = full / len;

            RaycastHit2D[] hits = Physics2D.RaycastAll(seg.start, dir, len, hitLayers);
            if (hits.Length > 1) System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (int h = 0; h < hits.Length; h++)
            {
                PaddleDot dot = hits[h].collider != null ? hits[h].collider.GetComponent<PaddleDot>() : null;
                if (dot == null) continue;

                // ★同じ線（Stroke）単位で無視する。BuildChainFromは円の中を何度も反射する仕様を
                // 1回の呼び出しで完結させているため、ここ（後から現れた別の線を検出する場所）で
                // 同じ線を拾う必要はそもそも無い
                if (IsSameStroke(dot, seg.reflectionSourceDot) || IsSameStroke(dot, seg.originDot)) continue;

                bool penetrated = dot.EvaluateExternalHit(penetration, hits[h].point, hits[h].normal, out float justMulOut);
                if (penetrated) continue; // 貫通：このセグメントの見た目は変化なし。他の候補も同じセグメント上にあり得るので探索は続ける

                // 反射：この先に繋がっていた古いセグメント連鎖はまるごと削除してから打ち切る（浮き対策）
                if (seg.next != null)
                {
                    RemoveChainFrom(seg.next);
                    seg.next = null;
                }

                seg.end = hits[h].point;
                if (seg.line != null) seg.line.SetPosition(1, seg.end);
                UpdateSparkShape(seg, seg.start, seg.end);
                seg.reflectionSourceDot = dot; // この線が消えたら反射前の方向(segDir)に戻すために覚えておく
                seg.reflectionSourceDotGeneration = dot.PoolGeneration;
                seg.hadReflectionSourceDot = true;
                TrySpawnPaddleHitVfx(seg.end);

                damageMultiplier = Mathf.Max(damageMultiplier, justMulOut);
                reflectCount++;

                if (bulletType != null && bulletType.paddleBounceLimit >= 0 && reflectCount >= bulletType.paddleBounceLimit)
                {
                    break;
                }

                Vector2 reflectDir = Vector2.Reflect(dir, PaddleReflectNormal(dot, hits[h].normal, dir)).normalized;
                ReflectedBulletFXManager.NotifyBeamReflect(seg.end, dot, justMulOut > 1.0001f, reflectDir); // 弾の反射と同じ演出
                // ★先端方式：反射した先は、反射点から先端が伸びていく（当たりは先端が届いた時）
                StartFront(seg.end, reflectDir, true, hits[h].collider, seg);

                // ★再入対策：その場で伸びきる設定の時、発射元自身のWallHealthを破壊させ、それが自分自身の
                // Destroy()を引き起こしていた場合、ここで即座に打ち切る
                if (this == null) return;

                if (showDebugLog)
                {
                    Debug.Log($"[EnemyBeamBullet] Live reflect: セグメント途中(={seg.end})で新しい線を検出。反射点から先端を伸ばします。");
                }

                break; // このセグメントの処理はここで終わり、次のセグメントへ
            }
        }

        ReturnSegmentSnapshot(snapshot);
    }

    // このセグメントから先（next連鎖）を全部削除する。打ち切りで不要になった古い連鎖の後始末に使う
    private void RemoveChainFrom(BeamSegment startSeg)
    {
        BeamSegment cur = startSeg;
        while (cur != null)
        {
            BeamSegment toRemove = cur;
            cur = cur.next;
            if (toRemove.growing)
            {
                BeamFront f = FindFront(toRemove);
                if (f != null) fronts.Remove(f);
            }
            segments.Remove(toRemove);
            if (toRemove.visualRoot != null) Destroy(toRemove.visualRoot);
        }
    }

    // =========================================================
    // ビジュアル（LineRenderer + 火花パーティクル）
    // =========================================================
    private void CreateSegmentVisual(BeamSegment seg)
    {
        GameObject go = new GameObject("BeamSegment");
        go.transform.SetParent(transform, true);
        seg.visualRoot = go;

        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.SetPosition(0, seg.start);
        lr.SetPosition(1, seg.start); // 伸びる演出のため、最初は長さゼロから開始

        float w = (bulletType != null && bulletType.beamWidth > 0f) ? bulletType.beamWidth : 0.15f;
        lr.startWidth = w;
        lr.endWidth = w;

        // 両端を丸くする（デフォルトの0だと角ばった四角い切り口になる）。
        // セグメントごとに別々のLineRendererなので、反射で繋がる継ぎ目もこれで滑らかに見える
        lr.numCapVertices = 8;
        lr.numCornerVertices = 4;

        Color cA = (bulletType != null) ? bulletType.beamColor : Color.cyan;
        Color cB = (bulletType != null) ? bulletType.beamColorEnd : Color.cyan;
        // 反射後の区間は、弾の反射と同じ配色（ノーマル＝白〜シアン、ジャスト＝白〜オレンジ〜赤）にする（ReflectedBulletFXManager）
        if (seg.isReflected && ReflectedBulletFXManager.TryGetBeamReflectColors(IsJustReflected, out Color rA, out Color rB))
        {
            cA = rA; cB = rB;
        }

        // 「始点=cA、終点=cB」で位置固定にすると、反射で繋がる隣のセグメントとの境目に
        // 毎回同じ色の境界ができて反射角度がくっきり見えてしまう。色の並びをランダムにして防ぐ
        float[] positions = { 0f, Random.Range(0.3f, 0.7f), 1f };
        GradientColorKey[] colorKeys = new GradientColorKey[3];
        GradientAlphaKey[] alphaKeys = new GradientAlphaKey[3];
        for (int i = 0; i < 3; i++)
        {
            float t = Random.value;
            colorKeys[i] = new GradientColorKey(Color.Lerp(cA, cB, t), positions[i]);
            alphaKeys[i] = new GradientAlphaKey(Mathf.Lerp(cA.a, cB.a, t), positions[i]);
        }
        Gradient beamGrad = new Gradient();
        beamGrad.SetKeys(colorKeys, alphaKeys);
        lr.colorGradient = beamGrad;
        seg.baseColorKeys = colorKeys;
        seg.baseAlphaKeys = alphaKeys;

        if (beamMaterial != null) lr.material = beamMaterial;
        lr.sortingLayerName = sortingLayerName;
        lr.sortingOrder = sortingOrder;
        seg.line = lr;

        if (bulletType != null && bulletType.beamSparkParticlePrefab != null)
        {
            GameObject sparkGo = Instantiate(bulletType.beamSparkParticlePrefab, seg.start, Quaternion.identity, go.transform);
            seg.spark = sparkGo.GetComponent<ParticleSystem>();
            UpdateSparkShape(seg, seg.start, seg.start);
        }
    }

    // beamPulseEnabled時のみ、ビーム本体の明るさ（不透明度）を周期的に明滅させる。
    // 対象外の既存ビーム敵には一切コストをかけないよう、bulletType.beamPulseEnabledでまとめて早期returnする
    private void LateUpdate()
    {
        // 反射後の区間に、弾の反射と同じ光の粒・火の粉・稲妻・先端の発光を出す（ReflectedBulletFXManager）
        ReflectedBulletFXManager.TickBeam(this);

        if (bulletType == null || !bulletType.beamPulseEnabled) return;

        float pulseMul = Mathf.Lerp(bulletType.beamPulseMinAlphaMultiplier, 1f,
            (Mathf.Sin(Time.time * bulletType.beamPulseSpeed) + 1f) * 0.5f);

        for (int i = 0; i < segments.Count; i++)
        {
            BeamSegment seg = segments[i];
            if (seg.line == null || seg.baseAlphaKeys == null) continue;

            if (seg.pulseAlphaKeysScratch == null || seg.pulseAlphaKeysScratch.Length != seg.baseAlphaKeys.Length)
            {
                seg.pulseAlphaKeysScratch = new GradientAlphaKey[seg.baseAlphaKeys.Length];
            }
            if (seg.pulseGradient == null) seg.pulseGradient = new Gradient();

            for (int k = 0; k < seg.baseAlphaKeys.Length; k++)
            {
                seg.pulseAlphaKeysScratch[k] = new GradientAlphaKey(
                    seg.baseAlphaKeys[k].alpha * pulseMul * seg.fadeAlphaMul,
                    seg.baseAlphaKeys[k].time);
            }

            seg.pulseGradient.SetKeys(seg.baseColorKeys, seg.pulseAlphaKeysScratch);
            seg.line.colorGradient = seg.pulseGradient;
        }
    }

    private void UpdateSparkShape(BeamSegment seg, Vector3 from, Vector3 to)
    {
        if (seg.spark == null) return;

        Vector3 mid = (from + to) * 0.5f;
        seg.spark.transform.position = mid;

        float length = Vector3.Distance(from, to);
        float angle = Mathf.Atan2(to.y - from.y, to.x - from.x) * Mathf.Rad2Deg;
        seg.spark.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        var shape = seg.spark.shape;
        if (shape.shapeType == ParticleSystemShapeType.Box)
        {
            Vector3 boxSize = shape.scale;
            boxSize.x = Mathf.Max(0.02f, length);
            shape.scale = boxSize;
        }

        var emission = seg.spark.emission;
        emission.rateOverTimeMultiplier = Mathf.Max(1f, length) * sparkDensityPerUnit;
    }

    // =========================================================
    // 継続ダメージ（都度判定。未反射区間=PixelDancer/Floor、反射後区間=エネミー）
    // =========================================================
    /// <summary>
    /// ★PaddleDotプーリング対応（2026/9/30の線の使い回し導入で効かなくなった判定の復旧）：
    /// 線が消えてもDotはDestroyされずプールへ戻る（非アクティブ化）か、別の線として使い回される（PoolGenerationが進む）ため、
    /// 参照がnullにならず「線が消えた」判定（reflectionSourceDot == null）が働かなくなっていた。
    /// 判定の直前にこれを呼び、消えた線のDot参照をnullに戻すことで、プーリング導入前と同じ状態にする
    /// （PinnedReflectBulletと同じ判定方法。以降の既存ロジックは一切変更しない）。
    /// </summary>
    private void ClearStaleDotRefs()
    {
        foreach (BeamSegment seg in segments)
        {
            if (seg.reflectionSourceDot != null && IsDotGone(seg.reflectionSourceDot, seg.reflectionSourceDotGeneration))
                seg.reflectionSourceDot = null;
            if (seg.originDot != null && IsDotGone(seg.originDot, seg.originDotGeneration))
                seg.originDot = null;
        }
    }

    private static bool IsDotGone(PaddleDot dot, int recordedGeneration)
    {
        return !dot.gameObject.activeInHierarchy || dot.PoolGeneration != recordedGeneration;
    }

    // 未反射区間（PixelDancer/Floor）へのダメージの間隔（Beam Unreflected Damage Tick Rate）。
    //   Tickごとに経過時間を足していき、この間隔に届いたTickだけ未反射区間のダメージを与える（最初のTickは必ず与える）
    private float unreflectedDamageInterval;
    private float unreflectedDamageAcc;
    private float tickInterval;

    private IEnumerator DamageTickRoutine(float interval)
    {
        tickInterval = interval;
        float uRate = (bulletType != null && bulletType.beamUnreflectedDamageTickRate > 0f) ? bulletType.beamUnreflectedDamageTickRate : 0f;
        unreflectedDamageInterval = uRate > 0f ? 1f / uRate : interval;
        unreflectedDamageAcc = unreflectedDamageInterval; // 最初のTickでは今まで通りダメージを与える
        while (true)
        {
            float t = 0f;
            while (t < interval)
            {
                t += Time.deltaTime * GetTimeScale();
                yield return null;
            }
            ApplyTickDamage();
        }
    }

    private void ApplyTickDamage()
    {
        if (bulletType == null || segments.Count == 0) return;
        int baseDamage = Mathf.RoundToInt(currentDamage);

        ClearStaleDotRefs();
        UpdateWallTrackedSegments();
        UpdateEnemyTrackedSegments();
        UpdatePlayerTrackedSegments();
        UpdateReflectionSourceDots();
        UpdateReflectionSourceEnemyLines();
        CheckForNewEnemyLines();
        CheckOpenSegmentsForNewHit();
        TickPaddleReflectionVfx();
        TickBeamReflectorVfx();

        // 未反射区間のダメージは、指定があればその間隔に届いたTickだけ与える（指定が無ければ毎Tick＝従来どおり）
        unreflectedDamageAcc += tickInterval;
        bool applyUnreflected = unreflectedDamageAcc + 0.0001f >= unreflectedDamageInterval;
        if (applyUnreflected) unreflectedDamageAcc -= unreflectedDamageInterval;

        // segmentsが判定中に増える可能性があるため件数を固定してから回す
        int count = segments.Count;
        for (int i = 0; i < count; i++)
        {
            BeamSegment seg = segments[i];
            if (seg.growing) continue; // 先端が届くまでは継続ダメージを入れない
            if (!seg.isReflected)
            {
                if (applyUnreflected) TickUnreflectedSegment(seg, baseDamage);
            }
            else
            {
                TickReflectedSegment(seg, baseDamage);
            }

            // ★再入対策：ApplyBeamDamage等が同期的に自分自身（発射元）のWallHealthを破壊させ、
            // それがOnBrokenイベント経由でこのBeam自身をDestroy()させることがある（例：円で反射させた
            // 自弾が発射元自身に命中するケース）。Destroy()はフレーム終わりまで実際には破棄されないため、
            // 何もしないとこのループが「破棄予約済み」の自分自身の上で残りのセグメントを処理し続けてしまう
            if (this == null) return;
        }
    }

    // 終端したセグメントの続き（対象が消えた後の延長分）を、このセグメント自身のnextとして生やす共通処理。
    // Block破壊・Enemy撃破・反射元の線の消滅、いずれも「対象が消えたのでそこで止めず先へ進む」という点で同じ
    private void ContinueSegmentPastGone(BeamSegment seg)
    {
        // ★先端方式：そこから先端が伸びていく（当たりは先端が届いた時）
        StartFront(seg.end, seg.segDir, seg.isReflected, null, seg);
    }

    // Blockに当たって終端したセグメントを、そのBlockの現在位置に追従させる。
    // Blockが動いて隙間ができた場合はセグメントを伸ばし（逆に近づいた場合は縮め）、
    // 常にセグメントの先端がBlockの表面に触れ続けるようにする（ダメージが途切れないようにするため）。
    // Blockが破壊されて無くなった場合は、そこで止めずに同じ方向へビームを延長する（止まるのは対象に当たった時だけ）
    private void UpdateWallTrackedSegments()
    {
        // BuildChainFrom呼び出しでsegmentsが増える可能性があるため、対象は先にスナップショットしておく
        var snapshot = RentSegmentSnapshot();

        foreach (BeamSegment seg in snapshot)
        {
            if (!segments.Contains(seg)) continue; // 同じTick内で既に削除されている場合はスキップ
            if (seg.growing) continue; // 先端が届くまでは対象外

            // WallHealth.Break()はGameObjectをDestroyせず、Collider/Rendererを無効化するだけなので、
            // 参照がnullになることはほぼ無い。IsBrokenも合わせて見る必要がある
            if (seg.hadTerminalWall && (seg.terminalWall == null || seg.terminalWall.IsBroken))
            {
                // Blockが破壊されて消えた：そこで止めず、同じ方向へビームを延長する
                seg.hadTerminalWall = false; // 一度だけ処理する
                ContinueSegmentPastGone(seg);
                continue;
            }

            if (seg.terminalWall == null) continue; // 追従対象が無い

            Vector2 toWall = (Vector2)seg.terminalWall.transform.position - (Vector2)seg.start;
            if (toWall.sqrMagnitude < 0.0001f) continue;
            Vector2 dir = toWall.normalized;

            // seg.startは反射した線(originDot)の表面ちょうどにあるため、単発のRaycastだとBlockより先に
            // その同じ線を拾ってしまうことがある。RaycastAllで全ヒットを取り、その線はスキップする
            RaycastHit2D[] hits = Physics2D.RaycastAll(seg.start, dir, maxSegmentDistance, hitLayers);
            if (hits.Length > 1) System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            RaycastHit2D hit = default;
            for (int hi = 0; hi < hits.Length; hi++)
            {
                if (hits[hi].collider == null) continue;
                PaddleDot hitDot = hits[hi].collider.GetComponent<PaddleDot>();
                if (IsSameStroke(hitDot, seg.originDot) || IsSameStroke(hitDot, seg.reflectionSourceDot)) continue;
                hit = hits[hi];
                break;
            }

            WallHealth hitWall = hit.collider != null ? GetWallHealth(hit.collider) : null;

            if (hitWall != seg.terminalWall)
            {
                // 別の何かが手前を塞いでいる等、今回は追従できない。次のTickで再試行する
                continue;
            }

            seg.end = hit.point;
            if (seg.line != null) seg.line.SetPosition(1, seg.end);
            UpdateSparkShape(seg, seg.start, seg.end);
        }

        ReturnSegmentSnapshot(snapshot);
    }

    // Enemyに当たって終端したセグメントを、そのEnemyの現在位置に追従させる。考え方はUpdateWallTrackedSegmentsと同じ。
    // Enemyが撃破されて無くなった場合は、そこで止めずに同じ方向へビームを延長する
    private void UpdateEnemyTrackedSegments()
    {
        var snapshot = RentSegmentSnapshot();

        foreach (BeamSegment seg in snapshot)
        {
            if (!segments.Contains(seg)) continue;
            if (seg.growing) continue; // 先端が届くまでは対象外

            if (seg.hadTerminalEnemy && seg.terminalEnemyPart == null && seg.terminalEnemyReceiver == null)
            {
                // Enemyが撃破されて消えた：そこで止めず、同じ方向へビームを延長する
                seg.hadTerminalEnemy = false; // 一度だけ処理する
                ContinueSegmentPastGone(seg);
                continue;
            }

            Transform targetT = seg.terminalEnemyPart != null ? seg.terminalEnemyPart.transform
                               : (seg.terminalEnemyReceiver != null ? seg.terminalEnemyReceiver.transform : null);
            if (targetT == null) continue; // 追従対象が無い

            Vector2 toTarget = (Vector2)targetT.position - (Vector2)seg.start;
            if (toTarget.sqrMagnitude < 0.0001f) continue;
            Vector2 dir = toTarget.normalized;

            // seg.startはこのセグメントが直前に反射した線(originDot)の表面ちょうどにあるため、単発のRaycastだと
            // Enemyへ向かうRayがEnemyより先にその同じ線を拾ってしまうことがある。RaycastAllで全ヒットを取り、
            // originDot（反射元の線）はスキップして探す
            RaycastHit2D[] hits = Physics2D.RaycastAll(seg.start, dir, maxSegmentDistance, hitLayers);
            if (hits.Length > 1) System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            RaycastHit2D hit = default;
            bool found = false;
            for (int hi = 0; hi < hits.Length; hi++)
            {
                if (hits[hi].collider == null) continue;
                PaddleDot hitDot = hits[hi].collider.GetComponent<PaddleDot>();
                if (IsSameStroke(hitDot, seg.originDot) || IsSameStroke(hitDot, seg.reflectionSourceDot)) continue; // 自分が反射した/生まれた線は無視
                hit = hits[hi];
                found = true;
                break;
            }

            if (!found)
            {
                if (showDebugLog)
                {
                    Debug.Log($"[EnemyBeamBullet] UpdateEnemyTrackedSegments: 追従用Raycastが何にも当たらなかった。seg.start={seg.start}, targetPos={targetT.position}, dir={dir}, 旧end={seg.end}（更新せず維持）");
                }
                continue;
            }

            EnemyPart hitPart = hit.collider.GetComponent<EnemyPart>();
            EnemyDamageReceiver hitReceiver = hitPart == null ? GetEnemyDamageReceiver(hit.collider) : null;

            bool sameTarget = (seg.terminalEnemyPart != null && hitPart == seg.terminalEnemyPart)
                            || (seg.terminalEnemyReceiver != null && hitReceiver == seg.terminalEnemyReceiver);
            if (!sameTarget)
            {
                if (showDebugLog)
                {
                    Debug.Log($"[EnemyBeamBullet] UpdateEnemyTrackedSegments: 追従用Raycastが別のcolliderに当たった。collider={hit.collider.gameObject.name}, seg.start={seg.start}, targetPos={targetT.position}, dir={dir}, 旧end={seg.end}（更新せず維持）");
                }
                continue; // 別の何かが手前を塞いでいる等、今回は追従できない
            }

            seg.end = hit.point;
            if (seg.line != null) seg.line.SetPosition(1, seg.end);
            UpdateSparkShape(seg, seg.start, seg.end);
        }

        ReturnSegmentSnapshot(snapshot);
    }

    // 反射後、何にも当たらず最大距離まで伸びきったセグメント(isOpenEnd)の経路に、後からEnemyやBlockが
    // 移動してきて重なった場合、そこでセグメントを打ち切る。ダメージ自体はこの後に走るTickReflectedSegmentが
    // 打ち切り後のセグメントを見て入れるので、ここでは見た目の終端位置と追従対象の設定だけを行う
    private void CheckOpenSegmentsForNewHit()
    {
        var snapshot = RentSegmentSnapshot();

        foreach (BeamSegment seg in snapshot)
        {
            if (!segments.Contains(seg)) continue;
            if (seg.growing) continue; // 先端が届くまでは対象外
            if (!seg.isOpenEnd || !seg.isReflected) continue;

            Vector2 full = seg.end - seg.start;
            float len = full.magnitude;
            if (len < 0.01f) continue;
            Vector2 dir = full / len;

            RaycastHit2D[] hits = Physics2D.RaycastAll(seg.start, dir, len, hitLayers);
            if (hits.Length > 1) System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (int h = 0; h < hits.Length; h++)
            {
                Collider2D col = hits[h].collider;
                if (col == null) continue;

                WallHealth hitWall = GetWallHealth(col);
                if (hitWall != null)
                {
                    seg.isOpenEnd = false;
                    seg.end = hits[h].point;
                    if (seg.line != null) seg.line.SetPosition(1, seg.end);
                    UpdateSparkShape(seg, seg.start, seg.end);
                    seg.terminalWall = hitWall;
                    seg.hadTerminalWall = true;
                    break;
                }

                EnemyPart hitPart = col.GetComponent<EnemyPart>();
                EnemyDamageReceiver hitReceiver = hitPart == null ? GetEnemyDamageReceiver(col) : null;
                if (hitPart == null && hitReceiver == null) continue;

                if (showDebugLog)
                {
                    Debug.Log($"[EnemyBeamBullet] CheckOpenSegmentsForNewHit: 伸びきったセグメントの経路上でEnemyを検出、打ち切ります。collider={col.gameObject.name}, 旧end={seg.end}, 新end={hits[h].point}, start={seg.start}, dir={dir}");
                }

                seg.isOpenEnd = false;
                seg.end = hits[h].point;
                if (seg.line != null) seg.line.SetPosition(1, seg.end);
                UpdateSparkShape(seg, seg.start, seg.end);
                seg.terminalEnemyPart = hitPart;
                seg.terminalEnemyReceiver = hitReceiver;
                seg.hadTerminalEnemy = true;
                break;
            }
        }

        ReturnSegmentSnapshot(snapshot);
    }

    // Playerに当たって終端した未反射セグメントを、Playerの現在位置に追従させる。考え方はUpdateWallTrackedSegmentsと同じ。
    // Playerは基本破壊されないが、念のため対象が消えた場合は同じ方向へビームを延長する
    private void UpdatePlayerTrackedSegments()
    {
        var snapshot = RentSegmentSnapshot();

        foreach (BeamSegment seg in snapshot)
        {
            if (!segments.Contains(seg)) continue;
            if (seg.growing) continue; // 先端が届くまでは対象外

            if (seg.hadTerminalPlayer && seg.terminalPlayer == null)
            {
                seg.hadTerminalPlayer = false; // 一度だけ処理する
                ContinueSegmentPastGone(seg);
                continue;
            }

            if (seg.terminalPlayer == null) continue; // 追従対象が無い

            Vector2 toTarget = (Vector2)seg.terminalPlayer.transform.position - (Vector2)seg.start;
            if (toTarget.sqrMagnitude < 0.0001f) continue;
            Vector2 dir = toTarget.normalized;

            RaycastHit2D hit = Physics2D.Raycast(seg.start, dir, maxSegmentDistance, hitLayers);
            if (hit.collider == null) continue;
            if (hit.collider.GetComponent<PixelDancerController>() != seg.terminalPlayer)
            {
                // 別の何かが手前を塞いでいる等、今回は追従できない
                continue;
            }

            seg.end = hit.point;
            if (seg.line != null) seg.line.SetPosition(1, seg.end);
            UpdateSparkShape(seg, seg.start, seg.end);
        }

        ReturnSegmentSnapshot(snapshot);
    }

    // 反射のきっかけとなった線(PaddleDot)が消えたセグメントを検知し、反射前の状態（本来の進行方向=segDir）に
    // 戻して伸び直す。反射先に生えていた古いセグメント連鎖は丸ごと削除してから作り直す
    private void UpdateReflectionSourceDots()
    {
        var snapshot = RentSegmentSnapshot();

        foreach (BeamSegment seg in snapshot)
        {
            if (!segments.Contains(seg)) continue;
            if (!seg.hadReflectionSourceDot || seg.reflectionSourceDot != null) continue;

            // 線が消えた：反射前の方向(segDir)に戻って続きを伸ばす
            seg.hadReflectionSourceDot = false; // 一度だけ処理する

            if (seg.next != null)
            {
                RemoveChainFrom(seg.next);
                seg.next = null;
            }

            ContinueSegmentPastGone(seg);
        }

        ReturnSegmentSnapshot(snapshot);
    }

    // 敵の線（EnemyLineReflector）で反射していたセグメントの線が消えたら、反射前の方向(segDir)に戻って続きを伸ばす
    //（UpdateReflectionSourceDotsと同じ考え方。Area10最終ボスNeonDancerの後半のみ）
    private void UpdateReflectionSourceEnemyLines()
    {
        var snapshot = RentSegmentSnapshot();
        foreach (BeamSegment seg in snapshot)
        {
            if (!segments.Contains(seg)) continue;
            if (!seg.hadReflectionSourceEnemyLine) continue;
            if (seg.reflectionSourceEnemyLine != null && seg.reflectionSourceEnemyLine.IsSolid && seg.reflectionSourceEnemyLine.isActiveAndEnabled) continue;

            seg.hadReflectionSourceEnemyLine = false; // 一度だけ処理する
            seg.reflectionSourceEnemyLine = null;
            if (seg.next != null)
            {
                RemoveChainFrom(seg.next);
                seg.next = null;
            }
            ContinueSegmentPastGone(seg);
            if (this == null) return;
        }

        ReturnSegmentSnapshot(snapshot);
    }

    // 反射済みのセグメント上に後から敵の線が引かれたら、そこで反射して未反射に戻す
    //（CheckForNewReflectionsと同じ考え方。Area10最終ボスNeonDancerの後半のみ）
    private void CheckForNewEnemyLines()
    {
        var snapshot = RentSegmentSnapshot();
        foreach (BeamSegment seg in snapshot)
        {
            if (this == null) return;
            if (!segments.Contains(seg) || seg.growing || !seg.isReflected || seg.hadReflectionSourceEnemyLine) continue;

            Vector2 full = seg.end - seg.start;
            float len = full.magnitude;
            if (len < 0.01f) continue;
            Vector2 dir = full / len;

            RaycastHit2D[] hits = Physics2D.RaycastAll(seg.start, dir, len, hitLayers);
            if (hits.Length > 1) System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int h = 0; h < hits.Length; h++)
            {
                if (hits[h].collider == null || hits[h].distance < 0.001f) continue;
                EnemyLineReflector line = hits[h].collider.GetComponent<EnemyLineReflector>();
                if (line == null || !line.IsSolid) continue;

                if (seg.next != null)
                {
                    RemoveChainFrom(seg.next);
                    seg.next = null;
                }
                seg.end = hits[h].point;
                if (seg.line != null) seg.line.SetPosition(1, seg.end);
                UpdateSparkShape(seg, seg.start, seg.end);
                // 元々の終端（敵・ブロック・プレイヤー・線・反射壁）の記録は、追従処理が終点を戻さないよう消す
                seg.terminalWall = null; seg.hadTerminalWall = false;
                seg.terminalEnemyPart = null; seg.terminalEnemyReceiver = null; seg.hadTerminalEnemy = false;
                seg.terminalPlayer = null; seg.hadTerminalPlayer = false;
                seg.reflectionSourceDot = null; seg.hadReflectionSourceDot = false; seg.reflectionSourceDotGeneration = -1;
                seg.reflectedOffReflector = null;
                seg.isOpenEnd = false;
                seg.reflectionSourceEnemyLine = line;
                seg.hadReflectionSourceEnemyLine = true;
                damageMultiplier = 1f;
                ResetA8ForEnemyLine();

                Vector2 reflectDir = Vector2.Reflect(dir, hits[h].normal).normalized;
                line.NotifyBeamReflected(seg.end, reflectDir); // 反射のSE・エフェクト（NeonDancer側で再生）
                StartFront(seg.end, reflectDir, false, hits[h].collider, seg); // ★先端方式
                if (this == null) return;
                break;
            }
        }

        ReturnSegmentSnapshot(snapshot);
    }

    // 反射している線に、Enemy/Blockと同じように接している間ずっとVFXを繰り返し再生する
    private void TickPaddleReflectionVfx()
    {
        foreach (BeamSegment seg in segments)
        {
            if (seg.reflectionSourceDot == null) continue; // まだ反射していない、または線が既に消えた
            // ヒットのたびに反射SEも鳴らす（ビームを反射している間だけ。同じフレームの重複は1回にまとめる）
            Stroke srcStroke = seg.reflectionSourceDot.ParentStroke;
            PaddleDrawer.Instance?.PlayPaddleHitSE(srcStroke != null ? srcStroke.Type : PaddleDot.LineType.Normal, IsJustReflected);
            // 新しい線反射の演出（ReflectedBulletFXManager）を、ヒットのたびに出す（旧VFXの代わり）
            if (ReflectedBulletFXManager.HandlesBeamReflect)
            {
                Vector3 nd = seg.next != null ? seg.next.end - seg.next.start : seg.end - seg.start;
                seg.reflectTickCount++; // 1回目の反射（0）→ヒットのたびに1ずつ増え、演出が少しずつ大きくなる
                ReflectedBulletFXManager.NotifyBeamReflectTick(seg.end, seg.reflectionSourceDot, IsJustReflected, nd, seg.reflectTickCount);
            }
            else TrySpawnPaddleHitVfx(seg.end);
        }
    }

    // BeamReflector（Obelisk本体等）に接している間ずっとVFXを繰り返し再生する（PaddleDotと同じ考え方）
    private void TickBeamReflectorVfx()
    {
        foreach (BeamSegment seg in segments)
        {
            if (seg.reflectedOffReflector == null) continue;
            seg.reflectedOffReflector.PlayReflectVfx(seg.end);
        }
    }

    private void TickUnreflectedSegment(BeamSegment seg, int baseDamage)
    {
        // seg.endは発射時にPlayer/Floor表面ぴったりの1点で固定されている。Playerがその境界付近に
        // 重なった場合、終点ちょうどでの判定は不安定になり得るため、判定用の終点だけわずかに先まで
        // 伸ばす（見た目のセグメント自体・seg.endは変更しない）
        Vector3 tickDir = seg.end - seg.start;
        Vector3 tickEnd = (tickDir.sqrMagnitude > 0.0001f) ? seg.end + tickDir.normalized * 0.1f : seg.end;
        RaycastHit2D[] hits = Physics2D.LinecastAll(seg.start, tickEnd, hitLayers);

        if (showDebugLog)
        {
            Debug.Log($"[EnemyBeamBullet] TickUnreflectedSegment: {hits.Length}件のヒット (start={seg.start}, end={seg.end})");

            bool foundFloorThisTick = false;
            for (int di = 0; di < hits.Length; di++)
            {
                if (hits[di].collider != null && hits[di].collider.GetComponent<FloorHealth>() != null) { foundFloorThisTick = true; break; }
            }
            if (!foundFloorThisTick)
            {
                FloorHealth[] floors = FindObjectsByType<FloorHealth>(FindObjectsSortMode.None);
                foreach (FloorHealth fh in floors)
                {
                    Collider2D fc = fh.GetComponent<Collider2D>();
                    if (fc == null) continue;
                    Debug.Log($"[EnemyBeamBullet]   [診断] FloorHealth({fh.gameObject.name}) collider.bounds={fc.bounds}, enabled={fc.enabled}, layer={fh.gameObject.layer}, seg.start={seg.start}, seg.end={seg.end}");
                }
                PixelDancerController[] players = FindObjectsByType<PixelDancerController>(FindObjectsSortMode.None);
                foreach (PixelDancerController p in players)
                {
                    Collider2D pc = p.GetComponent<Collider2D>();
                    Debug.Log($"[EnemyBeamBullet]   [診断] PixelDancer position={p.transform.position}, collider.bounds={(pc != null ? pc.bounds.ToString() : "null")}, seg.endまでの距離={Vector3.Distance(p.transform.position, seg.end):F3}");
                }
            }
        }

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i].collider;
            if (col == null) continue;

            if (showDebugLog)
            {
                Debug.Log($"[EnemyBeamBullet]   hit[{i}]: collider={col.gameObject.name}, layer={col.gameObject.layer}, hasPlayer={col.GetComponent<PixelDancerController>() != null}, hasFloor={col.GetComponent<FloorHealth>() != null}");
            }

            PixelDancerController player = col.GetComponent<PixelDancerController>();
            if (player != null)
            {
                PlayerHitFXManager.NotifyHitPoint(hits[i].point, true); // 被弾演出をビームが当たった位置に出す
                player.ApplyBeamDamage(baseDamage);
                continue;
            }

            FloorHealth floor = col.GetComponent<FloorHealth>();
            if (floor != null)
            {
                PlayerHitFXManager.NotifyHitPoint(hits[i].point, false); // 被弾演出をビームが当たった位置に出す（無いと床の中心・画像の上端に出る）
                bool applied = floor.ApplyBeamDamage(baseDamage);
                if (applied) TrySpawnFloorHitVfx(hits[i].point);
                if (showDebugLog) Debug.Log($"[EnemyBeamBullet]   -> FloorHealth.ApplyBeamDamage結果: {applied}");
            }
        }
    }

    // A8スキル「反射弾が敵に当たるたびダメージが増加」をBeamにも適用する。
    // EnemyBullet.RegisterEnemyHitAsBounceと同じ考え方（同フレーム多重ヒット防止・最大加算回数まで）。
    // 加算後のcurrentDamageは以降の全Tick（Wall/Enemy問わず）に引き継がれる
    // 敵の線（EnemyLineReflector）で未反射に戻った時：A8で増えた基礎ダメージを取り消す（弾と同じ仕様。Area10最終ボスNeonDancerの後半のみ）
    private void ResetA8ForEnemyLine()
    {
        currentDamage = bulletType != null ? bulletType.damage : currentDamage;
        a8EnemyHitCount = 0;
        lastA8HitFrame = -999;
    }

    private void RegisterA8EnemyHit()
    {
        if (a8MaxAdditions <= 0 || a8EnemyHitCount >= a8MaxAdditions) return;
        if (Time.frameCount == lastA8HitFrame) return;

        lastA8HitFrame = Time.frameCount;
        a8EnemyHitCount++;
        currentDamage = 1f + a8EnemyHitCount * a8DamagePerHit;
    }

    private void TickReflectedSegment(BeamSegment seg, int baseDamage)
    {
        // seg.endはBeamReflector等のコライダー境界ぴったりの1点で固定されている。TickUnreflectedSegmentと同じ理由で、
        // 境界ちょうどでの判定は不安定になり得るため、判定用の終点だけわずかに先まで伸ばす（見た目のセグメント自体・seg.endは変更しない）
        Vector3 tickDir = seg.end - seg.start;
        Vector3 tickEnd = (tickDir.sqrMagnitude > 0.0001f) ? seg.end + tickDir.normalized * 0.1f : seg.end;
        RaycastHit2D[] hits = Physics2D.LinecastAll(seg.start, tickEnd, hitLayers);
        // ★負荷軽減：毎回newせず使い回す（セグメントごとに順番に呼ばれ、入れ子にはならない）
        HashSet<Object> hitThisTick = _hitThisTickScratch;
        hitThisTick.Clear();

        bool isTrackedEnemySeg = seg.terminalEnemyPart != null || seg.terminalEnemyReceiver != null;
        if (showDebugLog && (hits.Length > 0 || isTrackedEnemySeg))
        {
            Debug.Log($"[EnemyBeamBullet] TickReflectedSegment: {hits.Length}件のヒット (start={seg.start}, end={seg.end}, isTrackedEnemySeg={isTrackedEnemySeg})");
        }

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D col = hits[i].collider;
            if (col == null) continue;

            if (showDebugLog)
            {
                Debug.Log($"[EnemyBeamBullet]   hit[{i}]: collider={col.gameObject.name}, hasEnemyPart={col.GetComponent<EnemyPart>() != null}, hasEnemyDamageReceiver(walk-up)={GetEnemyDamageReceiver(col) != null}");
            }

            // Block（WallHealth）。終端したセグメントがそのまま当たり続けている間、継続してダメージを入れる
            WallHealth wall = GetWallHealth(col);
            if (wall != null)
            {
                if (!hitThisTick.Add(wall)) continue; // 同一セグメント内、多重ヒット防止
                wall.ApplyBeamDamage(false, damageMultiplier > 1.0001f, hits[i].point);
                // ★再入対策：発射元自身のWallHealthを破壊させ、それが自分自身のDestroy()を
                // 引き起こすことがある（円で反射させた自弾が発射元に命中するケース等）。
                // Destroy()は即座には反映されないため、同一セグメント内の残りのhitsも処理し続けてしまう
                if (this == null) return;
                continue;
            }

            // WeakPoint Systemが有効な敵は、本体ではなく子のEnemyPartがダメージを受ける
            EnemyPart part = col.GetComponent<EnemyPart>();
            if (part != null)
            {
                if (!hitThisTick.Add(part)) continue; // 同一セグメント内、複数パーツの多重ヒット防止
                bool newHitFx = ReflectedBulletFXManager.BeginExternalEnemyHit(); // 旧の敵ヒットVFXを止め、ダメージが入ったら新しい演出を出す
                bool applied = part.TryApplyExternalReflectedDamage(baseDamage, damageMultiplier, hits[i].point);
                if (applied) { PlayBeamEnemyHitFx(seg, hits[i].point, newHitFx); RegisterA8EnemyHit(); }
                if (showDebugLog) Debug.Log($"[EnemyBeamBullet]   -> EnemyPart.TryApplyExternalReflectedDamage結果: {applied}");
                continue;
            }

            EnemyDamageReceiver receiver = GetEnemyDamageReceiver(col);
            if (receiver == null) continue;
            if (!hitThisTick.Add(receiver)) continue; // 同一セグメント内、複数パーツの多重ヒット防止

            bool newHitFx2 = ReflectedBulletFXManager.BeginExternalEnemyHit();
            bool result = receiver.TryApplyExternalReflectedDamage(baseDamage, damageMultiplier, hits[i].point);
            if (result) { PlayBeamEnemyHitFx(seg, hits[i].point, newHitFx2); RegisterA8EnemyHit(); }
            if (showDebugLog) Debug.Log($"[EnemyBeamBullet]   -> EnemyDamageReceiver.TryApplyExternalReflectedDamage結果: {result}");
        }

        if (showDebugLog)
        {
            bool foundEnemyThisTick = false;
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider == null) continue;
                if (hits[i].collider.GetComponent<EnemyPart>() != null || GetEnemyDamageReceiver(hits[i].collider) != null)
                {
                    foundEnemyThisTick = true;
                    break;
                }
            }
            if (!foundEnemyThisTick)
            {
                LogAllEnemyColliderBounds(seg);
            }
        }
    }

    // BulletTypeのenemyHitVfxPrefabをEnemyへのヒット位置に再生する（既存弾のEnemyBulletFeedback.TrySpawnEnemyHitVfxと同じ考え方）
    // 反射したビームがエネミーに当たった時：新しい演出（ReflectedBulletFXManager）か、従来のEnemy Hit Vfx
    private void PlayBeamEnemyHitFx(BeamSegment seg, Vector3 pos, bool newFx)
    {
        if (!newFx) { TrySpawnEnemyHitVfx(pos); return; }
        Vector3 d = seg != null ? seg.end - seg.start : Vector3.up;
        Vector2 dir = d.sqrMagnitude > 0.0001f ? (Vector2)d.normalized : Vector2.up;
        // ビーム本体（先端の光など）に隠れないよう、ビームより手前の並び順にする
        int layer = seg != null && seg.line != null ? seg.line.sortingLayerID : SortingLayer.NameToID("Default");
        int order = ReflectedBulletFXManager.BeamFxSortingOrder;
        ReflectedBulletFXManager.PlayExternalEnemyHit(pos, damageMultiplier > 1.0001f, dir, layer, order);
    }

    private void TrySpawnEnemyHitVfx(Vector3 pos)
    {
        if (bulletType == null || bulletType.enemyHitVfxPrefab == null) return;
        GameObject vfx = Instantiate(bulletType.enemyHitVfxPrefab, pos, Quaternion.identity);
        Destroy(vfx, 1f);
    }

    // BulletTypeのpaddleHitVfxPrefabを線への反射位置に再生する（既存弾のEnemyBulletFeedback.TrySpawnPaddleHitVfxと同じ考え方。
    // PaddleDot.EvaluateExternalHit自体が鳴らす汎用のヒットSE/VFXとは別に、BulletTypeごとの追加演出として再生する）
    private void TrySpawnPaddleHitVfx(Vector3 pos)
    {
        if (ReflectedBulletFXManager.HandlesBeamReflect) return; // 線反射は新しい演出（ReflectedBulletFXManager）を出すので旧VFXは出さない
        if (bulletType == null || bulletType.paddleHitVfxPrefab == null) return;
        GameObject vfx = Instantiate(bulletType.paddleHitVfxPrefab, pos, Quaternion.identity);
        Destroy(vfx, 1f);
    }

    // BulletTypeのfloorHitVfxPrefabをFloorへのヒット位置に再生する
    private void TrySpawnFloorHitVfx(Vector3 pos)
    {
        if (bulletType == null || bulletType.floorHitVfxPrefab == null) return;
        GameObject vfx = Instantiate(bulletType.floorHitVfxPrefab, pos, Quaternion.identity);
        Destroy(vfx, 1f);
    }

    // デバッグ用：反射セグメントがエネミーに命中しなかった時、シーン内の全エネミーColliderの実際のワールド座標bounds を出力する
    // セグメントのstart/endと直接見比べて、幾何学的に交差しているかを確認するため
    private void LogAllEnemyColliderBounds(BeamSegment seg)
    {
        EnemyPart[] parts = FindObjectsByType<EnemyPart>(FindObjectsSortMode.None);
        foreach (EnemyPart part in parts)
        {
            Collider2D col = part.GetComponent<Collider2D>();
            if (col == null) continue;
            Debug.Log($"[EnemyBeamBullet]   [診断] EnemyPart({part.gameObject.name}) collider.bounds={col.bounds}, enabled={col.enabled}, layer={col.gameObject.layer}, セグメントstart={seg.start}, end={seg.end}");
        }

        EnemyDamageReceiver[] receivers = FindObjectsByType<EnemyDamageReceiver>(FindObjectsSortMode.None);
        foreach (EnemyDamageReceiver rcv in receivers)
        {
            Collider2D col = rcv.GetComponent<Collider2D>();
            if (col == null) continue;
            Debug.Log($"[EnemyBeamBullet]   [診断] EnemyDamageReceiver({rcv.gameObject.name}) collider.bounds={col.bounds}, enabled={col.enabled}, layer={col.gameObject.layer}, セグメントstart={seg.start}, end={seg.end}");
        }
    }

    // デバッグ用：反射セグメントが何にも当たらず素通りした時、シーン内の全WallHealth(Block)Colliderの
    // 実際のワールド座標bounds・layerを出力する。hitLayersのlayer漏れ（Enemyの時と同じ現象）を疑うため
    private void LogAllWallHealthColliderInfo(Vector3 segStart, Vector2 dir)
    {
        WallHealth[] walls = FindObjectsByType<WallHealth>(FindObjectsSortMode.None);
        foreach (WallHealth wall in walls)
        {
            Collider2D col = wall.GetComponent<Collider2D>();
            if (col == null) continue;
            Debug.Log($"[EnemyBeamBullet]   [診断] WallHealth({wall.gameObject.name}) collider.bounds={col.bounds}, enabled={col.enabled}, layer={col.gameObject.layer}, hitLayers含む={((hitLayers.value & (1 << col.gameObject.layer)) != 0)}, segStart={segStart}, dir={dir}");
        }
    }

    // =========================================================
    // 寿命
    // =========================================================
    private IEnumerator LifeRoutine(float duration)
    {
        float t = 0f;
        while (t < duration + extraLifeSeconds)
        {
            t += Time.deltaTime * GetTimeScale();
            yield return null;
        }

        // 火花の新規発生は止める（発生済みの粒子は自身の寿命で自然にフェードアウトする）
        foreach (BeamSegment seg in segments)
        {
            if (seg.spark != null) seg.spark.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        float fadeDuration = (bulletType != null && bulletType.beamFadeOutDuration > 0f) ? bulletType.beamFadeOutDuration : 0.15f;
        float ft = 0f;
        while (ft < fadeDuration)
        {
            ft += Time.deltaTime * GetTimeScale();
            float alphaMul = 1f - Mathf.Clamp01(ft / fadeDuration);

            foreach (BeamSegment seg in segments)
            {
                seg.fadeAlphaMul = alphaMul; // beamPulseEnabled時はLateUpdateがこの値を見て計算するので、ここでは書き込むだけ

                if (seg.line == null || seg.baseAlphaKeys == null) continue;
                if (bulletType != null && bulletType.beamPulseEnabled) continue; // colorGradientの書き込み元をLateUpdateに一本化（競合防止）

                // ★負荷軽減：セグメントごとに配列・Gradientを使い回す（LineRendererは代入時に中身を写し取るため共有して問題ない）
                if (seg.fadeAlphaKeysScratch == null || seg.fadeAlphaKeysScratch.Length != seg.baseAlphaKeys.Length)
                    seg.fadeAlphaKeysScratch = new GradientAlphaKey[seg.baseAlphaKeys.Length];
                GradientAlphaKey[] fadedAlphaKeys = seg.fadeAlphaKeysScratch;
                for (int i = 0; i < fadedAlphaKeys.Length; i++)
                {
                    fadedAlphaKeys[i] = new GradientAlphaKey(seg.baseAlphaKeys[i].alpha * alphaMul, seg.baseAlphaKeys[i].time);
                }

                if (seg.fadeGradient == null) seg.fadeGradient = new Gradient();
                Gradient g = seg.fadeGradient;
                g.SetKeys(seg.baseColorKeys, fadedAlphaKeys);
                seg.line.colorGradient = g;
            }

            yield return null;
        }

        Destroy(gameObject);
    }
}
