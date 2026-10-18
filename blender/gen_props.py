# blender/gen_props.py
# 杂物段（M6 T2）：把 Poly Haven 的 CC0 道具并进入口走廊的 base 集合。
#
# 为什么不让 Unity 直接吃这些 FBX：本工程所有材质由 Bootstrap3D 在装配时 new 出来
# （工程内没有 .mat 资产），道具自带的材质引用指向的是不存在的路径；而"名字→材质"
# 的分派表（Bootstrap3D.SurfFor）与三桶验收门只认 Corridor.fbx 这一棵树里的名字。
# 所以几何在这里并入，贴图单独落 Content/Textures/Prop_<slug>/，Unity 侧照旧按名建材质。
#
# 名字口径：`Prop_<slug>`，同一资产多件时 `Prop_<slug>__<n>`（slug 内不含下划线，
# 所以 Unity 侧切 "__" 就能反查贴图目录，不必再维护一张映射表）。
#
# 尺寸/三角数口径来自 tools/ph_prop_dims.mjs 读 Poly Haven 索引的 dimensions(毫米数组)
# 与 polycount —— 与本脚本的实测是**两个独立来源**，所以这两道门有牙：
# 尺寸逐轴对不上＝单位或缩放错，三角数对不上＝导进来的不是那一份 1k 几何。
# 索引各资产的轴向不统一（medical_box 的高在数组第 2 位），故尺寸比**排序后的三元组**。
#
# 落位约束（每条都在 Blender 侧当场判红，改摆放前先想清楚会不会撞）：
# 1. 实测尺寸与官方逐轴差 <= TOL（比排序三元组）。
# 2. 三角数与官方索引完全一致。
# 3. 任何件不得横跨走廊中线（跨中线＝堵路）。
# 4. 贴墙件内缘到对面墙至少留 CLEARANCE 通行带。
# 5. 任何件不得伸出墙体外表面 ±OUT_Y（Unity 侧"聚合宽度＝净宽"那条门靠它）。
# 6. 贴墙的件不得糊在门洞/窗洞上（洞口是"这楼像真的"的关键，被杂物挡死等于白开）。
import math
import os

import bpy
from mathutils import Matrix

def _stage_dir():
    """道具几何的中转目录——必须和 tools/fetch_props.sh 写到同一处。

    坑：Git Bash 的 $TMPDIR/TMP/TEMP 都是 MSYS 口径的 "/tmp"（真身 %LOCALAPPDATA%\\Temp），
    而 Windows 原生 blender.exe 把 "/tmp" 认成 "C:\\tmp"，两边读不到同一份文件（实测首轮就栽这）。
    所以逐个候选试，取第一个"真存在且有 fbx"的；找不到就把全部候选报进错误信息。
    """
    cands = []
    env = os.environ.get("PH_PROPS_DIR")
    if env:
        cands.append(env)
    la = os.environ.get("LOCALAPPDATA")
    if la:
        cands.append(os.path.join(la, "Temp", "ph-props"))
    for base in (os.environ.get("TMPDIR") or "", "/tmp", os.environ.get("TEMP") or ""):
        if base:
            cands.append(os.path.join(base, "ph-props"))
    for c in cands:
        if os.path.isdir(c) and any(f.endswith(".fbx") for f in os.listdir(c)):
            return c
    raise RuntimeError("找不到道具几何目录，候选=%s（先跑 ./tools/fetch_props.sh）" % cands)

IN_Y = 1.15          # 墙体内表面（与 gen_corridor.py 同一口径）
OUT_Y = 1.25         # 墙体外表面
CLEARANCE = 1.05     # 最窄通行带
TOL = 0.12           # 尺寸逐轴容差（相对官方索引）
WALL_TOUCH = 0.15    # 横向离这面墙不到这个距离＝算"贴这面墙的件"

