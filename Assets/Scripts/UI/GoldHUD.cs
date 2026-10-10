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

    /// <summary>05_Gameのセッションゴールド表示（アイテムのGoldが飛んでいく先。ItemFXManager）</summary>
    public static GoldHUD SessionInstance { get; private set; }

    private Coroutine punchCo;
    private Vector3 iconBaseScale = Vector3.one, textBaseScale = Vector3.one;

    private void Awake()
    {
        if (goldIcon != null) iconBaseScale = goldIcon.rectTransform.localScale;
        if (goldText != null) textBaseScale = goldText.rectTransform.localScale;
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
        if (showSessionGold) UpdateText(amount);
    }

    private void OnPersistentGoldChanged(int amount)
    {
        if (!showSessionGold) UpdateText(amount);
    }

    private void UpdateText(int amount)
    {
        if (goldText != null)
            goldText.text = amount.ToString();
    }
}
