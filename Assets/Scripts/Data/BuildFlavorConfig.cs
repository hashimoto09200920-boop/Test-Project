using UnityEngine;

/// <summary>
/// ビルドの種類（ベータ用／本番用）の設定。Assets/Resources/BuildFlavorConfig.asset に置く（実行時にResourcesから読む）。
/// 切り替えはメニュー「Tools/ビルド/ベータ用に切り替え」「本番用に切り替え」で行う（パッケージ名・デバッグボタン表示も一緒に切り替わる）。
///
/// ・ベータ用：ジェムの使用回数とスタミナを無制限として扱う。セーブデータには書き込まない（その場の扱いだけ）ため、
///   課金の「無制限購入済み」の印とは混ざらない。パッケージ名は本番用の末尾に「.beta」を付け、1台に本番用と同時に入れられる。
/// ・本番用：従来どおり制限あり。
/// アセットが無い時は本番用として扱う（安全側）。
/// </summary>
[CreateAssetMenu(menuName = "Game/Build Flavor Config", fileName = "BuildFlavorConfig")]
public class BuildFlavorConfig : ScriptableObject
{
    public enum Flavor { Release, Beta }

    [Tooltip("ビルドの種類。メニュー「Tools/ビルド/〜に切り替え」で変更する（パッケージ名なども一緒に切り替わるため、ここを直接変えないこと）")]
    public Flavor flavor = Flavor.Release;

    [Tooltip("本番用のAndroidパッケージ名。ベータ用はこの末尾にBeta Package Suffixを付ける")]
    public string releaseAndroidPackageName = "com.DefaultCompany.TestProject";

    [Tooltip("ベータ用のパッケージ名に付ける末尾（本番用と別アプリとして1台に同時に入れられる）")]
    public string betaPackageSuffix = ".beta";

    [Header("ホーム画面のアプリ名（Androidビルド時に設定。Product Name は変えない＝Editorの保存データの場所は変わらない）")]
    [Tooltip("ホーム画面のアプリ名（日本語以外の端末）。空なら Product Name のまま")]
    public string appDisplayName = "Neon Dancer";
    [Tooltip("ホーム画面のアプリ名（端末の言語が日本語の時）。空なら上と同じ")]
    public string appDisplayNameJapanese = "ネオンダンサー";

    [Tooltip("ベータ用のアプリ名（ホーム画面の表示名）に付ける末尾。Androidビルド時だけ付く（Editorの保存データの場所は変わらない）")]
    public string betaAppNameSuffix = " β";

    [Tooltip("ON：Androidの自動バックアップを許可する（再インストール時にGoogleアカウントからデータが戻る）。" +
             "テスト中はOFF（入れ直すと必ず初期化された状態から始まる）。ローンチ直前にONに戻してビルドする")]
    public bool allowAndroidBackup = false;

    [Header("アプリのアイコン（切り替えメニュー実行時にProject Settingsへ設定。画像そのものではなく場所の文字列で持つ＝ゲーム本体に画像が入らない）")]
    [Tooltip("本番用アイコン（正方形。AndroidのAdaptiveの背景・Round・Legacyに使う）")]
    public string releaseIconPath = "Assets/Art/Icon/AppIcon_Release.png";
    [Tooltip("ベータ用アイコン（本番用に「β」バッジを付けたもの）")]
    public string betaIconPath = "Assets/Art/Icon/AppIcon_Beta.png";
    [Tooltip("Adaptiveアイコンの前景（透明の画像。1枚絵を背景側に使うため前景は空にする）")]
    public string adaptiveForegroundPath = "Assets/Art/Icon/AppIcon_EmptyForeground.png";

    private static BuildFlavorConfig s_cached;
    private static bool s_loaded;

    /// <summary>現在の設定（無ければnull）</summary>
    public static BuildFlavorConfig Current
    {
        get
        {
            if (!s_loaded || (s_cached == null && Application.isEditor))
            {
                s_cached = Resources.Load<BuildFlavorConfig>("BuildFlavorConfig");
                s_loaded = true;
            }
            return s_cached;
        }
    }

    /// <summary>ベータ用ビルドか（アセットが無ければfalse＝本番用）</summary>
    public static bool IsBeta
    {
        get
        {
            var c = Current;
            return c != null && c.flavor == Flavor.Beta;
        }
    }
}
