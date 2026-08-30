using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// SoundNeonSpriteFixer.FixSpriteMode()の修正ミスの復旧専用。
    /// Assets/Art/AreaSelect直下の①〜⑪画像は元々spriteMode=Single（正常）だったにも関わらず、
    /// Background用に追加したspriteMeshType=FullRectへの変更が誤って巻き込みで適用されてしまった。
    /// これをTight（元の値）へ戻す。Backgroundフォルダの①〜⑤（Full Rectが正しい）には触れない。
    /// </summary>
    public static class AreaSelectMeshTypeRevert
    {
        private const string AreaSelectFolder = "Assets/Art/AreaSelect";
        private static readonly char[] CircledDigitPrefixes = "①②③④⑤⑥⑦⑧⑨⑩⑪⑫".ToCharArray();

        [DidReloadScripts]
        private static void OnScriptsReloaded()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.delayCall += Run;
        }

        [MenuItem("Tools/Revert AreaSelect Mesh Type To Tight (Fix Mistake)")]
        public static void Run()
        {
            if (!Directory.Exists(AreaSelectFolder))
            {
                Debug.LogWarning("[AreaSelectMeshTypeRevert] AreaSelectフォルダが見つかりません。");
                return;
            }

            int fixedCount = 0;
            foreach (var fullPath in Directory.GetFiles(AreaSelectFolder, "*.png", SearchOption.TopDirectoryOnly))
            {
                string fileName = Path.GetFileName(fullPath);
                if (fileName.Length == 0 || System.Array.IndexOf(CircledDigitPrefixes, fileName[0]) < 0) continue;

                string assetPath = fullPath.Replace('\\', '/');
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null)
                {
                    Debug.LogError($"[AreaSelectMeshTypeRevert] TextureImporterが見つかりません: {assetPath}");
                    continue;
                }

                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                if (settings.spriteMeshType == SpriteMeshType.Tight)
                {
                    Debug.Log($"[AreaSelectMeshTypeRevert] 既にTightです: {assetPath}");
                    continue;
                }

                settings.spriteMeshType = SpriteMeshType.Tight;
                importer.SetTextureSettings(settings);
                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
                fixedCount++;
                Debug.Log($"[AreaSelectMeshTypeRevert] Mesh TypeをTightに戻しました: {assetPath}");
            }

            Debug.Log($"[AreaSelectMeshTypeRevert] 完了。{fixedCount}件復旧しました。");
        }
    }
}
