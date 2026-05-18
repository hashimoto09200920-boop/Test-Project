using UnityEngine;

/// <summary>
/// ハロウィンボス Phase2 の分身フラグメント（6体）。
/// 属性（カボチャ/コウモリ/魔女帽子）を持ち、記憶ゲームの当たり判定を担当する。
/// EnemyDamageReceiverは持たず、ヒット処理はHalloweenBossControllerに委譲する。
/// Collider2DはIsTrigger=trueで設定すること。
/// </summary>
public class HalloweenFragment : MonoBehaviour
{
    public enum AttributeType { Pumpkin, Bat, WitchHat }

    [Header("Visuals")]
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private Sprite normalSprite;
    [SerializeField] private Sprite pumpkinSprite;
    [SerializeField] private Sprite batSprite;
    [SerializeField] private Sprite witchHatSprite;
    [SerializeField] private Color glowColor = new Color(1f, 0.9f, 0.1f);

    [Header("Muzzle")]
    [Tooltip("射撃起点。未設定なら transform 位置を使用")]
    [SerializeField] private Transform muzzle;

    public AttributeType Attribute { get; set; }
    public bool IsSolid { get; private set; }

    private HalloweenBossController boss;
    private Collider2D col;
    private bool showingAttribute;

    private void Awake()
    {
        col = GetComponent<Collider2D>();
        if (bodyRenderer == null)
            bodyRenderer = GetComponent<SpriteRenderer>();
    }

    public void Init(HalloweenBossController bossController, AttributeType type)
    {
        boss = bossController;
        Attribute = type;
    }

    public void SetSolid(bool solid)
    {
        IsSolid = solid;
        if (col != null) col.enabled = solid;
    }

    public void ShowAttribute(bool show)
    {
        showingAttribute = show;
        RefreshSprite();
    }

    public void SetGlowing(bool glow)
    {
        if (bodyRenderer != null)
            bodyRenderer.color = glow ? glowColor : Color.white;
    }

    public void SetVisible(bool visible)
    {
        if (bodyRenderer != null) bodyRenderer.enabled = visible;
        if (col != null) col.enabled = visible && IsSolid;
    }

    private void RefreshSprite()
    {
        if (bodyRenderer == null) return;
        if (!showingAttribute)
        {
            bodyRenderer.sprite = normalSprite;
            return;
        }
        switch (Attribute)
        {
            case AttributeType.Pumpkin:  bodyRenderer.sprite = pumpkinSprite;  break;
            case AttributeType.Bat:      bodyRenderer.sprite = batSprite;      break;
            case AttributeType.WitchHat: bodyRenderer.sprite = witchHatSprite; break;
            default:                     bodyRenderer.sprite = normalSprite;   break;
        }
    }

    public Transform GetMuzzle() => muzzle != null ? muzzle : transform;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsSolid) return;
        EnemyBullet bullet = other.GetComponent<EnemyBullet>();
        if (bullet == null || !bullet.IsReflected) return;
        boss?.OnFragmentHit(this, bullet);
    }
}
