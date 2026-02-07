using UnityEngine;

public class DialogController : MonoBehaviour
{
    [Header("Dialogs")]
    public GameObject failDialog;    // ← ここが Fail 用
    public GameObject resultDialog;

    void Start()
    {
        if (failDialog != null) failDialog.SetActive(false);
        if (resultDialog != null) resultDialog.SetActive(false);
    }

    // FailDialog を表示
    public void ShowFail()
    {
        if (failDialog != null)
            failDialog.SetActive(true);
    }

    // ResultDialog を表示
    public void ShowResult()
    {
        if (resultDialog != null)
            resultDialog.SetActive(true);
    }

    // 共通：左クリックで表示中のダイアログを閉じる
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (failDialog != null && failDialog.activeSelf)
                failDialog.SetActive(false);

            if (resultDialog != null && resultDialog.activeSelf)
                resultDialog.SetActive(false);
        }
    }
}
