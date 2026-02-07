using UnityEngine;

[RequireComponent(typeof(EnemyBullet))]
public class EnemyBulletDebugText : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 1.0f, 0f);
    [SerializeField] private float characterSize = 0.18f;
    [SerializeField] private int fontSize = 48;

    [Header("Visibility")]
    [Tooltip("ON: デバッグテキストを表示する。OFF: 非表示")]
    [SerializeField] private bool showDebugText = true;

    private EnemyBullet bullet;
    private TextMesh textMesh;
    private GameObject textObject;

    // ★跳ね返り回数表示用
    private int initialRemainingBounces;
    private bool infiniteBounce;

    private void Awake()
    {
        bullet = GetComponent<EnemyBullet>();

        textObject = new GameObject("BulletDebug_Text");
        textObject.transform.SetParent(transform);
        textObject.transform.localPosition = offset;

        textMesh = textObject.AddComponent<TextMesh>();
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.fontSize = fontSize;
        textMesh.characterSize = characterSize;
        textMesh.color = Color.white;
        textMesh.text = "";

        // ★初期跳ね返り回数を保存
        initialRemainingBounces = bullet.RemainingPaddleBounces;
        infiniteBounce = initialRemainingBounces < 0;

        // ★表示ON/OFF設定を適用
        if (textObject != null)
        {
            textObject.SetActive(showDebugText);
        }
    }

    public void SetDebugTextEnabled(bool enabled)
    {
        showDebugText = enabled;
        if (textObject != null)
        {
            textObject.SetActive(enabled);
        }
    }

    private void LateUpdate()
    {
        if (!showDebugText || bullet == null || textMesh == null || textObject == null) return;

        // 加速表示
        string accelMaxText = (bullet.AccelMaxCountLast > 0)
            ? bullet.AccelMaxCountLast.ToString()
            : "?";

        // 跳ね返り表示
        string bounceText;
        if (infiniteBounce)
        {
            bounceText = "B ∞";
        }
        else
        {
            int used = Mathf.Max(0, initialRemainingBounces - bullet.RemainingPaddleBounces);
            bounceText = $"B {used}/{initialRemainingBounces}";
        }

        // 表示合成
        textMesh.text =
            $"{bullet.CurrentSpeed:0.0} " +
            $"{bullet.AccelCount}/{accelMaxText} " +
            $"{bounceText}";

        // 位置更新
        textMesh.transform.localPosition = offset;

        // ★Spiral Motion等で親が回転していても、テキストは常に水平を維持
        textObject.transform.rotation = Quaternion.identity;
    }
}
