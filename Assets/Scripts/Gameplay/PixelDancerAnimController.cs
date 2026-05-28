using System.Collections;
using UnityEngine;

public class PixelDancerAnimController : MonoBehaviour
{
    private Animator animator;
    private int currentIndex = -1;
    private readonly string[] animNames = { "PixelDancer_パルクール", "PixelDancer_スピン", "PixelDancer_キック" };

    private Coroutine loopCoroutine;

    void Awake()
    {
        animator = GetComponent<Animator>();
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
    }

    IEnumerator RandomLoop()
    {
        while (true)
        {
            int next;
            do { next = Random.Range(0, animNames.Length); }
            while (next == currentIndex && animNames.Length > 1);
            currentIndex = next;

            Debug.Log($"[PixelDancer] Playing: {animNames[currentIndex]}");
            animator.Play(animNames[currentIndex]);
            yield return null; // アニメーション開始を1フレーム待つ

            float length = animator.GetCurrentAnimatorStateInfo(0).length;
            Debug.Log($"[PixelDancer] ClipLength: {length}s");
            yield return new WaitForSeconds(length);
        }
    }
}
