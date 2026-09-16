using UnityEngine;

// 背景装飾を、ごくわずかに左右へ揺れるように回転させる（振り子のような自転）。
// SlowDrift（transform.position）やBreathingGlow（SpriteRenderer.color）とは
// 別のプロパティ（transform.rotation）だけを触るため、同じGameObjectに共存させても競合しない。
// 揺れは常に「起動時の向き」を基準に絶対値で計算するため、蓄積誤差やドリフトは発生しない。
public class GentleWobble : MonoBehaviour
{
    [Tooltip("最大回転角度（度）。0を中心に-angle〜+angleの範囲で揺れる")]
    [SerializeField] private float maxAngle = 3f;

    [Tooltip("揺れの周期（秒）")]
    [SerializeField] private float periodSeconds = 6f;

    private Quaternion baseRotation;

    private void Awake()
    {
        baseRotation = transform.rotation;
    }

    private void Update()
    {
        float angle = Mathf.Sin(Time.time * (2f * Mathf.PI / periodSeconds)) * maxAngle;
        transform.rotation = baseRotation * Quaternion.Euler(0f, 0f, angle);
    }
}
