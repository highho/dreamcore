# 《昨日的明天》3D 轨道与 Blender 资产管线 · 设计

日期：2026-10-17 ｜ 状态：**已批准**（§8 默认值整批追认）｜ 基线提交：`4ba3c9d`

文中所有行号引用（`Loop3D.cs:33-38` 等）一律针对基线提交 `4ba3c9d` 的工作树；代码一旦
改动，引用需重新核对，不可直接沿用。

## 0. 已定案（本轮对话拍板，不再重开）

| 项 | 决定 | 依据 |
|---|---|---|
| 游戏中文名 | 《昨日的明天》 | 用户给定，补上记忆中最后一个待拍板项 |
| 交付轨道 | 真 3D 第一人称（Main3D） | 用户选定；~~2D 轨道保留为参照~~ **2026-10-17 用户改判"2d的可以扔了"**，2D 保底已废弃并从仓库删除，出货单轨 |
| 结构方案 | A：单走廊复用 + 异常变体集 | 用户选定；建模量一套，异常为纯开关 |
| 时间预算 | 8 小时满预算，jam 刚开赛 | 用户答"刚开"；**M6 美术充实三段另放宽为"半天以上不设限"** |
| 主题约束 | 本届无主题限制 | 用户答；内核自主定为"昨日想象中的明天已过期" |
| 美术源 | ~~Blender 建模 + 程序化贴图，零第三方资产~~ **已被两次改判覆盖**：贴图取 ambientCG CC0（M3 勘误，§5），道具网格取 Poly Haven CC0（M6，§9.5）；红线是**仅 CC0、CC-BY 不接受**，不是"零第三方" | 用户裁定；逐件许可证读数见 `logs/props-license.txt` |

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

> 本节按 §9.1 实测与 §9.2 主控裁定**整体重写为前缀式 wire 契约**（Task 3）。原文的
> "异常语义由集合路径给出"已被实测证伪：Blender 集合从不进 FBX，Unity 拿到的是扁平层级。

Blender 侧集合结构（**仅作编排侧容器**，方便建模时按桶开关，不再指望它过 FBX）：

```
base                     恒定不变的走廊本体（墙面/地面/灯/门/信箱/窗…）
anom/01/show             异常 01 激活时出现的物件集合
anom/01/hide             异常 01 激活时消失的代理集合
```

FBX wire 契约（唯一真相在 `AnomalyResolver.cs`，其它处一律调它的 `IsAnomalyWireName`）：

```
floor / wallL / Door_3 / Light_2 …      base 本体：裸名，不加任何前缀
anom<NN>_show_<leaf>                    异常激活时出现的物件（anom01_show_shadeFigure）
anom<NN>_hide_<base目标名>_off          异常激活时消失的代理；剥掉尾部 _off 即 base 目标名
                                        （anom01_hide_Light_2_off → 指认 Light_2）
```

- 异常语义由**对象名前缀语法**给出；解析器递归遍历整个场景根按名分桶，不依赖任何
  `anom`/`show`/`hide` 节点（§9.1：这些节点在 FBX 里根本不存在）。
- `hide` 代理用固定后缀 `_off`，不是直接同名的对象，代理自己永不渲染。**原因（Blender 硬约束）**：
  Blender 对象名全局唯一，`base` 里的 `灯3` 与消失代理无法同名，后者会被自动改名为 `灯3.001`，
  "同名即目标"的映射当场失效。`_off` 后缀保证名字唯一且映射是机械可逆的。
- 解析失败的疑似 wire 名（如位数不对的 `anom1_show_x`、bucket 不是 show/hide 的 `anom01_foo`）
  由解析器打 `Debug.LogWarning` 后忽略，不静默、不抛异常。
- 导出为单个 FBX，不拆文件；Unity 侧导入一次。

Unity 侧运行时数据（由 `AnomalyResolver.Resolve(sceneRoot)` 在 Bootstrap3D 装配时生成，
**不在编辑器里手摆，也不再是 ScriptableObject 资产**）：

```
[Serializable] AnomalyEntry            // 列表直接序列化在场景里的 Loop3D 组件上
    string id                          // "anom03"
    string title                       // 来自 AnomalyTitles 表，人类可读，用于日志与调试 HUD
    List<GameObject> show
    List<GameObject> hide
    Apply() / Revert() / SettleToNormal()
```

