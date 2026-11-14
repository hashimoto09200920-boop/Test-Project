using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// フード画面の「本日のおすすめ」のカードの演出（2026/11/14）。カードの見本（ShopUI.drinkCardTemplate）の子 FX_SpecialFrame に付く。
/// 表示・非表示は ShopUI が決める。粒子（パーティクル）は使わず、画像を動かすだけの軽い作り（OPPOのチカチカ対策）
/// ・A5 太い金の枠：明るさをゆっくり強弱させる
/// ・A1 後光：フードの絵の後ろの金色の光の筋をゆっくり回す
/// ・A2 光の走り：数秒おきに、白っぽい光の帯がカードを斜めに横切る（カードの枠の内側だけで見える）
/// ・Flash()：下の「本日のおすすめ」の文字を押した時に、カードを強く光らせる
/// 作成はメニュー「Tools/フード画面/本日のおすすめと購入枠5つを組み込む」
/// </summary>
public class FoodSpecialFrame : MonoBehaviour
{
    [Header("太い金の枠")]
    [SerializeField] private Image frame;
    [SerializeField] private Color color = new Color(1f, 0.82f, 0.25f, 1f);
    [Range(0f, 1f)] [SerializeField] private float alphaMin = 0.75f;
    [Range(0f, 1f)] [SerializeField] private float alphaMax = 1f;
    [Tooltip("1秒あたりの強弱の回数")]
    [SerializeField] private float pulseSpeed = 0.8f;

    [Header("後光（フードの絵の後ろ）")]
    [SerializeField] private RectTransform rays;
    [Tooltip("回る速さ（度/秒）")]
    [SerializeField] private float raysSpeed = 20f;

    [Header("光の走り")]
    [SerializeField] private RectTransform shine;
    [Tooltip("光の帯が横切る間隔（秒）")]
    [SerializeField] private float shineInterval = 2.5f;
    [Tooltip("光の帯がカードを横切るのにかかる時間（秒）")]
    [SerializeField] private float shineDuration = 0.6f;
    [Tooltip("光の帯の動く範囲（カードの真ん中から左右へ）")]
    [SerializeField] private float shineTravel = 320f;

    [Header("押した時に強く光る")]
    [SerializeField] private float flashSeconds = 0.8f;

    private float flashUntil = -1f;
    private float shineClock;

    // 後光はフードの絵の後ろ（この枠とは別の場所）にあるので、枠と一緒に出し入れする
    private void OnEnable() { shineClock = 0f; if (rays != null) rays.gameObject.SetActive(true); }
    private void OnDisable() { if (rays != null) rays.gameObject.SetActive(false); }

    /// <summary>カードを強く光らせる（おすすめの文字を押した時）。光の帯もすぐ走らせる</summary>
    public void Flash()
    {
        flashUntil = Time.unscaledTime + flashSeconds;
        shineClock = 0f;
    }

    private void Update()
    {
        float now = Time.unscaledTime;
        float dt = Time.unscaledDeltaTime;
        bool flashing = now < flashUntil;

        if (frame != null)
        {
            float t = (Mathf.Sin(now * pulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
            var c = color;
            c.a = flashing ? 1f : Mathf.Lerp(alphaMin, alphaMax, t);
            if (flashing) c = Color.Lerp(c, Color.white, 0.5f + 0.5f * Mathf.Sin(now * 30f));
            frame.color = c;
        }

        if (rays != null) rays.localRotation = Quaternion.Euler(0f, 0f, -now * raysSpeed);

        if (shine != null)
        {
            shineClock += dt;
            float cycle = Mathf.Max(shineDuration, shineInterval);
            if (shineClock > cycle) shineClock -= cycle;
            float k = shineClock / Mathf.Max(0.01f, shineDuration);
            bool on = k <= 1f;
            if (shine.gameObject.activeSelf != on) shine.gameObject.SetActive(on);
            if (on) shine.anchoredPosition = new Vector2(Mathf.Lerp(-shineTravel, shineTravel, k), 0f);
        }
    }

#if UNITY_EDITOR
    public void EditorWire(Image img, RectTransform raysRt, RectTransform shineRt) { frame = img; rays = raysRt; shine = shineRt; }
#endif
}
