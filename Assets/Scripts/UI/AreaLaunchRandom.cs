using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Game.Progress;

namespace Game.UI
{
    /// <summary>
    /// Area ボタン1件：クリックで AreaId を保存し、条件に合う Stage を Area 内からランダム選択して 05_Game へ遷移。
    /// 条件は Inspector で編集可能。
    /// Button の OnClick を Inspector で設定する必要はない（コードで購読）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Button))]
    public class AreaLaunchRandom : MonoBehaviour
    {
        [Header("識別子")]
        [Tooltip("Area_01 / Area_02 など ProgressIds に合わせる")]
        public string areaId = "Area_01";

        [Header("次のシーン")]
        [Tooltip("Build Profiles の Scene List に登録必須")]
        public string nextSceneName = "05_Game";

        [Header("ランダム選択条件")]
        [Min(1)] public int minStageNumber = 1;
        [Min(1)] public int maxStageNumber = 9999;

        [Tooltip("含めたいタグ（OR 条件）。空なら無視")]
        public string[] includeTags;
        [Tooltip("除外したいタグ（OR 条件）。空なら無視")]
        public string[] excludeTags;

        [Header("難易度レンジ（StageDef.recommendedLevel）")]
        [Min(1)] public int minRecommendedLevel = 1;
        [Min(1)] public int maxRecommendedLevel = 9999;

        [Header("その他条件")]
        [Tooltip("ボス戦も候補に含めるか")]
        public bool allowBoss = true;

        [Header("ランダムモード")]
        public RandomMode randomMode = RandomMode.SystemRandom;
        [Tooltip("FixedSeed 指定時のみ使用")]
        public int fixedSeed = 12345;

        [Header("ログ")]
        public bool verboseLog = true;

        [Header("参照（自動取得可）")]
        public Button button;
        public Text titleText; // 任意
        public Text subText;   // 任意

        private bool _subscribed;

        public enum RandomMode { SystemRandom, UnityRandom, FixedSeed }

        private void Reset()
        {
            button = GetComponent<Button>();
            if (titleText == null) titleText = transform.Find("TitleText")?.GetComponent<Text>();
            if (subText   == null) subText   = transform.Find("SubText")?.GetComponent<Text>();
        }

        private void Awake()
        {
            button = button ? button : GetComponent<Button>();
            TrySubscribe();
        }

        private void OnEnable()
        {
            button = button ? button : GetComponent<Button>();
            TrySubscribe();
        }

        private void OnDisable()
        {
            TryUnsubscribe();
        }

        private void TrySubscribe()
        {
            if (button == null) { Debug.LogWarning($"[AreaLaunchRandom] Button missing on '{name}'"); return; }
            if (_subscribed) return;
            button.onClick.AddListener(OnClick);
            _subscribed = true;
        }

        private void TryUnsubscribe()
        {
            if (button == null || !_subscribed) return;
            button.onClick.RemoveListener(OnClick);
            _subscribed = false;
        }

        private void OnClick()
        {
            if (string.IsNullOrWhiteSpace(areaId))
            {
                Debug.LogWarning("[AreaLaunchRandom] areaId is empty.");
                return;
            }

            // Area 保存（UI の状態を進行データに反映）
            var pm = ProgressManager.Instance;
            if (pm != null && pm.Data != null)
            {
                pm.Data.selectedAreaId = areaId;
                if (verboseLog) Debug.Log($"[AreaLaunchRandom] Set selectedAreaId='{areaId}'");
            }
            else
            {
                Debug.LogWarning("[AreaLaunchRandom] ProgressManager.Instance not found.");
            }

            // ステージ候補の収集
            var chosen = ChooseRandomStage(areaId);
            if (chosen == null)
            {
                Debug.LogWarning($"[AreaLaunchRandom] 条件に合う Stage が見つかりません: area='{areaId}'");
                return;
            }

            // 選択されたステージ番号を GameSession に保存
            GameSession.SelectedStageNumber = chosen.stageNumber;
            if (verboseLog) Debug.Log($"[AreaLaunchRandom] Set SelectedStageNumber={chosen.stageNumber}");

            if (!Application.isPlaying) return;
            if (string.IsNullOrWhiteSpace(nextSceneName))
            {
                Debug.LogWarning("[AreaLaunchRandom] nextSceneName is empty.");
                return;
            }
            if (verboseLog) Debug.Log($"[AreaLaunchRandom] Launch -> area='{areaId}', stage={chosen.stageNumber} ('{chosen.displayName}'), scene='{nextSceneName}'");
            SceneManager.LoadScene(nextSceneName);
        }

        private StageDef ChooseRandomStage(string area)
        {
            var db = StageDB.Instance;
            if (db == null)
            {
                Debug.LogWarning("[AreaLaunchRandom] StageDB.Instance not found.");
                return null;
            }

            var list = db.GetStagesInArea(area);
            if (list == null || list.Count == 0) return null;

            IEnumerable<StageDef> q = list;

            // 番号レンジ
            q = q.Where(s => s.stageNumber >= minStageNumber && s.stageNumber <= maxStageNumber);

            // recommendedLevel レンジ
            q = q.Where(s => s.recommendedLevel >= minRecommendedLevel && s.recommendedLevel <= maxRecommendedLevel);

            // タグフィルタ
            if (includeTags != null && includeTags.Length > 0)
            {
                q = q.Where(s => s.tags != null && s.tags.Any(t => includeTags.Contains(t)));
            }
            if (excludeTags != null && excludeTags.Length > 0)
            {
                q = q.Where(s => s.tags == null || !s.tags.Any(t => excludeTags.Contains(t)));
            }

            // ボス許可
            if (!allowBoss)
            {
                q = q.Where(s => !s.isBoss);
            }

            // ここで「ステージ個別アンロック」を導入したい場合は、プロジェクトの仕様に合わせて拡張可
            // （現状は Area 解放を前提にし、ステージ個別ロックは未使用）

            var candidates = q.ToList();
            if (candidates.Count == 0) return null;

            int idx = 0;
            switch (randomMode)
            {
                case RandomMode.SystemRandom:
                    idx = new System.Random().Next(0, candidates.Count);
                    break;
                case RandomMode.UnityRandom:
                    idx = UnityEngine.Random.Range(0, candidates.Count);
                    break;
                case RandomMode.FixedSeed:
                    idx = new System.Random(fixedSeed).Next(0, candidates.Count);
                    break;
            }

            return candidates[idx];
        }
    }
}