# 洞口（左墙两扇门、右墙一扇窗）：与 gen_corridor.py 的 DOOR_ALONGS/WIN_* 同一口径，
# 故意各写一处——改洞口不同步改这里，等于新增一条"杂物堵门"没人拦。
OPENINGS = [
    ("left", 2.95, 3.85, 0.0, 2.10),    # 门洞 3.4±0.45
    ("left", 8.55, 9.45, 0.0, 2.10),    # 门洞 9.0±0.45
    ("right", 7.30, 10.70, 1.20, 2.80),  # 窗洞
]

# (slug, 资产 id, 官方尺寸三元组(米，轴序不保证), 官方三角数, 件数,
#  逐件 (沿程, 横向, 离地, 朝向))
# 朝向 "auto"＝按"上半部质心偏向哪头"判出靠背方向，自动转身让正面朝走廊内侧；
#            数字＝手工度数（挂墙件、对称件、想歪一点摆的）。
PROPS = [
    ("chair", "SchoolChair_01", [0.566, 0.675, 1.006], 5072, 3, [
        (1.55, -0.80, 0.10, "auto"),
        (2.15, -0.82, 0.10, "auto"),
        (20.45, 0.80, 0.10, "auto"),
    ]),
    ("bench", "painted_wooden_bench", [1.165, 0.497, 0.889], 630, 1, [
        (5.55, -0.86, 0.10, "auto"),
    ]),
    ("table", "side_table_01", [0.550, 0.450, 0.551], 2756, 1, [
        (4.60, 0.84, 0.10, "auto"),
    ]),
    ("medbox", "medical_box", [0.525, 0.350, 0.098], 3718, 1, [
        (4.60, 0.84, 0.66, "auto"),
    ]),
    ("shelf", "steel_frame_shelves_01", [1.097, 0.502, 2.141], 4348, 1, [
        (21.60, 0.80, 0.10, "auto"),
    ]),
    ("container", "plastic_container", [0.895, 0.626, 0.430], 4070, 1, [
        (11.60, -0.80, 0.10, "auto"),
    ]),
    ("crate", "plastic_crate_02", [0.506, 0.406, 0.254], 5840, 1, [
        (12.35, -0.72, 0.10, 25.0),
    ]),
    ("sign", "WetFloorSign_01", [0.300, 0.359, 0.630], 228, 1, [
        (13.30, 0.45, 0.10, 30.0),
    ]),
    ("bag", "trashbag", [0.528, 0.463, 0.575], 4482, 1, [
        (14.05, 0.83, 0.10, "auto"),
    ]),
    ("extinguisher", "korean_fire_extinguisher_01", [0.280, 0.367, 0.659], 9913, 1, [
        (16.60, -0.86, 0.10, "auto"),
    ]),
    ("plunger", "plunger", [0.172, 0.172, 0.421], 3278, 1, [
        (17.20, -0.90, 0.10, "auto"),
    ]),
    ("clock", "wall_clock", [0.320, 0.047, 0.320], 3658, 1, [
        (7.45, -1.10, 2.30, "auto"),
    ]),
    ("alarm", "fire_alarm", [0.099, 0.026, 0.144], 2705, 1, [
        (19.80, -1.10, 2.05, "auto"),
    ]),
]


def _import_meshes(slug):
    """导一件资产，只返回它带进来的网格。

    绝不"先清场"：本脚本在 gen_corridor.build 之后运行，select_all+delete 会把
    整条走廊删干净（实测过一次，FBX 只剩道具）。改成按导入前后的对象差集取值，
    非网格对象（FBX 根空物体）拆掉父子后删除，别跟着进 Corridor.fbx。
    """
    src = os.path.join(_stage_dir(), slug + ".fbx")
    if not os.path.exists(src):
        raise RuntimeError("缺道具几何 %s（先跑 ./tools/fetch_props.sh）" % src)
    before = {o.name for o in bpy.data.objects}
    if hasattr(bpy.ops.wm, "fbx_import"):
        bpy.ops.wm.fbx_import(filepath=src)
    else:
        bpy.ops.import_scene.fbx(filepath=src)
    fresh = [o for o in bpy.data.objects if o.name not in before]
    meshes = [o for o in fresh if o.type == "MESH"]
    for o in meshes:                       # 脱离 FBX 的空物体父级，世界位置不丢
        mw = o.matrix_world.copy()
        o.parent = None
        o.matrix_world = mw
    for o in fresh:
        if o.type != "MESH":
            bpy.data.objects.remove(o, do_unlink=True)
    if not meshes:
        raise RuntimeError("道具 %s 导进来没有网格" % slug)
    return meshes


