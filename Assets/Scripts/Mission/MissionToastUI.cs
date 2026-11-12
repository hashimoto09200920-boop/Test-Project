using System.Collections;
using System.Collections.Generic;
using Game.Localization;
using TMPro;
using UnityEngine;

/// <summary>
/// プレイ中にミッションを達成した時の通知（画面上部・05_Game）。
/// ・上から入ってきて表示し、上へ消える。操作は止めない・タップも受けない
/// ・複数達成した時は1件ずつ順番に出す
/// ・VS演出・カットイン・3択・一時停止など、時間が止まっている間（Time.timeScale==0）は待つ
/// ・Result画面が出たら、残っている通知は捨てる（Result画面の「MISSION CLEAR」枠に出るため）
/// 作成は Tools > ミッション > 9。位置・大きさ・色・秒数はInspectorで調整。
/// </summary>
public class MissionToastUI : MonoBehaviour
{
    [Header("参照（メニュー9で自動設定）")]
    [SerializeField] private RectTransform toastRoot;
    [SerializeField] private CanvasGroup toastGroup;
    [SerializeField] private TMP_Text tagText;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text progressText;

    [Header("文字")]
    [SerializeField] private string tagLabel = "MISSION CLEAR";
    [Tooltip("達成数の表示（{0}=目標の回数）")]
    [SerializeField] private string progressFormat = "{0} / {0}";
    [Tooltip("お知らせ（次のエリアの解放・スロット+1）の通知の上の文字（2026/11/11）")]
    [SerializeField] private string noticeTagLabel = "REWARD";

    [Header("動き（秒・実時間）")]
    [Tooltip("表示位置（toastRootのanchoredPosition）。上の画面外からここへ入ってくる")]
    [SerializeField] private Vector2 shownPosition = new Vector2(140f, -60f);
    [Tooltip("入ってくる距離（上へずらした量）")]
    [SerializeField] private float slideDistance = 160f;
    [SerializeField] private float inSeconds = 0.3f;
    [SerializeField] private float holdSeconds = 2.2f;
    [SerializeField] private float outSeconds = 0.3f;
    [Tooltip("続けて出す時の間の秒数")]
    [SerializeField] private float gapSeconds = 0.2f;

    [Header("効果音")]
    [SerializeField] private AudioClip toastSE;
    [SerializeField] [Range(0f, 1f)] private float toastSEVolume = 1f;

    // 通知1枚分（ミッション達成、または達成の後に続けて出すお知らせ1行）
    private struct ToastItem { public MissionDefinition def; public string noticeLine; }
    private readonly Queue<ToastItem> queue = new Queue<ToastItem>();
    private bool playing;
    private AudioSource audioSource;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) { audioSource = gameObject.AddComponent<AudioSource>(); audioSource.playOnAwake = false; }
        if (progressText != null) GameNumberFont.Apply(progressText);
        HideNow();
    }

    private void OnEnable()
    {
        MissionProgressManager.OnMissionAchieved += Enqueue;
        MissionProgressManager.OnResultShownEvent += ClearAll;
    }

    private void OnDisable()
    {
        MissionProgressManager.OnMissionAchieved -= Enqueue;
        MissionProgressManager.OnResultShownEvent -= ClearAll;
    }

    private void Enqueue(MissionDefinition def)
    {
        if (def == null || GameSession.IsInTutorial) return;
        // 解放のミッション（お知らせだけ・自動で受取済）は「MISSION CLEAR」の通知を出さず、お知らせの行だけ出す（2026/11/11 同じ名前の通知が続いて見づらかったため）
        if (def.rewardType != MissionRewardType.Notice) queue.Enqueue(new ToastItem { def = def });
        // お知らせ（次のエリアの解放・ジェムの装備スロット+1）は、達成の通知の後に1行ずつ通知で出す（2026/11/11）
        bool en = LocalizationManager.CurrentLanguageStatic == GameLanguage.English;
        foreach (var line in def.GetNoticeLines(en))
            if (!string.IsNullOrEmpty(line)) queue.Enqueue(new ToastItem { def = def, noticeLine = line });
    }

    private void ClearAll()
    {
        queue.Clear();
        StopAllCoroutines();
        playing = false;
        HideNow();
    }

    private void HideNow()
    {
        if (toastGroup != null) toastGroup.alpha = 0f;
        if (toastRoot != null) toastRoot.gameObject.SetActive(false);
    }

    /// <summary>今、通知を進めてよいか（時間が止まっている演出・画面の間は待つ）</summary>
    private static bool CanShow => Time.timeScale > 0f && !Game.UI.SkillSelectionUI.IsShowing;

    private void Update()
    {
        if (!playing && queue.Count > 0 && CanShow) StartCoroutine(PlayRoutine(queue.Dequeue()));
    }

    private IEnumerator PlayRoutine(ToastItem item)
    {
        playing = true;
        var def = item.def;
        bool en = LocalizationManager.CurrentLanguageStatic == GameLanguage.English;
        bool isNotice = !string.IsNullOrEmpty(item.noticeLine);
        if (tagText != null) tagText.text = isNotice ? noticeTagLabel : tagLabel;
        if (nameText != null) nameText.text = isNotice ? item.noticeLine : def.GetDisplayName(en);
        if (progressText != null) progressText.text = isNotice ? "" : string.Format(progressFormat, def.target);
        if (toastRoot != null) toastRoot.gameObject.SetActive(true);

        if (toastSE != null && audioSource != null)
        {
            float vol = toastSEVolume * (SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f);
            audioSource.PlayOneShot(toastSE, vol);
        }

        float total = inSeconds + holdSeconds + outSeconds, t = 0f;
        while (t < total)
        {
            // 途中で時間が止まる演出が始まったら、その間は隠して待つ
            if (!CanShow) { if (toastGroup != null) toastGroup.alpha = 0f; yield return null; continue; }
            t += Time.unscaledDeltaTime;
            float k; // 0=上の画面外、1=表示位置
            if (t < inSeconds) k = EaseOut(t / Mathf.Max(0.01f, inSeconds));
            else if (t < inSeconds + holdSeconds) k = 1f;
            else k = 1f - EaseIn((t - inSeconds - holdSeconds) / Mathf.Max(0.01f, outSeconds));
            if (toastRoot != null) toastRoot.anchoredPosition = shownPosition + Vector2.up * slideDistance * (1f - k);
            if (toastGroup != null) toastGroup.alpha = k;
            yield return null;
        }
        HideNow();
        if (gapSeconds > 0f) yield return new WaitForSecondsRealtime(gapSeconds);
        playing = false;
    }

    private static float EaseOut(float x) { x = Mathf.Clamp01(x); return 1f - (1f - x) * (1f - x); }
    private static float EaseIn(float x) { x = Mathf.Clamp01(x); return x * x; }

#if UNITY_EDITOR
    [ContextMenu("通知を表示して確認（Play前）")]
    private void PreviewShow()
    {
        if (toastRoot == null) return;
        toastRoot.gameObject.SetActive(true);
        toastRoot.anchoredPosition = shownPosition;
        if (toastGroup != null) toastGroup.alpha = 1f;
        if (tagText != null) tagText.text = tagLabel;
        if (nameText != null) nameText.text = "ソウルを100体救出する";
        if (progressText != null) progressText.text = string.Format(progressFormat, 100);
        UnityEditor.EditorUtility.SetDirty(toastRoot.gameObject);
    }

    [ContextMenu("通知を隠す")]
    private void PreviewHide()
    {
        HideNow();
        if (toastRoot != null) UnityEditor.EditorUtility.SetDirty(toastRoot.gameObject);
    }
#endif
}
