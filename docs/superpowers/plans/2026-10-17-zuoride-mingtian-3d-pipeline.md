# 《昨日的明天》3D 轨道与 Blender 资产管线 · 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把 `Main3D` 灰盒改造成"单条 Blender 走廊 + 10 组数据化异常变体"的可通关 3D 找异常作品，并在 M1 结束时先拿到一个可提交物。

**Architecture:** Blender 集合路径（`base` / `anom/<id>/{show,hide}`）是资产侧唯一契约；Unity 侧 `AnomalyResolver` 把这套路径解析成 `[Serializable] AnomalyEntry` 列表（序列化在场景的 `Loop3D` 上，理由见 Task 3 开头的修正说明），`Loop3D` 只对 `AnomalyEntry` 做 `Apply/Revert`，完全不知道资产来自 FBX 还是灰盒。灯与应急光由 Bootstrap3D 在 Unity 侧创建并挂进同一套路径，因此解析器只有一份。

**Tech Stack:** Unity 6.3 LTS（6000.3.21f1）+ URP 17.0.1、C#（legacy input）、Blender 5.2.2 LTS Python（`bpy`）、FBX、Git Bash 脚本、ffmpeg（音频沿用）。

**Spec:** [docs/superpowers/specs/2026-10-17-zuoride-mingtian-3d-pipeline-design.md](file:///G:/AIgame/jam/docs/superpowers/specs/2026-10-17-zuoride-mingtian-3d-pipeline-design.md) —— 本计划逐节实现该设计，行号引用一律针对基线提交 `4ba3c9d`。

## Global Constraints

以下为全局约束，每个任务的隐含要求都包含本节（数值逐字抄自 spec）：

- 交付轨道：**真 3D 第一人称（Main3D）**。2D 轨道（`Bootstrap.cs`、`Main.unity`、`builds/win64`）保留为参照与保底，**本计划一律不改**。
- 不新增任何 Unity 包：`Packages/manifest.json` 现无 `com.unity.test-framework`，验收靠 batchmode 编辑器自验收口，不引注册表依赖。
- 数值：`maxDepth = 8`、`maxMistakes = 3`、`cleanChance = 0.35`、异常数量 **10**（超时保底 6，优先保留 anom01/02/03/05/06/08）。
- depth 1 **强制无异常**；干净判定先于抽样（顺序写死，见 spec §4.2.2）。
- 走廊：宽 2.4m × 高 3m × 长 24m，**直线、无转角**；base < 2 万三角面，单个异常物件 < 3 千。
- 贴图 512×512，**光照不烘焙**，材质全部 URP/Lit 或 URP/Unlit。
- **零第三方资产**：几何、贴图、音频一律程序化生成或自建。
- HUD：**ScreenSpaceCamera**（spec §8.4 定案，须在 M3 从现状 Overlay 改掉）。
- Blender：`D:\Game Library\Steam\steamapps\common\Blender\blender.exe`（5.2.2 LTS）。
- MCP：`uvx --from mcp-for-blender mcp-for-blender`（2.1.9），Blender 侧端口 **9876**，交互需 GUI 常驻。
- 仓库行尾已由 `.gitattributes` 钉成 LF；`Library/`、`builds/`、`logs/` 已 ignore，`.meta` 必须跟踪。
- **验收口径**：每个任务的出口条件都要贴实际输出（脚本 print 统计 / Unity batchmode 日志 grep / exe 存在性 / Player.log）。自写检查脚本必须先被喂一个坏样本、确认它会 FAIL，之后才允许它 PASS 背书。
- 时钟：8 小时 jam，设计与勘察已用约 0.5h；任务顺序即时间盒顺序，超时按 spec §7 降级路径执行，不讨价还价。

## 文件结构（先定边界，再拆任务）

**新建**

| 文件 | 单一职责 |
|---|---|
| `blender/probe_collections.py` | M0 探针：造 `base`/`anom/01/{show,hide}` 集合与小立方体，导出 FBX |
| `blender/gen_corridor.py` | base 走廊几何的唯一真相（2.4×3×24m + 墙裙/门洞/信箱/窗/管线），MCP 与 headless 共用 |
| `blender/gen_anomalies.py` | 10 组异常变体物件的摆放，按集合路径登记 |
| `blender/gen_textures.py` | 程序化贴图 → PNG 写入 Unity 目录（水磨石/绿漆墙裙/米白墙/锈迹） |
| `blender/build_scene.py` | 总入口：调上面三个 + 保存 `.blend` + 导出 FBX |
| `tools/blender_probe.sh` | 胶水：headless 跑探针 + 断言 FBX 产物存在 |
| `tools/blender_build.sh` | 胶水：headless 建模 + 导出 + 断言 |
| `tools/unity_verify.sh` | 胶水：跑 `Verify3D.Run`（含坏样本开关）+ grep 日志 + 失败门 |
| `Scripts/Data/AnomalyEntry.cs` | `[Serializable] AnomalyEntry`（id/title/show/hide + Apply/Revert/SettleToNormal）+ `AnomalyTitles` 表 |
| `Scripts/3D/GreyboxAnomalies.cs` | M1 临时灰盒异常组（Task 7 删除） |
| `Scripts/Data/AnomalyResolver.cs` | 纯解析：场景根 → `List<(id,show[],hide[])>`，不碰资产保存 |
| `Editor/Verify3D.cs` | 自验收口：层级/装配/玩法三段断言，支持 `VERIFY_BAD` 坏样本注入 |
| `blender/README.md` | 集合命名与导出契约的落地文档（给下个会话读） |

**修改**

| 文件 | 改什么 |
|---|---|
| `Scripts/3D/Loop3D.cs` | 删随机掷骰子，改为目录驱动 + 可注入 seed + `Apply/Revert` |
| `Editor/Bootstrap3D.cs` | 灰盒 cube → 实例化 FBX + 调 `AnomalyResolver` 存目录；灯挂进 `anom` 路径；HUD 换 ScreenSpaceCamera |
| `Scripts/3D/Hud3D.cs` | 结语文案（含挂历彩蛋）、异常记录语义 |
| `Scripts/3D/FirstPersonController.cs` | 接脚步音 |
| `tools/build.sh` | `OUT` 按模式分流（修 spec §9 第一条坑） |
| `tools/SUBMIT_CHECKLIST.md` | 3D 化：新命令、新提交物路径、MCP 安全面登记 |

---

### Task 1: FBX 集合与命名保真探针（M0，硬门）

整个管线契约建立在"Blender 集合会变成 Unity 层级节点、对象名不被改写、1 Blender 单位 = 1 Unity 米"这三条假设上。**三条里任何一条不成立，spec §3 的命名规范就要换**，所以必须先证。

**Files:**
- Create: `blender/probe_collections.py`
- Create: `tools/blender_probe.sh`
- Create: `dreamcore/Assets/_Project/Editor/Verify3D.cs`（本任务只写层级探针段，后续任务追加玩法段）
- Create: `dreamcore/Assets/Content/3D/Probe.fbx`（脚本产物）

**Interfaces:**
- Consumes: 无
- Produces: `Dreamcore.EditorTools.Verify3D.ProbeHierarchy()`（batchmode `-executeMethod` 入口）；日志约定字符串 `[VERIFY] PASS` / `[VERIFY] FAIL`，后续任务沿用同一约定；`tools/blender_probe.sh` 的退出码约定（0=产物在位且日志 PASS）。

- [ ] **Step 1: 写探针脚本，造出契约里那三条假设的可见样本**

```python
# blender/probe_collections.py
# M0 探针：三个小立方体放进 base / anom/01/show / anom/01/hide，导出 FBX。
# 用法（headless）:
#   blender --background --python blender/probe_collections.py
import os
import bpy

OUT = os.environ.get(
    "PROBE_FBX_OUT",
    r"G:\AIgame\jam\dreamcore\Assets\Content\3D\Probe.fbx")

bpy.ops.wm.read_factory_settings(use_empty=True)
scene_coll = bpy.context.scene.collection


def new_coll(name, parent):
    coll = bpy.data.collections.new(name)
    parent.children.link(coll)
    return coll


base = new_coll("base", scene_coll)
anom = new_coll("anom", scene_coll)
a01 = new_coll("01", anom)
show = new_coll("show", a01)
hide = new_coll("hide", a01)


def cube(name, coll, loc, size=1.0):
    bpy.ops.mesh.primitive_cube_add(size=size, location=loc)
    ob = bpy.context.active_object
    ob.name = name
    for c in list(ob.users_collection):
        c.objects.unlink(ob)
    coll.objects.link(ob)
    return ob


# 间距 10m 用来验单位尺度：Unity 侧读到的 localPosition 差值应≈10
cube("wallL", base, (0.0, 0.0, 0.0))
cube("floor", base, (0.0, 0.0, 10.0))
cube("shadeFigure", show, (0.0, 0.0, 20.0))
cube("floor_off", hide, (0.0, 0.0, 30.0))

os.makedirs(os.path.dirname(OUT), exist_ok=True)
bpy.ops.export_scene.fbx(
    filepath=OUT,
    use_selection=False,
    use_visible=True,
    object_types={"MESH", "EMPTY"},
    use_mesh_modifiers=True,
    apply_unit_scale=True,
    bake_space_transform=False,
    path_mode="COPY",
    embed_textures=False,
    add_leaf_bones=False,
    use_metadata=False,
)

# 自检输出：这些 print 就是探针的"真实模型 oracle"，Unity 侧结论必须与它对照
print("[PROBE] exported:", OUT, "exists=", os.path.exists(OUT))
for coll, names in [(base, "base"), (show, "anom/01/show"), (hide, "anom/01/hide")]:
    print("[PROBE] coll<%s> objects=%s" % (names, [o.name for o in coll.objects]))
print("[PROBE] total_objects=", len(bpy.data.objects))
```

- [ ] **Step 2: headless 跑一次，确认 FBX 真的落盘**

Run: `cd /g/AIgame/jam && "/d/Game Library/Steam/steamapps/common/Blender/blender.exe" --background --python blender/probe_collections.py 2>&1 | grep -E "\[PROBE\]|Error"`
Expected: 四行 `[PROBE]`，其中 `exists= True`，且 `total_objects= 4`。出现 `Error: Connection reset` 或空输出 = 导出器抛异常，停下修脚本。

- [ ] **Step 3: 写 Unity 层级断言口（先让它会失败）**

```csharp
// dreamcore/Assets/_Project/Editor/Verify3D.cs
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Dreamcore.EditorTools
{
    /// <summary>
    /// 自验收口（batchmode -executeMethod）。约定：日志只认 [VERIFY] PASS / [VERIFY] FAIL。
    /// 坏样本开关：环境变量 VERIFY_BAD=1 时，期望集里塞进一个绝不存在的名，
    /// 用来证明这个检查器真的会失败——不被喂坏样本之前，它的 PASS 不算背书。
    /// </summary>
    public static class Verify3D
    {
        const string ProbePath = "Assets/Content/3D/Probe.fbx";

        public static void ProbeHierarchy()
        {
            var expected = new List<string> { "wallL", "floor", "shadeFigure", "floor_off" };
            if (Environment.GetEnvironmentVariable("VERIFY_BAD") == "1")
                expected.Add("no_such_object_xyz");

            AssetDatabase.ImportAsset(ProbePath, ImportAssetOptions.ForceSynchronousImport);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ProbePath);
            if (prefab == null)
            {
                Debug.LogError("[VERIFY] FAIL: 无法加载 " + ProbePath);
                return;
            }

            var paths = new List<string>();
            var objectNames = new HashSet<string>();
            var sb = new StringBuilder();
            Walk(prefab.transform, "", paths, objectNames, sb);
            Debug.Log("[VERIFY] ---- 层级实测 ----\n" + sb);

            bool collNodes = false;
            foreach (var p in paths)
                if (p.StartsWith("base") || p.StartsWith("anom")) collNodes = true;
            Debug.Log("[VERIFY] 集合是否成为节点: " + collNodes);

            var missing = new List<string>();
            foreach (var name in expected)
                if (!objectNames.Contains(name)) missing.Add(name);

            // 尺度探针：floor 与 wallL 的 z 差应≈10（1 Blender unit = 1 Unity meter）
            Transform tWall = FindByName(prefab.transform, "wallL");
            Transform tFloor = FindByName(prefab.transform, "floor");
            if (tWall != null && tFloor != null)
                Debug.Log("[VERIFY] 尺度 z差=" + Mathf.Abs(tFloor.position.z - tWall.position.z)
                          + "（期望≈10）");

            if (missing.Count == 0)
                Debug.Log("[VERIFY] PASS: 期望对象名全部保真 " + string.Join(",", expected));
            else
                Debug.LogError("[VERIFY] FAIL: 缺失 " + string.Join(",", missing));
        }

        /// <summary>
        /// 只有带网格的节点算"物体"，空节点是 Blender 集合的残影。
        /// 两个集合分开统计，才能分别回答"名字保真吗"和"层级还在吗"。
        /// </summary>
        static void Walk(Transform t, string prefix, List<string> paths,
                         HashSet<string> objectNames, StringBuilder sb)
        {
            string path = string.IsNullOrEmpty(prefix) ? t.name : prefix + "/" + t.name;
            paths.Add(path);
            bool isMesh = t.GetComponent<MeshFilter>() != null;
            if (isMesh) objectNames.Add(t.name);
            sb.AppendLine(path + (isMesh ? "  <mesh>" : "  <node>"));
            for (int i = 0; i < t.childCount; i++)
                Walk(t.GetChild(i), path, objectNames, sb);
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
```

- [ ] **Step 4: 先喂坏样本，证明检查器会 FAIL**

Run: `cd /g/AIgame/jam && VERIFY_BAD=1 "/e/Unity/Unity/Editor/Editor/Unity.exe" -batchmode -quit -projectPath G:/AIgame/jam/dreamcore -executeMethod Dreamcore.EditorTools.Verify3D.ProbeHierarchy -logFile G:/AIgame/jam/logs/verify-probe-bad.log; grep -o "\[VERIFY\] FAIL[^"]*" logs/verify-probe-bad.log | head -2`
Expected: 打出 `[VERIFY] FAIL: 缺失 no_such_object_xyz`。**若这里打出的是 PASS，检查器是假的，禁止往下走**——先修 `Walk` 的叶子名收集逻辑。

- [ ] **Step 5: 去掉坏样本，取真实结论**

Run: `cd /g/AIgame/jam && "/e/Unity/Unity/Editor/Editor/Unity.exe" -batchmode -quit -projectPath G:/AIgame/jam/dreamcore -executeMethod Dreamcore.EditorTools.Verify3D.ProbeHierarchy -logFile G:/AIgame/jam/logs/verify-probe.log; grep -E "\[VERIFY\]" logs/verify-probe.log`
Expected: 三行关键读数 —— `集合是否成为节点: True`、`尺度 z差≈10`、`[VERIFY] PASS`。

- [ ] **Step 6: 按实测结论定契约，逐条落进 spec §9**

三份判定与对应处置，必须把 `[VERIFY]` 那几行原文贴进 spec 再决定：

| 实测 | 处置 |
|---|---|
| `集合成为节点: True` 且 `PASS` | spec §3 命名规范原样生效，Task 3 按路径解析 |
| 叶子名 `PASS` 但节点 `False` | 集合丢了，退化为**前缀命名**：对象改名 `anom01_show_shadeFigure` / `anom01_hide_floor_off`，Task 3 的解析器改成按前缀分桶（其余不变） |
| 叶子名也缺（`FAIL`） | FBX 通道不通，改用 glTF：`bpy.ops.export_scene.gltf(filepath=..., export_format="GLB")`；Unity 侧 `Probe.glb` 重跑 Step 4–5 |
| `尺度 z差` ≠ 10 | 记录实际倍率，Task 6 建模前统一乘该系数，别在 Unity 导入设置里赌 |

- [ ] **Step 7: Commit**

```bash
git add blender/probe_collections.py tools/blender_probe.sh \
        dreamcore/Assets/_Project/Editor/Verify3D.cs \
        docs/superpowers/specs/2026-10-17-zuoride-mingtian-3d-pipeline-design.md
git commit -m "spike: M0 探针实测 Blender 集合经 FBX 到 Unity 的层级保真度"
```

---

### Task 2: Blender MCP 组装（用户明确要求的动作）

**Files:**
- Create: `tools/blender_mcp_setup.md`（装法与坑，逐字可复现）
- Modify: Qoder 用户级 MCP 配置（`manage_mcp add`，需用户点头）
- 外部: `%APPDATA%\Blender Foundation\Blender\5.2\scripts\addons\addon.py`

**Interfaces:**
- Consumes: `uvx`（`E:\ServBay\ServBay\packages\python\current\Scripts\uvx`）
- Produces: 可调用的 MCP 工具集（建模指令的实际工具名以 `mcp_list` 结果为准，不预设）；Blender 侧算子 `bpy.ops.blendermcp.start_server`，端口 9876。

- [ ] **Step 1: 生成 5.2 配置目录（绕开已实测的坑）**

Steam 版 5.2 从未启动过，`addon-paths` 只认 5.0/5.1。先起一次 GUI 让它建目录：

Run: `"/d/Game Library/Steam/steamapps/common/Blender/blender.exe" & sleep 25; ls ~/AppData/Roaming/Blender\ Foundation/Blender/`
Expected: 目录列表出现 `5.2/`。这一步会拉起 GUI、需要用户在场；**不要替用户关掉它**，告知"5.2 配置目录已生成，可以关了"。

- [ ] **Step 2: 装 addon 并确认落点**

Run: `export PATH="/e/ServBay/ServBay/packages/python/current/Scripts:$PATH"; uvx --from mcp-for-blender mcp-for-blender addon-paths; uvx --from mcp-for-blender mcp-for-blender install-addon; ls ~/AppData/Roaming/Blender\ Foundation/Blender/5.2/scripts/addons/`
Expected: `addon-paths` 现在列出 5.2；`addons/` 下出现 `addon.py`。若 `install-addon` 仍只装进 5.0/5.1，手工拷贝并把该缺陷写进 `tools/blender_mcp_setup.md`。

- [ ] **Step 3: 把 server 写进 Qoder 用户级 MCP 配置**

用 `manage_mcp`（action=add，name=`blender`，config 的 command 指向 `uvx`、args 为
`--from mcp-for-blender mcp-for-blender`，**不设** `BLENDER_MCP_SAFE_MODE`——导出 FBX 要写文件）。
这是配置写动作，执行前向用户确认那一次。

- [ ] **Step 4: 起 GUI + 拉起 socket server**

Run: 启动 Blender GUI；在 3D 视图 N 面板 → Blender MCP → Start Server（或启动脚本执行
`bpy.ops.blendermcp.start_server()`）。
Expected: `netstat -ano | grep 9876` 出现 LISTENING。

- [ ] **Step 5: 打通一次真实调用（唯一的完成判据）**

用 `mcp_list` 发现工具，再调一个"读当前场景物体数"的最小工具。
Expected: 返回值是真实 Blender 状态。**没有这一步的回执，MCP 视为未装好**，Task 7 一律退回 headless。

- [ ] **Step 6: 登记并 Commit**

把 addon 落点缺陷、safe mode 决定（不开）、"GUI 常驻"约束写进 `tools/blender_mcp_setup.md` 与
spec §9，然后 `git add tools/blender_mcp_setup.md docs && git commit -m "chore: 组装 Blender MCP"`。

---

### Task 3: 异常数据层（`AnomalyEntry` + 解析器）

**对 spec §3 的一处修正（先说明，避免执行者两难）**：spec 写的是 `AnomalyDefinition`/`AnomalyCatalog` 两个
ScriptableObject **资产**。资产持有场景 GameObject 的引用是伪引用——场景一重载即失效，而 Main3D 每次
构建都由 Bootstrap 重建。因此改为 **`[Serializable] AnomalyEntry` 列表，直接序列化在场景里的 `Loop3D`
组件上**（就是 Unity 常规接线方式）。解析逻辑与命名约定完全不变，只是落点从 `.asset` 换成场景字段。
本任务 Step 6 同步修订 spec §3 的措辞。

**Files:**
- Create: `dreamcore/Assets/_Project/Scripts/Data/AnomalyEntry.cs`
- Create: `dreamcore/Assets/_Project/Scripts/Data/AnomalyResolver.cs`
- Modify: `docs/superpowers/specs/2026-10-17-zuoride-mingtian-3d-pipeline-design.md`（§3 落点措辞）

**Interfaces:**
- Consumes: Task 1 定下的集合布局约定（路径式 `anom/<id>/{show,hide}`，或探针判定不成立时的前缀式）
- Produces:
  - `Dreamcore.AnomalyEntry`：`string id`、`string title`、`List<GameObject> show`、`List<GameObject> hide`、`void Apply()`、`void Revert()`
  - `Dreamcore.AnomalyResolver`：`static List<AnomalyEntry> Resolve(Transform sceneRoot)`
  - `Dreamcore.AnomalyTitles`：`static readonly Dictionary<string, string> Table`
  - **命名约定只有 `AnomalyResolver` 一处知道**。Task 1 若判定退化为前缀式，只改这个文件。

- [ ] **Step 1: 写 `AnomalyEntry`（Apply/Revert 是全部职责）**

```csharp
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
```

- [ ] **Step 2: 写解析器（路径式集合布局 → `AnomalyEntry`）**

```csharp
// dreamcore/Assets/_Project/Scripts/Data/AnomalyResolver.cs
using System.Collections.Generic;
using UnityEngine;

namespace Dreamcore
{
    /// <summary>
    /// 按集合布局把场景根解析成异常表。约定（spec §3）：
    ///   base                     恒定本体
    ///   anom/<NN>/show/<名字>     异常激活时出现
    ///   anom/<NN>/hide/<名字>_off 异常激活时消失；去掉 _off 即 base 里的目标名
    /// hide 的代理自己永不显示，只负责指认目标；指认不到就打警告并丢弃该条（不静默）。
    /// </summary>
    public static class AnomalyResolver
    {
        const string HideSuffix = "_off";

        public static List<AnomalyEntry> Resolve(Transform sceneRoot)
        {
            var result = new List<AnomalyEntry>();
            Transform anomRoot = FindChildRecursive(sceneRoot, "anom");
            if (anomRoot == null)
            {
                Debug.LogWarning("[Resolver] 场景里找不到 anom 节点，异常表为空。");
                return result;
            }

            // base 里的全部物体，供 hide 代理按名指认
            var byName = new Dictionary<string, GameObject>();
            Collect(sceneRoot, byName);

            foreach (Transform idNode in anomRoot)
            {
                string id = "anom" + idNode.name; // "01" -> "anom01"
                Transform showNode = FindDirectChild(idNode, "show");
                Transform hideNode = FindDirectChild(idNode, "hide");
                if (showNode == null && hideNode == null) continue;

                var entry = new AnomalyEntry
                {
                    id = id,
                    title = AnomalyTitles.Table.ContainsKey(id)
                        ? AnomalyTitles.Table[id] : idNode.name + "（无标题）",
                };

                if (showNode != null)
                    foreach (Transform t in showNode) entry.show.Add(t.gameObject);

                if (hideNode != null)
                    foreach (Transform proxy in hideNode)
                    {
                        proxy.gameObject.SetActive(false); // 代理自己不参与显示
                        if (!proxy.name.EndsWith(HideSuffix))
                        {
                            Debug.LogWarning("[Resolver] hide 代理缺 _off 后缀，忽略: " + proxy.name);
                            continue;
                        }
                        string targetName = proxy.name.Substring(0, proxy.name.Length - HideSuffix.Length);
                        GameObject target;
                        if (!byName.TryGetValue(targetName, out target) || target == proxy.gameObject)
                        {
                            Debug.LogWarning("[Resolver] hide 指认不到 base 目标: " + targetName);
                            continue;
                        }
                        entry.hide.Add(target);
                    }

                result.Add(entry);
            }

            result.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            return result;
        }

        static void Collect(Transform t, Dictionary<string, GameObject> byName)
        {
            if (!byName.ContainsKey(t.name)) byName.Add(t.name, t.gameObject);
            for (int i = 0; i < t.childCount; i++) Collect(t.GetChild(i), byName);
        }

        static Transform FindDirectChild(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
                if (parent.GetChild(i).name == name) return parent.GetChild(i);
            return null;
        }

        static Transform FindChildRecursive(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var r = FindChildRecursive(t.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }
    }
}
```

- [ ] **Step 3: 编译门（先让它编译不过，再修到过——不做这步就不知道日志在哪）**

Run: `cd /g/AIgame/jam && "/e/Unity/Unity/Editor/Editor/Unity.exe" -batchmode -quit -projectPath G:/AIgame/jam/dreamcore -logFile G:/AIgame/jam/logs/compile-t3.log; grep -cE "error CS" logs/compile-t3.log`
Expected: `0`。非 0 就把 `error CS` 行原文贴出来逐条修，不要靠猜改。

- [ ] **Step 4: 修订 spec §3 的落点措辞**

把"Unity 侧数据资产（均由 Bootstrap3D 生成）… `AnomalyCatalog : ScriptableObject`"这段改成
`[Serializable] AnomalyEntry` 列表序列化在 `Loop3D` 上，并写明原因（资产引用场景物体是伪引用）。

- [ ] **Step 5: Commit**

```bash
git add dreamcore/Assets/_Project/Scripts/Data/AnomalyEntry.cs \
        dreamcore/Assets/_Project/Scripts/Data/AnomalyResolver.cs \
        dreamcore/Assets/_Project/Scripts/Data/*.meta \
        docs/superpowers/specs/2026-10-17-zuoride-mingtian-3d-pipeline-design.md
git commit -m "feat: 异常数据层 AnomalyEntry 与集合路径解析器"
```

---

### Task 4: `Loop3D` 重写为目录驱动（M1 玩法核心）

**Files:**
- Modify: `dreamcore/Assets/_Project/Scripts/3D/Loop3D.cs`（整文件替换）
- Modify: `dreamcore/Assets/_Project/Scripts/3D/ShadeFigure.cs`（去掉 `mr.enabled` 门控，只留 billboard）
- Modify: `dreamcore/Assets/_Project/Editor/Verify3D.cs`（追加玩法断言段）

**Interfaces:**
- Consumes: `Dreamcore.AnomalyEntry`（Task 3）
- Produces: `Loop3D` 公开面 —— `int maxDepth`、`int maxMistakes`、`float cleanChance`、
  `List<AnomalyEntry> anomalies`、`int Depth`、`int Mistakes`、`AnomalyEntry Current`、
  `bool HasAnomaly`、`event Action<int,int> OnHudUpdate`、`event Action<bool> OnEnd`、
  `void Begin(int? seed = null)`、`void Report(bool goForward)`、`void ResetRound()`
  （`Hud3D` 与 `TriggerGate` 只用得到 `Begin`/`Report`/`maxMistakes`/两个事件，签名保持兼容）

- [ ] **Step 1: 写玩法断言（此时必然失败——`Loop3D` 还是旧的随机掷骰子）**

在 `Verify3D.cs` 追加：

```csharp
        static readonly System.Text.StringBuilder fails = new System.Text.StringBuilder();

        /// <summary>玩法段断言。VERIFY_BAD=1 时故意断一条不可能成立的，用来证明检查器有牙。</summary>
        public static void VerifyGameplay()
        {
            fails.Length = 0;

            // 造 10 个假异常（灰盒阶段就用空 GameObject，不需要美术）
            var holder = new GameObject("verify_holder");
            var catalog = new List<Dreamcore.AnomalyEntry>();
            var objs = new List<GameObject>();
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

            var go = new GameObject("verify_loop");
            var loop = go.AddComponent<Dreamcore.D3.Loop3D>();
            loop.anomalies = catalog;
            loop.cleanChance = 0f; // 本段只验抽取规则，干净层单列

            Assert(loop.Depth == 1, "初始深度应为 1");
            loop.Begin(12345);
            Assert(!loop.HasAnomaly, "depth 1 必须干净（玩家要先见过正常）");

            // 不重复 + 确定性
            var seen = new List<string>();
            bool ended = false;
            loop.OnEnd += w => ended = w;
            for (int i = 0; i < 7 && !ended; i++)
            {
                loop.Report(!loop.HasAnomaly); // 永远答对
                if (loop.Current != null) seen.Add(loop.Current.id);
            }
            Assert(seen.Count == new HashSet<string>(seen).Count, "同一局内异常不得重复");
            Assert(ended, "连答 7 次对应到深度 8 触发好结局");

            // 三错即败
            loop.Begin(999);
            int winFlags = 0;
            loop.OnEnd += w => { if (!w) winFlags |= 1; };
            for (int i = 0; i < 3; i++) loop.Report(loop.HasAnomaly); // 永远答错
            Assert((winFlags & 1) == 1, "错满 3 次必须触发坏结局");
            int depthAfterLose = loop.Depth;
            loop.Report(false);
            Assert(loop.Depth == depthAfterLose, "结束后再判定必须被忽略（ended 门）");

            // 干净层确实会出现
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
            Assert(anyClean, "存在 seed 能让干净层出现（否则玩家可无脑回头）");

            // Apply/Revert 真开关
            var e = catalog[3];
            e.show[0].SetActive(false);
            e.hide[0].SetActive(true);
            e.Apply();
            Assert(e.show[0].activeSelf && !e.hide[0].activeSelf, "Apply 应开 show、关 hide");
            e.Revert();
            Assert(!e.show[0].activeSelf && e.hide[0].activeSelf, "Revert 应逐字还原");

            if (Environment.GetEnvironmentVariable("VERIFY_BAD") == "1")
                Assert(false, "坏样本：这条断言必须失败");

            foreach (var o in objs) UnityEngine.Object.DestroyImmediate(o);
            UnityEngine.Object.DestroyImmediate(holder);
            UnityEngine.Object.DestroyImmediate(go);

            if (fails.Length == 0) Debug.Log("[VERIFY] PASS: 玩法断言全过");
            else Debug.LogError("[VERIFY] FAIL:\n" + fails);
        }

        static void Assert(bool ok, string what)
        {
            if (!ok) fails.AppendLine("  - " + what);
            Debug.Log("[VERIFY] " + (ok ? "ok  " : "FAIL") + "  " + what);
        }
```

- [ ] **Step 2: 跑一次，确认它 FAIL（红）**

Run: `cd /g/AIgame/jam && "/e/Unity/Unity/Editor/Editor/Unity.exe" -batchmode -quit -projectPath G:/AIgame/jam/dreamcore -executeMethod Dreamcore.EditorTools.Verify3D.VerifyGameplay -logFile G:/AIgame/jam/logs/verify-play-red.log; grep -E "\[VERIFY\]" logs/verify-play-red.log | head -8`
Expected: `error CS` 或一串 FAIL（`Loop3D` 还没有 `anomalies` 字段，编译就该断）。**若这里直接 PASS，说明断言是假的，重写它**。

- [ ] **Step 3: 重写 `Loop3D`**

```csharp
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
            }

            OnHudUpdate?.Invoke(Depth, Mistakes);
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
```

- [ ] **Step 4: `ShadeFigure` 去掉显示门控（它和 `SetActive` 方案抢方向盘）**

把 `ShadeFigure.cs:17-24` 的 `mr.enabled = false` 与 `:28-31` 的 `SetActive` 方法删掉，`Update` 里的
`if (mr == null || !mr.enabled …)` 改成 `if (lookTarget == null)` 即可——显隐交回 `AnomalyEntry`，
这个组件只剩"始终面向玩家"。

- [ ] **Step 5: 跑绿**

Run: `cd /g/AIgame/jam && "/e/Unity/Unity/Editor/Editor/Unity.exe" -batchmode -quit -projectPath G:/AIgame/jam/dreamcore -executeMethod Dreamcore.EditorTools.Verify3D.VerifyGameplay -logFile G:/AIgame/jam/logs/verify-play.log; grep -E "\[VERIFY\]" logs/verify-play.log`
Expected: 全 `ok` + 末行 `[VERIFY] PASS: 玩法断言全过`。

- [ ] **Step 6: 回喂坏样本，确认检查器有牙**

Run: 同 Step 5 但前缀 `VERIFY_BAD=1`，日志写 `logs/verify-play-bad.log`。
Expected: 末行 `[VERIFY] FAIL:` 且包含"坏样本：这条断言必须失败"。**Step 5 的 PASS 只有在 Step 6 见过 FAIL 之后才算背书。**

- [ ] **Step 7: Commit**

```bash
git add dreamcore/Assets/_Project/Scripts/3D/Loop3D.cs \
        dreamcore/Assets/_Project/Scripts/3D/ShadeFigure.cs \
        dreamcore/Assets/_Project/Editor/Verify3D.cs
git commit -m "feat: Loop3D 改为目录驱动，depth1 强制干净且不重复抽取"
```

---

### Task 5: Bootstrap3D 灰盒装配 + 构建分流（**M1 出口 = 可提交物**）

M1 的目的不是美术，是**先拿到一个能提交、能通关的 3D 版本**。异常组全部用 Unity 图元当灰盒造出来，
名字严格照 `AnomalyResolver` 的约定，这样"集合布局 → 解析 → 抽骰 → Apply/Revert"整条链在任何一个 FBX 存在之前就已经被验证过。
Task 6/7 只把造物体的那一步换成实例化 FBX，其余接线一行不改。

顺带修两处已知缺陷：
1. `tools/build.sh` 的 `OUT` 写死 `builds/win64`，3D 构建会**覆盖 2D 提交物并重新打包它**（spec §9 第一条坑）。
2. Task 4 Step 4 删掉了 `ShadeFigure` 的显示门控，而 `Bootstrap3D.cs:104-114` 仍在场景里常驻造 3 个黑影——照旧跑就是"每层都有人影"，人影不再是异常信号。黑影显隐交回异常目录。
3. `Bootstrap3D.cs:150-151` 给 `loop.sceneLights`/`loop.shadeFigures` 赋值，Task 4 已删除这两个字段，**不改就编译不过**。

**Files:**
- Create: `dreamcore/Assets/_Project/Editor/GreyboxAnomalies.cs`
- Modify: `dreamcore/Assets/_Project/Editor/Bootstrap3D.cs`（`:104-114` 删常驻黑影、`:148-151` 换装配、`:118` 后插入造组）
- Modify: `tools/build.sh:12`（`OUT` 按模式分流）
- Modify: `dreamcore/Assets/_Project/Editor/Verify3D.cs`（追加装配段 `VerifyAssembly()`）

**Interfaces:**
- Consumes: `AnomalyResolver.Resolve(Transform)`、`AnomalyEntry.SettleToNormal()`（Task 3）；`Loop3D.anomalies`（Task 4）；`LightFlicker`、`ShadeFigure`（既有组件）
- Produces: `Dreamcore.EditorTools.GreyboxAnomalies.Build(Transform root, IReadOnlyList<LightFlicker> lights)`；`Dreamcore.EditorTools.Verify3D.VerifyAssembly()`；提交物路径改为 `builds/win64-3d/Dreamcore.exe` 与 `builds/win64-3d/Dreamcore-win64.zip`（2D 的 `builds/win64/` 不再被碰）

- [ ] **Step 1: 先修 `tools/build.sh` 的输出目录冲突**

把 `tools/build.sh:12` 的

```sh
OUT="$JAM_ROOT/builds/win64"
```

整行替换为：

```sh
if [ "$MODE" = "3d" ]; then OUT="$JAM_ROOT/builds/win64-3d"; else OUT="$JAM_ROOT/builds/win64"; fi
```

Run: `cd /g/AIgame/jam && bash -n tools/build.sh && echo SYNTAX-OK`
Expected: `SYNTAX-OK`（语法门；`set -eu` 下 `&&` 列表中间项失败不触发 errexit，所以 `:28` 那种 `grep && exit 1` 写法保留不动）。

- [ ] **Step 2: 写装配段断言（此时必然红——灰盒组还不存在，`anomalies` 是空表）**

追加进 `Verify3D.cs`（注意：本文件同时 `using System;` 与 `using UnityEngine;`，裸写 `Object` 有歧义，一律用 `UnityEngine.Object.`）：

```csharp
        /// <summary>装配段：验 Main3D.unity 里异常目录真的接进了 Loop3D，且初始态是"正常"。</summary>
        public static void VerifyAssembly()
        {
            fails.Length = 0;
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/Main3D.unity",
                UnityEditor.SceneManagement.OpenSceneMode.Single);

            var loop = UnityEngine.Object.FindFirstObjectByType<Dreamcore.D3.Loop3D>();
            Assert(loop != null, "Main3D 里必须有 Loop3D");
            if (loop == null) { Report(); return; }

            Assert(loop.anomalies.Count == 10, "异常表应有 10 条，实际 " + loop.anomalies.Count);

            var ids = new HashSet<string>();
            int refsTotal = 0;
            foreach (var e in loop.anomalies)
            {
                Assert(!string.IsNullOrEmpty(e.id) && ids.Add(e.id), "id 非空且不重复：" + e.id);
                Assert(AnomalyTitles.Table.ContainsKey(e.id), "id 在标题表里登记过：" + e.id);
                Assert(!string.IsNullOrEmpty(e.title), "title 非空：" + e.id);
                Assert(e.show.Count + e.hide.Count > 0, "至少指认一个物体：" + e.id);

                foreach (var g in e.show)
                {
                    refsTotal++;
                    Assert(g != null, "show 引用非空：" + e.id);
                    if (g != null) Assert(!g.activeSelf, "show 物体初始必须关着（异常未出现）：" + e.id + "/" + g.name);
                }
                foreach (var g in e.hide)
                {
                    refsTotal++;
                    Assert(g != null, "hide 引用非空（指认失败会被丢弃，这里必须是真的）：" + e.id);
                    if (g != null) Assert(g.activeSelf, "hide 目标初始必须开着：" + e.id + "/" + g.name);
                }
            }
            Assert(refsTotal >= 15, "10 组异常总共至少指认 15 个物体，实际 " + refsTotal);

            // 常驻黑影必须已经消失（显隐权交回目录）
            foreach (var sf in UnityEngine.Object.FindObjectsByType<ShadeFigure>(FindObjectsSortMode.None))
                Assert(!sf.gameObject.activeSelf, "常驻黑影不该存在：" + sf.name);

            // 判定门与玩家：一条前进 + 一条回头
            var gates = UnityEngine.Object.FindObjectsByType<TriggerGate>(FindObjectsSortMode.None);
            int fwd = 0, back = 0;
            foreach (var g in gates) { if (g.isForwardGate) fwd++; else back++; }
            Assert(fwd == 1 && back == 1, "两端判定门各一个，实际 前" + fwd + "/后" + back);
            var fpc = UnityEngine.Object.FindFirstObjectByType<FirstPersonController>();
            Assert(fpc != null && fpc.GetComponent<CharacterController>() != null, "玩家带 CharacterController");
            Assert(Camera.main != null, "Main Camera 在位");

            if (Environment.GetEnvironmentVariable("VERIFY_BAD") == "1")
                Assert(loop.anomalies.Count == 999, "坏样本：这条断言必须失败");

            Report();
        }

        static void Report()
        {
            if (fails.Length == 0) Debug.Log("[VERIFY] PASS: 装配断言全过");
            else Debug.LogError("[VERIFY] FAIL:\n" + fails);
        }
```

`VerifyGameplay()` 末尾那段 `if (fails.Length == 0) … else …` 换成调用 `Report();`（两个断言口共用同一套日志约定）。

本段用到 `ShadeFigure` / `TriggerGate` / `FirstPersonController`，它们都在 `Dreamcore.D3` 下，而 `Verify3D.cs`（Task 1 写的）的 using 区只有 `System`/`System.Collections.Generic`/`System.Text`/`UnityEditor`/`UnityEngine`。**在 using 区补一行**，否则会报 CS0246：

```csharp
using Dreamcore.D3;
```

（`Loop3D` 仍按已写好的全限定名 `Dreamcore.D3.Loop3D` 引用，不用回头改。）

- [ ] **Step 3: 跑一次确认红**

先重建场景，再断言：

Run: `cd /g/AIgame/jam && "/e/Unity/Unity/Editor/Editor/Unity.exe" -batchmode -quit -projectPath G:/AIgame/jam/dreamcore -executeMethod Dreamcore.EditorTools.Bootstrap3D.Run -logFile logs/bs5-red.log; "/e/Unity/Unity/Editor/Editor/Unity.exe" -batchmode -quit -projectPath G:/AIgame/jam/dreamcore -executeMethod Dreamcore.EditorTools.Verify3D.VerifyAssembly -logFile logs/verify-asm-red.log; grep -E "\[VERIFY\]" logs/verify-asm-red.log | head -6`
Expected: `error CS`（`sceneLights` 字段已被 Task 4 删掉，这里就该编译失败）或一串 FAIL。**直接 PASS = 断言没牙，回去重写。**

- [ ] **Step 4: 写灰盒异常组**

```csharp
// dreamcore/Assets/_Project/Editor/GreyboxAnomalies.cs
using System.Collections.Generic;
using UnityEngine;
using Dreamcore;
using Dreamcore.D3;

namespace Dreamcore.EditorTools
{
    /// <summary>
    /// 用图元造出 base + anom/&lt;NN&gt;/{show,hide} 三层，名字严格按 AnomalyResolver 的约定。
    /// 只在 M1（还没有 FBX）期间存在；Task 7 换成实例化 FBX 后整个文件删除。
    /// hide 节点下只放"名字载体"（空 GameObject），与 FBX 里的 EMPTY 同构。
    /// </summary>
    public static class GreyboxAnomalies
    {
        const float HalfW = 1.1f;   // 贴墙摆放（走廊宽 2.4）
        const float BackZ = 23.4f;

        public static void Build(Transform root, IReadOnlyList<LightFlicker> lights)
        {
            groups.Clear();               // 静态缓存，每次重建场景都要归零
            anomRoot = New("anom", root).transform;
            var b = New("base", root).transform;

            // 可被 hide 指认的本体
            Prop("Poster_6", b, new Vector3(-HalfW, 1.5f, 6f), new Vector3(0.02f, 0.7f, 0.5f), C(0.86f, 0.83f, 0.72f));
            Prop("Poster_10", b, new Vector3(HalfW, 1.5f, 10f), new Vector3(0.02f, 0.7f, 0.5f), C(0.86f, 0.83f, 0.72f));
            Prop("Poster_18", b, new Vector3(-HalfW, 1.45f, 18f), new Vector3(0.02f, 0.6f, 0.44f), C(0.90f, 0.86f, 0.70f));
            Prop("Door_3", b, new Vector3(-HalfW, 1.05f, 3.4f), new Vector3(0.04f, 2.1f, 0.9f), C(0.42f, 0.30f, 0.22f));
            Prop("MailboxDoor", b, new Vector3(HalfW, 1.25f, 12.6f), new Vector3(0.03f, 0.75f, 0.95f), C(0.48f, 0.49f, 0.52f));
            Prop("BikeUp", b, new Vector3(HalfW - 0.25f, 0.55f, 15.5f), new Vector3(0.55f, 1.1f, 0.2f), C(0.30f, 0.32f, 0.36f));
            Prop("WindowBuildings", b, new Vector3(HalfW + 0.4f, 1.9f, 9f), new Vector3(0.02f, 1.6f, 3.4f), C(0.62f, 0.66f, 0.74f));

            // anom01 声控灯全灭，只剩绿色应急光：hide 用灯名指认，show 是一盏低压应急灯
            var g01s = Show("01"); var g01h = Hide("01");
            var emerg = New("emergencyLight", g01s);
            emerg.transform.localPosition = new Vector3(0f, 0.35f, 12f);
            var el = emerg.AddComponent<Light>();
            el.type = LightType.Point;
            el.color = new Color(0.25f, 0.9f, 0.35f);
            el.intensity = 0.9f;
            el.range = 6f;
            foreach (var lf in lights)
                Proxy("Light_" + lf.gameObject.name.Split('_')[1] + "_off", g01h, lf.transform.position);

            // anom02 尽头多出背对镜头的人影
            var fig = Prop("extraFigure", Show("02"), new Vector3(0f, 0.9f, BackZ - 0.6f),
                new Vector3(0.8f, 1.8f, 1f), C(0.02f, 0.02f, 0.02f));
            Object.DestroyImmediate(fig.GetComponent<Collider>());
            fig.AddComponent<ShadeFigure>();

            // anom03 一户防盗门半开（closed ↔ open 互换）
            Swap("03", b, "Door_3", new Vector3(-HalfW + 0.42f, 1.05f, 3.4f),
                new Vector3(0.9f, 2.1f, 0.04f), C(0.42f, 0.30f, 0.22f));

            // anom04 开锁小广告整排消失（纯 hide，无 show）
            var h04 = Hide("04");
            Proxy("Poster_6_off", h04, new Vector3(-HalfW, 1.5f, 6f));
            Proxy("Poster_10_off", h04, new Vector3(HalfW, 1.5f, 10f));
            Proxy("Poster_18_off", h04, new Vector3(-HalfW, 1.45f, 18f));

            // anom05 地上多出一双小孩旧布鞋
            Prop("Shoes", Show("05"), new Vector3(-0.35f, 0.04f, 7.2f), new Vector3(0.42f, 0.08f, 0.18f), C(0.35f, 0.22f, 0.18f));

            // anom06 靠墙的旧自行车倒了
            Swap("06", b, "BikeUp", new Vector3(HalfW - 0.15f, 0.22f, 15.5f),
                new Vector3(1.05f, 0.42f, 0.5f), C(0.30f, 0.32f, 0.36f));

            // anom07 信报箱小门全部敞开
            Swap("07", b, "MailboxDoor", new Vector3(HalfW - 0.32f, 1.25f, 12.6f),
                new Vector3(0.65f, 0.75f, 0.03f), C(0.48f, 0.49f, 0.52f));

            // anom08 门缝下渗出一滩水
            var water = Prop("WaterPuddle", Show("08"), new Vector3(-HalfW + 0.35f, 0.02f, 3.4f),
                new Vector3(0.7f, 0.01f, 0.45f), C(0.10f, 0.12f, 0.14f));
            water.GetComponent<MeshRenderer>().sharedMaterial =
                new Material(Shader.Find("Universal Render Pipeline/Unlit")) { color = new Color(0.10f, 0.12f, 0.14f) };

            // anom09 天花板垂下一段红电线
            Prop("RedWire", Show("09"), new Vector3(0.3f, 2.45f, 17.2f), new Vector3(0.035f, 1.1f, 0.035f), C(0.62f, 0.10f, 0.10f));

            // anom10 窗外该有楼的地方全黑
            Swap("10", b, "WindowBuildings", new Vector3(HalfW + 0.4f, 1.9f, 9f),
                new Vector3(0.02f, 1.6f, 3.4f), C(0.01f, 0.01f, 0.015f));
        }

        /// <summary>base 里换掉一个：show 放替代品，hide 放 "<原名>_off" 名字载体。</summary>
        static void Swap(string id, Transform baseNode, string targetName, Vector3 pos, Vector3 scale, Color col)
        {
            Prop(targetName + "_open", Show(id), pos, scale, col);
            Proxy(targetName + "_off", Hide(id), baseNode.Find(targetName).position);
        }

        // ---------- 层级与图元 ----------

        static readonly Dictionary<string, Transform> groups = new Dictionary<string, Transform>();

        static Transform Show(string id) { return Group(id, "show"); }
        static Transform Hide(string id) { return Group(id, "hide"); }

        static Transform Group(string id, string leaf)
        {
            string key = id + "/" + leaf;
            Transform t;
            if (groups.TryGetValue(key, out t)) return t;
            var idNode = GroupRoot(id);
            t = New(leaf, idNode).transform;
            groups[key] = t;
            return t;
        }

        static Transform GroupRoot(string id)
        {
            Transform t;
            if (groups.TryGetValue("id/" + id, out t)) return t;
            t = New(id, anomRoot).transform;
            groups["id/" + id] = t;
            return t;
        }

        static Transform anomRoot;

        static GameObject New(string name, Transform parent)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        static GameObject Prop(string name, Transform parent, Vector3 pos, Vector3 scale, Color col)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial =
                new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name + "_mat", color = col };
            return go;
        }

        static void Proxy(string name, Transform parent, Vector3 worldPos)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
        }

        static Color C(float r, float g, float b) { return new Color(r, g, b); }
    }
}
```

上面这段有两个必须留意的点：`groups`/`anomRoot` 是静态状态，所以 `Build()` 开头第一件事就是 `groups.Clear()` 再取 `anomRoot`（已经写在代码里，别再挪动顺序）；`New` 返回 `GameObject`，凡是要当父节点用的地方都得 `.transform`。

- [ ] **Step 5: 改 `Bootstrap3D` 的三处接线**

1. 删掉 `Bootstrap3D.cs:104-114` 整段"黑影贴片"（`shades` 列表与 `foreach`）。人影的显隐权归异常目录。
2. 在 `MakeGate("GateForward", …)` 之后（原 `:118` 后）插入一行：

```csharp
            GreyboxAnomalies.Build(root, lights);
```

3. 把 `Bootstrap3D.cs:147-151` 的玩法循环段替换为：

```csharp
            // 玩法循环
            var loopGo = new GameObject("GameLoop");
            var loop = loopGo.AddComponent<Loop3D>();
            var entries = AnomalyResolver.Resolve(root);
            foreach (var e in entries) e.SettleToNormal();
            loop.anomalies = entries;
            if (entries.Count == 0) Debug.LogError("[Bootstrap3D] 异常表为空，玩法不成立。");
```

（`AnomalyResolver`/`AnomalyEntry` 在 `namespace Dreamcore` 下，`Dreamcore.EditorTools` 是它的嵌套命名空间，外层直接可见，不需要加 `using`。）

- [ ] **Step 6: 跑绿**

Run: 同 Step 3 的两条命令，日志写 `logs/bs5.log` 与 `logs/verify-asm.log`。
Expected: `grep -cE "error CS" logs/bs5.log` 为 `0`；`grep "\[VERIFY\]" logs/verify-asm.log` 全 `ok` + `[VERIFY] PASS: 装配断言全过`。

- [ ] **Step 7: 坏样本回喂**

Run: 同 Step 6 第二条命令，前缀 `VERIFY_BAD=1`，日志 `logs/verify-asm-bad.log`。
Expected: `[VERIFY] FAIL:` 且含"坏样本"。**Step 6 的 PASS 只有在这里见过 FAIL 之后才算背书。**

- [ ] **Step 8: 出货 M1 可提交物（3D）**

Run: `cd /g/AIgame/jam && ./tools/build.sh 3d 2>&1 | tail -6`
Expected: 末行 `== 完成，提交物：/g/AIgame/jam/builds/win64-3d/Dreamcore-win64.zip ==`。

Run: `cd /g/AIgame/jam && ls -la builds/win64-3d/Dreamcore.exe builds/win64-3d/Dreamcore-win64.zip && ls -la builds/win64/Dreamcore.exe`
Expected: 三个文件都在，且 `builds/win64/Dreamcore.exe` 的时间戳仍是 2D 那次构建的（**没被覆盖** = 分流生效）。

- [ ] **Step 9: 真人试玩一轮（M1 出口，不能跳）**

Run: `cd /g/AIgame/jam/builds/win64-3d && ./Dreamcore.exe`，手动打一轮。逐项核对：

| # | 观察 | 通过判据 |
|---|---|---|
| 1 | 第 1 层 | 走廊干净、没有任何异常物（记忆锚） |
| 2 | 前进到尽头 | 深度 +1，HUD 数字变了 |
| 3 | 有异常的那层走回起点 | 判定为对，深度 +1 |
| 4 | 干净层走回起点 | 错 +1，HUD 错数变化 |
| 5 | 换层后 | 上一层的异常物**消失**（Revert 生效，不残留） |
| 6 | 错满 3 次 | 坏结语文案出现，不再判定 |
| 7 | 走到第 8 层 | 好结语文案出现 |

任一项不过 → 停下修，别往下堆美术。修完重跑 Step 6–8。

- [ ] **Step 10: Commit**

```bash
git add dreamcore/Assets/_Project/Editor/GreyboxAnomalies.cs \
        dreamcore/Assets/_Project/Editor/Bootstrap3D.cs \
        dreamcore/Assets/_Project/Editor/Verify3D.cs \
        dreamcore/Assets/_Project/Editor/*.meta \
        tools/build.sh
git commit -m "feat: 灰盒异常组接入 Loop3D，3D 构建产物分流（M1 可提交物）"
```

---

### Task 6: 正式走廊建模与异常变体集（M2 建模段）

本任务只把 FBX 造出来并在 Unity 里**验层级**，不动 `Bootstrap3D` 的装配——灰盒继续顶着，M1 的提交物始终可出货。
切装配是 Task 7。这样任何一步翻车都不会让仓库处于"既不能玩也没有灰盒"的状态。

**轴映射是本任务唯一的真不确定项**，所以两条腿一起走：建模时走廊长度沿 Blender `+X`、高度沿 `+Z`，Unity 侧用一个 `-90°` Y 轴旋转把长度轴对到世界 `+Z`；
再用一条**方向性断言**（包围盒必须在 Z 方向最长）把轴搞反的情况当场抓出来，而不是等玩家走进墙里。

**Files:**
- Create: `blender/gen_corridor.py`
- Create: `blender/gen_anomalies.py`
- Create: `blender/build_scene.py`
- Create: `tools/blender_build.sh`
- Create: `blender/README.md`
- Create: `dreamcore/Assets/Content/3D/Corridor.fbx`（脚本产物）
- Modify: `dreamcore/Assets/_Project/Editor/Verify3D.cs`（追加 `VerifyFbx()`）

**Interfaces:**
- Consumes: Task 1 实测的集合→层级映射与命名保真结论（`blender/README.md` 那张表）；Task 3 的 `AnomalyResolver`（本任务直接拿来验 FBX 资产）；Task 5 定下的物件命名（`Door_3`/`Poster_6`/`BikeUp`/`MailboxDoor`/`WindowBuildings`/`Light_<z>`，`*_off` 代理）
- Produces: `dreamcore/Assets/Content/3D/Corridor.fbx`（根节点 `Corridor`，子节点 `base` 与 `anom`）；`Dreamcore.EditorTools.Verify3D.VerifyFbx()`；`tools/blender_build.sh`（退出码 0 = FBX 落盘且日志无 Error）；`AnomalyTitles` 的 10 个 id 在 FBX 侧全部有实体（Task 7 才能直接切装配）

- [ ] **Step 1: 写 base 走廊建模脚本**

```python
# blender/gen_corridor.py
# base 走廊几何的唯一真相：宽 2.4 × 高 3 × 长 24，中式筒子楼。
# 建模轴：长度沿 +X，高度沿 +Z（Blender Z-up）。Unity 侧用 -90° Y 旋转对齐，见 README。
import bpy

WIDTH, HEIGHT, LENGTH = 2.4, 3.0, 24.0
LIGHT_ZS = [2.0, 6.0, 10.0, 14.0, 18.0, 22.0]   # 沿走廊长度的位置，Unity 侧按此建点光


def coll(name, parent):
    found = bpy.data.collections.get(name)
    c = found if found is not None and found.name == name else bpy.data.collections.new(name)
    if c not in parent.children:
        parent.children.link(c)
    return c


def move_to(ob, collection):
    for c in list(ob.users_collection):
        c.objects.unlink(ob)
    collection.objects.link(ob)


def box(name, collection, along, height, side, size):
    """along=沿走廊长度(X), height=离地(Z), side=横向(Y, 走廊中线为 0)。"""
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(along, side, height))
    ob = bpy.context.active_object
    ob.name = name
    ob.scale = size
    bpy.ops.object.transform_apply(scale=True)
    move_to(ob, collection)
    return ob


def empty(name, collection, along, height, side):
    ob = bpy.data.objects.new(name, None)
    ob.location = (along, side, height)
    collection.objects.link(ob)
    return ob


def build(scene_coll):
    base = coll("base", scene_coll)
    half_w = WIDTH * 0.5
    half_l = LENGTH * 0.5

    box("Floor", base, half_l, 0.05, 0.0, (LENGTH, WIDTH, 0.1))
    box("WallLeft", base, half_l, HEIGHT * 0.5, -half_w, (LENGTH, 0.1, HEIGHT))
    box("WallRight", base, half_l, HEIGHT * 0.5, half_w, (LENGTH, 0.1, HEIGHT))
    box("Ceiling", base, half_l, HEIGHT + 0.05, 0.0, (LENGTH, WIDTH, 0.1))
    box("EndWall", base, LENGTH, HEIGHT * 0.5, 0.0, (0.1, WIDTH, HEIGHT))
    box("WainscotL", base, half_l, 0.6, -half_w + 0.06, (LENGTH, 0.012, 1.2))   # 绿漆墙裙
    box("WainscotR", base, half_l, 0.6, half_w - 0.06, (LENGTH, 0.012, 1.2))

    # 可被异常指认的本体（命名与 Task 5 灰盒逐字一致）
    box("Door_3", base, 3.4, 1.05, -half_w + 0.06, (0.9, 0.04, 2.1))
    box("Door_9", base, 9.0, 1.05, -half_w + 0.06, (0.9, 0.04, 2.1))
    box("Poster_6", base, 6.0, 1.5, -half_w + 0.07, (0.5, 0.02, 0.7))
    box("Poster_10", base, 10.0, 1.5, half_w - 0.07, (0.5, 0.02, 0.7))
    box("Poster_18", base, 18.0, 1.45, -half_w + 0.07, (0.44, 0.02, 0.6))
    box("MailboxBody", base, 12.6, 1.25, half_w - 0.12, (1.0, 0.22, 0.8))
    box("MailboxDoor", base, 12.6, 1.25, half_w - 0.02, (0.95, 0.03, 0.75))
    box("BikeUp", base, 15.5, 0.55, half_w - 0.30, (0.2, 0.55, 1.1))
    box("WindowBuildings", base, 9.0, 1.9, half_w + 0.45, (3.4, 0.02, 1.6))  # 窗外该有楼的地方

    for z in LIGHT_ZS:
        empty("Light_%d" % int(z), base, z, HEIGHT - 0.25, 0.0)

    return base
```

- [ ] **Step 2: 写异常变体集脚本（10 组，命名照 `AnomalyTitles`）**

```python
# blender/gen_anomalies.py
# anom/<NN>/{show,hide}。show 里是真几何，hide 里只放 EMPTY 名字载体（"<base名>_off"）。
# hide 代理自己在 Unity 侧被 AnomalyResolver 关掉，永不显示。
import bpy
from gen_corridor import HEIGHT, WIDTH, coll, box, empty, move_to

HALF_W = WIDTH * 0.5


def group(anom_root, nn):
    g = coll(nn, anom_root)
    return coll("show", g), coll("hide", g)


def build(scene_coll, light_zs):
    anom = coll("anom", scene_coll)

    s, h = group(anom, "01")            # 声控灯全灭，只剩绿色应急光
    box("EmergencyLight", s, 12.0, 0.35, 0.0, (0.3, 0.12, 0.12))
    for z in light_zs:
        empty("Light_%d_off" % int(z), h, z, HEIGHT - 0.25, 0.0)

    s, h = group(anom, "02")            # 尽头多出背对镜头的人影
    box("extraFigure", s, 23.0, 0.9, 0.0, (0.2, 0.8, 1.8))

    s, h = group(anom, "03")            # 一户防盗门半开
    box("Door_3_open", s, 3.85, 1.05, -HALF_W + 0.45, (0.04, 0.9, 2.1))
    empty("Door_3_off", h, 3.4, 1.05, -HALF_W + 0.06)

    s, h = group(anom, "04")            # 开锁疏通小广告整排消失
    empty("Poster_6_off", h, 6.0, 1.5, -HALF_W + 0.07)
    empty("Poster_10_off", h, 10.0, 1.5, HALF_W - 0.07)
    empty("Poster_18_off", h, 18.0, 1.45, -HALF_W + 0.07)

    s, h = group(anom, "05")            # 地上多出一双小孩旧布鞋
    box("Shoes", s, 7.2, 0.04, -0.35, (0.18, 0.42, 0.08))

    s, h = group(anom, "06")            # 靠墙的旧自行车倒了
    box("BikeUp_open", s, 15.5, 0.22, HALF_W - 0.20, (0.5, 1.05, 0.42))
    empty("BikeUp_off", h, 15.5, 0.55, HALF_W - 0.30)

    s, h = group(anom, "07")            # 信报箱小门全部敞开
    box("MailboxDoor_open", s, 12.6, 1.25, HALF_W - 0.35, (0.03, 0.65, 0.75))
    empty("MailboxDoor_off", h, 12.6, 1.25, HALF_W - 0.02)

    s, h = group(anom, "08")            # 门缝下渗出一滩水
    box("WaterPuddle", s, 3.4, 0.02, -HALF_W + 0.35, (0.45, 0.7, 0.01))

    s, h = group(anom, "09")            # 天花板垂下一段红电线
    box("RedWire", s, 17.2, 2.45, 0.30, (0.035, 0.035, 1.1))

    s, h = group(anom, "10")            # 窗外该有楼的地方全黑
    box("WindowBuildings_open", s, 9.0, 1.9, HALF_W + 0.45, (3.4, 0.02, 1.6))
    empty("WindowBuildings_off", h, 9.0, 1.9, HALF_W + 0.45)

    return anom
```

`BikeUp_open` / `MailboxDoor_open` 这类名字是 `Swap` 约定的产物（`<原名>_open`），`AnomalyResolver` 只按 `*_off` 反推目标名，不认 `_open`，所以随艺师怎么叫都行——但**必须与 base 里的名字不同名**（Blender 名字全局唯一，天然满足）。

- [ ] **Step 3: 写总入口与胶水**

```python
# blender/build_scene.py
# 总入口：重建工厂设置 → base + anom → 存 .blend → 导出 FBX 到 Unity 目录。
# 用法: blender --background --python blender/build_scene.py
import os
import sys
import bpy

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import gen_corridor
import gen_anomalies

BLEND_OUT = os.environ.get("CORRIDOR_BLEND", os.path.join(HERE, "Corridor.blend"))
FBX_OUT = os.environ.get(
    "CORRIDOR_FBX_OUT",
    r"G:\AIgame\jam\dreamcore\Assets\Content\3D\Corridor.fbx")

bpy.ops.wm.read_factory_settings(use_empty=True)
scene_coll = bpy.context.scene.collection

gen_corridor.build(scene_coll)
gen_anomalies.build(scene_coll, gen_corridor.LIGHT_ZS)

bpy.ops.wm.save_as_mainfile(filepath=BLEND_OUT)
os.makedirs(os.path.dirname(FBX_OUT), exist_ok=True)
bpy.ops.export_scene.fbx(
    filepath=FBX_OUT,
    use_selection=False,
    use_visible=True,
    object_types={"MESH", "EMPTY"},
    use_mesh_modifiers=True,
    apply_unit_scale=True,
    bake_space_transform=False,
    path_mode="COPY",
    embed_textures=False,
    add_leaf_bones=False,
    use_metadata=False,
)

names = [o.name for o in bpy.data.objects]
print("[BUILD] objects=%d exported=%s exists=%s" % (len(names), FBX_OUT, os.path.exists(FBX_OUT)))
print("[BUILD] base_children=%s" % [o.name for o in bpy.data.collections["base"].objects])
print("[BUILD] anom_ids=%s" % sorted(c.name for c in bpy.data.collections["anom"].children))
```

```sh
# tools/blender_build.sh
# headless 建模 + 导出 + 产物断言。退出码 0 = FBX 在位且日志无 Error。
set -eu
JAM_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
BLENDER="${BLENDER_EXE:-/d/Game Library/Steam/steamapps/common/Blender/blender.exe}"
FBX="$JAM_ROOT/dreamcore/Assets/Content/3D/Corridor.fbx"
mkdir -p "$JAM_ROOT/logs"

"$BLENDER" --background --python-exit-code 4 --python "$JAM_ROOT/blender/build_scene.py" \
  > "$JAM_ROOT/logs/blender-build.log" 2>&1 || { echo "!! blender 退出码非 0，见 logs/blender-build.log"; exit 1; }

grep -E "\[BUILD\]|\[PROBE\]|Error" "$JAM_ROOT/logs/blender-build.log" || true
[ -f "$FBX" ] || { echo "!! 缺产物：$FBX"; exit 1; }
grep -q "^\[BUILD\] anom_ids=.*'10'" "$JAM_ROOT/logs/blender-build.log" \
  || { echo "!! 10 组异常没造全，见 logs/blender-build.log"; exit 1; }
echo "== FBX OK: $FBX ($(wc -c < "$FBX") bytes) =="
```

- [ ] **Step 4: 跑建模胶水**

Run: `cd /g/AIgame/jam && ./tools/blender_build.sh`
Expected: `[BUILD] objects=44 exported=... exists=True`（base 22：7 面 + 9 个可指认本体 + 6 盏灯位空物体；anom 22：9 个 show + 13 个 `*_off` 代理）、`anom_ids=['01', '02', ..., '10']`，末行 `== FBX OK: ... ==`。
`--python-exit-code 4` 是实测在位的开关（`blender --background --help` 里有这一条）：**不加它，Python 抛异常时 Blender 照样退 0**，那个 `||` 门就是假的。
`Error: Not found: bpy.ops.export_scene.fbx` = 导出插件没启用，脚本前加 `bpy.ops.wm.save_userpref()` 前先 `addon_enable('object_export_scene_fbx')`；`objects` 数量对不上就照 `[BUILD] base_children` 逐项查。

- [ ] **Step 5: 写 FBX 层级断言（Unity 侧，含方向性断言）**

追加进 `Verify3D.cs`：

```csharp
        /// <summary>只验 FBX 资产层级，不碰 Main3D：把 prefab 实例化进临时场景，直接跑 AnomalyResolver。</summary>
        public static void VerifyFbx()
        {
            fails.Length = 0;
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Content/3D/Corridor.fbx");
            Assert(asset != null, "Corridor.fbx 已导入为 GameObject 资产");
            if (asset == null) { Report(); return; }

            var tmp = new GameObject("verify_fbx_stage");
            var inst = UnityEngine.Object.Instantiate(asset);
            inst.transform.SetParent(tmp.transform, false);
            // 轴向补偿的唯一真相是 Bootstrap3D.FbxYaw（见 Step 6），这里跟着它转，别自己写角度
            inst.transform.localRotation = Quaternion.Euler(0f, Bootstrap3D.FbxYaw, 0f);

            // base/anom 不一定是根的直接子节点：Blender 的场景集合可能多出一层 Collection，
            // 所以递归找（与 AnomalyResolver 的口径一致），但层数要记进 README。
            var baseNode = FindByName(inst.transform, "base");
            var anomNode = FindByName(inst.transform, "anom");
            Assert(baseNode != null, "FBX 里能找到 base 节点");
            Assert(anomNode != null, "FBX 里能找到 anom 节点");
            if (baseNode == null || anomNode == null) { UnityEngine.Object.DestroyImmediate(tmp); Report(); return; }
            Assert(AnomalyResolver.Resolve(inst.transform).Count > 0, "解析器能在这套层级上跑出异常");
            Assert(inst.transform.name.StartsWith("Corridor"), "根节点名取自文件名 Corridor（Instantiate 会加 (Clone)），实际 " + inst.transform.name);

            // 方向性断言：走廊必须沿世界 +Z 展开（-90° Y 旋转之后）。搞反轴会在这里断掉。
            var r = new Bounds(Vector3.zero, Vector3.zero);
            bool first = true;
            foreach (var mr in inst.GetComponentsInChildren<Renderer>())
            {
                if (first) { r = mr.bounds; first = false; }
                else r.Encapsulate(mr.bounds);
            }
            Assert(r.size.z > 20f && r.size.z > r.size.x * 2f,
                "包围盒 Z 方向最长（实测 size=" + r.size + "）");
            Assert(r.size.y > 2.5f && r.size.y < 3.6f, "高度约 3m（实测 " + r.size.y + "）");
            Assert(r.min.y > -0.25f, "地面不穿到地板下（实测 min.y=" + r.min.y + "）");

            // 命名保真：契约里的名字一个都不许被导入器改写
            foreach (var want in new[] { "Floor", "WainscotL", "Door_3", "Poster_6", "MailboxDoor",
                                         "BikeUp", "WindowBuildings", "Light_2", "Light_22" })
                Assert(FindByName(inst.transform, want) != null, "base 名字保真：" + want);

            // 解析器直接对 FBX 层级跑通
            var entries = AnomalyResolver.Resolve(inst.transform);
            Assert(entries.Count == 10, "解析出 10 组异常，实际 " + entries.Count);
            int showTotal = 0, hideTotal = 0;
            foreach (var e in entries)
            {
                Assert(AnomalyTitles.Table.ContainsKey(e.id), "id 在标题表里：" + e.id);
                showTotal += e.show.Count; hideTotal += e.hide.Count;
            }
            Assert(entries[0].hide.Count == 6, "anom01 应指认 6 盏灯，实际 " + entries[0].hide.Count);
            Assert(hideTotal >= 12, "hide 指认总数 ≥12，实际 " + hideTotal);
            Assert(showTotal >= 9, "show 物体总数 ≥9，实际 " + showTotal);

            UnityEngine.Object.DestroyImmediate(tmp);

            if (Environment.GetEnvironmentVariable("VERIFY_BAD") == "1")
                Assert(entries.Count == 3, "坏样本：这条断言必须失败");

            Report();
        }
```

`FindByName(Transform, string)` 已在 Task 1（`Verify3D.cs:230`）定义，本段直接复用，**不要再写一遍**（重复定义 = CS0111）。
解析失败只发 `Debug.LogWarning`，所以警告的核查放在 Step 7 用日志 grep 做，不在这里加组件。

- [ ] **Step 6: 定下轴向补偿的单一真相 `Bootstrap3D.FbxYaw`**

在 `Bootstrap3D` 类顶部（`const string FontPath` 之后）加：

```csharp
        // FBX 根绕 Y 的旋转角度：走廊长度必须落在世界 +Z。唯一落点，装配与验收都读它。
        public const float FbxYaw = -90f;
```

Blender 是 Z-up、建模时长度走 +X，Unity 是 Y-up、走廊要走 +Z，所以默认先按 `-90f` 试。
Run: `cd /g/AIgame/jam && "/e/Unity/Unity/Editor/Editor/Unity.exe" -batchmode -quit -projectPath G:/AIgame/jam/dreamcore -executeMethod Dreamcore.EditorTools.Verify3D.VerifyFbx -logFile logs/verify-fbx-yaw.log; grep -E "包围盒|高度约|地面不穿" logs/verify-fbx-yaw.log`

按日志里实测的 `size=(x, y, z)` 三分支处理，并把结论原样写进 `blender/README.md` 的轴映射小节：

| 实测 | 处置 |
|---|---|
| `z` 最长、`y≈3` | 通过，`FbxYaw` 保持 `-90f`（或按需微调为 `90f`：若 `z` 最长但地面/端墙方向反了，走廊会往 -Z 延伸，玩家出门就撞墙，用 `90f` 翻回来） |
| `x` 最长 | 导出器没做轴转换。改 `build_scene.py` 的 `export_scene.fbx(...)` 补 `axis_forward='-Z', axis_up='Y'` 重导，再跑一次；仍不行则 `FbxYaw` 归 `0f` 并把 `gen_corridor.py` 的长度轴改到 Blender `+Y`，重跑 Task 6 全部步骤 |
| `y` 最长 | 高度轴被当成水平轴，`bake_space_transform` 相关；改 `apply_unit_scale=False` 重导一次看变化，仍不行就在 `gen_corridor.py` 里把 `box()` 的 location 参数顺序换成 `(along, height, side)` 并同步改 `size` 分量——**只改一处，别两边都改** |

`FbxYaw` 只有这一处定义。Task 7 的装配读它，Step 5 的断言也读它，**任何"我在 Unity 里再转一次"的想法都是转两次**。

- [ ] **Step 7: 跑绿 + 坏样本**

Run: `cd /g/AIgame/jam && "/e/Unity/Unity/Editor/Editor/Unity.exe" -batchmode -quit -projectPath G:/AIgame/jam/dreamcore -executeMethod Dreamcore.EditorTools.Verify3D.VerifyFbx -logFile logs/verify-fbx.log; grep -E "\[VERIFY\]|Resolver" logs/verify-fbx.log | head -24`
Expected: 全 `ok` + `[VERIFY] PASS`，且**没有** `[Resolver] hide 指认不到 base 目标`（有就是 FBX 侧 `*_off` 与 base 名字对不上，逐条改名重导）。

Run: `VERIFY_BAD=1` 重跑同一条，日志 `logs/verify-fbx-bad.log`。
Expected: `[VERIFY] FAIL:` 含"坏样本"。

- [ ] **Step 8: 写契约文档并 Commit**

`blender/README.md` 至少写清四件事（给下个会话读的，不复述 spec）：集合路径约定与 `_off` 后缀；`base`/`anom` 里每个名字的当前清单（照 Step 4 日志粘）；轴映射实测结论（Step 7 的包围盒数字原样记下）；`tools/blender_build.sh` 的用法与退出码含义。

```bash
git add blender/ tools/blender_build.sh \
        dreamcore/Assets/Content/3D/Corridor.fbx \
        dreamcore/Assets/Content/3D/Corridor.fbx.meta \
        dreamcore/Assets/_Project/Editor/Verify3D.cs
git commit -m "feat: Blender 走廊建模与 10 组异常变体导出 FBX"
```

---

### Task 7: 切换装配到 FBX，删掉灰盒（**M2 出口**）

这一步把 M1 的灰盒顶掉，换成真实几何。三个已知坑必须在这一步处理干净，否则会以"能跑但不能玩"的形式通过：

1. FBX 网格**不带碰撞体**，也不带 Unity `Light`。不补碰撞体就是"穿墙走"，不收养灯就是"全黑走廊"。
2. 灯的位置真相在 Blender（`Light_2`…`Light_22` 是 EMPTY），组件真相在 Unity。按名前缀收养，别在两边各写一份坐标。
3. FBX 也没材质。不涂就是刺眼的导入白，先按名涂占位色，正式贴图是 Task 8 的事。

**Files:**
- Create: `tools/unity_verify.sh`
- Modify: `dreamcore/Assets/_Project/Editor/Bootstrap3D.cs`（整段替换 `CreateScene()`，新增 3 个 adopt helper，删 `MakeQuad`）
- Delete: `dreamcore/Assets/_Project/Editor/GreyboxAnomalies.cs` 与其 `.meta`

**Interfaces:**
- Consumes: `Corridor.fbx` 层级（Task 6）、`Bootstrap3D.FbxYaw`（Task 6 Step 6）、`AnomalyResolver`/`AnomalyEntry`（Task 3）、`Loop3D.anomalies`（Task 4）、`ShadeFigure`/`LightFlicker`（既有）
- Produces: `tools/unity_verify.sh`（一次跑完 VerifyFbx/VerifyAssembly/VerifyGameplay 三段，`VERIFY_BAD=1` 切换期望值，退出码 0 = 三段都符合预期）；`Bootstrap3D.CorridorFbxPath`；Main3D.unity 的正式装配（Task 8/9/10 都在它上面改）

- [ ] **Step 1: 写三段验收胶水**

```sh
#!/bin/sh
# 三段验收一次跑完。VERIFY_BAD=1 时把期望从 PASS 翻成 FAIL（坏样本回喂）。
set -eu
JAM_ROOT="$(cd "$(dirname "$0")/.." && pwd)"
UNITY="/e/Unity/Unity/Editor/Editor/Unity.exe"
PROJ="$JAM_ROOT/dreamcore"
mkdir -p "$JAM_ROOT/logs"
EXPECT=PASS
[ "${VERIFY_BAD:-0}" = "1" ] && EXPECT=FAIL

run_one() {
  method="$1"; tag="$2"
  "$UNITY" -batchmode -quit -projectPath "$PROJ" \
    -executeMethod "Dreamcore.EditorTools.Verify3D.$method" \
    -logFile "$JAM_ROOT/logs/verify-$tag.log" || true
  line=$(grep -E "\[VERIFY\] (PASS|FAIL)" "$JAM_ROOT/logs/verify-$tag.log" | tail -1 || true)
  case "$line" in
    *"VERIFY] $EXPECT"*) echo "[$tag] 符合预期 $EXPECT :: $line" ;;
    *) echo "[$tag] 期望 $EXPECT，实际这一行：$line"; exit 1 ;;
  esac
}

run_one VerifyFbx fbx
run_one VerifyAssembly asm
run_one VerifyGameplay play
echo "== 分段验收全部符合预期（EXPECT=$EXPECT）=="
```

Run: `cd /g/AIgame/jam && chmod +x tools/unity_verify.sh && bash -n tools/unity_verify.sh && echo SYNTAX-OK`
Expected: `SYNTAX-OK`。注意 `EXPECT=FAIL` 那行在 `set -e` 下是 `[ ] && EXPECT=FAIL` 形式：测试失败时列表返回非 0，但它是 `&&` 列表的**非末位**命令，不触发 errexit——这正是我们要的"条件赋值不致命"。

- [ ] **Step 2: 替换 `Bootstrap3D.CreateScene()`（`:58-160` 整段）**

```csharp
        static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = new Color(0.10f, 0.09f, 0.08f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.05f, 0.05f, 0.06f);
            RenderSettings.fogStartDistance = 2f;
            RenderSettings.fogEndDistance = 26f;

            var root = new GameObject("Corridor").transform;

            Material matFloor = MakeMat("Floor", new Color(0.30f, 0.28f, 0.25f));
            Material matWall = MakeMat("Wall", new Color(0.55f, 0.54f, 0.50f));
            Material matWainscot = MakeMat("Wainscot", new Color(0.22f, 0.38f, 0.26f)); // 绿漆墙裙
            Material matCeil = MakeMat("Ceiling", new Color(0.62f, 0.61f, 0.58f));
            Material matBlack = MakeUnlit("ShadeBlack", new Color(0.02f, 0.02f, 0.02f));

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(CorridorFbxPath);
            if (fbx == null) Debug.LogError("[Bootstrap3D] 缺 " + CorridorFbxPath + "，先跑 ./tools/blender_build.sh");

            var inst = Object.Instantiate(fbx);
            inst.transform.SetParent(root, false);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.Euler(0f, FbxYaw, 0f);

            var baseNode = FindDeep(inst.transform, "base");
            var anomNode = FindDeep(inst.transform, "anom");
            if (baseNode == null || anomNode == null)
                Debug.LogError("[Bootstrap3D] FBX 里缺 base/anom 节点，装配不成立。");
            else
            {
                PaintShell(baseNode, matFloor, matWall, matWainscot, matCeil);
                AdoptLights(baseNode);
                AttachBillboards(anomNode, matBlack);
            }

            // 判定门：起点=回头，尽头=前进
            MakeGate("GateBack", root, new Vector3(0f, 1.5f, 1.2f), false);
            MakeGate("GateForward", root, new Vector3(0f, 1.5f, CorridorLength - 1.2f), true);

            // Player：根(CharacterController) + 子相机
            var player = new GameObject("Player");
            player.tag = "Player";
            player.transform.position = new Vector3(0f, 0.1f, 1.8f);
            var fpc = player.AddComponent<FirstPersonController>();
            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(player.transform, false);
            camGo.transform.localPosition = new Vector3(0f, 1.6f, 0f);
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            cam.nearClipPlane = 0.1f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.02f, 0.025f);
            var camData = cam.GetUniversalAdditionalCameraData();
            camData.renderPostProcessing = true;
            camData.volumeLayerMask = ~0;
            fpc.cameraTransform = camGo.transform;

            // 全局 Volume（复用 2D 版梦核滤镜 Profile）
            var volGo = new GameObject("GlobalVolume");
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (vol.sharedProfile == null)
                Debug.LogWarning("[Bootstrap3D] 未找到 " + ProfilePath + "，先跑 2D 版 Bootstrap 生成滤镜 Profile。");

            // 玩法循环
            var loopGo = new GameObject("GameLoop");
            var loop = loopGo.AddComponent<Loop3D>();
            var entries = AnomalyResolver.Resolve(root);
            foreach (var e in entries) e.SettleToNormal();
            loop.anomalies = entries;
            if (entries.Count == 0) Debug.LogError("[Bootstrap3D] 异常表为空，玩法不成立。");

            CreateHud();
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

            EditorSceneManager.SaveScene(scene, Scene3DPath);
        }

        // ---------- FBX 收养 ----------

        /// <summary>base 网格：涂占位材质 + 补碰撞体（FBX 两者都没有）。含子层级——日历件挂在空物体之下，只遍历直接子节点会整批漏掉。</summary>
        static void PaintShell(Transform baseNode, Material floor, Material wall, Material wainscot, Material ceiling)
        {
            foreach (var mr in baseNode.GetComponentsInChildren<MeshRenderer>(true))
            {
                string n = mr.name;
                if (n == "Floor") mr.sharedMaterial = floor;
                else if (n == "Ceiling") mr.sharedMaterial = ceiling;
                else if (n.StartsWith("Wainscot")) mr.sharedMaterial = wainscot;
                else mr.sharedMaterial = wall;
                if (mr.GetComponent<Collider>() == null) mr.gameObject.AddComponent<BoxCollider>();
            }
        }

        static void AdoptLights(Transform baseNode)
        {
            foreach (var t in baseNode.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("Light_")) continue;
                var l = t.GetComponent<Light>();
                if (l == null) l = t.gameObject.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1f, 0.85f, 0.60f);
                l.intensity = 1.8f;
                l.range = 7f;
                l.shadows = LightShadows.None;
                if (t.GetComponent<LightFlicker>() == null) t.gameObject.AddComponent<LightFlicker>();
            }
        }

        /// <summary>人影要始终对着玩家才有效——billboard 组件只能由 Unity 挂，FBX 给不了。</summary>
        static void AttachBillboards(Transform anomNode, Material black)
        {
            var fig = FindDeep(anomNode, "extraFigure");
            if (fig == null) { Debug.LogWarning("[Bootstrap3D] 没找到 extraFigure，anom02 不会有人影。"); return; }
            var mr = fig.GetComponent<MeshRenderer>();
            if (mr != null) mr.sharedMaterial = black;
            if (fig.GetComponent<ShadeFigure>() == null) fig.gameObject.AddComponent<ShadeFigure>();
        }

        static Transform FindDeep(Transform t, string name)
        {
            if (t.name == name) return t;
            for (int i = 0; i < t.childCount; i++)
            {
                var r = FindDeep(t.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }
```

同时在类顶部常量区（`const string FontPath` 之后）加一行，并删掉原 `:104-114` 黑影块遗留的 `MakeQuad`（已无人调用）与 `:88-102` 的造灯循环、`GreyboxAnomalies.Build(root, lights);` 那一行：

```csharp
        const string CorridorFbxPath = "Assets/Content/3D/Corridor.fbx";
```

（`CorridorWidth`/`CorridorHeight` 留着当文档；`MakeBox` 被 `MakeGate` 用着，别删。）

- [ ] **Step 3: 删灰盒文件**

Run: `cd /g/AIgame/jam && git rm dreamcore/Assets/_Project/Editor/GreyboxAnomalies.cs dreamcore/Assets/_Project/Editor/GreyboxAnomalies.cs.meta`
Expected: `rm '...'` 两行。（`.meta` 若尚未提交过，用 `rm -f` 并跳过 git rm 的那一条。）

- [ ] **Step 4: 重建场景 + 三段验收**

Run: `cd /g/AIgame/jam && "/e/Unity/Unity/Editor/Editor/Unity.exe" -batchmode -quit -projectPath G:/AIgame/jam/dreamcore -executeMethod Dreamcore.EditorTools.Bootstrap3D.Run -logFile logs/bs7.log && grep -cE "error CS" logs/bs7.log; ./tools/unity_verify.sh`
Expected: `0` 然后三行 `符合预期 PASS` + `== 分段验收全部符合预期（EXPECT=PASS）==`（三段：fbx / asm / play；Task 8、Task 9 会再插两段，横幅不写段数）。
`grep "Resolver" logs/verify-asm.log` 必须**空**——非空就是 FBX 的 `*_off` 与 base 名对不上。

- [ ] **Step 5: 坏样本回喂**

Run: `cd /g/AIgame/jam && VERIFY_BAD=1 ./tools/unity_verify.sh; echo "rc=$?"`
Expected: `rc=1` 且第一段的实际行是 `[VERIFY] FAIL:` 开头（`fbx` 段先断）。**Step 4 的三段 PASS 只在这里见过 FAIL 之后才算背书。**

- [ ] **Step 6: 重出 M2 提交物并真人试玩**

Run: `cd /g/AIgame/jam && ./tools/build.sh 3d 2>&1 | tail -4 && ls -la builds/win64-3d/Dreamcore-win64.zip`
Expected: `== 完成，提交物：.../builds/win64-3d/Dreamcore-win64.zip ==`。

Run: `cd /g/AIgame/jam/builds/win64-3d && ./Dreamcore.exe`，Task 5 Step 9 的 7 项重走一遍，另外加 4 项（真实几何独有的）：

| # | 观察 | 通过判据 |
|---|---|---|
| 8 | 贴墙走 | 墙有碰撞，不穿模；地面不塌陷 |
| 9 | 灯 | 6 盏暖黄点光沿走廊排开，会闪；anom01 触发时全灭只剩绿色应急光 |
| 10 | 人影 | 只在 anom02 那一层出现，且始终正对镜头（billboard） |
| 11 | 尺度 | 门约 2.1m、墙裙约 1.2m 高，看起来像人住的楼，不是模型比例游戏 |

第 9、10 项不过 = 收养步骤没生效，回 Step 2 查名字前缀；第 8 项不过 = `PaintShell` 的碰撞体没补上。

- [ ] **Step 7: Commit**

```bash
git add dreamcore/Assets/_Project/Editor/Bootstrap3D.cs tools/unity_verify.sh
git commit -m "feat: Bootstrap3D 装配真实走廊 FBX，收养灯与碰撞体（M2 出口）"
```

---

### Task 8: 程序化贴图 + 光照调参 + HUD 换 ScreenSpaceCamera（**M3 观感**）

贴图走"Blender 生成 PNG → Unity 侧按名涂材质"，**不走 FBX 材质**。理由与灯同理：FBX 的材质导入设置不可控，而 Unity 侧一行 `AssetDatabase.LoadAssetAtPath` 完全可控，且能复用 Task 7 已经写好的 `PaintShell` 涂色入口（只把纯色换成贴图）。

**Blender 侧 API 实测（5.2.2 LTS，本任务落笔前刚跑过）**：
- `bpy.types.Texture.to_image` **不存在**（`AttributeError: 'CloudsTexture' object has no attribute 'to_image'`）→ 不许用纹理 bake，改用 `bpy.data.images.new` + numpy 直写 `pixels`。
- `img.pixels[:] = numpy_array`（`float32`，长度 `w*h*4`）可用；`foreach_set`/`foreach_get` 可用；`alpha=False` 建出来的图仍是 4 通道（`len(pixels)=w*h*4` 实测 16384/64²）。
- `img.filepath_raw = "G:/…/x.png"; img.file_format='PNG'; img.save()` 可用，产物实测 265–408 bytes 起。
- `pixels` 索引 0 是**左下角**（实测把 index0 涂红、末位涂白，回读一致）→ 画竖向渐变时别把上下弄反。
- Blender 自带 numpy 2.3.4，可以直接 `import numpy`。

**Files:**
- Create: `blender/gen_textures.py`
- Modify: `blender/build_scene.py`（导出前调一次 `gen_textures.main()`）
- Modify: `tools/blender_build.sh`（加 PNG 产物断言）
- Modify: `dreamcore/Assets/_Project/Editor/Bootstrap3D.cs`（`PaintShell` 换贴图材质；HUD `:166` 换 `ScreenSpaceCamera`）
- Modify: `dreamcore/Assets/_Project/Editor/Verify3D.cs`（追加 `VerifyLook()`）
- Create: `dreamcore/Assets/Content/Textures/{terrazzo_floor,wainscot_green,wall_beige,ceiling_white,metal_rust,poster_ad}.png`（脚本产物）

**Interfaces:**
- Consumes: Task 7 的 `PaintShell(Transform, …)` 涂材质入口、`Corridor.fbx` 的 base 命名、`tools/unity_verify.sh` 的三段结构
- Produces: `Bootstrap3D.MakeTexMat(string name, string texPath, Vector2 tiling, Color tint)`；`Dreamcore.EditorTools.Verify3D.VerifyLook()`；`blender/gen_textures.py` 的 `main(out_dir)` 约定（只认 `G:/AIgame/jam/dreamcore/Assets/Content/Textures`）；`tools/unity_verify.sh` 增加第四段 `VerifyLook`

- [ ] **Step 1: 写贴图生成脚本**

```python
# blender/gen_textures.py
# 程序化生成 6 张 PNG 到 Unity 的 Content/Textures。只用 images.new + numpy 直写像素，
# 不用 bpy.types.Texture.to_image（5.2 已实测不存在）。
import os
import numpy

SIZE = {"terrazzo_floor": 512, "wainscot_green": 512, "wall_beige": 512,
        "ceiling_white": 256, "metal_rust": 256, "poster_ad": 256}

OUT_DEFAULT = r"G:\AIgame\jam\dreamcore\Assets\Content\Textures"


def canvas(name, rgb):
    n = SIZE[name]
    a = numpy.zeros((n, n, 4), dtype=numpy.float32)
    a[:, :, 0] = rgb[0]
    a[:, :, 1] = rgb[1]
    a[:, :, 2] = rgb[2]
    a[:, :, 3] = 1.0
    return n, a


def grain(a, rng, amp):
    a[:, :, 0] += rng.normal(0, amp, a.shape[:2])
    a[:, :, 1] += rng.normal(0, amp, a.shape[:2])
    a[:, :, 2] += rng.normal(0, amp, a.shape[:2])
    return numpy.clip(a, 0.0, 1.0)


def speckle(a, rng, count, radius, color):
    n = a.shape[0]
    for _ in range(count):
        cx, cy = rng.integers(0, n, 2)
        r = int(radius)
        y0, y1 = max(0, cy - r), min(n, cy + r)
        x0, x1 = max(0, cx - r), min(n, cx + r)
        a[y0:y1, x0:x1, 0:3] = color
    return a


def streaks(a, rng, count, color, alpha):
    """竖向刷痕：墙裙/锈迹用。列坐标 x，索引 0 在左下角。"""
    n = a.shape[0]
    for _ in range(count):
        x = int(rng.integers(0, n))
        w = int(rng.integers(1, 4))
        top = float(rng.uniform(0.2, 1.0))          # 从顶部往下的高度比例
        y1 = int(n * top)
        y0 = max(0, y1 - int(n * rng.uniform(0.05, 0.35)))
        band = a[y0:y1, x:min(n, x + w), 0:3]
        a[y0:y1, x:min(n, x + w), 0:3] = band * (1 - alpha) + numpy.array(color) * alpha
    return a


def terrazzo():
    n, a = canvas("terrazzo_floor", (0.62, 0.60, 0.56))
    rng = numpy.random.default_rng(11)
    a = grain(a, rng, 0.035)
    for count, r, col in [(900, 1, (0.30, 0.29, 0.27)),   # 深灰碎石
                          (600, 2, (0.80, 0.79, 0.75)),   # 白石
                          (240, 2, (0.55, 0.34, 0.22)),   # 赭石
                          (90, 3, (0.25, 0.35, 0.28))]:   # 绿玻璃渣
        a = speckle(a, rng, count, r, col)
    # 磨光：中心略亮，四角略暗（水磨石打蜡感）
    y, x = numpy.mgrid[0:n, 0:n]
    d = ((x - n / 2) ** 2 + (y - n / 2) ** 2) / (n * n / 2.0)
    a[:, :, 0:3] *= (1.06 - 0.14 * d)[:, :, None]
    return "terrazzo_floor", a


def wainscot():
    n, a = canvas("wainscot_green", (0.155, 0.30, 0.185))
    rng = numpy.random.default_rng(23)
    a = grain(a, rng, 0.018)
    a = streaks(a, rng, 70, (0.10, 0.20, 0.12), 0.35)
    a = streaks(a, rng, 26, (0.42, 0.40, 0.33), 0.22)     # 下部磨白
    half = n // 2
    a[0:half, :, 0:3] = a[0:half, :, 0:3] * 0.94 + numpy.array((0.05, 0.04, 0.03))
    return "wainscot_green", a


def wall():
    n, a = canvas("wall_beige", (0.70, 0.67, 0.58))
    rng = numpy.random.default_rng(37)
    a = grain(a, rng, 0.030)
    a = streaks(a, rng, 18, (0.52, 0.49, 0.42), 0.30)     # 水渍竖痕
    y, x = numpy.mgrid[0:n, 0:n]
    d = ((x - n / 2) ** 2 + (y - n / 2) ** 2) / (n * n / 2.0)
    a[:, :, 0:3] *= (1.0 - 0.10 * d)[:, :, None]           # 泛黄四角
    return "wall_beige", a


def ceiling():
    n, a = canvas("ceiling_white", (0.80, 0.79, 0.75))
    rng = numpy.random.default_rng(41)
    a = grain(a, rng, 0.022)
    a = streaks(a, rng, 8, (0.66, 0.64, 0.58), 0.18)
    return "ceiling_white", a


def rust():
    n, a = canvas("metal_rust", (0.34, 0.34, 0.36))
    rng = numpy.random.default_rng(53)
    a = grain(a, rng, 0.030)
    a = speckle(a, rng, 320, 3, (0.42, 0.20, 0.10))
    a = speckle(a, rng, 140, 5, (0.30, 0.15, 0.08))
    a = streaks(a, rng, 24, (0.45, 0.24, 0.12), 0.40)
    return "metal_rust", a


def poster():
    """开锁小广告：白底 + 黑标题块 + 一行行号码点。不做真字（AI 生图/程序化都不该指望真字）。"""
    n, a = canvas("poster_ad", (0.90, 0.88, 0.80))
    rng = numpy.random.default_rng(67)
    a = grain(a, rng, 0.012)
    a[int(n * 0.78):int(n * 0.92), int(n * 0.14):int(n * 0.86), 0:3] = (0.08, 0.07, 0.07)
    for i in range(5):
        yy = int(n * (0.52 - i * 0.09))
        a[yy:yy + int(n * 0.05), int(n * 0.18):int(n * 0.82), 0:3] = (0.16, 0.12, 0.10)
    a[int(n * 0.05):int(n * 0.10), :, 0:3] = (0.55, 0.52, 0.44)   # 撕边
    return "poster_ad", a


def main(out_dir=None):
    import bpy
    out = out_dir or OUT_DEFAULT
    os.makedirs(out, exist_ok=True)
    made = []
    for fn in (terrazzo, wainscot, wall, ceiling, rust, poster):
        name, arr = fn()
        n = arr.shape[0]
        img = bpy.data.images.new(name, n, n, alpha=False)
        img.pixels[:] = arr.reshape(n * n * 4)      # 实测可用：float32 一维，长度 w*h*4
        path = os.path.join(out, name + ".png")
        img.filepath_raw = path
        img.file_format = 'PNG'
        img.save()
        ok = os.path.exists(path) and os.path.getsize(path) > 1000
        made.append("%s=%s(%d)" % (name, ok, os.path.getsize(path) if os.path.exists(path) else 0))
    print("[TEX] " + " ".join(made))
    return all("=True" in m for m in made)


if __name__ == "__main__":
    import sys
    sys.exit(0 if main() else 1)
```

- [ ] **Step 2: headless 跑一次，只看贴图**

Run: `cd /g/AIgame/jam && "/d/Game Library/Steam/steamapps/common/Blender/blender.exe" --background --python blender/gen_textures.py 2>&1 | grep -E "\[TEX\]|Error|Traceback"; ls -la dreamcore/Assets/Content/Textures/`
Expected: 一行 `[TEX] terrazzo_floor=True(…)` 六个都 `True`，`ls` 出 6 个 PNG（每个 ≥ 若干 KB，因为 512² 有噪声不会太小）。任一 `False` 或 `Traceback` → 停下修，不要往下涂材质。

Run: `cd /g/AIgame/jam && python -c "import struct,sys;[print(p,struct.unpack('>II',open(p,'rb').read()[16:24])) for p in ['terrazzo_floor','wainscot_green','poster_ad'] ]" 2>/dev/null || echo SKIP-PY`
Expected: `('terrazzo_floor', (512, 512))` 之类的真实宽高（PNG 头 16–24 字节就是 IHDR 的宽高）。这一步是**用 Blender 之外的独立读法交叉验证尺寸**，不是走个过场。

- [ ] **Step 3: 接进总建模入口与胶水**

`blender/build_scene.py` 里 `import gen_anomalies` 之后加一行，并在 `gen_corridor.build(...)` 之前调用：

```python
import gen_textures
```

```python
    if not gen_textures.main():
        raise SystemExit("[BUILD] 贴图生成失败")
```

`tools/blender_build.sh` 的产物断言段（`[ -f "$FBX" ]` 那行之后）加：

```sh
for t in terrazzo_floor wainscot_green wall_beige ceiling_white metal_rust poster_ad; do
  [ -f "$JAM_ROOT/dreamcore/Assets/Content/Textures/$t.png" ] \
    || { echo "!! 缺贴图：$t.png"; exit 1; }
done
echo "== 贴图 6/6 在位 =="
```

Run: `cd /g/AIgame/jam && ./tools/blender_build.sh`
Expected: `== 贴图 6/6 在位 ==` + `== FBX OK: … ==`。

- [ ] **Step 4: Unity 侧按名涂贴图**

在 `Bootstrap3D` 加常量与工厂（`CorridorFbxPath` 之后）：

```csharp
        const string TexDir = "Assets/Content/Textures";
```

```csharp
        static Material MakeTexMat(string name, string file, Vector2 tiling, Color tint)
        {
            var m = MakeMat(name, tint);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "/" + file + ".png");
            if (tex == null) { Debug.LogError("[Bootstrap3D] 缺贴图 " + file + ".png"); return m; }
            tex.wrapMode = TextureWrapMode.Repeat;
            m.SetTexture("_BaseMap", tex);
            m.SetTextureScale("_BaseMap", tiling);
            m.color = Color.white;   // 染色交给贴图本身，避免双重压暗
            return m;
        }
```

`CreateScene()` 里把四个 `MakeMat(...)` 纯色换成贴图版（`matBlack` 保持 `MakeUnlit` 不动）：

```csharp
            Material matFloor = MakeTexMat("Floor", "terrazzo_floor", new Vector2(6f, 1f), Color.white);
            Material matWall = MakeTexMat("Wall", "wall_beige", new Vector2(8f, 1f), Color.white);
            Material matWainscot = MakeTexMat("Wainscot", "wainscot_green", new Vector2(8f, 1f), Color.white);
            Material matCeil = MakeTexMat("Ceiling", "ceiling_white", new Vector2(8f, 1f), Color.white);
```

`PaintShell` 里给金属与小广告单列一支（其余仍走 wall）：

```csharp
                if (n == "Floor") mr.sharedMaterial = floor;
                else if (n == "Ceiling") mr.sharedMaterial = ceiling;
                else if (n.StartsWith("Wainscot")) mr.sharedMaterial = wainscot;
                else if (n.StartsWith("Mailbox") || n.StartsWith("Door") || n.StartsWith("Bike")) mr.sharedMaterial = metal;
                else if (n.StartsWith("Poster")) mr.sharedMaterial = poster;
                else mr.sharedMaterial = wall;
```

`PaintShell` 的签名与调用同步改成 6 个材质（原 4 个的形参表整行替换，调用处同理）：

```csharp
        static void PaintShell(Transform baseNode, Material floor, Material wall, Material wainscot,
                               Material ceiling, Material metal, Material poster)
```

```csharp
                PaintShell(baseNode, matFloor, matWall, matWainscot, matCeil, matMetal, matPoster);
```

```csharp
            Material matMetal = MakeTexMat("Metal", "metal_rust", new Vector2(2f, 2f), Color.white);
            Material matPoster = MakeTexMat("Poster", "poster_ad", new Vector2(1f, 1f), Color.white);
```

**为什么 tiling 是横向大、纵向 1**：Blender 的 cube 每个面 UV 都是 0–1，24m 长的地板一个面就吃掉整张贴图，所以横向拉 6–8 格（≈3–4m 一图）；纵向 1 格对 2.4m 宽/3m 高正好。**副作用**：侧墙与端墙共用一张材质，端墙（0.1×2.4m）会被压扁——jam 里可接受，端墙在走廊尽头且 fog 已经很浓。别为此再加第二套材质，那是 Task 11 之后的事。

- [ ] **Step 5: HUD 换 ScreenSpaceCamera（URP 后处理吃不到 Overlay）**

`Bootstrap3D.cs:166` 那行 `canvas.renderMode = RenderMode.ScreenSpaceOverlay;` 换成：

```csharp
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.planeDistance = 1f;
```

`ScreenSpaceOverlay` 的 Canvas 不进相机的渲染路径，URP 的 Bloom/色调映射对它无效——HUD 会显得"贴在外面"，与梦核滤镜脱节。换 `ScreenSpaceCamera` 后 HUD 会被同一套后处理拖进梦里（这是 spec §5 要的）。
透视缩放后果：`planeDistance=1` + 默认 60° FOV 会把 UI 缩到约 0.87 倍。**先跑 Step 6 再看**，若觉得字小，只把 `fontSize` 40→46 或把 `planeDistance` 降到 `0.6f`，两个数别同时动。

- [ ] **Step 6: 写观感断言并跑绿**

追加进 `Verify3D.cs`（本段用到 `Volume` 与 `GetUniversalAdditionalCameraData()`，`Volume` 在 `UnityEngine.Rendering` 下、扩展方法在 `UnityEngine.Rendering.Universal` 下，`Verify3D.cs` 两个都还没 using——**先补这两行**，否则 CS0246/CS1061）：

```csharp
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
```

```csharp
        /// <summary>观感段：贴图真的挂上了、HUD 走的是相机空间、后处理开关还开着。</summary>
        public static void VerifyLook()
        {
            fails.Length = 0;
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/Main3D.unity",
                UnityEditor.SceneManagement.OpenSceneMode.Single);

            foreach (var file in new[] { "terrazzo_floor", "wainscot_green", "wall_beige",
                                         "ceiling_white", "metal_rust", "poster_ad" })
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Content/Textures/" + file + ".png");
                Assert(tex != null, "贴图已导入：" + file);
                if (tex != null) Assert(tex.width >= 256, "贴图分辨率 ≥256：" + file + "=" + tex.width);
            }

            // 只统计 base 的网格：anom 下的 *_off 与 extraFigure 本就不该有贴图（前者涂的是
            // 从 base 换下来的那份，后者是 Unlit 黑片），全局扫会把它们算进分母、必然假红。
            var corridorGo = GameObject.Find("Corridor");
            Assert(corridorGo != null, "场景里有 Corridor 根节点");
            var baseNode = corridorGo != null ? FindByName(corridorGo.transform, "base") : null;
            Assert(baseNode != null, "场景里有 base 节点");
            int painted = 0, total = 0;
            if (baseNode != null)
            {
                foreach (var mr in baseNode.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (mr == null) continue;
                    total++;
                    if (mr.sharedMaterial == null)
                    {
                        Debug.Log("[VERIFY] base 网格没有材质：" + mr.name);
                        continue;
                    }
                    if (mr.sharedMaterial.HasProperty("_BaseMap") && mr.sharedMaterial.GetTexture("_BaseMap") != null) painted++;
                    else Debug.Log("[VERIFY] base 网格未涂贴图：" + mr.name + " 用 " + mr.sharedMaterial.name);
                }
                Assert(painted >= total - 1, "base 网格都挂上了带 _BaseMap 的材质（只许日历圈红件 Calendar2000_mark 免贴图）：" + painted + "/" + total);
            }

            var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
            Assert(canvas != null, "HUD Canvas 在位");
            if (canvas != null)
            {
                Assert(canvas.renderMode == RenderMode.ScreenSpaceCamera,
                    "HUD 必须是 ScreenSpaceCamera，实际 " + canvas.renderMode);
                Assert(canvas.planeDistance > 0.05f, "planeDistance 要大于 nearClip，实际 " + canvas.planeDistance);
            }

            var cam = Camera.main;
            Assert(cam != null, "Main Camera 在位");
            if (cam != null)
            {
                var cd = cam.GetUniversalAdditionalCameraData();
                Assert(cd != null && cd.renderPostProcessing, "相机开了 renderPostProcessing，否则滤镜白搭");
            }

            var vol = UnityEngine.Object.FindFirstObjectByType<Volume>();
            Assert(vol != null && vol.sharedProfile != null, "全局 Volume 挂着梦核 Profile");

            if (Environment.GetEnvironmentVariable("VERIFY_BAD") == "1")
                Assert(painted == 0, "坏样本：这条断言必须失败");

            Report();
        }
```

`tools/unity_verify.sh` 的 `run_one VerifyGameplay play` 之前插入一行：

```sh
run_one VerifyLook look
```

Run: `cd /g/AIgame/jam && "/e/Unity/Unity/Editor/Editor/Unity.exe" -batchmode -quit -projectPath G:/AIgame/jam/dreamcore -executeMethod Dreamcore.EditorTools.Bootstrap3D.Run -logFile logs/bs8.log && grep -cE "error CS" logs/bs8.log; ./tools/unity_verify.sh`
Expected: `0` 然后四行 `符合预期 PASS`。（`painted/total` 只数 `base` 下的网格，分母应为 base 网格总数；日志里 `[VERIFY] base 网格未涂贴图：` 只允许出现 `Calendar2000_mark` 一条（Task 10 才有，Task 8 阶段应为 0 条）。若出现别的名字，就是 `PaintShell` 的前缀分支没接住那块网格。）

Run: `cd /g/AIgame/jam && VERIFY_BAD=1 ./tools/unity_verify.sh; echo "rc=$?"`
Expected: `rc=1`，且失败段是 `look`。

- [ ] **Step 7: 光照与雾的最终数值（一次性拍死，别再循环调）**

`CreateScene()` 里保持/改成这组（与 Task 7 一致，只有 `fogEndDistance` 从 26 收到 20）：

```csharp
            RenderSettings.ambientLight = new Color(0.085f, 0.078f, 0.070f);
            RenderSettings.fogStartDistance = 1.5f;
            RenderSettings.fogEndDistance = 20f;
```

`AdoptLights` 的灯参数保持 `intensity 1.8f / range 7f / 暖黄 (1, .85, .60)`；`LightFlicker` 用默认 `flickerAmp 0.25 / flickerSpeed 1.7`（`LightFlicker.cs` 的默认值就是这组，不用改文件）。
**为什么只动这几个**：6 盏 range 7m 的点光在 24m 走廊上必然重叠，`fogEnd=26` 会把整条走廊糊成一团灰、把贴图白做；收到 20m 后远处自然沉进雾里，窗外该黑就黑。改完只跑一次 Step 6 的绿，不接受"再调一点看看"的循环——那是 M5 之后的事。

- [ ] **Step 8: 真人看一轮 + Commit**

Run: `cd /g/AIgame/jam && ./tools/build.sh 3d 2>&1 | tail -3`
Expected: `== 完成，提交物：/g/AIgame/jam/builds/win64-3d/Dreamcore-win64.zip ==`。

Run: `cd /g/AIgame/jam/builds/win64-3d && ./Dreamcore.exe`
判据（只看观感，玩法沿用 Task 7 Step 6）：水磨石地面能看出碎石颗粒；绿漆墙裙是一条连续的绿带而不是色块；HUD 文字与场景同呼吸（被后处理带着走，不是硬贴）；走廊尽头收进雾里而不是无限清晰。
四条里有"看着不像"的，只调 Step 4 的 tiling 数字（唯一允许改的观感参数），重跑 Step 6 后复看。

```bash
git add blender/gen_textures.py blender/build_scene.py tools/blender_build.sh tools/unity_verify.sh \
        dreamcore/Assets/Content/Textures/ dreamcore/Assets/Content/Textures/*.meta \
        dreamcore/Assets/_Project/Editor/Bootstrap3D.cs dreamcore/Assets/_Project/Editor/Verify3D.cs \
        docs/superpowers/plans/2026-10-17-zuoride-mingtian-3d-pipeline.md
git commit -m "feat: 程序化贴图与相机空间 HUD（M3 观感）"
```

---

### Task 9: 音频接线（只接脚步与环境音，异常 cue 故意不接）

`AudioController`（`FX/AudioController.cs`，namespace `Dreamcore`）已经写好在等接线：`Awake` 自建两个 `AudioSource`，`ambientLoop` 非空就循环播，对外只暴露 `PlayFootstep()` / `PlayAnomalyCue()` / `PlaySfx()`。素材四件都在 `Assets/_Project/Audio/`：`ambient_loop.wav`、`footstep.wav`、`anomaly_cue.wav`、`tape_click.wav`。

**为什么不接 `anomaly_cue`**：本作的判定就是"这一层有没有异常"。若异常出现时播一声 cue，等于把答案直接给玩家，玩法当场塌掉。环境音 + 脚步只交代"你在一个空间里"，不交代对错。
这条决定要写成断言（`anomalyCue == null`），否则下一个会话会"顺手把音效接上"。

**Files:**
- Modify: `dreamcore/Assets/_Project/Scripts/3D/FirstPersonController.cs`（加 `audio` 引用与按位移计步）
- Modify: `dreamcore/Assets/_Project/Editor/Bootstrap3D.cs`（造 Audio 节点、挂 `AudioController`、绑 `fpc.audio`）
- Modify: `dreamcore/Assets/_Project/Editor/Verify3D.cs`（追加 `VerifySound()`）

**Interfaces:**
- Consumes: `Dreamcore.AudioController` 的 `ambientLoop`/`footstep`/`anomalyCue`/`ambientVolume`/`sfxVolume` 字段与 `PlayFootstep()`（既有实现，逐字照用）；Task 7 的 `fpc` 局部变量与 `tools/unity_verify.sh`
- Produces: `FirstPersonController.audio`（`AudioController` 字段，Bootstrap 绑定）、`FirstPersonController.stepDistance`（默认 `0.75f`）、`Dreamcore.EditorTools.Verify3D.VerifySound()`；`tools/unity_verify.sh` 增加第五段

- [ ] **Step 1: 给控制器加计步**

`FirstPersonController` 的 `[Header("视角")]` 组之后加：

```csharp
        [Header("声音")]
        public AudioController audio;      // Bootstrap3D 绑定；为空则静默
        public float stepDistance = 0.75f; // 米/步，配合 walkSpeed 1.6 ≈ 2.1 步/秒
```

字段区加 `float stepAcc;`，`Awake()` 末尾加：

```csharp
            if (audio == null) audio = FindFirstObjectByType<AudioController>();
```

`Update()` 里 `cc.Move(move * Time.deltaTime);` 之后加：

```csharp
            float planar = new Vector2(move.x, move.z).magnitude * Time.deltaTime;
            if (cc.isGrounded && planar > 0.0001f)
            {
                stepAcc += planar;
                if (stepAcc >= stepDistance)
                {
                    stepAcc = 0f;
                    if (audio != null) audio.PlayFootstep();
                }
            }
```

按**位移**计步而不是按时间，理由：站着不动不该有脚步；贴着墙滑步也不该刷脚步。`AudioController` 在 `namespace Dreamcore` 下，`Dreamcore.D3` 是它的嵌套命名空间，外层类型直接可见，不需要 `using`。

- [ ] **Step 2: Bootstrap 造 Audio 节点并绑引用**

`CreateScene()` 里 `fpc.cameraTransform = camGo.transform;` 之后加：

```csharp
            var audioGo = new GameObject("Audio");
            var ac = audioGo.AddComponent<AudioController>();
            ac.ambientLoop = LoadClip("ambient_loop");
            ac.footstep = LoadClip("footstep");
            ac.anomalyCue = null;   // 剧透防线，勿"顺手接上"；VerifySound 会断言它
            fpc.audio = ac;
```

helpers 区加：

```csharp
        static AudioClip LoadClip(string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/" + name + ".wav");
            if (clip == null) Debug.LogError("[Bootstrap3D] 缺音频 " + name + ".wav");
            return clip;
        }
```

`tape_click.wav` 与 `anomaly_cue.wav` 保持不接线——不是遗漏，是本任务开头那条决定。

- [ ] **Step 3: 写声音段断言**

追加进 `Verify3D.cs`：

```csharp
        /// <summary>声音段：环境音与脚步真的挂上了，剧透用的 anomalyCue 必须仍然为空。</summary>
        public static void VerifySound()
        {
            fails.Length = 0;
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/Main3D.unity",
                UnityEditor.SceneManagement.OpenSceneMode.Single);

            var ac = UnityEngine.Object.FindFirstObjectByType<AudioController>();
            Assert(ac != null, "场景里有 AudioController");
            if (ac == null) { Report(); return; }

            Assert(ac.ambientLoop != null, "环境音已挂上（梦核的空场感全靠它）");
            Assert(ac.footstep != null, "脚步已挂上");
            Assert(ac.anomalyCue == null,
                "anomalyCue 必须为空：异常出现时给声音提示等于把判定答案交给玩家");
            Assert(ac.ambientVolume > 0f && ac.ambientVolume < 1f, "环境音量在区间内，实际 " + ac.ambientVolume);

            var fpc = UnityEngine.Object.FindFirstObjectByType<FirstPersonController>();
            Assert(fpc != null, "FirstPersonController 在位");
            if (fpc != null)
            {
                Assert(fpc.audio == ac, "控制器的 audio 指向场景里那个 AudioController");
                Assert(fpc.stepDistance > 0.3f && fpc.stepDistance < 1.5f, "步距合理，实际 " + fpc.stepDistance);
            }

            foreach (var name in new[] { "ambient_loop", "footstep", "anomaly_cue", "tape_click" })
                Assert(AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/" + name + ".wav") != null,
                    "音频资产可加载：" + name);

            if (Environment.GetEnvironmentVariable("VERIFY_BAD") == "1")
                Assert(ac.anomalyCue != null, "坏样本：这条断言必须失败");

            Report();
        }
```

注意坏样本这条写的是"期望 `anomalyCue != null`"——它失败的原因与正例断言同源，这样 `VERIFY_BAD=1` 时必然 FAIL，而正例 PASS 也确实是因为 cue 没接。

- [ ] **Step 4: 接进胶水并跑绿/跑坏**

`tools/unity_verify.sh` 的 `run_one VerifyLook look` 之后加：

```sh
run_one VerifySound sound
```

Run: `cd /g/AIgame/jam && "/e/Unity/Unity/Editor/Editor/Unity.exe" -batchmode -quit -projectPath G:/AIgame/jam/dreamcore -executeMethod Dreamcore.EditorTools.Bootstrap3D.Run -logFile logs/bs9.log && grep -cE "error CS" logs/bs9.log; ./tools/unity_verify.sh`
Expected: `0` + 五行 `符合预期 PASS`（顺序 fbx / asm / look / sound / play）。

Run: `cd /g/AIgame/jam && VERIFY_BAD=1 ./tools/unity_verify.sh; echo "rc=$?"`
Expected: `rc=1`。第一段 `fbx` 就 FAIL，所以还要单独确认 sound 段自己会坏：

Run: `cd /g/AIgame/jam && VERIFY_BAD=1 "/e/Unity/Unity/Editor/Editor/Unity.exe" -batchmode -quit -projectPath G:/AIgame/jam/dreamcore -executeMethod Dreamcore.EditorTools.Verify3D.VerifySound -logFile logs/verify-sound-bad.log; grep -E "\[VERIFY\] FAIL|坏样本" logs/verify-sound-bad.log | head -4`
Expected: 出现 `[VERIFY] FAIL` 与"坏样本"两行。

- [ ] **Step 5: 实机听一轮 + Commit**

Run: `cd /g/AIgame/jam && ./tools/build.sh 3d 2>&1 | tail -3 && cd builds/win64-3d && ./Dreamcore.exe`
判据：开局就有环境音（底噪/楼道声）不停；走动时脚步与位移同步、停下即停；**异常出现时没有任何提示音**。
若脚步太密/太疏，只动 `stepDistance`（0.55–1.0 之间试），别动 `walkSpeed`。

```bash
git add dreamcore/Assets/_Project/Scripts/3D/FirstPersonController.cs \
        dreamcore/Assets/_Project/Editor/Bootstrap3D.cs \
        dreamcore/Assets/_Project/Editor/Verify3D.cs tools/unity_verify.sh
git commit -m "feat: 环境音与脚步接入，anomalyCue 保持不接（防剧透）"
```

---

### Task 10: 2000 年挂历彩蛋与结局文案定稿（M4 内容）

spec §8.5 已定案采用"2000 年挂历 / 2 月 30 日"彩蛋；§8.6 的结局文案是**全计划唯一还开放的决定**——本任务先出草稿、拿到用户定稿才落盘，未定稿前 `Hud3D` 现有文案不动。

彩蛋用**几何**表达而不是贴图：Blender 内置 Bfont 只保证 ASCII，画不出"月"字；而"2000 / 2 / 30"这三个数字 + 一个 7×6 的日历格 + 圈红的一格，已经足够让玩家认出"这日历上有不存在的日期"。用 3D 文字转网格还省掉一张贴图和一次 UV 对位。

**Files:**
- Modify: `blender/gen_corridor.py`（新增 `calendar()` 并在 `build()` 末尾调用）
- Modify: `dreamcore/Assets/_Project/Scripts/3D/Hud3D.cs`（结局文案 + 深度文案措辞）
- Modify: `dreamcore/Assets/_Project/Editor/Verify3D.cs`（`VerifyFbx` 加彩蛋断言）
- Modify: `docs/superpowers/specs/2026-10-17-zuoride-mingtian-3d-pipeline-design.md`（§8.6 记定稿）

**Interfaces:**
- Consumes: Task 6 的 `box()`/`move_to()`/`coll()` helpers、Task 6 Step 6 记进 README 的轴映射表
- Produces: `base/Calendar2000`（父空物体）及其子网格 `Calendar2000_frame`、`Calendar2000_year`、`Calendar2000_month`、`Calendar2000_day`、`Calendar2000_mark`；`Hud3D` 的新结语文案（逐字来自用户定稿）

- [ ] **Step 1: 先向用户拿结局文案定稿（不许跳过，也不许代拍）**

把下面两版草稿**原样**呈现给用户，并说明差异：A 版克制（不解释异常来源）、B 版留一句"为什么是 2000 年"的回响。拿到选定版本或用户自己改写的版本后，才继续 Step 3。

```text
A 版
  好结局：你出来了。\n楼道的声控灯一盏盏亮起来，全是真的。
  坏结局：你还在这儿。\n挂历上圈着的那天，从来没有过。

B 版
  好结局：你走完了这一层。\n天快亮了，2000 年还没来过。
  坏结局：你又回到了起点。\n它把日历停在二月三十号，等你继续数。
```

拿到定稿后在 spec §8.6 那行追加 `—— 定稿：<原文>（2026-10-17）`，**并把用户原话逐字抄进去，不做润色**。

- [ ] **Step 2: 挂历几何进 base**

`gen_corridor.py` 里加：

```python
def calendar(base):
    """2000 年挂历，日期圈在 2 月 30 日（不存在的日子）。纯几何+ASCII 数字，绕开 Bfont 无汉字。
    局部坐标沿用 box() 的约定：(along=沿走廊 X, side=横向 Y, height=竖直 Z)。
    挂历贴墙 → 薄轴是 side(Y)，整体不用旋转；只有文本要绕 X 转 90° 才站起来。"""
    holder = bpy.data.objects.new("Calendar2000", None)
    base.objects.link(holder)
    parent = holder

    def link(ob, name):
        ob.name = name
        move_to(ob, base)
        ob.parent = parent
        return ob

    # 纸面（薄板）：沿走廊 0.30 宽、竖直 0.40 高、厚 0.012
    link(box("cal_paper", base, 0.0, 0.0, 0.0, (0.30, 0.012, 0.40)), "Calendar2000_frame")
    # 7 列（沿走廊方向）× 6 行（竖直方向）格线，薄轴同为 Y
    for i in range(7):
        link(box("cal_col%d" % i, base, -0.13 + i * 0.043, 0.006, 0.06,
                 (0.0015, 0.004, 0.20)), "Calendar2000_grid_col%d" % i)
    for j in range(6):
        link(box("cal_row%d" % j, base, 0.0, 0.006, 0.16 - j * 0.040,
                 (0.26, 0.004, 0.0015)), "Calendar2000_grid_row%d" % j)

    def text_body(body, name, along, height, size):
        cu = bpy.data.curves.new(name, 'FONT')
        cu.body = body
        cu.size = size
        cu.extrude = 0.002
        ob = bpy.data.objects.new(name, cu)
        # 文本躺在 XY 平面（法线 +Z）→ 绕 X 转 90° 站起来，法线转向 side 轴
        ob.rotation_euler = (1.5708, 0.0, 0.0)
        ob.location = (along, 0.010, height)
        base.objects.link(ob)
        ob.parent = parent
        return ob

    text_body("2000", "Calendar2000_year", 0.0, 0.185, 0.055)
    text_body("2", "Calendar2000_month", -0.095, 0.115, 0.040)
    text_body("30", "Calendar2000_day", 0.055, -0.055, 0.040)
    link(box("cal_mark", base, 0.055, 0.014, -0.055, (0.055, 0.006, 0.075)), "Calendar2000_mark")

    # 摆放：沿走廊 4.6m、视线高 1.65m、贴左墙内侧（side = -半宽 + 余量）
    holder.location = (4.6, -WIDTH * 0.5 + 0.075, 1.65)
    return holder
```

`build()` 末尾（`for z in LIGHT_ZS` 之前）加一行 `calendar(base)`。
**朝向只有一个可拧的旋钮**：文本的 `rotation_euler`。站错的表现是"数字躺在地上"，Step 4 的薄轴断言会当场断出来。
`Task 6 Step 6` 记进 README 的轴映射可能带符号翻转：**若 Step 4 报薄轴不对、或挂历跑到右墙，只把 `holder.location` 的 side 分量取反**，`rotation_euler` 与 Unity 侧的 `FbxYaw` 都不许动（`FbxYaw` 是唯一旋转落点，两处都转＝转两次）。

`Calendar2000_mark`（那个圈红的小方框）在 Unity 侧要单独涂红。`CreateScene()` 的材质区补一行：

```csharp
            Material matRed = MakeMat("CalRed", new Color(0.62f, 0.11f, 0.10f));
```

`PaintShell` 的分支里加一条（放在 `Mailbox/Door/Bike` 那条之前，条件更精确的先判）：

```csharp
                else if (n == "Calendar2000_mark") mr.sharedMaterial = red;
```

`PaintShell` 形参只再加一个 `Material red`，调用处传 `matRed`：

```csharp
        static void PaintShell(Transform baseNode, Material floor, Material wall, Material wainscot,
                               Material ceiling, Material metal, Material poster, Material red)
```

```csharp
                PaintShell(baseNode, matFloor, matWall, matWainscot, matCeil, matMetal, matPoster, matRed);
```

其余日历件（纸面、格线、数字）不 special-case，直接落进最后的 `else mr.sharedMaterial = wall;`——米白泛黄的墙漆正好就是旧挂历纸的颜色，少一个参数少一处会错的地方。

- [ ] **Step 3: 落文案与措辞**

`Hud3D.cs` 里 `endText.text = win ? … : …;` 换成 Step 1 定稿的两段原文（`\n` 保留）。同时把深度文案 `"地下 " + d + " 层"` 改成与实际场景一致的说法——走廊是**同一层反复走**，不是往下：改成 `"第 " + d + " 遍"`。
顺手把 `Hud3D.cs` 顶部注释里"Overlay Canvas（不吃后处理…）"改成"ScreenSpaceCamera（与场景同受后处理）"，那是 Task 8 已经改掉的事实。

- [ ] **Step 4: 加彩蛋断言并重跑四段**

在 `Verify3D.VerifyFbx()` 的 `// 命名保真` 那段之后追加：

```csharp
            var cal = FindByName(inst.transform, "Calendar2000");
            Assert(cal != null, "base 里有 Calendar2000 挂历彩蛋");
            if (cal != null)
            {
                var cr = new Bounds(cal.position, Vector3.zero);
                int rend = 0;
                foreach (var mr in cal.GetComponentsInChildren<Renderer>()) { cr.Encapsulate(mr.bounds); rend++; }
                Assert(rend >= 10, "挂历至少 10 个可见件（纸面+13 格线+3 数字+红框），实际 " + rend);
                // 薄轴：Blender 的 side(Y) 经 FbxYaw=-90 后落在 Unity X。符号可能翻，所以用 Abs 判"贴墙"。
                Assert(cr.size.x < 0.14f, "薄轴是 Unity X（挂历贴墙站立，实测 size=" + cr.size + "）");
                Assert(cr.size.y > 0.30f && cr.size.y < 0.60f, "挂历竖向约 0.4m，实测 " + cr.size.y);
                Assert(cr.size.z > 0.20f && cr.size.z < 0.60f, "挂历沿走廊约 0.3m，实测 " + cr.size.z);
                Assert(Mathf.Abs(cr.center.x) > 0.85f && Mathf.Abs(cr.center.x) < 1.35f,
                    "挂历贴在墙上（|x|≈1.2，实测 " + cr.center.x + "）");
                Assert(cr.center.y > 1.3f && cr.center.y < 2.0f, "视线高度范围内（实测 " + cr.center.y + "）");
                Assert(FindByName(cal, "Calendar2000_mark") != null, "日期圈红件在位");
            }
```

五条 `cr.*` 断言各管一件事，互不重复：件数（少画了没发现）、薄轴（站错就是"数字躺在地上"）、竖向与横向尺寸（别做成一面墙那么大）、贴墙距离（跑到走廊中间会挡路）、视线高度（贴天花板就读不到）。
**FAIL 时的处置只有一个旋钮**：`holder.location` 的 side 分量取反（符号翻转）或整段重跑 Step 2 的 `rotation_euler`——`FbxYaw` 不动。

Run: `cd /g/AIgame/jam && ./tools/blender_build.sh && "/e/Unity/Unity/Editor/Editor/Unity.exe" -batchmode -quit -projectPath G:/AIgame/jam/dreamcore -executeMethod Dreamcore.EditorTools.Bootstrap3D.Run -logFile logs/bs10.log && grep -cE "error CS" logs/bs10.log; ./tools/unity_verify.sh`
Expected: `== FBX OK … ==`、`0`、然后五段 `符合预期 PASS`。
Run: `cd /g/AIgame/jam && VERIFY_BAD=1 ./tools/unity_verify.sh; echo "rc=$?"` → Expected: `rc=1`。

- [ ] **Step 5: 实机看一眼挂历 + Commit**

Run: `cd /g/AIgame/jam && ./tools/build.sh 3d 2>&1 | tail -3 && cd builds/win64-3d && ./Dreamcore.exe`
判据：进门往左看，4.6m 处墙上有一张纸，能读出 2000、2、30 与一个红框；结语文案与 Step 1 定稿逐字一致（截图 `Win+Shift+S` 存着，Task 11 要当封面候选）。

```bash
git add blender/gen_corridor.py dreamcore/Assets/_Project/Scripts/3D/Hud3D.cs \
        dreamcore/Assets/_Project/Editor/Bootstrap3D.cs dreamcore/Assets/_Project/Editor/Verify3D.cs \
        dreamcore/Assets/Content/3D/Corridor.fbx docs/superpowers/specs/2026-10-17-zuoride-mingtian-3d-pipeline-design.md
git commit -m "feat: 2000 年挂历彩蛋与结局文案定稿"
```

---

### Task 11: 异常点名工装、终局验收与提交物（M4 出口 → M5）

spec §7 的 M3 出口条件是"**10 个异常实机全部可辨识**"。靠随机抽骰验它是不诚实的（一局最多见 7 个，还得运气），所以本任务先做一个只在环境变量下生效的点名工装，再一次跑完 10 项。
工装不进正常玩法路径：没设变量时行为与玩家所见完全一致，因此不需要在发布前摘掉（jam 时间不多了，摘它本身才是风险）。

**Files:**
- Modify: `dreamcore/Assets/_Project/Scripts/3D/Loop3D.cs`（`Roll()` 加环境变量点名分支 + `forced` 标志）
- Modify: `dreamcore/Assets/_Project/Editor/Verify3D.cs`（`VerifyGameplay` 加工装自证断言）
- Modify: `tools/SUBMIT_CHECKLIST.md`（3D 化，四处定点替换）
- Modify: `docs/superpowers/plans/2026-10-17-zuoride-mingtian-3d-pipeline.md`（末尾追加"执行期勘误与实测登记"一节）

**Interfaces:**
- Consumes: 前五段验收（`tools/unity_verify.sh`）、`AnomalyTitles.Table` 的 10 个 id、Task 5 的 `builds/win64-3d/` 路径
- Produces: `Loop3D` 的 `DREAMCORE_ANOM` 环境变量口径（值 = `anom01`…`anom10`，空 = 正常随机）；3D 化的 `SUBMIT_CHECKLIST.md`；终版提交物 `builds/win64-3d/Dreamcore-win64.zip`

- [ ] **Step 1: 加点名工装（只在环境变量下生效）**

`Loop3D` 字段区加：

```csharp
        string forceId = System.Environment.GetEnvironmentVariable("DREAMCORE_ANOM");
        public bool forced;      // 工装生效过没有：VerifyGameplay 要读，所以是 public
        public int forceHits;    // 生效次数
```

`Roll()` 里在 `else if (rng.NextDouble() < cleanChance || unused.Count == 0)` **之前**插入一支优先级最高的分支：

```csharp
            else if (!string.IsNullOrEmpty(forceId))
            {
                // 验收工装：点名哪条就每层都出哪条，让玩家能逐条核对 10 个异常
                Current = null;
                foreach (var cand in anomalies)
                    if (cand != null && cand.id == forceId) { Current = cand; break; }
                if (Current == null)
                    Debug.LogError("[Loop3D] DREAMCORE_ANOM=" + forceId + " 在目录里找不到");
                else { forced = true; forceHits++; Current.Revert(); Current.Apply(); }
            }
```

用 `id` 精确匹配（不是前缀、不是序号），所以 `anom03` 只会命中 `anom03`。`Current.Revert()` 先归位一次，是为了"同一层重掷也不叠加"——连续两层都是同一异常时，`Apply()` 之前物件状态必须是正常的。
**这条分支必须在 `Depth == 1` 那一支之后**（depth 1 永远是记忆锚，工装也不例外），照上面插入位置原样放在 `else if` 链里即可。
`unused` 在工装分支里不动用——同一异常每层重复正是本工装的用途，不参与"本局不重复"的口径。

- [ ] **Step 2: 让工装自己会失败**

`Begin()` 里 `RevertCurrent();` 之后加一行：

```csharp
            forced = false;
            forceHits = 0;
```

`VerifyGameplay()` 末尾（坏样本之前）加：

```csharp
            // 工装自证：设了 DREAMCORE_ANOM 就必须真的走过点名分支
            string anomEnv = System.Environment.GetEnvironmentVariable("DREAMCORE_ANOM");
            if (string.IsNullOrEmpty(anomEnv))
                Debug.Log("[VERIFY] ok   未设 DREAMCORE_ANOM，跳过工装自证（正常口径已由上面各条覆盖）");
            else
            {
                Assert(loop.forced, "设了 DREAMCORE_ANOM 就必须走到点名分支（否则这条环境变量分支是死码）");
                Assert(loop.forceHits >= 2, "点名分支至少生效两次，实际 " + loop.forceHits);
            }
```

这条的作用是：**别让"设了变量但分支没生效"伪装成通过**。`Begin` 在第 1 层，`forced` 要到第 2 层以后才置位，所以断言前必须先 `loop.Report(...)` 推进过一层——上面这段已经排在 `anyClean` 那串循环之后，此时 `loop` 已被推进多次，直接断言即可。

Run: `cd /g/AIgame/jam && DREAMCORE_ANOM=anom04 "/e/Unity/Unity/Editor/Editor/Unity.exe" -batchmode -quit -projectPath G:/AIgame/jam/dreamcore -executeMethod Dreamcore.EditorTools.Verify3D.VerifyGameplay -logFile logs/verify-forced.log; grep -E "\[VERIFY\]" logs/verify-forced.log | tail -3`
Expected: `[VERIFY] PASS`（设了变量 → `forced` 置位 → 断言成立）。
对照：`cd /g/AIgame/jam && ./tools/unity_verify.sh`（不设变量）也必须 PASS，且 `logs/verify-play.log` 里 `depth 1 必须干净` 为 `ok`——工装不许污染正常口径。

- [ ] **Step 3: 十项可辨识 sweep（M3 出口条件在这里补验）**

Run: `cd /g/AIgame/jam && ./tools/build.sh 3d 2>&1 | tail -3`
工装下"点名哪条就每层都出哪条"，所以每一项都是独立一局，一局只看一层就够（约 20 秒）。逐条启动，每条记"认出/认不出"：

```sh
cd /g/AIgame/jam/builds/win64-3d
DREAMCORE_ANOM=anom01 ./Dreamcore.exe   # 看完一层，关掉，再下一条
DREAMCORE_ANOM=anom02 ./Dreamcore.exe
DREAMCORE_ANOM=anom03 ./Dreamcore.exe
DREAMCORE_ANOM=anom04 ./Dreamcore.exe
DREAMCORE_ANOM=anom05 ./Dreamcore.exe
DREAMCORE_ANOM=anom06 ./Dreamcore.exe
DREAMCORE_ANOM=anom07 ./Dreamcore.exe
DREAMCORE_ANOM=anom08 ./Dreamcore.exe
DREAMCORE_ANOM=anom09 ./Dreamcore.exe
DREAMCORE_ANOM=anom10 ./Dreamcore.exe
```

（Git Bash 里这样写前缀变量是给子进程的正确姿势；若改用 cmd，要 `set DREAMCORE_ANOM=anom01 && Dreamcore.exe`，别在 PowerShell 里照抄 `$env:` 之外的写法。）

| id | 期望现象（spec §4.3） | 认出 |
|---|---|---|
| anom01 | 6 盏暖灯全灭，只剩绿色应急光 | ☐ |
| anom02 | 尽头一个背对镜头的黑影，且始终正对你 | ☐ |
| anom03 | 3.4m 处那扇门半开 | ☐ |
| anom04 | 墙上小广告整排不见了 | ☐ |
| anom05 | 地上一双小布鞋 | ☐ |
| anom06 | 靠墙自行车倒了 | ☐ |
| anom07 | 信报箱小门全敞 | ☐ |
| anom08 | 门缝下一滩水 | ☐ |
| anom09 | 天花板垂下红电线 | ☐ |
| anom10 | 窗外该有楼的地方全黑 | ☐ |

**认不出来的项**按 spec §7 降级：优先修表现（尺寸、颜色对比、位置高度），实在来不及就砍到 6 项（保留 anom01/02/03/05/06/08），并同步把 `AnomalyTitles.Table` 与 `gen_anomalies.py` 的组删到 6——**两处一起删**，否则 `VerifyAssembly` 的 `count == 10` 会断。删完把这两处的数字改成 6 并在 Step 6 登记。

- [ ] **Step 4: 抓一次实机日志**

Run: `cd /g/AIgame/jam && ls -la "$USERPROFILE/AppData/LocalLow/DefaultCompany/Dreamcore/Player.log" 2>/dev/null || find "$USERPROFILE/AppData/LocalLow" -name "Player.log" -newermt '-30 minutes' 2>/dev/null | head -3`
Run: `grep -icE "exception|error" <上面找到的 Player.log>`
Expected: 计数为 0，或只有已登记为无害的 URP `DebugUpdater` "referenced script … '<null>'" 那类（`SUBMIT_CHECKLIST.md` 常见坑已记）。有真实异常 → 回对应任务修，修完重跑 Step 3。

- [ ] **Step 5: 提交清单 3D 化**

`tools/SUBMIT_CHECKLIST.md` 做四处定点替换（保留 2D 那套作为回退，另起一节写 3D，别整篇覆盖）：

1. `## 构建前` 那节末尾加一行：`- [ ] ./tools/build.sh 3d（3D 轨道，产物落 builds/win64-3d/，不会覆盖 2D 的 builds/win64/）`
2. `## 玩法抽查` 整节替换为：

```markdown
## 玩法抽查（3D 轨道，5 分钟）
- [ ] 走到尽头=前进、回到起点=回头，判定各正确
- [ ] 第 1 遍绝无异常（记忆锚）；同一局内异常不重复
- [ ] 错 3 次出坏结局；走满 8 遍出好结局
- [ ] 墙上不穿模、地面不塌陷（贴墙走一圈）
- [ ] 有环境音、脚步跟位移走、异常出现时**没有**提示音
- [ ] HUD 文字随场景一起被滤镜带走（不是硬贴在画面上）
- [ ] 挂历彩蛋能读出 2000 / 2 / 30 与红框
```

3. `itch.io / 主办方提交页` 那节：zip 路径改 `builds/win64-3d/Dreamcore-win64.zip`；标题写《昨日的明天》；操作说明改 `WASD 移动 + 鼠标转视角，走到走廊尽头=认为正常，折回起点=发现异常`；素材声明改 `全部几何/贴图程序化生成（Blender 5.2 bpy + numpy），音频程序合成，无第三方资产`。
4. `## 常见坑（实测）` 末尾追加两条，逐字照写：

```markdown
- **Blender MCP 的安全面**：`BLENDER_MCP_SAFE_MODE` 未设置，socket :9876 上的 MCP 能在 Blender 里跑任意 Python。只在本地开发用，别在联网机器常驻 GUI。
- **`Texture.to_image` 在 5.2.2 不存在**，贴图只能 `images.new` + numpy 直写 `pixels`（索引 0 是左下角）。`--python-exit-code` 必须加，否则 Python 抛异常时 Blender 仍退 0。
```

Run: `cd /g/AIgame/jam && grep -c "^- \[ \]" tools/SUBMIT_CHECKLIST.md`
Expected: 数字比改前大（用 `git diff --stat tools/SUBMIT_CHECKLIST.md` 一起看），且 `grep -n "win64-3d" tools/SUBMIT_CHECKLIST.md` 命中提交页那条。

- [ ] **Step 6: 终版构建、zip 与勘误登记（M5）**

Run: `cd /g/AIgame/jam && ./tools/build.sh 3d 2>&1 | tail -4 && ls -la builds/win64-3d/Dreamcore-win64.zip builds/win64/Dreamcore-win64.zip`
Expected: 两个 zip 都在，`win64-3d` 的 size 明显大于 2D 版（3D 场景 + 6 张贴图），2D 的 size 与 Task 5 Step 8 记下的一致（**证明分流后 2D 提交物没被重打包**）。

Run: `cd /g/AIgame/jam && grep -rn -E "token|secret|password|\.env" --include="*.cs" --include="*.sh" --include="*.py" --include="*.md" . | grep -v "\.git/" | head -10`
Expected: 只出现"登记安全面"这类叙述，没有任何真实凭据。（`.qoder-credits/` 已 ignore。）

在本计划末尾追加一节，登记三件事（**追加，不改写前面任何一段正文**）：

```markdown
---

## 执行期勘误与实测登记（2026-10-17，实施时追加）

- Task 3：异常表落点从 spec §3 的 ScriptableObject 资产改为 `[Serializable] AnomalyEntry` 列表（资产引用场景物体是伪引用）。
- Task 6：`Bootstrap3D.FbxYaw` 实测取值 `<填>`；轴映射结论 `<填 README 里那行>`。
- Task 11：异常项最终数量 `<10 或 6，以及删了哪几条>`；十项 sweep 认不出来的项 `<逐条>`。
- 计划里 `blender/README.md` 的"objects 44"若与实际不符，以 `[BUILD]` 日志为准并在此登记新数。
```

`<填>` 三处必须在 Step 6 之前已被真实数字替换，否则不许 commit。

```bash
git add tools/SUBMIT_CHECKLIST.md docs/superpowers/plans/2026-10-17-zuoride-mingtian-3d-pipeline.md \
        dreamcore/Assets/_Project/Scripts/3D/Loop3D.cs dreamcore/Assets/_Project/Editor/Verify3D.cs \
        dreamcore/Assets/_Project/Scenes/Main3D.unity dreamcore/Assets/_Project/Scenes/Main3D.unity.meta
git commit -m "chore: 3D 提交清单、异常点名工装与终版构建登记（M5）"
```

- [ ] **Step 7: 交出去**

把 `builds/win64-3d/Dreamcore-win64.zip` 按主办方要求上传；`SUBMIT_CHECKLIST.md` 逐条打勾并把"哪条没过"写在该条后面（不许留空）。若主办方要求现场演示，保留 `builds/win64-3d/Dreamcore.exe` 所在目录整体（`Dreamcore_Data` 必须同级）。

---

## 自检（写完后回看，已修）

- **spec 覆盖**：§2 组件边界→Task 3/4/7；§3 命名约定→Task 1/3/6；§4 玩法与 10 异常→Task 4/5/6/11；§5 渲染与音频预算→Task 8/9；§6 MCP→Task 2；§7 时间盒 M0–M5→Task 1–11 顺序；§8.1–8.5→Global Constraints 与 Task 8/10；§8.6→Task 10 Step 1（**唯一需用户裁定的开放项**）。
- **无占位**：全部代码块与命令均给出实际内容；仅 Task 10 Step 1 的文案与 Task 11 Step 6 的三个 `<填>` 属于"必须由现场实测/用户裁定产生"的空，已标出取证方式。
- **类型一致**：`AnomalyEntry.Apply/Revert/SettleToNormal`、`AnomalyResolver.Resolve(Transform)`、`Loop3D.anomalies/Begin(int?)/Report(bool)`、`Bootstrap3D.FbxYaw/CorridorFbxPath/MakeTexMat/PaintShell`（8 形参，Task 8 加 2 个、Task 10 加 1 个）、`AudioController.ambientLoop/footstep/anomalyCue/PlayFootstep`、`FirstPersonController.audio/stepDistance` 在 1–11 号任务里签名逐处对齐；`Verify3D` 的六个入口（`ProbeHierarchy`/`VerifyFbx`/`VerifyAssembly`/`VerifyLook`/`VerifySound`/`VerifyGameplay`）共用同一套 `fails` + `Report()` 日志约定；`FbxYaw` 只有定义处一处赋值。
- **验收段数**：`tools/unity_verify.sh` 随任务增长——Task 7 三段（fbx/asm/play）、Task 8 四段（+look）、Task 9 五段（+sound），顺序恒为 fbx → asm → look → sound → play。
- **已知偏差**：spec §7 把"10 个异常实机全部可辨识"记在 M3，本计划把它放在 Task 11 Step 3 补验（要先有点名工装才是诚实的验证）。M3 结束时只保证几何与贴图在位。
