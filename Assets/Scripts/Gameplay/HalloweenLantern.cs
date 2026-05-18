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
    [SerializeField] private Sprite litSprite;
    [SerializeField] private Color litColor = new Color(1f, 0.8f, 0.2f);

    [Header("Settings")]
    [SerializeField] private float litDurationSeconds = 30f;

    public bool IsLit { get; private set; }
    public bool IsPermanentlyLit { get; private set; }

    private HalloweenBossController boss;
    private Coroutine litCoroutine;

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
        if (litCoroutine != null)
        {
            StopCoroutine(litCoroutine);
            litCoroutine = null;
        }
        IsLit = true;
        UpdateVisual();
    }

    private void LightUp()
    {
        if (IsPermanentlyLit) return;
        if (litCoroutine != null) StopCoroutine(litCoroutine);

        IsLit = true;
        UpdateVisual();
        litCoroutine = StartCoroutine(LitCountdown());
        boss?.OnLanternStateChanged();
    }

    private IEnumerator LitCountdown()
    {
        yield return new WaitForSeconds(litDurationSeconds);
        if (!IsPermanentlyLit)
        {
            IsLit = false;
            litCoroutine = null;
            UpdateVisual();
            boss?.OnLanternStateChanged();
        }
    }

    private void UpdateVisual()
    {
        if (spriteRenderer == null) return;
        spriteRenderer.sprite = IsLit && litSprite != null ? litSprite : unlitSprite;
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
