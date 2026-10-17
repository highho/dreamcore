# blender/ — 走廊建模与 FBX 导出契约

本目录是 **base 走廊几何 + 10 组异常变体的唯一真相**（headless Blender 5.2.2）。
产物 `dreamcore/Assets/Content/3D/Corridor.fbx` 由脚本生成，**不要手改**：改模型 = 改脚本重跑。
本文只写"下个会话必须知道才不会踩坑"的四件事，不复述 spec。

## 1. 命名契约：前缀式 wire 名，集合不导出

实测两遍（Task 1，spec §9.1）：**Blender 集合永远不会进 FBX** —— `bpy.ops.export_scene.fbx`
的 43 个参数里没有任何"集合导出为节点"的开关，导出的 FBX 里 grep 集合名零命中。
所以契约名必须是**对象自身的名**，Unity 侧 `AnomalyResolver`（`Assets/_Project/Scripts/Data/AnomalyResolver.cs`）
递归全树**只按名分桶**，语法 `^anom(\d{2})_(show|hide)_(.+)$`：

| 形态 | 含义 | 例 |
|---|---|---|
| `裸名` | base 恒定本体，永不匹配 wire 语法 | `Floor` / `Door_3` / `Light_2` |
| `anom<NN>_show_<leaf>` | 该组异常激活时**出现**的网格 | `anom02_show_extraFigure` |
| `anom<NN>_hide_<base目标名>_off` | 该组异常激活时**消失**的指认代理（EMPTY）；剥掉尾部 `_off` 后按名指认 base 里的目标 | `anom01_hide_Light_2_off` → `Light_2` |

- `gen_corridor.py` / `gen_anomalies.py` 里的 `coll("base")` / `coll("01")` / `coll("show")` / `coll("hide")`
  **只作本侧编排容器，导出后不存在**，任何下游读取（含验收断言）都不得依赖它们。
  已知副作用：`coll()` 按名 get-or-create，所以 10 个组共用同一个 `show`/`hide` 集合（多父级链接）——
  不影响 FBX（对象各导出一次），但**别指望"某个组的 show 里只有这个组的东西"**；真要把组拆开，
  把叶子集合改名成 `show_<NN>`/`hide_<NN>` 即可（一处改动）。
- `hide` 代理自己在 Unity 侧被 `Resolve()` 关掉，永不显示；指认不到 base 目标时解析器发
  `[Resolver] hide 指认不到 base 目标` 警告并丢弃该条（不静默）。`tools/blender_build.sh` 把这条
  警告当作判红条件，改名后重导即可。
- `_open` 只是 Swap 约定下的艺术名（`anom03_show_Door_3_open` 替换 base 的 `Door_3`），
  解析器不认 `_open`，随艺师怎么叫都行，但**不许与 base 里的名字同名**。
- base 与 anom 的二分**只能调 `AnomalyResolver.IsAnomalyWireName(name)`**（Task 7 的装配、Task 8 的
  验收都用它），**不许再复制那份正则**。

## 2. 当前名字清单（照 `logs/blender-build.log` 的 `[BUILD]` 原样粘）

`objects=44`：base 22 + anom 22（`wire_check=ok show=9 hide=13 hide_targets_ok=13 malformed=0`）。

**base**（`Floor WallLeft WallRight Ceiling EndWall WainscotL WainscotR` 七面 + 9 个可指认本体 + 6 盏灯位 EMPTY）：

```
Floor, WallLeft, WallRight, Ceiling, EndWall, WainscotL, WainscotR,
Door_3, Door_9, Poster_6, Poster_10, Poster_18,
MailboxBody, MailboxDoor, BikeUp, WindowBuildings,
Light_2, Light_6, Light_10, Light_14, Light_18, Light_22
```

`Door_9` / `MailboxBody` 是纯装饰（无人指认、也不匹配 wire 语法），所以 Task 5 灰盒里没有它们，
不影响 Task 7 整只替换。`Light_*` 六个是 **EMPTY 灯位锚点**（Task 7 在此挂点光）。

