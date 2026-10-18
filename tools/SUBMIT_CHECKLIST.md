# 提交前检查清单（8h jam 最后 30 分钟过一遍）

出货轨道只有一条：**真 3D**（2D 保底已于 2026-10-17 由用户裁定废弃并从仓库删除）。

## 构建前（改过几何/贴图/音频后必做）
- [ ] `./tools/blender_build.sh`（改过 `blender/gen_corridor.py` / `gen_anomalies.py` / `gen_props.py` /
      `gen_body.py` 才需要；headless 重导**两只** FBX——`Corridor.fbx`（base 走廊 + 10 组异常 + 15 件杂物）
      与 `Body.fbx`（腿脚四件，挂 Player 不挂走廊）并跑 wire 契约验收）
- [ ] `./tools/fetch_textures.sh`（若 `Assets/Content/Textures/*/BaseColor.jpg` 不在；走网络取 ambientCG CC0，
      zip 落在 `${TMPDIR:-/tmp}/acg-zips`、不进版本库。缺它不会编译失败，但材质会变刺眼白，
      `logs/bootstrap3d.log` 里打 `[Bootstrap3D] 缺贴图 ...，先跑 ./tools/fetch_textures.sh` 的 LogError）
- [ ] `./tools/fetch_props.sh`（若 `Assets/Content/Textures/Prop_*/` 不在；Poly Haven 13 件 CC0 道具，
      **先逐件核实 `https://polyhaven.com/a/<id>` 页面上有 "CC0 1.0 Universal" 才下载**，命中数 <1 直接跳过并判红。
      几何暂存 `${TMPDIR:-/tmp}/ph-props/<slug>.fbx`、不进版本库；贴图落 `Prop_<slug>/{BaseColor,Normal}.jpg`；
      出处清单 `Assets/Content/Textures/PROPS_CREDITS.md`）
- [ ] `./tools/make_audio.sh`（若 `Assets/_Project/Audio/*.wav` 不在；ffmpeg 纯合成 4 条）
- [ ] `./tools/build.sh`（内部会重跑 Bootstrap3D 重建 Main3D 场景，幂等）
- [ ] 三段验收：`./tools/unity_verify.sh`（绿跑）＋ `VERIFY_BAD=1 ./tools/unity_verify.sh`（坏样本必须逐条红）
- [ ] 实机冒烟：`powershell -File tools/shot3d.ps1 -Exe <builds\win64-3d\Dreamcore.exe> -OutDir logs\shots`
      —— 它启动构建包、合成 WASD/鼠标（含三段低头 06/07/08 与走路连拍 10–14）、逐帧存 PNG 并比对帧哈希，
      跑完自己杀进程

## 玩法抽查（3 分钟，用上面那批截图看）
- [ ] 双击进标题页《昨日的明天》（第一行给按键、第二行给玩法），点"开 始"或按 Enter/Space 进走廊，HUD 左上角显示"地下 N 层 / 异常记录 x/3"
- [ ] W 前进、S 后退、鼠标转向；走到走廊**两端的判定门**才结算一层：有异常时回头（转身走回起点方向）算对
- [ ] 错 3 次出坏结局；到底出好结局（结局文案打在屏幕中央，游戏不弹菜单）
- [ ] 6 盏声控灯会闪；窗外板在正常态是淡蓝灰、anom10 时压成全黑
- [ ] 后处理在帧上看得见：灯有辉光、四角暗角、墙面有颗粒（`logs/shots3/crop02.jpg` 为证）
- [ ] 音量：环境底噪不压过事件音；走路有脚步声（每 1.5m 落一步）
- [ ] 身高与腿脚（M7 加）：低头能看到**两条腿**（锥形裤腿＋鞋口），不是"两个圆饼"、也不是空地板；
      走动时两腿反相摆动、松开 WASD 后回到对称站姿。取证帧 `logs/shots10/07_look_down_b.png`（腿有层次）、
      `11_leg_b.png`（迈步中，两腿不对称）、`14_leg_rest.png`（静止，回到对称）。
      **帧哈希在这里没有判据力**：FilmGrain 是时域噪声、6 盏灯在闪，任意两帧必然不同——只有眼睛看得出来
      （`logs/verify-asm.log` 的 `[VERIFY] 腿脚与身高` 行给数字口径：眼睛 y=1.75、鞋在眼前方 0.350m/垂角 77.6°）
- [ ] 杂物不挡道（M6 杂物段起加）：15 件道具都贴墙或成对靠墙，走一趟不被卡住、
      没有物件横在走廊正中；`logs/verify-fbx.log` 的 `[VERIFY] 杂物 <名> ... 通行带=X.XXm` 逐件可读，
      下限 1.05m，四条形如"缺席/跨中线/通行带不足/糊住门窗洞"的门全 ok
- [ ] Esc 退出（构建版锁了光标，没这条玩家只能 Alt+F4）——`shot3d.ps1` 末尾会真按一次 Esc 并打
      `ESC quit: OK (9000 ms, exit code -1073741819)` / `FAILED`，不用手测。
      **两个读数陷阱**：① 退出码是 `0xC0000005`（卸载期访问违例，Mono + d3d12 关栈时崩，不影响已玩到的部分），
      别把它读成"退出失败"；② 关栈要 9s 左右，`-QuitWaitMs` 默认 30000，
      真超时了先去看 `player.log` 里有没有 `[Physics::Module] Cleanup current backend` +
      `Input System module state changed to: Shutdown`——有就是"退出了但没来得及消失"，不是绑定坏了

## itch.io / 主办方提交页
- [ ] 上传 `builds/win64-3d/Dreamcore-win64.zip`（M8 标题页接入后约 62.6MB / 169 条目，
      `zip_release.ps1` 会回读压缩包自检，
      缺 `UnityPlayer.dll` 直接 exit 1，不会静默出一个双击没反应的包）
