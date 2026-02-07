using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 煙幕弾から生成される煙のエフェクトを管理
/// - 徐々に拡散
/// - 視界制限（範囲内のオブジェクトを非表示）
/// - 白線/赤線の円判定で消滅
/// </summary>
public class SmokeCloud : MonoBehaviour
{
    [Header("Smoke Parameters")]
    [SerializeField] private float maxRadius = 3f;
    [SerializeField] private float duration = 5f;
    [SerializeField] private float expansionSpeed = 0.5f;

    [Header("Visual")]
    [SerializeField] private ParticleSystem smokeParticle;
    [SerializeField] private CircleCollider2D smokeTrigger; // 視界制限用のトリガー

    [Header("Rendering")]
    [Tooltip("煙の描画レイヤー名（未設定なら\"Default\"）")]
    [SerializeField] private string sortingLayerName = "Default";

    [Tooltip("煙の描画順序（大きいほど手前）。弾/敵/プレイヤーより大きい値を設定。デフォルト1000で最前面")]
    [SerializeField] private int sortingOrder = 1000;

    [Header("Audio")]
    [Tooltip("煙が円判定で消滅する時のSE（未設定なら鳴らない）")]
    [SerializeField] private AudioClip circleDissolveClip;

    private float currentRadius = 0f;
    private float elapsedTime = 0f;
    private HashSet<GameObject> hiddenObjects = new HashSet<GameObject>(); // 非表示にしたオブジェクト

    public void Initialize(float radius, float dur, float expSpeed)
    {
        Initialize(radius, dur, expSpeed, null);
    }

    public void Initialize(float radius, float dur, float expSpeed, AudioClip dissolveSE)
    {
        maxRadius = Mathf.Max(1f, radius);
        duration = Mathf.Max(1f, dur);
        expansionSpeed = Mathf.Max(0.1f, expSpeed);
        circleDissolveClip = dissolveSE;
        currentRadius = 0.1f;
        elapsedTime = 0f;

        // パーティクルシステムの設定
        if (smokeParticle != null)
        {
            var main = smokeParticle.main;
            main.startLifetime = duration;
            main.startSpeed = expansionSpeed;

            var shape = smokeParticle.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.1f; // 初期は小さく

            smokeParticle.Play();
        }

        // トリガーコライダーの設定
        if (smokeTrigger == null)
        {
            smokeTrigger = gameObject.AddComponent<CircleCollider2D>();
        }
        smokeTrigger.isTrigger = true;
        smokeTrigger.radius = currentRadius;

        // 自動削除
        Destroy(gameObject, duration);
    }

    private void Awake()
    {
        // Awake時にコンポーネントを取得
        if (smokeParticle == null)
        {
            smokeParticle = GetComponentInChildren<ParticleSystem>();
        }

        if (smokeTrigger == null)
        {
            smokeTrigger = GetComponent<CircleCollider2D>();
            if (smokeTrigger == null)
            {
                smokeTrigger = gameObject.AddComponent<CircleCollider2D>();
                smokeTrigger.isTrigger = true;
            }
        }

        // 煙の描画順序を設定（最前面に表示）
        ApplySortingOrder();
    }

    private void ApplySortingOrder()
    {
        if (smokeParticle == null) return;

        ParticleSystemRenderer renderer = smokeParticle.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.sortingLayerName = sortingLayerName;
            renderer.sortingOrder = sortingOrder;
        }
    }

    private void Update()
    {
        elapsedTime += Time.deltaTime;

        // 徐々に拡散
        if (currentRadius < maxRadius)
        {
            currentRadius += expansionSpeed * Time.deltaTime;
            currentRadius = Mathf.Min(currentRadius, maxRadius);

            // コライダーサイズ更新
            if (smokeTrigger != null)
            {
                smokeTrigger.radius = currentRadius;
            }

            // パーティクルの範囲更新
            if (smokeParticle != null)
            {
                var shape = smokeParticle.shape;
                shape.radius = currentRadius;
            }
        }

        // 持続時間終了で消滅
        if (elapsedTime >= duration)
        {
            RestoreAllHiddenObjects();
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 視界制限対象のオブジェクトを非表示にする
        if (ShouldHideObject(other.gameObject))
        {
            HideObject(other.gameObject);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        // 煙の範囲から出たら再表示
        if (hiddenObjects.Contains(other.gameObject))
        {
            ShowObject(other.gameObject);
        }
    }

    private bool ShouldHideObject(GameObject obj)
    {
        if (obj == null) return false;

        // 未反射弾・反射弾・敵を非表示にする
        if (obj.GetComponent<EnemyBullet>() != null) return true;
        if (obj.GetComponent<EnemyMover>() != null) return true;
        if (obj.GetComponentInParent<EnemyMover>() != null) return true;

        return false;
    }

    private void HideObject(GameObject obj)
    {
        if (obj == null) return;
        if (hiddenObjects.Contains(obj)) return;

        // SpriteRendererを非表示
        SpriteRenderer[] renderers = obj.GetComponentsInChildren<SpriteRenderer>();
        foreach (var renderer in renderers)
        {
            renderer.enabled = false;
        }

        hiddenObjects.Add(obj);
        Debug.Log($"[SmokeCloud] Hid object: {obj.name}");
    }

    private void ShowObject(GameObject obj)
    {
        if (obj == null) return;
        if (!hiddenObjects.Contains(obj)) return;

        // SpriteRendererを再表示
        SpriteRenderer[] renderers = obj.GetComponentsInChildren<SpriteRenderer>();
        foreach (var renderer in renderers)
        {
            renderer.enabled = true;
        }

        hiddenObjects.Remove(obj);
        Debug.Log($"[SmokeCloud] Showed object: {obj.name}");
    }

    private void RestoreAllHiddenObjects()
    {
        // 煙消滅時に全オブジェクトを再表示
        foreach (var obj in hiddenObjects)
        {
            if (obj != null)
            {
                SpriteRenderer[] renderers = obj.GetComponentsInChildren<SpriteRenderer>();
                foreach (var renderer in renderers)
                {
                    renderer.enabled = true;
                }
            }
        }
        hiddenObjects.Clear();
    }

    /// <summary>
    /// 白線/赤線で囲まれた時に呼ばれる（PaddleDrawerから呼び出し想定）
    /// </summary>
    public void DissolveByCircle(Vector2 circleCenter, float circleRadius)
    {
        // 煙の中心が円内にあるかチェック
        float distance = Vector2.Distance(transform.position, circleCenter);
        if (distance <= circleRadius)
        {
            Debug.Log($"[SmokeCloud] Dissolved by circle at {circleCenter} with radius {circleRadius}");

            // 煙消滅SEを再生
            if (circleDissolveClip != null)
            {
                AudioSource.PlayClipAtPoint(circleDissolveClip, transform.position, 1f);
            }

            RestoreAllHiddenObjects();
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        // 念のため全オブジェクトを再表示
        RestoreAllHiddenObjects();
    }
}
