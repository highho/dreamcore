using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Dreamcore.D3
{
    /// <summary>
    /// 3D HUD：ScreenSpaceCamera Canvas（spec §8 第 4 项裁定——Overlay 的 UI 不进后处理链，
    /// 梦核滤镜吃不到 HUD，文字像贴在外面上、和走廊两个世界）。
    /// 显示深度/异常记录 + 结局文字 + 开局一次性引导条。
    /// </summary>
    public class Hud3D : MonoBehaviour
    {
        public Text depthText;
        public Text mistakesText;
        public Text endText;
        [Header("引导条（M8）：进场景即显示，holdSeconds 后用 fadeSeconds 淡完再关掉")]
        public Text hintText;
        public CanvasGroup hintGroup;
        public float holdSeconds = 8f;
        public float fadeSeconds = 1.5f;

        Loop3D loop;

        void Awake()
        {
            loop = FindFirstObjectByType<Loop3D>();
            if (loop == null) return;

            loop.OnHudUpdate += (d, m) =>
            {
                if (depthText != null) depthText.text = "地下 " + d + " 层";
                if (mistakesText != null) mistakesText.text = "异常记录 " + m + "/" + loop.maxMistakes;
            };
            loop.OnEnd += (win) =>
            {
                if (endText == null) return;
                endText.gameObject.SetActive(true);
                endText.text = win
                    ? "你出来了。\n楼道的声控灯一盏盏亮起来，全是真的。"
                    : "你还在这儿。\n挂历上圈着的那天，从来没有过。";
            };
        }

        void Start()
        {
            if (loop != null) loop.Begin();
            if (hintText != null && hintGroup != null) StartCoroutine(HintRoutine());
        }

        /// <summary>
        /// 引导只播一次（跟"真正开局"同生命周期，从标题页进来才算一次；层与层之间不重播）。
        /// 淡完 SetActive(false)：Text 每帧都要重建网格，一条不再显示的字没理由留在树上。
        /// </summary>
        IEnumerator HintRoutine()
        {
            yield return new WaitForSeconds(holdSeconds);
            float t = 0f;
            while (t < fadeSeconds)
            {
                t += Time.deltaTime;
                hintGroup.alpha = 1f - t / fadeSeconds;
                yield return null;
            }
            hintGroup.alpha = 0f;
            hintGroup.gameObject.SetActive(false);
        }
    }
}
