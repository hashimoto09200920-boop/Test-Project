using UnityEngine;
using Game.Progress;
using System.Linq;
using System.Text;

public class UnitDBProbe : MonoBehaviour
{
    [Header("F5 で Basic / Relic の定義を一括ログ")]
    public bool enableF5 = true;

    [Header("起動時に一度ログを出す")]
    public bool logOnStart = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("UnitDBProbe");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<UnitDBProbe>();
    }

    private void Start()
    {
        if (logOnStart)
        {
            LogAllUnits();
        }
    }

    private void Update()
    {
        if (!enableF5) return;
        if (Input.GetKeyDown(KeyCode.F5))
        {
            LogAllUnits();
        }
    }

    private static void LogAllUnits()
    {
        if (UnitDB.Instance == null || UnitDB.Instance.Catalog == null)
        {
            Debug.LogWarning("[UnitDBProbe] UnitDB not ready or UnitCatalog not loaded.");
            return;
        }

        var cat = UnitDB.Instance.Catalog;
        var basics = cat.units.Where(u => u != null && u.unitType == UnitType.Basic).ToList();
        var relics = cat.units.Where(u => u != null && u.unitType == UnitType.Relic).ToList();

        var sb = new StringBuilder(512);
        sb.AppendLine($"[UnitDBProbe] Units (Total: {cat.units.Count})");
        sb.AppendLine($"- Basic ({basics.Count})");
        foreach (var u in basics) sb.AppendLine(Format(u));
        sb.AppendLine($"- Relic ({relics.Count})");
        foreach (var u in relics) sb.AppendLine(Format(u));

        Debug.Log(sb.ToString());
    }

    private static string Format(UnitDef u)
    {
        string unlock = (u.unlockByStages == null || u.unlockByStages.Count == 0)
            ? "-"
            : string.Join(",", u.unlockByStages.Select(c => $"{c.areaId}-S{c.stageNumber}"));
        return $"  {u.unitId} ({u.displayName}) type={u.unitType} price={u.priceGold} unlock=[{unlock}]";
    }
}
