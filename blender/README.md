# blender/ — 走廊建模与 FBX 导出契约

本目录是 **base 走廊几何 + 10 组异常变体的唯一真相**（headless Blender 5.2.2）。
产物 `dreamcore/Assets/Content/3D/Corridor.fbx` 由脚本生成，**不要手改**：改模型 = 改脚本重跑。
本文只写"下个会话必须知道才不会踩坑"的五件事，不复述 spec。

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
- **杂物（`gen_props.py`）走的是同一套 base 命名**：实例名 `Prop_<slug>`（单件）或 `Prop_<slug>__<n>`
  （同件多摆，`n` 从 1 起）。slug 里**不许出现下划线**，Unity 侧 `Bootstrap3D.SurfFor` 靠
  `IndexOf("__")` 切实例后缀、`Substring(5)` 取 slug 去查材质表；slug 带下划线会被切错、
  查不到材质 → 落进"未登记 base 名"那条门判红。`Prop_` 不匹配 wire 正则，所以它天然归 base 侧。

## 2. 当前名字清单

**别把数量抄进文档当判据**（本项目已经因为"25 个网格""objects=44"这类写死的数被坑过两轮）：
真实清单以 `logs/blender-build.log` 的 `[BUILD]` 行为准，验收侧的名单以
`dreamcore/Assets/_Project/Editor/Verify3D.cs` 里的 `CorridorBaseNames` / `PropBaseNames` /
`BaseEmptyNames` / `AnomShowNames` / `AnomHideNames` 为唯一真相，所有计数断言都从这些数组 `.Length` 派生。

```sh
grep -E '^\[BUILD\] (objects|wire_check|anom_ids|props|base_total)' logs/blender-build.log
```

三族名字的形状：

| 族 | 例 | 谁生成 |
|---|---|---|
| 走廊壳体与构件 | `Floor` `WallLeft` `WainscotR` `Door_3` `DoorPlate_9` `Lamp_6` `Poster_18` `MailboxBody` `WindowBuildings` | `gen_corridor.py` |
| 灯位锚点（EMPTY） | `Light_2` `Light_6` … `Light_22` | `gen_corridor.py` |
| 杂物 | `Prop_chair__1` `Prop_bench` `Prop_extinguisher` `Prop_clock` | `gen_props.py` |
| 异常 show / hide 代理 | `anom02_show_extraFigure` / `anom01_hide_Light_2_off` | `gen_anomalies.py` |


## 3. 轴映射实测（Task 6 Step 6，别按猜测改）

建模轴：长度沿 Blender `+X`、横向 `+Y`（走廊中线 0）、高度 `+Z`；走廊占 `x∈[0,24]`、`y∈[-1.2,1.2]`、`z∈[0,3.1]`。

**实测结论：`Bootstrap3D.FbxYaw = 90f`（不是 brief 默认猜的 `-90f`）。** 两次跑的实际读数：

```
FbxYaw=-90 → 聚合 size=(2.91, 3.10, 24.05) min=(-1.25, 0.00, -24.05) max=(1.66, 3.10, 0.00)   ← 长度最长但朝 -Z，玩家出门撞端墙
FbxYaw= 90 → 聚合 size=(2.91, 3.10, 24.05) min=(-1.66, 0.00, 0.00)  max=(1.25, 3.10, 24.05)   ← 通过
```

（`logs/verify-fbx-yaw.log` = -90 的探针跑，`logs/verify-fbx.log` = 90 的绿跑；聚合只用 `Renderer.bounds`。）

> 口径勘误（M3）：上面两次读数里的**宽 2.91** 是当时窗外楼板的包围盒露在墙外约 0.5m 撑出来的假宽度；
> 窗板收进墙体后真实净宽 2.50m（`logs/verify-asm.log` 实测 2.500003）。读数按当时的 FBX 原样保留不改，
> 轴映射与 `FbxYaw=90f` 的结论不受影响（那两条门查的是 `size.z` 与 `min.z/max.z`）。

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

