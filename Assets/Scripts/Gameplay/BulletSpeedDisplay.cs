using UnityEngine;
using TMPro;

/// <summary>
/// 弾の速度をGame画面上に表示する専用コンポーネント
/// </summary>
[RequireComponent(typeof(EnemyBullet))]
public class BulletSpeedDisplay : MonoBehaviour
{
    [Header("表示設定")]
    [SerializeField] private bool showSpeed = true;
    [SerializeField] private Vector3 offset = new Vector3(0f, 0.5f, 0f);
    [SerializeField] private float fontSize = 3f;
    [SerializeField] private Color textColor = Color.yellow;

    private EnemyBullet bullet;
    private GameObject textObject;
    private TextMeshPro textMesh;

    private void Awake()
    {
        bullet = GetComponent<EnemyBullet>();

        if (!showSpeed) return;

        // TextMeshProオブジェクト作成
        textObject = new GameObject("SpeedDisplay");
        textObject.transform.SetParent(transform);
        textObject.transform.localPosition = offset;

        // TextMeshPro コンポーネント追加
        textMesh = textObject.AddComponent<TextMeshPro>();
        textMesh.fontSize = fontSize;
        textMesh.color = textColor;
        textMesh.alignment = TextAlignmentOptions.Center;
        textMesh.text = "0.0";

        // 常に水平を維持
        textObject.transform.rotation = Quaternion.identity;
    }

    private void LateUpdate()
    {
        if (!showSpeed || textMesh == null || bullet == null) return;

        // 速度を表示
        textMesh.text = $"{bullet.CurrentSpeed:F1}";

        // 常に水平を維持
        textObject.transform.rotation = Quaternion.identity;
    }

    /// <summary>
    /// 表示ON/OFF切り替え
    /// </summary>
    public void SetDisplayEnabled(bool enabled)
    {
        showSpeed = enabled;
        if (textObject != null)
        {
            textObject.SetActive(enabled);
        }
    }
}
