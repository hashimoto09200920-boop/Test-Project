using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
using System.IO;
using System.Linq;
#endif

namespace Game.UI
{
    /// <summary>ボタンとハンドラの紐付け方法</summary>
    public enum BindListenersMode
    {
        /// <summary>各ボタンの GameObject 名（Start/Reset/Quit）でハンドラを決める</summary>
        ByName,
        /// <summary>Inspector の Start/Reset/Quit スロット順でハンドラを決める</summary>
        BySlot,
        /// <summary>buttonsGroup の子の並び順（上＝Start, 中央＝Reset, 下＝Quit）。画像に変えても安定。</summary>
        ByOrder
    }

    /// <summary>
    /// 01_Title の Start / Reset / Quit を司る最小メニュー。
    /// Inspector でボタンと遷移先シーン名を割当。
    /// </summary>
    [DisallowMultipleComponent]
    public class TitleMenu : MonoBehaviour
    {
        [Header("Buttons (必須)")]
        public Button startButton;
        public Button resetButton;
        public Button quitButton;
        [Tooltip("言語選択ボタン（Add Language Buttonで自動生成可能）。押すとSEが鳴るだけで、遷移や画面表示は行わない（機能未実装のプレースホルダー）")]
        public Button languageButton;

        [Header("Neon Button Style (共通枠+個別文字画像)")]
        [Tooltip("Start/Settings/Language/Quit共通のネオン枠画像（白ネオン管、内部塗りつぶしあり）")]
        public Sprite neonFrameSprite;
        [Tooltip("各ボタンの文字だけのネオン管画像（枠なし）。Assign Button Text Spritesで自動アサイン可能")]
        public Sprite startTextSprite;
        public Sprite settingsTextSprite;
        public Sprite languageTextSprite;
        public Sprite quitTextSprite;

        [Header("Options")]
        /// <summary>ByName=GameObject名で紐付け / BySlot=Inspectorスロット順 / ByOrder=ButtonsGroupの子の並び順（上→Start, 中央→Reset, 下→Quit）</summary>
        public BindListenersMode bindListenersMode = BindListenersMode.ByOrder;

        [Tooltip("BindListenersMode.ByOrder のとき使用。この Transform の子 0,1,2 の Button に順に Start / Reset / Quit を割り当てます。")]
        public Transform buttonsGroup;

        [Header("Scene Names")]
        public string areaSelectSceneName = "03_AreaSelect";

        [Header("Sound Effects")]
        [Tooltip("ボタンクリック時の効果音")]
        public AudioClip buttonClickSE;

        [Header("Sound Settings Panel (Setup Sound Panelで自動生成可能)")]
        [Tooltip("サウンド設定パネル本体")]
        public GameObject soundPanel;
        public Slider bgmVolumeSlider;
        public Slider seVolumeSlider;
        public TextMeshProUGUI bgmVolumeText;
        public TextMeshProUGUI seVolumeText;
        [Tooltip("サウンド設定パネルを閉じてタイトルへ戻るボタン")]
        public Button soundBackButton;

        [Header("Debug (Setup Debug Reset Buttonで自動生成可能)")]
        [Tooltip("デバッグ用：ゲーム進行度を初期化する06_Resetシーンへ遷移するボタン。" +
            "06_Reset側でYes/No確認とProgressManager.ResetAll()実行・Titleへの復帰まで完結する")]
        public Button debugResetProgressButton;

        private AudioSource audioSource;
        private bool isTransitioning = false;
        private SoundSettingsManager soundSettingsManager;

        private void Awake()
        {
            // AudioSourceを取得または作成
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }

            // ボタンにリスナーを登録
            switch (bindListenersMode)
            {
                case BindListenersMode.ByName:
                    BindListenersByName();
                    break;
                case BindListenersMode.BySlot:
                    BindListenersBySlot();
                    break;
                case BindListenersMode.ByOrder:
                    BindListenersByOrder();
                    break;
            }

            // サウンド設定パネルのスライダー・戻るボタンにリスナーを登録
            if (bgmVolumeSlider != null) bgmVolumeSlider.onValueChanged.AddListener(OnBGMVolumeChanged);
            if (seVolumeSlider != null) seVolumeSlider.onValueChanged.AddListener(OnSEVolumeChanged);
            if (soundBackButton != null) soundBackButton.onClick.AddListener(HideSoundPanel);
            if (debugResetProgressButton != null) debugResetProgressButton.onClick.AddListener(OnClickDebugResetProgress);

            // 初期は非表示
            if (soundPanel != null) soundPanel.SetActive(false);
        }

        private void Start()
        {
            soundSettingsManager = SoundSettingsManager.Instance;

            // シーン開始時にフェードイン
            StartCoroutine(FadeInOnStart());
        }

        /// <summary>
        /// ButtonsGroup の子の並び順でハンドラを登録する（上から Start, Settings, Language, Quit）。
        /// 画像に変えたり GameObject 名がずれていても、表示順だけで正しく動く。
        /// </summary>
        private void BindListenersByOrder()
        {
            if (buttonsGroup == null)
            {
                Debug.LogWarning("[TitleMenu] BindListenersMode.ByOrder ですが buttonsGroup が未設定です。ByName で代用します。");
                BindListenersByName();
                return;
            }
            int count = buttonsGroup.childCount;
            if (count < 3)
            {
                Debug.LogWarning($"[TitleMenu] buttonsGroup の子が 3 未満です（現在 {count}）。");
            }
            for (int i = 0; i < Mathf.Min(4, count); i++)
            {
                var child = buttonsGroup.GetChild(i);
                var button = child.GetComponent<Button>();
                if (button == null)
                {
                    Debug.LogWarning($"[TitleMenu] buttonsGroup の子 {i} '{child.name}' に Button がありません。");
                    continue;
                }
                button.onClick.RemoveAllListeners();
                if (i == 0)
                {
                    button.onClick.AddListener(OnClickStart);
                    Debug.Log($"[Awake] 表示 1 番目 '{child.name}' → OnClickStart（並び順で紐付け）");
                }
                else if (i == 1)
                {
                    button.onClick.AddListener(OnClickSettings);
                    Debug.Log($"[Awake] 表示 2 番目 '{child.name}' → OnClickSettings（並び順で紐付け）");
                }
                else if (i == 2 && count >= 4)
                {
                    // ★4番目(Quit)が存在する時だけ3番目をLanguageとして扱う。
                    //   Languageボタン未追加(3個のまま)の環境では、従来通り3番目はQuitのまま動く。
                    button.onClick.AddListener(OnClickLanguage);
                    Debug.Log($"[Awake] 表示 3 番目 '{child.name}' → OnClickLanguage（並び順で紐付け）");
                }
                else
                {
                    button.onClick.AddListener(OnClickQuit);
                    Debug.Log($"[Awake] 表示 {i + 1} 番目 '{child.name}' → OnClickQuit（並び順で紐付け）");
                }
            }
        }

        /// <summary>
        /// 各ボタンの GameObject 名に応じてハンドラを登録する。
        /// Inspector でどのスロットにどのボタンを入れても、名前で正しく動作する。
        /// </summary>
        private void BindListenersByName()
        {
            var buttons = new[] { startButton, resetButton, quitButton, languageButton };
            foreach (var button in buttons)
            {
                if (button == null) continue;
                button.onClick.RemoveAllListeners();
                var name = button.gameObject.name;
                if (name.Contains("Start"))
                {
                    button.onClick.AddListener(OnClickStart);
                    Debug.Log($"[Awake] '{name}' → OnClickStart を登録（名前で紐付け）");
                }
                else if (name.Contains("Settings") || name.Contains("Reset"))
                {
                    button.onClick.AddListener(OnClickSettings);
                    Debug.Log($"[Awake] '{name}' → OnClickSettings を登録（名前で紐付け）");
                }
                else if (name.Contains("Language"))
                {
                    button.onClick.AddListener(OnClickLanguage);
                    Debug.Log($"[Awake] '{name}' → OnClickLanguage を登録（名前で紐付け）");
                }
                else if (name.Contains("Quit"))
                {
                    button.onClick.AddListener(OnClickQuit);
                    Debug.Log($"[Awake] '{name}' → OnClickQuit を登録（名前で紐付け）");
                }
                else
                {
                    Debug.LogWarning($"[TitleMenu] ボタン '{name}' は Start/Settings/Language/Quit のいずれにも一致しません。");
                }
            }
        }

        /// <summary>
        /// Inspector のスロット順でハンドラを登録する（従来どおり）。
        /// </summary>
        private void BindListenersBySlot()
        {
            if (startButton != null)
            {
                startButton.onClick.RemoveAllListeners();
                startButton.onClick.AddListener(OnClickStart);
                Debug.Log($"[Awake] startButton スロット '{startButton.gameObject.name}' → OnClickStart");
            }
            if (resetButton != null)
            {
                resetButton.onClick.RemoveAllListeners();
                resetButton.onClick.AddListener(OnClickSettings);
                Debug.Log($"[Awake] resetButton スロット '{resetButton.gameObject.name}' → OnClickSettings");
            }
            if (quitButton != null)
            {
                quitButton.onClick.RemoveAllListeners();
                quitButton.onClick.AddListener(OnClickQuit);
                Debug.Log($"[Awake] quitButton スロット '{quitButton.gameObject.name}' → OnClickQuit");
            }
            if (languageButton != null)
            {
                languageButton.onClick.RemoveAllListeners();
                languageButton.onClick.AddListener(OnClickLanguage);
                Debug.Log($"[Awake] languageButton スロット '{languageButton.gameObject.name}' → OnClickLanguage");
            }
        }

