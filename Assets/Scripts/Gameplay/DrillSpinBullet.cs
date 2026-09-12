using UnityEngine;

/// <summary>
/// スプライトを複数コマ切り替えることで「ドリルが自軸回転しているような」見た目を作る専用コンポーネント。
/// EnemyBullet.cs の spriteRotateSpeed（平面上のZ回転）や PinnedReflectBullet の留まり中スピンは、
/// 1枚絵をクルクル回すだけなので「扇風機」のような見た目になってしまう。
/// 本コンポーネントは、それらのZ回転の代わりに「弾の進行方向（速度ベクトル）に向けて向きをリアルタイムに
/// 合わせる」処理と「複数コマのスプライト切り替え」を毎フレームLateUpdate()で行うことで、
/// ドリルの切っ先が着弾予定地点（カーブ中も含めた現在の進行方向）を向きつつ、軸回転しているような
/// 質感を表現する。EnemyBullet.cs / PinnedReflectBullet.cs本体には一切手を入れず、独立コンポーネントとして追加する。
/// </summary>
public class DrillSpinBullet : MonoBehaviour
{
    private SpriteRenderer visualRenderer;
    private Rigidbody2D rb;
    private Sprite[] frames;
    private float frameInterval = 0.05f;
    private int frameIndex;
    private float timer;
    private float lastAngle;
    private bool hasAngle;

    private void Awake()
    {
        Transform v = transform.Find("Visual");
        if (v != null) visualRenderer = v.GetComponent<SpriteRenderer>();
        rb = GetComponent<Rigidbody2D>();
    }

    /// <summary>発射直後に呼ぶ。frames全体を1周させる速さをrotationsPerSecondで指定する</summary>
    public void Configure(Sprite[] spinFrames, float rotationsPerSecond)
    {
        frames = spinFrames;
        if (frames != null && frames.Length > 0 && rotationsPerSecond > 0f)
            frameInterval = 1f / (rotationsPerSecond * frames.Length);
    }

    private void LateUpdate()
    {
        if (visualRenderer == null || frames == null || frames.Length == 0) return;

        // 弾の切っ先が現在の進行方向（カーブ中は刻々と変わる）を向くよう、
        // EnemyBullet.Update()のZ回転を上書きする形で、速度ベクトルから角度を計算して反映する。
        // スプライトは縦向き（上方向が切っ先）で作成しているため、90度分のオフセットを補正する。
        // ★速度が0（留まっている間等）でも、EnemyBullet側のspriteRotateSpeedによる回転は
        //   止まらず動き続けるため、ここでの上書きを止めると回転がそのまま見えてしまう。
        //   速度が有効な間だけ角度を更新し、0の間も直近の角度で毎フレーム上書きし続ける
        if (rb != null && rb.linearVelocity.sqrMagnitude > 0.0001f)
        {
            lastAngle = Mathf.Atan2(rb.linearVelocity.y, rb.linearVelocity.x) * Mathf.Rad2Deg;
            hasAngle = true;
        }
        if (hasAngle)
        {
            visualRenderer.transform.rotation = Quaternion.Euler(0f, 0f, lastAngle - 90f);
        }

        timer += Time.deltaTime;
        if (timer >= frameInterval)
        {
            timer -= frameInterval;
            frameIndex = (frameIndex + 1) % frames.Length;
            visualRenderer.sprite = frames[frameIndex];
        }
    }
}
