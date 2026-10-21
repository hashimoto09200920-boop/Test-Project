using System.Collections;
using UnityEngine;

public class PixelDancerAnimController : MonoBehaviour
{
    private Animator animator;
    private PixelDancerController dancerController;
    private int currentIndex = -1;
    private readonly string[] animNames = { "PixelDancer_パルクール", "PixelDancer_スピン", "PixelDancer_キック", "PixelDancer_新技" };

    private Coroutine loopCoroutine;

    void Awake()
    {
        animator = GetComponent<Animator>();
        dancerController = GetComponent<PixelDancerController>();
    }

    // ★パルクールの逆さまコマ（アニメイベント OnInvertedFrameStart 0.5秒 → OnInvertedFrameEnd 0.95秒）の途中でダンスを止めると、
    //   終わりのイベントが来ないまま「逆さま中」が残り、左向きの時だけ上下反転し続ける（NeonDancer後半移行・ダウン復帰で発生）。
    //   止めた時と次のダンスを始める直前に必ず解除する（パルクールを最初から再生すれば0.5秒のイベントで再びONになる）
    private void ClearInvertedFrame()
    {
        if (dancerController != null) dancerController.OnInvertedFrameEnd();
    }

    void OnEnable()
    {
        loopCoroutine = StartCoroutine(RandomLoop());
    }

    void OnDisable()
    {
        if (loopCoroutine != null) StopCoroutine(loopCoroutine);
        loopCoroutine = null;
        currentIndex = -1;
        ClearInvertedFrame();
    }

    public void StopDance()
    {
        if (loopCoroutine != null) { StopCoroutine(loopCoroutine); loopCoroutine = null; }
        currentIndex = -1;
        ClearInvertedFrame();
    }

    public void ResumeDance()
    {
        if (loopCoroutine != null) return;
        loopCoroutine = StartCoroutine(RandomLoop());
    }

    IEnumerator RandomLoop()
    {
        while (true)
        {
            int next;
            do { next = Random.Range(0, animNames.Length); }
            while (next == currentIndex && animNames.Length > 1);
            currentIndex = next;

            ClearInvertedFrame();
            animator.Play(animNames[currentIndex]);
            yield return null; // アニメーション開始を1フレーム待つ

            float length = animator.GetCurrentAnimatorStateInfo(0).length;
            yield return new WaitForSeconds(length);
        }
    }
}
