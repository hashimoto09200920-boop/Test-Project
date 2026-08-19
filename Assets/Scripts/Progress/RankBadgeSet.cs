using UnityEngine;

namespace Game.Progress
{
    /// <summary>
    /// ランク（S/A/B/C/D/E）ごとのバッジ画像をまとめて保持する共有アセット。
    /// 03_AreaSelectの結晶ノード表示とResult画面の両方から、同じアセットを参照して使い回す。
    /// </summary>
    [CreateAssetMenu(fileName = "RankBadgeSet", menuName = "Game/Rank Badge Set")]
    public class RankBadgeSet : ScriptableObject
    {
        [SerializeField] private Sprite rankS;
        [SerializeField] private Sprite rankA;
        [SerializeField] private Sprite rankB;
        [SerializeField] private Sprite rankC;
        [SerializeField] private Sprite rankD;
        [SerializeField] private Sprite rankE;

        /// <summary>ランク文字("S"/"A"/...)に対応するバッジSpriteを返す。該当なしはnull</summary>
        public Sprite GetSprite(string rank) => rank switch
        {
            "S" => rankS,
            "A" => rankA,
            "B" => rankB,
            "C" => rankC,
            "D" => rankD,
            "E" => rankE,
            _ => null,
        };
    }
}
