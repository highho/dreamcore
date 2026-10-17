using UnityEngine;
using UnityEngine.UI;

namespace Dreamcore.D3
{
    /// <summary>
    /// 3D HUD：Overlay Canvas（不吃后处理，文字保持清晰——场景本身已被 URP 滤镜盖住）。
    /// 显示深度/异常记录 + 结局文字。
    /// </summary>
    public class Hud3D : MonoBehaviour
    {
        public Text depthText;
        public Text mistakesText;
        public Text endText;

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
                    ? "你出来了。\n天快亮了，楼道里还是那股消毒水的味道。"
                    : "你没能出来。\n它还在原地等你，等下一个回来的孩子。";
            };
        }

        void Start()
        {
            if (loop != null) loop.Begin();
        }
    }
}