**落点修正的理由**：ScriptableObject 资产持有场景 GameObject 的引用是伪引用——场景一重载
即失效，而 Main3D 每次都由 Bootstrap3D 重建；`AnomalyEntry` 列表序列化在 `Loop3D` 上、
物件由解析器在装配当场从 FBX 实例接线，才是 Unity 常规接线方式。
`maxDepth=8`、`maxMistakes=3`、`cleanChance=0.35` 等数值仍按 §8 序列化在 `Loop3D` 上。

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
  **M3 勘误（2026-10-17，上方原文不回改）**：贴图口径变了。实际采用 **ambientCG 的 CC0 PBR 贴图**
  （5 个资产 × `BaseColor`/`Normal`/`Roughness` 三张 1K JPG，共约 13MB，由 `./tools/fetch_textures.sh`
  落盘，出处清单 `dreamcore/Assets/Content/Textures/CREDITS.md`），不再走 Blender 程序化生成。
  CC0 1.0 无署名义务、可商用，但**"零第三方资产"这句已经不成立**——提交清单必须如实写
  "第三方 CC0 贴图（ambientCG）+ 程序合成音频 + Blender 自建几何"。
  换法理由：25 个网格要的是"水磨石地面／白灰墙／绿瓷砖墙裙"这种一眼可辨的材质身份，
  程序化噪声在剩余预算内给不出这个辨识度的置信度。`Roughness.jpg` 已下载但**本轮未接进材质**
  （URP Lit 的粗糙度走 `_MaskMap` 的 A 通道，做掩膜转换的成本高于它在这个降质滤镜下的收益）。
  **M6 勘误（2026-10-18，上方原文不回改）**：第三方口径又不止贴图了。**道具网格本身也是第三方 CC0**——
  13 件来自 Poly Haven（`./tools/fetch_props.sh` 取，逐件核实页面上的 "CC0 1.0 Universal" 才下载，
  出处 `Assets/Content/Textures/PROPS_CREDITS.md`），由 `blender/gen_props.py` 归一到米制、
  按 `Prop_<slug>[__<n>]` 并入 `base` 后与走廊一起导出。所以提交清单的素材声明要写成
  "Blender 自建走廊壳体与异常件 + **第三方 CC0 道具网格（Poly Haven）** + 第三方 CC0 贴图（ambientCG / Poly Haven）
  + 程序合成音频"。面数预算同步放宽：15 件道具实例合计 **60 842** 三角面
  （逐件面数在 `logs/blender-build.log` 的 `[BUILD] prop ... tris=` 行，与 Poly Haven 官方 `polycount` 等值断言），
  原"base 走廊 < 2 万"那条只覆盖自建壳体、不含道具。详见 §9.5。
- **后处理**：~~复用 `VolumeProfile-Dreamcore`（`Bootstrap3D.cs:143` 已接）~~
  **M4 实机证伪（2026-10-18）**：那个资产出厂时 `components: []` 是**空的**，"已接"只到"Volume 挂了个空壳"，
  构建包里根本没有 Bloom/暗角/颗粒。现在由 `Bootstrap3D.EnsureDreamcoreProfile` 真填五道 override
  （Bloom 0.55/阈值 0.75、Vignette 0.38、FilmGrain Medium1 0.45、ColorAdjustments 曝光 -0.1/饱和 -10、
  色散 0.22），细节与那个"存成 `{fileID: 0}` 空引用"的坑见 §9.4。
- **光照**：不烘焙。沿用 `RenderSettings.fog` linear 2–26、ambient 0.10、
  6 盏暖黄点光关阴影（`Bootstrap3D.cs:96-100`）。
  ~~**未验风险**：纯点光 + 无阴影下，24m 走廊内 10 个异常的可辨识度未经实机确认~~
  **M3–M4 已验**：`logs/captures` 的 before/after 帧 10 组全部 md5 不同（编辑态取证），
  构建包实机截图另证灯辉光/暗角/颗粒在帧上（`logs/shots2/crop02.jpg`）。
  过程中真砍过东西：三条异常（anom05/08/10）几何上被埋没，是靠这条判据抓出来改的，不是靠肉眼。
