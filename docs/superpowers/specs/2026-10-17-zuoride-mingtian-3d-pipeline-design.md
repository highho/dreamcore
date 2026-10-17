# 《昨日的明天》3D 轨道与 Blender 资产管线 · 设计

日期：2026-10-17 ｜ 状态：**已批准**（§8 默认值整批追认）｜ 基线提交：`4ba3c9d`

文中所有行号引用（`Loop3D.cs:33-38` 等）一律针对基线提交 `4ba3c9d` 的工作树；代码一旦
改动，引用需重新核对，不可直接沿用。

## 0. 已定案（本轮对话拍板，不再重开）

| 项 | 决定 | 依据 |
|---|---|---|
| 游戏中文名 | 《昨日的明天》 | 用户给定，补上记忆中最后一个待拍板项 |
| 交付轨道 | 真 3D 第一人称（Main3D） | 用户选定；2D 轨道保留为参照，不再投入内容 |
| 结构方案 | A：单走廊复用 + 异常变体集 | 用户选定；建模量一套，异常为纯开关 |
| 时间预算 | 8 小时满预算，jam 刚开赛 | 用户答"刚开" |
| 主题约束 | 本届无主题限制 | 用户答；内核自主定为"昨日想象中的明天已过期" |
| 美术源 | Blender 建模 + 程序化贴图，零第三方资产 | 承接既定"无版权"约束 |

叙事内核：千禧年前后的中国居民空间（绿漆墙裙、CRT、2000 年挂历、单位大院），
"曾经被许诺的未来已经过期"。这是《昨日的明天》这个名字的兑现方式。

## 1. 目标与非目标

**目标**：8 小时内交付一个可通关、异常可辨识、氛围成立的 3D 找异常作品。

**非目标**（明确排除，防止时间盒被撑破）：
- 不做多场景串联（方案 B 已否）—— 只有一条走廊。
- 不做光照烘焙 —— 时间与陷阱成本都过高。
- 不做角色动画/骨骼 —— 黑影是静态贴片或静态模型。
- 不追求高模 —— 最终观感由降质滤镜决定，面数预算见 §5。
- 不动 2D 轨道的代码与产物（保底参照，`Bootstrap.cs`/`Main.unity` 原样保留）。

## 2. 架构与组件边界

四个单元，跨单元接口只有"产物约定"一条：

| 单元 | 职责 | 对外接口 | 依赖 |
|---|---|---|---|
| `blender/`（新建） | 走廊几何唯一真相、异常变体摆放、导出 | `dreamcore/Assets/Content/3D/Corridor.fbx` + 集合命名规范 | 仅 Blender |
| `Bootstrap3D`（改） | 从 FBX 层级装配场景、生成异常目录、重建 Main3D | `-executeMethod Dreamcore.EditorTools.Bootstrap3D.Run` | FBX、VolumeProfile |
| `Loop3D`（改） | 按目录取异常 → Apply/Revert；判定与结局 | `AnomalyCatalog`（入），`OnHudUpdate`/`OnEnd`（出） | **不依赖资产来源** |
| 胶水 `tools/` | `blender_export.sh`、`build.sh` 按模式分流产物 | 两条命令 | 上述三者 |

数据流：

```
GUI 建模（MCP 驱动，人机交互）
   └→ blender/gen_corridor.py + anomalies.py   ←同一份脚本，两种执行方式
        └→ headless blender --background --python export.py
             └→ Assets/Content/3D/Corridor.fbx
                  └→ Bootstrap3D.Run  装配 + 生成 AnomalyCatalog.asset
                       └→ Main3D.unity
                            └→ build.sh 3d → builds/win64-3d/Dreamcore-win64.zip
```

**关键解耦**：`Loop3D` 的异常数据结构不依赖 Blender 资产是否到位。异常定义是
"一组物件的开合"，物件可以是 FBX 里的真模型，也可以是灰盒 cube。因此玩法能先于
美术独立验通——这是 §7 保底降级的结构基础。

## 3. 命名与产物约定（跨单元契约，改动需两侧同步）

Blender 侧集合结构：

