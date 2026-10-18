# 提交页文案（逐字段可直接粘贴）

提交截止 **2026-10-18 15:00**，每支队伍只能提交一次、提交后不可修改。
本文件把提交页每个字段的最终文案固定下来，粘贴前对照最后一节的核查清单。

---

## 作品名称

```
昨日的明天
```

## 一句话简介

```
8 小时单人作品。中式梦核找异常：你在深夜的老居民楼里一层层往下走，觉得哪里不对劲，就回头。
```

## 代码仓库

```
https://github.com/highho/dreamcore
```

## 演示地址

```
https://github.com/highho/dreamcore/releases/tag/v1.0-jam
```

直链（如果表单要的是可下载文件本体而不是页面）：

```
https://github.com/highho/dreamcore/releases/download/v1.0-jam/Dreamcore-win64.zip
```

## 封面图链接

```
https://raw.githubusercontent.com/highho/dreamcore/main/docs/images/cover.png
```

1600×900，实机构图裁切 + 标题压字。备用（无标题版，适合主办方自己排封面）：
`.../main/docs/images/shot-corridor.png`

## 详细说明

> 下面这段整体粘贴。

```
【一句话】
中式梦核找异常：你在深夜的老居民楼里一层层往下走，觉得哪里不对劲，就回头。

【内核】
昨日想象中的明天已经过期。场景取千禧年前后中国城镇的居民楼公共走廊——水磨石地面、
绿漆墙裙、声控灯、信报箱、防盗门、开锁疏通的小广告。这些空间在 2000 年前后是"明天"，
今天回头看，那个明天从来没有来过。

【玩法】
一条 24 米的直线走廊反复走，每次"下一层"看到的是同一条走廊，差异只来自异常。
· 第 1 层永远是干净的 —— 先让你记住"正常长什么样"，之后才谈得上"不对劲"
· 判定只发生在走廊两端：走到尽头 = 前进，走回起点 = 回头
· 规则一句话：有异常时回头为对，干净时前进为对
· 8 层到底就出来，错 3 次出局，两种结局
· 10 组异常：声控灯全灭只剩绿色应急光 / 尽头多出背对镜头的人影 / 一户防盗门半开 /
  开锁疏通小广告整排消失 / 地上多出一双小孩旧布鞋 / 靠墙的旧自行车倒了 /
  信报箱小门全部敞开 / 门缝下渗出一滩水 / 天花板垂下一段红电线 / 窗外该有楼的地方全黑
一局 3–5 分钟。

【技术栈】
Unity 6.3 LTS + URP 17.0.1（零新增包）· Blender 5.2.2 + Python 程序化建模 ·
legacy Input Manager + CharacterController · DirectX 12 / Mono 脚本后端 ·
Windows x64 · AI 辅助开发（代码、着色器、工装脚本、取证自动化）

【架构：除了两只 FBX 和贴图，场景与材质全是代码】
build_scene.py（1,008 行 Python）headless 导出 Corridor.fbx 与 Body.fbx →
Bootstrap3D.cs（843 行）从 FBX + 贴图 + 音频幂等重建 Main3D.unity 与 Title.unity →
运行时 9 个脚本共 660 行 → Verify3D.cs（1,801 行）三段 batchmode 门禁 → 62.6MB 出货包。
场景文件是产物不是源文件，所以它能被删掉重建，也能被机器判定建对没有。
8 小时预算下，能重建的东西才敢改。

【两个值得一提的设计】
1) 前缀式 wire 契约：异常分组不靠层级、不靠集合、不靠元数据，只靠物体名字的字符串语法
   跨进程传递（^anom(\d{2})_(show|hide)_(.+)$）。原因是实测发现 Blender 集合完全不进 FBX ——
   对导出产物严格 grep 集合名 0 命中，5.2.2 的 43 个 fbx 导出参数里没有"集合导出为节点"开关。
   命名约定只有 AnomalyResolver.cs 一个文件知道，六处消费方一律调同一个判定函数。
2) FBX 里只有世界量是米：该通道把节点 baked 成 lossyScale=100 + 顶点预除 100，
   所以 localScale、Mesh.bounds、BoxCollider.size 都不是米，只有 Renderer.bounds 是。
   所有几何断言必须走 bounds。走廊实测口径 宽 2.50m × 高 3.10m × 长 24.05m。

【质量：门禁代码是游戏代码的 4.2 倍】
游戏运行时 660 行，编辑态装配与门禁 2,767 行，产线与取证脚本 1,176 行。
三段 batchmode 门禁（建模 / 装配 / 玩法），判据计数一律从名单 .Length 派生、禁魔数；
VERIFY_BAD=1 把期望值翻成物理不可能的数，让每条门各成一红，证明门真的会失败。
四支实机取证工装跑真构建包：真窗口 GDI 截图 + SendInput 合成键鼠，逐帧 md5 与 luma 双判据。
10 组异常的可辨识性 10/10 是这么量出来的（逐组 before/after 帧哈希必须 differs）。

【诚实登记：量具的局限】
· 帧哈希对"腿会不会摆"没有判据力 —— FilmGrain 是时域噪声、6 盏灯在闪，任意两帧必然不同
· 中心/四角亮度比 1.202 > 1，读数是暗端墙面和内容给的，不是滤镜给的，不能当暗角证据
· 编辑态取证不等于出货画面：batchmode 进不了 Play Mode，灯不闪、后处理求值不确定
· Mouse Y 是平滑轴，30px 合成移动读成"低头没反应"，那是量具坏了不是玩家按不动
· ESC 退出码 0xC0000005 不等于退出失败（Mono + d3d12 卸载期访问违例，关栈要 9s）

【素材与许可】
"零第三方资产"这句不成立。准确口径：
· 自建：走廊壳体与真开洞门窗灯具、10 组异常、玩家腿脚四件 —— 全部 Blender Python 程序化导出
· 第三方 CC0：道具网格 13 件（Poly Haven，13 件资产 → 15 个摆放实例）；
  墙面地面 PBR 贴图 7 套（ambientCG）；道具随件贴图 13 套（Poly Haven）
· 音频 4 条：ffmpeg 程序合成（50Hz 市电嗡鸣 + 粉噪 / 白噪磁带咔哒 / 45Hz 异常闷响 / 棕噪脚步），
  没有一条是采样
全部第三方资产均为 CC0 1.0 Universal（无署名义务、可商用），逐件在资产页核实
"CC0 1.0 Universal" 字样才下载。红线：仅 CC0，CC-BY 不接受。
出处清单：dreamcore/Assets/Content/Textures/CREDITS.md 与 PROPS_CREDITS.md

【运行方式】
下载 Dreamcore-win64.zip → 完整解压 → 双击 Dreamcore.exe。
必须先解压：一个 Windows 构建包 ≠ exe + _Data，还要同目录的 UnityPlayer.dll 与 MonoBleedingEdge/。
操作：标题页点「开 始」或按 Enter / Space 开局；WASD 移动 + 鼠标转向；
走到走廊两端的门结算一层；Esc 退出。
环境：Windows x64，DirectX 12，无需安装。

【已知局限】
只有 Windows（无 macOS / Linux / WebGL）；无暂停菜单、无设置页、无音量控制、无存档；
异常目录固定 10 组，多局之间只换顺序与干净层分布，重玩性有限；无配音、无音乐；
低多边形与程序化几何是预算结果，不全是风格选择。

【过程记录】
23 个提交，从 2D 保底选型到真 3D 轨道的一次整体改判都留在历史里。
设计文档与实施计划在 docs/superpowers/，架构详解在 docs/ARCHITECTURE.md，
提交前检查清单（含全部实测坑）在 tools/SUBMIT_CHECKLIST.md。
```

## 标签

```
horror, short, dreamcore, psychological, singleplayer
```

---

## 粘贴前核查清单

- [ ] 作品名称写中文《昨日的明天》即可 —— `PlayerSettings.productName` 是英文 "Dreamcore"，两者不必强求一致
- [ ] 演示地址打开后能看到 Release 页与 `Dreamcore-win64.zip`（62.6MB / 169 条目），资产字节数应为 **65,638,810**
- [ ] 封面图链接在**未登录**状态下能直接出图（仓库是 public，raw 域名可达）
- [ ] 简介与详细说明里没有出现"零第三方资产""全部图像 AI 生成""IL2CPP"这三句 —— 前两句属已废弃的 2D 轨道，第三句与产物实证不符
- [ ] 提交后不可修改：确认"只能提交一次"这条已读
