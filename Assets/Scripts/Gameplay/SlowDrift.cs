using UnityEngine;

// 背景装飾を、起動時の位置を中心にごくゆっくり往復させる。手前の要素より遅く・小さく
// 動かすことで「奥にあるほどゆっくり動いて見える」というパララックス（視差）の原理を
// 利用し、奥行きの説得力を補強する。基準位置からの相対オフセットで毎フレーム位置を
// 決め直す方式なので、一方向に流れ続けて画面外へ出て行くことは構造上起こらない。
public class SlowDrift : MonoBehaviour
{
    [Tooltip("往復する方向（正規化しなくてOK）")]
    [SerializeField] private Vector2 direction = Vector2.right;

    [Tooltip("基準位置からの振れ幅（ワールド単位）。この距離より遠くへは動かない")]
    [SerializeField] private float amplitude = 0.3f;

    [Tooltip("往復1周期の時間（秒）。長いほどゆっくり漂う")]
    [SerializeField] private float periodSeconds = 45f;

    private Vector3 basePosition;

    private void Awake()
    {
        basePosition = transform.position;
    }

    private void Update()
    {
        float offset = Mathf.Sin(Time.time * (2f * Mathf.PI / periodSeconds)) * amplitude;
        transform.position = basePosition + (Vector3)(direction.normalized * offset);
    }
}
