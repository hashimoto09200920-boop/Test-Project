using UnityEngine;

/// <summary>
/// ダメージポップアップの固定プール管理。
/// 起動時に確保した固定数のDamagePopupを使い回し、実行中はInstantiate/Destroyを一切行わない。
/// 被弾が連続してプールを使い切った場合は、最も古いものを奪って再利用する（表示が途切れない）。
/// </summary>
public class DamagePopupManager : MonoBehaviour
{
    public static DamagePopupManager Instance { get; private set; }

    [Tooltip("使い回すDamagePopupのPrefab")]
    [SerializeField] private DamagePopup popupPrefab;

    [Tooltip("同時に確保しておくポップアップの最大数。画面内の敵数や連射頻度に応じて調整")]
    [SerializeField] private int poolSize = 32;

    private DamagePopup[] pool;
    private int nextIndex;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (popupPrefab == null)
        {
            Debug.LogWarning("[DamagePopupManager] popupPrefabが未設定です。", this);
            return;
        }

        pool = new DamagePopup[Mathf.Max(1, poolSize)];
        for (int i = 0; i < pool.Length; i++)
        {
            DamagePopup p = Instantiate(popupPrefab, transform);
            p.gameObject.SetActive(false);
            pool[i] = p;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Show(Vector3 position, int damage, bool isPowered, bool isShieldHit,
        float normalFontSize, float poweredFontSize, Color normalColor, Color poweredColor)
    {
        if (pool == null || pool.Length == 0) return;

        DamagePopup popup = pool[nextIndex];
        nextIndex = (nextIndex + 1) % pool.Length;

        popup.transform.position = position;
        popup.Activate(damage, isPowered, isShieldHit, normalFontSize, poweredFontSize, normalColor, poweredColor);
    }
}
