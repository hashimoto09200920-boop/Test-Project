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
    private Sprite attackSprite;
    private Sprite hitSprite;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
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

    /// <summary>優先度に従って表示Spriteを決定する（Hit > Attack > Normal）</summary>
    private void RefreshSprite()
    {
        if (spriteRenderer == null) return;

        bool shouldSwap = (isHitActive && hitSprite != null) || (isAttackActive && attackSprite != null);

        // Animatorが存在する場合は停止・再開でAnimatorの上書きを防ぐ
        if (animator != null)
            animator.enabled = !shouldSwap;

        if (isHitActive && hitSprite != null)
            spriteRenderer.sprite = hitSprite;
        else if (isAttackActive && attackSprite != null)
            spriteRenderer.sprite = attackSprite;
        else
            spriteRenderer.sprite = normalSprite;
    }
}
