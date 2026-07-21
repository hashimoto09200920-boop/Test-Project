using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// レーザービーム弾。既存のEnemyBullet（Rigidbody2D駆動）とは完全に別のクラス。
/// 発射時にRaycastの連鎖でその場から全セグメント（発射点→着弾/反射点→…）を確定させる。
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
        public PixelDancerController terminalPlayer; // このセグメントがPlayerに当たって終端した場合のPixelDancerController（移動追従に使う）。無ければnull
        public bool hadTerminalPlayer; // terminalPlayerに一度でも値が入ったか
        public bool isOpenEnd; // 何にも当たらず最大距離まで伸びきったセグメントか（後からEnemyが移動してきた時に打ち切るための判定用）
        public Vector2 segDir; // このセグメントが進んでいた方向（Block/Enemyが消えた時・反射前の線が消えた時、そこから先へ延長するのに使う）
        public GradientColorKey[] baseColorKeys; // フェードアウト計算のベースになる、元のグラデーション色
        public GradientAlphaKey[] baseAlphaKeys; // フェードアウト計算のベースになる、元のグラデーション不透明度
        public PaddleDot originDot; // このセグメントが直前に反射して生まれた場合、その反射元の線（CheckForNewReflectionsでの誤検出防止用）
    }

    private readonly List<BeamSegment> segments = new List<BeamSegment>();
    private EnemyData.BulletType bulletType;
    private float damageMultiplier = 1f; // Just反射で更新され、以降の全セグメントに引き継がれる（既存EnemyBulletのDamageMultiplierと同じ「一度Justになったら持続」仕様）
    private Collider2D[] ownerColliders;
    private int penetration;
    private int reflectCount; // ビーム1本の生涯を通じた反射回数（発射時＋都度判定の反射を全部合算してPaddle Bounce Limitと比較する）

    private static float GetTimeScale() => SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;

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

        if (direction.sqrMagnitude < 0.0001f)
        {
            Debug.LogWarning($"[EnemyBeamBullet] Fire()に渡された発射方向がほぼゼロベクトルです（direction={direction}）。呼び出し元のAim Mode解決を確認してください。");
        }

        int areaNumber = (GameSession.HasValidArea() && GameSession.SelectedArea != null)
            ? GameSession.SelectedArea.areaNumber : 0;
        penetration = (bt != null) ? bt.GetPenetration(areaNumber) : -1;

        List<BeamSegment> initial = BuildChainFrom(origin, direction.normalized, false);

        if (segments.Count == 0)
        {
            Destroy(gameObject);
            return;
        }

        foreach (var seg in initial)
        {
            CreateSegmentVisual(seg);
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

        StartCoroutine(GrowSegments(initial));

        float life = (bt != null && bt.lifeTime > 0f) ? bt.lifeTime : 5f;
        StartCoroutine(LifeRoutine(life));

        float tickRate = (bt != null && bt.beamDamageTickRate > 0f) ? bt.beamDamageTickRate : 4f;
        StartCoroutine(DamageTickRoutine(1f / tickRate));
        StartCoroutine(LiveReflectionCheckRoutine(1f / tickRate));
    }

    // =========================================================
    // セグメント連鎖の構築
    // 発射時（Fire）と、都度判定で新しい反射が見つかった時（CheckForNewReflections）の両方から呼ばれる。
    // 呼び出しごとに、新しく追加されたセグメントのリストを返す（見た目の生成・伸びる演出はこれを使う）。
    // =========================================================
    private List<BeamSegment> BuildChainFrom(Vector3 startOrigin, Vector2 startDir, bool startHasReflected, Collider2D preIgnore = null)
    {
        var newSegs = new List<BeamSegment>();
        BeamSegment prevSeg = null;
        Vector3 segStart = startOrigin;
        Vector2 dir = startDir;
        bool hasReflected = startHasReflected;
        var ignored = new HashSet<Collider2D>();
        // 呼び出し元（CheckForNewReflections）ですでに反射済みのコライダーを、この場でもう一度検出しないよう先に除外しておく
        if (preIgnore != null) ignored.Add(preIgnore);

        // 次に作られるセグメントが「直前に反射して生まれた」場合の、その反射元の線。
        // この呼び出しの最初はpreIgnore、以降はこのループ内で反射するたびに更新する
        PaddleDot pendingOriginDot = preIgnore != null ? preIgnore.GetComponent<PaddleDot>() : null;

        // セグメントを作成し、リスト登録・連結(next)まで行う
        BeamSegment AddSeg(Vector3 s, Vector3 e, bool reflected)
        {
            var seg = new BeamSegment { start = s, end = e, isReflected = reflected, segDir = dir };
            // 反射して生まれたセグメントには、その反射元の線を覚えさせておく。この場で無視するだけでなく、
            // 後々（別のTickの）CheckForNewReflectionsが同じ線を誤検出しないようにするため
            seg.originDot = pendingOriginDot;
            pendingOriginDot = null; // 使ったらクリア（次の反射が起きるまでは持ち越さない）
            segments.Add(seg);
            newSegs.Add(seg);
            if (prevSeg != null) prevSeg.next = seg;
            prevSeg = seg;
            return seg;
        }

        for (int guard = 0; guard < maxSegmentsSafety; guard++)
        {
            RaycastHit2D hit = FindNextRelevantHit(segStart, dir, ignored, hasReflected);

            if (!hit.collider)
            {
                if (showDebugLog && hasReflected)
                {
                    Debug.Log($"[EnemyBeamBullet] BuildChainFrom: 何にもヒットせず最大距離まで直進。segStart={segStart}, dir={dir}, hasReflected={hasReflected}");
                    LogAllWallHealthColliderInfo(segStart, dir);
                }
                AddSeg(segStart, segStart + (Vector3)(dir * maxSegmentDistance), hasReflected).isOpenEnd = true;
                return newSegs;
            }

            // Block（WallHealth。ダメージあり）
            WallHealth wall = GetWallHealth(hit.collider);
            if (wall != null)
            {
                if (showDebugLog)
                {
                    Debug.Log($"[EnemyBeamBullet] BuildChainFrom: WallHealth検出。collider={hit.collider.gameObject.name}, layer={hit.collider.gameObject.layer}, hasReflected={hasReflected}, hitPoint={hit.point}");
                }
                BeamSegment wallSeg = AddSeg(segStart, hit.point, hasReflected);
                wallSeg.terminalWall = wall; // Blockが移動した時に追従させるため覚えておく
                wallSeg.hadTerminalWall = true;
                wall.ApplyBeamDamage(!hasReflected, damageMultiplier > 1.0001f, hit.point);
                return newSegs;
            }

            // Enemy（反射後のみ。ここで停止する。以降はTickReflectedSegmentが同じ終端位置で継続ダメージを入れる）
            if (hasReflected)
            {
                EnemyPart hitPart = hit.collider.GetComponent<EnemyPart>();
                EnemyDamageReceiver hitReceiver = hitPart == null ? GetEnemyDamageReceiver(hit.collider) : null;
                if (hitPart != null || hitReceiver != null)
                {
                    BeamSegment enemySeg = AddSeg(segStart, hit.point, hasReflected);
                    enemySeg.terminalEnemyPart = hitPart; // 移動追従に使う（どちらか一方のみ入る）
                    enemySeg.terminalEnemyReceiver = hitReceiver;
                    enemySeg.hadTerminalEnemy = true;
                    int baseDamage = bulletType != null ? bulletType.damage : 0;
                    if (hitPart != null) hitPart.TryApplyExternalReflectedDamage(baseDamage, damageMultiplier, hit.point);
                    else hitReceiver.TryApplyExternalReflectedDamage(baseDamage, damageMultiplier, hit.point);
                    TrySpawnEnemyHitVfx(hit.point);
                    return newSegs;
                }
            }

            // 画面境界の壁（HP無し、跳ね返らずダメージも無し。そのまま終端する）
            if (IsBoundaryWallCollider(hit.collider))
            {
                AddSeg(segStart, hit.point, hasReflected);
                return newSegs;
            }

            // Player / Floor（未反射区間のみ該当。ここで終端する）
            if (!hasReflected)
            {
                PixelDancerController player = hit.collider.GetComponent<PixelDancerController>();

                if (player != null && bulletType != null && bulletType.beamIgnorePlayer)
                {
                    // Playerを無視する設定：当たり判定を持たず貫通してFloorまで直進する。
                    // segStartは動かさない（動かすと、最終的なセグメントの始点がPlayerの位置になり、
                    // 発射地点からPlayerまでの区間が描画されなくなる）。ignoredに入れるだけで、
                    // 次のFindNextRelevantHitが同じ発射地点から再探索してこのコライダーを飛ばしてくれる
                    ignored.Add(hit.collider);
                    continue;
                }

                FloorHealth floor = hit.collider.GetComponent<FloorHealth>();
                if (player != null || floor != null)
                {
                    BeamSegment targetSeg = AddSeg(segStart, hit.point, false);
                    if (player != null)
                    {
                        // FloorはPixelDancerと違って動かないので追従不要。Playerだけ移動追従させる
                        targetSeg.terminalPlayer = player;
                        targetSeg.hadTerminalPlayer = true;
                    }
                    return newSegs;
                }
            }

            // Paddleライン
            PaddleDot dot = hit.collider.GetComponent<PaddleDot>();
            if (dot != null)
            {
                bool penetrated = dot.EvaluateExternalHit(penetration, hit.point, hit.normal, out float justMulOut);
                if (penetrated)
                {
                    // 貫通：このコライダーは無視済みとして記録し、同じセグメントのまま直進を継続する
                    ignored.Add(hit.collider);
                    segStart = hit.point;
                    continue;
                }

                // 反射：ここでセグメントを確定し、新しいセグメントを反射方向へ開始する
                BeamSegment preReflectSeg = AddSeg(segStart, hit.point, hasReflected);
                preReflectSeg.reflectionSourceDot = dot; // この線が消えたら反射前の方向(segDir)に戻すために覚えておく
                preReflectSeg.hadReflectionSourceDot = true;
                TrySpawnPaddleHitVfx(hit.point);

                damageMultiplier = Mathf.Max(damageMultiplier, justMulOut);
                hasReflected = true;
                reflectCount++;

                if (bulletType != null && bulletType.paddleBounceLimit >= 0 && reflectCount >= bulletType.paddleBounceLimit)
                {
                    return newSegs;
                }

                // このコライダーは反射済みとして記録する。反射方向によっては次のRayが同じコライダーに
                // 再ヒットし得るため（浅い角度・真後ろ付近の反射など）、同一チェーン内での二重反射を防ぐ
                ignored.Add(hit.collider);
                pendingOriginDot = dot; // 次に生まれるセグメントは、この線から反射して生まれる

                dir = Vector2.Reflect(dir, hit.normal).normalized;
                segStart = hit.point;
                continue;
            }

            // 未分類のコライダー：安全側として無視し直進を継続（同じものに何度も当たらないよう記録だけしておく）
            ignored.Add(hit.collider);
        }

        if (showDebugLog)
        {
            Debug.LogWarning("[EnemyBeamBullet] BuildChainFrom: guard上限に到達しました。安全のため打ち切ります。");
        }

        return newSegs;
    }

    /// <summary>
    /// Raycastの結果から、無視すべきコライダー（貫通済み・未反射区間のオーナー/敵など）を飛ばして
    /// 次に意味のあるヒットを1つ返す。同一Rayの結果内で判定するため、コライダーのサイズに関係なく正確。
    /// </summary>
    private RaycastHit2D FindNextRelevantHit(Vector3 origin, Vector2 dir, HashSet<Collider2D> ignored, bool hasReflected)
    {
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, dir, maxSegmentDistance, hitLayers | boundaryWallLayers);
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
                t += Time.deltaTime * GetTimeScale();
                yield return null;
            }
            CheckForNewReflections();
        }
    }

    private void CheckForNewReflections()
    {
        // segmentsは判定中に増減し得るため、対象は先にスナップショットしておく
        var snapshot = new List<BeamSegment>(segments);

        foreach (BeamSegment seg in snapshot)
        {
            // 同じTick内で、より手前のセグメントの打ち切りによってすでに削除された可能性がある
            if (!segments.Contains(seg)) continue;

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

                // このセグメントが既にこの線で反射済み、またはこの線から直接反射して生まれたセグメントなら、
                // 浮動小数点誤差でセグメント端ちょうどのこの線を再検出してしまっているだけなので無視する
                // （同じ線に何度も反射し続けて角度が振動するバグの原因になる）
                if (dot == seg.reflectionSourceDot || dot == seg.originDot) continue;

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
                seg.hadReflectionSourceDot = true;
                TrySpawnPaddleHitVfx(seg.end);

                damageMultiplier = Mathf.Max(damageMultiplier, justMulOut);
                reflectCount++;

                if (bulletType != null && bulletType.paddleBounceLimit >= 0 && reflectCount >= bulletType.paddleBounceLimit)
                {
                    break;
                }

                Vector2 reflectDir = Vector2.Reflect(dir, hits[h].normal).normalized;
                List<BeamSegment> newSegs = BuildChainFrom(seg.end, reflectDir, true, hits[h].collider);
                if (newSegs.Count > 0) seg.next = newSegs[0];
                foreach (var ns in newSegs)
                {
                    CreateSegmentVisual(ns);
                }
                StartCoroutine(GrowSegments(newSegs));

                if (showDebugLog)
                {
                    Debug.Log($"[EnemyBeamBullet] Live reflect: セグメント途中(={seg.end})で新しい線を検出。{newSegs.Count}本のセグメントを追加。");
                }

                break; // このセグメントの処理はここで終わり、次のセグメントへ
            }
        }
    }

    // このセグメントから先（next連鎖）を全部削除する。打ち切りで不要になった古い連鎖の後始末に使う
    private void RemoveChainFrom(BeamSegment startSeg)
    {
        BeamSegment cur = startSeg;
        while (cur != null)
        {
            BeamSegment toRemove = cur;
            cur = cur.next;
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

    // 発射点→着弾点へ伸びる演出。反射で生まれたセグメントも同じ時間で順番に伸びる
    private IEnumerator GrowSegments(List<BeamSegment> segsToGrow)
    {
        float growDuration = (bulletType != null && bulletType.beamGrowDuration > 0f) ? bulletType.beamGrowDuration : 0.1f;

        foreach (var seg in segsToGrow)
        {
            if (growDuration <= 0f)
            {
                if (seg.line != null) seg.line.SetPosition(1, seg.end);
                UpdateSparkShape(seg, seg.start, seg.end);
                continue;
            }

            float t = 0f;
            while (t < growDuration)
            {
                t += Time.deltaTime * GetTimeScale();
                float ratio = Mathf.Clamp01(t / growDuration);
                Vector3 current = Vector3.Lerp(seg.start, seg.end, ratio);
                if (seg.line != null) seg.line.SetPosition(1, current);
                UpdateSparkShape(seg, seg.start, current);
                yield return null;
            }

            if (seg.line != null) seg.line.SetPosition(1, seg.end);
            UpdateSparkShape(seg, seg.start, seg.end);
        }
    }

    // =========================================================
    // 継続ダメージ（都度判定。未反射区間=PixelDancer/Floor、反射後区間=エネミー）
    // =========================================================
    private IEnumerator DamageTickRoutine(float interval)
    {
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
        int baseDamage = bulletType.damage;

        UpdateWallTrackedSegments();
        UpdateEnemyTrackedSegments();
        UpdatePlayerTrackedSegments();
        UpdateReflectionSourceDots();
        CheckOpenSegmentsForNewHit();
        TickPaddleReflectionVfx();

        // segmentsが判定中に増える可能性があるため件数を固定してから回す
        int count = segments.Count;
        for (int i = 0; i < count; i++)
        {
            BeamSegment seg = segments[i];
            if (!seg.isReflected)
            {
                TickUnreflectedSegment(seg, baseDamage);
            }
            else
            {
                TickReflectedSegment(seg, baseDamage);
            }
        }
    }

    // 終端したセグメントの続き（対象が消えた後の延長分）を、このセグメント自身のnextとして生やす共通処理。
    // Block破壊・Enemy撃破・反射元の線の消滅、いずれも「対象が消えたのでそこで止めず先へ進む」という点で同じ
    private void ContinueSegmentPastGone(BeamSegment seg)
    {
        List<BeamSegment> continuation = BuildChainFrom(seg.end, seg.segDir, seg.isReflected, null);
        if (continuation.Count > 0)
        {
            seg.next = continuation[0];
            foreach (var ns in continuation)
            {
                CreateSegmentVisual(ns);
            }
            StartCoroutine(GrowSegments(continuation));
        }
    }

    // Blockに当たって終端したセグメントを、そのBlockの現在位置に追従させる。
    // Blockが動いて隙間ができた場合はセグメントを伸ばし（逆に近づいた場合は縮め）、
    // 常にセグメントの先端がBlockの表面に触れ続けるようにする（ダメージが途切れないようにするため）。
    // Blockが破壊されて無くなった場合は、そこで止めずに同じ方向へビームを延長する（止まるのは対象に当たった時だけ）
    private void UpdateWallTrackedSegments()
    {
        // BuildChainFrom呼び出しでsegmentsが増える可能性があるため、対象は先にスナップショットしておく
        var snapshot = new List<BeamSegment>(segments);

        foreach (BeamSegment seg in snapshot)
        {
            if (!segments.Contains(seg)) continue; // 同じTick内で既に削除されている場合はスキップ

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
                if (hitDot != null && (hitDot == seg.originDot || hitDot == seg.reflectionSourceDot)) continue;
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
    }

    // Enemyに当たって終端したセグメントを、そのEnemyの現在位置に追従させる。考え方はUpdateWallTrackedSegmentsと同じ。
    // Enemyが撃破されて無くなった場合は、そこで止めずに同じ方向へビームを延長する
    private void UpdateEnemyTrackedSegments()
    {
        var snapshot = new List<BeamSegment>(segments);

        foreach (BeamSegment seg in snapshot)
        {
            if (!segments.Contains(seg)) continue;

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
                if (hitDot != null && (hitDot == seg.originDot || hitDot == seg.reflectionSourceDot)) continue; // 自分が反射した/生まれた線は無視
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
    }

    // 反射後、何にも当たらず最大距離まで伸びきったセグメント(isOpenEnd)の経路に、後からEnemyやBlockが
    // 移動してきて重なった場合、そこでセグメントを打ち切る。ダメージ自体はこの後に走るTickReflectedSegmentが
    // 打ち切り後のセグメントを見て入れるので、ここでは見た目の終端位置と追従対象の設定だけを行う
    private void CheckOpenSegmentsForNewHit()
    {
        var snapshot = new List<BeamSegment>(segments);

        foreach (BeamSegment seg in snapshot)
        {
            if (!segments.Contains(seg)) continue;
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
    }

    // Playerに当たって終端した未反射セグメントを、Playerの現在位置に追従させる。考え方はUpdateWallTrackedSegmentsと同じ。
    // Playerは基本破壊されないが、念のため対象が消えた場合は同じ方向へビームを延長する
    private void UpdatePlayerTrackedSegments()
    {
        var snapshot = new List<BeamSegment>(segments);

        foreach (BeamSegment seg in snapshot)
        {
            if (!segments.Contains(seg)) continue;

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
    }

    // 反射のきっかけとなった線(PaddleDot)が消えたセグメントを検知し、反射前の状態（本来の進行方向=segDir）に
    // 戻して伸び直す。反射先に生えていた古いセグメント連鎖は丸ごと削除してから作り直す
    private void UpdateReflectionSourceDots()
    {
        var snapshot = new List<BeamSegment>(segments);

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
    }

    // 反射している線に、Enemy/Blockと同じように接している間ずっとVFXを繰り返し再生する
    private void TickPaddleReflectionVfx()
    {
        foreach (BeamSegment seg in segments)
        {
            if (seg.reflectionSourceDot == null) continue; // まだ反射していない、または線が既に消えた
            TrySpawnPaddleHitVfx(seg.end);
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
                player.ApplyBeamDamage(baseDamage);
                continue;
            }

            FloorHealth floor = col.GetComponent<FloorHealth>();
            if (floor != null)
            {
                bool applied = floor.ApplyBeamDamage(baseDamage);
                if (applied) TrySpawnFloorHitVfx(hits[i].point);
                if (showDebugLog) Debug.Log($"[EnemyBeamBullet]   -> FloorHealth.ApplyBeamDamage結果: {applied}");
            }
        }
    }

    private void TickReflectedSegment(BeamSegment seg, int baseDamage)
    {
        RaycastHit2D[] hits = Physics2D.LinecastAll(seg.start, seg.end, hitLayers);
        HashSet<Object> hitThisTick = new HashSet<Object>();

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
                continue;
            }

            // WeakPoint Systemが有効な敵は、本体ではなく子のEnemyPartがダメージを受ける
            EnemyPart part = col.GetComponent<EnemyPart>();
            if (part != null)
            {
                if (!hitThisTick.Add(part)) continue; // 同一セグメント内、複数パーツの多重ヒット防止
                bool applied = part.TryApplyExternalReflectedDamage(baseDamage, damageMultiplier, hits[i].point);
                if (applied) TrySpawnEnemyHitVfx(hits[i].point);
                if (showDebugLog) Debug.Log($"[EnemyBeamBullet]   -> EnemyPart.TryApplyExternalReflectedDamage結果: {applied}");
                continue;
            }

            EnemyDamageReceiver receiver = GetEnemyDamageReceiver(col);
            if (receiver == null) continue;
            if (!hitThisTick.Add(receiver)) continue; // 同一セグメント内、複数パーツの多重ヒット防止

            bool result = receiver.TryApplyExternalReflectedDamage(baseDamage, damageMultiplier, hits[i].point);
            if (result) TrySpawnEnemyHitVfx(hits[i].point);
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
        while (t < duration)
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
                if (seg.line == null || seg.baseAlphaKeys == null) continue;

                GradientAlphaKey[] fadedAlphaKeys = new GradientAlphaKey[seg.baseAlphaKeys.Length];
                for (int i = 0; i < fadedAlphaKeys.Length; i++)
                {
                    fadedAlphaKeys[i] = new GradientAlphaKey(seg.baseAlphaKeys[i].alpha * alphaMul, seg.baseAlphaKeys[i].time);
                }

                Gradient g = new Gradient();
                g.SetKeys(seg.baseColorKeys, fadedAlphaKeys);
                seg.line.colorGradient = g;
            }

            yield return null;
        }

        Destroy(gameObject);
    }
}
