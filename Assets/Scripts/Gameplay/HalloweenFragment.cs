using UnityEngine;
using System.Collections;

/// <summary>
/// ハロウィンボス Phase2 の分身フラグメント（6体）。
/// 属性（カボチャ/コウモリ/魔女帽子）を持ち、記憶ゲームの当たり判定を担当する。
/// EnemyDamageReceiverは持たず、ヒット処理はHalloweenBossControllerに委譲する。
/// Collider2DはIsTrigger=trueで設定すること。
/// </summary>
public class HalloweenFragment : MonoBehaviour
{
    public enum AttributeType { Pumpkin, Bat, WitchHat }

    [Header("Visuals")]
    [SerializeField] private SpriteRenderer bodyRenderer;
    [Tooltip("属性アイコン表示用SpriteRenderer（子GameObjectにアタッチ）")]
    [SerializeField] private SpriteRenderer iconRenderer;
    [SerializeField] private Color glowColor = new Color(1f, 0.9f, 0.1f);

    [Header("Attribute Icons")]
    [SerializeField] private Sprite pumpkinIcon;
    [SerializeField] private Sprite batIcon;
    [SerializeField] private Sprite witchHatIcon;

    [Header("Icon Animation")]
    [Tooltip("フェードイン時間（秒）")]
    [SerializeField] private float iconFadeInDuration = 0.3f;
    [Tooltip("フェードアウト＋縮小時間（秒）")]
    [SerializeField] private float iconFadeOutDuration = 0.5f;
    [Tooltip("表示時のスケール（0〜1）")]
    [Range(0.1f, 2f)]
    [SerializeField] private float iconDisplayScale = 0.8f;
    [Tooltip("表示時の不透明度（1=完全不透明、0=完全透明）")]
    [Range(0f, 1f)]
    [SerializeField] private float iconDisplayAlpha = 0.7f;

    [Header("Attribute SE")]
    [SerializeField] private AudioClip attributeRevealSE;
    [Range(0f, 1f)]
    [SerializeField] private float attributeRevealSEVolume = 1f;

    [Header("Muzzle")]
    [Tooltip("射撃起点。未設定なら transform 位置を使用")]
    [SerializeField] private Transform muzzle;

    [Header("Blink")]
    [Tooltip("点滅間隔（秒）— 神経衰弱1発目ヒット時")]
    [SerializeField] private float blinkInterval = 0.15f;

    [Header("Fragment Animator")]
    [Tooltip("TauntアニメーションなどをフラグメントごとにPlayするAnimator")]
    [SerializeField] private Animator fragmentAnimator;

    [Header("Result VFX")]
    [SerializeField] private ParticleSystem matchVfx;
    [SerializeField] private ParticleSystem mismatchVfx;

    [Header("Shake")]
    [SerializeField] private float normalShakeDuration  = 0.15f;
    [SerializeField] private float normalShakeIntensity = 0.06f;
    [SerializeField] private float justShakeDuration    = 0.25f;
    [SerializeField] private float justShakeIntensity   = 0.14f;

    [Header("Result Tint")]
    [Tooltip("一致成功時のスプライトtint（ハロウィンオレンジゴールド）")]
    [SerializeField] private Color matchTintColor = new Color(1f, 0.75f, 0.2f);
    [Tooltip("不一致失敗時のスプライトtint（ハロウィン紫）")]
    [SerializeField] private Color mismatchTintColor = new Color(0.7f, 0.1f, 0.9f);

    public AttributeType Attribute { get; set; }
    public bool IsSolid { get; private set; }

    private HalloweenBossController boss;
    private Collider2D col;
    private FragmentHealthDisplay healthDisplay;
    private Coroutine iconAnimCoroutine;
    private Coroutine blinkCoroutine;
    private Coroutine damageSpriteCoroutine;
    private Coroutine shakeCoroutine;

