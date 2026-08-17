using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Progress
{
    /// <summary>
    /// セーブデータの単一常駐マネージャ。DontDestroyOnLoad。
    /// ※ 注意：AreaProgress / ProgressData は既存ファイルに定義済み前提（ここでは再定義しません）
    ///   - ProgressData には以下プロパティがある想定：
    ///     List<AreaProgress> areas;
    ///     List<string> ownedBasicUnitIds;
    ///     List<string> ownedRelicUnitIds;
    ///     int gold;
    ///   - ProgressData に、AreaProgress を生成・取得する GetOrCreateArea(areaId) がある前提。
    /// </summary>
    public class ProgressManager : MonoBehaviour
    {
        public static ProgressManager Instance { get; private set; }

        // 現在の進捗（メモリ）
        public ProgressData Data = new ProgressData();

        private const string Key = "GAME_PROGRESS_V1";

        // ---- 自動生成（シーンに置く必要なし） ----
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureInstance()
        {
            if (Instance == null)
            {
                var go = new GameObject("ProgressManager");
                DontDestroyOnLoad(go);
                Instance = go.AddComponent<ProgressManager>();
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Load();
        }

        // ================== Save / Load ==================

        public void Save()
        {
            try
            {
                var json = JsonUtility.ToJson(Data);
                PlayerPrefs.SetString(Key, json);
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                Debug.LogError($"[ProgressManager] Save failed: {e}");
            }
        }

        public void Load()
        {
            try
            {
                if (PlayerPrefs.HasKey(Key))
                {
                    var json = PlayerPrefs.GetString(Key);
                    var loaded = JsonUtility.FromJson<ProgressData>(json);
                    Data = loaded ?? new ProgressData();
                }
                else
                {
                    Data = new ProgressData();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[ProgressManager] Load failed: {e}");
                Data = new ProgressData();
            }

            // 念のため null 防御
            if (Data.areas == null) Data.areas = new List<AreaProgress>();
            if (Data.ownedBasicUnitIds == null) Data.ownedBasicUnitIds = new List<string>();
            if (Data.ownedRelicUnitIds == null) Data.ownedRelicUnitIds = new List<string>();
            if (Data.gemInventory == null) Data.gemInventory = new List<GemInstance>();
            if (Data.equippedGemIndices == null) Data.equippedGemIndices = new List<int>();

            MigrateGemUsesIfNeeded();
        }

        /// <summary>
        /// ジェム使用回数システム導入前のセーブデータに対し、既存の所持ジェムへ
        /// 残り使用回数を遡って設定する（初回ロード時のみ・1回限り）。
        /// GemManagerの初期化順に依存しないよう、Resources.Loadで直接GemDefinitionを引く。
        /// </summary>
        private void MigrateGemUsesIfNeeded()
        {
            if (Data.gemUsesMigrated) return;

            foreach (var gemInst in Data.gemInventory)
            {
                if (gemInst == null) continue;
                var def = Resources.Load<Game.Gems.GemDefinition>($"GameData/Gems/{gemInst.gemDefinitionName}");
                gemInst.remainingUses = def != null ? def.maxUses : 30;
            }

            Data.gemUsesMigrated = true;
            Save();
        }

        public void ResetAll()
        {
            Data = new ProgressData
            {
                areas = new List<AreaProgress>(),
                ownedBasicUnitIds = new List<string>(),
                ownedRelicUnitIds = new List<string>(),
                gold = 0,
                slotLevel = 1,
                gemInventory = new List<GemInstance>(),
                equippedGemIndices = new List<int>(),
            };
            Save();
        }

        /// <summary>
        /// 互換API（既存スクリプトから呼ばれても動くように残す）
        /// </summary>
        public void ResetProgress() => ResetAll();

        // ================== Stage ==================

        public bool IsStageCleared(string areaId, int stageNumber)
        {
            if (string.IsNullOrEmpty(areaId) || stageNumber <= 0) return false;
            var ap = Data.areas?.Find(a => a.areaId == areaId);
            return ap != null && ap.clearedStages != null && ap.clearedStages.Contains(stageNumber);
        }

        private const int SlotLevelUpStage = 3;
        private const int MaxSlotLevel = 10;

        /// <summary>
        /// まだ未登録ならクリア登録。新規登録なら true を返す。
        /// Stage3 の初回クリア時はスロットレベルを+1する。
        /// </summary>
        public bool MarkStageCleared(string areaId, int stageNumber)
        {
            if (string.IsNullOrEmpty(areaId) || stageNumber <= 0) return false;
            var ap = Data.GetOrCreateArea(areaId);
            if (ap.clearedStages == null) ap.clearedStages = new List<int>();

            if (!ap.clearedStages.Contains(stageNumber))
            {
                ap.clearedStages.Add(stageNumber);
                ap.clearedStages.Sort();

                // Stage3 初回クリア時にスロットレベルを上昇
                if (stageNumber == SlotLevelUpStage && Data.slotLevel < MaxSlotLevel)
                {
                    Data.slotLevel++;
                    Debug.Log($"[Progress] SlotLevel increased to {Data.slotLevel} ({areaId} Stage {stageNumber} first clear)");
                }

                Save();
                Debug.Log($"[Progress] {areaId} Stage {stageNumber} cleared. (new? True)");
                return true;
            }

            Debug.Log($"[Progress] {areaId} Stage {stageNumber} cleared. (new? False)");
            return false;
        }

        /// <summary>
        /// 現在のスロットレベルを取得
        /// </summary>
        public int GetSlotLevel() => Data.slotLevel;

        // ================== Rank ==================

        // ランクの序列（末尾ほど上位）
        private static readonly string[] RankOrder = { "E", "D", "C", "B", "A", "S" };

        /// <summary>
        /// 指定Areaのクリア時ランクを記録する。既存のベストランクより上位の場合のみ更新・保存する。
        /// </summary>
        public void UpdateAreaBestRank(string areaId, string rank)
        {
            if (string.IsNullOrEmpty(areaId) || string.IsNullOrEmpty(rank)) return;

            var ap = Data.GetOrCreateArea(areaId);
            if (IsRankHigherThan(rank, ap.bestRank))
            {
                ap.bestRank = rank;
                Save();
                Debug.Log($"[Progress] {areaId} bestRank updated to {rank}");
            }
        }

        /// <summary>
        /// デバッグ用：UpdateAreaBestRankと違い「上位ランクのみ更新」の制限を無視して直接上書きする。
        /// 降格やクリア（空文字）もできるため、テスト目的以外では使用しないこと。
        /// </summary>
        public void DebugSetAreaBestRank(string areaId, string rank)
        {
            if (string.IsNullOrEmpty(areaId)) return;

            var ap = Data.GetOrCreateArea(areaId);
            ap.bestRank = rank ?? "";
            Save();
            Debug.Log($"[Progress] (DEBUG) {areaId} bestRank forcibly set to \"{ap.bestRank}\"");
        }

        /// <summary>指定Areaの過去最高ランクを取得（未達成なら空文字）</summary>
        public string GetAreaBestRank(string areaId)
        {
            var ap = Data.areas?.Find(a => a.areaId == areaId);
            return ap != null ? ap.bestRank : "";
        }

        /// <summary>ランクがA以上（AまたはS）かどうか</summary>
        public static bool IsRankAOrBetter(string rank) => rank == "A" || rank == "S";

        private static bool IsRankHigherThan(string rank, string otherRank)
        {
            int idx = System.Array.IndexOf(RankOrder, rank);
            int otherIdx = string.IsNullOrEmpty(otherRank) ? -1 : System.Array.IndexOf(RankOrder, otherRank);
            return idx > otherIdx;
        }

        // ================== Unit ==================

        public bool UnlockBasicUnit(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId)) return false;
            if (Data.ownedBasicUnitIds == null) Data.ownedBasicUnitIds = new List<string>();

            if (!Data.ownedBasicUnitIds.Contains(unitId))
            {
                Data.ownedBasicUnitIds.Add(unitId);
                Save();
                return true;
            }
            return false;
        }

        public bool UnlockRelicUnit(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId)) return false;
            if (Data.ownedRelicUnitIds == null) Data.ownedRelicUnitIds = new List<string>();

            if (!Data.ownedRelicUnitIds.Contains(unitId))
            {
                Data.ownedRelicUnitIds.Add(unitId);
                Save();
                return true;
            }
            return false;
        }

        public bool RemoveOwnedBasic(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId)) return false;
            if (Data.ownedBasicUnitIds == null) return false;

            var ok = Data.ownedBasicUnitIds.Remove(unitId);
            if (ok) Save();
            return ok;
        }

        public bool RemoveOwnedRelic(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId)) return false;
            if (Data.ownedRelicUnitIds == null) return false;

            var ok = Data.ownedRelicUnitIds.Remove(unitId);
            if (ok) Save();
            return ok;
        }

        // ================== Gold ==================

        public int GetGold() => Mathf.Max(0, Data.gold);

        public void AddGold(int amount)
        {
            if (amount <= 0) return;
            Data.gold += amount;
            if (Data.gold < 0) Data.gold = 0;
            Save();
        }

        /// <summary>
        /// Gold を消費。成功時 true（不足なら false）。
        /// </summary>
        public bool SpendGold(int amount)
        {
            if (amount <= 0) return true;
            if (Data.gold < amount) return false;
            Data.gold -= amount;
            if (Data.gold < 0) Data.gold = 0;
            Save();
            return true;
        }
    }
}
