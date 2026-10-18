// dreamcore/Assets/_Project/Scripts/3D/Loop3D.cs
using System;
using System.Collections.Generic;
using UnityEngine;
using Dreamcore;

namespace Dreamcore.D3
{
    /// <summary>
    /// 目录驱动的 3D 找异常循环。取代原来的 Random.value 掷骰子：
    /// depth 1 强制干净（先给玩家"正常"的记忆锚），之后先掷 cleanChance 判干净层、
    /// 未命中才从未用过的异常中等概率抽一个。顺序写死，勿调。
    /// 判定：走到尽头=前进，走回起点=回头；有异常时回头为对（与 2D 版字面一致）。
    /// </summary>
    public class Loop3D : MonoBehaviour
    {
        public int maxDepth = 8;
        public int maxMistakes = 3;
        [Range(0f, 1f)] public float cleanChance = 0.35f;

        public List<AnomalyEntry> anomalies = new List<AnomalyEntry>();

        [Header("音效（Bootstrap3D 赋值；验收夹具里为 null 即静音跑）")]
        public AudioClip anomalyCue;   // 异常落地的那一记低鸣，给"这层不对劲"一个听觉锚
        public AudioClip decisionClip; // 玩家在判定门上做出选择＝磁带走带声

        public int Depth { get; private set; } = 1;
        public int Mistakes { get; private set; }
        public AnomalyEntry Current { get; private set; }
        public bool HasAnomaly { get { return Current != null; } }

        public event Action<int, int> OnHudUpdate; // depth, mistakes
        public event Action<bool> OnEnd;           // win

        System.Random rng;
        readonly List<AnomalyEntry> unused = new List<AnomalyEntry>();
        bool ended;

        public void Begin(int? seed = null)
        {
            rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
            ended = false;
            Depth = 1;
            Mistakes = 0;
            unused.Clear();
            if (anomalies != null) unused.AddRange(anomalies);
            Roll();
        }

        /// <summary>goForward=true 走到尽头（前进判定）/ false 回到起点（回头判定）。</summary>
        public void Report(bool goForward)
        {
            if (ended) return;

            if (decisionClip != null) AudioSource.PlayClipAtPoint(decisionClip, SoundPos());

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

        void Roll()
        {
            RevertCurrent();

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
                if (anomalyCue != null) AudioSource.PlayClipAtPoint(anomalyCue, SoundPos());
            }

            OnHudUpdate?.Invoke(Depth, Mistakes);
        }

        /// <summary>声源放在玩家耳朵上（PlayClipAtPoint 要 3D 位置）。验收夹具的临时场景没有主相机，退回原点。</summary>
        Vector3 SoundPos()
        {
            var cam = Camera.main;
            return cam != null ? cam.transform.position : Vector3.zero;
        }

        void RevertCurrent()
        {
            if (Current != null) Current.Revert();
            Current = null;
        }

        void Finish(bool win)
        {
            ended = true;
            RevertCurrent();
            OnHudUpdate?.Invoke(Depth, Mistakes);
            OnEnd?.Invoke(win);
        }
    }
}
