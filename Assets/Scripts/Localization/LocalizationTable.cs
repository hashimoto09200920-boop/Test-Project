using System.Collections.Generic;
using UnityEngine;

namespace Game.Localization
{
    /// <summary>
    /// キーごとに日本語/英語の文言を持つ翻訳テーブル。Assets/Resources/GameData/LocalizationTable.asset
    /// として1つだけ配置し、LocalizationManager.Get(key)から参照される。
    /// 新しい文言を追加するときは、このアセットのInspectorでEntriesに1行追加するだけでよい。
    /// </summary>
    [CreateAssetMenu(fileName = "LocalizationTable", menuName = "Game/Localization Table")]
    public class LocalizationTable : ScriptableObject
    {
        [System.Serializable]
        public class Entry
        {
            [Tooltip("コード側からLoc.Get(\"key\")のように参照する識別子。他の文言と重複しないこと")]
            public string key;
            [TextArea(1, 3)] public string japanese;
            [TextArea(1, 3)] public string english;
        }

        public List<Entry> entries = new List<Entry>();

        private Dictionary<string, Entry> _lookup;

        private static LocalizationTable _instance;
        public static LocalizationTable Instance
        {
            get
            {
                if (_instance == null)
                    _instance = Resources.Load<LocalizationTable>("GameData/LocalizationTable");
                return _instance;
            }
        }

        public string Get(string key, GameLanguage lang)
        {
            return Get(key, lang, key);
        }

        public string Get(string key, GameLanguage lang, string fallback)
        {
            if (_lookup == null) BuildLookup();
            if (_lookup.TryGetValue(key, out var e))
            {
                string value = lang == GameLanguage.English ? e.english : e.japanese;
                return string.IsNullOrEmpty(value) ? fallback : value;
            }
            return fallback;
        }

        private void BuildLookup()
        {
            _lookup = new Dictionary<string, Entry>();
            foreach (var e in entries)
            {
                if (!string.IsNullOrEmpty(e.key) && !_lookup.ContainsKey(e.key))
                    _lookup[e.key] = e;
            }
        }

        private void OnValidate()
        {
            // ★Inspectorで編集した内容を次回Get()呼び出し時に反映させるため、キャッシュを破棄する
            _lookup = null;
        }
    }
}
