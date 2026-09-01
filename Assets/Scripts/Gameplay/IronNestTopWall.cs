using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 画面上端に横一列のブロックを配置する（FortressのPattern2 BottomWallの上端版）
/// </summary>
public class IronNestTopWall : MonoBehaviour
{
    [Header("Block")]
    [Tooltip("上端壁ブロックのPrefab（WallHealthコンポーネント必須）")]
    [SerializeField] private GameObject topWallBlockPrefab;

    [Tooltip("ブロック1個のワールド幅（敷き詰め間隔の計算に使用）")]
    [SerializeField] private float blockWidth = 1f;

    [Tooltip("生成するブロックの親Transform。未設定時はシーンルートに配置する。")]
    [SerializeField] private Transform blockRoot;

    [Header("Position")]
    [Tooltip("SkillHUDの横幅（ピクセル単位）。SkillHUDが見つからない場合のフォールバック値。")]
    [SerializeField] private float skillHudPixelWidth = 280f;

    [Tooltip("画面上端Y座標からのオフセット（ワールド単位）\n負の値で下にずらす")]
    [SerializeField] private float topWallYOffset = 0f;

    [Header("Timing")]
    [Tooltip("ドミノ配置の1ブロックあたりの表示間隔（秒）")]
    [SerializeField] private float dominoInterval = 0.1f;

    [Tooltip("ブロックの再配置間隔（秒）")]
    [SerializeField] private float replaceInterval = 18f;

    [Tooltip("再配置時：古いブロック消去から新しいブロック出現までの待機時間（秒）")]
    [SerializeField] private float replaceDelay = 1f;

    [Tooltip("消去前の点滅回数")]
    [SerializeField] private int blockBlinkCount = 3;

    [Tooltip("点滅1回あたりのOFF/ON時間（秒）")]
    [SerializeField] private float blockBlinkInterval = 0.12f;

    [Header("SE")]
    [Tooltip("ブロック出現時のSE")]
    [SerializeField] private AudioClip spawnClip;

    [Tooltip("SE再生用AudioSource（未設定時はPlayClipAtPointで再生）")]
    [SerializeField] private AudioSource spawnAudioSource;

    [Range(0f, 1f)]
    [SerializeField] private float spawnVolume = 1f;

    // =========================================================
    // ランタイム
    // =========================================================

    private readonly List<GameObject> blocks = new List<GameObject>();
    private bool replacing = false;
    private bool leftToRight = true;
    private float replaceTimer;
    private RectTransform skillHudCachedRect;

    private static float MasterSEVolume => SoundSettingsManager.Instance != null ? SoundSettingsManager.Instance.SEVolume : 1f;

    private void Start()
    {
        var hudGo = GameObject.Find("SkillHUD");
        if (hudGo != null) skillHudCachedRect = hudGo.GetComponent<RectTransform>();

        replaceTimer = replaceInterval;
        StartCoroutine(SpawnDomino());
    }

    private void Update()
    {
        if (replacing) return;
        replaceTimer -= Time.deltaTime;
        if (replaceTimer <= 0f)
        {
            replaceTimer = replaceInterval;
            StartCoroutine(Replace());
        }
    }

    private void OnDestroy()
    {
        DestroyBlocks();
    }

    // =========================================================
    // スポーン
    // =========================================================

    private IEnumerator SpawnDomino()
    {
        if (topWallBlockPrefab == null) yield break;

        yield return null; // Camera.main の初期化を待つ

        if (Camera.main == null) yield break;

        float halfH    = Camera.main.orthographicSize;
        float halfW    = halfH * Camera.main.aspect;
        Vector3 camPos = Camera.main.transform.position;

        float xMin = GetSkillHudRightWorldX(camPos.x, halfW);
        // ★右端をカメラ端ちょうど(halfW)にすると、右端タイルの外側半分が画面外にはみ出し
        //   スマホ実機で見切れる原因になっていた。ブロック半分幅ぶん内側に寄せる。
        float xMax = camPos.x + halfW - blockWidth * 0.5f;
        float y    = camPos.y + halfH + topWallYOffset; // 画面上端 + Yオフセット

        float span    = xMax - xMin;
        int   count   = Mathf.Max(1, Mathf.RoundToInt(span / Mathf.Max(blockWidth, 0.01f)));
        float spacing = span / count;

        for (int i = 0; i < count; i++)
        {
            int idx = leftToRight ? i : (count - 1 - i);
            float x = xMin + spacing * (idx + 0.5f);
            Vector3 pos = new Vector3(x, y, transform.position.z);
            GameObject block = Instantiate(topWallBlockPrefab, pos, Quaternion.identity, blockRoot);
            blocks.Add(block);

            if (spawnClip != null)
            {
                float volume = spawnVolume * MasterSEVolume;
                if (spawnAudioSource != null)
                    spawnAudioSource.PlayOneShot(spawnClip, volume);
                else
                    AudioSource.PlayClipAtPoint(spawnClip, pos, volume);
            }

            yield return new WaitForSeconds(dominoInterval);
        }
    }

    // =========================================================
    // 再配置
    // =========================================================

    private IEnumerator Replace()
    {
        replacing = true;
        yield return StartCoroutine(BlinkAndDestroy());
        yield return new WaitForSeconds(replaceDelay);
        leftToRight = !leftToRight;
        yield return StartCoroutine(SpawnDomino());
        replacing = false;
    }

    // =========================================================
    // ヘルパー
    // =========================================================

    private float GetSkillHudRightWorldX(float camX, float halfW)
    {
        if (skillHudCachedRect != null)
        {
            Canvas rootCanvas = skillHudCachedRect.GetComponentInParent<Canvas>();
            if (rootCanvas != null)
            {
                RectTransform canvasRT = rootCanvas.GetComponent<RectTransform>();
                Vector3[] canvasCorners = new Vector3[4];
                Vector3[] hudCorners    = new Vector3[4];
                canvasRT.GetWorldCorners(canvasCorners);
                skillHudCachedRect.GetWorldCorners(hudCorners);

                float canvasLeft  = canvasCorners[0].x;
                float canvasRight = canvasCorners[2].x;
                float canvasWidth = canvasRight - canvasLeft;

                if (canvasWidth > 0.001f)
                {
                    float hudRight = Mathf.Max(hudCorners[2].x, hudCorners[3].x);
                    float t = (hudRight - canvasLeft) / canvasWidth;
                    return camX - halfW + t * (halfW * 2f);
                }
            }
        }
        if (skillHudPixelWidth > 0f && Screen.width > 0)
            return camX - halfW + skillHudPixelWidth * ((halfW * 2f) / Screen.width);
        return camX - halfW;
    }

    private IEnumerator BlinkAndDestroy()
    {
        float totalWait = 0f;
        foreach (var b in blocks)
        {
            if (b == null) continue;
            if (!b.activeInHierarchy) { Destroy(b); continue; }
            WallHealth wh = b.GetComponent<WallHealth>();
            if (wh != null)
                wh.StartBlinkAndDestroy(blockBlinkCount, blockBlinkInterval);
            else
                Destroy(b);
        }
        totalWait = blockBlinkCount * blockBlinkInterval * 2f;
        blocks.Clear();
        yield return new WaitForSeconds(totalWait);
    }

    private void DestroyBlocks()
    {
        foreach (var b in blocks)
            if (b != null) Destroy(b);
        blocks.Clear();
    }
}