```
base                    恒定不变的走廊本体（墙面/地面/灯/门/信箱/窗…）
anom/01/show/<名字>      异常 01 激活时出现的物件
anom/01/hide/<名字>_off  异常 01 激活时消失的物件；去掉 _off 即 base 里的目标名
```

- 异常语义由**集合路径**（`show` / `hide`）给出，不由对象名里的 token 给出，避免命名规则脆弱。
- `hide` 代理用固定后缀 `_off`，不是直接同名的对象。**原因（Blender 硬约束）**：Blender
  对象名全局唯一，`base/灯3` 与 `anom/01/hide/灯3` 无法共存，后者会被自动改名为
  `灯3.001`，"同名即目标"的映射当场失效。`_off` 后缀保证名字唯一且映射是机械可逆的。
- 导出为单个 FBX，不拆文件；Unity 侧导入一次。

Unity 侧数据资产（均由 Bootstrap3D 生成，**不在编辑器里手摆**）：

```
AnomalyDefinition : ScriptableObject
    string id           // "anom03"
    string title        // 人类可读，用于日志与调试 HUD
    GameObject[] show
    GameObject[] hide
AnomalyCatalog : ScriptableObject
    AnomalyDefinition[] definitions
    int   maxDepth
    int   maxMistakes
    float cleanChance
```

## 4. 玩法与异常编排

### 4.1 现有缺陷（这是结构问题，不是美术缺口）

`Loop3D.cs:33-38` 当前实现为 `HasAnomaly = Random.value < 0.55` 的运行时掷骰子，
异常表现只有"灯灭 + 随机亮黑影"。玩家没有见过"正常"，"异常"无从谈起；且随机黑影
的出现位置不可预期，判定不成立。本轮整体替换。

### 4.2 规则

1. **depth 1 强制无异常**。玩家必须先建立"正常长什么样"的记忆锚。
2. depth ≥ 2 每轮先掷 `cleanChance`：命中则该层**干净**（无异常）；未命中则从
   `AnomalyCatalog` 里**本局未出现过**的异常中等概率抽一个。
   - 干净层不可省：否则"永远有异常 → 无脑回头"成为支配策略，玩法当场塌掉。
   - 顺序写死（先干净判定、后抽样），避免实现歧义。
3. 判定沿用已有两端触发门：走到尽头 = 前进，走回起点 = 回头；有异常时回头为对，
   无异常时前进为对（与 2D 版规则字面一致）。
4. 错 3 次 → 坏结局；抵达 depth 8 → 好结局。
5. **可见性约束**：每个异常必须在走过 24m 直线走廊时必然进入视野。因此走廊
   **不设转角**——转角会切断"回头"时的视觉记忆，且增加建模量。
6. 抽取结果与表现必须确定性：同一 id 每次表现完全一致，记忆才可比较。

### 4.3 异常清单（10 个）

| id | 表现 | show / hide |
|---|---|---|
| anom01 | 声控灯全灭，只剩绿色应急光 | hide 全部点光，show 应急灯 |
| anom02 | 走廊尽头多出背对镜头的人影 | show 人影 @z≈22 |
| anom03 | 一户防盗门半开 | hide 关闭的门，show 半开的门 |
| anom04 | 墙上开锁疏通小广告整排消失 | hide 广告组 |
| anom05 | 地上多出一双小孩旧布鞋，鞋尖朝镜头 | show 布鞋 |
| anom06 | 靠墙的旧自行车倒了 | hide 直立车，show 倒地车 |
| anom07 | 信报箱小门全部敞开 | hide 关闭箱体，show 敞开箱体 |
| anom08 | 门缝下渗出一滩水，带反光 | show 水渍贴片 |
| anom09 | 天花板垂下一段不属于这里的红电线，微晃 | show 红电线 |
| anom10 | 窗外该有居民楼的地方全黑 | hide 夜空，show 纯黑 |

**主题彩蛋（建议）**：好结局画面里，走廊尽头墙上多出一张 2000 年挂历，日期印着
不存在的 **2 月 30 日**——《昨日的明天》在这一格兑现，成本只有一个 show 物件。

## 5. 渲染、音频与美术预算

- **面数**：base 走廊 < 2 万三角面；单个异常物件 < 3 千。
- **材质**：全部 URP/Lit；贴图由 Blender 程序化生成（水磨石地面、绿漆墙裙、米白墙、
  锈迹信报箱），512×512 足够——最终观感本来就要被降质滤镜吃掉。零第三方资产，
  与提交清单的素材声明一致。
