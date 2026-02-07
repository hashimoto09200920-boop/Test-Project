using UnityEngine;
using TMPro;

public class DamagePopup : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private TextMeshPro text;

    [Header("Motion")]
    [SerializeField] private Vector3 moveVelocity = new Vector3(0f, 1.2f, 0f);
    [SerializeField] private float lifeTime = 0.6f;

    [Header("Fade")]
    [SerializeField] private bool fadeOut = true;

    private float timer;
    private Color baseColor;

    private void Awake()
    {
        if (text == null) text = GetComponentInChildren<TextMeshPro>();
        if (text != null) baseColor = text.color;
    }

    public void Setup(int damage, bool isPowered, float normalFontSize, float poweredFontSize, Color normalColor, Color poweredColor)
    {
        if (text == null) return;

        // (2) 表示から「-」を除去（見た目だけ）
        text.text = Mathf.Abs(damage).ToString();

        if (isPowered)
        {
            text.fontSize = poweredFontSize;
            text.color = poweredColor;
        }
        else
        {
            text.fontSize = normalFontSize;
            text.color = normalColor;
        }

        baseColor = text.color;
    }

    private void Update()
    {
        timer += Time.deltaTime;
        transform.position += moveVelocity * Time.deltaTime;

        if (fadeOut && text != null)
        {
            float t = Mathf.Clamp01(timer / lifeTime);
            Color c = baseColor;
            c.a = 1f - t;
            text.color = c;
        }

        if (timer >= lifeTime)
        {
            Destroy(gameObject);
        }
    }
}
