using UnityEngine;

/// <summary>
/// 通称「ドリル反射」弾専用コンポーネント。線に当たった瞬間には反射せず、その場に留まって
/// 一定間隔(Hit Interval)で当たり判定を繰り返す。規定回数(Required Hits)のヒットが累計で
/// 貯まった時だけ、通常の反射処理（PaddleDot側）に進む。
/// 規定回数に届く前に線が消えた場合は、そのまま元の速度・方向で直進を継続する
/// （累計ヒット数は保持され、次に当たった別の線に引き継がれる）。
/// 反射が成立した瞬間は累計ヒット数だけ0にリセットされ、次に当たった敵・ブロックに対しても
/// 同じ多段ヒット挙動を繰り返す（＝反射しても特性そのものは引き継がれる）。
///
/// 留まっている間は弾自身のCollider2Dを無効化する（当たり判定を無くす）ことで、
/// EnemyBullet.prefab・PaddleDot.prefab双方に設定された反発係数1の物理マテリアルによる
/// 弾かれを構造的に発生させない。留まっている間の位置はRigidbody2D.positionを直接指定して
/// 完全に固定し（速度は使わない）、見た目の「めり込み」もこの固定位置への一方向オフセットで
/// 表現する。線(セグメント)の消滅判定は、親のStroke単位ではなく実際に触れているPaddleDot
/// （個々のセグメント）自身の生死で行う（1本の線を構成する各セグメントは、親Strokeより
/// 先に個別に破棄されうるため）。
/// </summary>
public class PinnedReflectBullet : MonoBehaviour
{
    [SerializeField] private int requiredHits = 5;
    [SerializeField] private float hitInterval = 0.2f;
    [SerializeField] private bool spinWhilePinned = true;
    [SerializeField] private float spinSpeed = 720f;
    [Tooltip("留まっている間、見た目上どれだけ線にめり込んでいくかの速さ（Unity単位/秒）")]
    [SerializeField] private float creepSpeed = 0.15f;
    [Tooltip("見た目のめり込み量の最大値（Unity単位）。これ以上は深く沈み込まない")]
    [SerializeField] private float maxCreepVisualDepth = 0.3f;

    // Collider再有効化前に、周囲の別セグメントから確実に離れるための退避距離（Unity単位）。
    // ★反射成立時：かつて0.5では隣接セグメントに引っかかり、Colliderが再有効化された瞬間に
    //   Unity物理エンジンの反発係数1による弾き返しが再度発生する不具合があり、1.5まで上げて
    //   解消した。その後、方向符号の修正など他の不具合修正を経て、改めて1.5→1→0.5の順で
    //   実機確認したところ引っかかりは再発しなかったため、コマ飛び感を抑えるため最終的に0.5に確定
    private const float ReflectClearanceDistance = 0.5f;

    // ★直進再開時（線が消えて反射せず直進する場合）に使う退避距離。反射成立時と同じ0.5だが、
    //   目的（引っかかり回避）が異なるため独立した定数として分離している
    private const float UnpinClearanceDistance = 0.5f;

    private Rigidbody2D rb;
    private Collider2D col;

    private int currentHits;
    private bool isPinned;
    private float pinnedTimer;
    private float pinnedElapsed;
    private Vector2 pinnedDirection;
    private Vector2 pinnedBasePosition;
    private Stroke pinnedStroke;

    // 反射成立時に呼び戻すためのコンテキスト（TryPinを呼んだ瞬間の情報をそのまま保持する）
    private PaddleDot pinnedDot;
    private Collider2D pinnedDotCollider;
    private EnemyBullet pinnedBullet;
    private Vector2 pinnedNormal;
    private Vector3 pinnedHitPos;

    // ★Just判定は「最初にこの弾を線が捕まえた瞬間」を基準に一度だけ確定させ、規定回数の
    //   ヒットを溜め終えて実際に反射するまで（複数の線をまたいだ場合も含め）保持し続ける。
    //   反射確定のタイミングでbornTime基準の判定をやり直すと、既に猶予時間を過ぎてしまって
    //   おりJust成立がほぼ不可能になる不具合があったための対策
    private bool hasCapturedJust;
    private bool pinnedIsJust;