**Task 7 裁定：走 (a) 认了，不修**（`FbxYaw` 是整个根的一次刚体旋转，壳/家具/灯位/异常件一起翻，
彼此不错位；灰盒已随 Task 7 删除）。因此 `WallLeft`/`WainscotL` 这类 L/R 名字在 90f 下
**与其实际所在的那面墙不符**（`WallLeft` 中心在 Unity x=+1.20），这是已知外观事实，
不是 bug；任何摆放一律按实测世界量，**禁止按名字语义推断它在哪面墙**。

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
产物与日志：`dreamcore/Assets/Content/3D/Corridor.fbx` 与 `Body.fbx`（两只，见 §6）、`blender/Corridor.blend`、
`logs/blender-build.log`、`logs/verify-fbx.log` / `logs/verify-fbx-bad.log`。

- 门只认 `[VERIFY] PASS|FAIL` 行；Unity 若在开日志前中止（许可证/工程锁），`rm -f` + `touch` marker +
  `[ "$VLOG" -nt "$MARKER" ]` 三件套会拒绝读陈旧日志（**别把它改掉**，那是假绿的来源）。
- `--python-exit-code 4` 必须放在 `--python` **之左（之前）**：放后面在 Blender 5.2.2 上会被忽略，
  Python 抛异常照样退 0，那道 `||` 门就是装饰。
- 环境变量覆写：`BLENDER_EXE`（Blender 可执行文件）、`CORRIDOR_BLEND`、`CORRIDOR_FBX_OUT`、
  `BODY_FBX_OUT`（腿脚那只，见 §6）、`PH_PROPS_DIR`（道具几何暂存目录，见 §5）。
- `Error: Not found: bpy.ops.export_scene.fbx` = 导出插件没启用；`objects` 数量对不上照
  `[BUILD] base_children` 逐项查。
- 验收口在 `dreamcore/Assets/_Project/Editor/Verify3D.cs::VerifyFbx()`（`-logFile` 读 `[VERIFY]`）。
  装配侧（`Bootstrap3D`）已经整只收养这只 FBX，灰盒 `GreyboxAnomalies` 早已删除。

## 5. 杂物段（`gen_props.py`）：第三方 CC0 道具并入 base

它不是"再建一个集合"，而是**把外部网格并进 `base` 后一起导出**，所以 §1 的命名契约、§3 的轴映射
与尺度口径对它全部适用。跑之前先 `./tools/fetch_props.sh`（它把几何暂存、贴图落 `Assets/Content/Textures/Prop_<slug>/`）。

- **暂存目录必须用 Windows 口径**：Git Bash 的 `$TMPDIR` 是 MSYS 的 `/tmp`（真身 `%LOCALAPPDATA%\Temp`），
  而 `blender.exe` 是 Windows 原生程序，把 `/tmp` 认成 `C:\tmp` → `RuntimeError: 缺道具几何 /tmp\ph-props\chair.fbx`。
  `_stage_dir()` 因此按候选链找（`PH_PROPS_DIR` → `%LOCALAPPDATA%\Temp\ph-props` → `$TMPDIR/ph-props` → `/tmp/ph-props`），
  第一个"存在且含 .fbx"的才算，全落空才报错。
- **绝不"先清场"再导入**：本脚本在 `gen_corridor.build()` 之后运行，`select_all + delete` 会把整条走廊删干净
  （实测过一次，FBX 只剩道具）。改成取导入前后的对象**差集**，只留 MESH、非 MESH 的临时空物体就地删除。
- **`Object.copy()` 在 Blender 5.2 不接受任何参数**：`proto.copy(data=...)` 直接 `TypeError`。
  写 `ob = proto.copy(); ob.data = proto.data.copy()`（网格也要拷，否则多实例共享同一份、改不动）。
- **原型件 `_Proto_<slug>` 用完必须删**，`build_scene.py` 里还有一道"泄漏门"兜底：它若留在场景里会跟着导进
  Corridor.fbx，被 Unity 侧"未登记 base 名"判红。
