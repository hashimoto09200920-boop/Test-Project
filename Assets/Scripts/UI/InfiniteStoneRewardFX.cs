using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 無限化の石を獲得した時の演出（03_AreaSelect。Area2/5/8の初回クリア後の獲得通知で使う）。
/// ① 通知パネルが出た瞬間：石のアイコンが枠の上の方にポンと出て、虹色の輪と光の粒が広がる（石は後ろの光とともにゆっくり上下に揺れる）
/// ② OKを押した時：石が右上の所持数（InfiniteStoneHUD）へ弧を描いて飛んでいき、着いたら所持数が増える数字に数え上がって弾む
/// 部品はメニュー「Tools/エリア選択画面/無限化の石の獲得演出を組み込む（03_AreaSelect）」でPlay前に作る。動きは全て実時間（一時停止の影響を受けない）。
/// 画面全体を光らせる演出は入れない（目に負担がかかるため）。毎フレームの処理は演出中だけ。
/// </summary>
public class InfiniteStoneRewardFX : MonoBehaviour
{
    [Header("参照（メニューで自動設定）")]
    [SerializeField] private RectTransform stone;
    [SerializeField] private RectTransform glow;
    [SerializeField] private Image glowImage;
    [SerializeField] private RectTransform ring;
    [SerializeField] private Image ringImage;
    [SerializeField] private RectTransform[] sparks;
    [SerializeField] private Image[] sparkImages;
    [SerializeField] private InfiniteStoneHUD hud;

    [Header("① 出現")]
    [Tooltip("石を出す位置：通知パネルの枠（Box）の中心からのずれ(px)")]
    [SerializeField] private Vector2 appearOffset = new Vector2(0f, 175f);
    [Tooltip("石の大きさ(px)")]
    [SerializeField] private float stoneSize = 96f;
    [Tooltip("ポンと出る時間（秒）と、一番大きくなる時の倍率")]
    [SerializeField] private float popSeconds = 0.3f;
    [SerializeField] private float popOvershoot = 1.25f;
    [Tooltip("虹色の輪：広がる時間（秒）・最初と最後の大きさ（石の大きさに対する倍率）・最初の濃さ")]
    [SerializeField] private float ringSeconds = 0.6f;
    [SerializeField] private float ringStartScale = 0.4f;
    [SerializeField] private float ringEndScale = 2.6f;
    [SerializeField, Range(0f, 1f)] private float ringAlpha = 0.85f;
    [Tooltip("光の粒：飛び散る時間（秒）・距離(px)・大きさ(px)")]
    [SerializeField] private float sparkSeconds = 0.7f;
    [SerializeField] private float sparkDistance = 150f;
    [SerializeField] private float sparkSize = 26f;
    [Tooltip("石の後ろの光：大きさ（石に対する倍率）・濃さ・明滅の速さ（1秒あたりの回数）")]
    [SerializeField] private float glowScale = 1.9f;
    [SerializeField, Range(0f, 1f)] private float glowAlpha = 0.5f;
    [SerializeField] private float glowPulsePerSecond = 0.8f;
    [Tooltip("表示中、石がゆっくり上下に揺れる幅(px)と速さ（1秒あたりの回数）")]
    [SerializeField] private float bobAmount = 4f;
    [SerializeField] private float bobPerSecond = 0.6f;
    [Tooltip("虹色が一周する速さ（1秒あたりの周回数）")]
    [SerializeField] private float rainbowPerSecond = 0.6f;

    [Header("② 所持数へ飛んでいく")]
    [Tooltip("飛ぶ時間（秒）と、弧の高さ(px)")]
    [SerializeField] private float flySeconds = 0.55f;
    [SerializeField] private float flyArcHeight = 120f;
    [Tooltip("着いた時に所持数が弾む倍率と時間（秒）、数え上がる時間（秒）")]
    [SerializeField] private float arrivePunchScale = 1.35f;
    [SerializeField] private float arrivePunchSeconds = 0.25f;
    [SerializeField] private float countUpSeconds = 0.4f;