def _bbox(ob):
    lo = [1e9, 1e9, 1e9]
    hi = [-1e9, -1e9, -1e9]
    for vtx in ob.data.vertices:
        w = ob.matrix_world @ vtx.co
        for i in range(3):
            lo[i] = min(lo[i], w[i])
            hi[i] = max(hi[i], w[i])
    return lo, hi


def _tris(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)


def _join(parts, name):
    bpy.ops.object.select_all(action="DESELECT")
    for o in parts:
        o.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    if len(parts) > 1:
        bpy.ops.object.join()
    ob = bpy.context.view_layer.objects.active
    ob.name = name
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    for slot in list(ob.material_slots):      # 材质交给 Unity 按名建，这里清空引用
        slot.material = None
    return ob


def _check_size(ob, want, tag, errs):
    lo, hi = _bbox(ob)
    got = [hi[i] - lo[i] for i in range(3)]
    # 轴序不保证，比排序后的三元组：只抓"缩放/单位错"，不猜作者的轴向
    gs, ws = sorted(got), sorted(want)
    for i in range(3):
        if ws[i] <= 0:
            continue
        if abs(gs[i] - ws[i]) / ws[i] > TOL:
            errs.append("%s 实测尺寸 %s 与官方排序三元组 %s 逐轴差过 %.0f%%（单位/缩放错？）"
                        % (tag, [round(v, 3) for v in got], [round(v, 3) for v in ws], TOL * 100))
    return got


def _check_tris(ob, want, tag, errs):
    got = _tris(ob)
    if got != want:
        errs.append("%s 三角数 %d 与官方索引 %d 不符＝导进来的不是那份 1k 几何" % (tag, got, want))
    return got


def _drop_to_origin(ob):
    """把网格挪成"底面中心在原点"，节点位置之后就直接是落点。"""
    lo, hi = _bbox(ob)
    cx = 0.5 * (lo[0] + hi[0])
    cy = 0.5 * (lo[1] + hi[1])
    ob.data.transform(Matrix.Translation((-cx, -cy, -lo[2])))
    ob.location = (0.0, 0.0, 0.0)
    bpy.context.view_layer.update()


def _back_heading(ob):
    """上半部（60% 高度以上）顶点质心偏向的方向＝靠背朝向（角度按 Z 旋转口径）。

    返回 None＝上下一般粗的对称件，转身没意义。注意这是**背面**，不是正面。
    """
    lo, hi = _bbox(ob)
    hz = hi[2] - lo[2]
    hx = 0.5 * (lo[0] + hi[0])
    hy = 0.5 * (lo[1] + hi[1])
    sx = sy = 0.0
    n = 0
    for vtx in ob.data.vertices:
        w = ob.matrix_world @ vtx.co
        if w[2] > lo[2] + 0.6 * hz:
            sx += w[0] - hx
            sy += w[1] - hy
            n += 1
    if n == 0:
        return None
    sx /= n
    sy /= n
    dx = abs(sx) / max(1e-6, hi[0] - lo[0])
    dy = abs(sy) / max(1e-6, hi[1] - lo[1])
    if max(dx, dy) < 0.04:
        return None
    if dx > dy:
        return 0.0 if sx > 0 else 180.0
    return 90.0 if sy > 0 else -90.0


def _face_inward(ob, side):
    """让正面朝走廊内侧：背面朝 back，转身量 = inward - (back + 180)。"""
    inward = 90.0 if side < 0 else -90.0   # 左墙（y<0）面向 +y，右墙面向 -y
    back = _back_heading(ob)
    if back is None:
        return 0.0
    return (inward - back - 180.0) % 360.0


