using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Stage3クリア後のResult統計スクリーン
/// GemRewardUI の Phase2 終了後に Show() を呼ぶ。
/// タップで onComplete を発火してシーン遷移へ続く。
///
/// 事前に [ContextMenu("Setup Result Screen UI")] でHierarchyを生成すること。
/// </summary>
public class ResultScreenUI : MonoBehaviour
{
    // ===== Panel =====

    [Header("Panel")]
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private float fadeInDuration   = 0.4f;
    [SerializeField] private float fadeOutDuration  = 0.4f;
    [Tooltip("フェードイン完了後、タップ受付を開始するまでの待機時間（連打防止）")]
    [SerializeField] private float tapEnableDelay   = 0.5f;
    [Tooltip("SkillHUD分のX軸オフセット（skillHudWidth / 2）")]
    [SerializeField] private float hudOffset = 140f;

    // ===== Reflect =====

    [Header("反射")]
    [SerializeField] private TextMeshProUGUI normalReflectText;
    [SerializeField] private TextMeshProUGUI justReflectText;
    [SerializeField] private TextMeshProUGUI justRateText;

    // ===== Damage =====

    [Header("ダメージ")]
    [SerializeField] private TextMeshProUGUI hpDamageText;
    [SerializeField] private TextMeshProUGUI shieldDamageText;
    [SerializeField] private TextMeshProUGUI blockDamageText;
    [SerializeField] private TextMeshProUGUI damageTakenText;

    // ===== Other =====

    [Header("その他")]
    [SerializeField] private TextMeshProUGUI overheatText;
    [SerializeField] private TextMeshProUGUI goldText;

    [Header("SE")]
    [SerializeField] private AudioClip tapSE;
    [SerializeField] [Range(0f, 1f)] private float tapSEVolume = 1f;

    // ===== Runtime =====

    private Action onClose;
    private CanvasGroup panelCg;
    private bool isWaitingForTap = false;
    private AudioSource audioSource;

    // =====================================================
    // Lifecycle
    // =====================================================

    private void Awake()
    {
        if (resultPanel != null)
        {
            panelCg = resultPanel.GetComponent<CanvasGroup>();
            if (panelCg == null) panelCg = resultPanel.AddComponent<CanvasGroup>();
            // タップ検知は Update() の Input 直接検知で行うため Button.onClick は使わない
        }

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        Hide();
    }

    // UI EventSystem に依存せず Input を直接監視
    private void Update()
    {
        if (!isWaitingForTap) return;

        bool tapped = Input.GetMouseButtonDown(0)
            || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);
        if (tapped)
            OnTapClose();
    }

    // =====================================================
    // Public API
    // =====================================================

    /// <summary>Resultスクリーンを表示する。任意の場所をタップ後に onComplete を発火。</summary>
    public void Show(Action onComplete)
    {
        onClose = onComplete;
        PopulateStats();
        resultPanel?.SetActive(true);
        StartCoroutine(ShowCoroutine());
    }

    private IEnumerator ShowCoroutine()
    {
        yield return StartCoroutine(FadeInCoroutine());
        // フェードイン完了後、tapEnableDelay 秒待ってからタップ受付（連打防止）
        if (tapEnableDelay > 0f)
            yield return new WaitForSecondsRealtime(tapEnableDelay);
        else
            yield return null;
        isWaitingForTap = true;
    }

    private void OnTapClose()
    {
        isWaitingForTap = false;
        PlayTapSE();
        StartCoroutine(FadeOutAndClose());
    }

    private IEnumerator FadeOutAndClose()
    {
        if (panelCg != null && fadeOutDuration > 0f)
        {
            float elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                panelCg.alpha = Mathf.Clamp01(1f - elapsed / fadeOutDuration);
                yield return null;
            }
            panelCg.alpha = 0f;
        }
        Hide();
        var cb = onClose;
        onClose = null;
        cb?.Invoke();
    }

    private void PlayTapSE()
    {
        if (audioSource == null || tapSE == null) return;
        float vol = tapSEVolume * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
        audioSource.PlayOneShot(tapSE, vol);
    }

    // =====================================================
    // Private
    // =====================================================

    private void PopulateStats()
    {
        int justPct = Mathf.RoundToInt(SessionStats.JustRate * 100f);

        SetText(normalReflectText, $"通常　{SessionStats.NormalReflectCount:N0}");
        SetText(justReflectText,   $"ジャスト　{SessionStats.JustReflectCount:N0}");
        SetText(justRateText,      $"JUST　{justPct}%");
        SetText(hpDamageText,      $"HP　{SessionStats.HpDamageDealt:N0}");
        SetText(shieldDamageText,  $"Shield　{SessionStats.ShieldDamageDealt:N0}");
        SetText(blockDamageText,   $"Block　{SessionStats.BlockDamageDealt:N0}");
        SetText(damageTakenText,   $"被ダメ　{SessionStats.DamageTaken:N0}");
        SetText(overheatText,      $"OH　{SessionStats.OverheatCount}回");
        SetText(goldText,          $"Gold　+{SessionStats.GoldEarned:N0}");
    }

    private static void SetText(TextMeshProUGUI tmp, string text)
    {
        if (tmp != null) tmp.text = text;
    }

    private IEnumerator FadeInCoroutine()
    {
        if (panelCg == null) yield break;
        panelCg.alpha = 0f;
        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            panelCg.alpha = Mathf.Clamp01(elapsed / fadeInDuration);
            yield return null;
        }
        panelCg.alpha = 1f;
    }

    private void Hide()
    {
        resultPanel?.SetActive(false);
    }

    // =====================================================
    // ContextMenu Setup（初回一度だけ実行）
    // =====================================================

