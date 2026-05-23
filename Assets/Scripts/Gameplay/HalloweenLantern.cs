using UnityEngine;
using System.Collections;

/// <summary>
/// ハロウィンボス Phase1 用ランタン。
/// 反射弾が当たるとlitDurationSeconds秒間点灯し、ボスを実体化させる。
/// 3つ同時点灯でボスが恒久的に実体化。
/// </summary>
public class HalloweenLantern : MonoBehaviour
{
    [Header("Visuals")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite unlitSprite;
    [Tooltip("点灯時のアニメーションフレーム（Lit01/Lit02など）")]
    [SerializeField] private Sprite[] litSprites;
    [Tooltip("点灯アニメーションのフレーム間隔（秒）")]
    [SerializeField] private float litAnimInterval = 0.2f;
    [SerializeField] private Color litColor = new Color(1f, 0.8f, 0.2f);

    [Header("Settings")]
    [SerializeField] private float litDurationSeconds = 30f;

    [Header("Sound")]
    [Tooltip("点灯時に鳴らすSE")]
    [SerializeField] private AudioClip litSE;
    [Range(0f, 1f)]
    [SerializeField] private float litSEVolume = 1f;

    public bool IsLit { get; private set; }
    public bool IsPermanentlyLit { get; private set; }

    private HalloweenBossController boss;
    private Coroutine litCoroutine;
    private Coroutine litAnimCoroutine;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void Init(HalloweenBossController bossController)
    {
        boss = bossController;
    }

    // 3つ同時点灯時にボスコントローラーから呼ばれる
    public void MakePermanent()
    {
        if (IsPermanentlyLit) return;
        IsPermanentlyLit = true;
        if (litCoroutine != null) { StopCoroutine(litCoroutine); litCoroutine = null; }
        IsLit = true;
        StartLitAnim();
    }

    private void LightUp()
    {
        if (IsPermanentlyLit) return;
        if (litCoroutine != null) StopCoroutine(litCoroutine);

        IsLit = true;
        StartLitAnim();

        if (litSE != null)
        {
            float vol = litSEVolume * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
            AudioSource.PlayClipAtPoint(litSE, transform.position, vol);
        }

        litCoroutine = StartCoroutine(LitCountdown());
        boss?.OnLanternStateChanged();
    }

    private void StartLitAnim()
    {
        if (litAnimCoroutine != null) StopCoroutine(litAnimCoroutine);
        if (litSprites != null && litSprites.Length > 1)
            litAnimCoroutine = StartCoroutine(LitAnimLoop());
        else
            UpdateVisualStatic();
        if (spriteRenderer != null) spriteRenderer.color = litColor;
    }

    private void StopLitAnim()
    {
        if (litAnimCoroutine != null) { StopCoroutine(litAnimCoroutine); litAnimCoroutine = null; }
        UpdateVisualStatic();
    }

    private IEnumerator LitAnimLoop()
    {
        int index = 0;
        while (true)
        {
            if (spriteRenderer != null && litSprites[index] != null)
                spriteRenderer.sprite = litSprites[index];
            index = (index + 1) % litSprites.Length;
            yield return new WaitForSeconds(litAnimInterval);
        }
    }

    private IEnumerator LitCountdown()
    {
        yield return new WaitForSeconds(litDurationSeconds);
        if (!IsPermanentlyLit)
        {
            IsLit = false;
            litCoroutine = null;
            StopLitAnim();
            boss?.OnLanternStateChanged();
        }
    }

    private void UpdateVisualStatic()
    {
        if (spriteRenderer == null) return;
        if (IsLit && litSprites != null && litSprites.Length > 0)
            spriteRenderer.sprite = litSprites[0];
        else
            spriteRenderer.sprite = unlitSprite;
        spriteRenderer.color = IsLit ? litColor : Color.white;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (IsLit || IsPermanentlyLit) return;

        EnemyBullet bullet = other.GetComponent<EnemyBullet>();
        if (bullet == null || !bullet.IsReflected) return;

        Destroy(bullet.gameObject);
        LightUp();
    }
}
