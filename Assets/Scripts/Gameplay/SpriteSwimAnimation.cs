using UnityEngine;

/// <summary>
/// 1枚スプライトで泳いでいるように見せるアニメーションコンポーネント。
/// 回転の揺れ + X方向スケールの伸縮をsin波で組み合わせる。
/// Play前のInspectorで全パラメータ調整可能。
/// </summary>
public class SpriteSwimAnimation : MonoBehaviour
{
    [Header("Rotation")]
    [Tooltip("回転の最大角度（度）。例: 10 = ±10度揺れる")]
    [Range(0f, 45f)]
    public float rotationAmplitude = 5f;

    [Tooltip("回転の速度（1秒あたりの往復回数）。例: 1.5 = 1秒で1.5往復")]
    [Range(0.1f, 5f)]
    public float rotationFrequency = 1.5f;

    [Header("Scale X (うねり)")]
    [Tooltip("Xスケールの伸縮幅。例: 0.08 = 基準値±0.08伸縮")]
    [Range(0f, 0.5f)]
    public float scaleAmplitude = 0.04f;

    [Tooltip("スケール伸縮の速度（1秒あたりの往復回数）。rotationFrequencyの2倍推奨")]
    [Range(0.1f, 10f)]
    public float scaleFrequency = 3f;

    [Header("Phase Offset")]
    [Tooltip("回転とスケールの位相ずらし（ラジアン）。π/2（1.57）推奨")]
    public float phaseOffset = Mathf.PI / 2f;

    [Header("Random Start Phase")]
    [Tooltip("ON: 開始位相をランダムにする（複数敵が同じ動きにならないように）")]
    public bool useRandomStartPhase = true;

    private Vector3 baseScale;
    private float startPhase;
    private Vector2 prevPosition;
    private bool facingRight = true;
    private EnemySpriteShake spriteShake;

    private void Start()
    {
        spriteShake = GetComponent<EnemySpriteShake>();
        baseScale = transform.localScale;
        startPhase = useRandomStartPhase ? Random.Range(0f, Mathf.PI * 2f) : 0f;
        prevPosition = (Vector2)transform.position;
    }

    private void Update()
    {
        // シェイク中は向き判定をスキップ（シェイクのlocalPosition変化で誤判定するため）
        // prevPositionは現在位置に追従させてシェイク終了後の差分を正確にする
        if (spriteShake != null && spriteShake.IsShaking)
        {
            prevPosition = (Vector2)transform.position;
        }
        else
        {
            Vector2 currentPos = (Vector2)transform.position;
            float deltaX = currentPos.x - prevPosition.x;
            if (deltaX < -0.001f) facingRight = false;
            else if (deltaX > 0.001f) facingRight = true;
            prevPosition = currentPos;
        }

        float t = Time.time + startPhase;

        // 回転
        float angle = Mathf.Sin(t * rotationFrequency * Mathf.PI * 2f) * rotationAmplitude;
        transform.localRotation = Quaternion.Euler(0f, 0f, angle);

        // Xスケール伸縮 + 左向き時にX反転（Yは変えない）
        float scaleX = baseScale.x + Mathf.Sin(t * scaleFrequency * Mathf.PI * 2f + phaseOffset) * scaleAmplitude;
        float signX = facingRight ? 1f : -1f;
        transform.localScale = new Vector3(scaleX * signX, baseScale.y, baseScale.z);
    }
}