- **光照**：不烘焙。沿用 `RenderSettings.fog` linear 2–26、ambient 0.10、
  6 盏暖黄点光关阴影（`Bootstrap3D.cs:96-100`）。
  **未验风险**：纯点光 + 无阴影下，24m 走廊内 10 个异常的可辨识度未经实机确认；
  看不清的异常在 M3 直接砍，不做灯光重做。
- **后处理**：复用 `VolumeProfile-Dreamcore`（`Bootstrap3D.cs:143` 已接）。
- **音频**：4 个合成 wav 已在位但 3D 侧无人调用。脚步声接 `FirstPersonController`，
  异常提示音接 `Loop3D.Apply`，环境音循环接场景启动。

## 6. Blender MCP 组装（含实测事实）

**版本与包名**（实测）：
- 本机 Blender **5.2.2 LTS**，位于 `D:\Game Library\Steam\steamapps\common\Blender`
  （注册表键 `Steam App 365670`）。
- PyPI 正主是 **`mcp-for-blender` 2.1.9**；`blender-mcp` 为 legacy（2.0.0），上游
  README 明确"新装请用 mcp-for-blender"。仓库 `ahujasid/mcp-for-blender`，
  31221 star，最后提交 2026-10-06，未归档。
- `uv 0.12.0` 在位（`E:\ServBay\ServBay\packages\python\current\Scripts\uvx`）；
  `uvx --from mcp-for-blender` 可解析，31 包装成功。
- Blender 5.2 仍保留旧 addon 体系：`bpy.ops.preferences.addon_install` / `addon_enable`
  存在（实测）；`addon.py` 6441 行 / 270KB，含算子 `blendermcp.start_server`、
  `blendermcp.stop_server`，面板位于 `VIEW_3D / UI`，默认端口 **9876**。

