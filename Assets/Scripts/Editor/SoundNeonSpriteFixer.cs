using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Assets/Art/SOUND/やAssets/Art/HELP/、Assets/Art/AreaSelect/、Assets/Art/Background/直下の
    /// ネオン/発光文字画像・背景画像が、なぜかSprite Mode=Multiple（複数）でインポートされ、
    /// Unityが自動でパーツ分割してしまう不具合を修正する。この状態だと
    /// AssetDatabase.LoadAssetAtPath&lt;Sprite&gt;()が分割後の断片(例：「S」の部分だけ)を返してしまい、
    /// 画像が欠けて見える。Single（単一）に直す。
    /// ★Mesh Type（Tight/Full Rect）はSOUND/HELP/AreaSelectの既存画像では触らない。
    /// 　AreaConstellationFX等で既に個別調整済みの位置・スケールがズレてしまう事故が過去にあったため
    /// 　（Backgroundフォルダ用に追加した変更が誤って巻き込みで適用されてしまった）。
    /// 　Mesh Type=Full Rect化はBackgroundフォルダの①〜⑤（Area09 Cosmos用の新規画像）だけに限定する。
    /// ★対象フォルダ直下は「①②③…」の丸数字で始まるファイル名（このシリーズの命名規則）だけを対象にし、
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

        private const string BackgroundFolder = "Assets/Art/Background";
        private static readonly string[] SpriteModeOnlyScanFolders =
        {
            "Assets/Art/AreaSelect",
        };
        private static readonly char[] CircledDigitPrefixes = "①②③④⑤⑥⑦⑧⑨⑩⑪⑫".ToCharArray();

        [DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.delayCall += FixSpriteMode;
        }

        [MenuItem("Tools/Fix Neon Title Sprites (Multiple→Single)")]
        public static void FixSpriteMode()
        {
            int fixedCount = 0;

            // SOUND/HELP/AreaSelect：Sprite Modeのみ修正（Mesh Typeは既存の調整値を尊重して触らない）
            foreach (var path in TargetPaths.Concat(FindCircledDigitPngs(SpriteModeOnlyScanFolders)))
            {
                if (FixOne(path, fixMeshTypeToFullRect: false)) fixedCount++;
            }

            // Background：Area09 Cosmos用に新規作成した①〜⑤のみ。Sprite Mode + Mesh Type両方修正
            foreach (var path in FindCircledDigitPngs(new[] { BackgroundFolder }))
            {
                if (FixOne(path, fixMeshTypeToFullRect: true)) fixedCount++;
            }

            Debug.Log($"[SoundNeonSpriteFixer] 完了。{fixedCount}件修正しました。");
        }

        private static bool FixOne(string path, bool fixMeshTypeToFullRect)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[SoundNeonSpriteFixer] TextureImporterが見つかりません: {path}");
                return false;
            }

            bool modeOk = importer.spriteImportMode == SpriteImportMode.Single;
            bool meshOk = !fixMeshTypeToFullRect || GetMeshType(importer) == SpriteMeshType.FullRect;
            if (modeOk && meshOk)
            {
                Debug.Log($"[SoundNeonSpriteFixer] 既に修正済みです: {path}");
                return false;
            }

            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritesheet = new SpriteMetaData[0];
            if (fixMeshTypeToFullRect) SetMeshType(importer, SpriteMeshType.FullRect);
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
            Debug.Log($"[SoundNeonSpriteFixer] 修正しました（Mesh Type変更:{fixMeshTypeToFullRect}）: {path}");
            return true;
        }

        private static SpriteMeshType GetMeshType(TextureImporter importer)
        {
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            return settings.spriteMeshType;
        }

        private static void SetMeshType(TextureImporter importer, SpriteMeshType meshType)
        {
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = meshType;
            importer.SetTextureSettings(settings);
        }

        private static System.Collections.Generic.IEnumerable<string> FindCircledDigitPngs(string[] folders)
        {
            foreach (var folder in folders)
            {
                if (!Directory.Exists(folder)) continue;

                foreach (var fullPath in Directory.GetFiles(folder, "*.png", SearchOption.TopDirectoryOnly))
                {
                    string fileName = Path.GetFileName(fullPath);
                    if (fileName.Length == 0 || System.Array.IndexOf(CircledDigitPrefixes, fileName[0]) < 0) continue;

                    yield return fullPath.Replace('\\', '/');
                }
            }
        }
    }
}
