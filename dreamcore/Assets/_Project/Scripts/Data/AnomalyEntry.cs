// dreamcore/Assets/_Project/Scripts/Data/AnomalyEntry.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dreamcore
{
    /// <summary>
    /// 一个异常 = 一组物件的开合。不关心物件来自 FBX 还是灰盒，也不关心它是灯还是墙。
    /// show 里的物件平时关闭，hide 里的物件平时打开；Revert 必须逐字还原。
    /// </summary>
    [Serializable]
    public class AnomalyEntry
    {
        public string id;
        public string title;
        public List<GameObject> show = new List<GameObject>();
        public List<GameObject> hide = new List<GameObject>();

        public void Apply()
        {
            foreach (var g in show) if (g != null) g.SetActive(true);
            foreach (var g in hide) if (g != null) g.SetActive(false);
        }

        public void Revert()
        {
            foreach (var g in show) if (g != null) g.SetActive(false);
            foreach (var g in hide) if (g != null) g.SetActive(true);
        }

        /// <summary>把初始状态钉成"正常"：show 全关、hide 全开。Bootstrap 装配后调用一次。</summary>
        public void SettleToNormal()
        {
            foreach (var g in show) if (g != null) g.SetActive(false);
            foreach (var g in hide) if (g != null) g.SetActive(true);
        }
    }

    /// <summary>异常标题表（spec §4.3 的逐字对应，用于日志与调试 HUD）。</summary>
    public static class AnomalyTitles
    {
        public static readonly Dictionary<string, string> Table = new Dictionary<string, string>
        {
            { "anom01", "声控灯全灭，只剩绿色应急光" },
            { "anom02", "走廊尽头多出背对镜头的人影" },
            { "anom03", "一户防盗门半开" },
            { "anom04", "墙上开锁疏通小广告整排消失" },
            { "anom05", "地上多出一双小孩旧布鞋" },
            { "anom06", "靠墙的旧自行车倒了" },
            { "anom07", "信报箱小门全部敞开" },
            { "anom08", "门缝下渗出一滩水" },
            { "anom09", "天花板垂下一段红电线" },
            { "anom10", "窗外该有楼的地方全黑" },
        };
    }
}
