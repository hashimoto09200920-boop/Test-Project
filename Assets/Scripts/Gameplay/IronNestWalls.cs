using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// IronNestの画面下端・左右端にブロック壁を配置する
/// FortressEnemyのPattern2（Bottom Wall）とPattern4（Side Walls）を移植
/// </summary>
public class IronNestWalls : MonoBehaviour
{
    // =========================================================
    // Bottom Wall
    // =========================================================
    [Header("Bottom Wall")]
    [Tooltip("底面壁ブロックのPrefab（WallHealthコンポーネント必須）")]
    [SerializeField] private GameObject bottomWallBlockPrefab;

    [Tooltip("ブロック1個のワールド幅（敷き詰め間隔の計算に使用）")]
    [SerializeField] private float blockWidth = 1f;

    [Tooltip("画面最下端Y座標からのオフセット（ワールド単位）\n正の値で上にずらす")]
    [SerializeField] private float bottomWallYOffset = 0f;

    [Tooltip("ドミノ配置の1ブロックあたりの表示間隔（秒）")]
    [SerializeField] private float bottomWallDominoInterval = 0.1f;

    [Tooltip("底面壁ブロックの再配置間隔（秒）")]
    [SerializeField] private float bottomWallReplaceInterval = 18f;

    [Tooltip("底面壁ブロック出現時のSE")]
    [SerializeField] private AudioClip bottomWallSpawnClip;

    [Tooltip("SE再生用AudioSource（未設定時はPlayClipAtPointで再生）")]
    [SerializeField] private AudioSource bottomWallSpawnAudioSource;

    [Range(0f, 1f)]
    [SerializeField] private float bottomWallSpawnVolume = 1f;

    // =========================================================
    // Side Walls
    // =========================================================
    [Header("Side Walls")]
    [Tooltip("左右縦壁ブロックのPrefab（WallHealthコンポーネント必須）")]
    [SerializeField] private GameObject sideWallBlockPrefab;

    [Tooltip("ブロック1個のワールド高さ（敷き詰め間隔の計算に使用）")]
    [SerializeField] private float sideBlockHeight = 1f;

    [Tooltip("画面左端X座標からのオフセット（ワールド単位）\n正の値で右にずらす")]
    [SerializeField] private float sideWallLeftXOffset = 0f;

    [Tooltip("画面右端X座標からのオフセット（ワールド単位）\n正の値で左にずらす")]
    [SerializeField] private float sideWallRightXOffset = 0f;

    [Tooltip("ドミノ配置の1ブロックあたりの表示間隔（秒）")]
    [SerializeField] private float sideWallDominoInterval = 0.1f;

    [Tooltip("true=下から上、false=上から下にドミノ配置")]
    [SerializeField] private bool sideWallBottomToTop = true;

    [Tooltip("左右縦壁ブロックの再配置間隔（秒）")]
    [SerializeField] private float sideWallReplaceInterval = 20f;

    [Tooltip("サイドウォールブロック出現時のSE")]
    [SerializeField] private AudioClip sideWallSpawnClip;

    [Tooltip("SE再生用AudioSource（未設定時はPlayClipAtPointで再生）")]
    [SerializeField] private AudioSource sideWallSpawnAudioSource;

    [Range(0f, 1f)]
    [SerializeField] private float sideWallSpawnVolume = 1f;

    // =========================================================
    // Shared Settings
    // =========================================================
    [Header("Shared Settings")]
    [Tooltip("SkillHUDの横幅（ピクセル単位）。SkillHUDが見つからない場合のフォールバック値。")]
    [SerializeField] private float skillHudPixelWidth = 280f;

    [Tooltip("生成するブロックの親Transform。未設定時はシーンルートに配置する。")]
    [SerializeField] private Transform blockRoot;

    [Tooltip("再配置時：古いブロック消去から新しいブロック出現までの待機時間（秒）")]
    [SerializeField] private float replaceDelay = 1f;

    [Tooltip("消去前の点滅回数")]
    [SerializeField] private int blockBlinkCount = 3;

    [Tooltip("点滅1回あたりのOFF/ON時間（秒）")]
    [SerializeField] private float blockBlinkInterval = 0.12f;

    // =========================================================
    // ランタイム
    // =========================================================
    private readonly List<GameObject> bottomWallBlocks = new List<GameObject>();
    private readonly List<GameObject> sideWallBlocks   = new List<GameObject>();

    private bool bottomWallLeftToRight = true;
    private bool bottomWallReplacing   = false;
    private bool sideWallReplacing     = false;

    private float bottomWallTimer;
    private float sideWallTimer;

    private RectTransform skillHudCachedRect;

    // =========================================================
    // Unity ライフサイクル
    // =========================================================
    private void Start()
    {
        var hudGo = GameObject.Find("SkillHUD");
        if (hudGo != null) skillHudCachedRect = hudGo.GetComponent<RectTransform>();

        bottomWallTimer = bottomWallReplaceInterval;
        sideWallTimer   = sideWallReplaceInterval;

        StartCoroutine(SpawnBottomWallDomino());
        StartCoroutine(SpawnSideWallDomino());
    }

    private void Update()
    {
        if (!bottomWallReplacing)
        {
            bottomWallTimer -= Time.deltaTime;
            if (bottomWallTimer <= 0f)
            {
                bottomWallTimer = bottomWallReplaceInterval;
                StartCoroutine(ReplaceBottomWall());
            }
        }

        if (!sideWallReplacing)
        {
            sideWallTimer -= Time.deltaTime;
            if (sideWallTimer <= 0f)
            {
                sideWallTimer = sideWallReplaceInterval;
                StartCoroutine(ReplaceSideWalls());
            }
        }
    }

