using UnityEngine;

namespace Game.Localization
{
    public enum GameLanguage
    {
        Japanese = 0,
        English = 1,
    }

    /// <summary>
    /// ゲーム全体の言語設定を管理するシングルトン。SoundSettingsManagerと同じ設計
    /// （PlayerPrefsに保存・DontDestroyOnLoadで全シーンを跨いで常駐）。
    /// タイトル画面のLanguagePanelから切り替え、以降このInstance経由で全UIが文言を取得する想定。
    /// </summary>
    public class LocalizationManager : MonoBehaviour
    {
        private const string PREF_KEY = "GameLanguage";

        public static LocalizationManager Instance { get; private set; }

        /// <summary>
        /// Play中でInstanceが存在しない場合（Editor拡張のプレビュー機能等）に使う言語上書き。
        /// Instanceが存在する間はこちらは無視され、常にInstance.CurrentLanguageが優先される。
        /// </summary>
        public static GameLanguage? EditorPreviewLanguage = null;

        /// <summary>
        /// 現在参照すべき言語。Instanceが生きていればそちらを、いなければEditorPreviewLanguageを、
        /// それも無ければ日本語を返す。Get系の静的メソッドはこれを使うため、Play前のEditor拡張からも
        /// 正しく英語プレビューできる。
        /// </summary>
        public static GameLanguage CurrentLanguageStatic =>
            Instance != null ? Instance.CurrentLanguage : (EditorPreviewLanguage ?? GameLanguage.Japanese);

        public GameLanguage CurrentLanguage { get; private set; } = GameLanguage.Japanese;

        /// <summary>言語が切り替わった時に発火。表示中のUIはこれを購読して文言を再取得すること。</summary>
        public event System.Action OnLanguageChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Load();
        }

        private void Load()
        {
            int saved = PlayerPrefs.GetInt(PREF_KEY, (int)GameLanguage.Japanese);
            CurrentLanguage = (GameLanguage)saved;
            Debug.Log($"[LocalizationManager] Loaded language: {CurrentLanguage}");
        }

        public void SetLanguage(GameLanguage lang)
        {
            if (CurrentLanguage == lang) return;
            CurrentLanguage = lang;
            PlayerPrefs.SetInt(PREF_KEY, (int)lang);
            PlayerPrefs.Save();
            Debug.Log($"[LocalizationManager] Language changed to: {lang}");
            OnLanguageChanged?.Invoke();
        }

        /// <summary>
        /// キーに対応する現在の言語の文字列を返す。LocalizationTableが未設定/キー未登録の場合はキー自体を返す
        /// （翻訳漏れが画面上ですぐ分かるようにするため、空文字にはしない）。
        /// </summary>
        public string Get(string key)
        {
            var table = LocalizationTable.Instance;
            return table != null ? table.Get(key, CurrentLanguage) : key;
        }

        /// <summary>
        /// キー未登録時に生のキーではなくfallbackを返すオーバーロード
        /// （元データ自体を渡せる呼び出し元で、翻訳漏れ時にキー文字列が画面に出るのを避けたい場合に使う）。
        /// </summary>
        public string Get(string key, string fallback)
        {
            var table = LocalizationTable.Instance;
            return table != null ? table.Get(key, CurrentLanguage, fallback) : fallback;
        }

        /// <summary>
        /// 静的版のGet。Instanceが存在しなくても(Play前のEditor拡張プレビュー等)、
        /// CurrentLanguageStatic(EditorPreviewLanguage)を使って翻訳を引ける。
        /// SkillDefinition/GemDefinitionのGetLocalizedXxx()はこちらを使う。
        /// </summary>
        public static string GetStatic(string key, string fallback)
        {
            var table = LocalizationTable.Instance;
            return table != null ? table.Get(key, CurrentLanguageStatic, fallback) : fallback;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
