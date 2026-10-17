using System.Collections.Generic;
using UnityEngine;

namespace Dreamcore
{
    /// <summary>
    /// 找异常 loop（8番出口式简化）：
    /// 玩家在机位序列上前进；每轮当前机位以 anomalyChance 概率激活一个异常。
    /// 判定：画面有异常 → 按「回头」正确；无异常 → 按「前进」正确。
    /// 正确则深度 +1，到达 maxDepth → 好结局；错误累计 maxMistakes → 坏结局。
    /// </summary>
    public class LoopController : MonoBehaviour
    {
        [Header("Tuning")]
        public int maxDepth = 8;
        public int maxMistakes = 3;
        [Range(0f, 1f)] public float anomalyChance = 0.55f;

        public int Depth { get; private set; }
        public int Mistakes { get; private set; }
        public Spot CurrentSpot { get; private set; }
        public int CurrentAnomalyIndex { get; private set; } = -1; // -1 = 无异常

        private List<Spot> spots;
        private int spotIndex;

        public void Begin(SpotsCatalog catalog)
        {
            spots = (catalog != null && catalog.spots.Count > 0) ? catalog.spots : null;
            if (spots == null)
            {
                Debug.LogError("SpotsCatalog 为空！请把图放进 Assets/Content/Spots/ 并跑 Bootstrap 生成。");
                return;
            }
            Depth = 1;
            Mistakes = 0;
            spotIndex = Random.Range(0, spots.Count); // 随机起点
            ShowCurrent();
        }

        void ShowCurrent()
        {
            CurrentSpot = spots[spotIndex % spots.Count];
            CurrentAnomalyIndex = -1;
            if (Random.value < anomalyChance && CurrentSpot.anomalies.Count > 0)
                CurrentAnomalyIndex = Random.Range(0, CurrentSpot.anomalies.Count);

            if (GameManager.Instance != null && GameManager.Instance.ui != null)
                GameManager.Instance.ui.ShowSpot(CurrentSpot, CurrentAnomalyIndex);
        }

        /// <summary>玩家报告：goForward=true 前进 / false 回头。</summary>
        public void Report(bool goForward)
        {
            if (GameManager.Instance.State != GameState.Playing) return;

            bool hasAnomaly = CurrentAnomalyIndex >= 0;
            bool correct = hasAnomaly ? !goForward : goForward;

            var gm = GameManager.Instance;
            if (correct)
            {
                if (gm.audio != null) gm.audio.PlayFootstep();
                Depth++;
                if (Depth >= maxDepth) { GameManager.Instance.OnGameOver(true); return; }
            }
            else
            {
                if (gm.audio != null) gm.audio.PlayAnomalyCue();
                Mistakes++;
                if (Mistakes >= maxMistakes) { GameManager.Instance.OnGameOver(false); return; }
            }

            spotIndex = (spotIndex + 1) % spots.Count;
            ShowCurrent();
        }
    }
}
