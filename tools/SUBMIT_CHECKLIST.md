# 提交前检查清单（8h jam 最后 30 分钟过一遍）

## 构建前（图/音频更新后必做）
- [ ] `./tools/process_art.sh`（若有新图）
- [ ] `./tools/make_audio.sh`（若音频没生成过）
- [ ] `./tools/build.sh`（内部会重跑 Bootstrap，幂等）
- [ ] 双击 `builds/win64/Dreamcore.exe` 启动：标题 → 开始 → 玩一局到结局（好/坏都验一次）

## 玩法抽查（3 分钟）
- [ ] 前进/回头按钮判定正确（有异常按回头算对）
- [ ] 错误 3 次出坏结局；到达深度出好结局
- [ ] 图切换淡入淡出正常，无黑屏卡死
- [ ] 音量合适（环境音不压过事件音），无爆音

## itch.io / 主办方提交页
- [ ] 上传 `builds/win64/Dreamcore-win64.zip`
- [ ] 标题（中文名建议现场定，如《回家》《筒子楼》）
- [ ] 简介模板：
  > 8小时单人作品。中式梦核找异常：你在深夜的老居民楼里一层层往下走，
  > 觉得哪里不对劲，就回头。
- [ ] 标签：horror, short, dreamcore, psychological, singleplayer
- [ ] 封面图：游戏内画面截图（960×540 裁剪即可，风格自带滤镜）
- [ ] 截图 2-3 张：标题画面 + 游戏画面 + 结局画面
- [ ] 操作说明：鼠标点击「前进/回头」两个按钮
- [ ] 素材声明：全部图像 AI 生成 + 程序合成音频，无第三方资产

## 截图怎么截（Windows）
- 运行 exe，`Win+Shift+S` 框选截图；标题画面和游戏画面各一张

## 常见坑（实测）
- PlayerSettings.productName 是英文 "Dreamcore"，提交页自己写中文名即可
- 若改图后忘了重跑 build.sh，包里的图是旧的（catalog 不自动刷新）
- exe 依赖同目录的 Dreamcore_Data 文件夹，必须一起打包（build.sh 已处理）
- 梦核低清是风格，但 960×540 拉伸到 1080p 全屏会糊——属预期效果，别在最后 10 分钟"修复"它
- **URP 无参 Create() 陷阱（本模板已规避）**：`UniversalRenderPipelineAsset.Create()` 不带参时默认 Renderer Data 只在内存里，存盘后引用丢失 → 构建期 shader 剥离循环刷 "Default Renderer is missing" 日志暴涨（实测 64.7GB 卡死构建）。Bootstrap 已改为"先建独立 renderer 资产再 Create(rd)"，改模板时勿回退
- **Player.log 里的 "The referenced script on this Behaviour (Game Object '<null>') is missing!" 是 URP DebugUpdater 的无害噪音**（Debug 系统退出阶段自建物体，脚本在发布构建被剥离），不是你的脚本丢失；真正的脚本丢失会打印具体物体名