        private void OnClickStart()
        {
            // 既に遷移中なら何もしない（連打防止）
            if (isTransitioning) return;

            Debug.Log(">>> OnClickStart() が呼ばれました → 03_AreaSelect へ遷移");
            if (!Application.isPlaying) return;
            if (string.IsNullOrWhiteSpace(areaSelectSceneName))
            {
                Debug.LogWarning("[TitleMenu] areaSelectSceneName is empty.");
                return;
            }

            isTransitioning = true;
            StartCoroutine(FadeOutAndLoadScene(areaSelectSceneName));
        }

        private void OnClickSettings()
        {
            // 既に遷移中なら何もしない（連打防止）
            if (isTransitioning) return;

            Debug.Log(">>> OnClickSettings() が呼ばれました → サウンド設定パネルを表示");
            if (!Application.isPlaying) return;

            PlayButtonSE();
            ShowSoundPanel();
        }

        private void ShowSoundPanel()
        {
            if (soundPanel == null)
            {
                Debug.LogWarning("[TitleMenu] soundPanelが未設定です。先に「Setup Sound Panel」を実行してください。");
                return;
            }
            soundPanel.SetActive(true);

            // 現在の音量をスライダーに反映
            if (soundSettingsManager != null)
            {
                if (bgmVolumeSlider != null)
                {
                    bgmVolumeSlider.SetValueWithoutNotify(soundSettingsManager.BGMVolume);
                    UpdateBGMVolumeText(soundSettingsManager.BGMVolume);
                }
                if (seVolumeSlider != null)
                {
                    seVolumeSlider.SetValueWithoutNotify(soundSettingsManager.SEVolume);
                    UpdateSEVolumeText(soundSettingsManager.SEVolume);
                }
            }
        }

        private void HideSoundPanel()
        {
            PlayButtonSE();
            if (soundPanel != null) soundPanel.SetActive(false);
        }

        private void OnBGMVolumeChanged(float value)
        {
            if (soundSettingsManager != null) soundSettingsManager.BGMVolume = value;
            UpdateBGMVolumeText(value);
        }

        private void OnSEVolumeChanged(float value)
        {
            if (soundSettingsManager != null) soundSettingsManager.SEVolume = value;
            UpdateSEVolumeText(value);
        }

        private void UpdateBGMVolumeText(float value)
        {
            if (bgmVolumeText != null) bgmVolumeText.text = $": {Mathf.RoundToInt(value * 100)}%";
        }

        private void UpdateSEVolumeText(float value)
        {
            if (seVolumeText != null) seVolumeText.text = $": {Mathf.RoundToInt(value * 100)}%";
        }

        private void OnClickLanguage()
        {
            // 既に遷移中なら何もしない（連打防止）
            if (isTransitioning) return;

            // ★言語切り替え機能は未実装のプレースホルダー。SEを鳴らすだけで遷移・画面表示は一切行わない。
            Debug.Log(">>> OnClickLanguage() が呼ばれました → SEのみ再生（機能未実装）");
            if (!Application.isPlaying) return;

            PlayButtonSE();
        }

        private void OnClickQuit()
        {
            // 既に遷移中なら何もしない（連打防止）
            if (isTransitioning) return;

            Debug.Log(">>> OnClickQuit() が呼ばれました → Quit");
            if (!Application.isPlaying) return;

            isTransitioning = true;
            StartCoroutine(QuitWithDelay());
        }

        /// <summary>
        /// デバッグ用：確認ダイアログを挟まず、即座にゲーム進行度を初期化する。
        /// 06_Reset(ResetUI.OnClickYes)と同じ処理(ProgressManager.ResetAll()、無ければPlayerPrefs全削除)を
        /// その場で行うだけで、シーン遷移はしない。
        /// </summary>
        private void OnClickDebugResetProgress()
        {
            Debug.Log(">>> OnClickDebugResetProgress() が呼ばれました → 進行度を即座に初期化");
            if (!Application.isPlaying) return;

            var pm = Game.Progress.ProgressManager.Instance;
            if (pm != null)
            {
                pm.ResetAll();
                Debug.Log("[TitleMenu] ProgressManager.ResetAll() 実行完了。");
            }
            else
            {
                Debug.LogWarning("[TitleMenu] ProgressManager.Instance が見つかりません。PlayerPrefsを全削除します。");
                PlayerPrefs.DeleteAll();
                PlayerPrefs.Save();
            }

            PlayButtonSE();
        }

        /// <summary>
        /// ボタンクリック時の効果音を再生
        /// </summary>
        private void PlayButtonSE()
        {
            if (buttonClickSE == null || audioSource == null) return;

            // ★SoundSettingsManager経由で再生することでSE音量設定を反映する。
            //   取得前(Start前)など万一soundSettingsManagerが無い場合は素の音量で再生する。
            if (soundSettingsManager != null)
            {
                soundSettingsManager.PlaySE(audioSource, buttonClickSE);
            }
            else
            {
                audioSource.PlayOneShot(buttonClickSE);
            }
        }

        /// <summary>
        /// SEを再生してからアプリケーション終了
        /// </summary>
        private System.Collections.IEnumerator QuitWithDelay()
        {
            PlayButtonSE();
            yield return new WaitForSeconds(0.2f);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        /// <summary>
        /// フェードアウトしながらシーン遷移
        /// </summary>
        private System.Collections.IEnumerator FadeOutAndLoadScene(string sceneName)
        {
            PlayButtonSE();
            yield return new WaitForSeconds(0.2f);

            Debug.Log($"[TitleMenu] Fading out and loading scene: {sceneName}");

            // フェード用の黒い画像を作成
            GameObject fadeObj = new GameObject("FadeOut");
            Canvas fadeCanvas = fadeObj.AddComponent<Canvas>();
            fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            fadeCanvas.sortingOrder = 9999; // 最前面に表示

            UnityEngine.UI.CanvasScaler scaler = fadeObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            GameObject imageObj = new GameObject("FadeImage");
            imageObj.transform.SetParent(fadeObj.transform, false);

            UnityEngine.UI.Image fadeImage = imageObj.AddComponent<UnityEngine.UI.Image>();
            fadeImage.color = new Color(0, 0, 0, 0); // 黒、透明から開始

            RectTransform rectTransform = imageObj.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.sizeDelta = Vector2.zero;

            // フェードアウト処理（0.5秒）
            float duration = 0.5f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Clamp01(elapsed / duration);
                fadeImage.color = new Color(0, 0, 0, alpha);
                yield return null;
            }

            // 完全に黒くなったらシーン遷移
            // ★同期LoadSceneはシーン全体の読み込み・初期化が終わるまでメインスレッドを止めるため、
            // 画面が黒い状態でも「遷移した瞬間に一瞬ガクッと固まる」体感の直接の原因になっていた。
            // 非同期にして、読み込み中も他の処理（フェード等）が進められるようにする。
            var op = SceneManager.LoadSceneAsync(sceneName);
            while (op != null && !op.isDone)
            {
                yield return null;
            }
        }

        /// <summary>
        /// シーン開始時にフェードイン
        /// </summary>
        private System.Collections.IEnumerator FadeInOnStart()
        {
            Debug.Log("[TitleMenu] Starting fade in");

            // フェード用の黒い画像を作成
            GameObject fadeObj = new GameObject("FadeIn");
            Canvas fadeCanvas = fadeObj.AddComponent<Canvas>();
            fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            fadeCanvas.sortingOrder = 9999; // 最前面に表示

            UnityEngine.UI.CanvasScaler scaler = fadeObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            GameObject imageObj = new GameObject("FadeImage");
            imageObj.transform.SetParent(fadeObj.transform, false);

            UnityEngine.UI.Image fadeImage = imageObj.AddComponent<UnityEngine.UI.Image>();
            fadeImage.color = new Color(0, 0, 0, 1); // 黒、完全不透明から開始

            RectTransform rectTransform = imageObj.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.sizeDelta = Vector2.zero;

            // フェードイン処理（0.5秒）
            float duration = 0.5f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = 1f - Mathf.Clamp01(elapsed / duration); // 1から0へ
                fadeImage.color = new Color(0, 0, 0, alpha);
                yield return null;
            }

