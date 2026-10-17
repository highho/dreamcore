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
    /// 坏样本开关：环境变量 VERIFY_BAD=1 时把期望/观测翻假，一次跑红上面每一条分支：
    /// ① 往期望集塞一个绝不存在的 wire 名（缺名分支）；② 往观测集塞一个假的 hide 代理，
    /// 它的 `_off` 目标不存在（hide 目标分支）；③ 把世界尺寸容差从 ≈1m 翻成 ≈100m
    /// （尺寸分支）。三条都只由 VERIFY_BAD=1 触发，默认路径绝不使用这些假期望。
    /// 不被喂坏样本、每条分支都亲眼红过之前，这个门的 PASS 不算背书。
    /// </summary>
    public static class Verify3D
    {
        const string ProbePath = "Assets/Content/3D/Probe.fbx";

        // 前缀式语法：anom<NN>_(show|hide)_<leaf>
        static readonly Regex WireGrammar = new Regex(@"^anom(\d{2})_(show|hide)_(.+)$");

        static readonly bool BadSample = Environment.GetEnvironmentVariable("VERIFY_BAD") == "1";

        // 世界尺寸容差：探针立方体在 Blender 侧为 1m，故每个 mesh 节点世界尺度应落在 0.9–1.1。
        // 100 倍 / 0.01 倍 的预除缩放必须把这里判红。
        // 注意：实测 lossyScale 恒为 100（cm→m 通道缩放，见 spec §9.2），真正作门的是净 worldSize；
        // 尺寸容差被翻成 90–110 只发生在 VERIFY_BAD=1 的坏样本跑，用来证明这条分支真的有牙。
        const float GoodSizeLo = 0.9f;
        const float GoodSizeHi = 1.1f;
        static readonly float SizeLo = BadSample ? 90f : GoodSizeLo;
        static readonly float SizeHi = BadSample ? 110f : GoodSizeHi;

        // 坏样本模式下注入"观测集"的假 hide 代理：去掉 _off 后的目标 nosuch 在 FBX 里绝不存在。
        static readonly string[] BadHideProxies = BadSample
            ? new[] { "anom01_hide_nosuch_off" }
            : new string[0];

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
            if (BadSample)
                wireExpected.Add("anom99_show_no_such_object_xyz"); // 绝不存在，逼缺名分支变红

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

            // 坏样本注入点：假 hide 代理只进"观测集"，让 hide→目标 这条分支有一条真实红过的记录。
            // meshNodes 不受影响，所以尺寸断言读到的仍是 FBX 的真实几何（配 90–110 的假容差必然判红）。
            if (BadSample)
            {
                Debug.Log("[VERIFY] ---- 坏样本注入（仅 VERIFY_BAD=1）---- 假 hide 代理 "
                          + string.Join(",", BadHideProxies)
                          + " 尺寸容差翻成 [" + SizeLo + "," + SizeHi + "]（默认为 ["
                          + GoodSizeLo + "," + GoodSizeHi + "]）");
                foreach (var fake in BadHideProxies) objectNames.Add(fake);
            }

            // (1) 每个期望名（裸名 + wire 名）必须逐字节到齐
            foreach (var name in expected)
                if (!objectNames.Contains(name)) failures.Add("缺失 " + name);

            // (2) base 桶正面检查：裸名在位，且不得被前缀语法误伤
            foreach (var bare in bareExpected)
                if (objectNames.Contains(bare) && WireGrammar.IsMatch(bare))
                    failures.Add("base 裸名不应匹配 wire 前缀语法: " + bare);

            // (3) wire 名往返：解析 id/bucket/leaf；hide 的 leaf 去掉 _off 必须解析到已存在物件
            int roundTrip = 0;
            int hideResolved = 0;
            int failuresBeforeRoundTrip = failures.Count;
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
                        else
                            hideResolved++;
                    }
                }
                else if (bucket != "show")
                {
                    failures.Add("未知 bucket: " + bucket + "（来自 " + name + "）");
                }
            }
            // "已核"这句只在往返这一段确实一条失败都没加的时候才打印，否则红跑里 grep 这句会误读成成功。
            if (failures.Count == failuresBeforeRoundTrip)
                Debug.Log("[VERIFY] wire 往返: 解析到 " + roundTrip + " 个前缀名，hide→base 目标映射已核（"
                          + hideResolved + " 个 hide 代理）");
            else
                Debug.Log("[VERIFY] ---- 实测 ---- wire 往返: 解析到 " + roundTrip
                          + " 个前缀名，hide 映射通过 " + hideResolved + " 个（本段有失败，见上方 FAIL）");

            // (4) 世界尺度断言在几何上（不只是两点距离）：每个 mesh 节点打证据 + 断言 1m
            foreach (var kv in meshNodes)
            {
                var t = kv.Value;
                var mf = t.GetComponent<MeshFilter>();
                var mesh = mf != null ? mf.sharedMesh : null;
                if (mesh == null) continue;
                // 尺寸乘式只在"网格 AABB 对旋转不变"时才等于真实世界 AABB。本探针是 1m 立方体，
                // 旋转后 AABB 不变，所以这个乘式成立；但实测节点 worldRot=(270.02,0,0)（导入器逐节点
                // 做 Z-up→Y-up），换成非对称网格就不成立了 —— 故同场打印 renderer.bounds 作交叉证据，
                // 后续 FBX 若带旋转，先比对这两个读数再动尺寸门。
                Vector3 worldSize = Vector3.Scale(mesh.bounds.size, t.lossyScale);
                var rend = t.GetComponent<Renderer>();
                Debug.Log("[VERIFY] 节点 " + kv.Key
                          + " local=" + t.localPosition
                          + " world=" + t.position
                          + " worldRot=" + t.eulerAngles
                          + " lossyScale=" + t.lossyScale
                          + " boundsSize=" + mesh.bounds.size
                          + " worldSize=" + worldSize
                          + (rend != null ? " rendBounds=" + rend.bounds.size + "/" + rend.bounds.center : "")
                          + " verts=" + mesh.vertexCount);
                if (!InRange(worldSize.x) || !InRange(worldSize.y) || !InRange(worldSize.z))
                    failures.Add("世界尺寸不在容差 [" + SizeLo + "," + SizeHi + "]（疑 100×/0.01× 导入）: "
                                 + kv.Key + " worldSize=" + worldSize + " worldRot=" + t.eulerAngles);
            }
            // root lossyScale ≈ 1（几何尺度的前提；异常包裹会在这里露出 100 倍）
            var rootLossy = prefab.transform.lossyScale;
            Debug.Log("[VERIFY] rootLossyScale=" + rootLossy + " rootRot=" + prefab.transform.eulerAngles);
            if (!InRange(rootLossy.x) || !InRange(rootLossy.y) || !InRange(rootLossy.z))
                failures.Add("root lossyScale 不在容差 [" + SizeLo + "," + SizeHi + "]: " + rootLossy);

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
            {
                // 逐条单独成行：坏样本跑要能看出"哪几条分支各自被喂红"（缺名/hide 目标/尺寸），
                // 只有一行拼接的 FAIL 无法证明每条分支的牙。末行汇总，胶水 tail -1 仍命中 FAIL。
                foreach (var f in failures)
                    Debug.LogError("[VERIFY] FAIL: " + f);
                Debug.LogError("[VERIFY] FAIL: 共 " + failures.Count + " 项 —— " + string.Join(" | ", failures));
            }
        }

        static bool InRange(float v)
        {
            return v >= SizeLo && v <= SizeHi;
        }

        /// <summary>
        /// Task 3 验收口：解析器 AnomalyResolver.Resolve 对既有 Probe.fbx 夹具的端到端自证。
        /// 断言 ①条目数/id/title ②show 收的是接线物体本身 ③hide 收到剥 _off 后的 base 目标
        /// （代理自己绝不进 hide）④SettleToNormal/Apply/Revert 的开合逐字还原
        /// ⑤IsAnomalyWireName 真值表。坏样本开关沿用 VERIFY_BAD=1：期望条目数恒保持真实值 1
        /// （不再用 count 短路掉 else 块），改在 else 块内注入 4 条彼此独立的坏期望——
        /// title 逐字 / show 本体指认 / hide→base 目标名 / IsAnomalyWireName 真值表一项。
        /// 一次坏跑即产出 4 行各自独立的 [VERIFY] FAIL，每分支各证一次红；默认路径绝不用假期望。
        /// 临时根最后必删，不留场景残骸、不改夹具预制。
        /// </summary>
        public static void VerifyResolveProbe()
        {
            var failures = new List<string>();
            // 坏样本不再靠"条目数"短路：期望条目数恒 = 真实值 1，让 else 块两种模式都进得去。
            // 真正的坏期望注入全部下移到 else 块内部（title / show 本体 / hide 目标 / wire 真值表），
            // 每条独立成一红，聚合进同一个 failures 列表逐行打印。
            const int expectedEntries = 1;
            if (BadSample)
                Debug.Log("[VERIFY] ---- 坏样本注入（仅 VERIFY_BAD=1）---- 期望条目数保持真实值 1（不靠 count 短路），"
                          + "改在 else 块内注入 4 条独立坏期望：title / show 本体指认 / hide→base 目标名 / IsAnomalyWireName 真值表一项；默认路径绝不用假期望");

            AssetDatabase.ImportAsset(ProbePath, ImportAssetOptions.ForceSynchronousImport);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProbePath);
            if (prefab == null)
            {
                Debug.LogError("[VERIFY] FAIL: 无法加载 " + ProbePath);
                return;
            }

            var tempRoot = new GameObject("VerifyResolveProbeRoot");
            try
            {
                var inst = UnityEngine.Object.Instantiate(prefab);
                inst.transform.SetParent(tempRoot.transform, false);
                Debug.Log("[VERIFY] 夹具实例根节点名: " + inst.name
                          + "（解析器按递归走，不按根名，(Clone) 后缀无害）");

                var entries = AnomalyResolver.Resolve(tempRoot.transform);

                // (1) 条目数 / id / title
                if (entries.Count != expectedEntries)
                    failures.Add("异常条目数=" + entries.Count + "，期望=" + expectedEntries);
                else
                {
                    Debug.Log("[VERIFY] 条目数符合期望: " + entries.Count);
                    var e = entries[0];
                    if (e.id != "anom01") failures.Add("id 应为 anom01，实际: " + e.id);
                    // 坏样本注入①（title 逐字）：else 块两种模式都会跑到这里，坏期望真正生效、独立成一行 FAIL
                    string wantTitle = BadSample ? "故意错的标题" : AnomalyTitles.Table["anom01"];
                    if (e.title != wantTitle)
                        failures.Add("title 不符: " + e.title + "（期望 " + wantTitle + "）");
                    else
                        Debug.Log("[VERIFY] title 逐字命中表: " + e.title);

                    // (2) show：收的就是接线物体本身
                    var wantShow = new List<string> { "anom01_show_shadeFigure", "anom01_show_probeCubeB" };
                    // 坏样本注入②（show 本体指认）：往期望集塞一个夹具里绝不存在的 show 名。真实 e.show
                    // 仍只两项，故 count==2 分支照进、本体循环对这个假名 FindByName 必为 null，独立成一行 FAIL。
                    if (BadSample)
                        wantShow.Add("anom99_show_no_such_object");
                    if (e.show.Count != 2)
                        failures.Add("show.Count 应为 2，实际: " + e.show.Count);
                    else
                    {
                        int failuresBeforeShow = failures.Count;
                        foreach (var name in wantShow)
                        {
                            var t = FindByName(tempRoot.transform, name);
                            var go = t != null ? t.gameObject : null;
                            if (go == null || !e.show.Contains(go))
                                failures.Add("show 缺接线物体本身: " + name);
                        }
                        // 只有本段一条失败都没加时才打"已核"，否则红跑里这句会被误读成通过（沿用 ProbeHierarchy 口径）。
                        if (failures.Count == failuresBeforeShow)
                            Debug.Log("[VERIFY] show 两项均为接线物体本身: " + string.Join(",", wantShow));
                        else
                            Debug.Log("[VERIFY] ---- 实测 ---- show 本体指认有失败，见上方 FAIL（本段 "
                                      + (failures.Count - failuresBeforeShow) + " 条）");
                    }

                    // (3) hide：代理剥 _off 后指认到 base 目标；代理自己绝不进 hide
                    // 坏样本注入③（hide→base 目标名）：把期望目标名翻成 base 里绝不存在的名字。真实
                    // e.hide[0].name 仍是 floor，故目标名比较必然判红，独立成一行 FAIL。
                    string wantHideTarget = BadSample ? "no_such_base_target" : "floor";
                    if (e.hide.Count != 1)
                        failures.Add("hide.Count 应为 1，实际: " + e.hide.Count);
                    else if (e.hide[0] == null || e.hide[0].name != wantHideTarget)
                        failures.Add("hide[0] 应为 base 目标 " + wantHideTarget + "，实际: "
                                     + (e.hide[0] == null ? "null" : e.hide[0].name));
                    else
                        Debug.Log("[VERIFY] hide[0]=floor（代理 anom01_hide_floor_off 已指认 base 目标）");

                    var proxyT = FindByName(tempRoot.transform, "anom01_hide_floor_off");
                    if (proxyT != null && (e.hide.Contains(proxyT.gameObject)
                                           || e.show.Contains(proxyT.gameObject)))
                        failures.Add("hide 代理自己混进了 show/hide 桶: " + proxyT.name);

                    // (4) 状态机三段：SettleToNormal / Apply / Revert 逐字还原
                    var shadeT = FindByName(tempRoot.transform, "anom01_show_shadeFigure");
                    var cubeT = FindByName(tempRoot.transform, "anom01_show_probeCubeB");
                    var floorT = FindByName(tempRoot.transform, "floor");
                    if (shadeT == null || cubeT == null || floorT == null || proxyT == null)
                        failures.Add("夹具节点缺失，无法跑状态机断言");
                    else
                    {
                        e.SettleToNormal();
                        CheckActive(failures, "SettleToNormal", shadeT, false);
                        CheckActive(failures, "SettleToNormal", cubeT, false);
                        CheckActive(failures, "SettleToNormal", floorT, true);
                        CheckActive(failures, "SettleToNormal", proxyT, false);

                        e.Apply();
                        CheckActive(failures, "Apply", shadeT, true);
                        CheckActive(failures, "Apply", cubeT, true);
                        CheckActive(failures, "Apply", floorT, false);
                        CheckActive(failures, "Apply", proxyT, false);

                        e.Revert();
                        CheckActive(failures, "Revert", shadeT, false);
                        CheckActive(failures, "Revert", cubeT, false);
                        CheckActive(failures, "Revert", floorT, true);
                        CheckActive(failures, "Revert", proxyT, false);
                        if (failures.Count == 0)
                            Debug.Log("[VERIFY] Revert 逐字还原 SettleToNormal 后的正常态");
                    }
                }

                // (5) IsAnomalyWireName 真值表（口径 = WireGrammar：两位数字 + show|hide 桶）
                CheckWireName(failures, "anom01_show_x", true);
                CheckWireName(failures, "anom01_hide_x_off", true);
                // 坏样本注入④（wire 真值表一项）：把 "floor" 的期望翻成 true。真实判定恒为 false
                // （裸名不匹配前缀语法），故这一项必然判红，独立成一行 FAIL。其余五项口径不变。
                CheckWireName(failures, "floor", BadSample ? true : false);
                CheckWireName(failures, "wallL", false);
                CheckWireName(failures, "anom1_show_x", false);
                CheckWireName(failures, "Calendar2000_mark", false);
                if (failures.Count == 0)
                    Debug.Log("[VERIFY] IsAnomalyWireName 真值表 6/6 命中（anom1_show_x 位数不足 → false，与解析器同口径）");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tempRoot);
                Debug.Log("[VERIFY] 临时根已销毁，无场景残骸（夹具预制未触碰）");
            }

            if (failures.Count == 0)
                Debug.Log("[VERIFY] PASS: AnomalyResolver 对 Probe.fbx 端到端成立（条目/id/title + show 本体 + hide→base 指认 + Apply/Revert 还原 + wire 真值表）");
            else
            {
                foreach (var f in failures)
                    Debug.LogError("[VERIFY] FAIL: " + f);
                Debug.LogError("[VERIFY] FAIL: 共 " + failures.Count + " 项 —— " + string.Join(" | ", failures));
            }
        }

        static void CheckWireName(List<string> failures, string name, bool expected)
        {
            bool actual = AnomalyResolver.IsAnomalyWireName(name);
            if (actual != expected)
                failures.Add("IsAnomalyWireName(" + name + ")=" + actual + "，期望 " + expected);
        }

        static void CheckActive(List<string> failures, string phase, Transform t, bool expected)
        {
            if (t.gameObject.activeSelf != expected)
                failures.Add(phase + ": " + t.name + " activeSelf=" + t.gameObject.activeSelf
                             + "，期望 " + expected);
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

        // ---- Task 4 玩法段 fixture：base 裸名 + 前缀式 wire 名（default 组 = anom01..anom06 + anom08，extra 集 = anom09）----
        static readonly string[] FixtureBase = { "floor", "wallL", "lampA", "poster", "door", "bike" };
        static readonly string[] FixtureShow =
        {
            "anom01_show_extraFigure",
            "anom02_show_extraPoster",
            "anom03_show_doorOpen",
            "anom05_show_shoes",
            "anom06_show_bikeFallen",
            "anom08_show_waterPuddle",
            "anom09_show_redWire",
        };
        static readonly string[] FixtureHideProxy =
        {
            "anom01_hide_lampA_off",
            "anom03_hide_door_off",
            "anom04_hide_poster_off",
            "anom06_hide_bike_off",
        };
        // 默认目录 7 条 + extra 集 1 条（anom09）= fixture 里共 8 个不同 id
        static readonly string[] DefaultIds = { "anom01", "anom02", "anom03", "anom04", "anom05", "anom06", "anom08" };
        static readonly string[] FullIds = { "anom01", "anom02", "anom03", "anom04", "anom05", "anom06", "anom08", "anom09" };

        /// <summary>
        /// Task 4 玩法段验收口（batchmode -executeMethod …Verify3D.VerifyGameplay）。
        /// 就地搭灰盒 fixture（GameObject.CreatePrimitive + 前缀式 wire 名），走
        /// AnomalyResolver.Resolve → Loop3D.Setup → Begin/Report 的完整链路；
        /// 不引用 GreyboxAnomalies（那是 Task 5 才创建的文件），形态抄 VerifyResolveProbe。
        /// 异常是否生效一律按 activeSelf 断（Apply/Revert/SettleToNormal 的真实语义），不按 mr.enabled。
        /// 坏样本（VERIFY_BAD=1）注入 5 条彼此独立的坏期望：目录 id 列表 / depth1 干净位 /
        /// hide→base 目标名 / Apply 后 show 的 activeSelf / 好结局后的状态机相位。
        /// 每条各成一红、聚合进局部 failures 逐行打印（本文件无共享 fails，也无 Report()）；
        /// 默认路径绝不用假期望。临时 fixture 与临时 Loop3D 在 finally 里 DestroyImmediate。
        /// </summary>
        public static void VerifyGameplay()
        {
            var failures = new List<string>();
            if (BadSample)
                Debug.Log("[VERIFY] ---- 坏样本注入（仅 VERIFY_BAD=1）---- 5 条独立坏期望："
                          + "目录 id 列表塞入 anom99_not_in_fixture / depth1 干净位翻成'应有异常' / "
                          + "hide→base 目标名翻成 no_such_base_target / Apply 后 show 期望仍关着 / "
                          + "好结局后相位期望翻成 Normal；默认路径绝不用这些假期望");

            var fixture = BuildGameplayFixture();
            GameObject loopGo = null;
            try
            {
                // (0) 解析器 → 目录：走真 wire 名，而不是手摆 AnomalyEntry
                var all = AnomalyResolver.Resolve(fixture.transform);
                Debug.Log("[VERIFY] fixture 经 AnomalyResolver 解析到 " + all.Count + " 条异常");
                var defaultCatalog = all.FindAll(e => e.id != "anom09");

                loopGo = new GameObject("VerifyGameplayLoop");
                var loop = loopGo.AddComponent<Dreamcore.D3.Loop3D>();

                // (1) Setup 空值守卫：null 目录 / null 根都要抛 ArgumentNullException
                int nullCatalogHits = 0, nullRootHits = 0;
                try { loop.Setup(null, fixture.transform); }
                catch (ArgumentNullException) { nullCatalogHits++; }
                try { loop.Setup(defaultCatalog, null); }
                catch (ArgumentNullException) { nullRootHits++; }
                Assert(failures, nullCatalogHits == 1,
                       "Setup(null, root) 必须抛 ArgumentNullException，实际抛了 " + nullCatalogHits + " 次");
                Assert(failures, nullRootHits == 1,
                       "Setup(catalog, null) 必须抛 ArgumentNullException，实际抛了 " + nullRootHits + " 次");

                // (2) 默认目录 = anom01..anom06 + anom08（id 逐字、顺序按解析器排序）
                loop.Setup(defaultCatalog, fixture.transform);
                CheckIds(failures, "默认目录 id", loop.GetAnomalyIdsForTest(),
                         BadSample ? WithBogus(DefaultIds, "anom99_not_in_fixture") : DefaultIds);
                Assert(failures, loop.CountAnomalies() == DefaultIds.Length,
                       "默认目录应装 " + DefaultIds.Length + " 条，实际 " + loop.CountAnomalies());

                // (3) 幂等：换成 default+extra（anom09）共 8 条，再连调两次不得叠加
                loop.Setup(all, fixture.transform);
                int firstCount = loop.CountAnomalies();
                loop.Setup(all, fixture.transform);
                Assert(failures, firstCount == FullIds.Length && loop.CountAnomalies() == FullIds.Length,
                       "Setup 幂等：含 extra 集后连调两次仍应 " + FullIds.Length
                       + " 条，实际首次 " + firstCount + "/二次 " + loop.CountAnomalies());
                CheckIds(failures, "装配后 id", loop.GetAnomalyIdsForTest(), FullIds);

                // (4) Setup 将全场钉成"正常"：show 全关、hide（base 目标）全开
                int normalViolations = 0;
                foreach (var e in loop.anomalies)
                {
                    foreach (var g in e.show) if (Active(g)) normalViolations++;
                    foreach (var g in e.hide) if (!Active(g)) normalViolations++;
                }
                Assert(failures, normalViolations == 0,
                       "Setup 后 show 必须全关、hide 目标必须全开，违例 " + normalViolations + " 处");
                Assert(failures, loop.Depth == 1, "初始深度应为 1，实际 " + loop.Depth);
                Assert(failures, loop.Mistakes == 0, "初始异常记录应为 0，实际 " + loop.Mistakes);
                Assert(failures, loop.Current == null, "Setup 后不应有在用异常，实际 "
                         + (loop.Current == null ? "null" : loop.Current.id));

                // (5) hide 桶收到的是剥 _off 后的 base 目标本身（不是代理）
                // 坏样本注入③：把期望目标名翻成 base 里绝不存在的名字。
                var e01 = all.Find(e => e.id == "anom01");
                var hideTarget = e01 != null && e01.hide.Count > 0 ? e01.hide[0] : null;
                string wantHideTarget = BadSample ? "no_such_base_target" : "lampA";
                Assert(failures, hideTarget != null && hideTarget.name == wantHideTarget,
                       "anom01 hide[0] 应为 base 目标 " + wantHideTarget + "，实际 "
                       + (hideTarget == null ? "无" : hideTarget.name));

                // (6) Apply / Revert / SettleToNormal 真开关（按 activeSelf 断，不看 mr.enabled）
                if (e01 == null || hideTarget == null)
                    failures.Add("anom01 条目或其 hide 目标缺失，无法跑 Apply/Revert 断言");
                else
                {
                    var fig = e01.show.Find(g => g != null && g.name == "anom01_show_extraFigure");
                    Assert(failures, fig != null, "anom01 show 里应能找到 anom01_show_extraFigure");
                    if (fig != null)
                    {
                        e01.Apply();
                        // 坏样本注入④：Apply 之后仍期望 show 关着（与 Apply 的语义相反，必红）
                        Assert(failures, fig.activeSelf == !BadSample,
                               "Apply 应开 show：extraFigure.activeSelf=" + fig.activeSelf);
                        Assert(failures, !Active(hideTarget), "Apply 应关 hide 目标 lampA");
                        e01.Revert();
                        Assert(failures, !fig.activeSelf && Active(hideTarget),
                               "Revert 应逐字还原：extraFigure.activeSelf=" + fig.activeSelf
                               + " lampA.activeSelf=" + Active(hideTarget));
                        e01.Apply();
                        loop.SettleNowForTest();
                        Assert(failures, !fig.activeSelf && Active(hideTarget),
                               "SettleNowForTest 应把所有条目钉回正常态（含在用的异常）");
                    }
                }

                // (7) depth 1 强制干净 —— 玩家先要见过"正常"
                // 坏样本注入②：真实期望恒为"无异常"，坏样本把它翻成"应有异常"。
                loop.cleanChance = 0f; // 本段只验抽取规则，干净层单列
                loop.Begin(12345);
                bool wantAnomalyAtDepth1 = BadSample;
                Assert(failures, loop.HasAnomaly == wantAnomalyAtDepth1,
                       "depth 1 必须干净（HasAnomaly=" + loop.HasAnomaly + "，期望 "
                       + wantAnomalyAtDepth1 + "）");
                Assert(failures, loop.GetState() == "Normal", "Begin 后相位应为 Normal，实际 " + loop.GetState());

                // (8) 不重复 + 连答到 maxDepth 出好结局
                var seen = new List<string>();
                bool ended = false;
                bool lastWin = false;
                loop.OnEnd += w => { ended = true; lastWin = w; };
                for (int i = 0; i < 7 && !ended; i++)
                {
                    loop.Report(!loop.HasAnomaly); // 永远答对
                    if (loop.Current != null) seen.Add(loop.Current.id);
                }
                Assert(failures, seen.Count == new HashSet<string>(seen).Count,
                       "同一局内异常不得重复，抽到 " + seen.Count + " 层、去重后 "
                       + new HashSet<string>(seen).Count + " 个");
                Assert(failures, ended && lastWin, "连答 7 次应到深度 8 触发好结局，实际 ended="
                         + ended + " win=" + lastWin);

                // 坏样本注入⑤：好结局后相位期望翻成 Normal（真实为 Ending）
                string wantStateAfterWin = BadSample ? "Normal" : "Ending";
                Assert(failures, loop.GetState() == wantStateAfterWin,
                       "好结局后相位应为 " + wantStateAfterWin + "，实际 " + loop.GetState());
                Assert(failures, loop.Mistakes <= loop.maxMistakes,
                       "结局时异常记录不得越过 maxMistakes，实际 " + loop.Mistakes + "/" + loop.maxMistakes);

                // (9) ResetLoop 把场面归位并允许重开（含 Time.timeScale 复原）
                loop.ResetLoop();
                Assert(failures, loop.GetState() == "Normal" && loop.Depth == 1 && loop.Mistakes == 0
                       && loop.Current == null,
                       "ResetLoop 后应回到 Normal/深度1/0 记录/无在用异常，实际 " + loop.GetState()
                       + "/" + loop.Depth + "/" + loop.Mistakes);
                Assert(failures, Mathf.Approximately(Time.timeScale, loop.timeScale),
                       "ResetLoop 应把 Time.timeScale 复原成 timeScale 字段值，实际 "
                       + Time.timeScale + " vs " + loop.timeScale);

                // (10) 三错即败 + 结束后判定被忽略（ended 门）
                loop.Begin(999);
                int loseFlags = 0;
                loop.OnEnd += w => { if (!w) loseFlags |= 1; };
                for (int i = 0; i < 3; i++) loop.Report(loop.HasAnomaly); // 永远答错
                Assert(failures, (loseFlags & 1) == 1, "错满 3 次必须触发坏结局");
                int depthAfterLose = loop.Depth;
                loop.Report(false);
                Assert(failures, loop.Depth == depthAfterLose,
                       "结束后再判定必须被忽略（ended 门），实际 " + loop.Depth + " vs " + depthAfterLose);

                // (11) 干净层确实会出现（否则"无脑回头"成为支配策略）
                bool anyClean = false;
                for (int seed = 1; seed <= 60 && !anyClean; seed++)
                {
                    loop.cleanChance = 0.35f;
                    loop.Begin(seed);
                    for (int i = 0; i < 6; i++)
                    {
                        loop.Report(!loop.HasAnomaly);
                        if (loop.Depth == 1 || loop.Mistakes >= 3) break;
                        if (!loop.HasAnomaly) { anyClean = true; break; }
                    }
                }
                Assert(failures, anyClean, "存在 seed 能让干净层出现（否则玩家可无脑回头）");
            }
            finally
            {
                if (loopGo != null) UnityEngine.Object.DestroyImmediate(loopGo);
                UnityEngine.Object.DestroyImmediate(fixture);
                Debug.Log("[VERIFY] 临时 fixture 与临时 Loop3D 已 DestroyImmediate，无场景残骸（wire 名节点都在 fixture 子树内）");
            }

            if (failures.Count == 0)
                Debug.Log("[VERIFY] PASS: 玩法断言全过（Setup 守卫+幂等 / 目录 id / activeSelf 三段 / depth1 干净 / 不重复 / 好坏结局 / ended 门 / 干净层）");
            else
            {
                foreach (var f in failures)
                    Debug.LogError("[VERIFY] FAIL: " + f);
                Debug.LogError("[VERIFY] FAIL: 共 " + failures.Count + " 项 —— " + string.Join(" | ", failures));
            }
        }

        /// <summary>
        /// 就地搭玩法 fixture：base 用图元（有 MeshFilter，解析器按名分桶），
        /// show 用图元（异常出现件），hide 用空 GameObject（只做 _off 名字载体，与 FBX 里的 EMPTY 同构）。
        /// </summary>
        static GameObject BuildGameplayFixture()
        {
            var root = new GameObject("VerifyGameplayFixture");
            var b = new GameObject("base");
            b.transform.SetParent(root.transform, false);
            foreach (var name in FixtureBase)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name;
                go.transform.SetParent(b.transform, false);
            }
            var s = new GameObject("show");
            s.transform.SetParent(root.transform, false);
            foreach (var name in FixtureShow)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = name;
                go.transform.SetParent(s.transform, false);
            }
            var h = new GameObject("hide");
            h.transform.SetParent(root.transform, false);
            foreach (var name in FixtureHideProxy)
            {
                var go = new GameObject(name);
                go.transform.SetParent(h.transform, false);
            }
            return root;
        }

        static string[] WithBogus(string[] ids, string bogus)
        {
            var l = new List<string>(ids);
            l.Add(bogus);
            return l.ToArray();
        }

        static void CheckIds(List<string> failures, string label, List<string> actual, string[] expected)
        {
            bool same = actual != null && actual.Count == expected.Length;
            if (same)
                for (int i = 0; i < expected.Length; i++)
                    if (actual[i] != expected[i]) { same = false; break; }
            Assert(failures, same, label + " 应逐字为 [" + string.Join(",", expected) + "]，实际 ["
                     + (actual == null ? "null" : string.Join(",", actual)) + "]");
        }

        /// <summary>本文件惯例：断言即打印，失败聚合到调用方自己的局部 failures。</summary>
        static void Assert(List<string> failures, bool ok, string what)
        {
            if (!ok) failures.Add(what);
            Debug.Log("[VERIFY] " + (ok ? "ok  " : "FAIL") + "  " + what);
        }

        static bool Active(GameObject g)
        {
            return g != null && g.activeSelf;
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