- **音频**：4 个合成 wav 已在位但 3D 侧无人调用。脚步声接 `FirstPersonController`，
  异常提示音接 `Loop3D.Apply`，环境音循环接场景启动。
  **M3 实接（2026-10-17）**：`footstep.wav`→`FirstPersonController`（按水平位移每 1.5m 落一步，不按时间，
  落地状态才计数）；`anomaly_cue.wav`→`Loop3D.Roll` 抽中异常并 `Apply()` 之后；`tape_click.wav`→`Loop3D.Report`
  判定落下时；`ambient_loop.wav`→场景根 `AmbientAudio`（loop+playOnAwake+`spatialBlend=0`）。
  接线证据在 `logs/verify-asm.log` 的 `[VERIFY] 音频接线:` 行。
  **本节所有 `Bootstrap3D.cs:行号` 引用是基线 `4ba3c9d`/M2 期读数，M3 改动后已错位，勿按行号取用。**

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

**M3–M5 出口实测（2026-10-18 追加，不回改上表）**：
- M3「10 个异常实机全部可辨识」→ **达成，且一条没砍**。判据是 `logs/captures` 的 before/after 帧 md5
  逐组不同（10/10），代价是修了三处几何埋没（§9.3）与两处贴墙摆放（§9.4）。
  降级预案（砍到 6）没启用。
- M4「清单全绿」→ `tools/SUBMIT_CHECKLIST.md` 已从 2D 口径整体迁到 3D（构建路径、操作说明、
  抽查项、截图来源、自包含包结构、后端口径），三段验收绿跑 + `VERIFY_BAD=1` 15 条坏期望逐条红。
- M5「提交物就位」→ `builds/win64-3d/Dreamcore-win64.zip`（45MB / 166 条目），
  且已用"解到项目外临时目录再启动"证明自包含可玩。

**M6 美术充实三段出口实测（2026-10-18 追加，不回改上表）**：用户裁定"建模过于简陋"后加的三段，
不在原 8 小时盒内（时间盒放宽为"半天以上不设限"）。

| 段 | 内容 | 出口条件（实际读数） |
|---|---|---|
| S1–S6 | 墙体真开洞 + 门窗灯构件 + 异常侧同步 + 命名→材质表 + 门禁重构 + 全链 | 三段验收绿跑；`CheckHoles` 两条结构门（洞中心不被墙覆盖 / 覆盖件盖得住洞口）坏跑逐条红 |
| T1 | ambientCG 三张替换 + `_NORMALMAP` 关键词 + NormalMap 导入器门 | 装配段绿跑，Tex 桶逐件吃到 BaseColor/Normal |
| T2 | Poly Haven 13 件 CC0 道具 → 15 个实例并入 base + 三条摆放门 + 自身 UV 门 | 绿跑 rc=0（95 网格 / Tex 75·Flat 10·Glass 1 / 20 张 NormalMap）；`VERIFY_BAD=1` rc=1（fbx 27 / asm 25 / play 4 逐条红）；`build.sh` rc=0，zip 62.6MB / 166 条目；`logs/shots7` 五帧两两 DIFFER |

细节与踩坑见 §9.5；道具侧的建模口径见 `blender/README.md` §5。


## 8. 数值与表现定案（2026-10-17 用户整批追认默认值）

1. `maxDepth=8`、`maxMistakes=3` —— **沿用 2D 版数值**。
2. 异常数量 —— **10**；超时保底砍到 6（见 §7 降级）。
3. `cleanChance` —— **0.35**。
4. HUD —— **换 ScreenSpaceCamera**，与 2D 版当初决策一致：梦核质感优先，接受文字可读
   性下降。注意 3D 版现状是 `ScreenSpaceOverlay`（`Bootstrap3D.cs:166`），按已记录的
   URP 坑 Overlay 不吃后处理，须在 M3 改掉。
   —— **M3 已改（2026-10-17）**：`Bootstrap3D.CreateHud` 走 `ScreenSpaceCamera`，`worldCamera` 指主相机、
   `planeDistance=0.4f`（必须 > `nearClipPlane=0.1f`，且小于 CharacterController 半径 0.5f 顶出来的
   最小墙距，否则 UI 会扎进几何体被挡掉）。门在 `Verify3D.VerifyAssembly` 的 HUD 两条断言。
