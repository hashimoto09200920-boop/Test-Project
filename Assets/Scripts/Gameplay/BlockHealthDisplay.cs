using UnityEngine;

/// <summary>
/// ブロックのHP表示（ブロックの上に数値表示）
/// </summary>
[RequireComponent(typeof(WallHealth))]
public class BlockHealthDisplay : MonoBehaviour
{
    [Header("Display Settings")]
    [Tooltip("HP表示のY方向オフセット（ブロックからの距離）")]
    [SerializeField] private float displayOffsetY = 0.6f;
    [Tooltip("数値テキストのフォントサイズ")]
    [SerializeField] private int fontSize = 60;
    [Tooltip("HP表示色")]
    [SerializeField] private Color hpColor = Color.white;

    private WallHealth wallHealth;
    private TextMesh hpNumberText;

    private void Awake()
    {
        wallHealth = GetComponent<WallHealth>();

        // HP数値テキストを作成
        GameObject hpNumberObject = new GameObject("HP_Number");
        hpNumberObject.transform.SetParent(transform);
        hpNumberObject.transform.localPosition = new Vector3(0f, displayOffsetY, 0f);

        hpNumberText = hpNumberObject.AddComponent<TextMesh>();
        hpNumberText.anchor = TextAnchor.MiddleCenter;
        hpNumberText.alignment = TextAlignment.Center;
        hpNumberText.fontSize = fontSize;
        hpNumberText.characterSize = 0.05f;
        hpNumberText.color = hpColor;
        hpNumberText.text = "";
    }

    private void LateUpdate()
    {
        if (wallHealth == null || hpNumberText == null) return;

        // WallHealthから現在HPを取得（privateなのでリフレクション使用）
        var field = typeof(WallHealth).GetField("currentHp",
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);

        if (field != null)
        {
            int currentHp = (int)field.GetValue(wallHealth);

            // HP表示（破壊されたら非表示）
            if (currentHp > 0)
            {
                hpNumberText.text = $"{currentHp}";
            }
            else
            {
                hpNumberText.text = "";
            }
        }
    }
}