**已实测的坑**：`mcp-for-blender addon-paths` 只发现 `5.1` 与 `5.0` 的 addons 目录，
**没有 5.2**——因为 Steam 版 5.2 至今没被启动过（`%APPDATA%\Blender Foundation\Blender\`
下只有 `5.0/`、`5.1/`）。直接跑 `install-addon` 会把 addon 装进旧版本目录、对 5.2 无效。
**规避**：先启动一次 5.2 生成配置目录，再装 addon；或直接把 `addon.py` 拷进
`%APPDATA%\Blender Foundation\Blender\5.2\scripts\addons\`。

**MCP 与 headless 的分工**（各治一段，非二选一）：
- MCP = 交互建模、视口截图回看、比例与材质微调。**必须 GUI 常驻**：addon 的 socket
  server 靠 `bpy.app.timers` 消费命令队列，`--background` 不起事件循环。此假设在 M0
  顺带验证。
- headless `blender --background --python` = 每次出货前的可复现导出。`build.sh` 不能
  依赖"GUI 正好开着且服务器正好点起来了"。
- **两者共用同一份 `blender/gen_corridor.py`**：它既是 MCP 会话里逐步执行的源码，也是
  headless 全量重跑的入口。杜绝"GUI 里手改的东西导出时不存在"的双真相事故。

**安全暴露面（登记，不处置）**：addon 默认允许 MCP 在 Blender 内执行任意 Python。
`BLENDER_MCP_SAFE_MODE=1` 会拦文件读写与网络，但导出 FBX 需要写文件。本机单人 jam，
默认不开 safe mode，把这条当已知暴露面记入提交清单。

## 7. 时间盒与出口条件

| 盒 | 时段 | 内容 | 出口条件（须贴实际输出） |
|---|---|---|---|
| M0 | 0:00–0:30 | **FBX 层级保真探针**：Blender 3 个 cube 放进 2 个集合 → 导出 → Unity 导入 → Bootstrap 打印真实层级与名字 | 打印结果含集合名与对象名；不过则换 glTF，或退到"按子物体顺序装配" |
| M1 | 0:30–1:30 | 异常数据层 + `Loop3D` 改造 + **灰盒**异常物件 | 跑完一局好结局、一局坏结局；`build.sh 3d` 出 exe |
| M2 | 1:30–4:00 | Blender 建正式走廊 + 10 组异常资产 | FBX 替换灰盒后玩法判据不回退 |
| M3 | 4:00–5:30 | 贴图/灯光/后处理调参 + 音效接入 | 10 个异常实机全部可辨识 |
| M4 | 5:30–7:00 | 通关验证、结局文案、封面、提交清单更新 | 清单全绿 |
| M5 | 7:00–8:00 | 缓冲 + 终版构建 + zip | 提交物就位 |

**M1 结束即存在一个可提交物**，这是整份设计的保险：美术做不完，游戏仍然完整。

**验收口径**：每个盒的出口条件都要贴实际输出——Blender 脚本的 print 统计、Unity
batchmode 日志 grep 结果、exe 存在性、实机 Player.log。自写的检查脚本必须先被喂一个
坏样本、确认它会失败，之后才允许它背书。

**降级路径**（超时即刻执行，不讨价还价）：
- M0 探针失败 → 改 glTF 导出；再失败 → Bootstrap 侧按子物体顺序装配，命名规范作废。
- M2 超时 → 保留灰盒几何，只换 Blender 程序化贴图（玩法无损，观感有救）。
- M3 超时 → 异常数 10 砍到 6，优先留 anom01/02/03/05/06/08。

## 8. 数值与表现定案（2026-10-17 用户整批追认默认值）

1. `maxDepth=8`、`maxMistakes=3` —— **沿用 2D 版数值**。
2. 异常数量 —— **10**；超时保底砍到 6（见 §7 降级）。
3. `cleanChance` —— **0.35**。
4. HUD —— **换 ScreenSpaceCamera**，与 2D 版当初决策一致：梦核质感优先，接受文字可读
   性下降。注意 3D 版现状是 `ScreenSpaceOverlay`（`Bootstrap3D.cs:166`），按已记录的
   URP 坑 Overlay 不吃后处理，须在 M3 改掉。
5. 好结局"2000 年挂历 / 2 月 30 日"彩蛋 —— **采用**。
6. 结局文案 —— M4 出草稿后定稿（唯一仍开放的项）。

## 9. 已知坑位（实施期追加，不回改历史条目）

- `tools/build.sh:12` 的 `OUT` 写死 `builds/win64`，`3d` 模式会覆盖 2D 产物并把它打成
  `Dreamcore-win64.zip`。已定案走 3D，须在 M1 前修成按模式分流。
- URP 无参 `Create()` 丢 renderer → 构建期日志暴涨（历史实测 64.7GB 卡死）。
  `Bootstrap3D` 复用现有 Profile，勿回退该写法。
- Overlay UI 不吃后处理（见 §8.4）。
- `raw.githubusercontent.com` 本机被连接重置（curl rc=56），取源码走 `gh api`。
- `mcp-for-blender addon-paths` 只认已生成配置目录的 Blender 版本（见 §6）。

### 9.1 M0 探针实测（Task 1，2026-10-17，Blender 5.2.2 → Unity 6.3）

样本：4 个 1m 立方体分别放进集合 `base` / `anom/01/show` / `anom/01/hide`，沿 Blender +Z
每隔 10 排开，导出 `Assets/Content/3D/Probe.fbx`，Unity batchmode 打印真实层级。

Unity 侧原始读数（`logs/verify-probe.log`，逐字）：

```
[VERIFY] ---- 层级实测 ----
Probe  <node>
Probe/floor  <mesh>
Probe/floor_off  <mesh>
Probe/shadeFigure  <mesh>
Probe/wallL  <mesh>
[VERIFY] 集合是否成为节点: False
[VERIFY] 尺度 z差=0（期望≈10）
[VERIFY] 实测位置 wallL=(0.00, 0.00, 0.00) floor=(0.00, 10.00, 0.00) 距离=10 rootScale=(1.00, 1.00, 1.00) rootRot=(0.00, 0.00, 0.00)
[VERIFY] PASS: 期望对象名全部保真 wallL,floor,shadeFigure,floor_off
```

坏样本回喂（`VERIFY_BAD=1`，`logs/verify-probe-bad.log`）：`[VERIFY] FAIL: 缺失 no_such_object_xyz` —— 检查器会红，PASS 才算背书。

三条假设的实测结论：

| 假设 | 实测 | 依据 |
|---|---|---|
| 集合会变成 Unity 层级节点 | **不成立** | 层级是扁平的 `Probe/<对象名>`，`base`/`anom`/`01`/`show`/`hide` 一级都没出现 |
| 对象名不被改写 | **成立** | 四个叶子名逐字节等值，无前缀、无 sanitization、无 `.001` 撞名 |
| 1 Blender 单位 = 1 Unity 米 | **成立（世界尺度）** | `距离=10`、`rootScale=(1,1,1)`；但**轴被重排**：Blender +Z 的 10 米偏移落到 Unity +Y，所以计划原文的 `z差` 读数是 0，不是尺度错 |

归因到 Blender 导出侧，不是 Unity 导入侧：`Probe.fbx` 二进制里可提取的节点名只有
`RootNode` 与四个对象名（对整份文件严格 grep `base|anom` 零命中），集合名在文件里就已经不存在；且 Blender 5.2.2 的
`bpy.ops.export_scene.fbx` 参数表里没有任何"集合导出为节点"的开关（实测 dump：
`collection` / `use_active_collection` 只是过滤范围，`object_types` 只认对象不认集合）。

**候选出路（第二种，已实测但未采纳，等裁定）**：在 Blender 里按集合路径建同名 EMPTY 做父级
（`Rig/base`、`Rig/anom/01/show`…），立方体 parent 到对应 Empty。实测（`logs/probe-tmp2.log`）：

```
ProbeTmp/anom/01/hide/t_floor_off <mesh> local=(0.00, 0.00, 0.30) world=(0.00, 30.00, 0.00) lossy=(100.00, 100.00, 100.00) boundsSize=(0.01, 0.01, 0.01)
ProbeTmp/base/t_floor <mesh> local=(0.00, 0.00, 0.10) world=(0.00, 10.00, 0.00) lossy=(100.00, 100.00, 100.00) boundsSize=(0.01, 0.01, 0.01)
```

层级路径整条保住，世界位置也仍是 10/20/30 米。

**归因更正（Task 1 第二轮，2026-10-17；依据本任务 clean 跑的实际输出，不是推测）**：本节原文一度把
`lossyScale=100` + 顶点预除 100 记成"Empty 包裹带来的代价"，并暗示前缀式没有这个副作用。**这条归因已被
证伪。** 已提交的**扁平、无包裹**的前缀式 `Probe.fbx`（`logs/verify-probe.log`）五个节点**全部**报
`lossyScale=(100.00, 100.00, 100.00)` / `boundsSize=(0.01, 0.01, 0.01)` / `worldSize=(1.00, 1.00, 1.00)`，
而 `rootLossyScale=(1.00, 1.00, 1.00)`。也就是说：**100 倍局部缩放配上预除 100 的顶点，是 Blender 导出 →
Unity 导入这条通道本身的属性**（cm→m 单位缩放被写进节点 transform、几何同步预除），与有没有 Empty 包裹无关。
净**世界**尺寸是对的：1m 立方体读到的仍是 1m。

**由此定下后续任务一律遵守的规则：FBX 导入的节点里，只有"世界量"是米。**
可以按米读的：`Renderer.bounds.size/center`、`Collider.bounds.size/center`。
**不是**米的：`Transform.localScale`（这里读到 100）、`Mesh.bounds`（读到 0.01）、`BoxCollider.size`、
`Collider.center`（局部空间），以及处在被缩放祖先之下的 `localPosition`。想按米算尺寸/摆位，就走世界量，
别在局部空间做算术。

**Empty 包裹仍然是被拒绝的那条出路**，但拒绝理由要写准：通道级的 100× 它躲不掉（前缀式同样带着），
它**额外**还伤害两件事 —— ① 连子节点的 `localPosition` 也被一起除/乘（实测 `local=(0.00, 0.00, 0.30)` →
`world=(0.00, 30.00, 0.00)`），把最后一层局部读数也废掉；② 100× 落在**父级**而不是叶子，任何想给父节点
手摆 `Transform` 的代码会整枝一起歪。前缀式只保留"叶子 localScale=100"这一份通道税，不增加这两条。

**状态**：§3 的路径式契约按原样**不成立**。退化成前缀式、还是走 Empty 包裹，属跨任务契约
变更（牵动 Task 3 解析器 / Task 6 建模 / Task 7 装配），由主控裁定后再改 §3；本任务不自行
改约。已定案的两条：叶子名保真可依赖，世界尺度 1 单位 = 1 米可依赖（但别拿 Blender +Z 当
Unity +Z 用）。

**工具坑（实测，写脚本时必用）**：
- `blender --background --python X --python-exit-code 4` 里该参数**放在 `--python` 之后会被忽略**
  （故意抛异常仍 rc=0）；必须写作 `--background --python-exit-code 4 --python X` 才拿到 rc=4。
- 计划原文的 `grep -o "\[VERIFY\] FAIL[^"]*"` 引号不配对（`[^"]` 里的 `"` 提前闭合字符串），
  `bash -n` 直接报 unexpected EOF；改单引号 `'\[VERIFY\] FAIL[^"]*'` 才跑通。