    // ★1本の線は密集した複数のPaddleDotで構成されており、新しい線に当たった瞬間、
    //   隣接する複数のセグメントが同じフレーム内でほぼ同時に衝突検知されることがある。
    //   反射が成立した（currentHitsが規定回数へ到達した）フレームを記録しておき、
    //   同じフレーム内で他の隣接セグメントからTryPinが呼ばれても新規ピン留めをせず、
    //   PaddleDot側の同フレーム多段反射防止(TryAcquirePaddleReflectThisFrame)に処理を委ねる。
    //   これが無いと、成立した反射を隣接セグメントが同フレームで即座に上書きしてしまっていた
    private int lastReflectFrame = -1;

    // ★Physics2D.IgnoreCollisionが期待通りに機能しない（Collider2Dの有効/無効切り替えや
    //   レイヤー変更のタイミングと絡んで、設定したはずの無視が効かないケースがあった）ため、
    //   物理エンジンに依存しない、ゲームロジック側の確実なクールダウンで代替する。
    //   直前まで留まっていた特定のセグメントについては、離れてから一定時間はTryPin自体を
    //   素通りさせ、再ピン留め・再反射を一切発生させない
    private const float RecentDotCooldownSeconds = 0.25f;
    private PaddleDot recentlyLeftDot;
    private float recentlyLeftDotCooldownUntil;

    // ★反射した弾が敵/シールドに当たった場合も、線と同じ「めり込みながら規定回数ヒット」を
    //   引き継ぐ。敵の場合は反射(跳ね返り)ではなく、規定回数に達したら弾自体を消滅させる。
    //   ダメージ適用処理はEnemyDamageReceiver（WeakPoint System OFF時）とEnemyPart（ON時）の
    //   どちらからも呼ばれうるため、具体的な型に依存せずコールバック(Action)で受け取る。
    //   生死判定用に呼び出し元コンポーネント自体もUnityEngine.Objectとして保持する
    //   （Unityの「破棄されたオブジェクトはnull判定になる」仕組みをそのまま利用するため）
    private bool pinnedToEnemyMode;
    private UnityEngine.Object pinnedEnemyTarget;
    private System.Action<float, float, Vector3> pinnedEnemyDamageCallback;
    private float pinnedEnemyDamage;
    private float pinnedEnemyDamageMultiplier;