    private enum State { Hidden, Appear, Idle, Fly }
    private State state = State.Hidden;
    private float t;
    private Vector2 stonePos;          // このオブジェクトの中での石の位置
    private Vector2 flyFrom, flyTo;
    private float flyEndScale = 0.4f;
    private Vector2[] sparkDirs;

    private RectTransform Root => (RectTransform)transform;

    private void Awake() { HideAll(); }

    /// <summary>① 通知パネルが出た時に呼ぶ。anchor＝通知パネルの枠（Box）。amount＝獲得数（所持数の表示を、獲得前の数から始めるため）</summary>
    public void PlayAppear(RectTransform anchor, int amount)
    {
        if (stone == null) return;
        gameObject.SetActive(true);
        transform.SetAsLastSibling(); // 通知パネルより手前
        Vector3 local = anchor != null ? Root.InverseTransformPoint(anchor.position) : Vector3.zero;
        stonePos = (Vector2)local + appearOffset;
        stone.sizeDelta = Vector2.one * stoneSize;
        stone.anchoredPosition = stonePos;
        stone.localScale = Vector3.zero;
        stone.gameObject.SetActive(true);
        if (glow != null) { glow.sizeDelta = Vector2.one * stoneSize * glowScale; glow.anchoredPosition = stonePos; glow.gameObject.SetActive(true); }
        if (ring != null) { ring.sizeDelta = Vector2.one * stoneSize; ring.anchoredPosition = stonePos; ring.gameObject.SetActive(true); }
        int n = sparks != null ? sparks.Length : 0;
        sparkDirs = new Vector2[n];
        for (int i = 0; i < n; i++)
        {
            if (sparks[i] == null) continue;
            float a = (i / (float)n) * Mathf.PI * 2f + Random.Range(-0.2f, 0.2f);
            sparkDirs[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(0.7f, 1f);
            sparks[i].sizeDelta = Vector2.one * sparkSize;
            sparks[i].anchoredPosition = stonePos;
            sparks[i].gameObject.SetActive(true);
        }
        // 所持数の表示は、石が届くまで獲得前の数のまま
        if (hud != null && InfiniteStoneManager.Instance != null) hud.HoldDisplay(Mathf.Max(0, InfiniteStoneManager.Instance.Count - amount));
        state = State.Appear;
        t = 0f;
    }

    /// <summary>② OKを押した時に呼ぶ。石が所持数へ飛んでいき、着いたら所持数が数え上がる</summary>
    public void FlyToHud()
    {
        if (state == State.Hidden) return;
        if (glow != null) glow.gameObject.SetActive(false);
        if (ring != null) ring.gameObject.SetActive(false);
        foreach (var s in sparks) if (s != null) s.gameObject.SetActive(false);
        flyFrom = stone.anchoredPosition;
        RectTransform icon = hud != null ? hud.IconRect : null;
        if (icon != null)
        {
            flyTo = Root.InverseTransformPoint(icon.position);
            // 着いた時、所持数のアイコンと同じくらいの大きさになるように
            Vector3[] c = new Vector3[4]; icon.GetWorldCorners(c);
            float iconH = Root.InverseTransformVector(c[1] - c[0]).magnitude;
            flyEndScale = stoneSize > 0f ? Mathf.Clamp(iconH / stoneSize, 0.1f, 1f) : 0.4f;
        }
        else { flyTo = flyFrom + new Vector2(0f, 400f); flyEndScale = 0.3f; }
        state = State.Fly;
        t = 0f;
    }

    /// <summary>すぐに全部消す（別の通知を出す時など）。所持数の表示も元に戻す</summary>
    public void HideImmediate()
    {
        if (state != State.Hidden && hud != null) hud.ReleaseHold();
        HideAll();
    }

    private void HideAll()
    {
        state = State.Hidden;
        if (stone != null) stone.gameObject.SetActive(false);
        if (glow != null) glow.gameObject.SetActive(false);
        if (ring != null) ring.gameObject.SetActive(false);
        if (sparks != null) foreach (var s in sparks) if (s != null) s.gameObject.SetActive(false);
    }

    private Color Rainbow(float offset, float alpha)
    {
        Color c = Color.HSVToRGB(Mathf.Repeat(Time.unscaledTime * rainbowPerSecond + offset, 1f), 0.55f, 1f);
        c.a = alpha;
        return c;
    }

    private void Update()
    {
        if (state == State.Hidden) return;
        float dt = Time.unscaledDeltaTime;
        t += dt;

        if (state == State.Appear || state == State.Idle)
        {
            // 石：ポンと出る→ゆっくり上下に揺れる
            float k = popSeconds > 0f ? Mathf.Clamp01(t / popSeconds) : 1f;
            float s = k < 0.7f ? Mathf.Lerp(0f, popOvershoot, k / 0.7f) : Mathf.Lerp(popOvershoot, 1f, (k - 0.7f) / 0.3f);
            stone.localScale = Vector3.one * s;
            float bob = Mathf.Sin(t * bobPerSecond * Mathf.PI * 2f) * bobAmount;
            stone.anchoredPosition = stonePos + new Vector2(0f, bob);
            // 後ろの光：明滅＋虹色
            if (glow != null)
            {
                float p = (Mathf.Sin(t * glowPulsePerSecond * Mathf.PI * 2f) + 1f) * 0.5f;
                glow.localScale = Vector3.one * (s * (0.92f + 0.12f * p));
                glow.anchoredPosition = stone.anchoredPosition;
                if (glowImage != null) glowImage.color = Rainbow(0f, glowAlpha * (0.7f + 0.3f * p));
            }
            // 虹色の輪：広がりながら薄くなる（1回）
            if (ring != null && ring.gameObject.activeSelf)
            {
                float rk = ringSeconds > 0f ? Mathf.Clamp01(t / ringSeconds) : 1f;
                ring.localScale = Vector3.one * Mathf.Lerp(ringStartScale, ringEndScale, 1f - (1f - rk) * (1f - rk));
                if (ringImage != null) ringImage.color = Rainbow(0.3f, ringAlpha * (1f - rk));
                if (rk >= 1f) ring.gameObject.SetActive(false);
            }
            // 光の粒：飛び散りながら小さく・薄くなる（1回）
            if (sparks != null)
            {
                float sk = sparkSeconds > 0f ? Mathf.Clamp01(t / sparkSeconds) : 1f;
                float e = 1f - (1f - sk) * (1f - sk);
                for (int i = 0; i < sparks.Length; i++)
                {
                    if (sparks[i] == null || !sparks[i].gameObject.activeSelf) continue;
                    sparks[i].anchoredPosition = stonePos + sparkDirs[i] * sparkDistance * e;
                    sparks[i].localScale = Vector3.one * (1f - 0.6f * sk);
                    sparks[i].localRotation = Quaternion.Euler(0f, 0f, sk * 180f * (i % 2 == 0 ? 1f : -1f));
                    if (sparkImages != null && i < sparkImages.Length && sparkImages[i] != null) sparkImages[i].color = Rainbow(i / (float)sparks.Length, 1f - sk);
                    if (sk >= 1f) sparks[i].gameObject.SetActive(false);
                }
            }
            if (state == State.Appear && t >= Mathf.Max(popSeconds, ringSeconds, sparkSeconds)) state = State.Idle;
            return;
        }

        if (state == State.Fly)
        {
            float k = flySeconds > 0f ? Mathf.Clamp01(t / flySeconds) : 1f;
            float e = k * k * (3f - 2f * k);
            Vector2 p = Vector2.Lerp(flyFrom, flyTo, e) + new Vector2(0f, Mathf.Sin(k * Mathf.PI) * flyArcHeight);
            stone.anchoredPosition = p;
            stone.localScale = Vector3.one * Mathf.Lerp(1f, flyEndScale, e);
            if (k >= 1f)
            {
                stone.gameObject.SetActive(false);
                state = State.Hidden;
                if (hud != null) hud.PlayArrive(arrivePunchScale, arrivePunchSeconds, countUpSeconds);
            }
        }
    }
}
