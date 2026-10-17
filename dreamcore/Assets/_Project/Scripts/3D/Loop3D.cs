using UnityEngine;

namespace Dreamcore.D3
{
    /// <summary>
    /// 3D 版找异常 loop：判定规则与 2D 版一致（画面有异常→回头=正确；无异常→前进=正确）。
    /// "前进/回头"由走廊两端的 TriggerGate 触发（走到尽头=前进，回到起点=回头）。
    /// 异常表现 = 灯光熄灭 + 随机黑影（组件由 Bootstrap3D 布置并绑定引用）。
    /// </summary>
    public class Loop3D : MonoBehaviour
    {
        public int maxDepth = 8;
        public int maxMistakes = 3;
        [Range(0f, 1f)] public float anomalyChance = 0.55f;

        public LightFlicker[] sceneLights;
        public ShadeFigure[] shadeFigures;

        public int Depth { get; private set; } = 1;
        public int Mistakes { get; private set; }
        public bool HasAnomaly { get; private set; }

        public event System.Action<int, int> OnHudUpdate; // depth, mistakes
        public event System.Action<bool> OnEnd;           // win

        public void Begin()
        {
            Depth = 1;
            Mistakes = 0;
            RollRound();
        }

        void RollRound()
        {
            HasAnomaly = Random.value < anomalyChance;
            ApplyAnomaly(HasAnomaly);
            OnHudUpdate?.Invoke(Depth, Mistakes);
        }

        void ApplyAnomaly(bool on)
        {
            if (sceneLights != null)
                foreach (var l in sceneLights)
                    if (l != null) l.isDead = on;

            if (shadeFigures != null)
                foreach (var s in shadeFigures)
                    if (s != null) s.SetActive(false);

            if (on && shadeFigures != null && shadeFigures.Length > 0)
            {
                int n = Random.Range(1, Mathf.Min(3, shadeFigures.Length + 1));
                for (int i = 0; i < n; i++)
                    shadeFigures[Random.Range(0, shadeFigures.Length)].SetActive(true);
            }
        }

        /// <summary>goForward=true 走到尽头（前进判定）/ false 回到起点（回头判定）。</summary>
        public void Report(bool goForward)
        {
            bool correct = HasAnomaly ? !goForward : goForward;

            if (correct)
            {
                Depth++;
                if (Depth >= maxDepth) { OnEnd?.Invoke(true); return; }
            }
            else
            {
                Mistakes++;
                if (Mistakes >= maxMistakes) { OnEnd?.Invoke(false); return; }
            }
            RollRound();
        }
    }
}