5. 好结局"2000 年挂历 / 2 月 30 日"彩蛋 —— ~~采用~~ **2026-10-17 18:25 用户裁定砍掉**（8h 预算剩约 90 分钟，M4 通关验证 + M5 终版构建优先；
   该彩蛋至今无对应资产）。裁定理由记录：坏结局文案里的"挂历上圈着的那天"已经承载了这个意象，
   缺的是画面不是意思，不值当为一张贴图重跑 Blender 导出链。
6. 结局文案 —— M4 出草稿后定稿（唯一仍开放的项）。—— 定稿：**A 版**（用户 2026-10-17 逐字选定，不润色）。好结局：`你出来了。\n楼道的声控灯一盏盏亮起来，全是真的。` 坏结局：`你还在这儿。\n挂历上圈着的那天，从来没有过。`

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


### 9.3 M3 表现段实测（Task 8–11，2026-10-17，ambientCG 贴图 + 取证工装）

- **Blender 导出的 mesh 节点 `Transform.position` 恒读 `(0,0,0)`**。物体变换被烘进顶点（`box()` 里的
  `transform_apply(scale=True)` + 导出器的变换烘焙），节点本身留在原点。取证工装第一版用
  `target.transform.position` 瞄准，10 组异常全打成 `(0.00, 0.00, 0.00)`、10 张 before 逐像素相同，
  看起来像"相机坏了"，其实是取错了量。**要世界坐标就取 `MeshRenderer.bounds.center`**；
  只有 EMPTY 锚点（`Light_*`、`_off` 代理）才带真 `transform.position`。这条是 §9.2"只有世界量是米"
  的另一面：**位置也一样，只有 bounds 可信**。
- **Blender 原立方体每面 UV 铺满整个 0–1**，`transform_apply` 只改顶点不改 UV
  （`blender/probe_uv.py` 实测 25 个网格各 1 个 UV 层）。后果：24m 的地面和 0.18m 的布鞋各画一遍
  同样的贴图。规避＝Unity 侧按世界尺寸补 tiling（取 bounds 三条边里最长的两条，最短的是板厚，
  除以该表面的 tile 米数），代码在 `Bootstrap3D.Apply`，门是 `Floor` 的 `_BaseMap` tiling.x ≥ 10。
  **探针参数顺序的坑**：`blender --background --python x.py file.blend` 里那个 blend 路径会变成**脚本参数**、
  不会打开文件——第一版探针读的是默认场景那个 Cube（`mesh_count=1`），结论无效。
  正确写法 `blender --background file.blend --python-exit-code 4 --python x.py`。
- **URP Lit/Unlit 的两个属性名陷阱**：① 基色属性是 `_BaseColor`，`Material.color` 走的是 `_Color`，
  在这套 shader 上赋值不报错也不生效；② `SetTexture("_BumpMap", …)` **不会**替你开 `_NORMALMAP` 关键词，
  漏了它法线资产在、材质引用在、日志全绿，画面却是平的。门：`IsKeywordEnabled("_NORMALMAP")` 逐材质断言。
- **法线图必须改导入器**（`TextureImporter.textureType = NormalMap` + `SaveAndReimport()`），
  默认按普通图导入会走 sRGB 采样、凹凸方向全错。门查导入器本身而非材质，才抓得住"重跑 Bootstrap 之前的旧导入设置"。
- **三条异常在几何上根本不可见**（`logs/captures` 的 before/after **md5 相同**为证）：
  布鞋 `height=0.04`（占 z 0–0.08）和渗水 `height=0.02` 整件埋在楼板上表面（Floor 占 0–0.10）里；
  窗外楼群摆在 `+half_w+0.45`＝右墙外 0.5m，而**墙上没有洞**，永远被墙挡住。
  修正：鞋抬到 0.15、水膜抬到 0.11（浮在板顶之上）、窗板移到墙内表面 `half_w-0.06` 并抬到 height 2.0
  让开墙裙（占 z 0–1.2），anom10 的黑面板压 `half_w-0.08` 盖在窗板靠走廊一侧。
  **判据的形状值得记住**：可辨识性用"同一机位拍 before/after 两帧、比 md5"来判，比肉眼看一张图可靠。
- **取证工装 `dreamcore/Assets/_Project/Editor/CaptureAnomalies.cs` 的已知边界**（如实登记，不当缺陷修）：
  batchmode 进不了 Play Mode（既有实测），所以它是**编辑态静态渲染**——`LightFlicker` 不闪、
  URP 后处理链对非玩法相机是否求值不确定。它证明的是几何/贴图/灯位层面的可辨识性，
  **不等于**最终出货画面的观感；后处理与 HUD 那一路要靠构建后的实机截图另证。
