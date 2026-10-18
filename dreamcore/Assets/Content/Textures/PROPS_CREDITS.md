# 道具模型出处（全部 CC0）

来源：[Poly Haven](https://polyhaven.com/) —— 模型与贴图均以 **CC0 1.0 Universal**（公有领域）发布，
可商用、无需署名（此处署名仅为可追溯）。本文件由 `./tools/fetch_props.sh` 生成，
每件在下图前都抓过它的资产页并核到 "CC0 1.0 Universal" 字样。

| 目录 / 网格名 | Poly Haven 资产 | 本项目用途 |
|---|---|---|
| Prop_chair | SchoolChair_01 | 靠墙课椅 Prop_chair_1/2/3 |
| Prop_bench | painted_wooden_bench | 长凳 Prop_bench |
| Prop_shelf | steel_frame_shelves_01 | 金属置物架 Prop_shelf |
| Prop_table | side_table_01 | 小边桌 Prop_table |
| Prop_container | plastic_container | 塑料周转箱 Prop_container |
| Prop_crate | plastic_crate_02 | 塑料筐 Prop_crate |
| Prop_sign | WetFloorSign_01 | "小心地滑"立牌 Prop_sign |
| Prop_bag | trashbag | 垃圾袋 Prop_bag |
| Prop_extinguisher | korean_fire_extinguisher_01 | 灭火器 Prop_extinguisher |
| Prop_clock | wall_clock | 挂钟 Prop_clock |
| Prop_alarm | fire_alarm | 火警按钮 Prop_alarm |
| Prop_medbox | medical_box | 急救箱 Prop_medbox |
| Prop_plunger | plunger | 皮搋子 Prop_plunger |

每件只留 `BaseColor.jpg` + `Normal.jpg` 两张 1K 图（由 Poly Haven 的 `diff` / `nor_gl` 改名而来，
法线一律取 **jpg 变体的 OpenGL 图**）；几何 fbx(1k) 留在 `${TMPDIR:-/tmp}/ph-props/`，不进版本库
——它只被 blender/gen_props.py 消费，最终随 Corridor.fbx 一起导出。