    /// <summary>現在「その場に留まっている」状態かどうか。外部から参照用</summary>
    public bool IsPinned => isPinned;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
    }

    /// <summary>
    /// EnemyShooter側から発射直後に呼ぶ。EnemyData.BulletTypeの設定値をそのまま渡す想定。
    /// </summary>
    public void Configure(int requiredHits, float hitInterval, bool spinWhilePinned, float spinSpeed, float creepSpeed)
    {
        this.requiredHits = Mathf.Max(1, requiredHits);
        this.hitInterval = Mathf.Max(0.01f, hitInterval);
        this.spinWhilePinned = spinWhilePinned;
        this.spinSpeed = spinSpeed;
        this.creepSpeed = Mathf.Max(0.001f, creepSpeed);
    }

    /// <summary>
    /// PaddleDot.OnCollisionEnter2D側から、通常の反射処理に入る前に呼ぶ。
    /// true を返した場合、呼び出し元は通常の反射処理を一切行わず、このフレームはここで終える
    /// （まだ留まっている最中、または今回の接触で新たに留まり始めたケース）。
    /// false を返した場合は既に規定回数へ到達済みという意味なので、呼び出し元は通常通り
    /// 反射処理を進めてよい（このタイミングで累計ヒット数は0にリセットされる）。
    /// </summary>
    public bool TryPin(PaddleDot dot, Stroke stroke, EnemyBullet bullet, Rigidbody2D bulletRb, Vector2 normal, Vector3 hitPos)
    {
        if (isPinned) return true; // 既に留まっている間の重複コールは無視（タイマー側で進行を管理する）

        // 同フレーム内で既に反射が成立済みなら、隣接する別セグメントからの呼び出しであっても
        // 新規ピン留めはしない。PaddleDot側の同フレーム多段反射防止に処理を任せる
        if (Time.frameCount == lastReflectFrame) return false;

        // 直前まで留まっていたのと同じセグメントには、クールダウン中は一切反応しない
        // （Physics2D.IgnoreCollisionが効かない場合の保険。trueを返し、通常反射も含め何もしない）
        if (dot != null && dot == recentlyLeftDot && Time.time < recentlyLeftDotCooldownUntil)
        {
            return true;
        }

        currentHits++;

        pinnedDot = dot;
        pinnedDotCollider = dot != null ? dot.GetComponent<Collider2D>() : null;
        pinnedBullet = bullet;
        pinnedNormal = normal;
        pinnedHitPos = hitPos;
        pinnedStroke = stroke;

        if (currentHits >= requiredHits)
        {
            // 今回の接触そのもので規定回数へ到達（Required Hits=1等）：留まらずそのまま反射させる
            currentHits = 0;
            lastReflectFrame = Time.frameCount;
            return false;
        }

        // ここに到達した時点で「反射には至らない中間ヒット」が1回確定したのでSE/VFXを鳴らす
        // （最終的に反射する回のSE/VFXはPerformPinnedReflect側で既存の通常反射と同じ経路で鳴る）
        dot?.PlayPinnedHitTick(bullet, hitPos);

        // ★Just判定は「最初にこの弾を線が捕まえた瞬間」だけで一度確定させ、以後は上書きしない
        //   （複数の線をまたいで累計する場合も、最初に捕まった瞬間の成否を維持する）
        if (!hasCapturedJust)
        {
            hasCapturedJust = true;
            pinnedIsJust = dot != null && dot.EvaluateJustNow();
        }

        // 衝突法線（normal）は物理エンジンの反射処理と無関係な幾何学的情報であり、
        // ここから直接「壁にめり込む方向」（＝元の進行方向）を求める（実測で符号を確認済み）
        pinnedDirection = (normal.sqrMagnitude > 0.0001f)
            ? normal.normalized
            : (Vector2)transform.up;
        pinnedBasePosition = (rb != null) ? rb.position : (Vector2)transform.position;
        pinnedElapsed = 0f;

        isPinned = true;
        pinnedTimer = hitInterval;

        // 留まっている間、当たり判定そのものを無くす。物理マテリアルの反発係数に関わらず、
        // 押し返し・反発が原理的に発生しなくなる
        if (col != null) col.enabled = false;

        return true;
    }

    /// <summary>
    /// EnemyDamageReceiver / EnemyPart のOnCollisionEnter2D側から、通常の1回ダメージ処理に
    /// 入る前に呼ぶ。true を返した場合、呼び出し元は通常のダメージ処理を一切行わず、この
    /// フレームはここで終える（初回ヒット分のダメージは本メソッド内で既に適用済み）。
    /// false を返した場合は既に規定回数へ到達済みという意味なので、呼び出し元は通常通り
    /// ダメージ処理（1回分）を行い、必要なら弾を破棄してよい。
    /// </summary>
    /// <param name="target">留まり中に対象が破棄されたかどうかの判定に使う呼び出し元コンポーネント自身</param>
    /// <param name="applyDamage">1ヒット分のダメージ適用処理（baseDamage, damageMultiplier, hitPos）</param>
    public bool TryPinToEnemy(UnityEngine.Object target, System.Action<float, float, Vector3> applyDamage, EnemyBullet bullet, Vector2 normal, Vector3 hitPos, float damage, float damageMultiplier)
    {
        if (isPinned) return true; // 既に留まっている間の重複コールは無視（タイマー側で進行を管理する）
        if (Time.frameCount == lastReflectFrame) return false;

        currentHits++;

        pinnedToEnemyMode = true;
        pinnedDot = null;
        pinnedDotCollider = null;
        pinnedStroke = null;
        pinnedBullet = bullet;
        pinnedNormal = normal;
        pinnedHitPos = hitPos;
        pinnedEnemyTarget = target;
        pinnedEnemyDamageCallback = applyDamage;
        pinnedEnemyDamage = damage;
        pinnedEnemyDamageMultiplier = damageMultiplier;

        if (currentHits >= requiredHits)
        {
            // 今回の接触そのもので規定回数へ到達（Required Hits=1等）：留まらず通常通り1回分のダメージを適用させる
            currentHits = 0;
            lastReflectFrame = Time.frameCount;
            pinnedToEnemyMode = false;
            return false;
        }

        // 初回ヒット分のダメージを今すぐ適用する
        applyDamage(damage, damageMultiplier, hitPos);

        pinnedDirection = (normal.sqrMagnitude > 0.0001f)
            ? normal.normalized
            : (Vector2)transform.up;
        pinnedBasePosition = (rb != null) ? rb.position : (Vector2)transform.position;
        pinnedElapsed = 0f;

        isPinned = true;
        pinnedTimer = hitInterval;

        // 留まっている間、当たり判定そのものを無くす（線と同じ理由）
        if (col != null) col.enabled = false;

        return true;
    }

    private void Update()
    {
        if (!isPinned) return;

        pinnedElapsed += Time.deltaTime;

        if (pinnedToEnemyMode)
        {
            UpdatePinnedToEnemy();
            return;
        }

        // 実際に触れているセグメント（pinnedDot）自身が消えたかどうかで判定する。
        // 親のStroke（線全体）はまだ生きていても、触れていたセグメントだけ先に消えることがある
        if (pinnedDot == null || pinnedStroke == null)
        {
            Unpin();
            return;
        }

        pinnedTimer -= Time.deltaTime;
        if (pinnedTimer > 0f) return;

        pinnedTimer = hitInterval;
        currentHits++;

        if (currentHits >= requiredHits)
        {
            currentHits = 0;
            lastReflectFrame = Time.frameCount;
            isPinned = false;

            // ★反射時は壁の外側（元々弾が飛んできた側）へ退避させる必要がある。
            //   pinnedNormalはpinnedDirectionと同じ向き（壁にめり込む方向＝元の進行方向）で
            //   あり、そのまま使うとさらに壁の奥へ退避させてしまっていた（符号ミス）。
            //   壁から離れる方向は、その逆向き（-pinnedNormal）が正しい
            if (rb != null && pinnedNormal.sqrMagnitude > 0.0001f)
            {
                rb.position = rb.position - pinnedNormal.normalized * ReflectClearanceDistance;
            }

            // ★PaddleDot.PerformPinnedReflect()は「弾の現在の速度」を入射方向として
            //   Vector2.Reflectを計算する。留まっている間はLateUpdate()で速度を毎フレーム0に
            //   しているため、このままでは正しい入射方向が読み取れない。呼び出し直前に、
            //   実測で確認済みの正しい進行方向(pinnedDirection)を速度へ反映してから呼ぶ
            if (rb != null)
            {
                float speed = pinnedBullet != null ? pinnedBullet.TargetSpeed : 1f;
                rb.linearVelocity = pinnedDirection * Mathf.Max(speed, 0.1f);
            }

            // ★PerformPinnedReflect()内のMarkReflected()が弾のレイヤーを変更する（Unreflected→
            //   Reflected）。レイヤー変更が内部的な当たり判定の登録し直しを引き起こす可能性が
            //   あるため、先にPerformPinnedReflect()を完了させる。
            //   isJustは今ここで判定し直すのではなく、最初に線へ捕まった瞬間に確定させておいた値
            //   (pinnedIsJust)をそのまま渡す
            if (pinnedDot != null)
            {
                pinnedDot.PerformPinnedReflect(pinnedBullet, rb, pinnedNormal, pinnedHitPos, pinnedIsJust);
            }

            // ★反射が成立したので、次に別の何かへ捕まった時のためにJust確定状態をリセットする
            hasCapturedJust = false;
            pinnedIsJust = false;

            // ★IgnoreCollisionは無効化されたCollider2Dに対しては正しく登録されない（有効化した
            //   瞬間に設定が失われる）可能性があるため、必ずCollider有効化の後に呼ぶ
            if (col != null) col.enabled = true;
            IgnoreCurrentDotCollision();
            recentlyLeftDot = pinnedDot;
            recentlyLeftDotCooldownUntil = Time.time + RecentDotCooldownSeconds;
        }
        else
        {
            // 反射に至らない中間ヒットの分もSE/VFXを鳴らす
            pinnedDot?.PlayPinnedHitTick(pinnedBullet, pinnedHitPos);
        }
    }

    private void LateUpdate()
    {
        if (!isPinned || rb == null) return;

        // EnemyBullet.Update()が毎フレーム速度を書き換えて動かそうとしても、当たり判定が
        // 無効化されている（col.enabled=false）ため物理的な支障は無いが、見た目上は
        // 位置を直接固定しないと勝手に動いてしまう。ここで毎フレーム強制的に固定位置へ戻す。
        // 見た目上の「めり込み」は、この固定位置への一方向オフセットとして表現する
        float depth = Mathf.Min(creepSpeed * pinnedElapsed, Mathf.Max(0f, maxCreepVisualDepth));
        rb.position = pinnedBasePosition + pinnedDirection * depth;
        rb.linearVelocity = Vector2.zero;
    }

    /// <summary>
    /// 敵/シールドにピン留めされている間のUpdate処理。一定間隔でダメージを繰り返し与え、
    /// 規定回数に達したら弾自体を消滅させる（線と違い、反射や直進継続はしない）。
    /// 途中で対象(pinnedEnemyTarget)が破棄された場合は、留まらず元の方向へ直進を再開する
    /// （線が消えた時と同じ挙動）。
    /// </summary>
    private void UpdatePinnedToEnemy()
    {
        if (pinnedEnemyTarget == null)
        {
            pinnedToEnemyMode = false;
            Unpin();
            return;
        }

        pinnedTimer -= Time.deltaTime;
        if (pinnedTimer > 0f) return;

        pinnedTimer = hitInterval;
        currentHits++;

        // ★A8スキル（敵ヒットごとに基礎ダメージ加算）：刺さっている間の中間ヒットも
        //   「敵に当たった」1回としてカウント対象にする。通常弾が跳ね返ってパドル⇔敵を
        //   何度も往復するたびにダメージが積み上がるのと同じ体感になるよう、
        //   ティックのたびにbullet側のA8カウントを進め、更新後のDamageValueを使い回す
        if (pinnedBullet != null)
        {
            pinnedBullet.ApplyA8HitBonus();
            pinnedEnemyDamage = pinnedBullet.DamageValue;
        }

        pinnedEnemyDamageCallback?.Invoke(pinnedEnemyDamage, pinnedEnemyDamageMultiplier, pinnedHitPos);

        // ★敵/バリアへめり込んでいる間の中間ヒットVFXも、初回接触時と同じ経路
        //   （EnemyBulletFeedback.OnEnemyHit：enemyHitVfxPrefab / justPoweredVfxPrefab）で毎回出す
        if (pinnedBullet != null)
        {
            bool isPowered = pinnedEnemyDamageMultiplier > 1.0001f;
            pinnedBullet.GetComponent<EnemyBulletFeedback>()?.OnEnemyHit(pinnedHitPos, isPowered);
        }

        if (currentHits >= requiredHits)
        {
            currentHits = 0;
            lastReflectFrame = Time.frameCount;
            // 規定回数分ダメージを与えきったので弾自体を消滅させる（このコンポーネントも道連れに破棄される）
            if (pinnedBullet != null) Destroy(pinnedBullet.gameObject);
        }
    }

    /// <summary>留まっている間に線(セグメント)が消えた時、元の速度・方向で直進を再開させる</summary>
    private void Unpin()
    {
        isPinned = false;
        if (rb != null)
        {
            // Colliderを再有効化する前に、必ず周囲の（まだ生きている）別セグメントから
            // 離れる距離だけ位置を進めておく。1本の線は密集した複数のDotで構成されており、
            // 踏み込んだ位置のままColliderを再有効化すると、隣接する別セグメントへ即座に
            // 接触し、弾の物理マテリアル（反発係数1）による弾性衝突が発生してしまうため
            rb.position = rb.position + pinnedDirection * UnpinClearanceDistance;
        }
        // Collider有効化の後にIgnoreCollisionを呼ぶ（無効なCollider2Dに対しては
        // 正しく登録されない可能性があるため）
        if (col != null) col.enabled = true;
        IgnoreCurrentDotCollision();
        recentlyLeftDot = pinnedDot;
        recentlyLeftDotCooldownUntil = Time.time + RecentDotCooldownSeconds;
        if (rb != null)
        {
            float speed = pinnedBullet != null ? pinnedBullet.TargetSpeed : 1f;
            rb.linearVelocity = pinnedDirection * Mathf.Max(speed, 0.1f);
        }
        pinnedStroke = null;
        pinnedDot = null;
        pinnedDotCollider = null;
        pinnedToEnemyMode = false;
        pinnedEnemyTarget = null;
        pinnedEnemyDamageCallback = null;
    }

    /// <summary>触れていた特定のセグメントとの当たり判定を無視する（再ピン留め防止）</summary>
    private void IgnoreCurrentDotCollision()
    {
        if (col != null && pinnedDotCollider != null)
        {
            Physics2D.IgnoreCollision(col, pinnedDotCollider, true);
        }
    }
}
