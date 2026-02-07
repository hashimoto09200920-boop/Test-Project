using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Progress
{
    /// <summary>
    /// AreaCatalog（ScriptableObject）を Resources からロードし、
    /// ゲーム中のどこからでも参照できるようにするランタイムDB。
    /// </summary>
    public class AreaDB : MonoBehaviour
    {
        public static AreaDB Instance { get; private set; }

        [Header("カタログの Resources 内パス（拡張子不要）")]
        [SerializeField] private string catalogResourcePath = "GameData/AreaCatalog";

        [Tooltip("起動時に自動ロードするか")]
        [SerializeField] private bool loadOnStart = true;

        public AreaCatalog Catalog { get; private set; }

        // 自動生成（ProgressManager同様、シーン配置不要）
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureInstance()
        {
            if (Instance != null) return;
            var go = new GameObject("AreaDB");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<AreaDB>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (loadOnStart)
            {
                LoadCatalog();
            }
        }

        /// <summary>
        /// Resources から AreaCatalog をロード。
        /// </summary>
        public void LoadCatalog()
        {
            Catalog = Resources.Load<AreaCatalog>(catalogResourcePath);
#if UNITY_EDITOR
            if (Catalog == null)
            {
                Debug.LogWarning($"[AreaDB] AreaCatalog not found at Resources/{catalogResourcePath}. " +
                                 $"Create one via Create > Game/Progress/AreaCatalog and place it under Resources/GameData/");
            }
            else
            {
                Debug.Log($"[AreaDB] AreaCatalog loaded: {catalogResourcePath} (Areas: {Catalog.areas.Count})");
            }
#endif
        }

        /// <summary>
        /// 指定 Area のステージ番号一覧（昇順）を取得。見つからない場合は空配列。
        /// </summary>
        public int[] GetStageNumbers(string areaId)
        {
            if (Catalog == null || Catalog.areas == null) return System.Array.Empty<int>();
            var def = Catalog.areas.FirstOrDefault(a => a != null && a.areaId == areaId);
            if (def == null || def.stages == null) return System.Array.Empty<int>();
            return def.stages.Where(s => s != null).Select(s => s.number).OrderBy(n => n).ToArray();
        }

        /// <summary>
        /// 指定 Area/Stage の表示名を取得。無ければ "Stage X" を返す。
        /// </summary>
        public string GetStageDisplayName(string areaId, int stageNumber)
        {
            if (Catalog == null || Catalog.areas == null) return $"Stage {stageNumber}";
            var def = Catalog.areas.FirstOrDefault(a => a != null && a.areaId == areaId);
            if (def == null || def.stages == null) return $"Stage {stageNumber}";
            var s = def.stages.FirstOrDefault(x => x != null && x.number == stageNumber);
            return s != null && !string.IsNullOrEmpty(s.displayName) ? s.displayName : $"Stage {stageNumber}";
        }

        /// <summary>
        /// 定義済み AreaID 一覧を返す（例：["Area_01","Area_02"]）
        /// </summary>
        public string[] GetAreaIds()
        {
            if (Catalog == null || Catalog.areas == null) return System.Array.Empty<string>();
            return Catalog.areas.Where(a => a != null && !string.IsNullOrEmpty(a.areaId))
                                .Select(a => a.areaId)
                                .ToArray();
        }
    }
}