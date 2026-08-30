using UnityEngine;

/// <summary>
/// 地球の地表テクスチャをhorizontalTileCount列×2行のグリッドで並べて斜め方向（左＋下）に
/// スクロールさせ、自転しているように見せる。SpriteMask（丸いシルエット）で円形に
/// 切り抜かれる前提。このスクリプト自体はマスクを意識しない。
/// FogScroll.csと同じ「タイルを並べてスクロール→ループ時に反対側へ回す」方式を、
/// 横方向・縦方向それぞれ独立に適用する（自分自身の初期位置基準でループする＝
/// カメラに追従しない画面固定の背景要素のため）。
///
/// 横方向はhorizontalTileCount枚（既定3枚）を並べる。2枚だと開始位置のランダム化や
/// 端末アスペクト比によってはタイル境界が画面端ギリギリになり、瞬間的に隙間が見える
/// ことがあったため、余裕を持たせるために3枚以上を既定にしている。
/// 縦方向は2行固定（verticalScrollSpeed=0なら縦ループ自体を行わず、実質横スクロールのみになる）。
///
/// ★groundPatternsを2枚以上設定すると、タイルが画面外で横方向にループする瞬間
/// （縦ループ時は変更しない＝同じ地形がそのまま流れ続ける）に次のパターンへ順番に差し替える。
/// 画面外での差し替えのため切り替わりが視認されず、クロスフェードのような
/// 「両方が同時に薄く見えて白っぽくなる」問題が原理的に発生しない。
/// 各パターンは①と同じ画素サイズ・縦横比で用意すること（サイズが違うとタイル間隔がずれる）。
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class EarthRotationScroll : MonoBehaviour
{
    [Tooltip("横方向に並べるタイル枚数。2枚だと開始位置のランダム化等でタイル境界が画面端に" +
             "近くなり一瞬隙間が見えることがあるため、余裕を持たせるなら3枚以上を推奨")]
    [Range(2, 5)]
    [SerializeField] private int horizontalTileCount = 3;

    [Tooltip("横方向の自転速度（ワールド単位/秒）")]
    [Range(0.01f, 2f)]
    [SerializeField] private float scrollSpeed = 0.15f;

    [Tooltip("縦方向のスクロール速度（ワールド単位/秒）。0なら縦方向には動かない（従来通りの横スクロールのみ）")]
    [Range(0f, 2f)]
    [SerializeField] private float verticalScrollSpeed = 0f;

    [Tooltip("タイルの継ぎ目を重ねて隠す量（ワールド単位、横方向）。小さい値(0.1前後)は継ぎ目の1px隙間/にじみ隠し用。" +
             "大きくする(2〜4程度)と、各パターン画像の端に確保している海の余白同士が重なって隠れるため、" +
             "パターン間で見える『海の広さ』を狭くできる（重ねすぎると陸地部分まで隠れるので注意）")]
    [Range(0f, 5f)]
    [SerializeField] private float seamOverlap = 0.06f;

    [Tooltip("タイルの継ぎ目を重ねて隠す量（ワールド単位、縦方向）")]
    [Range(0f, 5f)]
    [SerializeField] private float seamOverlapVertical = 0.06f;

    [Tooltip("2枚以上設定すると、タイルが画面外で横方向にループする瞬間に次のパターンへ順番に差し替える。" +
             "空/1枚以下ならSpriteRendererに最初から設定されているスプライトのみを使い続ける（従来通り）")]
    [SerializeField] private Sprite[] groundPatterns;

    private const int VerticalTileCount = 2;

    // グリッド。index = row*horizontalTileCount+col。tiles[0](row0,col0)が元のGameObject自身
    private SpriteRenderer[] tiles;
    private Transform[] tileTransforms;
    private float tileWidth;
    private float tileHeight;
    private float spacingX; // tileWidth - seamOverlap
    private float spacingY; // tileHeight - seamOverlapVertical
    private float originX;
    private float originY;
    private bool initialized;
    private int nextPatternIndex;

    private float TimeScale =>
        SlowMotionManager.Instance != null ? SlowMotionManager.Instance.TimeScale : 1f;

    private int Index(int row, int col) => row * horizontalTileCount + col;

    private void Awake()
    {
        int count = horizontalTileCount * VerticalTileCount;
        tiles = new SpriteRenderer[count];
        tileTransforms = new Transform[count];
        tiles[0] = GetComponent<SpriteRenderer>();
        tileTransforms[0] = transform;
    }

    private void Update()
    {
        // 全Start()完了後の初回Updateで初期化（BackgroundManager設定済みを保証）
        if (!initialized)
        {
            if (tiles[0].sprite == null) return; // スプライト未設定なら待機
            Initialize();
        }

        float moveX = scrollSpeed * Time.deltaTime * TimeScale;
        float moveY = verticalScrollSpeed * Time.deltaTime * TimeScale;
        for (int i = 0; i < tileTransforms.Length; i++)
            tileTransforms[i].position += new Vector3(-moveX, -moveY, 0f);

        // ★自分自身の初期位置(originX/originY)を基準にループする。カメラ全体ではなく
        //   このオブジェクトのローカルな範囲内だけで無限スクロールさせるため。
        for (int row = 0; row < VerticalTileCount; row++)
        {
            for (int col = 0; col < horizontalTileCount; col++)
            {
                int i = Index(row, col);
                if (tileTransforms[i].position.x <= originX - spacingX)
                {
                    float maxX = MaxXInRow(row, col);
                    Vector3 p = tileTransforms[i].position;
                    p.x = maxX + spacingX;
                    tileTransforms[i].position = p;
                    tiles[i].sprite = NextPattern();
                }
                if (verticalScrollSpeed > 0f && tileTransforms[i].position.y <= originY - spacingY)
                {
                    float maxY = MaxYInColumn(col, row);
                    Vector3 p = tileTransforms[i].position;
                    p.y = maxY + spacingY;
                    tileTransforms[i].position = p;
                }
            }
        }
    }

    private float MaxXInRow(int row, int excludeCol)
    {
        float max = float.NegativeInfinity;
        for (int col = 0; col < horizontalTileCount; col++)
        {
            if (col == excludeCol) continue;
            float x = tileTransforms[Index(row, col)].position.x;
            if (x > max) max = x;
        }
        return max;
    }

    private float MaxYInColumn(int col, int excludeRow)
    {
        float max = float.NegativeInfinity;
        for (int row = 0; row < VerticalTileCount; row++)
        {
            if (row == excludeRow) continue;
            float y = tileTransforms[Index(row, col)].position.y;
            if (y > max) max = y;
        }
        return max;
    }

    // groundPatternsが2枚以上あれば順番に次のパターンを返す。未設定/1枚以下なら現在のスプライトのまま
    private Sprite NextPattern()
    {
        if (groundPatterns == null || groundPatterns.Length < 2) return tiles[0].sprite;
        Sprite next = groundPatterns[nextPatternIndex % groundPatterns.Length];
        nextPatternIndex++;
        return next;
    }

    private void Initialize()
    {
        SpriteRenderer sr = tiles[0];
        tileWidth = sr.sprite.bounds.size.x * transform.lossyScale.x;
        tileHeight = sr.sprite.bounds.size.y * transform.lossyScale.y;
        spacingX = Mathf.Max(0.01f, tileWidth - seamOverlap);
        spacingY = Mathf.Max(0.01f, tileHeight - seamOverlapVertical);

        originX = transform.position.x;
        originY = transform.position.y;

        for (int row = 0; row < VerticalTileCount; row++)
        {
            for (int col = 0; col < horizontalTileCount; col++)
            {
                int i = Index(row, col);
                if (i == 0) continue; // row0,col0(自分自身)は生成済み

                GameObject copy = new GameObject($"{gameObject.name}_Copy{row}_{col}");
                copy.transform.SetParent(transform.parent);
                copy.transform.position = new Vector3(
                    originX + col * spacingX,
                    originY + row * spacingY,
                    transform.position.z);
                copy.transform.localScale = transform.localScale;
                copy.transform.rotation = transform.rotation;

                SpriteRenderer copySR = copy.AddComponent<SpriteRenderer>();
                copySR.material = sr.material;
                copySR.sortingLayerID = sr.sortingLayerID;
                copySR.sortingOrder = sr.sortingOrder;
                copySR.maskInteraction = sr.maskInteraction;

                tiles[i] = copySR;
                tileTransforms[i] = copy.transform;
            }
        }

        if (groundPatterns != null && groundPatterns.Length >= 2)
        {
            for (int i = 0; i < tiles.Length; i++)
                tiles[i].sprite = groundPatterns[i % groundPatterns.Length];
            nextPatternIndex = tiles.Length % groundPatterns.Length;
        }
        else
        {
            for (int i = 1; i < tiles.Length; i++)
                tiles[i].sprite = sr.sprite;
        }

        initialized = true;
    }

    private void OnDestroy()
    {
        if (tileTransforms == null) return;
        for (int i = 1; i < tileTransforms.Length; i++)
            if (tileTransforms[i] != null) Destroy(tileTransforms[i].gameObject);
    }
}
