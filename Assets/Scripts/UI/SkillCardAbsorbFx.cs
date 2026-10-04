using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// スキル選択カードが選ばれた時、HUDアイコンへ吸い込まれるように飛んでいく演出。
    /// SkillCardUI本体（3枚の固定スロット）は選択直後に即座に非表示化して次の選択に備えさせるため、
    /// 見た目だけをコピーした軽量なゴースト（Image2枚のみ）をこのクラスで使い回して演出する。
    /// ゴースト・トレイル(残像)ともにプール化し、選択の度にInstantiate/Destroyが発生しないようにする。
    /// </summary>
    public class SkillCardAbsorbFx : MonoBehaviour
    {
        private RectTransform rt;
        private Image background;
        private Image icon;
        private CanvasGroup canvasGroup;

        private static readonly Queue<SkillCardAbsorbFx> pool = new Queue<SkillCardAbsorbFx>();
        private static readonly Queue<Image> trailPool = new Queue<Image>();
        private static Transform trailHiddenParent;
        private static bool sceneHookRegistered;
        private static FxRunner runner;

        // ★トレイルのフェードコルーチンをゴースト自身に持たせると、ゴーストが先にReturn()され
        //   非アクティブ化された時に道連れで止まり、トレイルだけ消えずに残ってしまう。
        //   常時アクティブな専用ランナーに持たせることで、ゴーストのライフサイクルと切り離す。
        private class FxRunner : MonoBehaviour { }

        private static FxRunner EnsureRunner()
        {
            if (runner != null) return runner;
            GameObject go = new GameObject("SkillCardAbsorbFxRunner");
            runner = go.AddComponent<FxRunner>();
            return runner;
        }

        private static void EnsureSceneHook()
        {
            if (sceneHookRegistered) return;
            sceneHookRegistered = true;
            SceneManager.sceneUnloaded += _ =>
            {
                pool.Clear();
                trailPool.Clear();
                trailHiddenParent = null;
                runner = null;
            };
        }

        public static SkillCardAbsorbFx Rent(Transform parent)
        {
            EnsureSceneHook();
            SkillCardAbsorbFx fx = pool.Count > 0 ? pool.Dequeue() : CreateNew();
            fx.transform.SetParent(parent, false);
            fx.transform.SetAsLastSibling();
            fx.gameObject.SetActive(true);
            return fx;
        }

        private static SkillCardAbsorbFx CreateNew()
        {
            GameObject go = new GameObject("SkillCardAbsorbFx", typeof(RectTransform));
            SkillCardAbsorbFx fx = go.AddComponent<SkillCardAbsorbFx>();
            fx.Build();
            return fx;
        }

        private void Build()
        {
            rt = (RectTransform)transform;
            background = gameObject.AddComponent<Image>();
            background.raycastTarget = false;
            background.preserveAspect = false;

            GameObject iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(transform, false);
            RectTransform iconRt = (RectTransform)iconGo.transform;
            iconRt.anchorMin = new Vector2(0.5f, 0.5f);
            iconRt.anchorMax = new Vector2(0.5f, 0.5f);
            iconRt.pivot = new Vector2(0.5f, 0.5f);
            iconRt.anchoredPosition = Vector2.zero;

            icon = iconGo.AddComponent<Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = true;

            canvasGroup = gameObject.AddComponent<CanvasGroup>();
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }

        /// <summary>見た目・初期ワールド位置・サイズを設定してから再生する</summary>
        public void Setup(Sprite bgSprite, Color bgColor, Sprite iconSprite, Vector2 size, Vector3 worldPosition)
        {
            rt.sizeDelta = size;
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;
            rt.position = worldPosition;

            background.sprite = bgSprite;
            background.color = bgColor;
            background.enabled = bgSprite != null;

            icon.sprite = iconSprite;
            icon.enabled = iconSprite != null;
            ((RectTransform)icon.transform).sizeDelta = size * 0.72f;

            canvasGroup.alpha = 1f;
        }

        private static void Return(SkillCardAbsorbFx fx)
        {
            fx.gameObject.SetActive(false);
            pool.Enqueue(fx);
        }

        /// <summary>選ばれたカード用：回転しながら縮小・フェードし、targetWorldPosへ吸い込まれる</summary>
        public IEnumerator PlayAbsorb(Vector3 targetWorldPos, float duration, float rotations, float trailInterval, System.Action onArrive)
        {
            Vector3 startPos = rt.position;
            Vector3 startScale = rt.localScale;
            float elapsed = 0f;
            float nextTrailTime = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // 後半にいくほど加速して吸い込まれるようにイーズインを使う
                float moveT = t * t;

                rt.position = Vector3.Lerp(startPos, targetWorldPos, moveT);
                rt.localScale = Vector3.Lerp(startScale, Vector3.zero, t);
                rt.localRotation = Quaternion.Euler(0f, 0f, -rotations * 360f * t);
                canvasGroup.alpha = 1f - t * t; // 最後まで比較的見えるようにt^2でフェード

                if (trailInterval > 0f && elapsed >= nextTrailTime)
                {
                    nextTrailTime += trailInterval;
                    SpawnTrailDot();
                }
                yield return null;
            }

            onArrive?.Invoke();
            Return(this);
        }

        /// <summary>選ばれなかったカード用：その場で軽く縮小しながら素早くフェードアウトする</summary>
        public IEnumerator PlayFadeOut(float duration)
        {
            float elapsed = 0f;
            Vector3 startScale = rt.localScale;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                canvasGroup.alpha = 1f - t;
                rt.localScale = Vector3.Lerp(startScale, startScale * 0.85f, t);
                yield return null;
            }
            Return(this);
        }

        // ======================================================
        // Trail（残像）：アイコンだけの軽量なImageを使い回す
        // ======================================================

        private void SpawnTrailDot()
        {
            Image dot = trailPool.Count > 0 ? trailPool.Dequeue() : CreateTrailDot();
            dot.transform.SetParent(transform.parent, false);
            dot.transform.SetSiblingIndex(transform.GetSiblingIndex()); // ゴースト本体より後ろに描画
            RectTransform dotRt = (RectTransform)dot.transform;
            dotRt.position = rt.position;
            dotRt.sizeDelta = rt.sizeDelta * 0.6f;
            dotRt.localRotation = rt.localRotation;
            dot.sprite = icon.sprite;
            dot.color = new Color(1f, 1f, 1f, 0.45f);
            dot.gameObject.SetActive(true);
            EnsureRunner().StartCoroutine(FadeTrailDot(dot));
        }

        private static Image CreateTrailDot()
        {
            GameObject go = new GameObject("SkillCardTrailDot", typeof(RectTransform));
            Image img = go.AddComponent<Image>();
            img.raycastTarget = false;
            img.preserveAspect = true;
            return img;
        }

        private IEnumerator FadeTrailDot(Image dot)
        {
            const float duration = 0.22f;
            float elapsed = 0f;
            Color c = dot.color;
            RectTransform dotRt = (RectTransform)dot.transform;
            while (elapsed < duration && dot != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = elapsed / duration;
                dot.color = new Color(c.r, c.g, c.b, c.a * (1f - t));
                dotRt.localScale = Vector3.one * Mathf.Lerp(1f, 0.6f, t);
                yield return null;
            }
            if (dot != null)
            {
                dot.gameObject.SetActive(false);
                dotRt.localScale = Vector3.one;
                if (trailHiddenParent == null)
                {
                    var go = new GameObject("SkillCardTrailDotPoolParent");
                    go.SetActive(false);
                    Object.DontDestroyOnLoad(go);
                    trailHiddenParent = go.transform;
                }
                dot.transform.SetParent(trailHiddenParent, false);
                trailPool.Enqueue(dot);
            }
        }
    }
}
