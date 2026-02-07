using UnityEngine;
using Game.Progress;

public class AreaDBProbe : MonoBehaviour
{
    [Header("F4 で Area_01 / Area_02 を一括ログ")]
    public bool enableF4 = true;

    [Header("起動時に一度ログを出す")]
    public bool logOnStart = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("AreaDBProbe");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<AreaDBProbe>();
    }

    private void Start()
    {
        if (logOnStart)
        {
            LogArea(AreaIds.Area_01);
            LogArea(AreaIds.Area_02);
        }
    }

    private void Update()
    {
        if (!enableF4) return;
        if (Input.GetKeyDown(KeyCode.F4))
        {
            LogArea(AreaIds.Area_01);
            LogArea(AreaIds.Area_02);
        }
    }

    private static void LogArea(string areaId)
    {
        if (AreaDB.Instance == null)
        {
            Debug.LogWarning("[AreaDBProbe] AreaDB not ready.");
            return;
        }
        var arr = AreaDB.Instance.GetStageNumbers(areaId);
        Debug.Log($"[AreaDBProbe] {areaId} -> {string.Join(",", arr)}");
    }
}
