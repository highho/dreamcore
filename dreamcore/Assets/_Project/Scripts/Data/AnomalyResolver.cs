// dreamcore/Assets/_Project/Scripts/Data/AnomalyResolver.cs
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Dreamcore
{
    /// <summary>
    /// 按"前缀式 wire 契约"（spec §9.1/§9.2 实测裁定）把场景根解析成异常表。约定：
    ///   floor / wallL / Door_3 / Light_2 …        base 恒定本体，裸名
    ///   anom&lt;NN&gt;_show_&lt;leaf&gt;                   异常激活时出现（anom01_show_shadeFigure）
    ///   anom&lt;NN&gt;_hide_&lt;base目标名&gt;_off        异常激活时消失（anom01_hide_Light_2_off → Light_2）
    /// Blender 集合只是编排侧的容器，实测从不进 FBX（§9.1），故 wire 只活在对象名里；
    /// 因此这里递归遍历整棵树按名分桶，不依赖任何 show/hide 节点。
    /// hide 的代理自己永不显示，只负责指认目标；指认不到、或缺 _off 后缀，
    /// 就打警告并丢弃该条（不静默）。解析失败的 wire 名同样打警告后忽略，不抛异常。
    /// 命名约定只有本文件知道——Task 7/8 一律调 IsAnomalyWireName，不各自复制正则。
    /// </summary>
    public static class AnomalyResolver
    {
        private const string IdPrefix = "anom";
        private const string ShowBucket = "show";
        private const string HideBucket = "hide";
        private const string HideSuffix = "_off";
        private const string NoTitle = "（无标题）";

        // wire 语法：anom<两位数字>_(show|hide)_<叶名>。Task 6 的 Blender 脚本按这一处对齐。
        private static readonly Regex WireGrammar =
            new Regex(@"^anom(\d{2})_(show|hide)_(.+)$");

        /// <summary>
        /// 场景里是否出现过 "anom&lt;数字&gt;…" 形态的名字。用它把疑似 wire 名
        /// （但解析失败的，如 anom1_show_x）挑出来打警告；真正的分桶判定用 WireGrammar。
        /// </summary>
        private static bool LooksLikeWireName(string name)
        {
            return name.StartsWith(IdPrefix)
                   && name.Length > IdPrefix.Length
                   && char.IsDigit(name[IdPrefix.Length]);
        }

        /// <summary>
        /// 该名字是否为合法的异常 wire 物件（两个桶都算）。
        /// Task 7/8 用它把 base 网格和异常网格区分开。
        /// </summary>
        public static bool IsAnomalyWireName(string name)
        {
            return name != null && WireGrammar.IsMatch(name);
        }

        public static List<AnomalyEntry> Resolve(Transform sceneRoot)
        {
            var result = new List<AnomalyEntry>();
            if (sceneRoot == null)
            {
                Debug.LogWarning("[Resolver] 场景根为空，异常表为空。");
                return result;
            }

            // base 里的全部物体，供 hide 代理按名指认
            var byName = new Dictionary<string, GameObject>();
            Collect(sceneRoot, byName);

            var wireObjects = new List<Transform>();
            CollectWire(sceneRoot, wireObjects);
            if (wireObjects.Count == 0)
            {
                Debug.LogWarning("[Resolver] 场景里没有任何 anom<NN>_ 前缀物件，异常表为空。");
                return result;
            }

            var byId = new Dictionary<string, AnomalyEntry>();

            foreach (var t in wireObjects)
            {
                var match = WireGrammar.Match(t.name);
                if (!match.Success)
                {
                    // 疑似 wire 却解析失败（位数不对 / bucket 不是 show|hide）：不静默。
                    Debug.LogWarning("[Resolver] wire 名解析失败，忽略: " + t.name);
                    continue;
                }

                string nn = match.Groups[1].Value;      // "01"
                string id = IdPrefix + nn;               // "anom01"
                string bucket = match.Groups[2].Value;   // show | hide
                string leaf = match.Groups[3].Value;

                AnomalyEntry entry;
                if (!byId.TryGetValue(id, out entry))
                {
                    entry = new AnomalyEntry
                    {
                        id = id,
                        title = AnomalyTitles.Table.ContainsKey(id)
                            ? AnomalyTitles.Table[id] : nn + NoTitle,
                    };
                    byId.Add(id, entry);
                }

                if (bucket == ShowBucket)
                {
                    entry.show.Add(t.gameObject);
                    continue;
                }

                // hide 桶
                t.gameObject.SetActive(false); // 代理自己不参与显示
                if (!leaf.EndsWith(HideSuffix))
                {
                    Debug.LogWarning("[Resolver] hide 代理缺 _off 后缀，忽略: " + t.name);
                    continue;
                }
                string targetName = leaf.Substring(0, leaf.Length - HideSuffix.Length);
                GameObject target;
                if (string.IsNullOrEmpty(targetName)
                    || !byName.TryGetValue(targetName, out target)
                    || target == t.gameObject)
                {
                    Debug.LogWarning("[Resolver] hide 指认不到 base 目标: " + targetName
                                     + "（来自 " + t.name + "）");
                    continue;
                }
                entry.hide.Add(target);
            }

            foreach (var entry in byId.Values) result.Add(entry);
            result.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            return result;
        }

        private static void Collect(Transform t, Dictionary<string, GameObject> byName)
        {
            if (!byName.ContainsKey(t.name)) byName.Add(t.name, t.gameObject);
            for (int i = 0; i < t.childCount; i++) Collect(t.GetChild(i), byName);
        }

        /// <summary>
        /// 递归收集所有 "anom&lt;数字&gt;…" 形态的物件（含解析失败的），顺序为深度优先。
        /// FBX 层级可能任意嵌套且无保证存在的 anom/show/hide 节点，所以只能全树扫名。
        /// </summary>
        private static void CollectWire(Transform t, List<Transform> wireObjects)
        {
            if (LooksLikeWireName(t.name)) wireObjects.Add(t);
            for (int i = 0; i < t.childCount; i++) CollectWire(t.GetChild(i), wireObjects);
        }
    }
}