- **素材口径变更见 §5 的 M3 勘误**：改走 ambientCG CC0（5 资产 × 3 map），`Roughness.jpg` 已下载但
  本轮未接进材质（URP Lit 的粗糙度走 `_MaskMap` 的 A 通道，转换成本高于它在降质滤镜下的收益）。
- **走廊真实净宽是 2.50m，不是 2.91m**（装配门口径随之从 `[2.6,3.3]` 改成 `[2.4,2.6]`）：
  旧的 2.91 由"窗外楼板露在右墙外 0.5m"撑出来——那正是上面 anom10 不可见的同一个缺陷，
  把窗板收进墙内表面 `half_w-0.06` 之后聚合宽自然掉到 2.500003。**教训：包围盒类门如果和美术缺陷
  共用同一个数，美术改了它就会"无故变红"**，所以口径里要写明这个数是谁撑出来的、改成了多少。
  `blender/README.md` 轴映射小节那两次 -90/90 读数按当时的 FBX 原样保留，只加勘误注记，不改历史读数。
- **贴图门要按 shader 分口径**：base 网格 16 个里 15 个是 URP/Lit，1 个（`WindowBuildings` 窗板）是
  M3 特意给的 URP/Unlit 自发光淡蓝灰——夜里窗外本来就该比室内亮。一视同仁查 `_BaseMap`/`_BumpMap`
  会把这唯一例外误伤成 FAIL，顺带在 Unity 日志里刷一条"Unlit 材质没有 `_BumpMap` 属性"的噪音。
  改法：贴图三条计数门只统计 `shader.name == "Universal Render Pipeline/Lit"` 的材质，
  另加一条"Lit 15 / Unlit 1 且那个 Unlit 必须是 WindowBuildings"的计数门钉死例外——
  防的是"整体回退成 Unlit 骗过贴图门"。装配段坏样本随之从 13 条扩到 14 条（各成一红、互不短路）。

### 9.4 M4–M5 实机取证（2026-10-18，构建包 + 屏幕截图 + 提交物自包含）

编辑态取证（§9.3 的 `CaptureAnomalies`）替代不了实机：这一轮三件事全是**只在构建包里才暴露**的。

- **`VolumeProfile.Add<T>()` 不挂子资产**（与 §常见坑里的 URP `Create()` 同族）：它只做
  `CreateInstance` + `components.Add`，**不调** `AssetDatabase.AddObjectToAsset`。于是
  `EnsureDreamcoreProfile` 第一版跑完，资产文件写成 `components: [{fileID: 0} ×5]`——
  编辑器里 `TryGet` 拿得到对象、看着一切正常，构建产物里那五项全是 null，**整条梦核滤镜不存在**。
  发现方式很偶然：实机截图后想定量确认暗角，顺手 grep 资产，才看见 `components: []` 的出厂状态。
  修：`GetOrAddOverride<T>` 里 Add 完立刻 `AddObjectToAsset(c, p)`；再补一条门——
  Profile 至少 5 道 override 且 Bloom/暗角/颗粒/色散四项 `intensity.value > 0`（Add 了不 Override 等于没加），
  外加"主相机 `renderPostProcessing` 真开着"。装配段坏样本 14→15 条。
  顺带两个 API 事实：这版 URP 的五个 override 类型在 **`UnityEngine.Rendering.Universal`** 下（不是 `Rendering`，
  按旧记忆写会 CS0234）；`ChromaticAberration` 是 `ClampedFloatParameter intensity`，不是旧版的 `AnimationCurve amount`。
- **我的量具不合格，如实登记**：想用"画面中心 vs 四角亮度比"判暗角，实测 `corner/centre = 1.202`——
  比 1 还大，因为中心是走廊尽头的暗端墙、四角是打亮的墙裙。**场景内容把指标污染了，这个比值不能当暗角的证据**。
  最终判据退回"同一脚本同一相机路径下改前后两帧对比 + 肉眼"（`logs/shots/crop02.jpg` 无滤镜 vs
  `logs/shots2/crop02.jpg` 有辉光/颗粒/暗角）。下次要做这类判据，先问"它在空场景上会不会动"。
