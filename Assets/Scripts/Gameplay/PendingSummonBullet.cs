using System;
using UnityEngine;

/// <summary>
/// 「召喚されたが、まだ発射されていない」弾専用コンポーネント。ツクヨミのStraight複数召喚パターンから
/// 使われる。付与された瞬間からCollider2Dを無効化し、Rigidbody2Dの位置を召喚位置に固定し続けることで
/// 見た目だけがその場に浮かんで待機している状態を表現する（線での反射・ダンサー/フロアへの命中判定の
/// 対象にはならない）。召喚直後はスプライトを透明から不透明へフェードインさせる。
/// 指定した秒数が経過したら、狙う方向を今の瞬間に計算し直してから本当に発射する。
/// </summary>
public class PendingSummonBullet : MonoBehaviour
{
    private Rigidbody2D rb;
    private Collider2D col;
    private EnemyBullet bullet;
    private SpriteRenderer visualRenderer;

    private Vector3 holdPosition;
    private float remainingSeconds;
    private Func<Vector2> directionProvider;
    private bool isLaunched;

    private float fadeInSeconds;
    private float fadeElapsed;
    private Color fadeBaseColor = Color.white;
    private bool fadeDone;

    private AudioClip launchSeClip;
    private float launchSeVolume;
    private Action<EnemyBullet> onLaunch;

    public void Configure(float delaySeconds, float fadeInSeconds, Func<Vector2> directionProvider,
        AudioClip summonSeClip = null, float summonSeVolume = 1f,
        AudioClip launchSeClip = null, float launchSeVolume = 1f,
        Action<EnemyBullet> onLaunch = null)
    {
        rb = GetComponent<Rigidbody2D>();
        col = GetComponent<Collider2D>();
        bullet = GetComponent<EnemyBullet>();

        // DrillSpinBullet等と同じ「Visual」子オブジェクトのSpriteRendererを対象にする
        Transform v = transform.Find("Visual");
        visualRenderer = v != null ? v.GetComponent<SpriteRenderer>() : GetComponent<SpriteRenderer>();

        holdPosition = transform.position;
        remainingSeconds = Mathf.Max(0f, delaySeconds);
        this.directionProvider = directionProvider;

        this.fadeInSeconds = Mathf.Max(0f, fadeInSeconds);
        fadeElapsed = 0f;
        fadeDone = this.fadeInSeconds <= 0f;
        if (visualRenderer != null)
        {
            fadeBaseColor = visualRenderer.color;
            if (!fadeDone)
            {
                Color c = fadeBaseColor;
                c.a = 0f;
                visualRenderer.color = c;
            }
        }

        if (col != null) col.enabled = false;

        this.launchSeClip = launchSeClip;
        this.launchSeVolume = launchSeVolume;
        this.onLaunch = onLaunch;
        PlaySe(summonSeClip, summonSeVolume);
    }

    /// <summary>EnemyShooterのfireSE再生と同じ方式（一時AudioSource＋PlayOneShot）</summary>
    private void PlaySe(AudioClip clip, float volume)
    {
        if (clip == null || volume <= 0f) return;

        float finalVolume = volume * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
        GameObject go = new GameObject("PendingSummonBullet_SE");
        go.transform.position = transform.position;
        AudioSource a = go.AddComponent<AudioSource>();
        a.spatialBlend = 0f;
        a.playOnAwake = false;
        a.loop = false;
        a.PlayOneShot(clip, finalVolume);
        Destroy(go, clip.length + 0.1f);
    }

    private void Update()
    {
        if (!fadeDone) UpdateFadeIn();

        if (isLaunched) return;

        remainingSeconds -= Time.deltaTime;
        if (remainingSeconds <= 0f) Launch();
    }

    private void UpdateFadeIn()
    {
        fadeElapsed += Time.deltaTime;
        float t = fadeInSeconds > 0f ? Mathf.Clamp01(fadeElapsed / fadeInSeconds) : 1f;
        if (visualRenderer != null)
        {
            Color c = fadeBaseColor;
            c.a = fadeBaseColor.a * t;
            visualRenderer.color = c;
        }
        if (t >= 1f) fadeDone = true;
    }

    private void LateUpdate()
    {
        // EnemyBullet.Update()が毎フレーム速度を書き換えて動かそうとしても、当たり判定が
        // 無効化されているため物理的な支障は無いが、見た目上は位置を直接固定しないと
        // 勝手に動いてしまう（PinnedReflectBulletと同じ考え方）
        if (isLaunched || rb == null) return;
        rb.position = holdPosition;
        rb.linearVelocity = Vector2.zero;
    }

    private void Launch()
    {
        isLaunched = true;

        // フェード完了前に発射時刻を迎えた場合でも、発射の瞬間は必ず完全不透明にする
        if (visualRenderer != null) visualRenderer.color = fadeBaseColor;

        // ★死後も攻撃が続くのを防ぐ（EnemyShooter無効化ボスの既存の注意事項と同じ対策）
        if (FloorHealth.IsBrokenGlobal || PixelDancerController.IsPlayerDeadGlobal)
        {
            if (bullet != null) Destroy(bullet.gameObject);
            else Destroy(gameObject);
            return;
        }

        if (col != null) col.enabled = true;
        if (bullet != null && directionProvider != null)
        {
            bullet.SetDirection(directionProvider());
        }

        // ★Missile Arc（Curve）等、発射の瞬間に改めて開始すべき演出はここで呼ぶ。
        //   召喚直後にApplyMissileArc()してしまうと、待機中もコルーチンのタイマーが進み続け、
        //   実際に発射される頃には軌道計算が終わってただ直進するだけになってしまう不具合があった
        onLaunch?.Invoke(bullet);

        PlaySe(launchSeClip, launchSeVolume);

        Destroy(this);
    }
}