    private void Awake()
    {
        col = GetComponent<Collider2D>();
        if (bodyRenderer == null)
            bodyRenderer = GetComponent<SpriteRenderer>();
        healthDisplay = GetComponent<FragmentHealthDisplay>();

        if (iconRenderer != null)
        {
            iconRenderer.enabled = false;
            Color c = iconRenderer.color;
            c.a = 0f;
            iconRenderer.color = c;
        }
    }

    public void Init(HalloweenBossController bossController, AttributeType type)
    {
        boss = bossController;
        Attribute = type;
    }

    public void SetSolid(bool solid)
    {
        IsSolid = solid;
        if (col != null) col.enabled = solid;
    }

    public void ShowAttribute(bool show)
    {
        if (iconAnimCoroutine != null) StopCoroutine(iconAnimCoroutine);

        if (show)
        {
            if (attributeRevealSE != null)
            {
                float vol = attributeRevealSEVolume *
                    (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
                AudioSource.PlayClipAtPoint(attributeRevealSE, transform.position, vol);
            }

            if (iconRenderer != null)
            {
                iconRenderer.sprite = GetIconSprite();
                iconRenderer.enabled = true;
                iconRenderer.transform.localScale = Vector3.one * iconDisplayScale;
            }

            iconAnimCoroutine = StartCoroutine(IconFadeInRoutine());
        }
        else
        {
            iconAnimCoroutine = StartCoroutine(IconFadeOutRoutine());
        }
    }

    public void SetGlowing(bool glow)
    {
        if (bodyRenderer == null) return;
        Color target = glow ? glowColor : Color.white;
        target.a = bodyRenderer.color.a;
        bodyRenderer.color = target;
    }

    public void StartBlinking()
    {
        StopBlinking();
        blinkCoroutine = StartCoroutine(BlinkRoutine());
    }

    public void StopBlinking()
    {
        if (blinkCoroutine != null)
        {
            StopCoroutine(blinkCoroutine);
            blinkCoroutine = null;
        }
        SetBodyAlpha(1f);
        SetGlowing(false);
    }

    public void PlayAnim(string stateName)
    {
        if (fragmentAnimator == null) return;
        fragmentAnimator.enabled = true;
        fragmentAnimator.Play(stateName, 0, 0f);
    }

    public void ShowDamageSprite(Sprite[] sprites, float duration)
    {
        if (sprites == null || sprites.Length == 0 || bodyRenderer == null) return;
        if (damageSpriteCoroutine != null) StopCoroutine(damageSpriteCoroutine);
        damageSpriteCoroutine = StartCoroutine(DamageSpriteRoutine(sprites, duration));
    }

    private IEnumerator DamageSpriteRoutine(Sprite[] sprites, float duration)
    {
        Sprite dmgSprite = sprites[Random.Range(0, sprites.Length)];
        Color savedColor = bodyRenderer.color;

        if (fragmentAnimator != null) fragmentAnimator.enabled = false;
        bodyRenderer.sprite = dmgSprite;
        bodyRenderer.color = new Color(savedColor.r, savedColor.g, savedColor.b, 1f);

        yield return new WaitForSeconds(duration);

        damageSpriteCoroutine = null;
        if (bodyRenderer != null)
        {
            bodyRenderer.color = savedColor;
            if (fragmentAnimator != null)
            {
                fragmentAnimator.enabled = true;
                fragmentAnimator.Play("Balloon", 0, 0f);
            }
        }
    }

    private IEnumerator BlinkRoutine()
    {
        SetGlowing(true);
        bool visible = true;
        while (true)
        {
            SetBodyAlpha(visible ? 1f : 0f);
            visible = !visible;
            yield return new WaitForSeconds(blinkInterval);
        }
    }

    public void SetBodyAlpha(float alpha)
    {
        if (bodyRenderer == null) return;
        Color c = bodyRenderer.color;
        c.a = alpha;
        bodyRenderer.color = c;
    }

    public void SetVisible(bool visible)
    {
        if (bodyRenderer != null) bodyRenderer.enabled = visible;
        if (col != null) col.enabled = visible && IsSolid;
        if (iconRenderer != null && !visible)
        {
            if (iconAnimCoroutine != null) StopCoroutine(iconAnimCoroutine);
            iconRenderer.enabled = false;
        }
        healthDisplay?.SetBarsVisible(visible);
    }

    public Transform GetMuzzle() => muzzle != null ? muzzle : transform;

    private Sprite GetIconSprite() => Attribute switch
    {
        AttributeType.Pumpkin  => pumpkinIcon,
        AttributeType.Bat      => batIcon,
        AttributeType.WitchHat => witchHatIcon,
        _                      => null,
    };

    private IEnumerator IconFadeInRoutine()
    {
        if (iconRenderer == null) yield break;
        Color c = iconRenderer.color;
        float elapsed = 0f;
        while (elapsed < iconFadeInDuration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(0f, iconDisplayAlpha, elapsed / iconFadeInDuration);
            iconRenderer.color = c;
            yield return null;
        }
        c.a = iconDisplayAlpha;
        iconRenderer.color = c;
        iconAnimCoroutine = null;
    }

    private IEnumerator IconFadeOutRoutine()
    {
        if (iconRenderer == null) yield break;
        Color c = iconRenderer.color;
        float startAlpha = c.a;
        float startScale = iconRenderer.transform.localScale.x;
        float elapsed = 0f;
        while (elapsed < iconFadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / iconFadeOutDuration;
            c.a = Mathf.Lerp(startAlpha, 0f, t);
            iconRenderer.color = c;
            float s = Mathf.Lerp(startScale, 0f, t);
            iconRenderer.transform.localScale = Vector3.one * s;
            yield return null;
        }
        c.a = 0f;
        iconRenderer.color = c;
        iconRenderer.enabled = false;
        iconAnimCoroutine = null;
    }

    public void TriggerShake(bool isJust = false)
    {
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            transform.localPosition = Vector3.zero;
        }
        float duration  = isJust ? justShakeDuration  : normalShakeDuration;
        float intensity = isJust ? justShakeIntensity : normalShakeIntensity;
        shakeCoroutine = StartCoroutine(ShakeRoutine(duration, intensity));
    }