- **一个 Windows 构建包不是"exe + _Data"**：还必须有同目录 `UnityPlayer.dll`，Mono 脚本后端另需 `MonoBleedingEdge/`。
  旧 `zip_release.ps1` 只收 `exe + _Data`，打出来的 25MB 包**双击没反应、也不报错**（`builds/win64-3d/` 里能跑是因为
  旁边有编辑器留下的 dll，包本身是残的）。修：收齐 exe/UnityPlayer.dll/_Data/MonoBleedingEdge(+D3D12)，
  并**回读压缩包**断言这三项在内（查产物而不是查源目录，才抓得住"漏收"）。现在 45MB / 166 条目。
  验证方式：把 zip 解到项目外的临时目录再启动（`%TEMP%/dreamcore-zipcheck`）——能跑才叫自包含。
- **脚本后端实测是 Mono 不是 IL2CPP**：`build.sh` 的 `[2/3]` 文案长期写"（IL2CPP）"，
  而产物里有 `Dreamcore_Data/Managed/*.dll`、`ProjectSettings.asset` 的 `scriptingBackend: {}` 为空。文案已改。
- **实机取证工装 `tools/shot3d.ps1`**：`-screen-fullscreen 0 -screen-width/-height` 起窗口模式（独占全屏下 GDI
  `CopyFromScreen` 容易拍到黑帧），`Start-Process -PassThru` 拿 pid，GDI 拍全屏 PNG，
  再用 `keybd_event`/`mouse_event` 合成 W/S/鼠标偏转，逐帧 md5 比对判"相机真的动了、转向真的活着"。
  两个必须有的护栏：① 发键前用 `GetForegroundWindow()` 与玩家窗口句柄核对，不一致就 throw
  （否则 WASD 会打进别的 app）；② `finally` 里无条件 Kill，脚本失败也不留后台游戏进程。
  它同时把 `player.log` 里的 error 级行打出来——本轮只剩三类已知无害噪音
  （`AMDUnityPlugin`/`NVUnityPlugin` 架构不匹配、`d3d12: failed to query info queue interface (0x80004002)`）。
- **贴墙件的中心要落在墙内表面上**：墙厚 0.1 ⇒ 内表面 `= half_w - 0.05`。海报原来用 `±0.07`，
  于是悬在墙前 1–3cm，实机里读成"一块立在走廊里的灰色板子"（`logs/shots2/crop02.jpg` 前景左下最清楚）。
  同一处还查出 `MailboxDoor` 摆在 `half_w-0.02`＝**埋进信报箱本体和墙里**，正常态完全看不见；
  改到 `half_w-0.24` 贴在本体朝走廊那一面。信报箱异常（anom07 敞开的门）本来就是独立 show 件、
  一直可见，所以这条缺陷 `logs/captures` 的 md5 判据抓不到——**它抓的是"异常前后有没有差分"，不抓"本体摆对没有"**。
- **出货包要给退出键**：`FirstPersonController.Start()` 锁了光标，构建版没有暂停菜单，
  玩家只剩 Alt+F4。加了 `Input.GetKeyDown(KeyCode.Escape) → Application.Quit()`。

### 9.5 M6 美术充实三段（2026-10-18，结构段 S1–S6 / 质感线 T1 / 杂物段 T2）

起因是用户裁定"3D 走廊建模过于简陋，需要寻找免费建模来充实"，四个缺口逐条拍板：
门窗不像真东西 / 墙面·地面质感假 / 整体氛围缺光源细节 / 道具太少空间空（并补"那两个方块太抽象了"）；
时间盒"半天以上不设限"；**素材红线"仅 CC0"（CC-BY 不接受）**；路线"先修结构再补杂物"。

**结构段（S1–S6）——门和窗得是真洞，不是贴在墙上的板子**：

- 墙体重开两处门洞 + 一处窗洞，配门套立柱/门楣/门牌/把手、窗框/玻璃/防盗栏、灯座/灯管。
  新增两条**结构门**（`Verify3D.CheckHoles`）：① 洞口中心不得仍落在任何墙体包围盒里
  （"没真开洞＝门框后面还是墙"，肉眼在暗光走廊里根本看不出来）；② 覆盖件必须在 (长×高) 平面上
  真盖得住洞口。这两条都是**坏跑里翻成反向期望**才证明有牙的。
