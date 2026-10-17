// dreamcore/Assets/_Project/Editor/Verify3D.cs
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Dreamcore.EditorTools
{
    /// <summary>
    /// 自验收口（batchmode -executeMethod）。约定：日志只认 [VERIFY] PASS / [VERIFY] FAIL。
    /// 契约形态：主控裁定的"前缀式 wire 契约"（Blender 集合只作编排、进不了 FBX，
    /// 异常语义由对象名前缀语法承载）。本方法是这条 wire 语法过 FBX 通道的探针，
    /// 不是成品解析器 —— 唯一真相在 Task 3 的 AnomalyResolver。
    /// 坏样本开关：环境变量 VERIFY_BAD=1 时，往期望集塞一个绝不存在的 wire 名，
    /// 用来证明这个门真的会红 —— 不被喂坏样本之前，它的 PASS 不算背书。
    /// </summary>
    public static class Verify3D
    {
        const string ProbePath = "Assets/Content/3D/Probe.fbx";

        // 前缀式语法：anom<NN>_(show|hide)_<leaf>
        static readonly Regex WireGrammar = new Regex(@"^anom(\d{2})_(show|hide)_(.+)$");

        // 世界尺寸容差：探针立方体在 Blender 侧为 1m，故每个 mesh 节点世界尺度应落在 0.9–1.1。
        // 100 倍 / 0.01 倍 的预除缩放必须把这里判红（正是 Empty 包裹反例暴露的坑）。
        const float SizeLo = 0.9f;
        const float SizeHi = 1.1f;

        public static void ProbeHierarchy()
        {
            var failures = new List<string>();

            // base 桶：裸名，且不得匹配 wire 前缀语法
            var bareExpected = new List<string> { "wallL", "floor" };
            // 前缀式 wire 名：证明语法对多个前缀、多桶成员都能过通道
            var wireExpected = new List<string>
            {
                "anom01_show_shadeFigure",
                "anom01_show_probeCubeB",
                "anom01_hide_floor_off",
            };
            if (Environment.GetEnvironmentVariable("VERIFY_BAD") == "1")
                wireExpected.Add("anom99_show_no_such_object_xyz"); // 绝不存在，逼门变红

            var expected = new List<string>();
            expected.AddRange(bareExpected);
            expected.AddRange(wireExpected);

            AssetDatabase.ImportAsset(ProbePath, ImportAssetOptions.ForceSynchronousImport);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProbePath);
            if (prefab == null)
            {
                Debug.LogError("[VERIFY] FAIL: 无法加载 " + ProbePath);
                return;
            }

            var paths = new List<string>();
            var objectNames = new HashSet<string>();
            var meshNodes = new Dictionary<string, Transform>();
            var sb = new StringBuilder();
            Walk(prefab.transform, "", paths, objectNames, meshNodes, sb);
            Debug.Log("[VERIFY] ---- 层级实测 ----\n" + sb);

            // (1) 每个期望名（裸名 + wire 名）必须逐字节到齐
            foreach (var name in expected)
                if (!objectNames.Contains(name)) failures.Add("缺失 " + name);

            // (2) base 桶正面检查：裸名在位，且不得被前缀语法误伤
            foreach (var bare in bareExpected)
                if (objectNames.Contains(bare) && WireGrammar.IsMatch(bare))
                    failures.Add("base 裸名不应匹配 wire 前缀语法: " + bare);

            // (3) wire 名往返：解析 id/bucket/leaf；hide 的 leaf 去掉 _off 必须解析到已存在物件
            int roundTrip = 0;
            foreach (var name in objectNames)
            {
                var mt = WireGrammar.Match(name);
                if (!mt.Success) continue;
                roundTrip++;
                string id = mt.Groups[1].Value;
                string bucket = mt.Groups[2].Value;
                string leaf = mt.Groups[3].Value;
                if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(leaf))
                {
                    failures.Add("wire 名解析出空段: " + name);
                    continue;
                }
                if (bucket == "hide")
                {
                    if (!leaf.EndsWith("_off"))
                        failures.Add("hide 叶名缺 _off 后缀: " + name);
                    else
                    {
                        string target = leaf.Substring(0, leaf.Length - "_off".Length);
                        if (string.IsNullOrEmpty(target) || !objectNames.Contains(target))
                            failures.Add("hide 目标解析失败: " + target + "（来自 " + name + "）");
                    }
                }
                else if (bucket != "show")
                {
                    failures.Add("未知 bucket: " + bucket + "（来自 " + name + "）");
                }
            }
            Debug.Log("[VERIFY] wire 往返: 解析到 " + roundTrip + " 个前缀名，hide→base 目标映射已核");

            // (4) 世界尺度断言在几何上（不只是两点距离）：每个 mesh 节点打证据 + 断言 1m
            foreach (var kv in meshNodes)
            {
                var t = kv.Value;
                var mf = t.GetComponent<MeshFilter>();
                var mesh = mf != null ? mf.sharedMesh : null;
                if (mesh == null) continue;
                Vector3 worldSize = Vector3.Scale(mesh.bounds.size, t.lossyScale);
                Debug.Log("[VERIFY] 节点 " + kv.Key
                          + " local=" + t.localPosition
                          + " world=" + t.position
                          + " lossyScale=" + t.lossyScale
                          + " boundsSize=" + mesh.bounds.size
                          + " worldSize=" + worldSize
                          + " verts=" + mesh.vertexCount);
                if (!InRange(worldSize.x) || !InRange(worldSize.y) || !InRange(worldSize.z))
                    failures.Add("世界尺寸偏离 1m（疑 100×/0.01× 导入）: " + kv.Key + " worldSize=" + worldSize);
            }
            // root lossyScale ≈ 1（几何尺度的前提；异常包裹会在这里露出 100 倍）
            var rootLossy = prefab.transform.lossyScale;
            Debug.Log("[VERIFY] rootLossyScale=" + rootLossy);
            if (!InRange(rootLossy.x) || !InRange(rootLossy.y) || !InRange(rootLossy.z))
                failures.Add("root lossyScale 偏离 1: " + rootLossy);

            // (5) 轴向事实只记录、不作失败：Blender +Z → Unity +Y，比较 +Y 分离
            Transform tWall = FindByName(prefab.transform, "wallL");
            Transform tFloor = FindByName(prefab.transform, "floor");
            if (tWall != null && tFloor != null)
                Debug.Log("[VERIFY] 轴向 wallL=" + tWall.position + " floor=" + tFloor.position
                          + " +Y分离=" + Mathf.Abs(tFloor.position.y - tWall.position.y)
                          + " 距离=" + Vector3.Distance(tWall.position, tFloor.position)
                          + "（期望 +Y≈10：Blender +Z→Unity +Y，不作门）");

            // (6) 集合层级：路径式契约实测已死（恒 False，见 §9.1），只记录、不作门，
            //     也不留一行会被后续 grep 误读成"尺度错了"的期望读数。
            bool collNodes = false;
            foreach (var p in paths)
                foreach (var seg in p.Split('/'))
                    if (seg == "base" || seg == "anom") collNodes = true;
            Debug.Log("[VERIFY] 集合层级(实测恒 False，已由 §9.1 记录，不作门): " + collNodes);

            if (failures.Count == 0)
                Debug.Log("[VERIFY] PASS: 前缀式 wire 契约成立（名保真 + base 裸名 + wire 往返 + 几何尺度≈1m + rootScale≈1） "
                          + string.Join(",", expected));
            else
                Debug.LogError("[VERIFY] FAIL: " + string.Join(" | ", failures));
        }

        static bool InRange(float v)
        {
            return v >= SizeLo && v <= SizeHi;
        }

        /// <summary>
        /// 只有带网格的节点算"物体"；空节点是 Blender 集合残影。
        /// objectNames 供名保真/往返，meshNodes 供逐节点尺寸断言。
        /// </summary>
        static void Walk(Transform t, string prefix, List<string> paths,
                         HashSet<string> objectNames, Dictionary<string, Transform> meshNodes,
                         StringBuilder sb)
        {
            string path = string.IsNullOrEmpty(prefix) ? t.name : prefix + "/" + t.name;
            paths.Add(path);
            bool isMesh = t.GetComponent<MeshFilter>() != null;
            if (isMesh)
            {
                objectNames.Add(t.name);
                if (!meshNodes.ContainsKey(t.name)) meshNodes.Add(t.name, t);
            }
            sb.AppendLine(path + (isMesh ? "  <mesh>" : "  <node>"));
            for (int i = 0; i < t.childCount; i++)
                Walk(t.GetChild(i), path, paths, objectNames, meshNodes, sb);
        }

        static Transform FindByName(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var r = FindByName(root.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }
    }
}
