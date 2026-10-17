using UnityEngine;

namespace Dreamcore.D3
{
    /// <summary>
    /// 3D 版灯闪：暖色点光强度低频波动 + 偶发跳变（电压不稳的楼道灯）。
    /// isDead=true 时熄灭（只剩微弱余辉）——"灯灭了"是梦核最经典的异常。
    /// </summary>
    public class LightFlicker : MonoBehaviour
    {
        public bool isDead = false;
        [Range(0.1f, 5f)] public float baseIntensity = 1.8f;
        public float flickerSpeed = 1.7f;
        [Range(0f, 1f)] public float flickerAmp = 0.25f;
        public float deadGlowIntensity = 0.04f;

        Light lt;
        float seed;

        void Awake()
        {
            lt = GetComponent<Light>();
            seed = Random.value * 100f;
        }

        void Update()
        {
            if (lt == null) return;
            float target;
            if (isDead)
            {
                target = deadGlowIntensity;
            }
            else
            {
                float n = Mathf.PerlinNoise(Time.time * flickerSpeed, seed);
                float spike = Random.value < 0.02f ? -0.6f : 0f;
                target = Mathf.Max(0.05f, baseIntensity * (1f - n * flickerAmp) + spike * baseIntensity);
            }
            lt.intensity = Mathf.Lerp(lt.intensity, target, Time.deltaTime * 6f);
        }
    }
}