- [ ] 标题：《昨日的明天》（已定，Title.unity 标题页同款）
- [ ] 简介模板：
  > 8小时单人作品。中式梦核找异常：你在深夜的老居民楼里一层层往下走，
  > 觉得哪里不对劲，就回头。
- [ ] 标签：horror, short, dreamcore, psychological, singleplayer
- [ ] 封面图：`logs/shots3/crop02.jpg`（1280×720 实机构图，走廊纵深＋灯辉光）
- [ ] 截图 2-3 张：`logs/shots7/01_start.png`（开局，含杂物）＋ `logs/shots7/02_after_walk.png`（走一层后）＋
      `logs/captures/anom*_after.png` 里挑一张异常态（这批是 640×360 编辑态取证图，只适合当"设计说明"配图）
- [ ] 操作说明：**标题页点"开 始"或按 Enter/Space 开局；WASD 移动 + 鼠标转向，走到走廊两端判定；Esc 退出**
- [ ] 素材声明（**M7 腿脚段后口径，2026-10-18**）：走廊壳体/门窗/灯具/10 组异常、以及玩家身体（腿脚四件，
      `blender/gen_body.py` → `Body.fbx`）均为 Blender 自建并程序化导出；
      **道具是第三方 CC0 网格**（Poly Haven 13 件，逐件在 `https://polyhaven.com/a/<id>` 核实页面上写着
      "CC0 1.0 Universal" 才下载，命中数逐件登记在 `logs/props-license.txt`，出处清单
      `dreamcore/Assets/Content/Textures/PROPS_CREDITS.md`）；贴图为 **ambientCG 的 CC0 PBR 贴图**
      （见 `dreamcore/Assets/Content/Textures/CREDITS.md`）+ Poly Haven 随件 1K 贴图；
      音频为 ffmpeg 程序合成。CC0 1.0 无署名义务、可商用，**但"零第三方资产"这句不成立，别照抄**。
      ~~全部图像 AI 生成 + 程序合成音频，无第三方资产~~（该句属 2D 轨道，已不成立，勿照抄）

## 常见坑（实测）
- PlayerSettings.productName 是英文 "Dreamcore"，提交页自己写中文名即可
- **一个 Windows 构建包 ≠ exe + _Data**：还要同目录的 `UnityPlayer.dll`，Mono 脚本后端另需 `MonoBleedingEdge/`。
  少任何一个都是"包能解压、双击没反应、也没有报错窗口"。`zip_release.ps1` 现在收齐这四项并回读压缩包断言
- 改了 `blender/*.py` 却只跑 `build.sh`：Bootstrap 会重装配场景，但用的是**旧 FBX**，几何改动看不见
- 脚本后端实测是 **Mono**（产物里有 `Dreamcore_Data/Managed/*.dll`），不是 IL2CPP；别按 IL2CPP 的口径写声明
- **`shot3d.ps1` 里鼠标 dy 必须和 dx 同量级（M7 实测）**：`Input.GetAxis("Mouse Y")` 是**平滑**轴
  （`ProjectSettings/InputManager.asset` 里 sensitivity 0.1），一次 30px 的合成移动摊到每帧只有约 0.1°，
  读出来是"低头没反应、腿看不见"——那是**量具坏了**，不是玩家按不动（同一次跑里 700px 的 yaw 一直好用）。
  现在分三段各 250px 打到低头限位。别把小 dy 的读数写进结论
- **眼睛正对着腿柱＝低头只看见两个大腿截面**（`logs/shots9/08` 就是这样）：腿在眼睛正下方时，
  0.7m 外半径 0.115 的大腿垂角张角 ±9°，把 1.6m 下的鞋整件挡死。相机 `localPosition.z` 退到 -0.30
  后鞋落到眼前方 0.350m/垂角 77.6° 才错开。门在 `Verify3D` M7 第⑥道（坏跑期望翻成 `<=0.10m`）
- player.log 里这两类是**无害噪音**，别在最后 10 分钟去"修"它：
  `Failed to load 'AMDUnityPlugin'/'NVUnityPlugin', expected x64 architecture`（帧率插件按平台架构找 dll）
  与 `d3d12: failed to query info queue interface (0x80004002)`（驱动不给查询队列，回退即可）
- **URP 无参 Create() 陷阱（本模板已规避）**：`UniversalRenderPipelineAsset.Create()` 不带参时默认 Renderer Data 只在内存里，存盘后引用丢失 → 构建期 shader 剥离循环刷 "Default Renderer is missing" 日志暴涨（实测 64.7GB 卡死构建）。Bootstrap 已改为"先建独立 renderer 资产再 Create(rd)"，改模板时勿回退
- **`VolumeProfile.Add<T>()` 是同族坑**：它只 `CreateInstance` + 塞列表，不做 `AssetDatabase.AddObjectToAsset`，
  存出来的资产是 `components: [{fileID: 0} x5]`——编辑器里像有 override，构建产物里全是 null。
  梦核滤镜必须走 `Bootstrap3D.GetOrAddOverride`（Add 完立刻挂子资产），门在 `Verify3D` 的"5 道 override + 强度>0"
- **Player.log 里的 "The referenced script on this Behaviour (Game Object '<null>') is missing!" 是 URP DebugUpdater 的无害噪音**（Debug 系统退出阶段自建物体，脚本在发布构建被剥离），不是你的脚本丢失；真正的脚本丢失会打印具体物体名
- ~~梦核低清是风格，但 960×540 拉伸到 1080p 会糊~~（2D 轨道的说法；3D 是原生分辨率 + 后处理滤镜，没有低清拉伸这回事）
