using UnityEngine;
using Game.Progress;

public class UnlockRulesProbe : MonoBehaviour
{
    [Header("F6 で判定ログ（日本語）")]
    public bool enableF6 = true;

    [Header("判定する Unit ID（例: UR1, UB2）")]
    public string unitToCheck = "UR1";

    [Header("（任意）Area も一緒にログ")]
    public bool alsoLogArea = false;

    [Header("Area ID（任意ログ用）")]
    public string areaToCheck = AreaIds.Area_02;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var go = new GameObject("UnlockRulesProbe");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<UnlockRulesProbe>();
    }

    private void Update()
    {
        if (!enableF6) return;
        if (Input.GetKeyDown(KeyCode.F6))
        {
            LogNow();
        }
    }

    private void LogNow()
    {
        // Unit の条件達成と所持状況
        bool condOk = UnlockRules.IsUnitUnlockedByConditions(unitToCheck);

        bool ownedBasic = false;
        bool ownedRelic = false;
        var pm = ProgressManager.Instance;
        if (pm != null && pm.Data != null)
        {
            if (pm.Data.ownedBasicUnitIds != null)
                ownedBasic = pm.Data.ownedBasicUnitIds.Contains(unitToCheck);
            if (pm.Data.ownedRelicUnitIds != null)
                ownedRelic = pm.Data.ownedRelicUnitIds.Contains(unitToCheck);
        }

        Debug.Log($"[Unit判定] Unit={unitToCheck}  条件達成={condOk}  所持(Basic)={ownedBasic}  所持(Relic)={ownedRelic}");

        // 任意：Area も併せて確認したいとき
        if (alsoLogArea)
        {
            bool areaUnlocked = UnlockRules.IsAreaUnlocked(areaToCheck);
            Debug.Log($"[Area判定] Area={areaToCheck}  解除={areaUnlocked}");
        }
    }
}
