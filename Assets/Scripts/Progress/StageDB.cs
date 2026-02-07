using System.Collections.Generic;
using UnityEngine;

namespace Game.Progress
{
    /// <summary>
    /// StageCatalog（SO）をロードし、ランタイム検索APIを提供するDB。
    /// 自動生成 + DontDestroyOnLoad。
    /// </summary>
    public class StageDB : MonoBehaviour
    {
        public static StageDB Instance { get; private set; }

        // 参照したSO
        public StageCatalog Catalog { get; private set; }

        // 検索用インデックス
        private readonly Dictionary<string, List<StageDef>> _byArea = new();
        private readonly Dictionary<string, StageDef> _byKey = new();

        // --- 自動生成 ---
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("StageDB");
            Instance = go.AddComponent<StageDB>();
            DontDestroyOnLoad(go);
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
            LoadCatalog();
        }

        private void LoadCatalog()
        {
            _byArea.Clear();
            _byKey.Clear();

            Catalog = Resources.Load<StageCatalog>("GameData/StageCatalog");
            if (Catalog == null || Catalog.stages == null)
            {
                Debug.LogWarning("[StageDB] StageCatalog not found or empty at Resources/GameData/StageCatalog");
                return;
            }

            int count = 0;
            foreach (var st in Catalog.stages)
            {
                if (st == null || string.IsNullOrWhiteSpace(st.areaId) || st.stageNumber < 1) continue;

                // byArea
                if (!_byArea.TryGetValue(st.areaId, out var list))
                {
                    list = new List<StageDef>();
                    _byArea.Add(st.areaId, list);
                }
                list.Add(st);

                // byKey
                var key = st.GetKey();
                if (!_byKey.ContainsKey(key))
                {
                    _byKey.Add(key, st);
                    count++;
                }
            }

            // 安定のため、Area ごとの配列は stageNumber 順で整列
            foreach (var kv in _byArea)
            {
                kv.Value.Sort((a, b) => a.stageNumber.CompareTo(b.stageNumber));
            }

            Debug.Log($"[StageDB] StageCatalog loaded: GameData/StageCatalog (Stages: {count})");
        }

        // ====== 検索API ======

        /// <summary>特定のステージ（areaId + stageNumber）を取得</summary>
        public StageDef GetStage(string areaId, int stageNumber)
        {
            if (string.IsNullOrWhiteSpace(areaId) || stageNumber < 1) return null;
            var key = $"{areaId}#{stageNumber}";
            _byKey.TryGetValue(key, out var st);
            return st;
        }

        /// <summary>エリア内の全ステージ（番号順）を取得</summary>
        public IReadOnlyList<StageDef> GetStagesInArea(string areaId)
        {
            if (string.IsNullOrWhiteSpace(areaId)) return System.Array.Empty<StageDef>();
            return _byArea.TryGetValue(areaId, out var list)
                ? (IReadOnlyList<StageDef>)list
                : System.Array.Empty<StageDef>();
        }

        /// <summary>全ステージ（Areaごとに番号順／ループ用）</summary>
        public IEnumerable<StageDef> GetAllStages()
        {
            foreach (var kv in _byArea)
            {
                foreach (var st in kv.Value)
                    yield return st;
            }
        }
    }
}
