using UnityEngine;

public class GravePoleEye : MonoBehaviour
{
    [Header("追従設定")]
    [SerializeField] private float trackRadiusMin = 0.08f;
    [SerializeField] private float trackRadiusMax = 0.13f;
    [SerializeField] private float trackSpeedMin  = 3f;
    [SerializeField] private float trackSpeedMax  = 7f;

    [Header("アイドル設定")]
    [SerializeField] private float idleSpeedMin  = 0.5f;
    [SerializeField] private float idleSpeedMax  = 1.5f;
    [SerializeField] private float idleRadiusMin = 0.05f;
    [SerializeField] private float idleRadiusMax = 0.10f;

    [Header("状態切り替え間隔")]
    [Tooltip("Track状態が続く秒数の範囲")]
    [SerializeField] private float trackDurationMin = 1.5f;
    [SerializeField] private float trackDurationMax = 4.0f;
    [Tooltip("Idle状態が続く秒数の範囲")]
    [SerializeField] private float idleDurationMin = 0.5f;
    [SerializeField] private float idleDurationMax = 2.5f;

    [Header("フェーズ設定")]
    [SerializeField] private Color phase1Color = new Color(0.6f, 0.1f, 1.0f);
    [SerializeField] private Color phase2Color = new Color(1.0f, 0.1f, 0.1f);

    private enum EyeState { Track, Idle }

    private Transform      player;
    private SpriteRenderer sr;
    private Vector3        baseLocalPos;
    private float          idleTime;

    // この目ごとにランダムに決まる実効値
    private float actualTrackRadius;
    private float actualTrackSpeed;
    private float actualIdleSpeed;
    private float actualIdleRadius;

    // 状態管理
    private EyeState currentState;
    private float    stateTimer;

    private void Start()
    {
        sr           = GetComponent<SpriteRenderer>();
        baseLocalPos = transform.localPosition;

        var pd = FindFirstObjectByType<PixelDancerController>();
        if (pd != null) player = pd.transform;

        if (sr != null) sr.color = phase1Color;

        // 目ごとに異なる値をランダムに決定
        actualTrackRadius = Random.Range(trackRadiusMin, trackRadiusMax);
        actualTrackSpeed  = Random.Range(trackSpeedMin,  trackSpeedMax);
        actualIdleSpeed   = Random.Range(idleSpeedMin,   idleSpeedMax);
        actualIdleRadius  = Random.Range(idleRadiusMin,  idleRadiusMax);

        // 初期状態と初期タイマーをランダムに設定（4つの目が同期しないよう位相をずらす）
        currentState = (Random.value > 0.5f) ? EyeState.Track : EyeState.Idle;
        stateTimer   = GetRandomDuration(currentState);
        idleTime     = Random.Range(0f, 100f); // 位相オフセット
    }

    private void Update()
    {
        // 状態タイマーを減算し、0以下になったら切り替え
        stateTimer -= Time.deltaTime;
        if (stateTimer <= 0f)
        {
            currentState = (currentState == EyeState.Track) ? EyeState.Idle : EyeState.Track;
            stateTimer   = GetRandomDuration(currentState);
        }

        // 親スケールを考慮してローカル量に変換
        float parentScale = (transform.parent != null) ? transform.parent.lossyScale.x : 1f;
        if (parentScale == 0f) parentScale = 1f;

        Vector3 targetLocal;

        if (currentState == EyeState.Track && player != null)
        {
            Vector3 toPlayer = player.position - transform.parent.position;
            Vector3 dir      = toPlayer.normalized;
            targetLocal      = baseLocalPos + (Vector3)(Vector2)(dir * (actualTrackRadius / parentScale));
        }
        else
        {
            idleTime   += Time.deltaTime;
            float r    = actualIdleRadius / parentScale;
            targetLocal = baseLocalPos + new Vector3(
                Mathf.Sin(idleTime * actualIdleSpeed)         * r,
                Mathf.Cos(idleTime * actualIdleSpeed * 0.7f) * r,
                0f);
        }

        transform.localPosition = Vector3.Lerp(
            transform.localPosition, targetLocal, Time.deltaTime * actualTrackSpeed);
    }

    private float GetRandomDuration(EyeState state)
    {
        return state == EyeState.Track
            ? Random.Range(trackDurationMin, trackDurationMax)
            : Random.Range(idleDurationMin,  idleDurationMax);
    }

    public void SetPhase(int phase)
    {
        if (sr != null)
            sr.color = (phase >= 2) ? phase2Color : phase1Color;
    }
}
