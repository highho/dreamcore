using UnityEngine;
using UnityEngine.UI;

namespace Dreamcore
{
    /// <summary>
    /// 全屏黑罩低频亮度波动：模拟电压不稳的日光灯/监控画面。
    /// 挂在 BlackOverlay 物体上（Image 组件，纯黑，alpha 由本脚本驱动）。
    /// </summary>
    public class FlickerController : MonoBehaviour
    {
        public Image blackOverlay;
        [Range(0f, 0.2f)] public float baseDark = 0.05f;
        [Range(0f, 0.2f)] public float flickerAmp = 0.06f;
        public float flickerSpeed = 1.7f;

        float seed;

        void Awake()
        {
            seed = Random.value * 100f;
            if (blackOverlay == null) blackOverlay = GetComponent<Image>();
        }

        void Update()
        {
            if (blackOverlay == null) return;
            float n = Mathf.PerlinNoise(Time.time * flickerSpeed, seed);
            float spike = Random.value < 0.02f ? Random.value * 0.1f : 0f; // 偶发跳变（灯闪）
            float a = baseDark + n * flickerAmp + spike;
            Color c = blackOverlay.color;
            c.a = Mathf.Clamp01(a);
            blackOverlay.color = c;
        }
    }
}