**anom**（10 组，与 `AnomalyTitles` 的 10 个 id 一一对应；22 个引用 = 9 show + 13 hide，与灰盒逐字同集）：

| 组 | show（网格） | hide 代理（EMPTY，剥 `_off` 即 base 目标） |
|---|---|---|
| anom01 声控灯全灭 | `anom01_show_emergencyLight` | `anom01_hide_Light_2_off` `_6_` `_10_` `_14_` `_18_` `_22_`（6 个） |
| anom02 尽头多一个人影 | `anom02_show_extraFigure` | — |
| anom03 防盗门半开 | `anom03_show_Door_3_open` | `anom03_hide_Door_3_off` |
| anom04 小广告整排消失 | —（纯 hide 组） | `anom04_hide_Poster_6_off` `_Poster_10_off` `_Poster_18_off` |
| anom05 一双旧布鞋 | `anom05_show_Shoes` | — |
| anom06 自行车倒了 | `anom06_show_BikeUp_open` | `anom06_hide_BikeUp_off` |
| anom07 信报箱门敞开 | `anom07_show_MailboxDoor_open` | `anom07_hide_MailboxDoor_off` |
| anom08 门缝渗水 | `anom08_show_WaterPuddle` | — |
| anom09 天花板垂红电线 | `anom09_show_RedWire` | — |
| anom10 窗外全黑 | `anom10_show_WindowBuildings_open` | `anom10_hide_WindowBuildings_off` |

## 3. 轴映射实测（Task 6 Step 6，别按猜测改）

建模轴：长度沿 Blender `+X`、横向 `+Y`（走廊中线 0）、高度 `+Z`；走廊占 `x∈[0,24]`、`y∈[-1.2,1.2]`、`z∈[0,3.1]`。

**实测结论：`Bootstrap3D.FbxYaw = 90f`（不是 brief 默认猜的 `-90f`）。** 两次跑的实际读数：

```
FbxYaw=-90 → 聚合 size=(2.91, 3.10, 24.05) min=(-1.25, 0.00, -24.05) max=(1.66, 3.10, 0.00)   ← 长度最长但朝 -Z，玩家出门撞端墙
FbxYaw= 90 → 聚合 size=(2.91, 3.10, 24.05) min=(-1.66, 0.00, 0.00)  max=(1.25, 3.10, 24.05)   ← 通过
```

（`logs/verify-fbx-yaw.log` = -90 的探针跑，`logs/verify-fbx.log` = 90 的绿跑；聚合只用 `Renderer.bounds`。）

由此定下的事实：**导入器本身做了 `Blender (X,Y,Z) → Unity (-X, Z, -Y)`**（长度与横向各翻一次号，高度对），
再叠加根上的 `FbxYaw` 绕 Y 旋转。所以单个 Y 旋钮只能满足两个约束之一：

| `FbxYaw` | 长度方向 | 横向 |
|---|---|---|
| `-90f` | ✗ 走廊往 -Z 延伸（端墙落在 z=-24） | ✓ `WallLeft` 在 Unity -X |
| `90f`（选定） | ✓ `z∈[0,24.05]`、端墙在 +Z | ✗ **Blender +Y 那侧落到 Unity -X** |

**已知代价（选 90f 换来的，交给 Task 7/8/10 决定要不要修）**：`FbxYaw` 是唯一旋钮，
禁止"我在 Unity 里再转一次"（那是转两次）。当前 FBX 里
`Door_3`/`Door_9`/`Poster_6`/`Poster_18`/`anom01_show_emergencyLight`… 这些 Blender `side<0` 的物件
落在 Unity **+X**（实测 `Door_3` world=(1.14, 1.05, 3.40)、`WallLeft` 中心 x=+1.20），
与 Task 5 灰盒的左右分配**对调**。两个单旋钮修法（各只改一处）：
(a) 认了——灰盒的左右本来只是占位，走廊两端对称、玩法不受影响；
(b) 在 `gen_corridor.py`/`gen_anomalies.py` 里把 `box()/empty()` 的 `side` 分量整体取反后重导。

