using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ゴールド表示UI。
/// 05_Game    : showSessionGold = true  → SessionGold を表示
/// 03_AreaSelect: showSessionGold = false → PersistentGold を表示
/// </summary>
public class GoldHUD : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image goldIcon;
    [SerializeField] private TextMeshProUGUI goldText;

    [Header("Settings")]
    [Tooltip("true = セッションゴールド（05_Game用）\nfalse = 永続ゴールド（03_AreaSelect用）")]
    [SerializeField] private bool showSessionGold = true;

    [Header("③ 増えた時に数字が増えていく（05_Gameのセッションゴールド表示だけ）")]
    [Tooltip("ON：ゴールドが増えた時、表示の数字が一気に変わらず増えていく（増えている間は文字が光る）")]
    [SerializeField] private bool countUpEnabled = true;
    [Tooltip("数字が増えきるまでの秒数")]
    [SerializeField] private float countUpSeconds = 0.5f;
    [Tooltip("数字が増えている間の文字の色")]
    [SerializeField] private Color countUpColor = new Color(1f, 1f, 0.55f, 1f);
    [Tooltip("数字が増えている間の文字の大きさの倍率")]
    [SerializeField] private float countUpScale = 1.12f;

    /// <summary>05_Gameのセッションゴールド表示（アイテムのGoldが飛んでいく先。ItemFXManager）</summary>
    public static GoldHUD SessionInstance { get; private set; }

    private Coroutine punchCo;
    private Vector3 iconBaseScale = Vector3.one, textBaseScale = Vector3.one;
    private Color textBaseColor = Color.white;

    // ③ 数字が増えていく
    private int countTarget;
    private float countShown;
    private int countShownInt = int.MinValue;
    private float countFrom;
    private float countStartTime;
    private bool counting;

    private void Awake()
    {
        GameNumberFont.Apply(goldText); // ★数字用フォント（Bungee）に切り替え（初回だけ。数字の高さは従来と同じに補正）
        if (goldIcon != null) iconBaseScale = goldIcon.rectTransform.localScale;
        if (goldText != null) { textBaseScale = goldText.rectTransform.localScale; textBaseColor = goldText.color; }
    }

    /// <summary>
    /// ③ 数字が増え始めるのを、指定秒数だけ遅らせる（アイテムのコインがGold表示に着くタイミングに合わせる。ItemFXManager）
    /// </summary>
    public void DelayCountUp(float seconds)
    {
        if (!showSessionGold || !countUpEnabled) return;
        countStartTime = Mathf.Max(countStartTime, Time.unscaledTime + Mathf.Max(0f, seconds));
    }

    private void Update()
    {
        if (!counting || goldText == null) return;
        float now = Time.unscaledTime;
        if (now < countStartTime) return;
        float k = countUpSeconds > 0f ? Mathf.Clamp01((now - countStartTime) / countUpSeconds) : 1f;
        countShown = Mathf.Lerp(countFrom, countTarget, 1f - (1f - k) * (1f - k));
        int v = k >= 1f ? countTarget : Mathf.RoundToInt(countShown);
        if (v != countShownInt) { countShownInt = v; goldText.text = v.ToString(); }
        // 増えている間は光って少し大きく（Punch中はPunch側が大きさを決める）
        float glow = Mathf.Sin(k * Mathf.PI);
        goldText.color = Color.Lerp(textBaseColor, countUpColor, k >= 1f ? 0f : Mathf.Max(glow, 0.6f));
        if (punchCo == null) goldText.rectTransform.localScale = textBaseScale * (k >= 1f ? 1f : Mathf.Lerp(1f, countUpScale, glow));
        if (k >= 1f)
        {
            counting = false;
            goldText.color = textBaseColor;
            if (punchCo == null) goldText.rectTransform.localScale = textBaseScale;
        }
    }

    private void OnEnable()
    {
        if (showSessionGold) SessionInstance = this;
    }

    private void OnDisable()
    {
        if (SessionInstance == this) SessionInstance = null;
    }

    /// <summary>Goldアイコンの位置をワールド座標（z=0の面）で返す</summary>
    public Vector3 IconWorldPosition()
    {
        RectTransform rt = goldIcon != null ? goldIcon.rectTransform : (RectTransform)transform;
        Camera cam = Camera.main;
        if (cam == null) return rt.position;
        Canvas canvas = GetComponentInParent<Canvas>();
        Canvas root = canvas != null ? canvas.rootCanvas : null;
        if (root != null && root.renderMode == RenderMode.WorldSpace) return rt.position;
        Camera uiCam = (root != null && root.renderMode == RenderMode.ScreenSpaceCamera) ? root.worldCamera : null;
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(uiCam, rt.position);
        Vector3 w = cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, -cam.transform.position.z));
        w.z = 0f;
        return w;
    }

    /// <summary>Gold表示をポンと弾ませる（アイテムのGoldが着いた時）</summary>
    public void Punch(float scale)
    {
        if (!isActiveAndEnabled) return;
        if (punchCo != null) StopCoroutine(punchCo);
        punchCo = StartCoroutine(PunchRoutine(scale, 0.22f));
    }

    private System.Collections.IEnumerator PunchRoutine(float scale, float duration)
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            float s = Mathf.Lerp(scale, 1f, 1f - (1f - k) * (1f - k));
            if (goldIcon != null) goldIcon.rectTransform.localScale = iconBaseScale * s;
            if (goldText != null) goldText.rectTransform.localScale = textBaseScale * s;
            yield return null;
        }
        if (goldIcon != null) goldIcon.rectTransform.localScale = iconBaseScale;
        if (goldText != null) goldText.rectTransform.localScale = textBaseScale;
        punchCo = null;
    }

    private void Start()
    {
        if (GoldManager.Instance == null)
        {
            Debug.LogWarning("[GoldHUD] GoldManager.Instance is null. Gold display will not update.");
            return;
        }

        GoldManager.Instance.OnSessionGoldChanged += OnSessionGoldChanged;
        GoldManager.Instance.OnPersistentGoldChanged += OnPersistentGoldChanged;

        // 初期表示
        int initial = showSessionGold ? GoldManager.Instance.SessionGold : GoldManager.Instance.PersistentGold;
        UpdateText(initial);
    }

    private void OnDestroy()
    {
        if (GoldManager.Instance != null)
        {
            GoldManager.Instance.OnSessionGoldChanged -= OnSessionGoldChanged;
            GoldManager.Instance.OnPersistentGoldChanged -= OnPersistentGoldChanged;
        }
    }

    private void OnSessionGoldChanged(int amount)
    {
        if (!showSessionGold) return;
        // ③ 増えた時だけ数字を増やしていく（減った時・初期表示は従来どおりすぐ切り替える）
        if (countUpEnabled && goldText != null && countShownInt != int.MinValue && amount > countTarget)
        {
            countFrom = counting ? countShown : countTarget;
            countTarget = amount;
            if (!counting) countStartTime = Mathf.Max(countStartTime, Time.unscaledTime);
            counting = true;
            return;
        }
        UpdateText(amount);
    }

    private void OnPersistentGoldChanged(int amount)
    {
        if (!showSessionGold) UpdateText(amount);
    }

    private void UpdateText(int amount)
    {
        countTarget = amount;
        countShown = amount;
        countShownInt = amount;
        counting = false;
        if (goldText != null)
            goldText.text = amount.ToString();
        if (goldText != null && countUpEnabled && showSessionGold) goldText.color = textBaseColor; // 増えている途中で切り替わった時に光ったままにならないよう戻す
    }
}