#if UNITY_EDITOR
    [ContextMenu("Setup Result Screen UI")]
    private void SetupResultScreenUI()
    {
        // この GameObject 自身の RT をフルスクリーンに（親=GemRewardUIに合わせてストレッチ）
        var myRT = GetComponent<RectTransform>();
        if (myRT != null)
        {
            myRT.anchorMin = Vector2.zero;
            myRT.anchorMax = Vector2.one;
            myRT.offsetMin = Vector2.zero;
            myRT.offsetMax = Vector2.zero;
            UnityEditor.EditorUtility.SetDirty(gameObject);
        }

        // 既存子を全削除
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        var so = new UnityEditor.SerializedObject(this);

        // ---- ResultPanel（フルスクリーン・全面タップ受け取り） ----
        var panel = new GameObject("ResultPanel");
        panel.transform.SetParent(transform, false);

        var panelRT = panel.AddComponent<RectTransform>();
        panelRT.anchorMin = Vector2.zero;
        panelRT.anchorMax = Vector2.one;
        panelRT.offsetMin = Vector2.zero;
        panelRT.offsetMax = Vector2.zero;

        panel.AddComponent<CanvasGroup>();

        var tapImg = panel.AddComponent<Image>();
        tapImg.color = Color.clear;
        tapImg.raycastTarget = true;

        var btn = panel.AddComponent<Button>();
        btn.targetGraphic = tapImg;
        // onClick は Awake() でコードから登録するため、ここでは設定不要

        so.FindProperty("resultPanel").objectReferenceValue = panel;

        // ---- ContentBg（実際の横長パネル・すべての子要素の親） ----
        // フォントサイズ変更に伴い高さを440に拡張
        var content = new GameObject("ContentBg");
        content.transform.SetParent(panel.transform, false);
        var contentRT = content.AddComponent<RectTransform>();
        contentRT.anchorMin = new Vector2(0.5f, 0.5f);
        contentRT.anchorMax = new Vector2(0.5f, 0.5f);
        contentRT.pivot     = new Vector2(0.5f, 0.5f);
        contentRT.sizeDelta = new Vector2(1260f, 440f);
        contentRT.anchoredPosition = new Vector2(hudOffset, 200f);
        var contentImg = content.AddComponent<Image>();
        contentImg.color = new Color(0.07f, 0.07f, 0.16f, 0.95f);
        contentImg.raycastTarget = false;

        Transform ct = content.transform;

        // ---- レイアウト定数 ----
        // ContentBg: 1260 x 440 (top=+220, bottom=-220)
        // RESULT(font40,h54): center y=185 → bottom=158
        // SepTop: y=147 (9px gap below RESULT)
        // Header(font32,h44): y=113 (12px below sep + 22 half-height)
        // Row1(font28,h40): y=63 (8px below header + 22+20)
        // Row2: y=15 (Row1 bottom=43, gap8, center=43-8-20=15)
        // Row3: y=-33 (Row2 bottom=-5, gap8, center=-5-8-20=-33)
        // Row4(center only): y=-81 (Row3 bottom=-53, gap8, center=-53-8-20=-81)
        // DivLeft/Right: top=147, bottom=-101(row4 bottom -81-20), h=248, center=23

        // ---- "RESULT" タイトル（font40・中央上部） ----
        CreateTMP("TitleLabel", ct, "RESULT", 40f,
            new Vector2(0f, 185f), new Vector2(600f, 54f),
            new Color(0.55f, 0.55f, 0.65f, 1f), TextAlignmentOptions.Center);

        // ---- 上部セパレーター ----
        CreateBox("SepTop", ct, new Vector2(1220f, 1f), new Vector2(0f, 147f),
            new Color(1f, 1f, 1f, 0.15f), false);

        // ---- 縦区切り線（SepTop〜Row4下端をカバー） ----
        CreateBox("DivLeft",  ct, new Vector2(1f, 248f), new Vector2(-210f, 23f), new Color(1f,1f,1f,0.15f), false);
        CreateBox("DivRight", ct, new Vector2(1f, 248f), new Vector2( 210f, 23f), new Color(1f,1f,1f,0.15f), false);

        // ---- 左列：反射 ----
        float lx = -420f;
        CreateTMP("HeaderLeft",         ct, "反射",      32f, new Vector2(lx, 113f), new Vector2(380f, 44f), new Color(0.6f,0.6f,0.7f,1f), TextAlignmentOptions.Center);
        var t1 = CreateTMP("NormalReflectText", ct, "通常　-",     28f, new Vector2(lx,  63f), new Vector2(380f, 40f), Color.white, TextAlignmentOptions.Center);
        var t2 = CreateTMP("JustReflectText",   ct, "ジャスト　-", 28f, new Vector2(lx,  15f), new Vector2(380f, 40f), Color.white, TextAlignmentOptions.Center);
        var t3 = CreateTMP("JustRateText",      ct, "JUST　-",     28f, new Vector2(lx, -33f), new Vector2(380f, 40f), Color.white, TextAlignmentOptions.Center);
        so.FindProperty("normalReflectText").objectReferenceValue = t1;
        so.FindProperty("justReflectText").objectReferenceValue   = t2;
        so.FindProperty("justRateText").objectReferenceValue      = t3;

        // ---- 中列：ダメージ ----
        float mx = 0f;
        CreateTMP("HeaderCenter",      ct, "ダメージ",  32f, new Vector2(mx, 113f), new Vector2(380f, 44f), new Color(0.6f,0.6f,0.7f,1f), TextAlignmentOptions.Center);
        var t4 = CreateTMP("HpDamageText",     ct, "HP　-",     28f, new Vector2(mx,  63f), new Vector2(380f, 40f), Color.white, TextAlignmentOptions.Center);
        var t5 = CreateTMP("ShieldDamageText", ct, "Shield　-", 28f, new Vector2(mx,  15f), new Vector2(380f, 40f), Color.white, TextAlignmentOptions.Center);
        var t6 = CreateTMP("BlockDamageText",  ct, "Block　-",  28f, new Vector2(mx, -33f), new Vector2(380f, 40f), Color.white, TextAlignmentOptions.Center);
        var t7 = CreateTMP("DamageTakenText",  ct, "被ダメ　-", 28f, new Vector2(mx, -81f), new Vector2(380f, 40f), Color.white, TextAlignmentOptions.Center);
        so.FindProperty("hpDamageText").objectReferenceValue      = t4;
        so.FindProperty("shieldDamageText").objectReferenceValue  = t5;
        so.FindProperty("blockDamageText").objectReferenceValue   = t6;
        so.FindProperty("damageTakenText").objectReferenceValue   = t7;

        // ---- 右列：その他 ----
        float rx = 420f;
        CreateTMP("HeaderRight",   ct, "その他",  32f, new Vector2(rx, 113f), new Vector2(380f, 44f), new Color(0.6f,0.6f,0.7f,1f), TextAlignmentOptions.Center);
        var t8 = CreateTMP("OverheatText", ct, "OH　-",   28f, new Vector2(rx, 63f), new Vector2(380f, 40f), Color.white, TextAlignmentOptions.Center);
        var t9 = CreateTMP("GoldText",     ct, "Gold　-", 28f, new Vector2(rx, 15f), new Vector2(380f, 40f), Color.white, TextAlignmentOptions.Center);
        so.FindProperty("overheatText").objectReferenceValue = t8;
        so.FindProperty("goldText").objectReferenceValue     = t9;

        so.ApplyModifiedProperties();
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[ResultScreenUI] Setup complete!");
    }

    // ---- helpers ----

    private static GameObject CreateBox(string name, Transform parent, Vector2 size, Vector2 pos, Color color, bool raycast)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size;
        rt.anchoredPosition = pos;
        var img = obj.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = raycast;
        return obj;
    }

    private static TextMeshProUGUI CreateTMP(string name, Transform parent, string text, float fontSize,
        Vector2 pos, Vector2 size, Color color, TextAlignmentOptions align)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        var rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var tmp = obj.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = fontSize;
        tmp.color     = color;
        tmp.alignment = align;
        tmp.enableWordWrapping = false;
        tmp.raycastTarget = false;
        return tmp;
    }
#endif
}
