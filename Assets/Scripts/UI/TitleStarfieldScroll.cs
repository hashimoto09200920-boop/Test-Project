using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// タイトル画面の最奥レイヤー(星空)を、画面いっぱいのImageを複数枚横に並べてゆっくり無限スクロールさせる。
    /// AreaSelectの DriftScroll(SpriteRenderer専用、ワールド空間のタイル背景用)と同じ「タイルを並べてスワップ」の
    /// 考え方を、UI Canvas上のImage用に作り直したもの。
    /// アタッチされたGameObject自身が1枚目のレイヤーとして使われ、Awake/Update開始時に必要な枚数のコピーを生成する。
    /// タイル枚数は画面幅に応じて動的に計算する(2枚固定だと、画面幅とタイル幅の関係次第で
    /// どのタイルにもカバーされない隙間が周期的に生じるため)。
    /// 継ぎ目なくタイリングできる画像(左右端が自然に繋がる画像)専用。
    /// </summary>
    [RequireComponent(typeof(Image))]
    [RequireComponent(typeof(RectTransform))]
    public class TitleStarfieldScroll : MonoBehaviour
    {
        [Tooltip("スクロール速度(px/秒)。「ほぼ静止、極めてゆっくり流れる」想定のためかなり小さい値にする")]
        [SerializeField] private float scrollSpeed = 4f;

        private RectTransform[] tiles;
        private float tileWidth;
        private float canvasLeft;
        private float canvasRight;
        private bool initialized;

        private void Update()
        {
            if (!initialized)
            {
                if (GetComponent<Image>().sprite == null) return;
                Initialize();
            }

            float move = scrollSpeed * Time.unscaledDeltaTime;

            // ★全タイルを左に動かした上で、右端が画面左端を割ったタイルだけを、
            //   現在最も右にあるタイルのさらに右隣へ付け直す。何枚あっても正しく無限ループする。
            float rightmostX = float.NegativeInfinity;
            foreach (var t in tiles)
            {
                t.anchoredPosition += Vector2.left * move;
                if (t.anchoredPosition.x > rightmostX) rightmostX = t.anchoredPosition.x;
            }

            foreach (var t in tiles)
            {
                if (t.anchoredPosition.x + tileWidth * 0.5f < canvasLeft)
                {
                    rightmostX += tileWidth;
                    t.anchoredPosition = new Vector2(rightmostX, t.anchoredPosition.y);
                }
            }
        }

        private void Initialize()
        {
            var img = GetComponent<Image>();
            var rt = (RectTransform)transform;

            // ★画面のアスペクト比がReferenceResolution(16:9)と異なると、固定サイズのままでは
            //   画面の左右端に隙間ができてしまう。Canvasの実際の論理サイズに合わせて、
            //   画像のアスペクト比を保ったまま幅を計算し直す(高さは常に画面いっぱいに埋める)。
            float canvasWidth = 1920f;
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null && img.sprite != null)
            {
                var canvasRect = canvas.GetComponent<RectTransform>().rect;
                canvasWidth = canvasRect.width;
                float spriteAspect = img.sprite.rect.width / img.sprite.rect.height;
                rt.sizeDelta = new Vector2(canvasRect.height * spriteAspect, canvasRect.height);
            }

            tileWidth = rt.rect.width;
            canvasLeft = -canvasWidth * 0.5f;
            canvasRight = canvasWidth * 0.5f;

            // ★画面全体をカバーするのに必要な最小枚数+予備1枚(スクロール中の継ぎ目切り替え用)。
            //   最低3枚は確保する(2枚だと画面幅とタイル幅の関係次第で隙間が生じるケースがあるため)。
            int tileCount = Mathf.Max(3, Mathf.CeilToInt(canvasWidth / tileWidth) + 1);

            tiles = new RectTransform[tileCount];
            tiles[0] = rt;

            for (int i = 1; i < tileCount; i++)
            {
                GameObject copyObj = new GameObject(gameObject.name + "_Copy" + i, typeof(RectTransform));
                copyObj.transform.SetParent(transform.parent, false);
                copyObj.transform.SetSiblingIndex(transform.GetSiblingIndex());

                var copyRt = (RectTransform)copyObj.transform;
                copyRt.anchorMin = rt.anchorMin;
                copyRt.anchorMax = rt.anchorMax;
                copyRt.pivot = rt.pivot;
                copyRt.sizeDelta = rt.sizeDelta;

                var copyImg = copyObj.AddComponent<Image>();
                copyImg.sprite = img.sprite;
                copyImg.color = img.color;
                copyImg.raycastTarget = false;
                copyImg.type = img.type;
                copyImg.preserveAspect = img.preserveAspect;

                tiles[i] = copyRt;
            }

            // ★左端のタイルの左端を画面左端にちょうど合わせ、以降のタイルを隙間なく右に並べる。
            for (int i = 0; i < tileCount; i++)
            {
                tiles[i].anchoredPosition = new Vector2(canvasLeft + tileWidth * (i + 0.5f), tiles[i].anchoredPosition.y);
            }

            initialized = true;
        }

        private void OnDestroy()
        {
            if (tiles == null) return;
            for (int i = 1; i < tiles.Length; i++)
            {
                if (tiles[i] != null) Destroy(tiles[i].gameObject);
            }
        }
    }
}
