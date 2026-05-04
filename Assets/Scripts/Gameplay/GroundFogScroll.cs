using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class GroundFogScroll : MonoBehaviour
{
    [Tooltip("上下に揺れる振幅（ワールド単位）")]
    [Range(0.05f, 1.0f)]
    [SerializeField] private float riseAmplitude = 0.3f;

    [Tooltip("揺れの速さ（Hz）")]
    [Range(0.01f, 1.0f)]
    [SerializeField] private float riseFrequency = 0.15f;

    [Tooltip("左右の揺らぎ振幅（ワールド単位）")]
    [Range(0f, 0.5f)]
    [SerializeField] private float driftAmplitude = 0.1f;

    [Tooltip("左右の揺らぎ速さ（Hz）")]
    [Range(0.01f, 1.0f)]
    [SerializeField] private float driftFrequency = 0.1f;

    private float baseY;
    private float baseX;
    private bool initialized;

    private void Update()
    {
        if (!initialized)
        {
            baseY = transform.localPosition.y;
            baseX = transform.localPosition.x;
            initialized = true;
        }

        float y = baseY + Mathf.Sin(Time.time * riseFrequency * Mathf.PI * 2f) * riseAmplitude;
        float x = baseX + Mathf.Sin(Time.time * driftFrequency * Mathf.PI * 2f) * driftAmplitude;
        transform.localPosition = new Vector3(x, y, transform.localPosition.z);
    }
}
