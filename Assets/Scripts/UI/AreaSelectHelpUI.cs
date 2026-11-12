using UnityEngine;

/// <summary>
/// エリアセレクト（03_AreaSelect）のHELP画面（2026/11/12）。
/// ・画面左上（ミッションボタンの上）の HelpButton で開き、HELP画面の戻るボタンで閉じる
/// ・HELP画面の中身は、05_Game の中断メニューのHELP画面をメニューで写したもの（15ページ・文言は翻訳表 pause.help.page* を共通で使う）
/// ・開いている間は後ろを暗くして、エリアセレクトのボタンを押せないようにする
/// 作成はメニュー「Tools/エリアセレクト/HELPボタンとHELP画面を作る（05_GameのHELPを写す）」
/// </summary>
public class AreaSelectHelpUI : MonoBehaviour
{
    [Tooltip("開いている時だけ表示する物（暗幕＋HELP画面）。メニューで設定")]
    [SerializeField] private GameObject content;
    [Tooltip("HELPを開くボタン（HelpButton）のホバー演出。閉じた時に拡大・点滅を元に戻す（メニューで設定）")]
    [SerializeField] private Game.UI.ButtonHoverEffect openButtonHover;

    private void Awake()
    {
        if (content != null) content.SetActive(false);
    }

    private void OnDisable()
    {
        if (content != null && Game.UI.ButtonHoverEffect.ModalPanelRoot == content.transform) Game.UI.ButtonHoverEffect.ModalPanelRoot = null;
    }

    /// <summary>HELPを開く（HelpButton）。開いている間は後ろのボタンのホバー演出を止める（ミッション画面と同じ）</summary>
    public void Open()
    {
        if (content == null) return;
        content.SetActive(true);
        Game.UI.ButtonHoverEffect.ModalPanelRoot = content.transform;
    }

    /// <summary>
    /// HELPを閉じる（HELP画面の戻るボタン）。
    /// ★HelpButton はクリック後も拡大したままにする設定（ShopButton を写したため）なので、閉じた時に元の大きさへ戻す。
    ///   戻さないと、ほかのボタンにカーソルを乗せた時に2つとも拡大して見える（2026/11/12）
    /// </summary>
    public void Close()
    {
        if (content == null) return;
        if (Game.UI.ButtonHoverEffect.ModalPanelRoot == content.transform) Game.UI.ButtonHoverEffect.ModalPanelRoot = null;
        content.SetActive(false);
        if (openButtonHover != null) openButtonHover.SmoothReset();
    }

    public bool IsOpen => content != null && content.activeSelf;

#if UNITY_EDITOR
    public void EditorWire(GameObject c, Game.UI.ButtonHoverEffect hover) { content = c; openButtonHover = hover; }
#endif
}
