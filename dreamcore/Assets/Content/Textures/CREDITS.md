# 贴图出处（全部 CC0）

来源：[ambientCG](https://ambientcg.com/) —— 所有素材以 **CC0 1.0 Universal** 发布，
可商用、无需署名（此处署名仅为礼貌与可追溯）。本文件由 `./tools/fetch_textures.sh` 生成。

| 目录 | ambientCG 资产 | 本项目用途 |
|---|---|---|
| Terrazzo002 | Terrazzo002 (CC0) | 水磨石地面 Floor |
| Plaster007 | Plaster007 (CC0) | 石灰白墙 WallLeft/WallRight、天花板 Ceiling、端墙 EndWall、白漆窗框/灯座 |
| Tiles032 | Tiles032 (CC0) | 绿瓷砖墙裙 WainscotL/WainscotR（资产本身就是墨绿釉面砖，不再靠 tint 染） |
| PaintedMetal006 | PaintedMetal006 (CC0) | 爆漆绿铁：尽头铁门 EndDoor、信箱 Mailbox*、门牌 DoorPlate_* |
| Wood073 | Wood073 (CC0) | 实木门板 Door_3/Door_9 与门套（带木节的整板纹，比地板拼花贴得像门） |
| Concrete002 | Concrete002 (CC0) | 哑光金属件底纹（管/栏杆/把手/线槽/分格条）、渗水、异常件底面 |
| Paper003 | Paper003 (CC0) | 墙上小广告 Poster_*（揉皱的白纸才有纸味） |

这一批是 M6 质感线换的：Plaster004（脏米色粗灰泥）、Tiles006（白砖靠 tint 染绿）、
WoodFloor051（拼花地板贴到门上）三张在实机截图里被判定"题材不对"，换成上表的资产。

每个资产只保留 `BaseColor.jpg` / `Normal.jpg` / `Roughness.jpg` 三张 1K 图（由 ambientCG 的
`Color` / `NormalGL` / `Roughness` 改名而来）；
原始 zip 留在 `${TMPDIR:-/tmp}/acg-zips`，不进版本库。
