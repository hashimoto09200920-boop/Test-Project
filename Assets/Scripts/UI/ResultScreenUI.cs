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

    // ===== Combat =====

    [Header("コンバット")]
    [SerializeField] private TextMeshProUGUI killsText;
    [SerializeField] private TextMeshProUGUI damageTakenText;
    [SerializeField] private TextMeshProUGUI blocksText;

    // ===== Other =====

    [Header("その他")]
    [SerializeField] private TextMeshProUGUI downsText;
    [SerializeField] private TextMeshProUGUI overheatText;
    [SerializeField] private TextMeshProUGUI goldText;

    // ===== Rank =====

    [Header("ランク")]
    [SerializeField] private TextMeshProUGUI rankText;

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
        gameObject.SetActive(true);
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

        SetText(normalReflectText, $"{SessionStats.NormalReflectCount:N0}");
        SetText(justReflectText,   $"{SessionStats.JustReflectCount:N0}");
        SetText(justRateText,      $"{justPct}%");
        SetText(killsText,         $"{SessionStats.EnemyKillCount}");
        SetText(damageTakenText,   $"{SessionStats.DamageTaken:N0}");
        SetText(blocksText,        $"{SessionStats.BlockDestroyCount}");
        SetText(downsText,         $"{SessionStats.DownCount}");
        SetText(overheatText,      $"{SessionStats.OverheatCount}");
        SetText(goldText,          $"+{SessionStats.GoldEarned:N0}");

        string rank = CalcRank();
        if (rankText != null)
        {
            rankText.text  = rank;
            rankText.color = GetRankColor(rank);
        }
    }

    // =====================================================
    // Rank 計算
    // =====================================================

    private static int CalcRankScore()
    {
        // Just% (30pt)
        float justPct = SessionStats.JustRate * 100f;
        int justPt = justPct >= 80f ? 30 :
                     justPct >= 65f ? 23 :
                     justPct >= 50f ? 15 :
                     justPct >= 35f ? 7  : 0;

        // KILLS (25pt)
        int kills   = SessionStats.EnemyKillCount;
        int killsPt = kills >= 20 ? 25 :
                      kills >= 15 ? 18 :
                      kills >= 10 ? 10 :
                      kills >= 5  ? 4  : 0;

        // RECEIVED (20pt)
        int dmg   = SessionStats.DamageTaken;
        int dmgPt = dmg == 0  ? 20 :
                    dmg <= 2  ? 14 :
                    dmg <= 8  ? 8  :
                    dmg <= 20 ? 3  : 0;

        // OVERHEAT (12pt)
        int oh   = SessionStats.OverheatCount;
        int ohPt = oh == 0 ? 12 :
                   oh == 1 ? 7  :
                   oh == 2 ? 4  :
                   oh <= 4 ? 1  : 0;

        // DOWNS (8pt)
        int downs   = SessionStats.DownCount;
        int downsPt = downs == 0 ? 8 :
                      downs == 1 ? 5 :
                      downs == 2 ? 2 : 0;

        // BLOCKS (5pt)
        int blocks   = SessionStats.BlockDestroyCount;
        int blocksPt = blocks >= 12 ? 5 :
                       blocks >= 9  ? 3 :
                       blocks >= 6  ? 1 : 0;

        return justPt + killsPt + dmgPt + ohPt + downsPt + blocksPt;
    }

    private static string CalcRank()
    {
        int score = CalcRankScore();
        if (score >= 90) return "S";
        if (score >= 75) return "A";
        if (score >= 58) return "B";
        if (score >= 40) return "C";
        if (score >= 20) return "D";
        return "E";
    }

    private static Color GetRankColor(string rank) => rank switch
    {
        "S" => new Color(1.0f, 0.84f, 0.0f),
        "A" => new Color(0.0f, 0.90f, 1.0f),
        "B" => new Color(0.2f, 0.90f, 0.3f),
        "C" => new Color(1.0f, 0.90f, 0.2f),
        "D" => new Color(1.0f, 0.50f, 0.1f),
        _   => new Color(0.7f, 0.30f, 0.3f),
    };

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

        // ---- "RESULT" タイトル（font44・左寄せ、Rank表示分を右に空ける） ----
        CreateTMP("TitleLabel", ct, "RESULT", 44f,
            new Vector2(-160f, 170f), new Vector2(700f, 56f),
            Color.white, TextAlignmentOptions.Center);

        // ---- Rank テキスト（font52・右上） ----
        var tRank = CreateTMP("RankText", ct, "S", 52f,
            new Vector2(470f, 170f), new Vector2(180f, 64f),
            new Color(1.0f, 0.84f, 0.0f), TextAlignmentOptions.Center);
        so.FindProperty("rankText").objectReferenceValue = tRank;

        // ---- レイアウト定数（3行均等配分） ----
        const float hy  =  92f;   // Header y
        const float r1y =  20f;   // Row1 y
        const float r2y = -55f;   // Row2 y
        const float r3y = -130f;  // Row3 y

        // 列ごとのラベル(左揃え)・数値(右揃え) 幅・オフセット
        const float lw = 220f; const float vw = 110f;

        // ---- 左列：REFLECT ----
        float lx = -420f;
        const float lLo = -40f; const float lVo = 115f;
        var hColor = new Color(0.45f, 0.85f, 1f, 1f);
        CreateTMP("HeaderLeft", ct, "REFLECT", 36f, new Vector2(lx, hy), new Vector2(380f, 46f), hColor, TextAlignmentOptions.Center);
        CreateTMP("NormalLabel",   ct, "NORMAL",   30f, new Vector2(lx+lLo, r1y), new Vector2(lw, 40f), Color.white, TextAlignmentOptions.Left);
        CreateTMP("JustLabel",     ct, "JUST",     30f, new Vector2(lx+lLo, r2y), new Vector2(lw, 40f), Color.white, TextAlignmentOptions.Left);
        CreateTMP("AccuracyLabel", ct, "ACCURACY", 30f, new Vector2(lx+lLo, r3y), new Vector2(lw, 40f), Color.white, TextAlignmentOptions.Left);
        var t1 = CreateTMP("NormalReflectText", ct, "-", 30f, new Vector2(lx+lVo, r1y), new Vector2(vw, 40f), Color.white, TextAlignmentOptions.Right);
        var t2 = CreateTMP("JustReflectText",   ct, "-", 30f, new Vector2(lx+lVo, r2y), new Vector2(vw, 40f), Color.white, TextAlignmentOptions.Right);
        var t3 = CreateTMP("JustRateText",      ct, "-", 30f, new Vector2(lx+lVo, r3y), new Vector2(vw, 40f), Color.white, TextAlignmentOptions.Right);
        so.FindProperty("normalReflectText").objectReferenceValue = t1;
        so.FindProperty("justReflectText").objectReferenceValue   = t2;
        so.FindProperty("justRateText").objectReferenceValue      = t3;

        // ---- 中列：COMBAT ----
        float mx = 0f;
        const float mLo = -50f; const float mVo = 105f;
        CreateTMP("HeaderCenter", ct, "COMBAT", 36f, new Vector2(mx, hy), new Vector2(380f, 46f), hColor, TextAlignmentOptions.Center);
        CreateTMP("KillsLabel",    ct, "KILLS",    30f, new Vector2(mx+mLo, r1y), new Vector2(lw, 40f), Color.white, TextAlignmentOptions.Left);
        CreateTMP("ReceivedLabel", ct, "RECEIVED", 30f, new Vector2(mx+mLo, r2y), new Vector2(lw, 40f), Color.white, TextAlignmentOptions.Left);
        CreateTMP("BlocksLabel",   ct, "BLOCKS",   30f, new Vector2(mx+mLo, r3y), new Vector2(lw, 40f), Color.white, TextAlignmentOptions.Left);
        var t4 = CreateTMP("KillsText",       ct, "-", 30f, new Vector2(mx+mVo, r1y), new Vector2(vw, 40f), Color.white, TextAlignmentOptions.Right);
        var t5 = CreateTMP("DamageTakenText", ct, "-", 30f, new Vector2(mx+mVo, r2y), new Vector2(vw, 40f), Color.white, TextAlignmentOptions.Right);
        var t6 = CreateTMP("BlocksText",      ct, "-", 30f, new Vector2(mx+mVo, r3y), new Vector2(vw, 40f), Color.white, TextAlignmentOptions.Right);
        so.FindProperty("killsText").objectReferenceValue         = t4;
        so.FindProperty("damageTakenText").objectReferenceValue   = t5;
        so.FindProperty("blocksText").objectReferenceValue        = t6;

        // ---- 右列：OTHER ----
        float rx = 420f;
        const float rLo = -60f; const float rVo = 85f;
        CreateTMP("HeaderRight", ct, "OTHER", 36f, new Vector2(rx, hy), new Vector2(380f, 46f), hColor, TextAlignmentOptions.Center);
        CreateTMP("DownsLabel",    ct, "DOWNS",    30f, new Vector2(rx+rLo, r1y), new Vector2(lw, 40f), Color.white, TextAlignmentOptions.Left);
        CreateTMP("OverheatLabel", ct, "OVERHEAT", 30f, new Vector2(rx+rLo, r2y), new Vector2(lw, 40f), Color.white, TextAlignmentOptions.Left);
        CreateTMP("GoldLabel",     ct, "GOLD",     30f, new Vector2(rx+rLo, r3y), new Vector2(lw, 40f), Color.white, TextAlignmentOptions.Left);
        var t7  = CreateTMP("DownsText",    ct, "-", 30f, new Vector2(rx+rVo, r1y), new Vector2(vw, 40f), Color.white, TextAlignmentOptions.Right);
        var t8  = CreateTMP("OverheatText", ct, "-", 30f, new Vector2(rx+rVo, r2y), new Vector2(vw, 40f), Color.white, TextAlignmentOptions.Right);
        var t9  = CreateTMP("GoldText",     ct, "-", 30f, new Vector2(rx+rVo, r3y), new Vector2(vw, 40f), Color.white, TextAlignmentOptions.Right);
        so.FindProperty("downsText").objectReferenceValue    = t7;
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
        tmp.text       = text;
        tmp.fontSize   = fontSize;
        tmp.color      = color;
        tmp.alignment  = align;
        tmp.fontStyle  = FontStyles.Bold;
        tmp.enableWordWrapping = false;
        tmp.raycastTarget = false;
        return tmp;
    }
#endif
}
