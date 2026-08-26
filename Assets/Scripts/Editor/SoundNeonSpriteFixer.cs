using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Assets/Art/SOUND/やAssets/Art/HELP/、Assets/Art/AreaSelect/直下のネオン/発光文字画像が、
    /// なぜかSprite Mode=Multiple（複数）でインポートされ、Unityが自動でパーツ分割してしまう
    /// 不具合を修正する。この状態だとAssetDatabase.LoadAssetAtPath&lt;Sprite&gt;()が分割後の断片
    /// (例：「S」の部分だけ)を返してしまい、画像が欠けて見える。Single（単一）に直す。
    /// ★AreaSelect直下は「①②③…」の丸数字で始まるファイル名（このシリーズの命名規則）だけを対象にし、
    /// 　Shopサブフォルダや無関係な既存アセット(Rank_*.png等)には触れない。
    /// </summary>
    public static class SoundNeonSpriteFixer
    {
        private static readonly string[] TargetPaths =
        {
            "Assets/Art/SOUND/① SOUND（パネルタイトル）.png",
            "Assets/Art/SOUND/② BGM（サウンドパネル内ラベル）.png",
            "Assets/Art/SOUND/③ SE（サウンドパネル内ラベル）.png",
            "Assets/Art/HELP/HELP.png",
        };

        private const string AreaSelectFolder = "Assets/Art/AreaSelect";
        private static readonly char[] CircledDigitPrefixes = "①②③④⑤⑥⑦⑧⑨⑩⑪⑫".ToCharArray();

        [MenuItem("Tools/Fix Neon Title Sprites (Multiple→Single)")]
        public static void FixSpriteMode()
        {
            int fixedCount = 0;
            foreach (var path in TargetPaths.Concat(FindAreaSelectNeonPngs()))
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    Debug.LogError($"[SoundNeonSpriteFixer] TextureImporterが見つかりません: {path}");
                    continue;
                }

                if (importer.spriteImportMode == SpriteImportMode.Single)
                {
                    Debug.Log($"[SoundNeonSpriteFixer] 既にSingleです: {path}");
                    continue;
                }

                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritesheet = new SpriteMetaData[0];
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
                fixedCount++;
                Debug.Log($"[SoundNeonSpriteFixer] Sprite ModeをSingleに修正しました: {path}");
            }

            Debug.Log($"[SoundNeonSpriteFixer] 完了。{fixedCount}件修正しました。");
        }

        private static System.Collections.Generic.IEnumerable<string> FindAreaSelectNeonPngs()
        {
            if (!Directory.Exists(AreaSelectFolder)) yield break;

            foreach (var fullPath in Directory.GetFiles(AreaSelectFolder, "*.png", SearchOption.TopDirectoryOnly))
            {
                string fileName = Path.GetFileName(fullPath);
                if (fileName.Length == 0 || System.Array.IndexOf(CircledDigitPrefixes, fileName[0]) < 0) continue;

                string assetPath = fullPath.Replace('\\', '/');
                yield return assetPath;
            }
        }
    }
}
