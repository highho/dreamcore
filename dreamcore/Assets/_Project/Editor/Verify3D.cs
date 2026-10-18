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

        // ================= M6 契约名单 =================
        // 走廊结构的唯一真相在 blender/gen_corridor.py 与 gen_anomalies.py；这四张表是它在 Unity
        // 验收侧的逐字镜像。**期望值一律从这里派生，不再写魔数**——M6 把 25 个网格涨到 80 个时，
        // 原先散在各处的 meshNodes==25 / litBase==15 / hideTotal==13 一次红了 7 条，
        // 那种红是账目红不是质量红，把数字改成新数字只会让下次加件再红一遍。
        // 加一个 base 件：Blender 改完，这里加一个名字，所有计数门自动跟上；
        // 三桶表（Flat/Glass）漏登记才会红，因为那才是"这物件该长什么样"的真实分歧点。
        // 本表故意不从 Bootstrap3D 读——那叫实现验自己，没有牙。

        // 杂物段（M6 T2）：Poly Haven 的 CC0 道具，几何由 blender/gen_props.py 落位、
        // 贴图由 tools/fetch_props.sh 落 Content/Textures/Prop_<slug>/。
        // 同一资产多件用 __<n> 后缀（slug 内不含下划线，Bootstrap3D.SurfFor 按 "__" 切回 slug）。
        // 声明顺序有讲究：本表必须在 BaseMeshNames 之前，静态字段按文本顺序初始化。
        static readonly string[] PropBaseNames =
        {
            "Prop_chair__1", "Prop_chair__2", "Prop_chair__3",
            "Prop_bench", "Prop_table", "Prop_medbox", "Prop_shelf",
            "Prop_container", "Prop_crate", "Prop_sign", "Prop_bag",
            "Prop_extinguisher", "Prop_plunger", "Prop_clock", "Prop_alarm",
        };
        static readonly string[] PropTexDirs =
        {
            "Prop_chair", "Prop_bench", "Prop_table", "Prop_medbox", "Prop_shelf",
            "Prop_container", "Prop_crate", "Prop_sign", "Prop_bag", "Prop_extinguisher",
            "Prop_plunger", "Prop_clock", "Prop_alarm",
        };
        static readonly string[] CorridorBaseNames =
        {
            // 壳体
            "Floor", "Ceiling", "EndWall",
            "WallLeft_A", "WallLeft_B", "WallLeft", "WallLeft_Lintel_3", "WallLeft_Lintel_9",
            "WallRight_A", "WallRight", "WallRight_Sill", "WallRight_Lintel",
            // 墙裙
            "WainscotL", "WainscotR", "WainscotCapL", "WainscotCapR",
            // 两户门（门斗衬板 + 门套三件 + 门板 + 把手 + 门牌）
            "Doorway_3", "DoorFrame_3_JambL", "DoorFrame_3_JambR", "DoorFrame_3_Head",
            "Door_3", "Door_3_Handle", "DoorPlate_3",
            "Doorway_9", "DoorFrame_9_JambL", "DoorFrame_9_JambR", "DoorFrame_9_Head",
            "Door_9", "Door_9_Handle", "DoorPlate_9",
            // 一扇真窗（窗框五件 + 玻璃 + 四根防盗栏 + 横栏）
            "WindowFrame_Sill", "WindowFrame_Head", "WindowFrame_JambL", "WindowFrame_JambR",
            "WindowFrame_Mullion", "Glass_R",
            "WindowBar_1", "WindowBar_2", "WindowBar_3", "WindowBar_4", "WindowRail",
            // 尽头楼梯间铁门 + 安全出口牌
            "EndDoor", "EndDoor_Handle", "ExitSign",
            // 六套灯具（灯座 + 自发光灯管；名字用 Lamp_ 是为了不撞 AdoptLights 的 Light_ 前缀）
            "LampBase_2", "Lamp_2", "LampBase_6", "Lamp_6", "LampBase_10", "Lamp_10",
            "LampBase_14", "Lamp_14", "LampBase_18", "Lamp_18", "LampBase_22", "Lamp_22",
            // 立面管线与地面分格
            "CableTrunk_L", "Pipe_1", "Pipe_2", "FloorStrip_1", "FloorStrip_2", "FloorStrip_3",
            // 可被异常指认的本体（裸名与 Task 5 灰盒逐字一致）
            "Poster_6", "Poster_10", "Poster_18",
            "MailboxBody", "MailboxDoor", "Mailbox_Div1", "Mailbox_Div2",
            "BikeUp", "WindowBuildings",
        };
        // base 名单＝走廊本体 + 杂物。所有计数门（MeshRenderer 总数、base/anom 二分、三桶算术、
        // 缺席件、缺碰撞体）都从这一张派生：加一件道具只要 gen_props.py 与 PropBaseNames 各加
        // 一个名字，门自动跟上；漏登记任何一边都会红，不会默默兜底。
        static readonly string[] BaseMeshNames = WithProps(CorridorBaseNames);

        static string[] WithProps(string[] corridor)
        {
            var all = new string[corridor.Length + PropBaseNames.Length];
            Array.Copy(corridor, all, corridor.Length);
            Array.Copy(PropBaseNames, 0, all, corridor.Length, PropBaseNames.Length);
            return all;
        }

        static readonly string[] BaseEmptyNames =
            { "Light_2", "Light_6", "Light_10", "Light_14", "Light_18", "Light_22" };
        static readonly string[] AnomShowNames =
        {
            "anom01_show_emergencyLight", "anom02_show_extraFigure", "anom03_show_Door_3_open",
            "anom05_show_Shoes", "anom06_show_BikeUp_open", "anom07_show_MailboxDoor_open",
            "anom08_show_WaterPuddle", "anom09_show_RedWire", "anom10_show_WindowBuildings_open",
        };
        // M6 的连带义务：一个 base 件被拆成多个可视零件，指认它的每组异常就得每个零件各有一条代理。
        // anom01 从 6 条涨到 12（灯灭了、自发光灯管不会自己跟着灭）；anom03 涨到 2（门板 + 把手）；
        // anom07 涨到 3（信箱门 + 两道分格）。
        static readonly string[] AnomHideNames =
        {
            "anom01_hide_Light_2_off", "anom01_hide_Light_6_off", "anom01_hide_Light_10_off",
            "anom01_hide_Light_14_off", "anom01_hide_Light_18_off", "anom01_hide_Light_22_off",
            "anom01_hide_Lamp_2_off", "anom01_hide_Lamp_6_off", "anom01_hide_Lamp_10_off",
            "anom01_hide_Lamp_14_off", "anom01_hide_Lamp_18_off", "anom01_hide_Lamp_22_off",
            "anom03_hide_Door_3_off", "anom03_hide_Door_3_Handle_off",
            "anom04_hide_Poster_6_off", "anom04_hide_Poster_10_off", "anom04_hide_Poster_18_off",
            "anom06_hide_BikeUp_off",
            "anom07_hide_MailboxDoor_off", "anom07_hide_Mailbox_Div1_off", "anom07_hide_Mailbox_Div2_off",
            "anom10_hide_WindowBuildings_off",
        };

        // base 材质三桶（验收侧独立重述，与 Bootstrap3D.SurfFor 两张表必须互相吻合）：
        // Flat＝URP/Unlit 纯色（自发光或纯黑，没有贴图概念）；Glass＝URP/Lit 且 _Surface 为透明；
        // 不在任何桶里的 base 名一律按"URP/Lit 且吃到 BaseMap+Normal+_NORMALMAP"要求。
        static readonly string[] FlatBaseNames =
        {
            "Doorway_3", "Doorway_9", "ExitSign", "WindowBuildings",
            "Lamp_2", "Lamp_6", "Lamp_10", "Lamp_14", "Lamp_18", "Lamp_22",
        };
        static readonly string[] GlassBaseNames = { "Glass_R" };

        /// <summary>洞口清单（Blender 建模口径）：Along=沿程米数、Height=离地米数、Width×Tall=洞口尺寸、
        /// Shrink=允许覆盖物比洞口小多少（玻璃本来就要缩进窗框）。世界系里沿程→Z、高度→Y，
        /// 横侧符号从墙体自身的 world.x 反推——FbxYaw=90 之后左右与灰盒对调（见 blender/README.md 轴映射）。</summary>
        struct Hole
        {
            public string WallPrefix;
            public string Cover;
            public float Along, Height, Width, Tall, Shrink;
            public Hole(string wallPrefix, string cover, float along, float height, float width, float tall, float shrink)
            {
                WallPrefix = wallPrefix; Cover = cover;
                Along = along; Height = height; Width = width; Tall = tall; Shrink = shrink;
            }
        }
        static readonly Hole[] Holes =
        {
            new Hole("WallLeft",  "Doorway_3", 3.4f, 1.05f, 0.9f, 2.1f, 0.02f),
            new Hole("WallLeft",  "Doorway_9", 9.0f, 1.05f, 0.9f, 2.1f, 0.02f),
            new Hole("WallRight", "Glass_R",   9.0f, 2.00f, 3.4f, 1.6f, 0.10f),
        };

        static bool InList(string[] list, string n) { return Array.IndexOf(list, n) >= 0; }

        static string BucketOf(string n)
        {
            if (InList(GlassBaseNames, n)) return "Glass";
            if (InList(FlatBaseNames, n)) return "Flat";
            return InList(BaseMeshNames, n) ? "Tex" : "Unknown";
        }

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

        /// <summary>
        /// Task 4 玩法段验收口（batchmode -executeMethod Dreamcore.EditorTools.Verify3D.VerifyGameplay）。
        /// 断言集合逐条对齐 brief Step 1，fixture 也照 Step 1 自己搭：10 条假异常（空 GameObject，
        /// 灰盒阶段不需要美术）直接赋给 Loop3D.anomalies，cleanChance 由本段自己设。
        /// 本段刻意不调 AnomalyResolver、不引用 GreyboxAnomalies（那是 Task 5 才创建的文件）：
        /// Loop3D 只认目录（计划 1090/1618 行 `loop.anomalies = entries;`、846 行断 `anomalies.Count == 10`），
        /// 装配永远是消费方的事，玩法层不该知道物件从哪来。
        /// 覆盖：初始 Depth==1 / Begin(12345) 后 depth1 干净 / 同局不重复 / 连答 7 次好结局 /
        /// 三错坏结局 / 结局后判定被忽略（ended 门）/ 存在能出干净层的 seed / Apply-Revert 真开关
        /// （异常生效一律按 activeSelf 断，这就是 AnomalyEntry 的显隐模型）。
        /// 坏样本（VERIFY_BAD=1）分支级注入 4 条、各成一红互不短路：
        /// ① depth1 干净位翻成"应有异常"；② ended 门的 OnEnd 派发次数期望翻成"2 次"；
        /// ③ Apply 后 show 的期望翻成"仍关着"；④ brief 自带的那条不可能断言（"坏样本：这条断言必须失败"）。
        /// 四条都注入在本次跑真正会走到的分支里，默认路径的期望值全是真实值、一条都不放宽。
        /// 形态沿用本文件真实惯例：入口自持局部 failures + 逐行 [VERIFY] FAIL: + 汇总行 + 单行 [VERIFY] PASS
        /// （本文件没有共享 fails/Report()，不新增，省得和既有两个口打架）。临时物件在 finally 里 DestroyImmediate。
        /// </summary>
        public static void VerifyGameplay()
        {
            var failures = new List<string>();
            if (BadSample)
                Debug.Log("[VERIFY] ---- 坏样本注入（仅 VERIFY_BAD=1）---- 4 条彼此独立的坏期望："
                          + "depth1 干净位翻成'应有异常' / Apply 后 show 期望翻成'仍关着' / "
                          + "ended 门翻成'结局回调可重复派发'（OnEnd 期望 2 次） / "
                          + "brief 自带的那条不可能断言；默认路径绝不用这些假期望");

            // 造 10 个假异常（灰盒阶段就用空 GameObject，不需要美术）
            var holder = new GameObject("verify_holder");
            var loopGo = new GameObject("verify_loop");
            var catalog = new List<Dreamcore.AnomalyEntry>();
            var objs = new List<GameObject>();
            try
            {
                for (int i = 1; i <= 10; i++)
                {
                    var on = new GameObject("on_" + i);
                    var off = new GameObject("off_" + i);
                    off.SetActive(false);
                    objs.Add(on); objs.Add(off);
                    catalog.Add(new Dreamcore.AnomalyEntry
                    {
                        id = "anom" + i.ToString("D2"),
                        title = "假异常 " + i,
                        show = new List<GameObject> { on },
                        hide = new List<GameObject> { off },
                    });
                }

                var loop = loopGo.AddComponent<Dreamcore.D3.Loop3D>();
                loop.anomalies = catalog;
                loop.cleanChance = 0f; // 本段只验抽取规则，干净层单列

                // (1) 初始深度应为 1
                Assert(failures, loop.Depth == 1, "初始深度应为 1，实际 " + loop.Depth);

                // (2) depth 1 必须干净（玩家要先见过"正常"）
                // 坏样本注入①（跑得到的分支）：真实期望恒为"无异常"，坏跑把它翻成"应有异常"。
                loop.Begin(12345);
                Assert(failures, loop.HasAnomaly == BadSample,
                       "depth 1 必须干净（HasAnomaly=" + loop.HasAnomaly + "，期望 " + BadSample + "）");

                // (3) 不重复 + 连答到好结局
                var seen = new List<string>();
                bool ended = false;
                loop.OnEnd += w => ended = w;
                for (int i = 0; i < 7 && !ended; i++)
                {
                    loop.Report(!loop.HasAnomaly); // 永远答对
                    if (loop.Current != null) seen.Add(loop.Current.id);
                }
                // 下限防退化：seed 12345 + cleanChance 0 固定抽出 6 层，只抽到 1 个的回归也必须判红。
                Assert(failures, seen.Count == new HashSet<string>(seen).Count && seen.Count >= 5,
                       "同一局内异常不得重复，抽到 " + seen.Count + " 层、去重后 "
                       + new HashSet<string>(seen).Count + " 个");
                Assert(failures, ended, "连答 7 次对应到深度 8 触发好结局，实际 ended=" + ended
                         + "、抽到 " + seen.Count + " 个异常");

                // (4) 三错即败
                loop.Begin(999);
                int winFlags = 0;
                // OnEnd 只在 Loop3D.Finish 里派发，所以"派发过几次结局"是 ended 门唯一可观察的副作用：
                // Depth 在错误答案下不会变，单靠它无法证伪（删掉 if (ended) return; 也照样读 1）。
                int endCalls = 0;
                loop.OnEnd += w => { if (!w) winFlags |= 1; endCalls++; };
                for (int i = 0; i < 3; i++) loop.Report(loop.HasAnomaly); // 永远答错
                Assert(failures, (winFlags & 1) == 1,
                       "错满 3 次必须触发坏结局，实际 winFlags=" + winFlags
                       + " Mistakes=" + loop.Mistakes);

                // (5) 结束后再判定必须被忽略（ended 门）
                // 双观测：Depth 加上 OnEnd 派发次数。Depth 单独没牙（答错永远不增量，删掉守卫也读 1），
                // 派发次数才有 —— 坏样本把期望翻成 2 次，独立证伪这一条。
                int depthAfterLose = loop.Depth;
                loop.Report(false);
                Assert(failures, loop.Depth == depthAfterLose && endCalls == (BadSample ? 2 : 1),
                       "结束后再判定必须被忽略（ended 门），实际 Depth " + loop.Depth + " vs " + depthAfterLose
                       + "、OnEnd 调用次数 " + endCalls + "（期望 " + (BadSample ? 2 : 1) + "）");

                // (6) 干净层确实会出现（否则玩家可无脑回头）
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

                // (7) Apply / Revert 真开关（按 activeSelf 断）
                var e = catalog[3];
                e.show[0].SetActive(false);
                e.hide[0].SetActive(true);
                e.Apply();
                // 坏样本注入②（跑得到的分支）：Apply 之后仍期望 show 关着（与 Apply 语义相反，必红）。
                Assert(failures, e.show[0].activeSelf == !BadSample && !e.hide[0].activeSelf,
                       "Apply 应开 show、关 hide（show.activeSelf=" + e.show[0].activeSelf
                       + " hide.activeSelf=" + e.hide[0].activeSelf + "，show 期望 " + !BadSample + "）");
                e.Revert();
                Assert(failures, !e.show[0].activeSelf && e.hide[0].activeSelf,
                       "Revert 应逐字还原（show.activeSelf=" + e.show[0].activeSelf
                       + " hide.activeSelf=" + e.hide[0].activeSelf + "）");

                // (8) brief 自带的坏样本主断言：VERIFY_BAD=1 时必须红，默认路径绝不执行。
                if (BadSample)
                    Assert(failures, false, "坏样本：这条断言必须失败");
            }
            finally
            {
                foreach (var o in objs) UnityEngine.Object.DestroyImmediate(o);
                UnityEngine.Object.DestroyImmediate(holder);
                UnityEngine.Object.DestroyImmediate(loopGo);
                Debug.Log("[VERIFY] verify_loop 与 verify_holder（含 20 个假异常物件）已 DestroyImmediate，无场景残骸");
            }

            if (failures.Count == 0)
                Debug.Log("[VERIFY] PASS: 玩法断言全过（初始深度 / depth1 干净 / 同局不重复 / 好结局 / 坏结局 / ended 门 / 干净层 / Apply-Revert 真开关）");
            else
            {
                foreach (var f in failures)
                    Debug.LogError("[VERIFY] FAIL: " + f);
                Debug.LogError("[VERIFY] FAIL: 共 " + failures.Count + " 项 —— " + string.Join(" | ", failures));
            }
        }

        /// <summary>
        /// 装配段（batchmode -executeMethod Dreamcore.EditorTools.Verify3D.VerifyAssembly）。
        /// Task 7 起验的是 Main3D.unity 的**真实 FBX 装配**：异常目录接进 Loop3D 且初始态为"正常"，
        /// 加上 M2 出口的五条真门（① Corridor(Clone) 实例根 + 25 个 MeshRenderer ② 6 盏点光收在
        /// Light_ 锚点上且带 LightFlicker ③ base 网格每个都有 Collider + sharedMaterial、show_ 网格不带碰撞体
        /// ④ active base 聚合包围盒的世界量 ⑤ extraFigure 挂 ShadeFigure。
        /// M3 起再加五条表现门：⑥ base 的 URP/Lit 材质吃到 _BaseMap+_BumpMap 且开了 _NORMALMAP 关键词
        /// （M6 起这条并入三桶门：只有 Tex 桶查贴图，Flat 桶查 Unlit，Glass 桶查透明状态）
        /// ⑦ 每张 Normal.jpg 的导入器类型必须是 NormalMap（名单在下面的 texDirs，与 MakeMats 各写一处）
        /// ⑧ Floor 按世界尺寸做 world-scale tiling
        /// ⑨ 异常件按语义给材质（窗外变黑/应急灯自亮/渗水高光泽）⑩ HUD 走 ScreenSpaceCamera
        /// 且 worldCamera 指主相机、planeDistance>nearClip。
        /// 这五条各抓一个"能编译、验收全绿、但游戏不能玩"的坑：没收养灯=全黑走廊、
        /// 没补碰撞体=穿墙走、没涂材质=刺眼导入白。
        /// 形态沿用本文件真实惯例：入口自持局部 failures + 复用既有 Assert(failures,ok,what) 三参断言 +
        /// 逐行 [VERIFY] FAIL: + 汇总行 + 单行 [VERIFY] PASS（本文件没有共享 fails/Report()，不新增）。
        /// Dreamcore.D3 的类型（Loop3D/ShadeFigure/TriggerGate/FirstPersonController/LightFlicker）一律全限定，
        /// 本文件 using 区只有 System*/UnityEditor/UnityEngine，无 Dreamcore/Dreamcore.D3；
        /// AnomalyTitles/AnomalyEntry/AnomalyResolver 在 Dreamcore 下，由 Dreamcore.EditorTools 外层命名空间直接可见。
        /// 坏样本开关沿用既有 static readonly bool BadSample（读 VERIFY_BAD），不重开环境变量。
        /// </summary>
        public static void VerifyAssembly()
        {
            var failures = new List<string>();
            // 坏样本注入的期望一律在 FAIL 行里自标"坏样本"，否则红跑日志读起来像门本身坏了
            const string BadTag = "（坏样本注入的假期望）";
            if (BadSample)
                Debug.Log("[VERIFY] ---- 坏样本注入（仅 VERIFY_BAD=1）---- 装配段 21 条彼此独立的坏期望："
                          + "MeshRenderer 95→99 / base·anom 二分 86+9→99 / Light 组件 6→99 / "
                          + "base 缺碰撞体 0→1 / base 材质为 null 0→1 / "
                          + "聚合 min.z 容差 [-0.3,0.3]→[23,24] / extraFigure 的 ShadeFigure 期望翻成'不存在' / "
                          + "灯与灯管中心距离容差 0.4m→0m / 异常表条数 10→999 / "
                          + "未登记 base 名允许数 0→1 / Tex 桶材质数 75→99 / 桶型不符允许数 0→1 / "
                          + "契约网格缺席允许数 0→1 / base 缺 _BaseMap 0→1 / base 未开 _NORMALMAP 0→1 / "
                          + "Floor tiling 容差 [10,100]→[99,100] / HUD 渲染模式期望翻成 Overlay / "
                          + "相机 renderPostProcessing 期望翻成'关着才对' / "
                          + "anomalyCue 期望翻成'未赋值' / 循环环境音路数 1→2 / "
                          + "杂物 _BaseMap tiling 期望从'保持 (1,1)'翻成'应被改写'；默认路径绝不用这些假期望");
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/Main3D.unity",
                UnityEditor.SceneManagement.OpenSceneMode.Single);

            var loop = UnityEngine.Object.FindFirstObjectByType<Dreamcore.D3.Loop3D>();
            Assert(failures, loop != null, "Main3D 里必须有 Loop3D");
            if (loop == null)
            {
                foreach (var f in failures) Debug.LogError("[VERIFY] FAIL: " + f);
                Debug.LogError("[VERIFY] FAIL: 共 " + failures.Count + " 项（无 Loop3D，装配段终止）");
                return;
            }

            Assert(failures, loop.anomalies.Count == 10, "异常表应有 10 条，实际 " + loop.anomalies.Count);

            var ids = new HashSet<string>();
            int refsTotal = 0;
            foreach (var e in loop.anomalies)
            {
                Assert(failures, !string.IsNullOrEmpty(e.id) && ids.Add(e.id), "id 非空且不重复：" + e.id);
                Assert(failures, AnomalyTitles.Table.ContainsKey(e.id), "id 在标题表里登记过：" + e.id);
                Assert(failures, !string.IsNullOrEmpty(e.title), "title 非空：" + e.id);
                Assert(failures, e.show.Count + e.hide.Count > 0, "至少指认一个物体：" + e.id);

                foreach (var g in e.show)
                {
                    refsTotal++;
                    Assert(failures, g != null, "show 引用非空：" + e.id);
                    if (g != null) Assert(failures, !g.activeSelf, "show 物体初始必须关着（异常未出现）：" + e.id + "/" + g.name);
                }
                foreach (var g in e.hide)
                {
                    refsTotal++;
                    Assert(failures, g != null, "hide 引用非空（指认失败会被丢弃，这里必须是真的）：" + e.id);
                    if (g != null) Assert(failures, g.activeSelf, "hide 目标初始必须开着：" + e.id + "/" + g.name);
                }
            }
            Assert(failures, refsTotal == AnomShowNames.Length + AnomHideNames.Length,
                   "10 组异常应逐字指认 " + AnomShowNames.Length + " show + " + AnomHideNames.Length
                   + " hide = " + (AnomShowNames.Length + AnomHideNames.Length) + " 个物体，实际 " + refsTotal);

            // 常驻黑影必须已经消失（显隐权交回目录）。Include 才抓得到"又亮起来的常驻人影"——
            // 默认(Exclude)重载会滤掉 inactive，正确装配下反查到 0 个、断言空过没牙（Task 5 裁定 3）。
            foreach (var sf in UnityEngine.Object.FindObjectsByType<Dreamcore.D3.ShadeFigure>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                Assert(failures, !sf.gameObject.activeSelf, "常驻黑影不该存在：" + sf.name);

            // 判定门与玩家：一条前进 + 一条回头（两个门都是 active，brief 口径不动）
            var gates = UnityEngine.Object.FindObjectsByType<Dreamcore.D3.TriggerGate>(FindObjectsSortMode.None);
            int fwd = 0, back = 0;
            foreach (var g in gates) { if (g.isForwardGate) fwd++; else back++; }
            Assert(failures, fwd == 1 && back == 1, "两端判定门各一个，实际 前" + fwd + "/后" + back);
            var fpc = UnityEngine.Object.FindFirstObjectByType<Dreamcore.D3.FirstPersonController>();
            Assert(failures, fpc != null && fpc.GetComponent<CharacterController>() != null, "玩家带 CharacterController");
            Assert(failures, Camera.main != null, "Main Camera 在位");

            // ---------- M2 装配真门（Task 7：灰盒换 FBX 后，灯/碰撞体/材质三件事没人做也会全绿）----------
            // 期望值全部从本文件顶部的 M6 契约名单派生（不再抄 logs 里的魔数）；
            // 尺寸门一律走 Renderer.bounds，因为只有世界量是米（spec §9.2），
            // 不读 Transform/Mesh.bounds/BoxCollider.size。
            // 坏样本注入的条目见本方法开头的汇总行（装配段 21 条，①MeshRenderer 95→99 ②灯数 6→99
            // ③base 缺碰撞体 0→1 ④base 材质为 null 0→1 ⑤聚合 min.z 容差 [-0.3,0.3]→[23,24]
            // ⑥extraFigure 的 ShadeFigure 期望翻成"不存在" ⑦灯与灯管距离 0.4m→0m ……）。
            // 默认路径一条都不放宽。
            var inst = GameObject.Find("Corridor(Clone)");
            Assert(failures, inst != null, "场景里存在 FBX 实例根 Corridor(Clone)");
            var allRenderers = inst != null
                ? inst.GetComponentsInChildren<MeshRenderer>(true) : new MeshRenderer[0];
            Assert(failures, allRenderers.Length == (BadSample ? 99 : BaseMeshNames.Length + AnomShowNames.Length),
                   "Corridor(Clone) 下 MeshRenderer 应为 " + (BaseMeshNames.Length + AnomShowNames.Length)
                   + "（" + BaseMeshNames.Length + " base 网格 + " + AnomShowNames.Length + " show），实际 "
                   + allRenderers.Length + (BadSample ? BadTag : ""));

            int baseCount = 0, anomCount = 0, missingCollider = 0, nullMaterial = 0, showWithCollider = 0;
            var agg = new Bounds(Vector3.zero, Vector3.zero);
            bool aggFirst = true;
            foreach (var mr in allRenderers)
            {
                if (AnomalyResolver.IsAnomalyWireName(mr.name))
                {
                    anomCount++;
                    if (mr.GetComponent<Collider>() != null) showWithCollider++;
                    continue; // 异常件不该变成隐形墙：不要求碰撞体
                }
                baseCount++;
                if (mr.GetComponent<Collider>() == null) missingCollider++;
                if (mr.sharedMaterial == null) nullMaterial++;
                if (!mr.gameObject.activeInHierarchy) continue; // 聚合只数 active 的 base 网格
                if (aggFirst) { agg = mr.bounds; aggFirst = false; }
                else agg.Encapsulate(mr.bounds);
            }
            Debug.Log("[VERIFY] base 网格=" + baseCount + " anom 网格=" + anomCount
                      + "；active base 聚合包围盒 size=" + agg.size + " min=" + agg.min + " max=" + agg.max);
            Assert(failures, baseCount == (BadSample ? 99 : BaseMeshNames.Length)
                       && anomCount == AnomShowNames.Length,
                   "按 IsAnomalyWireName 二分应为 base " + BaseMeshNames.Length + " / anom "
                   + AnomShowNames.Length + "，实际 " + baseCount + "/" + anomCount + (BadSample ? BadTag : ""));

            // ③ base 每个都要有碰撞体与材质（FBX 两者都不带，靠 Bootstrap3D.PaintShell 补）
            Assert(failures, missingCollider == (BadSample ? 1 : 0),
                   "base 网格缺碰撞体的个数应为 0（否则穿墙走），实际 " + missingCollider
                   + (BadSample ? BadTag : ""));
            Assert(failures, nullMaterial == (BadSample ? 1 : 0),
                   "base 网格 sharedMaterial 为 null 的个数应为 0（否则刺眼导入白），实际 " + nullMaterial
                   + (BadSample ? BadTag : ""));
            Assert(failures, showWithCollider == 0,
                   "anom 的 show_ 网格不应带 Collider（异常件不是隐形墙），实际 " + showWithCollider);

            // ② 灯：组件真相在 Unity、位置真相在 Blender，点光必须收在 Light_ 锚点上，
            //    而锚点必须落在 Blender 建好的那套灯具上（灯与灯座错位的症状是"光从半空打来"）。
            var sceneLights = UnityEngine.Object.FindObjectsByType<Light>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert(failures, sceneLights.Length == (BadSample ? 99 : BaseEmptyNames.Length),
                   "场景内 Light 组件应为 " + BaseEmptyNames.Length + " 盏，实际 "
                   + sceneLights.Length + (BadSample ? BadTag : ""));
            var wantLightNames = new HashSet<string>(BaseEmptyNames);
            foreach (var lt in sceneLights)
            {
                string host = lt.gameObject.name;
                Assert(failures, wantLightNames.Contains(host), "灯的宿主名必须是 Light_ 灯位锚点：" + host);
                Assert(failures, lt.type == LightType.Point, "灯应为点光，宿主 " + host + " 实际 " + lt.type);
                Assert(failures, Mathf.Approximately(lt.range, 7f), "灯 range 应为 7f，宿主 " + host + " 实际 " + lt.range);
                Assert(failures, lt.gameObject.GetComponent<Dreamcore.D3.LightFlicker>() != null,
                       "灯宿主必须挂 LightFlicker（不会闪的灯等于没有灯闪）：" + host);
                var tube = host.StartsWith("Light_") && inst != null
                    ? FindByName(inst.transform, "Lamp_" + host.Substring("Light_".Length)) : null;
                var tubeRend = tube != null ? tube.GetComponent<Renderer>() : null;
                Assert(failures, tubeRend != null, host + " 应有一根对应的 Lamp_ 灯管网格，实际 "
                       + (tubeRend == null ? "缺" : tubeRend.name));
                if (tubeRend != null)
                {
                    float d = Vector3.Distance(lt.transform.position, tubeRend.bounds.center);
                    Assert(failures, d < (BadSample ? 0f : 0.4f), host + " 与 " + tubeRend.name
                           + " 灯管中心距离应 <" + (BadSample ? "0" : "0.4") + "m（实测 "
                           + d.ToString("F3") + "m）" + (BadSample ? BadTag : ""));
                }
                Debug.Log("[VERIFY] 灯 " + host + " world=" + lt.transform.position
                          + " lossyScale=" + lt.transform.lossyScale
                          + " 距灯管=" + (tubeRend == null ? "无灯管"
                              : Vector3.Distance(lt.transform.position, tubeRend.bounds.center).ToString("F3") + "m"));
            }

            // ④ 世界量门：走廊必须沿 +Z 铺开，尺寸是"人住的楼"而不是 100 倍通道
            // 宽度的旧口径 [2.6,3.3]（实测 2.91）是错的：那时窗外楼板的包围盒露在墙外约 0.5m，
            // 把聚合宽度撑了出来。M3 把窗板收进墙体内（half_w-0.06）后，真实净宽 2.50m ——
            // 2.5m 也才是老居民楼走廊该有的尺寸，2.91m 反倒不合理。
            Assert(failures, agg.size.x >= 2.4f && agg.size.x <= 2.6f,
                   "聚合宽应落在 [2.4,2.6]m（窗板收进墙体后的真实净宽，实测 2.50；旧口径 2.91 是被露在墙外的窗板撑出来的），实际 " + agg.size.x);
            Assert(failures, agg.size.y >= 2.8f && agg.size.y <= 3.4f,
                   "聚合高应落在 [2.8,3.4]m（实测 3.10），实际 " + agg.size.y);
            Assert(failures, agg.size.z >= 23.5f && agg.size.z <= 24.6f,
                   "聚合长应落在 [23.5,24.6]m（实测 24.05），实际 " + agg.size.z);
            float minZLo = BadSample ? 23.0f : -0.3f;
            float minZHi = BadSample ? 24.0f : 0.3f;
            Assert(failures, agg.min.z >= minZLo && agg.min.z <= minZHi,
                   "走廊起点 min.z 应落在 [" + minZLo + "," + minZHi + "]（走廊必须往 +Z 延伸，实测 0.00）"
                   + (BadSample ? BadTag : "") + "，实际 " + agg.min.z);

            // ⑤ 人影 billboard 组件（FBX 给不了，只能 Unity 挂）。
            // show_ 节点在正常态是 inactive，GameObject.Find 只认 active 物件、会假报"节点缺失"，
            // 所以走本文件的 FindByName（递归含 inactive 子节点）。
            var figT = inst != null ? FindByName(inst.transform, "anom02_show_extraFigure") : null;
            var figShade = figT != null ? figT.GetComponent<Dreamcore.D3.ShadeFigure>() : null;
            Assert(failures, (figShade != null) != BadSample,
                   "anom02_show_extraFigure 上应挂 ShadeFigure（billboard），实际 "
                   + (figShade != null ? "已挂" : "缺失")
                   + (BadSample ? BadTag : "") + "，宿主 " + (figT == null ? "节点缺失" : figT.name));

            // ---------- M3 表现段真门 + M6 三桶登记门 ----------
            // 抓的是"能编译、装配门全绿、但画面不对"的坑：
            // ①base 材质没吃到 BaseMap/法线 → 灰白塑料感；②没开 _NORMALMAP → 资产在、关键词不在，
            // 法线贴了等于没贴（SetTexture 不会替你开关键词）；③tile 没按世界尺寸走 → 24m 地面和
            // 0.18m 布鞋各画一遍同样的贴图；④HUD 还在 Overlay → 后处理链吃不到 UI，梦核滤镜白做。
            // M6 把它从"Lit + 一个例外"升级成**三桶**：Tex（带贴图的 URP/Lit）/ Flat（URP/Unlit 纯色，
            // 自发光灯管、安全出口牌、门斗深处的黑、窗外衬板）/ Glass（URP/Lit 且 _Surface=透明）。
            // 每个 base 网格必须且只能落进一桶，桶由本文件的 Flat/Glass 名单决定——这张表与
            // Bootstrap3D.SurfFor 各写一处、必须互相吻合，才抓得住"新件只在装配侧登记"的单边漂移。
            // 坏样本注入的假期望：Lit 桶数 75→99 / 桶型不符 0→1 / 缺图数 0→1 / 缺关键词数 0→1 /
            // 契约网格缺席 0→1 / Floor tile 期望翻成 [99,100] / HUD 期望翻成 Overlay。默认路径一条都不放宽。
            const string TexRoot = "Assets/Content/Textures";
            const string LitShader = "Universal Render Pipeline/Lit";
            const string UnlitShader = "Universal Render Pipeline/Unlit";
            int texOk = 0, flatOk = 0, glassOk = 0, noBaseMap = 0, noBump = 0, noKeyword = 0, wrongBucket = 0;
            var unregistered = new List<string>();
            var wrongBucketNames = new List<string>();
            foreach (var mr in allRenderers)
            {
                if (AnomalyResolver.IsAnomalyWireName(mr.name)) continue; // 语义件走 AnomSurf，不在这张表里
                var mt = mr.sharedMaterial;
                string bucket = BucketOf(mr.name);
                if (bucket == "Unknown") { unregistered.Add(mr.name); continue; }
                bool okShape;
                if (bucket == "Glass")
                {
                    glassOk++;
                    // 光设 alpha 不透明、光设 _Surface 不开关键词、开了关键词不进透明队列，三种都是"看着像灰板"
                    okShape = mt != null && mt.shader.name == LitShader && mt.GetFloat("_Surface") >= 1f
                              && mt.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT")
                              && mt.renderQueue >= 3000;
                }
                else if (bucket == "Flat")
                {
                    flatOk++;
                    okShape = mt != null && mt.shader.name == UnlitShader;
                }
                else
                {
                    texOk++;
                    okShape = mt != null && mt.shader.name == LitShader;
                    if (okShape)
                    {
                        if (mt.GetTexture("_BaseMap") == null) noBaseMap++;
                        if (mt.GetTexture("_BumpMap") == null) noBump++;
                        if (!mt.IsKeywordEnabled("_NORMALMAP")) noKeyword++;
                    }
                }
                if (!okShape)
                    wrongBucketNames.Add(mr.name + "(该走 " + bucket + "，实为 "
                                         + (mt == null ? "null" : mt.shader.name
                                            + (bucket == "Glass" ? " _Surface=" + mt.GetFloat("_Surface") : "")) + ")");
            }
            wrongBucket = wrongBucketNames.Count;
            int wantTex = BaseMeshNames.Length - FlatBaseNames.Length - GlassBaseNames.Length;
            Assert(failures, unregistered.Count == (BadSample ? 1 : 0),
                   "每个 base 网格都必须登记进契约名单（未登记即红，不许默默兜底成墙色），实际越界 "
                   + unregistered.Count + (unregistered.Count == 0 ? "" : "（" + string.Join(",", unregistered.ToArray()) + "）")
                   + (BadSample ? BadTag : ""));
            Assert(failures, texOk == wantTex && flatOk == FlatBaseNames.Length && glassOk == GlassBaseNames.Length,
                   "base 材质三桶应为 Tex " + wantTex + " / Flat " + FlatBaseNames.Length + " / Glass "
                   + GlassBaseNames.Length + "（合计 " + BaseMeshNames.Length + "），实际 "
                   + texOk + "/" + flatOk + "/" + glassOk);
            Assert(failures, wrongBucket == (BadSample ? 1 : 0),
                   "每件 base 的实际 shader 必须与它登记的桶一致，实际不符 " + wrongBucket
                   + (wrongBucket == 0 ? "" : "（" + string.Join(" | ", wrongBucketNames.ToArray()) + "）")
                   + (BadSample ? BadTag : ""));
            // 表的另一头：登记了却不在场景里＝名单过期（会把 wantTex 的算术蒙对，所以单独查）
            var staleBucket = new List<string>();
            foreach (var n in FlatBaseNames)
                if (FindByName(inst.transform, n) == null) staleBucket.Add("Flat/" + n);
            foreach (var n in GlassBaseNames)
                if (FindByName(inst.transform, n) == null) staleBucket.Add("Glass/" + n);
            Assert(failures, staleBucket.Count == 0,
                   "三桶名单里登记的名字都得真在场景里（表不能过期），实际缺席 " + staleBucket.Count
                   + (staleBucket.Count == 0 ? "" : "（" + string.Join(",", staleBucket.ToArray()) + "）"));
            var absentBase = new List<string>();
            foreach (var n in BaseMeshNames)
            {
                var t = FindByName(inst.transform, n);
                if (t == null || t.GetComponent<MeshRenderer>() == null) absentBase.Add(n);
            }
            Assert(failures, absentBase.Count == (BadSample ? 1 : 0),
                   "契约名单里 " + BaseMeshNames.Length + " 个 base 网格应逐个在场景里且有 MeshRenderer，实际缺席 "
                   + absentBase.Count + (absentBase.Count == 0 ? "" : "（" + string.Join(",", absentBase.ToArray()) + "）")
                   + (BadSample ? BadTag : ""));
            Debug.Log("[VERIFY] base 三桶: Tex=" + texOk + " Flat=" + flatOk + " Glass=" + glassOk
                      + "；桶型不符=" + wrongBucket + " 未登记=" + unregistered.Count);

            Assert(failures, noBaseMap == (BadSample ? 1 : 0),
                   "base Lit 材质缺 _BaseMap 的个数应为 0，实际 " + noBaseMap + (BadSample ? BadTag : ""));
            Assert(failures, noBump == 0, "base Lit 材质缺法线图的个数应为 0，实际 " + noBump);
            Assert(failures, noKeyword == (BadSample ? 1 : 0),
                   "base Lit 材质未开 _NORMALMAP 的个数应为 0（资产在但关键词不在＝法线白贴），实际 "
                   + noKeyword + (BadSample ? BadTag : ""));

            // 法线图若还是普通图导入（sRGB 采样），凹凸方向全错——这项查导入器，不查材质。
            // 名单与 Bootstrap3D.MakeMats 的 dir 参数**故意各写一处**：换贴图时两边都要改，
            // 漏掉任何一边都会在这里红（工程侧漏改＝材质指向不存在的目录，门禁侧漏改＝新目录没人查导入器）。
            var texDirs = new List<string> { "Terrazzo002", "Plaster007", "Tiles032", "PaintedMetal006",
                                             "Wood073", "Concrete002", "Paper003" };
            texDirs.AddRange(PropTexDirs);   // 道具那 13 张同理：法线图按普通图导入＝凹凸方向全错
            int badNormalImport = 0;
            foreach (var d in texDirs)
            {
                var imp = AssetImporter.GetAtPath(TexRoot + "/" + d + "/Normal.jpg") as TextureImporter;
                if (imp == null || imp.textureType != TextureImporterType.NormalMap) badNormalImport++;
            }
            Assert(failures, badNormalImport == 0,
                   texDirs.Count + " 张 Normal.jpg 都应导入为 NormalMap，实际不合格 " + badNormalImport);

            // 道具走自己的 UV：Bootstrap3D 给 Prop_ 材质的 tileMeters=0，所以 _BaseMap 的 ST 必须还是 (1,1)。
            // 这条专门抓"顺手给道具也开了 world-scale tiling"——症状是灭火器上的说明标签、
            // 挂钟的表盘被按米裁掉一半，而贴图在位、关键词开着、三桶计数全正常，别的门一条都不会红。
            int propTiled = 0;
            foreach (var mr in allRenderers)
            {
                if (!mr.name.StartsWith("Prop_")) continue;
                var pm = mr.sharedMaterial;
                if (pm == null) continue;                     // 材质为 null 由上面那条门管
                var st = pm.GetTextureScale("_BaseMap");
                if (Mathf.Abs(st.x - 1f) > 0.001f || Mathf.Abs(st.y - 1f) > 0.001f) propTiled++;
            }
            Assert(failures, propTiled == (BadSample ? 99 : 0),
                   PropBaseNames.Length + " 件杂物的 _BaseMap tiling 应保持 (1,1)（道具用自身 UV），实际被改写 "
                   + propTiled + (BadSample ? BadTag : ""));

            // world-scale tiling：Floor 是唯一的大面，实测世界长 24.05m / tile 2m ⇒ ST.x≈12
            var tFloor2 = inst != null ? FindByName(inst.transform, "Floor") : null;
            var mrFloor = tFloor2 != null ? tFloor2.GetComponent<MeshRenderer>() : null;
            var floorSt = mrFloor != null && mrFloor.sharedMaterial != null
                ? mrFloor.sharedMaterial.GetTextureScale("_BaseMap") : Vector2.zero;
            float tileLo = BadSample ? 99f : 10f, tileHi = 100f;
            Assert(failures, floorSt.x >= tileLo && floorSt.x <= tileHi,
                   "Floor 的 _BaseMap tiling.x 应落在 [" + tileLo + "," + tileHi + "]（24.05m/2m≈12，实测 "
                   + floorSt + "）" + (BadSample ? BadTag : "") + "，实际 " + floorSt.x);
            Debug.Log("[VERIFY] Floor bounds=" + (mrFloor != null ? mrFloor.bounds.size.ToString() : "无")
                      + " tile=" + floorSt + " 材质=" + (mrFloor != null && mrFloor.sharedMaterial != null ? mrFloor.sharedMaterial.name : "null"));

            // 异常语义色：窗外套窗的 anom10 必须"变黑"，应急灯必须"自己亮"，渗水必须有高光
            Debug.Log("[VERIFY] ---- 异常件语义读数（材质色 + 光泽，可辨识性的实测依据）----");
            foreach (var pair in new[] {
                         new { n = "anom01_show_emergencyLight", wantDark = false },
                         new { n = "anom02_show_extraFigure", wantDark = true },
                         new { n = "anom08_show_WaterPuddle", wantDark = false },
                         new { n = "anom10_show_WindowBuildings_open", wantDark = true } })
            {
                var go = inst != null ? FindByName(inst.transform, pair.n) : null;
                var mt = go != null && go.GetComponent<MeshRenderer>() != null
                    ? go.GetComponent<MeshRenderer>().sharedMaterial : null;
                if (mt == null) { Assert(failures, false, "异常件缺材质：" + pair.n); continue; }
                var c = mt.GetColor("_BaseColor");
                float lum = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                float gloss = mt.HasProperty("_Smoothness") ? mt.GetFloat("_Smoothness") : 0f;
                Debug.Log("[VERIFY] " + pair.n + " 材质=" + mt.name + " shader=" + mt.shader.name
                          + " BaseColor=" + c + " 亮度=" + lum.ToString("F2") + " smoothness=" + gloss.ToString("F2"));
                if (pair.wantDark)
                    Assert(failures, lum <= 0.10f, pair.n + " 应近黑（亮度<=0.10），实际 " + lum.ToString("F2"));
            }
            var waterGo = inst != null ? FindByName(inst.transform, "anom08_show_WaterPuddle") : null;
            var waterMt = waterGo != null && waterGo.GetComponent<MeshRenderer>() != null
                ? waterGo.GetComponent<MeshRenderer>().sharedMaterial : null;
            Assert(failures, waterMt != null && waterMt.GetFloat("_Smoothness") >= 0.8f,
                   "anom08 渗水应有高光泽（smoothness>=0.8）才在点光下反光，实际 "
                   + (waterMt == null ? "无材质" : waterMt.GetFloat("_Smoothness").ToString("F2")));

            // HUD 渲染模式（spec §8 第 4 项裁定 ScreenSpaceCamera）
            var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            Assert(failures, canvas != null, "场景里有 HUD Canvas");
            bool wantOverlay = BadSample;
            Assert(failures, canvas != null && (canvas.renderMode == RenderMode.ScreenSpaceCamera) != wantOverlay,
                   "HUD Canvas 应为 ScreenSpaceCamera（Overlay 的 UI 不进后处理链），实际 "
                   + (canvas == null ? "无 Canvas" : canvas.renderMode.ToString()) + (BadSample ? BadTag : ""));
            var hudCam = Camera.main;
            Assert(failures, canvas != null && hudCam != null && canvas.worldCamera == hudCam
                       && canvas.planeDistance > hudCam.nearClipPlane,
                   "HUD 的 worldCamera 必须指主相机且 planeDistance>nearClip（否则整块 UI 被裁掉不显示），实际 plane="
                   + (canvas == null ? "无" : canvas.planeDistance.ToString()));

            // ---------- M3 后处理真门（实机截图逼出来的：Profile 出厂是空的）----------
            // 实机跑起来看画面才发现"复用 2D 梦核滤镜"这句话在资产层面不成立：
            // VolumeProfile-Dreamcore.asset 的 components 是 []，GlobalVolume 挂的是个空壳，
            // 相机 renderPostProcessing 开了也没东西可评。装配门全绿、游戏照跑、滤镜整条链不存在。
            // 所以这里查三件事：①场景里有全局 Volume 且 profile 非空 ②profile 里真有五道 override
            // 且四个强度都 >0（Add 了但没 Override 等于没加）③相机的 renderPostProcessing 真开着。
            // 坏样本注入第 15 条：③的期望翻成"关着才对"。
            var vol = UnityEngine.Object.FindFirstObjectByType<UnityEngine.Rendering.Volume>();
            Assert(failures, vol != null && vol.isGlobal && vol.sharedProfile != null,
                   "场景里要有全局 Volume 且挂了 Profile，实际 "
                   + (vol == null ? "无 Volume" : (vol.sharedProfile == null ? "Profile 为 null" : "isGlobal=" + vol.isGlobal)));
            var prof = vol != null ? vol.sharedProfile : null;
            int overrides = prof != null ? prof.components.Count : 0;
            Assert(failures, overrides >= 5,
                   "梦核滤镜 Profile 至少要有 5 道 override（Bloom/暗角/颗粒/调色/色散），实际 " + overrides);
            if (prof != null)
            {
                UnityEngine.Rendering.Universal.Bloom bl;
                UnityEngine.Rendering.Universal.Vignette vg;
                UnityEngine.Rendering.Universal.FilmGrain fg;
                UnityEngine.Rendering.Universal.ChromaticAberration cb;
                Assert(failures, prof.TryGet<UnityEngine.Rendering.Universal.Bloom>(out bl) && bl.intensity.value > 0f,
                       "Bloom 要存在且 intensity>0（灯不发光就不是梦核），实际 "
                       + (bl == null ? "无" : bl.intensity.value.ToString("F2")));
                Assert(failures, prof.TryGet<UnityEngine.Rendering.Universal.Vignette>(out vg) && vg.intensity.value > 0f,
                       "Vignette 要存在且 intensity>0，实际 " + (vg == null ? "无" : vg.intensity.value.ToString("F2")));
                Assert(failures, prof.TryGet<UnityEngine.Rendering.Universal.FilmGrain>(out fg) && fg.intensity.value > 0f,
                       "FilmGrain 要存在且 intensity>0，实际 " + (fg == null ? "无" : fg.intensity.value.ToString("F2")));
                Assert(failures, prof.TryGet<UnityEngine.Rendering.Universal.ChromaticAberration>(out cb) && cb.intensity.value > 0f,
                       "ChromaticAberration 要存在且 intensity>0，实际 " + (cb == null ? "无" : cb.intensity.value.ToString("F2")));
            }
            var camPP = Camera.main != null
                ? Camera.main.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>() : null;
            Assert(failures, camPP != null && camPP.renderPostProcessing != BadSample,
                   "主相机 renderPostProcessing 应为开（关了就绕过整条后处理链）"
                   + (BadSample ? BadTag : "") + "，实际 " + (camPP == null ? "无 URP 相机数据" : camPP.renderPostProcessing.ToString()));
            Debug.Log("[VERIFY] 后处理: Volume=" + (vol == null ? "无" : "全局") + " override 数=" + overrides
                      + " renderPostProcessing=" + (camPP == null ? "无" : camPP.renderPostProcessing.ToString()));

            // ---------- M3 音频门 ----------
            // 4 个合成 wav 从 M1 起就躺在 Assets/_Project/Audio 里没人调用＝资产在、游戏哑。
            // 这三条查的是"装配时真的把 clip 接到了组件上"，不查播放行为（PlayClipAtPoint 造的临时
            // AudioSource 在编辑态不可见，运行期才有）。
            var fpcAudio = UnityEngine.Object.FindFirstObjectByType<Dreamcore.D3.FirstPersonController>();
            Assert(failures, fpcAudio != null && fpcAudio.footstepClip != null,
                   "玩家 footstepClip 必须已赋值（走路没声＝脚下感全丢），实际 "
                   + (fpcAudio == null ? "无 FPC" : (fpcAudio.footstepClip == null ? "clip 为 null" : fpcAudio.footstepClip.name)));
            Assert(failures, (loop.anomalyCue != null) != BadSample,
                   "Loop3D.anomalyCue 必须已赋值" + (BadSample ? BadTag : "") + "，实际 "
                   + (loop.anomalyCue == null ? "null" : loop.anomalyCue.name));
            Assert(failures, loop.decisionClip != null,
                   "Loop3D.decisionClip 必须已赋值，实际 "
                   + (loop.decisionClip == null ? "null" : loop.decisionClip.name));

            var srcs = UnityEngine.Object.FindObjectsByType<AudioSource>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            int ambientLoops = 0;
            foreach (var s in srcs)
                if (s != null && s.clip != null && s.loop && s.playOnAwake && Mathf.Approximately(s.spatialBlend, 0f))
                    ambientLoops++;
            Assert(failures, ambientLoops == (BadSample ? 2 : 1),
                   "场景里应有且只有 1 路循环环境音（clip+loop+playOnAwake+2D 定位），实际 "
                   + ambientLoops + (BadSample ? BadTag : ""));
            Debug.Log("[VERIFY] 音频接线: footstep="
                      + (fpcAudio != null && fpcAudio.footstepClip != null ? fpcAudio.footstepClip.name : "缺")
                      + " anomalyCue=" + (loop.anomalyCue == null ? "缺" : loop.anomalyCue.name)
                      + " decision=" + (loop.decisionClip == null ? "缺" : loop.decisionClip.name)
                      + " 环境音路数=" + ambientLoops);

            // 坏样本：VERIFY_BAD=1 时把 10 条翻成 999，独立成一行 FAIL（默认路径绝不执行）。
            if (BadSample)
                Assert(failures, loop.anomalies.Count == 999, "坏样本：这条断言必须失败");

            if (failures.Count == 0)
                Debug.Log("[VERIFY] PASS: 装配断言全过（10 条目录 + show 关/hide 开 + 无常驻黑影 + 两端门 + 玩家 + 相机"
                          + " + Corridor(Clone) " + (BaseMeshNames.Length + AnomShowNames.Length) + " 网格"
                          + " + " + BaseEmptyNames.Length + " 盏点光收在灯管旁且带 LightFlicker"
                          + " + base 碰撞体与材质齐 + 聚合世界量 + extraFigure billboard"
                          + " + base 三桶登记齐（Tex " + (BaseMeshNames.Length - FlatBaseNames.Length - GlassBaseNames.Length)
                          + "/Flat " + FlatBaseNames.Length + "/Glass " + GlassBaseNames.Length
                          + "）且 Tex 桶吃到贴图/法线/_NORMALMAP、Glass 桶真是透明"
                          + " + Floor world-scale tiling + " + texDirs.Count + " 张 NormalMap 导入器 + 异常语义色与渗水光泽"
                          + " + " + PropBaseNames.Length + " 件杂物走自身 UV（tiling 保持 1,1）"
                          + " + HUD ScreenSpaceCamera + 梦核滤镜 5 道 override 与相机后处理开关"
                          + " + 音效三路接线与一路循环环境音）");
            else
            {
                foreach (var f in failures)
                    Debug.LogError("[VERIFY] FAIL: " + f);
                Debug.LogError("[VERIFY] FAIL: 共 " + failures.Count + " 项 —— " + string.Join(" | ", failures));
            }
        }

        /// <summary>
        /// M2 建模段验收口（batchmode -executeMethod Dreamcore.EditorTools.Verify3D.VerifyFbx）。
        /// 只验 Corridor.fbx 资产本身，不碰 Main3D 的装配（灰盒继续顶着，M1 提交物完好；切装配是 Task 7）：
        /// 把 prefab 实例化进临时场景根，套 Bootstrap3D.FbxYaw，然后直接跑 AnomalyResolver。
        /// 契约形态（Task 1 实测 + §9.1）：Blender 集合从不进 FBX，异常语义只活在对象名前缀里，
        /// 所以这里断言的是"名字集 + 用 AnomalyResolver.IsAnomalyWireName 做 base/anom 二分"，
        /// 不是 base/anom 节点（节点版断言已被证伪，写进去就是恒假的门）；也不复制正则。
        /// 尺寸断言一律走 Renderer.bounds：FBX 导入节点上只有世界量是米（§9.2）——
        /// Transform.localScale 读 100、Mesh.bounds 读 1/100，两者都不是米。逐节点打证据行
        /// （自身是 EMPTY / 父级是 EMPTY 全覆盖），把 Task 1 交下来的"非对称网格"与"Empty 父级"
        /// 两个疑问用读数结掉，不留假设。
        /// 形态沿用本文件真实惯例：入口自持局部 failures + 复用既有 Assert(failures,ok,what) 与
        /// FindByName(Transform,string)（复用不重定义，重定义＝CS0111）+ 逐行 [VERIFY] FAIL: + 汇总行
        /// + 单行 [VERIFY] PASS；Dreamcore.D3.* 全限定；裸 Object 有歧义 → 一律 UnityEngine.Object.
        /// 坏样本沿用既有 static readonly bool BadSample（不重读环境变量）：一次注入 7 条彼此独立的
        /// 坏期望，各成一红互不短路 —— ①组数 10→3；②anom01 hide 指认翻成 99；③base 期望名里塞一个
        /// 绝不存在的 no_such_base_xyz；④高度容差 2.5–3.6m 翻成 90–110m；⑤未登记名允许数 0→1；
        /// ⑥hide 落点越界数 0→1；⑦灯与灯管距离容差 0.4m 翻成 0m。默认路径期望值全是真实值。
        /// 临时物件在 finally 里 DestroyImmediate，不留场景残骸、不改资产。
        /// </summary>
        public static void VerifyFbx()
        {
            var failures = new List<string>();
            const string CorridorPath = "Assets/Content/3D/Corridor.fbx";   // 路径只写这一处
            // 坏样本注入的期望一律在 FAIL 行里自标"坏样本"，否则红跑日志读起来像门本身坏了
            // （"应指认 6 盏灯，实际 6"这种行既不说期望被翻过、也grep不到坏样本）。
            const string BadTag = "（坏样本注入的假期望）";
            const string NoSuchBareName = "no_such_base_xyz";
            if (BadSample)
                Debug.Log("[VERIFY] ---- 坏样本注入（仅 VERIFY_BAD=1）---- 7 条彼此独立的坏期望："
                          + "异常组数 10→3 / anom01 hide 指认数翻成 99 / base 期望名塞一个绝不存在的"
                          + " " + NoSuchBareName + " / 高度容差 2.5-3.6m 翻成 90-110m / "
                          + "未登记名允许数 0→1 / hide 落点越界数 0→1 / 灯距容差 0.4m 翻成 0m；"
                          + "另有五条结构门（洞中心被墙覆盖、覆盖件盖不住洞口、杂物跨中线、"
                          + "杂物通行带 1.05m 翻成 3.0m 且合格数期望 0→99、贴墙杂物堵门洞/窗洞）在坏跑里翻成反向期望；默认路径绝不用这些假期望");

            AssetDatabase.ImportAsset(CorridorPath, ImportAssetOptions.ForceSynchronousImport);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(CorridorPath);
            Assert(failures, asset != null, "Corridor.fbx 已导入为 GameObject 资产：" + CorridorPath);
            if (asset == null)
            {
                foreach (var f in failures) Debug.LogError("[VERIFY] FAIL: " + f);
                Debug.LogError("[VERIFY] FAIL: 共 " + failures.Count + " 项（FBX 资产缺失，VerifyFbx 终止）");
                return;
            }

            var tmp = new GameObject("verify_fbx_stage");
            try
            {
                var inst = UnityEngine.Object.Instantiate(asset);
                inst.transform.SetParent(tmp.transform, false);
                // 轴向补偿的唯一真相是 Bootstrap3D.FbxYaw（Bootstrap3D.cs 里唯一落点），这里跟着它转，别自己写角度
                inst.transform.localRotation = Quaternion.Euler(0f, Bootstrap3D.FbxYaw, 0f);

                Assert(failures, inst.transform.name.StartsWith("Corridor"),
                       "根节点名取自文件名 Corridor（Instantiate 会加 (Clone)），实际 " + inst.transform.name);

                // (1) 逐节点证据（RULING D）：EMPTY 与"父级是 EMPTY"都覆盖，非对称网格同时打
                //     mesh.bounds（局部量，不是米）与 renderer.bounds（世界量，才是米）
                var names = new List<string>();
                int emptyNodes = 0, meshNodes = 0, emptyParented = 0;
                WalkFbx(inst.transform, "STAGE", true, names,
                        ref emptyNodes, ref meshNodes, ref emptyParented);
                Debug.Log("[VERIFY] 节点统计: 全树 " + names.Count + " 个（MESH=" + meshNodes
                          + " EMPTY=" + emptyNodes + " 根=1），父级是 EMPTY 的节点=" + emptyParented
                          + "（实测层级扁平：FBX 根直接挂全部物件，集合没进来）");

                // (2) base/anom 二分只问 IsAnomalyWireName（Task 7 的装配也问它），不在此复制语法
                int nonWire = 0, wire = 0;
                var unknownNames = new List<string>();
                foreach (var n in names)
                {
                    if (AnomalyResolver.IsAnomalyWireName(n)) wire++;
                    else
                    {
                        nonWire++;
                        // 反向核对：树里出现的非 wire 名，除根节点外必须都在契约名单上。
                        // 只有正向保真循环的话，"FBX 多出一个没登记过的件"永远抓不到。
                        if (n != inst.transform.name
                            && !InList(BaseMeshNames, n) && !InList(BaseEmptyNames, n))
                            unknownNames.Add(n);
                    }
                }
                // 计数全部由契约名单派生（base 桶 + 灯位锚点 + 根 = 78；wire 名 = show + hide 代理 = 31）。
                // 这里不写魔数：M6 涨到 71 个 base 网格时，写死的 23/22 会红成"账目不对"而不是"结构坏了"。
                int wantNonWire = BaseMeshNames.Length + BaseEmptyNames.Length + 1;
                int wantWire = AnomShowNames.Length + AnomHideNames.Length;
                Assert(failures, nonWire == wantNonWire,
                       "不匹配 wire 语法的对象名（base 桶 + 根）应为 " + wantNonWire
                       + " 个（base 网格 " + BaseMeshNames.Length + " + 灯位 " + BaseEmptyNames.Length
                       + " + 根 1），实际 non-wire=" + nonWire);
                Assert(failures, wire == wantWire,
                       "wire 语法名应为 " + wantWire + " 个（" + AnomShowNames.Length
                       + " show + " + AnomHideNames.Length + " hide 代理），实际 " + wire);
                // 坏样本第 5 条：树里应当没有未登记名 → 翻成"应有 1 个"。
                Assert(failures, unknownNames.Count == (BadSample ? 1 : 0),
                       "树里不该有契约名单之外的名字，实际 " + unknownNames.Count
                       + (unknownNames.Count == 0 ? "" : "（" + string.Join(",", unknownNames.ToArray()) + "）")
                       + (BadSample ? BadTag : ""));

                // 命名保真：契约里的名字一个都不许被导入器改写（四张名单全跑，不再手挑代表）
                foreach (var want in BaseMeshNames)
                    Assert(failures, FindByName(inst.transform, want) != null, "base 网格名字保真：" + want);
                foreach (var want in BaseEmptyNames)
                    Assert(failures, FindByName(inst.transform, want) != null, "base 灯位锚点保真：" + want);
                foreach (var want in AnomShowNames)
                    Assert(failures, FindByName(inst.transform, want) != null, "anom show 名字保真：" + want);
                foreach (var want in AnomHideNames)
                    Assert(failures, FindByName(inst.transform, want) != null, "anom hide 名字保真：" + want);
                if (BadSample)
                    Assert(failures, FindByName(inst.transform, NoSuchBareName) != null,
                           "base 名字保真：" + NoSuchBareName + BadTag);

                // (3) 结构事实：名字载体必须是 EMPTY（Task 7 要在灯位上挂点光、hide 代理永不显示），
                //     其余全是带 Mesh 的实体；EMPTY 数 = 灯位 + hide 代理，MESH 数 = base 网格 + show。
                Assert(failures, emptyNodes == BaseEmptyNames.Length + AnomHideNames.Length,
                       "EMPTY 节点应为 " + (BaseEmptyNames.Length + AnomHideNames.Length)
                       + "（" + BaseEmptyNames.Length + " 个 Light_ 灯位 + " + AnomHideNames.Length
                       + " 个 hide 代理），实际 " + emptyNodes);
                Assert(failures, meshNodes == BaseMeshNames.Length + AnomShowNames.Length,
                       "MESH 节点应为 " + (BaseMeshNames.Length + AnomShowNames.Length)
                       + "（" + BaseMeshNames.Length + " 个 base 网格 + " + AnomShowNames.Length
                       + " 个 show），实际 " + meshNodes);

                // 方向性断言：走廊必须沿世界 +Z 展开（FbxYaw 之后）。搞反轴/反方向都在这里断掉。
                // 只看 Renderer.bounds —— 世界量才是米（§9.2），不许"修"成 Mesh.bounds。
                var r = new Bounds(Vector3.zero, Vector3.zero);
                bool first = true;
                int rendCount = 0;
                foreach (var mr in inst.GetComponentsInChildren<Renderer>())
                {
                    if (first) { r = mr.bounds; first = false; }
                    else r.Encapsulate(mr.bounds);
                    rendCount++;
                }
                Debug.Log("[VERIFY] 聚合包围盒（FbxYaw=" + Bootstrap3D.FbxYaw + "）size=" + r.size
                          + " min=" + r.min + " max=" + r.max + " 参与聚合的 Renderer=" + rendCount);
                Assert(failures, rendCount == BaseMeshNames.Length + AnomShowNames.Length,
                       "参与聚合的 Renderer 应为 " + (BaseMeshNames.Length + AnomShowNames.Length)
                       + "（EMPTY 无 Renderer），实际 " + rendCount);
                Assert(failures, r.size.z > 20f && r.size.z > r.size.x * 2f,
                       "包围盒 Z 方向最长（实测 size=" + r.size + "）");
                Assert(failures, r.size.y > (BadSample ? 90f : 2.5f) && r.size.y < (BadSample ? 110f : 3.6f),
                       "高度约 3m（实测 " + r.size.y + "）" + (BadSample ? BadTag : ""));
                Assert(failures, r.min.y > -0.25f, "地面不穿到地板下（实测 min.y=" + r.min.y + "）");
                // Step 6 分支表要求区分"z 最长"与"z 最长但朝 -Z 延伸"（后者玩家出门就撞端墙），
                // 单靠 size 判不出来，所以这里补符号：端墙必须在 +Z 前方。
                Assert(failures, r.max.z > 23f && r.min.z > -0.5f,
                       "走廊朝世界 +Z 延伸、端墙在前方（实测 min.z=" + r.min.z + " max.z=" + r.max.z + "）");

                // (3b) M6 结构真门：墙是真的开了洞、洞后面真的垫了东西。
                //      这两件事名字清单和包围盒都判不出来——"WallLeft 存在 + Door_3 存在"全绿，
                //      画面上却可以是"门框后面仍是一整面墙"或"门斗衬板飘到了天花板"。
                CheckHoles(failures, inst.GetComponentsInChildren<MeshRenderer>(true), BadTag);
                CheckDoorTrim(failures, inst.transform, BadTag);
                CheckProps(failures, inst.GetComponentsInChildren<MeshRenderer>(true), BadTag);

                // (3c) 六套灯具在位，且点光锚点真的落在灯具上（灯与灯座错位的症状是"光从半空打来"）
                foreach (var anchor in BaseEmptyNames)
                {
                    var t = FindByName(inst.transform, anchor);
                    var tube = FindByName(inst.transform, "Lamp_" + anchor.Substring("Light_".Length));
                    var base2 = FindByName(inst.transform, "LampBase_" + anchor.Substring("Light_".Length));
                    Assert(failures, t != null && tube != null && base2 != null,
                           anchor + " 应配齐一套灯具（Lamp_ 灯管 + LampBase_ 灯座），实际 灯管="
                           + (tube == null ? "缺" : tube.name) + " 灯座=" + (base2 == null ? "缺" : base2.name));
                    if (t == null || tube == null) continue;
                    float d = Vector3.Distance(t.position, tube.GetComponent<Renderer>().bounds.center);
                    // 坏样本第 6 条：这个距离的容差 0.4m 翻成 0m（灯贴在锚点正中心才"过"＝永不）
                    Assert(failures, d < (BadSample ? 0f : 0.4f),
                           anchor + " 与 " + tube.name + " 灯管中心的距离应 <"
                           + (BadSample ? "0" : "0.4") + "m（实测 " + d.ToString("F3") + "m）"
                           + (BadSample ? BadTag : ""));
                }

                // (4) 解析器直接对 FBX 层级跑通（RULING B：这条才是承载契约的检查）
                var entries = AnomalyResolver.Resolve(inst.transform);
                Assert(failures, entries.Count == (BadSample ? 3 : 10),
                       "解析出 10 组异常，实际 " + entries.Count + (BadSample ? BadTag : ""));
                int showTotal = 0, hideTotal = 0;
                foreach (var e in entries)
                {
                    Assert(failures, AnomalyTitles.Table.ContainsKey(e.id), "id 在标题表里：" + e.id);
                    Assert(failures, e.show.Count + e.hide.Count > 0, "该组至少指认一个物体：" + e.id);
                    showTotal += e.show.Count;
                    hideTotal += e.hide.Count;
                }
                if (entries.Count > 0)
                {
                    Assert(failures, entries[0].id == "anom01", "首组应为 anom01，实际 " + entries[0].id);
                    var hideNames = new List<string>();
                    foreach (var g in entries[0].hide) hideNames.Add(g.name);
                    Debug.Log("[VERIFY] anom01 hide 指认到的 base 目标: " + string.Join(",", hideNames.ToArray()));
                    int wantAnom01Hide = 0;
                    foreach (var w in AnomHideNames)
                        if (w.StartsWith("anom01_")) wantAnom01Hide++;
                    Assert(failures, entries[0].hide.Count == (BadSample ? 99 : wantAnom01Hide),
                           "anom01 应指认 " + wantAnom01Hide + " 个目标（6 盏灯的 Light_ 锚点 + 6 根自发光灯管），实际 "
                           + entries[0].hide.Count + (BadSample ? BadTag : ""));
                }
                else
                {
                    failures.Add("异常表为空，后面三条分组断言无从执行（前缀名没进 FBX？）");
                }
                // 引用集逐字等于契约：show=9、hide=22（M6 前是 13，涨的是"一个件拆成几个零件"的那几组）
                Assert(failures, showTotal == AnomShowNames.Length,
                       "show 引用数应为 " + AnomShowNames.Length + "，实际 " + showTotal);
                Assert(failures, hideTotal == AnomHideNames.Length,
                       "hide 引用数应为 " + AnomHideNames.Length + "，实际 " + hideTotal);
                // 指认落点必须是 base 桶里的真件：hide 代理剥掉 _off 后指到一个不存在或指到异常件上，
                // 症状是"异常激活了却什么都没消失"，而上面两条计数门照样全绿。
                var strayHide = new List<string>();
                foreach (var e in entries)
                    foreach (var g in e.hide)
                        if (!InList(BaseMeshNames, g.name) && !InList(BaseEmptyNames, g.name))
                            strayHide.Add(e.id + "→" + g.name);
                Assert(failures, strayHide.Count == (BadSample ? 1 : 0),
                       "hide 指认的落点都应登记在 base 名单里，实际越界 " + strayHide.Count
                       + (strayHide.Count == 0 ? "" : "（" + string.Join(",", strayHide.ToArray()) + "）")
                       + (BadSample ? BadTag : ""));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tmp);
                Debug.Log("[VERIFY] verify_fbx_stage 与实例已 DestroyImmediate，无场景残骸（资产未触碰）");
            }

            if (failures.Count == 0)
                Debug.Log("[VERIFY] PASS: Corridor.fbx 契约成立（根名 + base/anom 按 IsAnomalyWireName 二分 + "
                          + BaseMeshNames.Length + " 个 base 网格与 " + BaseEmptyNames.Length + " 个灯位逐字保真、"
                          + "无未登记名 + " + (BaseEmptyNames.Length + AnomHideNames.Length) + " 个 EMPTY 名字载体 + "
                          + "三个洞口真的通且覆盖件盖得住 + 六套灯具与灯位对齐 + 包围盒 Z 最长且朝 +Z + "
                          + "解析出 10 组 " + AnomShowNames.Length + "+" + AnomHideNames.Length + " refs 且落点全在 base 名单里）");
            else
            {
                foreach (var f in failures)
                    Debug.LogError("[VERIFY] FAIL: " + f);
                Debug.LogError("[VERIFY] FAIL: 共 " + failures.Count + " 项 —— " + string.Join(" | ", failures));
            }
        }

        /// <summary>
        /// Corridor.fbx 专用逐节点走查：每个节点打一行证据（lossyScale + mesh.bounds + renderer.bounds，
        /// EMPTY 节点打 "no renderer"），并累计 MESH/EMPTY/父级为 EMPTY 三类计数。
        /// 与 ProbeHierarchy 用的 Walk 不同签名、不同用途（那个只收集 mesh 节点），不复用也不重定义 FindByName。
        /// </summary>
        static void WalkFbx(Transform t, string parentKind, bool isRoot, List<string> names,
                            ref int emptyNodes, ref int meshNodes, ref int emptyParented)
        {
            var mf = t.GetComponent<MeshFilter>();
            var rend = t.GetComponent<Renderer>();
            bool isMesh = mf != null;
            string kind = isMesh ? "MESH" : (isRoot ? "ROOT" : "EMPTY");
            names.Add(t.name);
            if (isMesh) meshNodes++;
            else if (!isRoot) emptyNodes++;
            bool parentIsEmpty = parentKind == "EMPTY";
            if (parentIsEmpty) emptyParented++;

            var meshSize = isMesh && mf.sharedMesh != null ? mf.sharedMesh.bounds.size : Vector3.zero;
            Debug.Log("[VERIFY] 节点 " + t.name
                      + " 自身=" + kind + " 父级=" + parentKind
                      + " lossyScale=" + t.lossyScale
                      + " local=" + t.localPosition
                      + " world=" + t.position
                      + (isMesh ? " meshBoundsSize(非米)=" + meshSize
                                + (rend != null ? " rendBounds(米)=" + rend.bounds.size
                                                + "/" + rend.bounds.center : " NO RENDERER")
                          : " no renderer"));

            for (int i = 0; i < t.childCount; i++)
                WalkFbx(t.GetChild(i), kind, false, names, ref emptyNodes, ref meshNodes, ref emptyParented);
        }

        /// <summary>
        /// M6 结构真门：墙是真的开了洞、洞背后真的垫了东西。
        /// 名字清单和聚合包围盒都判不出这两件事——"WallLeft 在 + Door_3 在"全绿，画面上却可以是
        /// "门框后面仍是一整面墙"（忘了切墙）或"门斗衬板飘到天花板"（坐标算错）。
        /// ① 洞中心（墙体厚度正中）不得被同侧任何墙体网格的包围盒覆盖；
        /// ② 覆盖件必须在（长×高）平面上盖住整个洞口（允许 Shrink 的收缩量，玻璃本来要缩进窗框）。
        /// 横侧符号从该侧墙体自身的 bounds 均值反推、不硬写 ±1.2：FbxYaw=90 把 Blender +Y 落到
        /// Unity -X（左右与灰盒对调，见 blender/README.md 轴映射小节），硬写的符号会随 yaw 改动整体反号。
        /// </summary>
        static void CheckHoles(List<string> failures, MeshRenderer[] rends, string badTag)
        {
            foreach (var h in Holes)
            {
                int wallCount;
                int side = HoleSide(rends, h.WallPrefix, out wallCount);
                Assert(failures, side != 0, h.WallPrefix + " 侧应至少有一面墙（横侧符号要从它反推），实际该侧网格数=" + wallCount);
                if (side == 0) continue;

                var p = new Vector3(side * 1.20f, h.Height, h.Along);   // 洞中心＝墙体厚度正中
                bool walled = false;
                foreach (var mr in rends)
                    if (mr.name.StartsWith(h.WallPrefix) && mr.bounds.Contains(p)) walled = true;
                Assert(failures, (!walled) != BadSample,
                       "洞 " + h.Cover + " 的中心 " + p + " 不该落在任何 " + h.WallPrefix
                       + " 墙体包围盒里（没真开洞＝门框后面还是墙）" + (BadSample ? badTag : ""));

                MeshRenderer cov = null;
                foreach (var mr in rends)
                    if (mr.name == h.Cover) cov = mr;
                Assert(failures, cov != null, "洞 (" + h.Along + ", " + h.Height + ") 背后应有覆盖件 " + h.Cover);
                if (cov == null) continue;
                var b = cov.bounds;
                bool spans = b.min.z <= h.Along - h.Width * 0.5f + h.Shrink
                             && b.max.z >= h.Along + h.Width * 0.5f - h.Shrink
                             && b.min.y <= h.Height - h.Tall * 0.5f + h.Shrink
                             && b.max.y >= h.Height + h.Tall * 0.5f - h.Shrink;
                Assert(failures, spans != BadSample,
                       h.Cover + " 应在 (长×高) 平面上盖住洞口 z " + (h.Along - h.Width * 0.5f) + ".."
                       + (h.Along + h.Width * 0.5f) + " y " + (h.Height - h.Tall * 0.5f) + ".."
                       + (h.Height + h.Tall * 0.5f) + "（容差 " + h.Shrink + "），实测 " + b.min + ".." + b.max
                       + (BadSample ? badTag : ""));
                Debug.Log("[VERIFY] 洞 " + h.Cover + " 横侧=" + side + " 覆盖件包围盒 " + b.min + ".." + b.max);
            }
        }

        /// <summary>
        /// 这面墙落在世界 X 的哪一侧：由该前缀所有墙体的包围盒中心求和反推。
        /// FbxYaw=90 之后 Blender 的左右与灰盒口径对调，所以侧向一律问几何、不写死。
        /// CheckHoles 与 CheckProps 共用一份——两处各写一遍，改一次 yaw 就会一边对一边错。
        /// </summary>
        static int HoleSide(MeshRenderer[] rends, string wallPrefix, out int wallCount)
        {
            float sumX = 0f;
            wallCount = 0;
            foreach (var mr in rends)
                if (mr.name.StartsWith(wallPrefix)) { sumX += mr.bounds.center.x; wallCount++; }
            return wallCount == 0 ? 0 : (sumX > 0f ? 1 : -1);
        }

        /// <summary>
        /// 杂物段（M6 T2）的三道几何门。名字缺席、材质、碰撞体、三桶都由契约名单派生的通用门管了，
        /// 这里只管"摆得对不对"，而且管的是**过了 FBX 往返之后**的世界量：
        /// blender/gen_props.py 里有同一套判据，但那是建模口径（+X 沿程、±Y 横向），
        /// 这边是 Unity 口径（+Z 沿程、±X 横向）。轴向映射转错了的话那边全绿、
        /// 画面上却是一排道具横在走廊正中——只有这边抓得到。
        /// ① 不跨中线 ② 贴墙件留 &gt;=1.05m 通行带 ③ 不糊在门洞/窗洞上。
        /// ③ 与 ② 的判据都问 HoleSide，所以"左墙"在两边都指同一面墙。
        /// </summary>
        static void CheckProps(List<string> failures, MeshRenderer[] rends, string badTag)
        {
            const float inSurface = 1.15f;          // 墙体内表面（与 blender 的 IN_Y 同一口径）
            const float wallTouch = 0.15f;          // 离这面墙不到这个距离＝算"贴墙件"
            float clearLo = BadSample ? 3.0f : 1.05f;   // 坏样本：通行带要求涨到物理上不可能
            int crossing = 0, narrow = 0, blocked = 0, absent = 0;
            int sideL = 0, sideR = 0;
            foreach (var name in PropBaseNames)
            {
                MeshRenderer mr = null;
                foreach (var r in rends) if (r.name == name) { mr = r; break; }
                if (mr == null) { absent++; continue; }   // 缺席另有逐名保真门红，这里只计数不重复报
                var b = mr.bounds;
                if (b.min.x * b.max.x < 0f) crossing++;
                float inner = Mathf.Min(Mathf.Abs(b.min.x), Mathf.Abs(b.max.x));
                float passage = inSurface + inner;
                if (passage < clearLo) narrow++;
                bool left = b.center.x < 0f;
                if (left) sideL++; else sideR++;
                foreach (var h in Holes)
                {
                    int hs = HoleSide(rends, h.WallPrefix, out _);
                    if (hs == 0 || hs != (left ? -1 : 1)) continue;
                    bool touches = hs < 0 ? (b.min.x < -inSurface + wallTouch)
                                          : (b.max.x > inSurface - wallTouch);
                    bool overZ = b.min.z < h.Along + h.Width * 0.5f && b.max.z > h.Along - h.Width * 0.5f;
                    bool overY = b.min.y < h.Height + h.Tall * 0.5f && b.max.y > h.Height - h.Tall * 0.5f;
                    if (touches && overZ && overY)
                    {
                        blocked++;
                        Debug.Log("[VERIFY] 杂物 " + name + " 糊在 " + h.Cover + " 洞口（z "
                                  + (h.Along - h.Width * 0.5f).ToString("F2") + ".."
                                  + (h.Along + h.Width * 0.5f).ToString("F2") + " y "
                                  + (h.Height - h.Tall * 0.5f).ToString("F2") + ".."
                                  + (h.Height + h.Tall * 0.5f).ToString("F2") + "），实测 x "
                                  + b.min.x.ToString("F2") + ".." + b.max.x.ToString("F2"));
                    }
                }
                Debug.Log("[VERIFY] 杂物 " + name + " x " + b.min.x.ToString("F2") + ".."
                          + b.max.x.ToString("F2") + " z " + b.min.z.ToString("F2") + ".."
                          + b.max.z.ToString("F2") + " y " + b.min.y.ToString("F2") + ".."
                          + b.max.y.ToString("F2") + " 通行带=" + passage.ToString("F2") + "m");
            }
            Assert(failures, absent == 0,
                   PropBaseNames.Length + " 件杂物应有 " + PropBaseNames.Length + " 件在树里可测（缺席另有名单门逐条红），实际缺席 " + absent);
            Assert(failures, crossing == (BadSample ? 99 : 0),
                   "杂物不该横跨走廊中线（跨中线＝堵路），实际 " + crossing + (BadSample ? badTag : ""));
            // 坏跑期望写 99 而不是 PropBaseNames.Length：后者是个恒等式。clearLo=3.0 时任何摆法
            // 都必被判红（通行带物理上限 1.15+1.15=2.30 < 3.0），narrow 恒等于件数，断言永远成立，
            // 门在坏跑里其实是绿的。写 99 才让坏跑真红，且绿跑 narrow=0/坏跑 narrow=15 两侧都有差分。
            Assert(failures, narrow == (BadSample ? 99 : 0),
                   "每件杂物的通行带应 >=" + clearLo.ToString("F2") + "m（墙内表面到该件内缘），实际不合格 "
                   + narrow + (BadSample ? badTag : ""));
            Assert(failures, blocked == (BadSample ? 99 : 0),
                   "贴墙杂物不得糊在门洞/窗洞上，实际遮挡 " + blocked + " 件次" + (BadSample ? badTag : ""));
            Debug.Log("[VERIFY] 杂物分侧: 左=" + sideL + " 右=" + sideR + "（共 "
                      + PropBaseNames.Length + " 件，通行带下限 " + clearLo.ToString("F2") + "m）");
        }

        /// <summary>
        /// 门套三件与门牌的**相对位置**门。名字都在契约清单里，缺席早就被名单门红了；
        /// 这里查的是几何关系，因为"门牌贴在门板正中""左立柱跑到右立柱外侧"这两种装配
        /// 不影响任何计数门、也不影响包围盒，画面上却一眼假。
        /// 世界系口径：沿程=Z、高度=Y（洞口那侧的墙体横坐标由 FbxYaw 决定，本门只用 Z/Y）。
        /// </summary>
        static void CheckDoorTrim(List<string> failures, Transform root, string badTag)
        {
            foreach (var tag in new[] { "3", "9" })
            {
                var panel = RendOf(root, "Door_" + tag);
                var jambL = RendOf(root, "DoorFrame_" + tag + "_JambL");
                var jambR = RendOf(root, "DoorFrame_" + tag + "_JambR");
                var head = RendOf(root, "DoorFrame_" + tag + "_Head");
                var plate = RendOf(root, "DoorPlate_" + tag);
                if (panel == null || jambL == null || jambR == null || head == null || plate == null)
                {
                    Assert(failures, false, "Door_" + tag + " 的门套三件/门楣/门牌没齐（名单门应已先红）");
                    continue;
                }
                float jambGap = Mathf.Abs(jambR.bounds.center.z - jambL.bounds.center.z);
                bool straddle = jambL.bounds.center.z < panel.bounds.center.z
                                && panel.bounds.center.z < jambR.bounds.center.z
                                && jambGap > 0.5f && jambGap < 1.0f;
                bool headAbove = head.bounds.center.y > panel.bounds.max.y - 0.1f;
                bool plateAbove = plate.bounds.center.y > panel.bounds.max.y;
                // 三条各翻一次坏期望：坏跑里读到的都是"几何明明是对的、期望却要它错"⇒ 逐条成红
                Assert(failures, straddle != BadSample,
                       "Door_" + tag + " 的两根门套立柱应分居门板两侧、间距≈门宽（实测左 "
                       + jambL.bounds.center.z.ToString("F2") + " 右 " + jambR.bounds.center.z.ToString("F2")
                       + " 门板 " + panel.bounds.center.z.ToString("F2") + "）" + (BadSample ? badTag : ""));
                Assert(failures, headAbove != BadSample,
                       "Door_" + tag + " 的门楣应在门板顶缘之上（实测楣 " + head.bounds.center.y.ToString("F2")
                       + " 门顶 " + panel.bounds.max.y.ToString("F2") + "）" + (BadSample ? badTag : ""));
                Assert(failures, plateAbove != BadSample,
                       "DoorPlate_" + tag + " 应挂在门板上方（实测牌 " + plate.bounds.center.y.ToString("F2")
                       + " 门顶 " + panel.bounds.max.y.ToString("F2") + "）" + (BadSample ? badTag : ""));
                Debug.Log("[VERIFY] 门套 Door_" + tag + " 立柱间距=" + jambGap.ToString("F3")
                          + " 楣 y=" + head.bounds.center.y.ToString("F2")
                          + " 牌 y=" + plate.bounds.center.y.ToString("F2")
                          + " 门顶 y=" + panel.bounds.max.y.ToString("F2"));
            }
        }

        static Renderer RendOf(Transform root, string name)
        {
            var t = root == null ? null : FindByName(root, name);
            return t == null ? null : t.GetComponent<Renderer>();
        }

        /// <summary>本文件惯例：断言即打印，失败聚合到调用方自己的局部 failures。</summary>
        static void Assert(List<string> failures, bool ok, string what)
        {
            if (!ok) failures.Add(what);
            Debug.Log("[VERIFY] " + (ok ? "ok  " : "FAIL") + "  " + what);
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
