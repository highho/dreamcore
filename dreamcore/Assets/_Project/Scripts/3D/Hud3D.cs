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
                    ? "你出来了。\n楼道的声控灯一盏盏亮起来，全是真的。"
                    : "你还在这儿。\n挂历上圈着的那天，从来没有过。";
            };
        }

        void Start()
        {
            if (loop != null) loop.Begin();
        }
    }
}
