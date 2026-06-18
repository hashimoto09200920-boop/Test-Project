using UnityEngine;

public class MaskController : MonoBehaviour
{
    public enum MaskState { Appearing, Visible, Disappearing, Invisible }

    [Header("Cycle Timing")]
    [Tooltip("フェードイン時間（秒）")]
    [SerializeField] float fadeInDuration = 0.5f;
    [Tooltip("フェードイン完了後、フェードアウト開始までの待機時間（秒）")]
    [SerializeField] float visibleDuration = 0.8f;
    [Tooltip("フェードアウト時間（秒）")]
    [SerializeField] float fadeOutDuration = 0.4f;
    [Tooltip("ワープ後、次のフェードインまでの待機時間（秒）")]
    [SerializeField] float invisibleDuration = 0.5f;

    [Header("Scale Effect")]
    [Tooltip("フェードイン開始時 / フェードアウト終了時のスケール倍率")]
    [SerializeField] float spawnScale = 0.75f;

    [Header("Bob")]
    [Tooltip("上下浮遊の振幅（ワールド単位）")]
    [SerializeField] float bobAmplitude = 0.12f;
    [Tooltip("上下浮遊の周波数（1秒あたりの往復回数）")]
    [SerializeField] float bobFrequency = 1.0f;
    [Tooltip("左右ふらつきの振幅（ワールド単位）")]
    [SerializeField] float driftAmplitude = 0.6f;
    [Tooltip("左右ふらつきの周波数")]
    [SerializeField] float driftFrequency = 0.4f;

    [Header("Tilt")]
    [Tooltip("左右傾きの最大角度（度）")]
    [SerializeField] float tiltAmplitude = 5f;
    [Tooltip("左右傾きの周波数")]
    [SerializeField] float tiltFrequency = 0.7f;

    [Header("Warp Target")]
    [Tooltip("ワープ先として使うSpawnPointの最小インデックス（SP01=0）")]
    [SerializeField] int warpSpawnIndexMin = 0;
    [Tooltip("ワープ先として使うSpawnPointの最大インデックス（SP09=8）")]
    [SerializeField] int warpSpawnIndexMax = 8;

    [Header("References")]
    [SerializeField] SpriteRenderer spriteRenderer;
    [SerializeField] EnemySpriteShake spriteShake;

    private EnemyShooter _shooter;
    private MaskState _state;
    private float _stateTimer;
    private float _bobTime;
    private Vector3 _basePos;

    private float GetTimeScale() =>
        SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;

    private void Awake()
    {
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteShake == null) spriteShake = GetComponent<EnemySpriteShake>();
        _shooter = GetComponent<EnemyShooter>();

        var mover = GetComponent<EnemyMover>();
        if (mover != null) mover.suppressMovement = true;
        if (spriteShake != null) spriteShake.externalPositioning = true;
        if (_shooter != null) _shooter.enabled = false;

        SetAlpha(0f);
        SetLocalScale(spawnScale);
    }

    private void Start()
    {
        if (!Application.isPlaying) return;
        _basePos = transform.position;
        EnterState(MaskState.Appearing);
    }

    private void Update()
    {
        if (!Application.isPlaying) return;
        _stateTimer += Time.deltaTime * GetTimeScale();

        switch (_state)
        {
            case MaskState.Appearing:
            {
                float t = Mathf.Clamp01(_stateTimer / Mathf.Max(fadeInDuration, 0.001f));
                SetAlpha(t);
                SetLocalScale(Mathf.Lerp(spawnScale, 1f, t));
                ApplyBob();
                if (_stateTimer >= fadeInDuration)
                {
                    SetAlpha(1f);
                    SetLocalScale(1f);
                    if (_shooter != null)
                    {
                        var data = _shooter.GetEnemyData();
                        if (data != null) _shooter.SetEnemyData(data);
                        _shooter.enabled = true;
                    }
                    EnterState(MaskState.Visible);
                }
                break;
            }
            case MaskState.Visible:
            {
                ApplyBob();
                if (_stateTimer >= visibleDuration)
                {
                    if (_shooter != null) _shooter.enabled = false;
                    EnterState(MaskState.Disappearing);
                }
                break;
            }
            case MaskState.Disappearing:
            {
                float t = 1f - Mathf.Clamp01(_stateTimer / Mathf.Max(fadeOutDuration, 0.001f));
                SetAlpha(t);
                SetLocalScale(Mathf.Lerp(spawnScale, 1f, t));
                ApplyBob();
                if (_stateTimer >= fadeOutDuration)
                {
                    SetAlpha(0f);
                    SetLocalScale(spawnScale);
                    WarpToNewPosition();
                    EnterState(MaskState.Invisible);
                }
                break;
            }
            case MaskState.Invisible:
            {
                if (_stateTimer >= invisibleDuration)
                    EnterState(MaskState.Appearing);
                break;
            }
        }
    }

    private void ApplyBob()
    {
        _bobTime += Time.deltaTime * GetTimeScale();
        float xOff = Mathf.Sin(_bobTime * driftFrequency * Mathf.PI * 2f) * driftAmplitude;
        float yOff = Mathf.Sin(_bobTime * bobFrequency * Mathf.PI * 2f) * bobAmplitude;
        float tilt = Mathf.Sin(_bobTime * tiltFrequency * Mathf.PI * 2f) * tiltAmplitude;
        Vector3 shake = spriteShake != null ? spriteShake.CurrentOffset : Vector3.zero;
        transform.position = _basePos + new Vector3(xOff, yOff, 0f) + shake;
        transform.rotation = Quaternion.Euler(0f, 0f, tilt);
    }

    private void EnterState(MaskState newState)
    {
        _state = newState;
        _stateTimer = 0f;
    }

    private void WarpToNewPosition()
    {
        _basePos = ComputeWarpTarget();
        transform.position = _basePos;
        transform.rotation = Quaternion.identity;
    }

    private Vector3 ComputeWarpTarget()
    {
        var spawner = FindFirstObjectByType<EnemySpawner>();
        if (spawner != null)
        {
            int count = warpSpawnIndexMax - warpSpawnIndexMin + 1;
            int startIndex = Random.Range(0, count);
            for (int i = 0; i < count; i++)
            {
                int idx = warpSpawnIndexMin + (startIndex + i) % count;
                var pt = spawner.GetSpawnPoint(idx);
                if (pt != null)
                    return new Vector3(pt.position.x, pt.position.y, transform.position.z);
            }
        }
        return transform.position;
    }

    private void SetAlpha(float alpha)
    {
        if (spriteRenderer == null) return;
        var c = spriteRenderer.color;
        c.a = alpha;
        spriteRenderer.color = c;
    }

    private void SetLocalScale(float scale)
    {
        transform.localScale = Vector3.one * scale;
    }
}
