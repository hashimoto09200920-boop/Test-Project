using UnityEngine;
using System.Collections;

/// <summary>
/// 敵撃破時などに一瞬だけ時間を止めるヒットストップ機能。
/// シーン内のGameObjectにアタッチして使う（GameManagerなど）。
/// EnemyStats.Die() から HitStop.Instance?.Trigger() で呼ぶ。
///
/// 【バグ修正済み】2体同時撃破でフリーズしていた原因:
/// StopCoroutine で止めた時に timeScale が復元されないまま
/// 次の DoHitStop が prevTimeScale=0 を保存 → 終了後も 0 に戻していた。
/// → savedTimeScale を class フィールドにして「既に動作中なら上書きしない」方式に変更。
/// </summary>
public class HitStop : MonoBehaviour
{
    public static HitStop Instance { get; private set; }

    [Header("Hit Stop Settings")]
    [Tooltip("ヒットストップの継続時間（秒・実時間）。0.08〜0.15 推奨")]
    [SerializeField] private float duration = 0.08f;

    [Tooltip("ヒットストップ中のTimeScale（0=完全停止）")]
    [SerializeField] private float frozenTimeScale = 0f;

    private Coroutine stopCoroutine;
    private float savedTimeScale = 1f; // HitStop開始前のtimeScaleを保存（複数回呼ばれても初回値を保持）
    private bool isRunning = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this); // gameObjectではなくコンポーネントだけ削除
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            // 万一timeScaleが0のまま破棄された場合に備えて復元
            if (isRunning) Time.timeScale = savedTimeScale;
            Instance = null;
        }
    }

    /// <summary>
    /// ヒットストップを発火。Inspectorの duration / frozenTimeScale を使う。
    /// 既に動作中の場合はタイマーをリセットして継続。
    /// </summary>
    public void Trigger()
    {
        // isRunning でない時だけ savedTimeScale を保存（動作中に呼ばれた場合は0になった後の値を保存しないため）
        if (!isRunning)
        {
            savedTimeScale = Time.timeScale;
        }

        if (stopCoroutine != null) StopCoroutine(stopCoroutine);
        stopCoroutine = StartCoroutine(DoHitStop());
    }

    private IEnumerator DoHitStop()
    {
        isRunning = true;
        Time.timeScale = frozenTimeScale;

        yield return new WaitForSecondsRealtime(duration);

        // savedTimeScale に復元（SlowMotionManager が動いている場合もその値を保持）
        Time.timeScale = savedTimeScale;
        isRunning = false;
        stopCoroutine = null;
    }
}
