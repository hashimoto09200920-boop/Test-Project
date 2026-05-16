using UnityEngine;

/// <summary>
/// IronNest ボスコントローラー
/// 各NMは IronNestNM の Timing 設定で独立動作する。
/// このコントローラーは初回出現のタイミングだけ個別にずらす。
/// </summary>
public class IronNestBossController : MonoBehaviour
{
    [Header("NM References")]
    [SerializeField] private IronNestNM nm01;
    [SerializeField] private IronNestNM nm02;
    [SerializeField] private IronNestNM nm03;

    [Header("Initial Delay (seconds)")]
    [Tooltip("NM01 の初回出現までの遅延（秒）")]
    [SerializeField] private float nm01InitialDelay = 0f;

    [Tooltip("NM02 の初回出現までの遅延（秒）")]
    [SerializeField] private float nm02InitialDelay = 2f;

    [Tooltip("NM03 の初回出現までの遅延（秒）")]
    [SerializeField] private float nm03InitialDelay = 4f;

    private void Awake()
    {
        if (nm01 != null) nm01.StopAutoLoop();
        if (nm02 != null) nm02.StopAutoLoop();
        if (nm03 != null) nm03.StopAutoLoop();
    }

    private void Start()
    {
        if (nm01 != null) nm01.StartLoop(nm01InitialDelay);
        if (nm02 != null) nm02.StartLoop(nm02InitialDelay);
        if (nm03 != null) nm03.StartLoop(nm03InitialDelay);
    }
}
