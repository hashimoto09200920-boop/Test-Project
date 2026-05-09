using System.Collections;
using UnityEngine;

/// <summary>
/// 敵のスプライト一時差し替えを一元管理するコンポーネント。
/// AttackSprite・HitSpriteの競合を防ぐため、通常Spriteをここで保持する。
/// EnemyShooter（攻撃時）・EnemyDamageReceiver（被弾時）から呼び出す。
/// </summary>
[DisallowMultipleComponent]
public class EnemySpriteSwapper : MonoBehaviour
{
    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private Sprite normalSprite;

    // Attack・Hit それぞれ独立したコルーチン
    private Coroutine attackCo;
    private Coroutine hitCo;

    private bool isHitActive;
    private bool isAttackActive;
    private bool isTelegraphActive;

    /// <summary>被弾スプライト表示中かどうか</summary>
    public bool IsHitActive => isHitActive;
    private Sprite attackSprite;
    private Sprite hitSprite;
    private Sprite telegraphSpriteVal;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            // ルートにSRがない場合、Animatorを持たない子SR（Body）を優先して取得
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>())
            {
                if (sr.GetComponent<Animator>() == null)
                {
                    spriteRenderer = sr;
                    break;
                }
            }
        }
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        animator = GetComponent<Animator>();
        if (animator != null)
            animator.keepAnimatorStateOnDisable = true;
    }

    /// <summary>攻撃スプライトを一時表示する</summary>
    public void TriggerAttack(Sprite sprite, float duration)
    {
        if (sprite == null || duration <= 0f) return;
        if (spriteRenderer == null) return;

        if (normalSprite == null)
            normalSprite = spriteRenderer.sprite;

        attackSprite = sprite;
        isAttackActive = true;
        RefreshSprite();

        if (attackCo != null) StopCoroutine(attackCo);
        attackCo = StartCoroutine(AttackCoroutine(duration));
    }

    /// <summary>Telegraph開始時にスプライトを切り替える（StopTelegraph()まで保持）</summary>
    public void TriggerTelegraphStart(Sprite sprite)
    {
        if (sprite == null || spriteRenderer == null) return;
        if (normalSprite == null)
            normalSprite = spriteRenderer.sprite;
        telegraphSpriteVal = sprite;
        isTelegraphActive = true;
        RefreshSprite();
    }

    /// <summary>Telegraph終了時にスプライトを元に戻す</summary>
    public void StopTelegraph()
    {
        isTelegraphActive = false;
        telegraphSpriteVal = null;
        RefreshSprite();
    }

    /// <summary>被弾スプライトを一時表示する</summary>
    public void TriggerHit(Sprite sprite, float duration)
    {
        if (sprite == null || duration <= 0f) return;
        if (spriteRenderer == null) return;

        if (normalSprite == null)
            normalSprite = spriteRenderer.sprite;

        hitSprite = sprite;
        isHitActive = true;
        RefreshSprite();

        if (hitCo != null) StopCoroutine(hitCo);
        hitCo = StartCoroutine(HitCoroutine(duration));
    }

    private IEnumerator AttackCoroutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        isAttackActive = false;
        attackCo = null;
        RefreshSprite();
    }

    private IEnumerator HitCoroutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        isHitActive = false;
        hitCo = null;
        RefreshSprite();
    }

    /// <summary>
    /// スプライトを通常状態にリセットする。
    /// Instantiate によるクローン生成後に呼んで、継承した一時スプライト状態を解除する。
    /// </summary>
    public void ResetToNormal()
    {
        if (attackCo != null) { StopCoroutine(attackCo); attackCo = null; }
        if (hitCo != null)    { StopCoroutine(hitCo);    hitCo    = null; }
        isAttackActive    = false;
        isHitActive       = false;
        isTelegraphActive = false;
        telegraphSpriteVal = null;
        RefreshSprite();
    }

    /// <summary>フェーズ切り替え時に通常スプライトを更新する（JaguarRush用）</summary>
    public void SetBaseSprite(Sprite sp)
    {
        if (spriteRenderer == null || sp == null) return;
        normalSprite = sp;
        if (!isHitActive && !isAttackActive)
            spriteRenderer.sprite = sp;
    }

    /// <summary>優先度に従って表示Spriteを決定する（Hit > Attack > Normal）</summary>
    private void RefreshSprite()
    {
        if (spriteRenderer == null) return;

        bool shouldSwap = (isHitActive && hitSprite != null)
                       || (isAttackActive && attackSprite != null)
                       || (isTelegraphActive && telegraphSpriteVal != null);

        // Animatorが存在する場合は停止・再開でAnimatorの上書きを防ぐ
        if (animator != null)
            animator.enabled = !shouldSwap;

        if (isHitActive && hitSprite != null)
            spriteRenderer.sprite = hitSprite;
        else if (isAttackActive && attackSprite != null)
            spriteRenderer.sprite = attackSprite;
        else if (isTelegraphActive && telegraphSpriteVal != null)
            spriteRenderer.sprite = telegraphSpriteVal;
        else
            spriteRenderer.sprite = normalSprite;
    }
}
