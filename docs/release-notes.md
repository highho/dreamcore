8 小时单人 gamejam 提交版。

**下载 `Dreamcore-win64.zip`，完整解压后双击 `Dreamcore.exe`。**（不要直接在压缩包里双击 exe。）

- 平台：Windows x64（DirectX 12 / Mono 脚本后端）
- 时长：一局 3–5 分钟，8 层，10 组异常，2 种结局
- 操作：标题页点「开 始」或按 `Enter` / `Space` 开局；`WASD` 移动 + 鼠标转向；走到走廊**两端的门**结算一层；`Esc` 退出

玩法一句话：**有异常时回头算对，干净时前进算对。** 第 1 层永远是干净的——先让你记住"正常长什么样"。

素材：走廊与 10 组异常由 Blender Python 脚本程序化自建；道具为 Poly Haven 13 件 CC0 网格，贴图为 ambientCG / Poly Haven CC0；音频为 ffmpeg 程序合成。全部第三方资产均为 CC0 1.0 Universal。

已知局限：仅 Windows；无暂停菜单与设置页；异常目录固定 10 组；退出时可能报 `0xC0000005`（Mono + d3d12 卸载期访问违例，不影响游玩）。

构建与验收方式见仓库 README 与 `docs/ARCHITECTURE.md`。
