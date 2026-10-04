using UnityEngine;

/// <summary>
/// プレイヤー（PixelDancer）の魂の救出演出を追加する部品（NeonDancerの後半移行と同じ演出）。
/// - 魂のガイド：落下中（救出できる間）の魂の周りでリングを脈動させる（円で囲む対象だと分かるように）
/// - 虹色の尾：円で救出してから魂が本体へ戻るまでの間、虹色の光の粒を引く
/// PixelDancerControllerは変更せず、公開されている状態（IsFalling / RescueDisabled / SoulTransform / OnRescued）を見て動く。
/// </summary>
[DisallowMultipleComponent]
public class PixelDancerSoulRescueFX : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private PixelDancerController pixelDancer;

    [Header("魂のガイド（落下中の魂の周りで脈動するリング）")]
    [Tooltip("ガイドのリング（Soulの子 SoulGuide、加算合成）")]
    [SerializeField] private SpriteRenderer soulGuideRenderer;
    [Tooltip("リングの直径（ワールド単位）の最小/最大（脈動）")]
    [SerializeField] private Vector2 soulGuideSize = new Vector2(1.4f, 1.9f);
    [Tooltip("リングの不透明度の最小/最大（脈動）")]
    [SerializeField] private Vector2 soulGuideAlpha = new Vector2(0.35f, 0.9f);
    [Tooltip("脈動の回数/秒")]
    [SerializeField] private float soulGuidePulseFrequency = 1.5f;
    [SerializeField] private Color soulGuideColor = Color.white;

    [Header("虹色の尾（救出後、魂が本体へ戻るまで）")]
    [SerializeField] private ParticleSystem soulTrailParticles;
    [Tooltip("光の粒の1秒あたりの発生数")]
    [SerializeField] private float soulTrailRate = 45f;
    [Tooltip("光の粒の寿命（最小/最大、秒）")]
    [SerializeField] private Vector2 soulTrailLifetime = new Vector2(0.5f, 0.9f);
    [Tooltip("光の粒の大きさ（最小/最大）")]
    [SerializeField] private Vector2 soulTrailSize = new Vector2(0.08f, 0.18f);
    [Tooltip("虹色が1秒に何周するか")]
    [SerializeField] private float soulTrailHueSpeed = 0.8f;

    private float guideTime;
    private bool trailActive;
    private float trailTime;
    private float trailCarry;

    private void Awake()
    {
        if (pixelDancer == null) pixelDancer = GetComponent<PixelDancerController>();
        HideGuide();
    }

    private void OnEnable()
    {
        if (pixelDancer != null) pixelDancer.OnRescued += HandleRescued;
    }

    private void OnDisable()
    {
        if (pixelDancer != null) pixelDancer.OnRescued -= HandleRescued;
        trailActive = false;
        HideGuide();
    }

    private void HandleRescued()
    {
        HideGuide();
        trailActive = true;
        trailTime = 0f;
        trailCarry = 0f;
        if (soulTrailParticles != null) soulTrailParticles.Play();
    }

    private void LateUpdate()
    {
        if (pixelDancer == null) return;
        Transform soul = pixelDancer.SoulTransform;
        bool soulVisible = soul != null && soul.gameObject.activeInHierarchy;

        // ガイド：落下中で、救出できる時だけ
        if (soulVisible && pixelDancer.IsFalling && !pixelDancer.RescueDisabled)
        {
            guideTime += Time.deltaTime;
            UpdateGuide(guideTime);
        }
        else
        {
            guideTime = 0f;
            HideGuide();
        }

        // 虹色の尾：救出後、魂が本体へ戻って消えるまで
        if (trailActive)
        {
            if (!soulVisible) { trailActive = false; return; }
            trailTime += Time.deltaTime;
            if (soulTrailParticles != null)
            {
                trailCarry += soulTrailRate * Time.deltaTime;
                int n = Mathf.FloorToInt(trailCarry);
                trailCarry -= n;
                for (int i = 0; i < n; i++) EmitTrail(soul.position, trailTime);
            }
        }
    }

    private void UpdateGuide(float time)
    {
        if (soulGuideRenderer == null || soulGuideRenderer.sprite == null) return;
        float pulse = 0.5f + 0.5f * Mathf.Sin(time * soulGuidePulseFrequency * Mathf.PI * 2f);
        float size = Mathf.Lerp(soulGuideSize.x, soulGuideSize.y, pulse);
        float spriteSize = Mathf.Max(0.0001f, soulGuideRenderer.sprite.bounds.size.x);
        Transform gt = soulGuideRenderer.transform;
        Vector3 parentScale = gt.parent != null ? gt.parent.lossyScale : Vector3.one;
        float sx = Mathf.Abs(parentScale.x) > 0.0001f ? size / spriteSize / Mathf.Abs(parentScale.x) : 1f;
        float sy = Mathf.Abs(parentScale.y) > 0.0001f ? size / spriteSize / Mathf.Abs(parentScale.y) : 1f;
        gt.localScale = new Vector3(sx, sy, 1f);
        Color c = soulGuideColor;
        c.a = soulGuideColor.a * Mathf.Lerp(soulGuideAlpha.x, soulGuideAlpha.y, 1f - pulse); // 広がるほど薄く
        soulGuideRenderer.color = c;
        soulGuideRenderer.enabled = true;
    }

    private void HideGuide()
    {
        if (soulGuideRenderer == null) return;
        Color c = soulGuideRenderer.color; c.a = 0f; soulGuideRenderer.color = c;
        soulGuideRenderer.enabled = false;
    }

    private void EmitTrail(Vector3 pos, float time)
    {
        Color col = Color.HSVToRGB(Mathf.Repeat(time * soulTrailHueSpeed + Random.Range(0f, 0.15f), 1f), 0.8f, 1f);
        var ep = new ParticleSystem.EmitParams
        {
            position = pos + (Vector3)(Random.insideUnitCircle * 0.12f),
            velocity = (Vector3)(Random.insideUnitCircle * 0.25f),
            startLifetime = Random.Range(Mathf.Min(soulTrailLifetime.x, soulTrailLifetime.y), Mathf.Max(soulTrailLifetime.x, soulTrailLifetime.y)),
            startSize = Random.Range(Mathf.Min(soulTrailSize.x, soulTrailSize.y), Mathf.Max(soulTrailSize.x, soulTrailSize.y)),
            startColor = col,
            applyShapeToPosition = false,
        };
        soulTrailParticles.Emit(ep, 1);
    }
}
