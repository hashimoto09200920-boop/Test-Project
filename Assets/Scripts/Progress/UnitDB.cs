using System.Linq;
using UnityEngine;
using System.Collections.Generic;

namespace Game.Progress
{
    public class UnitDB : MonoBehaviour
    {
        public static UnitDB Instance { get; private set; }

        [Header("カタログの Resources 内パス（拡張子不要）")]
        [SerializeField] private string catalogResourcePath = "GameData/UnitCatalog";

        [SerializeField] private bool loadOnStart = true;

        public UnitCatalog Catalog { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureInstance()
        {
            if (Instance != null) return;
            var go = new GameObject("UnitDB");
            Object.DontDestroyOnLoad(go);
            Instance = go.AddComponent<UnitDB>();
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

        public void LoadCatalog()
        {
            Catalog = Resources.Load<UnitCatalog>(catalogResourcePath);
#if UNITY_EDITOR
            if (Catalog == null)
            {
                Debug.LogWarning($"[UnitDB] UnitCatalog not found at Resources/{catalogResourcePath}. " +
                                 $"Create one via Create > Game/Progress/UnitCatalog and place it under Resources/GameData/");
            }
            else
            {
                Debug.Log($"[UnitDB] UnitCatalog loaded: {catalogResourcePath} (Units: {Catalog.units.Count})");
            }
#endif
        }

        public UnitDef GetUnitById(string unitId)
        {
            if (Catalog == null || Catalog.units == null) return null;
            return Catalog.units.FirstOrDefault(u => u != null && u.unitId == unitId);
        }

        public string GetDisplayName(string unitId)
        {
            var def = GetUnitById(unitId);
            return def != null && !string.IsNullOrEmpty(def.displayName) ? def.displayName : unitId;
        }

        public int GetPrice(string unitId)
        {
            var def = GetUnitById(unitId);
            return def != null ? Mathf.Max(0, def.priceGold) : 0;
        }

        public IEnumerable<UnitDef> GetUnitsByType(UnitType type)
        {
            if (Catalog == null || Catalog.units == null) return System.Linq.Enumerable.Empty<UnitDef>();
            return Catalog.units.Where(u => u != null && u.unitType == type);
        }

        public string[] GetAllIds()
        {
            if (Catalog == null || Catalog.units == null) return System.Array.Empty<string>();
            return Catalog.units.Where(u => u != null && !string.IsNullOrEmpty(u.unitId))
                                .Select(u => u.unitId)
                                .ToArray();
        }
    }
}