def _check_placement(tag, lo, hi, errs):
    if lo[1] * hi[1] < 0.0:
        errs.append("%s 横跨中线（y %0.2f..%0.2f）＝堵路" % (tag, lo[1], hi[1]))
    inner = min(abs(lo[1]), abs(hi[1]))
    if IN_Y + inner < CLEARANCE:
        errs.append("%s 内缘只留 %0.2fm 通行带（要求 >=%0.2f）" % (tag, IN_Y + inner, CLEARANCE))
    if max(abs(lo[1]), abs(hi[1])) > OUT_Y:
        errs.append("%s 伸出墙外表面（|y| 到 %0.2f > %0.2f）" % (tag, max(abs(lo[1]), abs(hi[1])), OUT_Y))
    wall = "left" if (lo[1] + hi[1]) < 0 else "right"
    edge = -IN_Y if wall == "left" else IN_Y
    touches = (lo[1] < edge + WALL_TOUCH) if wall == "left" else (hi[1] > edge - WALL_TOUCH)
    if touches:
        for name, ox0, ox1, oz0, oz1 in OPENINGS:
            if name != wall:
                continue
            if lo[0] < ox1 and hi[0] > ox0 and lo[2] < oz1 and hi[2] > oz0:
                errs.append("%s 糊在%s侧洞口 %0.2f..%0.2f×%0.2f..%0.2f 上（x %0.2f..%0.2f z %0.2f..%0.2f）"
                            % (tag, "门" if oz1 <= 2.1 else "窗", ox0, ox1, oz0, oz1, lo[0], hi[0], lo[2], hi[2]))


def build(base_coll):
    """返回逐件一行读数；任何一条约束不满足就攒到最后一次性抛（一轮跑完看全部违规，
    而不是改一个落点跑一次）。"""
    made = []
    errs = []
    for slug, asset, want, tris_want, count, spots in PROPS:
        if len(spots) != count:
            raise RuntimeError("%s 声明 %d 件但给了 %d 个落点" % (slug, count, len(spots)))
        parts = _import_meshes(slug)
        proto = _join(parts, "_Proto_" + slug)
        got = _check_size(proto, want, slug, errs)
        _check_tris(proto, tris_want, slug, errs)
        _drop_to_origin(proto)
        for i, (along, side, z, orient) in enumerate(spots):
            tag = "Prop_%s" % slug if count == 1 else "Prop_%s__%d" % (slug, i + 1)
            # 每件都从 proto 复制一份网格：直接在 proto 上 transform_apply 会把第一件的
            # 转身烘进共享网格，后面的件就在第一件的朝向基础上再转一次。
            # Blender 5.2 的 Object.copy() 不收参数（实测 TypeError: takes at most 0
            # arguments），网格要单独赋。
            ob = proto.copy()
            ob.data = proto.data.copy()
            ob.name = tag
            bpy.context.scene.collection.objects.link(ob)
            bpy.context.view_layer.update()
            rot = _face_inward(ob, side) if orient == "auto" else float(orient)
            bpy.ops.object.select_all(action="DESELECT")
            ob.select_set(True)
            bpy.context.view_layer.objects.active = ob
            ob.rotation_euler = (0.0, 0.0, math.radians(rot))
            bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
            ob.location = (along, side, z)
            # 落位判据读的是 matrix_world，而它只在依赖图更新后才反映刚设的 location
            # （实测不 update 时 15 件全部读成"在原点上、横跨中线"＝整段假红）
            bpy.context.view_layer.update()
            lo, hi = _bbox(ob)
            _check_placement(tag, lo, hi, errs)
            for c in list(ob.users_collection):
                c.objects.unlink(ob)
            base_coll.objects.link(ob)
            made.append("%s tris=%d size=%s rot=%0.0f y=%0.2f..%0.2f z=%0.2f..%0.2f"
                        % (tag, _tris(ob), [round(v, 2) for v in got], rot, lo[1], hi[1], lo[2], hi[2]))
        bpy.data.objects.remove(proto, do_unlink=True)
    if errs:
        raise RuntimeError("杂物落位/口径自检失败 %d 条：\n  - %s" % (len(errs), "\n  - ".join(errs)))
    return made