### 9.2 前缀式 wire 契约裁定（Task 1 修复轮，2026-10-17）

§9.1 实测证明路径式集合契约在 FBX 通道下拿不到层级节点（`Probe.fbx` 严格 grep `base|anom` 零命中），
Empty 包裹虽能保住路径，却要**在通道级 100× 之外**再叠两重伤害（连子节点 `localPosition` 一起除/乘、把 100×
放到父级而非叶子），故主控裁定采用**前缀式 wire 契约**：
Blender 集合只作编排侧容器、不再指望它进 FBX，异常语义改由对象名前缀语法承载 —— base 物件保持裸名
（`wallL`/`floor`…），出现物件用 `anom<NN>_show_<leaf>`，消失代理用 `anom<NN>_hide_<base目标名>_off`。
探针已按此改名并新增第二个 show 物件（`anom01_show_probeCubeB`）证明语法对多前缀、多桶成员都成立：
Unity 侧实测 `worldSize=(1,1,1)`（节点 `lossyScale=100` 与 `boundsSize=0.01` 相消，净尺度 1m）、
`rootLossyScale=(1,1,1)`、`rootRot=(0,0,0)` 而逐节点 `worldRot=(270.02,0,0)`（导入器把 Z-up→Y-up 的
轴向重排**下放到每个节点自己**，不是放在根上）、hide→base 目标映射可解析，`+Y分离=10` 复述轴向事实。