    private void OnDestroy()
    {
        DestroyBlockList(bottomWallBlocks);
        DestroyBlockList(sideWallBlocks);
    }

    // =========================================================
    // Bottom Wall スポーン
    // =========================================================
    private IEnumerator SpawnBottomWallDomino()
    {
        if (bottomWallBlockPrefab == null) yield break;

        yield return null; // Camera.main の初期化を待つ
        if (Camera.main == null) yield break;

        float halfH    = Camera.main.orthographicSize;
        float halfW    = halfH * Camera.main.aspect;
        Vector3 camPos = Camera.main.transform.position;

        float xMin = GetSkillHudRightWorldX(camPos.x, halfW);
        float xMax = camPos.x + halfW;
        float y    = camPos.y - halfH + bottomWallYOffset;

        float span    = xMax - xMin;
        int   count   = Mathf.Max(1, Mathf.RoundToInt(span / Mathf.Max(blockWidth, 0.01f)));
        float spacing = span / count;

        for (int i = 0; i < count; i++)
        {
            int idx = bottomWallLeftToRight ? i : (count - 1 - i);
            float x = xMin + spacing * (idx + 0.5f);
            Vector3 pos = new Vector3(x, y, transform.position.z);
            GameObject block = Instantiate(bottomWallBlockPrefab, pos, Quaternion.identity, blockRoot);
            bottomWallBlocks.Add(block);
            PlaySpawnSE(bottomWallSpawnClip, bottomWallSpawnAudioSource, bottomWallSpawnVolume, pos);
            yield return new WaitForSeconds(bottomWallDominoInterval);
        }
    }

    // =========================================================
    // Side Walls スポーン
    // =========================================================
    private IEnumerator SpawnSideWallDomino()
    {
        if (sideWallBlockPrefab == null) yield break;

        yield return null; // Camera.main の初期化を待つ
        if (Camera.main == null) yield break;

        float halfH    = Camera.main.orthographicSize;
        float halfW    = halfH * Camera.main.aspect;
        Vector3 camPos = Camera.main.transform.position;

        float leftX  = GetSkillHudRightWorldX(camPos.x, halfW) + sideWallLeftXOffset;
        float rightX = camPos.x + halfW - sideWallRightXOffset;

        float yMin    = camPos.y - halfH;
        float yMax    = camPos.y + halfH;
        float span    = yMax - yMin;
        int   count   = Mathf.Max(1, Mathf.RoundToInt(span / Mathf.Max(sideBlockHeight, 0.01f)));
        float spacing = span / count;

        for (int i = 0; i < count; i++)
        {
            int idx = sideWallBottomToTop ? i : (count - 1 - i);
            float y = yMin + spacing * (idx + 0.5f);

            Vector3 leftPos  = new Vector3(leftX,  y, transform.position.z);
            Vector3 rightPos = new Vector3(rightX, y, transform.position.z);

            GameObject leftBlock  = Instantiate(sideWallBlockPrefab, leftPos,  Quaternion.identity, blockRoot);
            GameObject rightBlock = Instantiate(sideWallBlockPrefab, rightPos, Quaternion.identity, blockRoot);
            sideWallBlocks.Add(leftBlock);
            sideWallBlocks.Add(rightBlock);

            PlaySpawnSE(sideWallSpawnClip, sideWallSpawnAudioSource, sideWallSpawnVolume, leftPos);
            yield return new WaitForSeconds(sideWallDominoInterval);
        }
    }

    // =========================================================
    // 再配置
    // =========================================================
    private IEnumerator ReplaceBottomWall()
    {
        bottomWallReplacing = true;
        yield return StartCoroutine(BlinkAndDestroyList(bottomWallBlocks));
        yield return new WaitForSeconds(replaceDelay);
        bottomWallLeftToRight = !bottomWallLeftToRight;
        yield return StartCoroutine(SpawnBottomWallDomino());
        bottomWallReplacing = false;
    }

    private IEnumerator ReplaceSideWalls()
    {
        sideWallReplacing = true;
        DestroyBlockList(sideWallBlocks);
        yield return new WaitForSeconds(replaceDelay);
        sideWallBottomToTop = !sideWallBottomToTop;
        yield return StartCoroutine(SpawnSideWallDomino());
        sideWallReplacing = false;
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

    private IEnumerator BlinkAndDestroyList(List<GameObject> list)
    {
        foreach (var b in list)
        {
            if (b == null) continue;
            if (!b.activeInHierarchy) { Destroy(b); continue; }
            WallHealth wh = b.GetComponent<WallHealth>();
            if (wh != null)
                wh.StartBlinkAndDestroy(blockBlinkCount, blockBlinkInterval);
            else
                Destroy(b);
        }
        yield return new WaitForSeconds(blockBlinkCount * blockBlinkInterval * 2f);
        list.Clear();
    }

    private static void DestroyBlockList(List<GameObject> list)
    {
        foreach (var b in list)
            if (b != null) Destroy(b);
        list.Clear();
    }

    private void PlaySpawnSE(AudioClip clip, AudioSource source, float volume, Vector3 pos)
    {
        if (clip == null) return;
        float finalVolume = volume * (SoundSettingsManager.Instance != null
            ? SoundSettingsManager.Instance.SEVolume : 1f);
        if (source != null)
            source.PlayOneShot(clip, finalVolume);
        else
            AudioSource.PlayClipAtPoint(clip, pos, finalVolume);
    }
}