            // 完全に透明になったらフェードオブジェクトを削除
            Destroy(fadeObj);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Canvas直下に残っているデバッグ用のSkillListUI（"■カテゴリA/B/C (なし)"等を表示する、
        /// Title画面には無関係な要素）を非表示にする。GameObject自体は削除せず非アクティブ化するだけ（再実行しても安全）。
        /// </summary>
        [ContextMenu("0. Hide Debug SkillListUI (デバッグ用スキル一覧表示を非表示化)")]
        private void HideDebugSkillListUI()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[TitleMenu] Canvasが見つかりません。");
                return;
            }
            var skillListTf = canvas.transform.Find("SkillListUI");
            if (skillListTf == null)
            {
                Debug.LogWarning("[TitleMenu] SkillListUIが見つかりませんでした（既に削除済み、または名前が違う可能性があります）。");
                return;
            }
            skillListTf.gameObject.SetActive(false);
            EditorUtility.SetDirty(skillListTf.gameObject);
            Debug.Log("[TitleMenu] SkillListUIを非表示にしました。");
        }

        /// <summary>
        /// 共通ネオン枠画像(Assets/Art/Title/共通ネオン枠.png)をneonFrameSpriteに設定する。
        /// </summary>
        [ContextMenu("1. Assign Neon Frame Sprite (共通ネオン枠を設定)")]
        private void AssignNeonFrameSprite()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Title/共通ネオン枠.png");
            if (sprite == null)
            {
                Debug.LogError("[TitleMenu] 共通ネオン枠.pngが見つかりません。");
                return;
            }
            neonFrameSprite = sprite;
            EditorUtility.SetDirty(this);
            Debug.Log("[TitleMenu] neonFrameSpriteを設定しました。");
        }

        /// <summary>
        /// 各ボタンの文字だけのネオン管画像(Assets/Art/Title/START.png等)を対応するフィールドにアサインする。
        /// </summary>
        [ContextMenu("2. Assign Button Text Sprites (文字画像を設定)")]
        private void AssignButtonTextSprites()
        {
            startTextSprite = LoadTextSprite("START.png");
            settingsTextSprite = LoadTextSprite("SETTINGS.png");
            languageTextSprite = LoadTextSprite("LANGUAGE.png");
            quitTextSprite = LoadTextSprite("QUIT.png");
            EditorUtility.SetDirty(this);
            Debug.Log("[TitleMenu] 文字画像(START/SETTINGS/LANGUAGE/QUIT)を設定しました。");
        }

        private Sprite LoadTextSprite(string fileName)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/Title/{fileName}");
            if (sprite == null) Debug.LogError($"[TitleMenu] {fileName}が見つかりません。");
            return sprite;
        }

        /// <summary>
        /// Start/Settings/Quitの3ボタンを、共通ネオン枠+個別文字画像の二層構造に切り替える。
        /// 文字入りPNG(旧デザイン)は使わなくなる。再実行しても安全。
        /// </summary>
        [ContextMenu("3. Apply Neon Style To Start-Settings-Quit (共通枠+文字画像に切替)")]
        private void ApplyNeonStyleToExistingButtons()
        {
            if (neonFrameSprite == null)
            {
                Debug.LogWarning("[TitleMenu] neonFrameSpriteが未設定です。先に「1. Assign Neon Frame Sprite」を実行してください。");
                return;
            }
            if (startTextSprite == null || settingsTextSprite == null || quitTextSprite == null)
            {
                Debug.LogWarning("[TitleMenu] 文字画像が未設定です。先に「2. Assign Button Text Sprites」を実行してください。");
                return;
            }
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[TitleMenu] Canvasが見つかりません。");
                return;
            }

            // ★TitlePanel(親)の高さは600pxのまま(拡張しない)。ボタン自体を500x125に縮小し、
            //   縦幅を狭めた分だけ隙間(間隔158px、隙間33px)が自動的に広がる。
            ApplyNeonButtonVisual(FindDeep(canvas.transform, "StartButtonImage"), "StartButtonImage", startTextSprite, 237.5f, new Vector2(213.8f, 85.5f));
            ApplyNeonButtonVisual(FindDeep(canvas.transform, "SettingsButtonImage") ?? FindDeep(canvas.transform, "ResetButtonImage"), "SettingsButtonImage", settingsTextSprite, 79.2f, new Vector2(274.4f, 137.0f));
            ApplyNeonButtonVisual(FindDeep(canvas.transform, "QuitButtonImage"), "QuitButtonImage", quitTextSprite, -237.5f, new Vector2(165.4f, 89.4f));

            Debug.Log("[TitleMenu] Start/Settings/Quitを共通ネオン枠+文字画像に切り替え、ボタンサイズと間隔を調整しました。");
        }

        /// <summary>
        /// GameTitleロゴ("PIXEL DANCER"等)が画面上端からはみ出す問題を修正する。
        /// TitlePanel(anchoredPosition.y=-200, sizeDelta.y=600)の上端(=100)を基準に、
        /// GameTitleのsizeDelta(600x400)*localScale(2.66倍)で実際の描画上端を計算すると
        /// 画面上端(高さ1080想定なら540)を192.5pxオーバーしていたため、
        /// anchoredPosition.yを100→-120に変更し、画面内に27.5pxの余裕を持たせる。再実行しても安全。
        /// </summary>
        [ContextMenu("5. Fix GameTitle Position (タイトルロゴの画面上端はみ出しを修正)")]
        private void FixGameTitlePosition()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[TitleMenu] Canvasが見つかりません。");
                return;
            }
            // ★実際のGameObject名は末尾に半角スペースが付いている('GameTitle ')
            var titleTf = (FindDeep(canvas.transform, "GameTitle ") ?? FindDeep(canvas.transform, "GameTitle")) as RectTransform;
            if (titleTf == null)
            {
                Debug.LogWarning("[TitleMenu] GameTitleが見つかりませんでした。");
                return;
            }
            titleTf.anchoredPosition = new Vector2(titleTf.anchoredPosition.x, -120f);
            EditorUtility.SetDirty(titleTf);
            Debug.Log("[TitleMenu] GameTitleの位置を修正しました（画面上端のはみ出しを解消）。");
        }

        /// <summary>
        /// 旧"GameTitle"(PIXEL DANCERの仮ロゴ)を非表示にし、新しい"NEON DANCER"ロゴ用の
        /// コンテナ(TitleLogoLettersコンポーネント付き)をTitlePanel内に作成する。
        /// 実際の文字配置は、生成されたNeonDancerLogoのTitleLogoLettersコンポーネント側の
        /// 「1. Assign Letter Sprites」→「2. Apply Default Letter Style」→「Build Logo Letters」で行う。
        /// ★Y座標(15)は、ロゴとStart/Settings/Language/Quitボタンが重ならないよう手計算で決めた値。
        ///   TitlePanel座標系(anchorMin/Max=(0.5,1)、TitlePanel上端を基準=600とした相対値)で:
        ///   ・画面に実際に表示される範囲は Y=-340〜740 (CanvasScaler ReferenceResolution 1920x1080, Height基準)
        ///   ・ロゴ(wobble込み)の占有範囲は 概ね コンテナ中心+102 〜 コンテナ中心-274
        ///   ・Startボタンの占有範囲は Y=175〜300
        ///   コンテナ中心を600+15=615にすると、ロゴ上端≈717(画面上端740まで23pxの余裕)、
        ///   ロゴ下端≈341(Startボタン上端300との間に41pxの余白)になり、重ならない。
        /// 再実行しても安全(既存のNeonDancerLogoがあれば位置だけ更新する)。
        /// </summary>
        [ContextMenu("7. Setup Neon Dancer Logo (旧GameTitleを新ロゴ用コンテナに置き換え)")]
        private void SetupNeonDancerLogo()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[TitleMenu] Canvasが見つかりません。");
                return;
            }

            var oldTitleTf = FindDeep(canvas.transform, "GameTitle ") ?? FindDeep(canvas.transform, "GameTitle");
            if (oldTitleTf != null)
            {
                oldTitleTf.gameObject.SetActive(false);
                EditorUtility.SetDirty(oldTitleTf.gameObject);
            }

            var titlePanelTf = FindDeep(canvas.transform, "TitlePanel");
            if (titlePanelTf == null)
            {
                Debug.LogError("[TitleMenu] TitlePanelが見つかりません。");
                return;
            }

            var existing = titlePanelTf.Find("NeonDancerLogo");
            GameObject logoObj = existing != null ? existing.gameObject : new GameObject("NeonDancerLogo", typeof(RectTransform));
            logoObj.transform.SetParent(titlePanelTf, false);

            var rt = (RectTransform)logoObj.transform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            // ★旧GameTitleと同じ基準位置(anchoredPosition.x=10)を使う。Y座標(15)の根拠はこのメソッドのXMLコメント参照。
            rt.anchoredPosition = new Vector2(10f, 15f);
            rt.sizeDelta = new Vector2(1200f, 400f);

            var letters = logoObj.GetComponent<TitleLogoLetters>();
            if (letters == null) letters = logoObj.AddComponent<TitleLogoLetters>();

            EditorUtility.SetDirty(logoObj);
            Debug.Log("[TitleMenu] NeonDancerLogoコンテナを作成しました。TitleLogoLettersコンポーネントの" +
                "「1. Assign Letter Sprites」→「2. Apply Default Letter Style」→「Build Logo Letters」を順に実行してください。");
        }

        /// <summary>
        /// Start/Settings/Language/Quitの4ボタンに、PauseMenuの"PAUSE"タイトルに設定されているのと
        /// 全く同じ点滅・火花演出(TitleNeonEffect)を追加する。設定値は05_Game.unity側の実際のシーン値を
        /// そのまま複製している（乱数タネの色パレットも含む）。再実行しても安全。
        /// </summary>
        [ContextMenu("6. Apply Neon Effect To Buttons (点滅・火花をボタンに追加、Pauseと同じ設定)")]
        private void ApplyNeonEffectToButtons()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[TitleMenu] Canvasが見つかりません。");
                return;
            }

            string[] names = { "StartButtonImage", "SettingsButtonImage", "LanguageButtonImage", "QuitButtonImage" };
            int applied = 0;
            foreach (var name in names)
            {
                var tf = FindDeep(canvas.transform, name);
                if (tf == null)
                {
                    Debug.LogWarning($"[TitleMenu] {name}が見つかりませんでした。スキップします。");
                    continue;
                }
                ApplyNeonEffectToOne(tf.gameObject);
                applied++;
            }
            Debug.Log($"[TitleMenu] {applied}個のボタンに点滅・火花演出を追加しました（Pauseと同じ設定値）。");
        }

        private void ApplyNeonEffectToOne(GameObject go)
        {
            var effect = go.GetComponent<TitleNeonEffect>();
            if (effect == null) effect = go.AddComponent<TitleNeonEffect>();

            var glow = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Generated/UI/SoftGlowCircle.png");
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Generated/UI/UIAdditiveGlow.mat");
            if (glow == null || mat == null)
            {
                Debug.LogWarning($"[TitleMenu] {go.name}: SoftGlowCircle.png / UIAdditiveGlow.matが見つかりません（火花演出に必要）。");
            }

            // ★TitleNeonEffectのフィールドは全てprivateのため、SerializedObject経由で設定する。
            //   値はすべて05_Game.unity上のPauseMenu"PAUSE"タイトルの実測値と完全に一致させる。
            var so = new SerializedObject(effect);
            so.FindProperty("powerOnSequenceEnabled").boolValue = false;
            so.FindProperty("powerOnStartDelay").floatValue = 0.8f;
            so.FindProperty("powerOnStartDelayBrightness").floatValue = 0f;
            so.FindProperty("powerOnFlickerCount").intValue = 4;
            so.FindProperty("powerOnFlickerMinInterval").floatValue = 0.03f;
            so.FindProperty("powerOnFlickerMaxInterval").floatValue = 0.15f;
            so.FindProperty("powerOnDimBrightness").floatValue = 0.15f;

            so.FindProperty("randomFlickerEnabled").boolValue = true;
            so.FindProperty("randomFlickerIntervalMin").floatValue = 2f;
            so.FindProperty("randomFlickerIntervalMax").floatValue = 4f;
            so.FindProperty("randomFlickerBlinkCountMin").intValue = 1;
            so.FindProperty("randomFlickerBlinkCountMax").intValue = 3;
            so.FindProperty("randomFlickerDimBrightness").floatValue = 0.3f;
            so.FindProperty("randomFlickerBlinkDuration").floatValue = 0.1f;

            so.FindProperty("breathingEnabled").boolValue = true;
            so.FindProperty("breathingSpeed").floatValue = 0.6f;
            so.FindProperty("breathingAmount").floatValue = 0.3f;

            so.FindProperty("waveEnabled").boolValue = false;

            so.FindProperty("glowSprite").objectReferenceValue = glow;
            so.FindProperty("additiveGlowMaterial").objectReferenceValue = mat;

            so.FindProperty("sparkEnabled").boolValue = true;
            so.FindProperty("sparkIntervalMin").floatValue = 1f;
            so.FindProperty("sparkIntervalMax").floatValue = 3f;
            so.FindProperty("sparkAreaWidth").floatValue = 700f;
            so.FindProperty("sparkAreaHeight").floatValue = 100f;
            so.FindProperty("sparkBurstCount").intValue = 24;
            so.FindProperty("sparkSizeMin").floatValue = 6f;
            so.FindProperty("sparkSizeMax").floatValue = 8f;
            so.FindProperty("sparkSpeedMin").floatValue = 80f;
            so.FindProperty("sparkSpeedMax").floatValue = 260f;
            so.FindProperty("sparkSizeMultiplier").floatValue = 1.4f;
            so.FindProperty("sparkLifetimeMin").floatValue = 0.2f;
            so.FindProperty("sparkLifetimeMax").floatValue = 0.5f;
            so.FindProperty("sparkGravity").floatValue = 300f;

            Color[] areaColors =
            {
                new Color(0.608f, 0.561f, 0.780f, 1f),
                new Color(0.298f, 0.686f, 0.490f, 1f),
                new Color(0.553f, 0.600f, 0.682f, 1f),
                new Color(0.878f, 0.478f, 0.247f, 1f),
                new Color(0.698f, 0.227f, 0.322f, 1f),
                new Color(0.878f, 0.690f, 0.310f, 1f),
                new Color(0.310f, 0.561f, 0.878f, 1f),
                new Color(0.373f, 0.839f, 0.839f, 1f),
                new Color(0.639f, 0.682f, 0.878f, 1f),
                new Color(0.910f, 0.788f, 0.416f, 1f),
            };
            var colorsProp = so.FindProperty("sparkAreaColors");
            colorsProp.arraySize = areaColors.Length;
            for (int i = 0; i < areaColors.Length; i++)
            {
                colorsProp.GetArrayElementAtIndex(i).colorValue = areaColors[i];
            }

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(go);
        }

        /// <summary>
        /// Start/Settings/Language/Quitの4ボタンに、AreaSelectのGemManagementButton等と同じ
        /// ホバー拡大・SE・点滅演出(ButtonHoverEffect)を追加する。設定値は03_AreaSelect.unity上の
        /// GemManagementButtonの実測値をそのまま複製している。再実行しても安全。
        /// </summary>
        [ContextMenu("12. Apply Hover Effect To Buttons (ホバー拡大・SEをボタンに追加)")]
        private void ApplyHoverEffectToButtons()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[TitleMenu] Canvasが見つかりません。");
                return;
            }

            string[] names = { "StartButtonImage", "SettingsButtonImage", "LanguageButtonImage", "QuitButtonImage" };
            int applied = 0;
            foreach (var name in names)
            {
                var tf = FindDeep(canvas.transform, name);
                if (tf == null)
                {
                    Debug.LogWarning($"[TitleMenu] {name}が見つかりませんでした。スキップします。");
                    continue;
                }
                ApplyHoverEffectToOne(tf.gameObject);
                applied++;
            }
            Debug.Log($"[TitleMenu] {applied}個のボタンにホバー拡大・SEを追加しました（GemManagementButtonと同じ設定値）。");
        }

        private void ApplyHoverEffectToOne(GameObject go)
        {
            var hover = go.GetComponent<ButtonHoverEffect>();
            if (hover == null) hover = go.AddComponent<ButtonHoverEffect>();

            var hoverSE = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/GEM/カーソル移動1.mp3");
            var blinkImg = go.GetComponent<Image>();

            var so = new SerializedObject(hover);
            so.FindProperty("hoverScale").floatValue = 1.3f;
            so.FindProperty("hoverScaleDuration").floatValue = 0.1f;
            so.FindProperty("hoverSE").objectReferenceValue = hoverSE;
            so.FindProperty("hoverSEVolume").floatValue = 1f;
            so.FindProperty("blinkTarget").objectReferenceValue = blinkImg;
            so.FindProperty("blinkSpeed").floatValue = 1f;
            so.FindProperty("blinkColor").colorValue = new Color(0.392157f, 0.392157f, 0.392157f, 1f);
            so.FindProperty("blinkIntensity").floatValue = 0.8f;
            so.FindProperty("requireInteractable").boolValue = false;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(go);
        }

        /// <summary>
        /// タイトル画面の右端に、ゲーム進行度を初期化するデバッグ用ボタンを配置する。
        /// クリックすると06_Resetシーンへ遷移し(既存のOnClickDebugResetProgress経由)、
        /// そちらでYes/No確認 → ProgressManager.ResetAll()実行 → Titleへの復帰まで完結する
        /// (既存のResetUI.cs / SceneController.GoToReset()と同じ仕組みを流用しているだけで、新規のリセット処理は書いていない)。
        /// 本番のSTART等のボタンと混同しないよう、小さく控えめな見た目にする。再実行しても安全。
        /// </summary>
        [ContextMenu("13. Setup Debug Reset Button (デバッグ用進行度リセットボタンを配置)")]
        private void SetupDebugResetButton()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[TitleMenu] Canvasが見つかりません。");
                return;
            }

            var existing = FindDeep(canvas.transform, "DebugResetProgressButton");
            GameObject btnObj = existing != null ? existing.gameObject : new GameObject("DebugResetProgressButton", typeof(RectTransform));
            btnObj.transform.SetParent(canvas.transform, false);

            var rt = (RectTransform)btnObj.transform;
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = new Vector2(-24f, 0f);
            rt.sizeDelta = new Vector2(150f, 56f);

            var img = btnObj.GetComponent<Image>();
            if (img == null) img = btnObj.AddComponent<Image>();
            img.color = new Color(0.35f, 0.08f, 0.08f, 0.85f);
            img.raycastTarget = true;

            var btn = btnObj.GetComponent<Button>();
            if (btn == null) btn = btnObj.AddComponent<Button>();
            btn.targetGraphic = img;

            var textTf = btnObj.transform.Find("Text");
            GameObject textObj = textTf != null ? textTf.gameObject : new GameObject("Text", typeof(RectTransform));
            textObj.transform.SetParent(btnObj.transform, false);
            var textRect = (RectTransform)textObj.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.sizeDelta = Vector2.zero;
            textRect.anchoredPosition = Vector2.zero;

            var tmp = textObj.GetComponent<TextMeshProUGUI>();
            if (tmp == null) tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = "進行度\nリセット(DEBUG)";
            tmp.fontSize = 16f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(1f, 0.65f, 0.65f, 1f);
            tmp.raycastTarget = false;

            debugResetProgressButton = btn;

            EditorUtility.SetDirty(btnObj);
            Debug.Log("[TitleMenu] DebugResetProgressButtonを配置しました。");
        }

        /// <summary>
        /// buttonsGroup内のQuitButtonを複製してLanguageButtonを作り、対応する見た目画像レイヤーも追加する。
        /// クリック時はSEのみ再生し、遷移・画面表示は行わない（機能未実装のプレースホルダー）。
        /// 先に「3. Apply Neon Style」でQuitを新デザインにしてから実行すること。再実行しても安全。
        /// </summary>
        [ContextMenu("4. Add Language Button (LANGUAGEボタンを新規追加)")]
        private void AddLanguageButton()
        {
            if (neonFrameSprite == null)
            {
                Debug.LogWarning("[TitleMenu] neonFrameSpriteが未設定です。先に「1. Assign Neon Frame Sprite」を実行してください。");
                return;
            }
            if (languageTextSprite == null)
            {
                Debug.LogWarning("[TitleMenu] languageTextSpriteが未設定です。先に「2. Assign Button Text Sprites」を実行してください。");
                return;
            }
            if (buttonsGroup == null)
            {
                Debug.LogError("[TitleMenu] buttonsGroupが未設定です。");
                return;
            }

            var quitBtnTf = buttonsGroup.Find("QuitButton");
            if (quitBtnTf == null)
            {
                Debug.LogError("[TitleMenu] QuitButtonが見つかりません。複製元が必要です。");
                return;
            }

            // クリック判定側：既存のLanguageButtonがあれば使い回す。無ければQuitButtonを複製してQuitの直前に挿入する
            var existingBtnTf = buttonsGroup.Find("LanguageButton");
            GameObject btnObj;
            if (existingBtnTf != null)
            {
                btnObj = existingBtnTf.gameObject;
            }
            else
            {
                btnObj = Instantiate(quitBtnTf.gameObject, buttonsGroup);
                btnObj.name = "LanguageButton";
                btnObj.transform.SetSiblingIndex(quitBtnTf.GetSiblingIndex());
            }
            languageButton = btnObj.GetComponent<Button>();
            EditorUtility.SetDirty(btnObj);

            // 見た目側：QuitButtonImageを複製してPanels > TitlePanel配下に追加する
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[TitleMenu] Canvasが見つかりません。");
                return;
            }
            var quitImgTf = FindDeep(canvas.transform, "QuitButtonImage");
            if (quitImgTf == null)
            {
                Debug.LogError("[TitleMenu] QuitButtonImageが見つかりません。複製元が必要です。");
                return;
            }

            var existingImgTf = FindDeep(canvas.transform, "LanguageButtonImage");
            Transform langImgTf;
            if (existingImgTf != null)
            {
                langImgTf = existingImgTf;
            }
            else
            {
                var langImgObj = Instantiate(quitImgTf.gameObject, quitImgTf.parent);
                langImgTf = langImgObj.transform;
                langImgTf.SetSiblingIndex(quitImgTf.GetSiblingIndex());
            }

            ApplyNeonButtonVisual(langImgTf, "LanguageButtonImage", languageTextSprite, -79.2f, new Vector2(307.7f, 131.9f));

            EditorUtility.SetDirty(this);
            Debug.Log("[TitleMenu] LanguageButtonを追加しました（クリック時はSEのみ・遷移なし）。");
        }

        /// <summary>
        /// 1つのボタンの見た目画像レイヤーを、共通ネオン枠+個別文字画像の構成に更新する共通処理。
        /// </summary>
        private void ApplyNeonButtonVisual(Transform imgTf, string newName, Sprite textSprite, float anchoredY, Vector2 textSize)
        {
            if (imgTf == null)
            {
                Debug.LogWarning($"[TitleMenu] {newName}用の画像オブジェクトが見つかりませんでした。");
                return;
            }
            imgTf.gameObject.name = newName;

            var img = imgTf.GetComponent<Image>();
            if (img != null)
            {
                img.sprite = neonFrameSprite;
                img.type = Image.Type.Simple;
                // ★横幅だけ2倍にする指示のため、あえてpreserveAspect=falseにして横方向にだけ引き伸ばす
                //   （trueのままだと高さ基準でフィットし直され、見た目の横幅が変わらないため）
                img.preserveAspect = false;
                EditorUtility.SetDirty(img);
            }

            // ★横幅500・縦幅125に縮小。縦幅を狭めた分、SETTINGS/LANGUAGEの文字(137px/132px)は
            //   枠からわずかにはみ出る（フォントサイズは変えない指示のため許容）。
            var rt = imgTf as RectTransform;
            if (rt != null)
            {
                rt.sizeDelta = new Vector2(500f, 125f);
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, anchoredY);
                EditorUtility.SetDirty(rt);
            }

            // ★文字は枠の中央に重ねる。textSizeは呼び出し側で「実際の文字の見た目の高さ」が
            //   全ボタン共通になるよう、各画像の余白比率から逆算した個別サイズ（アスペクト比は保持）。
            var textTf = imgTf.Find("ButtonNeonText");
            GameObject textObj = textTf != null ? textTf.gameObject : new GameObject("ButtonNeonText", typeof(RectTransform));
            textObj.transform.SetParent(imgTf, false);

            // ★前バージョン(TMProテキスト)の名残があれば削除する
            var staleTmp = textObj.GetComponent<TextMeshProUGUI>();
            if (staleTmp != null) DestroyImmediate(staleTmp);

            var textRect = (RectTransform)textObj.transform;
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = Vector2.zero;
            textRect.sizeDelta = textSize;

            var textImg = textObj.GetComponent<Image>();
            if (textImg == null) textImg = textObj.AddComponent<Image>();
            textImg.sprite = textSprite;
            textImg.type = Image.Type.Simple;
            textImg.preserveAspect = true;
            textImg.raycastTarget = false;
            EditorUtility.SetDirty(textObj);
        }

        /// <summary>
        /// 見た目画像(〜ButtonImage)にButtonコンポーネントを追加し、クリック判定を画像のRectTransform矩形と
        /// 完全に一致させる。従来ButtonsGroup内にあった独立したクリック判定用Button（VerticalLayoutGroupで
        /// 自動配置されており、見た目画像の手動配置位置とズレていた）は削除せず非アクティブ化するだけにする
        /// （問題があった場合に手動で戻せるように）。startButton/resetButton/languageButton/quitButtonの参照先を
        /// 見た目画像側のButtonに差し替え、bindListenersModeをBySlotに変更する。再実行しても安全。
        /// </summary>
        [ContextMenu("8. Fix Button Click Area (クリック判定を見た目画像に統合)")]
        private void FixButtonClickArea()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[TitleMenu] Canvasが見つかりません。");
                return;
            }

            Button startBtn = SetupClickAreaOnImage(FindDeep(canvas.transform, "StartButtonImage"));
            Button settingsBtn = SetupClickAreaOnImage(FindDeep(canvas.transform, "SettingsButtonImage"));
            Button languageBtn = SetupClickAreaOnImage(FindDeep(canvas.transform, "LanguageButtonImage"));
            Button quitBtn = SetupClickAreaOnImage(FindDeep(canvas.transform, "QuitButtonImage"));

            // ★旧クリック判定用(ButtonsGroup内、VerticalLayoutGroupで自動配置)は削除せず非アクティブ化するだけ。
            //   問題があれば、ここをSetActive(true)に戻し、bindListenersModeをByOrderに戻せば元通りになる。
            if (buttonsGroup != null)
            {
                foreach (Transform child in buttonsGroup)
                {
                    child.gameObject.SetActive(false);
                    EditorUtility.SetDirty(child.gameObject);
                }
            }

            startButton = startBtn;
            resetButton = settingsBtn;
            languageButton = languageBtn;
            quitButton = quitBtn;
            bindListenersMode = BindListenersMode.BySlot;

            EditorUtility.SetDirty(this);
            Debug.Log("[TitleMenu] クリック判定を見た目画像(〜ButtonImage)に統合しました。旧ButtonsGroup内のボタンは非アクティブ化しました。");
        }

        /// <summary>
        /// 1つの見た目画像オブジェクトにButtonコンポーネントを追加(なければ)し、targetGraphicを自身のImageに設定する。
        /// TransitionはNoneにする（TitleNeonEffectによる色・輝度演出と、Buttonの標準色遷移が衝突しないように）。
        /// </summary>
        private Button SetupClickAreaOnImage(Transform imgTf)
        {
            if (imgTf == null)
            {
                Debug.LogWarning("[TitleMenu] 画像オブジェクトが見つかりませんでした。");
                return null;
            }
            var img = imgTf.GetComponent<Image>();
            if (img == null)
            {
                Debug.LogWarning($"[TitleMenu] {imgTf.name}にImageがありません。");
                return null;
            }

            // ★元の見た目画像はraycastTarget=falseで作られている(クリックを透過させ、
            //   奥のButtonsGroup側の透明な判定用ボタンに通す設計だったため)。
            //   このImage自身にButtonのクリック判定を持たせるにはtrueにする必要がある。
            img.raycastTarget = true;
            EditorUtility.SetDirty(img);

            var btn = imgTf.GetComponent<Button>();
            if (btn == null) btn = imgTf.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.None;

            EditorUtility.SetDirty(btn.gameObject);
            return btn;
        }

        /// <summary>
        /// タイトル画面の背景(Canvas直下の"Background "、単一Image)を、新しく作り直した
        /// 最奥レイヤー用の星空画像(星空①.png、左右シームレスタイル版)に差し替え、ゆっくり無限スクロールする
        /// TitleStarfieldScrollを追加する。「ほぼ静止、極めてゆっくり流れる」という当初の演出案に合わせたもの。
        /// スクロールにはストレッチアンカーではなく固定サイズの中央アンカーが必要なため、RectTransformの
        /// アンカー構成もCanvas全体を覆う固定サイズ(1920x1080、ReferenceResolutionと同じ)に変更する。
        /// パララックス化(中間の軌道弧・手前の光の粒子)は別レイヤーとして後で追加する予定。再実行しても安全。
        /// </summary>
        [ContextMenu("10. Setup Starfield Background (最奥の星空背景に差し替え+スクロール追加)")]
        private void SetupStarfieldBackground()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[TitleMenu] Canvasが見つかりません。");
                return;
            }

            var bgTf = FindDeep(canvas.transform, "Background ") ?? FindDeep(canvas.transform, "Background");
            if (bgTf == null)
            {
                Debug.LogError("[TitleMenu] 背景オブジェクト('Background ')が見つかりません。");
                return;
            }

            var img = bgTf.GetComponent<Image>();
            if (img == null)
            {
                Debug.LogError($"[TitleMenu] '{bgTf.name}'にImageコンポーネントがありません。");
                return;
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Title/星空①.png");
            if (sprite == null)
            {
                Debug.LogError("[TitleMenu] 星空①.pngが見つかりません。");
                return;
            }

            img.sprite = sprite;
            img.preserveAspect = false;

            var bgRect = bgTf as RectTransform;
            if (bgRect != null)
            {
                bgRect.anchorMin = new Vector2(0.5f, 0.5f);
                bgRect.anchorMax = new Vector2(0.5f, 0.5f);
                bgRect.pivot = new Vector2(0.5f, 0.5f);
                bgRect.sizeDelta = new Vector2(1920f, 1080f);
                bgRect.anchoredPosition = Vector2.zero;
            }

            var scroll = bgTf.GetComponent<TitleStarfieldScroll>();
            if (scroll == null) scroll = bgTf.gameObject.AddComponent<TitleStarfieldScroll>();

            EditorUtility.SetDirty(img);
            EditorUtility.SetDirty(bgTf.gameObject);
            Debug.Log($"[TitleMenu] '{bgTf.name}'の背景を星空①.pngに差し替え、無限スクロールを追加しました。");
        }

        /// <summary>
        /// タイトル画面の中間レイヤー演出(TitleOrbitTrailFX)を、星空(最奥)の手前・
        /// タイトルロゴ/ボタン(TitlePanel)の奥という位置(Canvas直下、Background の直後)に配置する。
        /// 静止画は使わず、彗星が楕円軌道を描きながらドットを残していく演出をコード生成のみで行う。
        /// 既に存在する場合は位置とアセット参照だけ更新する。再実行しても安全。
        /// </summary>
        [ContextMenu("11. Setup Orbit Trail FX (中間レイヤーの軌道彗星演出を配置)")]
        private void SetupOrbitTrailFX()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[TitleMenu] Canvasが見つかりません。");
                return;
            }

            var bgTf = FindDeep(canvas.transform, "Background ") ?? FindDeep(canvas.transform, "Background");

            var existing = FindDeep(canvas.transform, "OrbitTrailLayer");
            GameObject layerObj;
            if (existing != null)
            {
                layerObj = existing.gameObject;
            }
            else
            {
                layerObj = new GameObject("OrbitTrailLayer", typeof(RectTransform));
                layerObj.transform.SetParent(canvas.transform, false);
            }

            // ★星空(Background)の直後(=手前)、TitlePanel等より奥に来るよう並び順を固定する
            if (bgTf != null) layerObj.transform.SetSiblingIndex(bgTf.GetSiblingIndex() + 1);

            var rt = (RectTransform)layerObj.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;

            var fx = layerObj.GetComponent<TitleOrbitTrailFX>();
            if (fx == null) fx = layerObj.AddComponent<TitleOrbitTrailFX>();

            var glow = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Generated/UI/SoftGlowCircle.png");
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Generated/UI/UIAdditiveGlow.mat");
            if (glow == null || mat == null)
            {
                Debug.LogWarning("[TitleMenu] SoftGlowCircle.png / UIAdditiveGlow.matが見つかりません。");
            }

            var so = new SerializedObject(fx);
            so.FindProperty("glowSprite").objectReferenceValue = glow;
            so.FindProperty("additiveGlowMaterial").objectReferenceValue = mat;
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(layerObj);
            Debug.Log("[TitleMenu] OrbitTrailLayerを配置しました。");
        }

        /// <summary>
        /// Titleシーンに SoundSettingsManager が存在せず、SoundSettingsManager.Instance が常にnullになっていたため
        /// タイトル画面のサウンド設定(BGM/SE)が一切反映されない不具合があった。
        /// これを解消するため、Titleシーンにも SoundSettingsManager を配置する
        /// (SoundSettingsManager.Awake()でDontDestroyOnLoadされるので、以降のシーンでも同じインスタンスが使われる)。
        /// 既に存在する場合は何もしない。再実行しても安全。
        /// </summary>
        [ContextMenu("9. Setup Sound Settings Manager (タイトルにサウンド設定管理を配置)")]
        private void SetupSoundSettingsManager()
        {
            var existing = FindFirstObjectByType<SoundSettingsManager>();
            if (existing != null)
            {
                Debug.Log($"[TitleMenu] SoundSettingsManagerは既に存在します('{existing.gameObject.name}')。何もしません。");
                return;
            }

            var go = new GameObject("SoundSettingsManager");
            var mgr = go.AddComponent<SoundSettingsManager>();

            // ★05_GameシーンのPauseSystem内にあるSoundSettingsManagerと同じデフォルト値に揃える。
            var so = new SerializedObject(mgr);
            so.FindProperty("bgmVolume").floatValue = 0.5f;
            so.FindProperty("seVolume").floatValue = 1f;
            so.FindProperty("bgmVolumeKey").stringValue = "BGMVolume";
            so.FindProperty("seVolumeKey").stringValue = "SEVolume";
            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(go);
            Debug.Log("[TitleMenu] SoundSettingsManagerをTitleシーンに配置しました。");
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>
        /// Titleシーンにサウンド設定パネル（BGM/SEスライダー・戻るボタン）を生成する。
        /// PauseMenuUIのSoundPanelと同じ構成・同じ画像(Assets/Art/中断画面/配下)を使う。
        /// 再実行すると既存のSoundPanelを作り直す。
        /// </summary>
        [ContextMenu("Setup Sound Panel (サウンド設定パネルを生成)")]
        private void SetupSoundPanel()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                Debug.LogError("[TitleMenu] Canvasが見つかりません。");
                return;
            }

            var existing = canvas.transform.Find("SoundPanel");
            if (existing != null) DestroyImmediate(existing.gameObject);

            GameObject soundObj = new GameObject("SoundPanel");
            soundObj.transform.SetParent(canvas.transform, false);

            // ★PauseMenuUI側のSoundPanelは実際には(600,800)に調整されている(コード上の初期値500x400のままではない)。
            //   同じ画像(MainBg.png等)を使う都合上、比率を合わせるためTitleも同じサイズにする。
            RectTransform soundRect = soundObj.AddComponent<RectTransform>();
            soundRect.anchorMin = new Vector2(0.5f, 0.5f);
            soundRect.anchorMax = new Vector2(0.5f, 0.5f);
            soundRect.sizeDelta = new Vector2(600f, 800f);
            soundRect.anchoredPosition = Vector2.zero;

            // ★背景はPauseMenuUIのSoundPanelと同じ画像(MainBg.png)を使う。
            //   PauseMenuUI側もパネル本体(500x400)より一回り大きい画像(800x950)を
            //   ignoreLayoutで中央に重ねる構成になっているため、それをそのまま踏襲する。
            GameObject soundBgObj = new GameObject("SoundBg");
            soundBgObj.transform.SetParent(soundObj.transform, false);
            RectTransform soundBgRect = soundBgObj.AddComponent<RectTransform>();
            soundBgRect.anchorMin = new Vector2(0.5f, 0.5f);
            soundBgRect.anchorMax = new Vector2(0.5f, 0.5f);
            soundBgRect.sizeDelta = new Vector2(800f, 950f);
            soundBgRect.anchoredPosition = Vector2.zero;
            Image soundBg = soundBgObj.AddComponent<Image>();
            soundBg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/中断画面/MainBg.png");
            soundBg.color = Color.white;
            LayoutElement soundBgLayout = soundBgObj.AddComponent<LayoutElement>();
            soundBgLayout.ignoreLayout = true;

            VerticalLayoutGroup layout = soundObj.AddComponent<VerticalLayoutGroup>();
            // ★PauseMenuUI側は実際にはspacing=60(コード上の初期値30のままではない)。
            layout.spacing = 60f;
            layout.padding = new RectOffset(40, 40, 40, 40);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            CreateText(soundObj.transform, "TitleText", "サウンド設定", 36, TextAlignmentOptions.Center, 60f).fontStyle = FontStyles.Bold;

            // ★PauseMenuUI側は実際にはフォントサイズ36(コード上の初期値28のままではない)。
            bgmVolumeText = CreateText(soundObj.transform, "BGMVolumeText", ": 100%", 36, TextAlignmentOptions.Center, 40f);
            bgmVolumeSlider = CreateSlider(soundObj.transform, "BGMSlider");

            seVolumeText = CreateText(soundObj.transform, "SEVolumeText", ": 100%", 36, TextAlignmentOptions.Center, 40f);
            seVolumeSlider = CreateSlider(soundObj.transform, "SESlider");

            // ★PauseMenuUI側のBackButtonは実際には高さ120(コード上の初期値60のままではない)。
            soundBackButton = CreateButton(soundObj.transform, "BackButton", "BACK", 120f, createBg: true);
            ApplyPauseStyleBackButtonVisual(soundBackButton.transform);
            // ★クリックSEはHideSoundPanel()側で鳴らす(Awake()でリスナー登録される度に実行時反映されるため)。
            //   ここでAddListenerしても、スクリプトから動的に追加したリスナーはシリアライズされず、
            //   シーンを保存してもEditor拡張の一時的な効果で終わってしまう。

            soundPanel = soundObj;
            soundObj.SetActive(false);

            EditorUtility.SetDirty(this);
            Debug.Log("[TitleMenu] SoundPanelを生成しました。");
        }

        /// <summary>
        /// 既存のSoundPanelを壊さずに、「サウンド設定」「BGM」「SE」の文字をネオン管画像に置き換える。
        /// タイトル文字はText非表示+Image追加、BGM/SEは数値テキストを維持したまま左にアイコン画像を追加する。
        /// 再実行しても安全（DestroyImmediateは行わない）。
        /// </summary>
        [ContextMenu("Apply Sound Panel Neon Images (サウンド設定/BGM/SEをネオン画像に置換)")]
        private void ApplySoundPanelNeonImages()
        {
            if (soundPanel == null)
            {
                Debug.LogError("[TitleMenu] soundPanelが未設定です。先にSetup Sound Panelを実行してください。");
                return;
            }

            var soundSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/SOUND/① SOUND（パネルタイトル）.png");
            var bgmSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/SOUND/② BGM（サウンドパネル内ラベル）.png");
            var seSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/SOUND/③ SE（サウンドパネル内ラベル）.png");
            if (soundSprite == null || bgmSprite == null || seSprite == null)
            {
                Debug.LogError("[TitleMenu] SOUND/BGM/SEのネオン画像が見つかりません（Assets/Art/SOUND/）。");
                return;
            }

            // ★タイトル画像を60→190に拡大した分、パネルの縦幅が70px不足し戻るボタンがはみ出すため、
            //   パネル本体とその背景(MainBg.png)を拡大した差分(+100)だけ広げる。
            //   横幅・パネルと背景のマージン比率(+200/+150)は変更しない。
            var soundPanelRect = (RectTransform)soundPanel.transform;
            soundPanelRect.sizeDelta = new Vector2(soundPanelRect.sizeDelta.x, 900f);
            var soundBgTf = soundPanel.transform.Find("SoundBg") as RectTransform;
            if (soundBgTf != null) soundBgTf.sizeDelta = new Vector2(soundBgTf.sizeDelta.x, 1050f);

            var titleTextTf = soundPanel.transform.Find("TitleText");
            if (titleTextTf != null)
            {
                titleTextTf.gameObject.SetActive(false);

                var titleImgTf = soundPanel.transform.Find("TitleImage");
                GameObject titleImgObj = titleImgTf != null ? titleImgTf.gameObject : new GameObject("TitleImage", typeof(RectTransform));
                titleImgObj.transform.SetParent(soundPanel.transform, false);
                titleImgObj.transform.SetSiblingIndex(titleTextTf.GetSiblingIndex());

                var img = titleImgObj.GetComponent<Image>();
                if (img == null) img = titleImgObj.AddComponent<Image>();
                img.sprite = soundSprite;
                img.preserveAspect = true;
                img.raycastTarget = false;

                // ★SoundPanelのVerticalLayoutGroupはchildControlHeight=falseのため、高さは
                //   LayoutElementでは反映されず、子自身のRectTransform.sizeDelta.yがそのまま使われる
                //   （幅はchildControlWidth=trueで自動調整される）。PauseMenuUI.ApplyPauseTitleImage()と同じ考え方。
                // ★190という値は「Pause.pngと見た目の文字サイズを一致させる」ために実測して逆算した値。
                //   Pause.pngは実際の可視文字部分がキャンバス高316pxの55.7%で、pauseTitleImageHeight=200のとき
                //   可視文字の高さ=約111px。SOUND.pngは可視部分がキャンバス高301pxの58.8%なので、
                //   同じ可視文字高さ(約111px)にするには枠の高さを 111/0.588 ≒ 190 にする必要がある。
                var rt = (RectTransform)titleImgObj.transform;
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, 190f);

                var staleLE = titleImgObj.GetComponent<LayoutElement>();
                if (staleLE != null) DestroyImmediate(staleLE);
            }

            // ★52/48という値は「BGM/SEの可視文字の高さを揃える」ために実測して逆算した値。
            //   BGM.pngは可視部分がキャンバス高353pxの60.9%、SE.pngは可視部分がキャンバス高408pxの67.4%と
            //   余白比率が異なるため、同じ枠高さ(旧40px)では見た目のサイズが不揃いになっていた。
            //   目標の可視文字高さ(約32px、数値テキストと同程度)になるよう、画像ごとに枠の高さを変えて揃える。
            ApplyVolumeLabelIcon(bgmVolumeText, bgmSprite, 707f / 353f, 52f);
            ApplyVolumeLabelIcon(seVolumeText, seSprite, 612f / 408f, 48f);

            EditorUtility.SetDirty(soundPanel);
            Debug.Log("[TitleMenu] SoundPanelのテキストをネオン画像に置き換えました。");
        }

        /// <summary>
        /// SoundPanelの"SOUND"タイトル画像に、05_Game内PauseMenuUIの"PAUSE"タイトル画像に
        /// 実際に設定されているTitleNeonEffectの値(点滅・火花含む全フィールド)と全く同じ値を設定する。
        /// Titleシーンには参照できるPAUSEオブジェクトが存在しないため、05_Game.unityの
        /// PauseMenuUI側TitleImageの実際のシリアライズ値を直接読み取って転記している(推測ではない)。
        /// 再実行しても安全。先に「Setup Sound Panel」でTitleImageを作っておく必要がある。
        /// </summary>
        [ContextMenu("Apply Sound Title Neon Effect (PAUSEと同じ点滅・火花設定をSOUND画像に反映)")]
        private void ApplySoundTitleNeonEffect()
        {
            if (soundPanel == null)
            {
                Debug.LogError("[TitleMenu] soundPanelが未設定です。");
                return;
            }

            var titleImgTf = soundPanel.transform.Find("TitleImage");
            if (titleImgTf == null)
            {
                Debug.LogError("[TitleMenu] TitleImageが見つかりません。先に「Apply Sound Panel Neon Images」を実行してください。");
                return;
            }

            var neonEffect = titleImgTf.GetComponent<TitleNeonEffect>();
            if (neonEffect == null) neonEffect = titleImgTf.gameObject.AddComponent<TitleNeonEffect>();

            var glow = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Generated/UI/SoftGlowCircle.png");
            var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Generated/UI/UIAdditiveGlow.mat");
            if (glow == null || mat == null)
            {
                Debug.LogWarning("[TitleMenu] SoftGlowCircle.png / UIAdditiveGlow.matが見つかりません（火花演出に必要）。");
            }

            var so = new SerializedObject(neonEffect);
            so.FindProperty("powerOnSequenceEnabled").boolValue = false;
            so.FindProperty("randomFlickerEnabled").boolValue = true;
            so.FindProperty("randomFlickerIntervalMin").floatValue = 2f;
            so.FindProperty("randomFlickerIntervalMax").floatValue = 4f;
            so.FindProperty("randomFlickerBlinkCountMin").intValue = 1;
            so.FindProperty("randomFlickerBlinkCountMax").intValue = 3;
            so.FindProperty("randomFlickerDimBrightness").floatValue = 0.3f;
            so.FindProperty("randomFlickerBlinkDuration").floatValue = 0.1f;
            so.FindProperty("breathingEnabled").boolValue = true;
            so.FindProperty("breathingSpeed").floatValue = 0.6f;
            so.FindProperty("breathingAmount").floatValue = 0.3f;
            so.FindProperty("waveEnabled").boolValue = false;
            so.FindProperty("glowSprite").objectReferenceValue = glow;
            so.FindProperty("additiveGlowMaterial").objectReferenceValue = mat;
            so.FindProperty("sparkEnabled").boolValue = true;
            so.FindProperty("sparkIntervalMin").floatValue = 1f;
            so.FindProperty("sparkIntervalMax").floatValue = 3f;
            so.FindProperty("sparkAreaWidth").floatValue = 700f;
            so.FindProperty("sparkAreaHeight").floatValue = 100f;
            so.FindProperty("sparkBurstCount").intValue = 24;
            so.FindProperty("sparkSizeMin").floatValue = 6f;
            so.FindProperty("sparkSizeMax").floatValue = 8f;
            so.FindProperty("sparkSpeedMin").floatValue = 80f;
            so.FindProperty("sparkSpeedMax").floatValue = 260f;
            so.FindProperty("sparkSizeMultiplier").floatValue = 1.4f;
            so.FindProperty("sparkLifetimeMin").floatValue = 0.2f;
            so.FindProperty("sparkLifetimeMax").floatValue = 0.5f;
            so.FindProperty("sparkGravity").floatValue = 300f;

            var colorsProp = so.FindProperty("sparkAreaColors");
            Color[] pauseColors =
            {
                new Color(0.608f, 0.561f, 0.78f, 1f),
                new Color(0.298f, 0.686f, 0.49f, 1f),
                new Color(0.553f, 0.6f, 0.682f, 1f),
                new Color(0.878f, 0.478f, 0.247f, 1f),
                new Color(0.698f, 0.227f, 0.322f, 1f),
                new Color(0.878f, 0.69f, 0.31f, 1f),
                new Color(0.31f, 0.561f, 0.878f, 1f),
                new Color(0.373f, 0.839f, 0.839f, 1f),
                new Color(0.639f, 0.682f, 0.878f, 1f),
                new Color(0.91f, 0.788f, 0.416f, 1f),
            };
            colorsProp.arraySize = pauseColors.Length;
            for (int i = 0; i < pauseColors.Length; i++)
            {
                colorsProp.GetArrayElementAtIndex(i).colorValue = pauseColors[i];
            }

            so.ApplyModifiedProperties();

            EditorUtility.SetDirty(titleImgTf.gameObject);
            Debug.Log("[TitleMenu] SOUND画像にPAUSEと全く同じTitleNeonEffect設定を反映しました。");
        }

        [Tooltip("BGM/SEラベルアイコンの微調整用Y位置オフセット(px)。テキストとの見た目の縦位置がずれる場合にInspectorで調整する。")]
        [SerializeField] private float volumeIconYOffset = 0f;

        /// <summary>
        /// InspectorでvolumeIconYOffsetを変更した瞬間に、既存のBGM/SE LabelIconのY位置へ即座に反映する
        /// （Apply Sound Panel Neon Imagesの再実行が不要になる）。LabelIconが未生成の間は何もしない。
        /// </summary>
        private void OnValidate()
        {
            ApplyVolumeIconYOffsetLive(bgmVolumeText);
            ApplyVolumeIconYOffsetLive(seVolumeText);
        }

        private void ApplyVolumeIconYOffsetLive(TextMeshProUGUI volumeText)
        {
            if (volumeText == null) return;
            var iconTf = volumeText.transform.Find("LabelIcon") as RectTransform;
            if (iconTf == null) return;
            iconTf.anchoredPosition = new Vector2(iconTf.anchoredPosition.x, volumeIconYOffset);
        }

        /// <summary>
        /// BGM/SEの数値テキスト(例:": 100%")を左寄せにし、左側にラベルアイコン画像を追加する。
        /// 数値テキスト自体は削除せず維持する（音量に応じて動的に変わるため画像化できない）。
        /// </summary>
        /// <param name="iconCanvasHeight">アイコン画像の枠の高さ(px)。画像ごとの透過余白比率が異なるため、
        /// 見た目の文字サイズを揃えるには画像ごとに異なる値を渡す必要がある。</param>
        private void ApplyVolumeLabelIcon(TextMeshProUGUI volumeText, Sprite iconSprite, float aspect, float iconCanvasHeight)
        {
            if (volumeText == null || iconSprite == null) return;

            // ★アイコンとテキスト(": 25%"等)を隣接させて左寄せグループにする。
            //   パネルのpadding(40)に合わせてインデントし、アイコン直後にテキストが続くようmarginで詰める。
            const float indent = 40f;
            const float gap = 12f;
            float iconWidth = iconCanvasHeight * aspect;

            volumeText.alignment = TextAlignmentOptions.Left;
            volumeText.margin = new Vector4(indent + iconWidth + gap, 0f, 0f, 0f);

            var existing = volumeText.transform.Find("LabelIcon");
            GameObject iconObj = existing != null ? existing.gameObject : new GameObject("LabelIcon", typeof(RectTransform));
            iconObj.transform.SetParent(volumeText.transform, false);

            var rect = (RectTransform)iconObj.transform;
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(iconWidth, iconCanvasHeight);
            rect.anchoredPosition = new Vector2(indent, volumeIconYOffset);

            var img = iconObj.GetComponent<Image>();
            if (img == null) img = iconObj.AddComponent<Image>();
            img.sprite = iconSprite;
            img.preserveAspect = true;
            img.raycastTarget = false;
        }

        /// <summary>
        /// CreateButton(createBg:true)で生成したBackButtonの見た目を、PauseMenuUIのBackButtonと
        /// 同じ構成(BackBg.png + BackIcon.png、"BACK"文字は非表示)に差し替える。
        /// </summary>
        private void ApplyPauseStyleBackButtonVisual(Transform backButtonTf)
        {
            // ★CreateButton内でcreateBg:trueの時、name.Replace("Button","Bg")で"BackBg"という名前の
            //   子オブジェクトが先に作られている。
            var bgTf = backButtonTf.Find("BackBg");
            if (bgTf != null)
            {
                var bgImg = bgTf.GetComponent<Image>();
                if (bgImg != null)
                {
                    bgImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/中断画面/BackBg.png");
                    bgImg.color = Color.white;
                }
                var bgRect = bgTf as RectTransform;
                if (bgRect != null)
                {
                    // ★元はanchorMin=(0,0)/anchorMax=(1,1)のストレッチ(親に完全フィット)のため、
                    //   このままsizeDeltaだけ変更すると「親のサイズ+sizeDelta」になってしまい巨大化する。
                    //   PauseMenuUI側の実際のBackBgは中央基準の固定アンカー(0.5,0.5)なので、それに合わせる。
                    bgRect.anchorMin = new Vector2(0.5f, 0.5f);
                    bgRect.anchorMax = new Vector2(0.5f, 0.5f);
                    bgRect.sizeDelta = new Vector2(630f, 280f);
                    bgRect.anchoredPosition = new Vector2(0f, -6f);
                }
            }

            // ★"BACK"の文字ラベル(CreateButton内で"Text"という名前で生成される)は、
            //   PauseMenuUIと同様にBackIcon画像だけを見せるため非表示にする(削除はしない)。
            var textTf = backButtonTf.Find("Text");
            if (textTf != null) textTf.gameObject.SetActive(false);

            var existingIcon = backButtonTf.Find("BackIcon");
            GameObject iconObj = existingIcon != null ? existingIcon.gameObject : new GameObject("BackIcon", typeof(RectTransform));
            iconObj.transform.SetParent(backButtonTf, false);

            var iconRect = (RectTransform)iconObj.transform;
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.anchoredPosition = Vector2.zero;
            iconRect.sizeDelta = new Vector2(400f, 200f);

            var iconImg = iconObj.GetComponent<Image>();
            if (iconImg == null) iconImg = iconObj.AddComponent<Image>();
            iconImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/中断画面/BackIcon.png");
            iconImg.color = Color.white;
            iconImg.raycastTarget = false;

            // ★PauseMenuUIのBackButtonと同じホバー演出(拡大・SE・BackBgの点滅)を追加する。
            //   設定値は05_Game.unity上のPauseMenuUI BackButtonの実測値をそのまま複製している。
            var hover = backButtonTf.GetComponent<ButtonHoverEffect>();
            if (hover == null) hover = backButtonTf.gameObject.AddComponent<ButtonHoverEffect>();

            var hoverSE = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/GEM/カーソル移動2.mp3");
            var blinkTargetImg = bgTf != null ? bgTf.GetComponent<Image>() : null;

            var hoverSo = new SerializedObject(hover);
            hoverSo.FindProperty("hoverScale").floatValue = 1.05f;
            hoverSo.FindProperty("hoverScaleDuration").floatValue = 0.1f;
            hoverSo.FindProperty("hoverSE").objectReferenceValue = hoverSE;
            hoverSo.FindProperty("hoverSEVolume").floatValue = 1f;
            hoverSo.FindProperty("blinkTarget").objectReferenceValue = blinkTargetImg;
            hoverSo.FindProperty("blinkSpeed").floatValue = 1f;
            hoverSo.FindProperty("blinkColor").colorValue = new Color(0.392157f, 0.392157f, 0.392157f, 1f);
            hoverSo.FindProperty("blinkIntensity").floatValue = 0.8f;
            hoverSo.FindProperty("requireInteractable").boolValue = false;
            hoverSo.ApplyModifiedProperties();

            EditorUtility.SetDirty(backButtonTf.gameObject);
        }

        private TextMeshProUGUI CreateText(Transform parent, string name, string text, int fontSize, TextAlignmentOptions alignment, float height)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent, false);

            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.sizeDelta = new Vector2(400f, height);

            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.color = Color.white;
            tmp.raycastTarget = false;

            LayoutElement layoutElement = textObj.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = height;

            return tmp;
        }

        private Button CreateButton(Transform parent, string name, string text, float height, float width = 200f, bool createBg = false)
        {
            GameObject btnObj = new GameObject(name);
            btnObj.transform.SetParent(parent, false);

            RectTransform btnRect = btnObj.AddComponent<RectTransform>();
            btnRect.sizeDelta = new Vector2(width, height);

            if (createBg)
            {
                string bgName = name.Replace("Button", "Bg");
                GameObject bgObj = new GameObject(bgName);
                bgObj.transform.SetParent(btnObj.transform, false);
                RectTransform bgRect = bgObj.AddComponent<RectTransform>();
                bgRect.anchorMin = Vector2.zero;
                bgRect.anchorMax = Vector2.one;
                bgRect.sizeDelta = Vector2.zero;
                bgRect.anchoredPosition = Vector2.zero;
                Image bgImage = bgObj.AddComponent<Image>();
                bgImage.color = new Color(0.3f, 0.3f, 0.3f, 1f);
                bgImage.raycastTarget = false;
                LayoutElement bgLayout = bgObj.AddComponent<LayoutElement>();
                bgLayout.ignoreLayout = true;
            }

            Image btnImage = btnObj.AddComponent<Image>();
            btnImage.color = Color.clear;

            Button btn = btnObj.AddComponent<Button>();

            GameObject btnTextObj = new GameObject("Text");
            btnTextObj.transform.SetParent(btnObj.transform, false);

            RectTransform btnTextRect = btnTextObj.AddComponent<RectTransform>();
            btnTextRect.anchorMin = Vector2.zero;
            btnTextRect.anchorMax = Vector2.one;
            btnTextRect.sizeDelta = Vector2.zero;
            btnTextRect.anchoredPosition = Vector2.zero;

            TextMeshProUGUI btnText = btnTextObj.AddComponent<TextMeshProUGUI>();
            btnText.text = text;
            btnText.fontSize = 28;
            btnText.alignment = TextAlignmentOptions.Center;
            btnText.color = Color.white;

            LayoutElement layoutElement = btnObj.AddComponent<LayoutElement>();
            layoutElement.preferredWidth = width;
            layoutElement.preferredHeight = height;

            return btn;
        }

        private Slider CreateSlider(Transform parent, string name)
        {
            GameObject sliderObj = new GameObject(name);
            sliderObj.transform.SetParent(parent, false);

            // ★PauseMenuUI側のBGMSlider/SESliderは実際には高さ50(コード上の初期値30のままではない)。
            RectTransform sliderRect = sliderObj.AddComponent<RectTransform>();
            sliderRect.sizeDelta = new Vector2(400f, 50f);

            Slider slider = sliderObj.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;

            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(sliderObj.transform, false);
            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            Image bgImage = bgObj.AddComponent<Image>();
            // ★PauseMenuUI側のSliderBackgroundは実際には白(1,1,1,1)。コード上の初期値(0.2,0.2,0.2,1)のままではない。
            bgImage.color = Color.white;

            GameObject fillAreaObj = new GameObject("Fill Area");
            fillAreaObj.transform.SetParent(sliderObj.transform, false);
            RectTransform fillAreaRect = fillAreaObj.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.sizeDelta = Vector2.zero;

            GameObject fillObj = new GameObject("Fill");
            fillObj.transform.SetParent(fillAreaObj.transform, false);
            RectTransform fillRect = fillObj.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;
            Image fillImage = fillObj.AddComponent<Image>();
            fillImage.color = new Color(0.3f, 0.7f, 1f, 1f);
            slider.fillRect = fillRect;

            GameObject handleAreaObj = new GameObject("Handle Slide Area");
            handleAreaObj.transform.SetParent(sliderObj.transform, false);
            RectTransform handleAreaRect = handleAreaObj.AddComponent<RectTransform>();
            handleAreaRect.anchorMin = Vector2.zero;
            handleAreaRect.anchorMax = Vector2.one;
            handleAreaRect.sizeDelta = Vector2.zero;

            GameObject handleObj = new GameObject("Handle");
            handleObj.transform.SetParent(handleAreaObj.transform, false);
            RectTransform handleRect = handleObj.AddComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(20f, 30f);
            Image handleImage = handleObj.AddComponent<Image>();
            handleImage.color = Color.white;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleImage;

            LayoutElement layoutElement = sliderObj.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 50f;

            return slider;
        }
#endif
    }
}
