using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// NeonDancer専用の煙幕（NeonDancer_SmokeParticle）に重なっている敵の弾を隠す部品。
///
/// ★共有のSmokeCloudも煙に入った弾のSpriteRendererを非表示にするが、EnemyBullet自身がLateUpdateで毎フレーム
///   Visual/JustOverlayの表示を戻すため、実際には隠れていない。共有コード（SmokeCloud/EnemyBullet）は改修せず
///   （Camel/Golem/Shamanの砂煙の見た目を変えないため）、この部品でNeonDancerの煙幕に重なっている弾だけを隠す。
/// ・隠し方：Renderer.forceRenderingOff（描画だけを止める設定）。各処理が使うenabled（表示ON/OFF）とは別の設定のため、
///   弾側の表示処理とぶつからない。弾の画像・Trail（軌跡）・粒子エフェクトなど、弾の子のRendererをすべて隠す
/// ・対象：煙幕の範囲判定（CircleCollider2D）に入っている敵の弾（SmokeCloudと同じくEnemyBulletすべて）
/// ・煙幕の出始め/消えかけ（透明度がHide Min Cloud Alpha未満）は隠さない
/// ・弾が範囲から出た・プールへ戻った・当たり判定が切れた・煙幕が消えた時は、必ず描画を戻す
/// 配置はメニュー「Tools/NeonDancer/2 前半の攻撃・ステージ/⑤煙幕の霧を減らして弾を隠す（負荷軽減）」で行う。
/// </summary>
[DisallowMultipleComponent]
public class NeonDancerSmokeBulletHider : MonoBehaviour
{
    [Tooltip("煙幕全体の透明度がこの値以上の間だけ弾を隠す（出始め・消えかけ・円で消している途中は隠さない）")]
    [Range(0f, 1f)] [SerializeField] private float hideMinCloudAlpha = 0.5f;

    private NeonDancerSmokeHaze haze;
    private Collider2D trigger; // 煙幕の範囲判定（SmokeCloudのCircleCollider2D）

    private class Entry
    {
        public EnemyBullet bullet;
        public Collider2D collider;
        public Renderer[] renderers;
        public bool hidden;
    }

    private readonly Dictionary<EnemyBullet, Entry> inside = new Dictionary<EnemyBullet, Entry>();
    private readonly List<EnemyBullet> removeBuffer = new List<EnemyBullet>();

    private void Awake()
    {
        haze = GetComponent<NeonDancerSmokeHaze>();
        trigger = GetComponent<CircleCollider2D>();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;
        // SmokeCloud.ShouldHideObjectと同じ判定（当たったCollider自身のGameObjectにEnemyBullet）
        EnemyBullet b = other.GetComponent<EnemyBullet>();
        if (b == null || inside.ContainsKey(b)) return;
        inside[b] = new Entry
        {
            bullet = b,
            collider = other,
            renderers = b.GetComponentsInChildren<Renderer>(true), // 画像・Trail・粒子エフェクト（非アクティブの子も含む）
        };
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other == null) return;
        EnemyBullet b = other.GetComponent<EnemyBullet>();
        if (b != null && inside.TryGetValue(b, out Entry e))
        {
            SetHidden(e, false);
            inside.Remove(b);
        }
    }

    private void LateUpdate()
    {
        if (inside.Count == 0) return;
        bool hide = haze == null || haze.CloudAlpha >= hideMinCloudAlpha;

        removeBuffer.Clear();
        foreach (var kv in inside)
        {
            Entry e = kv.Value;
            // プールへ戻った・当たり判定が切れた弾は描画を戻して対象から外す（Exitが来なかった場合の保険）
            //   さらに、煙幕の範囲判定と接していない弾（プールで再利用された弾など）も必ず戻す
            if (e.bullet == null || !e.bullet.gameObject.activeInHierarchy || e.collider == null || !e.collider.enabled
                || (trigger != null && !trigger.IsTouching(e.collider)))
            {
                SetHidden(e, false);
                removeBuffer.Add(kv.Key);
                continue;
            }
            SetHidden(e, hide);
        }
        for (int i = 0; i < removeBuffer.Count; i++) inside.Remove(removeBuffer[i]);
    }

    // 煙幕が消える・無効になる時は、隠している弾の描画をすべて戻す（プールで再利用される弾が見えなくなるのを防ぐ）
    private void OnDisable()
    {
        foreach (var kv in inside) SetHidden(kv.Value, false);
        inside.Clear();
    }

    private static void SetHidden(Entry e, bool hidden)
    {
        if (e.hidden == hidden) return;
        e.hidden = hidden;
        var rs = e.renderers;
        if (rs == null) return;
        for (int i = 0; i < rs.Length; i++)
            if (rs[i] != null) rs[i].forceRenderingOff = hidden;
    }
}