**这组读数是 §9.1 归因更正的出处**：`lossyScale=100` + 顶点预除 100 **不是** Empty 包裹带来的，
它是 Blender 导出 → Unity 导入这条**通道本身的属性**，在扁平、无包裹、前缀命名的节点上就这么测出来的。
因此对后续所有任务定规则：**FBX 导入的节点里只有世界量是米** —— 可信的是 `Renderer.bounds.size/center`、
`Collider.bounds.size/center`；`Transform.localScale`（读到 100）、`Mesh.bounds`（读到 0.01）、
`BoxCollider.size`、`Collider.center`（局部）、以及被缩放祖先之下的 `localPosition` **都不是米**。
**显式警告：M0 门变绿不代表局部空间数字是米。** 门只证名保真与净世界尺度，局部量一律不许拿来算尺寸或摆位。
Empty 包裹仍被拒绝，理由见 §9.1：它额外把子节点 `localPosition` 也一起除了（`local=(0,0,0.30)` →
`world=(0,30,0)`），并把 100× 从叶子搬到父级。

`Verify3D.ProbeHierarchy()` 已把这几条（名保真/base 裸名不被前缀误伤/wire 往返 + hide 目标解析/几何尺度与
rootScale 断言）做成有牙的门，且**每条分支都在 `VERIFY_BAD=1` 的坏样本跑里亲眼红过**：一次注入同时打出
逐项 8 行 `[VERIFY] FAIL`（缺名 1 行 / hide 目标解析失败 1 行 / 世界尺寸越容差 5 行 / root lossyScale 1 行）
加 1 行汇总，clean 才 PASS。**语法唯一真相将由 Task 3 的 `AnomalyResolver` 定义，
§3 待 Task 3 重写，本轮不动 §3。**

