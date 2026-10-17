using Game.Progress;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// 03_AreaSelectの見やすさ改善（1回目）。
    /// ② スタミナが0の時：数字を赤く、「あと mm:ss」を目立たせる（下地も赤みを帯びる）。「あと mm:ss」の下地は文字の長さに合わせる
    /// ④ 解放済みでまだクリアしていないAreaのうち一番先のノードに「NEXT」タグを出し、点滅しながら拡大・縮小を繰り返す
    ///   （ランクバッジの位置。未クリアのAreaにはランクが無いため空いている）
    /// ⑤ 装備中のジェムに残り1回（次のプレイで消える）のものがあれば、ジェムボタンに「！」を出し、点滅しながら拡大・縮小を繰り返す
    /// ①（所持数の暗い下地）は動かない部品なのでメニューで作るだけ。
    /// （③ロック中Areaの解放条件はユーザー判断で不要となり削除）
    /// ★進行状況・ジェムは数秒おきに確認し、変わった時だけ見た目を書き換える。毎フレーム動かすのは「NEXT」「！」が出ている間だけ。
    /// 部品はメニュー「Tools/エリア選択画面/見やすさ改善（1回目）を組み込む（03_AreaSelect）」でPlay前に生成・参照設定する。
    /// </summary>
    public class AreaSelectGuideFX : MonoBehaviour
    {
        [System.Serializable]
        public class Node
        {
            public StageButton stage;
            [Tooltip("次に挑むAreaに出すタグ")]
            public GameObject nextTag;
        }

        [Header("参照（メニューで自動設定）")]
        [SerializeField, NonReorderable] private Node[] nodes;
        [SerializeField] private RectTransform gemAlertBadge;
        [SerializeField] private CanvasGroup gemAlertGroup;
        [SerializeField] private Image gemAlertGlow;
        [SerializeField] private Image staminaPlate;
        [SerializeField] private TextMeshProUGUI staminaCountText;
        [SerializeField] private TextMeshProUGUI staminaCountdownText;
        [SerializeField] private Image countdownPlate;

        [Header("② スタミナ0の時")]
        [SerializeField] private Color emptyCountColor = new Color(1f, 0.35f, 0.35f, 1f);
        [SerializeField] private Color emptyCountdownColor = new Color(1f, 0.85f, 0.35f, 1f);
        [SerializeField] private Color emptyPlateColor = new Color(0.3f, 0.04f, 0.07f, 0.85f);
        [Tooltip("「あと mm:ss」の下地の、文字の左右の余白(px)")]
        [SerializeField] private float countdownPlatePadding = 14f;

        [Header("④ NEXTの動き")]
        [Tooltip("点滅・拡大縮小の速さ（1秒あたりの回数）")]
        [SerializeField] private float nextPulsePerSecond = 1.6f;
        [SerializeField] private float nextScaleMin = 0.9f;
        [SerializeField] private float nextScaleMax = 1.05f;
        [Tooltip("点滅で一番暗くなった時の明るさ")]
        [SerializeField, Range(0f, 1f)] private float nextAlphaMin = 0.35f;

        [Header("⑤ ジェムの「！」の動き")]
        [Tooltip("点滅・拡大縮小の速さ（1秒あたりの回数）")]
        [SerializeField] private float alertPulsePerSecond = 1.6f;
        [SerializeField] private float alertScaleMin = 0.88f;
        [SerializeField] private float alertScaleMax = 1.18f;
        [Tooltip("点滅で一番暗くなった時の明るさ")]
        [SerializeField, Range(0f, 1f)] private float alertAlphaMin = 0.35f;
        [Tooltip("後ろの光の明るさ（暗い時〜明るい時）")]
        [SerializeField] private Vector2 alertGlowAlpha = new Vector2(0.15f, 0.75f);

        [Header("確認の間隔")]
        [Tooltip("進行状況（NEXT）・ジェムの残り回数（！）を確認し直す間隔（秒）。デバッグボタンで進行状況を変えた時にも追従する")]
        [SerializeField] private float refreshInterval = 2f;

        private float nextRefresh;
        private int nextIndex = -2;
        private bool gemAlert;
        private bool staminaEmpty;
        private bool staminaApplied;
        private Color countBase, countdownBase, plateBase;
        private string lastCountdown;
        private Vector3 alertBaseScale = Vector3.one;
        private float alertStart;
        private RectTransform nextRt;
        private CanvasGroup nextGroup;
        private Vector3 nextBaseScale = Vector3.one;
        private float nextStart;

        private void Awake()
        {
            if (staminaCountText != null) countBase = staminaCountText.color;
            if (staminaCountdownText != null) countdownBase = staminaCountdownText.color;
            if (staminaPlate != null) plateBase = staminaPlate.color;
            if (gemAlertBadge != null) { alertBaseScale = gemAlertBadge.localScale; gemAlertBadge.gameObject.SetActive(false); }
            if (countdownPlate != null) countdownPlate.enabled = false;
            foreach (var n in nodes ?? new Node[0])
                if (n?.nextTag != null) n.nextTag.SetActive(false);
        }

        private void Start()
        {
            if (StaminaManager.Instance != null) StaminaManager.Instance.OnStaminaChanged += RefreshStamina;
            RefreshStamina();
        }

        private void OnDestroy()
        {
            if (StaminaManager.Instance != null) StaminaManager.Instance.OnStaminaChanged -= RefreshStamina;
        }

        private void LateUpdate()
        {
            float now = Time.unscaledTime;
            if (now >= nextRefresh)
            {
                nextRefresh = now + Mathf.Max(0.2f, refreshInterval);
                RefreshNext();
                RefreshGemAlert(now);
                RefreshStamina();
            }
            UpdateCountdownPlate();
            if (nextRt != null) AnimateNext(now);
            if (gemAlert) AnimateGemAlert(now);
        }

        // ---------- ② スタミナ ----------
        private void RefreshStamina()
        {
            var sm = StaminaManager.Instance;
            bool empty = sm != null && !sm.IsUnlimited && sm.Count <= 0;
            if (staminaApplied && empty == staminaEmpty) return;
            staminaApplied = true;
            staminaEmpty = empty;
            if (staminaCountText != null) staminaCountText.color = empty ? emptyCountColor : countBase;
            if (staminaCountdownText != null)
            {
                staminaCountdownText.color = empty ? emptyCountdownColor : countdownBase;
                staminaCountdownText.fontStyle = empty ? FontStyles.Bold : FontStyles.Normal;
                lastCountdown = null; // 太さが変わると幅も変わるため、下地を測り直す
            }
            if (staminaPlate != null) staminaPlate.color = empty ? emptyPlateColor : plateBase;
        }

        /// <summary>「あと mm:ss」の下地を文字の長さに合わせる（文字が変わった時＝1秒に1回だけ）</summary>
        private void UpdateCountdownPlate()
        {
            if (staminaCountdownText == null || countdownPlate == null) return;
            string s = staminaCountdownText.text;
            if (lastCountdown != null && s == lastCountdown) return;
            lastCountdown = s;
            bool show = !string.IsNullOrEmpty(s) && staminaCountdownText.gameObject.activeInHierarchy;
            countdownPlate.enabled = show;
            if (!show) return;
            float w = staminaCountdownText.GetPreferredValues(s).x + countdownPlatePadding * 2f;
            var rt = countdownPlate.rectTransform;
            rt.sizeDelta = new Vector2(w, rt.sizeDelta.y);
        }

        // ---------- ④ NEXT ----------
        private void RefreshNext()
        {
            int found = -1;
            var pm = ProgressManager.Instance;
            if (pm != null && nodes != null)
            {
                for (int i = 0; i < nodes.Length; i++)
                {
                    var n = nodes[i];
                    if (n == null || n.stage == null || !n.stage.IsUnlocked()) continue;
                    if (pm.IsStageCleared(n.stage.areaId, FinalStage(n.stage.areaId))) continue;
                    if (found < 0 || AreaNumber(n.stage.areaId) < AreaNumber(nodes[found].stage.areaId)) found = i;
                }
            }
            if (found == nextIndex) return;
            nextIndex = found;
            if (nextRt != null) nextRt.localScale = nextBaseScale; // 前のタグを元の大きさへ
            nextRt = null; nextGroup = null;
            for (int i = 0; nodes != null && i < nodes.Length; i++)
            {
                var tag = nodes[i]?.nextTag;
                if (tag == null) continue;
                tag.SetActive(i == found);
                if (i != found) continue;
                nextRt = (RectTransform)tag.transform;
                nextGroup = tag.GetComponent<CanvasGroup>();
                nextBaseScale = Vector3.one;
                nextStart = Time.unscaledTime;
            }
        }

        private void AnimateNext(float now)
        {
            // 0→1→0 を繰り返す（出た瞬間は一番大きく明るい状態から）
            float k = 0.5f + 0.5f * Mathf.Cos((now - nextStart) * nextPulsePerSecond * Mathf.PI * 2f);
            nextRt.localScale = nextBaseScale * Mathf.Lerp(nextScaleMin, nextScaleMax, k);
            if (nextGroup != null) nextGroup.alpha = Mathf.Lerp(nextAlphaMin, 1f, k);
        }

        // ---------- ⑤ ジェムの！ ----------
        private void RefreshGemAlert(float now)
        {
            if (gemAlertBadge == null) return;
            var list = Game.Gems.GemManager.Instance != null ? Game.Gems.GemManager.Instance.GetEquippedGemsAtLastUse() : null;
            bool on = list != null && list.Count > 0;
            if (on == gemAlert && gemAlertBadge.gameObject.activeSelf == on) return;
            gemAlert = on;
            alertStart = now;
            gemAlertBadge.gameObject.SetActive(on);
            if (!on) gemAlertBadge.localScale = alertBaseScale;
        }

        private void AnimateGemAlert(float now)
        {
            if (gemAlertBadge == null) return;
            // 0→1→0 を繰り返す（出た瞬間は一番大きく明るい状態から）
            float k = 0.5f + 0.5f * Mathf.Cos((now - alertStart) * alertPulsePerSecond * Mathf.PI * 2f);
            gemAlertBadge.localScale = alertBaseScale * Mathf.Lerp(alertScaleMin, alertScaleMax, k);
            if (gemAlertGroup != null) gemAlertGroup.alpha = Mathf.Lerp(alertAlphaMin, 1f, k);
            if (gemAlertGlow != null) { Color c = gemAlertGlow.color; c.a = Mathf.Lerp(alertGlowAlpha.x, alertGlowAlpha.y, k); gemAlertGlow.color = c; }
        }

        // ---------- 小物 ----------
        private static int AreaNumber(string areaId)
        {
            if (string.IsNullOrEmpty(areaId)) return 99;
            int us = areaId.LastIndexOf('_');
            return us >= 0 && int.TryParse(areaId.Substring(us + 1), out int v) ? v : 99;
        }

        private static int FinalStage(string areaId)
        {
            var nums = AreaDB.Instance != null ? AreaDB.Instance.GetStageNumbers(areaId) : null;
            return nums != null && nums.Length > 0 ? nums[nums.Length - 1] : 3;
        }
    }
}