- **落位判据读 `matrix_world`，而它只在依赖图更新后才反映刚设的 `location`**：设完位置必须
  `bpy.context.view_layer.update()` 再取包围盒。漏这一句时 15 件全部误报"横跨中线（y -0.34..0.34）"
  ——那是**量具坏了不是摆放坏了**，别去放宽判据（本项目真差点这么干）。
- **尺寸与面数用两个独立 oracle 自证**：官方 `polycount` 是三角面精确数（`_check_tris` 逐件等值断言），
  索引里的 `dimensions` 是**毫米数组**且**轴序不保证一致**（`medical_box` 的高度落在第 3 槽），
  所以 `_check_size` 比的是**排序后的三元组**、容差 ±12%。口径来源 `tools/ph_prop_dims.mjs` → `logs/props-dims.txt`。
- **摆放的三条硬约束**（Unity 侧 `Verify3D.CheckProps` 用世界量再验一遍，两侧口径独立）：
  不跨中线、每件留 ≥1.05m 通行带、贴墙件不许糊在门洞/窗洞上。地面件 `z=FLOOR_TOP`（`gen_corridor.py:23`，=0.10）
  **是贴地不是悬空**——别按"地板在 0"去"修"它。墙上件（`Prop_clock`/`Prop_alarm`）的 `y=±1.10` 同理是
  贴着内表面 `1.15` 摆的。
- **道具用自己的 UV，不参与 world-scale tiling**：`Bootstrap3D.AddProp` 一律传 `tileMeters = 0f`，
  按米裁会把说明标签/表盘撕成两半。门在 `Verify3D` 装配段："15 件杂物的 `_BaseMap` tiling 应保持 (1,1)"。


## 6. 腿脚段（`gen_body.py`）：第二只 FBX，挂 Player 不挂走廊

M7 起因：用户裁定"第一人称＋可见腿脚"（`logs/shots7` 里眼睛离地 2.70m＝飘在走廊）。身体件**绝不进
`Corridor.fbx`** —— 那只文件的按名契约（§1）是走廊的，多四件 `Leg_*`/`Shoe_*` 会被 Unity 侧
"未登记 base 名"判红。所以 `build_scene.py` 导出走廊后 `wm.read_factory_settings(use_empty=True)`
清场，再建身体、导出 `dreamcore/Assets/Content/3D/Body.fbx`；两只文件共用同一个 `export_fbx(path)`，
导出参数不可能各改一半。

- **枢轴在髋/踝，不在脚底**：运行期摆腿是 `Quaternion.AngleAxis(角, 轴) * 基准` —— **绕自身原点**转。
  原点落在脚底的话，12° 的摆幅会像圆规一样从地面扫出去（脚不动、膝盖插进地里）。髋在
  `HIP_Z=0.95`、踝在 `ANKLE_Z=0.09`，鞋的原点=踝（鞋绕踝转才像迈步）。
- **自检读 `matrix_world.translation`，别读 `ob.location`**：`location` 是 `matrix_basis`，一旦
  `ob.parent = ...` + `matrix_parent_inverse` 就位，它就不再是世界位置。第一版拿 `location` 断言枢轴，
  四件全红。
- **轴映射**：Blender `+X`＝前 → Unity `+Z`（鞋尖朝前）；Blender `+Y`＝横向 → Unity `−X`
  （`Leg_L` 在 `+y=0.14`，装配后世界 `x=[-0.26,-0.02]`，左右是反的，别按 Unity 的 `+x` 找左腿）。
- **鞋必须是不对称的**：`hi.x < 0.15` 直接抛错（"鞋退化成对称方块"）。这条是用户那句"那两个方块太抽象了"
  留在工装里的哨兵——鞋尖超出踝 0.205m、鞋跟退 0.095m，俯视才读得出是鞋。
- 门：`tools/blender_build.sh` 三条 `[BUILD]` 读数（`body_parts=4` / `body_names=[...]` / `Body.fbx` 落盘）；
  Unity 侧 `Verify3D.VerifyAssembly` 的 M7 六道门（件数、不在走廊树、`leftLeg/rightLeg` 绑定、
  胶囊 `center=半高`、眼睛世界高度、**鞋在眼前方有水平余量**）。
