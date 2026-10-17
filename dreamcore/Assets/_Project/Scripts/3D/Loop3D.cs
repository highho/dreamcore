// dreamcore/Assets/_Project/Scripts/3D/Loop3D.cs
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Dreamcore.D3
{
    /// <summary>
    /// 目录驱动的 3D 找异常循环。取代原来的 Random.value 掷骰子：
    /// depth 1 强制干净（先给玩家"正常"的记忆锚），之后先掷 cleanChance 判干净层、
    /// 未命中才从未用过的异常中等概率抽一个。顺序写死，勿调（spec §4.2）。
    /// 判定：走到尽头=前进，走回起点=回头；有异常时回头为对（与 2D 版字面一致）。
    /// 异常表现 = AnomalyEntry.Apply/Revert 的 SetActive 开合，本类完全不知道物件来自
    /// 灰盒图元还是 FBX —— 装配由 Bootstrap3D（Task 5）经 AnomalyResolver 完成。
    /// 结语文案的唯一真相在 <see cref="Hud3D"/>（它订阅 <see cref="OnEnd"/> 自己写 endText），
    /// 本类只发事件、不碰任何文本，否则 Task 10 定稿只会改到其中一份副本。
    /// </summary>
    public class Loop3D : MonoBehaviour
    {
        public int maxDepth = 8;
        public int maxMistakes = 3;
        [Range(0f, 1f)] public float cleanChance = 0.35f;

        // 归位演出参数（只在实际 Play 模式里走协程；编辑器/batchmode 一律同步归位，验收才确定性）
        public int maxResetFlashes = 3;      // 归位前闪几次
        public float settleSeconds = 0.6f;   // 整段归位演出的时长
        public float resetDelay = 0.25f;     // 归位后再留多久收尾
        public float timeScale = 1f;         // Begin/ResetLoop 要恢复到的 Time.timeScale

        /// <summary>本局的异常目录（由 Bootstrap3D 装配时整表赋值，或由 Setup 装填）。</summary>
        public List<AnomalyEntry> anomalies = new List<AnomalyEntry>();

        public int Depth { get; private set; } = 1;
        public int Mistakes { get; private set; }
        public AnomalyEntry Current { get; private set; }
        public bool HasAnomaly { get { return Current != null; } }

        public event Action<int, int> OnHudUpdate; // depth, mistakes
        public event Action<bool> OnEnd;           // win

        enum Phase { Normal, Settling, Ending }

        Phase state = Phase.Normal;
        Transform root;
        System.Random rng;
        readonly List<AnomalyEntry> unused = new List<AnomalyEntry>();
        bool ended;

        /// <summary>
        /// 装配入口：把解析好的目录装进本循环，并把全场钉成"正常"（show 全关、hide 目标全开）。
        /// 空目录或空场景根一律抛 ArgumentNullException（不给"空装配静默通过"留活路）。
        /// 幂等：重复调用同一目录结果一致（每次从空表重建，绝不追加），可安全重跑 Bootstrap3D。
        /// </summary>
        public void Setup(List<AnomalyEntry> catalog, Transform sceneRoot)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (sceneRoot == null) throw new ArgumentNullException(nameof(sceneRoot));

            root = sceneRoot;
            anomalies = new List<AnomalyEntry>();
            foreach (var e in catalog)
                if (e != null && e.show.Count + e.hide.Count > 0) anomalies.Add(e); // 丢掉 show/hide 皆空的幻影条目

            enabled = true;
            Time.timeScale = timeScale;
            state = Phase.Normal;
            ended = false;
            Depth = 1;
            Mistakes = 0;
            RevertCurrent();
            SettleAll();
            RefillUnused();

            Debug.Log("[Loop3D] Setup：根=" + root.name + " 目录收到 " + catalog.Count
                      + " 条、可用 " + anomalies.Count + " 条，全场已钉成正常态");
            OnHudUpdate?.Invoke(Depth, Mistakes);
        }

        /// <summary>可用异常条数（Setup 已过滤空条目，Task 5 的门按这个数断言）。</summary>
        public int CountAnomalies()
        {
            return anomalies != null ? anomalies.Count : 0;
        }

        /// <summary>目录 id 列表（装配顺序，供验收口逐字比对；不参与玩法）。</summary>
        public List<string> GetAnomalyIdsForTest()
        {
            var ids = new List<string>();
            if (anomalies != null)
                foreach (var e in anomalies)
                    if (e != null) ids.Add(e.id);
            return ids;
        }

        /// <summary>状态机相位名（Normal/Settling/Ending），给验收口与调试 HUD 用。</summary>
        public string GetState()
        {
            return state.ToString();
        }

        /// <summary>开一局：seed 有值则可复现（验收工装与"同 seed 同表现"的确定性口径）。</summary>
        public void Begin(int? seed = null)
        {
            rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
            ended = false;
            enabled = true;
            Time.timeScale = timeScale;
            state = Phase.Normal;
            Depth = 1;
            Mistakes = 0;
            RevertCurrent();
            SettleAll();      // 新一局从"正常"起步，绝不带上一局的残骸
            RefillUnused();
            Roll();
        }

        /// <summary>goForward=true 走到尽头（前进判定）/ false 回到起点（回头判定）。</summary>
        public void Report(bool goForward)
        {
            if (ended) return;    // 结局已定，后续触发门一律无效

            bool correct = HasAnomaly ? !goForward : goForward;
            if (correct)
            {
                Depth++;
                if (Depth >= maxDepth) { Finish(true); return; }
            }
            else
            {
                Mistakes++;
                if (Mistakes >= maxMistakes) { Finish(false); return; }
            }
            Roll();
        }

        /// <summary>把循环收回"正常"并允许重开：表现归位、计数归零、目录重新可用。</summary>
        public void ResetLoop()
        {
            enabled = true;
            Time.timeScale = timeScale;
            if (Application.isPlaying && Current != null && maxResetFlashes > 0 && settleSeconds > 0f)
                StartCoroutine(SettleRoutine());
            else
                SettleAllNow();
            Depth = 1;
            Mistakes = 0;
            ended = false;
            RefillUnused();
            OnHudUpdate?.Invoke(Depth, Mistakes);
        }

        /// <summary>同步归位（验收口用；表现层协程的最后一步也是它）。</summary>
        public void SettleNowForTest()
        {
            SettleAllNow();
        }

        void Roll()
        {
            RevertCurrent();
            if (rng == null) rng = new System.Random(); // 没 Begin 就直接 Report 的兜底；Begin 之后不改变任何序列

            if (Depth == 1)
            {
                Current = null;                       // 记忆锚，绝不出异常
            }
            else if (rng.NextDouble() < cleanChance || unused.Count == 0)
            {
                Current = null;                       // 干净层，或目录已抽干
            }
            else
            {
                int i = rng.Next(unused.Count);
                Current = unused[i];
                unused.RemoveAt(i);                   // 本局不重复
                Current.Apply();
            }

            OnHudUpdate?.Invoke(Depth, Mistakes);
        }

        void RevertCurrent()
        {
            if (Current != null) Current.Revert();
            Current = null;
        }

        void SettleAll()
        {
            if (anomalies == null) return;
            foreach (var e in anomalies)
                if (e != null) e.SettleToNormal();
        }

        void SettleAllNow()
        {
            RevertCurrent();
            SettleAll();
            state = Phase.Normal;
        }

        void RefillUnused()
        {
            unused.Clear();
            if (anomalies != null) unused.AddRange(anomalies);
        }

        /// <summary>归位演出：闪 maxResetFlashes 下再逐字还原。只有真 Play 模式走这条。</summary>
        IEnumerator SettleRoutine()
        {
            state = Phase.Settling;
            var cur = Current;
            float per = settleSeconds / Mathf.Max(1, maxResetFlashes * 2);
            for (int i = 0; i < maxResetFlashes; i++)
            {
                if (cur != null) cur.Revert();
                yield return new WaitForSeconds(per);
                if (cur != null) cur.Apply();
                yield return new WaitForSeconds(per);
            }
            if (resetDelay > 0f) yield return new WaitForSeconds(resetDelay);
            SettleAllNow();
        }

        void Finish(bool win)
        {
            ended = true;
            RevertCurrent();
            OnHudUpdate?.Invoke(Depth, Mistakes);
            ShowEnding(win);
        }

        /// <summary>
        /// 收尾只做状态机的事：钳住记录数、进 Ending、停掉判定与时间。
        /// 结语文案一个字都不写在这里——那是 Hud3D 的既有职责（见类注释）。
        /// </summary>
        void ShowEnding(bool win)
        {
            Mistakes = Mathf.Min(Mistakes, maxMistakes);
            state = Phase.Ending;
            enabled = false;
            Time.timeScale = 0f;
            OnEnd?.Invoke(win);
        }
    }
}
