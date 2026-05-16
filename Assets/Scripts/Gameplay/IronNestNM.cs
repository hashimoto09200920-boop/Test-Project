using UnityEngine;
using System.Collections;

/// <summary>
/// IronNest のハッチから出るNM（NeonMonster）のモグラたたきアニメーション制御
/// </summary>
public class IronNestNM : MonoBehaviour
{
    [Header("Position")]
    [Tooltip("格納時のローカルY座標（ハッチより下）")]
    [SerializeField] private float hiddenLocalY = -2f;

    [Tooltip("出現時のローカルY座標（ハッチから頭が出た位置）")]
    [SerializeField] private float visibleLocalY = 0f;

    [Header("Timing")]
    [Tooltip("出現アニメーション時間（秒）")]
    [SerializeField] private float popupDuration = 0.3f;

    [Tooltip("格納アニメーション時間（秒）")]
    [SerializeField] private float retractDuration = 0.2f;

    [Tooltip("出現中に射撃する時間（秒）")]
    [SerializeField] private float visibleDuration = 3f;

    [Tooltip("格納後、次の出現までの待機時間（秒）")]
    [SerializeField] private float hiddenDuration = 2f;

    [Tooltip("ONにすると自動でループ出現する（ボスコントローラーがない場合のテスト用）")]
    [SerializeField] private bool autoLoop = true;

    private EnemyShooter shooter;
    private Animator anim;
    private bool isVisible = false;
    private Coroutine loopCoroutine;

    private void Awake()
    {
        shooter = GetComponent<EnemyShooter>();
        anim = GetComponent<Animator>();
        SetLocalY(hiddenLocalY);
        if (shooter != null) shooter.enabled = false;
        if (anim != null) anim.enabled = false;
    }

    private void Start()
    {
        if (autoLoop)
            StartLoop(0f);
    }

    // =========================================================
    // 外部から呼ぶAPI（ボスコントローラー用）
    // =========================================================
    public void PopUp()
    {
        if (isVisible) return;
        StartCoroutine(PopUpCoroutine());
    }

    public void Retract()
    {
        if (!isVisible) return;
        StartCoroutine(RetractCoroutine());
    }

    public bool IsVisible => isVisible;

    public void StartLoop(float initialDelay = 0f)
    {
        if (loopCoroutine != null) return;
        loopCoroutine = StartCoroutine(RunLoop(initialDelay));
    }

    public void StopAutoLoop()
    {
        autoLoop = false;
        if (loopCoroutine != null)
        {
            StopCoroutine(loopCoroutine);
            loopCoroutine = null;
        }
    }

    // =========================================================
    // 内部コルーチン
    // =========================================================
    private IEnumerator RunLoop(float initialDelay)
    {
        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);
        while (true)
        {
            yield return StartCoroutine(PopUpCoroutine());
            yield return new WaitForSeconds(visibleDuration);
            yield return StartCoroutine(RetractCoroutine());
            yield return new WaitForSeconds(hiddenDuration);
        }
    }

    private IEnumerator PopUpCoroutine()
    {
        isVisible = true;

        yield return StartCoroutine(MoveLocalY(hiddenLocalY, visibleLocalY, popupDuration));

        if (shooter != null) shooter.enabled = true;
        if (anim != null)
            anim.enabled = true;
        // ここで終了。待機・格納は呼び出し元（LoopCoroutine or BossController）が管理する
    }

    private IEnumerator RetractCoroutine()
    {
        if (shooter != null) shooter.enabled = false;
        if (anim != null)
        {
            anim.Play(0, 0, 0f);
            anim.Update(0f);
            anim.enabled = false;
        }

        float fromY = transform.localPosition.y;
        yield return StartCoroutine(MoveLocalY(fromY, hiddenLocalY, retractDuration));

        isVisible = false;
    }

    private IEnumerator MoveLocalY(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            SetLocalY(to);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            SetLocalY(Mathf.Lerp(from, to, t));
            yield return null;
        }
        SetLocalY(to);
    }

    private void SetLocalY(float y)
    {
        Vector3 pos = transform.localPosition;
        pos.y = y;
        transform.localPosition = pos;
    }
}