- 净宽口径：聚合 `Renderer.bounds.size.x = 2.50` ⇒ 任何件不得越过 `|x| 1.25`（墙外表面），
  内表面在 `1.15`。`FLOOR_TOP = 0.10`（`gen_corridor.py:23`），所以贴地件摆 z=0.10 是**贴地不是悬空**。
- hide 代理从 13 条扩到 22 条，位置随新几何；异常侧与 base 侧同步靠名字指认，不靠引用。

**质感线（T1）**：三张 ambientCG CC0 资产换掉程序化占位，`Normal.jpg` 必须先把导入器
`textureType` 设成 `NormalMap` 并 `EnableKeyword("_NORMALMAP")`——`SetTexture` 不替你开关键词，
漏了就是"日志和资产断言全绿、画面却是平的"。

**杂物段（T2）——13 件 Poly Haven CC0 道具并入 `base`，共 15 个实例**：

- **许可证只能逐件从页面字符串证**：Poly Haven 的 `attribution` 端点是 404（试过了），
  所以 `fetch_props.sh` 的门槛是抓 `https://polyhaven.com/a/<id>` 页面上的 `"CC0 1.0 Universal"`，
  命中数 <1 直接跳过并判红；13 件逐件读数留在 `logs/props-license.txt`。暂存几何与 zip 全在
  `${TMPDIR:-/tmp}`，版本库里只有 `Prop_<slug>/{BaseColor,Normal}.jpg` 与 `PROPS_CREDITS.md`。
- **两个独立 oracle 自证口径**，不靠肉眼：官方 `polycount` 是三角面**精确数**（逐件等值断言）；
  索引里的 `dimensions` 是**毫米数组且轴序不保证一致**（`medical_box` 的高度落在第 3 槽），
  所以尺寸判据比的是**排序后的三元组**、容差 ±12%。取数工装 `tools/ph_prop_dims.mjs` → `logs/props-dims.txt`。
- **多材质资产要显式材质前缀**：灭火器只有 `body_/glass_/paper_` 三套分支，没有裸 `Diffuse`，
  所以取数表写成 `<slug>:<id>[:<前缀>]` 三段（实测仅它需要前缀）。
- 六个 Blender/MSYS 坑（`/tmp` 口径、`Object.copy()` 不收参数、导入不能"先清场"否则删掉整条走廊、
  `_Proto_` 泄漏门、`matrix_world` 陈旧导致 15 件全误报跨中线、`set -e` 下 `[ ] &&` 的返回码）
  记在 `blender/README.md` §5，此处不复述。
- **我自己写的门里有一条恒真假绿，是坏跑抓出来的**：通行带门的坏跑期望原本写成 `PropBaseNames.Length`，
  而 `clearLo` 在坏跑里翻成 3.0m 时，通行带的物理上限只有 `1.15+1.15=2.30 < 3.0` ⇒ `narrow` **恒等于**件数
  ⇒ 断言恒成立 ⇒ 这条门在坏跑里其实是**绿的**。第一次 `VERIFY_BAD=1` 的 26 项红里没有它才发现。
  改成 99 哨兵后坏跑 27 项、它带着"实际不合格 15"如实变红，绿跑 `narrow=0`／坏跑 `narrow=15` 两侧都有差分。
  教训与 §9.4 那条"量具不合格"同族：**对照型判据要先证两侧真有差分**，把期望写成"实际必然等于的那个数"就是装饰。

**出口读数**：三段验收绿跑 rc=0（`[fbx]`/`[asm]`/`[play]` 全 PASS，`Corridor(Clone)` 95 网格、
三桶 Tex 75/Flat 10/Glass 1、20 张 NormalMap 导入器、15 件杂物 tiling 保持 (1,1)）；
`VERIFY_BAD=1` 红跑 rc=1（fbx 27 / asm 25 / play 4 项逐条红）；`build.sh` rc=0，
zip 从 45.2MB 涨到 **62.6MB / 166 条目**；`logs/shots7` 五帧两两 DIFFER（相机与转向活）。
另修一处取证工装读数：`shot3d.ps1` 的 Esc 轮询上限 12s 太短，实测该玩家**关栈要 9s 左右**、
退出码是 `0xC0000005`（卸载期访问违例，先前就存在的残留在 §9.4 之外仍未解决），
原写法把"退出了但没来得及消失"报成 `FAILED`；上限已参数化为 `-QuitWaitMs`（默认 30000）。