**尺度口径（spec §9.2，本任务的逐节点读数再次证实）**：FBX 导入节点上**只有世界量是米**。
- `lossyScale=(100,100,100)` 对 25 个 MESH 与 19 个 EMPTY **全都成立**（含 `Light_*` 灯位与 13 个 `_off` 代理）
  → 100× 是这条导出→导入通道的性质，与 Empty 无关；资产根 `Corridor` 自身 `lossyScale=(1,1,1)`。
- 非对称网格复核（Task 1 交下来的）：`Mesh.bounds.size × lossyScale` 得到的**数值对但轴不对**——
  `Floor` 的 `meshBoundsSize=(0.24, 0.02, 0.00)`×100=(24, 2.4, 0.1)，而世界读数是
  `rendBounds=(2.40, 0.10, 24.00)`；`EndWall` 同理（局部 (0.1,2.4,3.0) vs 世界 (2.40,3.00,0.10)）。
  → 任何按 `Mesh.bounds` 的**某个分量**当某个世界轴尺寸用的代码都会错，一律用 `Renderer.bounds`。
- 层级实测扁平：`父级是 EMPTY 的节点=0`（45 个节点 = 根 + 44 直接子节点）。
  **所以"Empty 当父级会不会污染子级"这条在本 FBX 里结构上不存在**，别拿本次绿跑当它的证明。
  但 `Light_*` 这些 EMPTY 自身带 `lossyScale=100`：Task 7 若把点光 `SetParent` 到灯位锚下，
  子级的 `localScale`/`localPosition` 会被这 100× 污染 —— 用世界量摆（`position=`/`worldPositionStays`），
  或读锚点后脱离父子关系。
- 直接子节点的 `localPosition` 在本层级里读起来**是**米（父级=根、根 lossyScale=1，如 `Light_2`
  local=(-2, 2.75, 0) → world=(0, 2.75, 2)）；一旦祖先被缩放（Task 10 的 holder 之类）就立刻不是。

## 4. 用法与退出码

```sh
cd /g/AIgame/jam && ./tools/blender_build.sh            # 建模+导出 → Unity 层级断言，期望 PASS
cd /g/AIgame/jam && VERIFY_BAD=1 ./tools/blender_build.sh  # 同一道门喂坏样本，期望 FAIL
```

退出码 **0 = 三个条件同时成立**：FBX 落盘 + Blender 日志无 `Error:` + `[VERIFY]` 结论符合期望
（默认 PASS，`VERIFY_BAD=1` 翻成 FAIL）。任一不满足退 1 并打 `!!` 开头的因由。
产物与日志：`dreamcore/Assets/Content/3D/Corridor.fbx`、`blender/Corridor.blend`、
`logs/blender-build.log`、`logs/verify-fbx.log` / `logs/verify-fbx-bad.log`。

- 门只认 `[VERIFY] PASS|FAIL` 行；Unity 若在开日志前中止（许可证/工程锁），`rm -f` + `touch` marker +
  `[ "$VLOG" -nt "$MARKER" ]` 三件套会拒绝读陈旧日志（**别把它改掉**，那是假绿的来源）。
- `--python-exit-code 4` 必须放在 `--python` **之左（之前）**：放后面在 Blender 5.2.2 上会被忽略，
  Python 抛异常照样退 0，那道 `||` 门就是装饰。
- 环境变量覆写：`BLENDER_EXE`（Blender 可执行文件）、`CORRIDOR_BLEND`、`CORRIDOR_FBX_OUT`。
- `Error: Not found: bpy.ops.export_scene.fbx` = 导出插件没启用；`objects` 数量对不上照
  `[BUILD] base_children` 逐项查。
- 验收口在 `dreamcore/Assets/_Project/Editor/Verify3D.cs::VerifyFbx()`（`-logFile` 读 `[VERIFY]`）。
  本任务**不动装配**：`Bootstrap3D` 仍用 `GreyboxAnomalies` 灰盒，切 FBX 是 Task 7。