    private IEnumerator ShakeRoutine(float duration, float intensity)
    {
        float elapsed = 0f;
        Vector3 offset = Vector3.zero;
        while (elapsed < duration)
        {
            transform.localPosition -= offset;
            float cur = intensity * (1f - elapsed / duration);
            offset = new Vector3(Random.Range(-cur, cur), Random.Range(-cur, cur), 0f);
            transform.localPosition += offset;
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.localPosition -= offset;
        shakeCoroutine = null;
    }

#if UNITY_EDITOR
    public void AssignVfxForSetup(ParticleSystem match, ParticleSystem mismatch)
    {
        matchVfx   = match;
        mismatchVfx = mismatch;
        UnityEditor.EditorUtility.SetDirty(this);
    }
#endif

    public void PlayMatchVfx()
    {
        if (mismatchVfx != null && mismatchVfx.isPlaying)
            mismatchVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (matchVfx != null) matchVfx.Play(true);
        ApplyTint(matchTintColor);
    }

    public void StopMatchVfx()
    {
        if (matchVfx != null)
            matchVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ApplyTint(Color.white);
    }

    public void PlayMismatchVfx()
    {
        if (matchVfx != null && matchVfx.isPlaying)
            matchVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (mismatchVfx != null) mismatchVfx.Play(true);
        ApplyTint(mismatchTintColor);
    }

    public void StopMismatchVfx()
    {
        if (mismatchVfx != null)
            mismatchVfx.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        ApplyTint(Color.white);
    }

    private void ApplyTint(Color rgb)
    {
        if (bodyRenderer == null) return;
        Color c = bodyRenderer.color;
        bodyRenderer.color = new Color(rgb.r, rgb.g, rgb.b, c.a);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsSolid) return;
        EnemyBullet bullet = other.GetComponent<EnemyBullet>();
        if (bullet == null || !bullet.IsReflected) return;
        boss?.OnFragmentHit(this, bullet);
    }
}