### 9.6 M7 身高与可见腿脚（2026-10-18）

起因是用户两句裁定：**"现在的视角像是飘浮在走廊，套个基础人体模型"**，随后在三选一里选
**"第一人称＋可见腿脚"**（否掉"先只修身高"与"改第三人称"）。

- **飘的根因是胶囊默认值**：`CharacterController` 出厂序列化是 `height 2 / center (0,0,0)`，
  根因此落在**身体中点**而不是脚底，相机再叠 1.6 ⇒ 眼睛离地 **2.70m**（天花板才 3.10m）。
  `center.y = height/2` 之后根＝脚底，眼睛 1.75m。半径 0.5 不能缩：HUD 的 `planeDistance=0.4` 是按它推的。
- **身体是第二只 FBX**（`blender/gen_body.py` → `Content/3D/Body.fbx`，四件 `Leg_L/R` `Shoe_L/R`）。
  绝不并入 `Corridor.fbx`——那只的按名契约是走廊的，多四件会被"未登记 base 名"判红；
  走廊导出完 `read_factory_settings(use_empty=True)` 清场再建身体，两只文件共用同一个 `export_fbx(path)`。
  枢轴在**髋/踝**而不是脚底：运行期摆腿绕自身原点，原点落地会像圆规从地面扫。细节与四个坑见 `blender/README.md` §6。
- **摆腿与脚步声共用同一个"按位移推进"的相位**（`stride/stepDistance`）：慢走 1.6m/s、每 1.5m 落一步，
  `sin(π·相位)` 成形、`legBlend` 在停步后 ~0.2s 阻尼回中立。音画同相，所以不会出现"脚落地了腿还在前摆"。
- **两条取证陷阱（都是量具坏，不是游戏坏）**：
  ① `Input.GetAxis("Mouse Y")` 是**平滑**轴（sensitivity 0.1），合成 30px 的 dy 摊到每帧只有约 0.1°，
     读出来是"低头没反应、看不见腿"，而同一次跑里 700px 的 yaw 一直好用。改成三段各 250px 才打得到低头限位。
  ② 腿柱长在眼睛**正下方**时，0.7m 外半径 0.115 的大腿垂角张角 ±9°，把 1.6m 下的鞋整件挡死
     （`logs/shots9/08` 实测就是"两个圆饼"）。相机 `localPosition.z` 退到 **-0.30**（仍在半径 0.5 的胶囊内）
     后鞋落到眼前方 0.350m／垂角 77.6°，低头时腿有前后层次。
- **帧哈希在腿这件事上没有判据力**：FilmGrain 是时域噪声 + 6 盏灯在闪，任意两帧必然 DIFFER。
  "腿会不会摆"只能看帧（`logs/shots10/11_leg_b.png` 两腿反相 vs `14_leg_rest.png` 对称），
  工装注释里已把这条写明，避免下家把 DIFFER 当证据。
- **门禁**：`Verify3D.VerifyAssembly` 的 M7 六道（① 胶囊 `center=半高` ② 眼睛世界 y∈[1.6,1.8]
  ③ Player 子树腿脚件数＝名单数 ④ 身体件不得出现在走廊树 ⑤ `leftLeg/rightLeg` 已绑定
  ⑥ 鞋在眼前方水平 ≥0.25m）。六道在坏跑里各翻成反向期望且**两侧都有差分**
  （③ 用 99 哨兵、⑥ 翻成 `<=0.10`，都不是"实际必然等于的那个数"）。

**出口读数**：绿跑三段全 PASS，`[VERIFY] 腿脚与身高` 行实测
`眼睛世界 y=1.75 ; 鞋在眼前方 0.350m/垂角 77.6° ; 胶囊 height=1.80 radius=0.50 center.y=0.90`、
四件的 `mat=Trousers/Shoes` 与高度带（腿 0.19..1.05、鞋 0.10..0.22＝Blender 读数 +0.10 楼板顶）；
`VERIFY_BAD=1` 红跑 fbx 27 / **asm 31**（＝ M6 的 25 条 + M7 六条，一条不多一条不少）/ play 4；
`build.sh` rc=0、zip 仍 **62.6MB / 166 条目**；`logs/shots10` 的 06/07/08 三段低头 + 10–14 走路连拍
肉眼确认"腿读得出是腿、迈步反相、松键回中立"。
