using System.Collections;
using UnityEngine;

public class PlayerStringOrchestrator : MonoBehaviour
{
    [Header("String Controllers")]
    [Tooltip("③ LineType反転の糸（PlayerString_03）")]
    [SerializeField] private PlayerStringController string03;
    [Tooltip("⑤ 座標反転の糸（PlayerString_05）")]
    [SerializeField] private PlayerStringController string05;

    [Header("Timing")]
    [Tooltip("ゲーム開始から最初の糸が出るまでの秒数")]
    [SerializeField] private float firstStringDelay = 5f;
    [Tooltip("ゲーム開始から2本目の糸が出るまでの秒数（firstStringDelayより大きくすること）")]
    [SerializeField] private float secondStringDelay = 12f;

    private void Start()
    {
        StartCoroutine(OrchestrateRoutine());
    }

    private IEnumerator OrchestrateRoutine()
    {
        yield return new WaitForSeconds(firstStringDelay);

        bool firstIsString03 = Random.value < 0.5f;
        var first  = firstIsString03 ? string03 : string05;
        var second = firstIsString03 ? string05 : string03;

        if (first != null) first.Activate();

        float secondWait = Mathf.Max(0f, secondStringDelay - firstStringDelay);
        yield return new WaitForSeconds(secondWait);

        if (second != null) second.Activate();
    }
}
