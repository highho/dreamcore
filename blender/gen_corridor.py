# blender/gen_corridor.py
# base 走廊几何的唯一真相：宽 2.4 × 高 3 × 长 24，中式筒子楼。
# 建模轴：长度沿 +X，高度沿 +Z（Blender Z-up）。Unity 侧用 -90° Y 旋转对齐，见 README。
#
# 命名契约（spec §9.1 实测裁定 + Task 1 修复轮）：Blender 集合进不了 FBX，
# 所以"契约名"必须是**对象名**。base 物件裸名；wire 名（anom<NN>_…）只出现在 gen_anomalies.py。
# 名字前缀同时是 Unity 侧的材质分派依据（Bootstrap3D.PaintShell 的有序规则表），
# 所以改名/加前缀要先去看那张表，否则新件会被兜底规则涂成墙色。
#
# 结构段（M6）的关键约束：**任何 base 件的 y 都不得超出 ±1.25**（墙体外表面）。
# 门洞/窗洞背后的衬板一律落在墙体厚度内（|y| ≤ 1.25）——一旦伸到墙外，
# Unity 侧"聚合包围盒宽度＝走廊净宽"这条门就失去物理含义（旧口径 2.91 就是这么来的，
# 见 spec §9.3 宽度勘误）。
import bpy

WIDTH, HEIGHT, LENGTH = 2.4, 3.0, 24.0
LIGHT_ZS = [2.0, 6.0, 10.0, 14.0, 18.0, 22.0]   # 沿走廊长度的位置，Unity 侧按此建点光

# 墙体：中心 ±1.2、厚 0.1 ⇒ 内表面 ±1.15、外表面 ±1.25。楼板顶 0.10、天花板底 3.00。
WALL_T = 0.1
IN_Y = WIDTH * 0.5 - WALL_T * 0.5      # 1.15 内表面
OUT_Y = WIDTH * 0.5 + WALL_T * 0.5     # 1.25 外表面
FLOOR_TOP = 0.10

# 洞口布置（沿程 X / 高度 Z）：两扇门在左墙，一扇窗在右墙
DOOR_ALONGS = [3.4, 9.0]
DOOR_W, DOOR_H = 0.9, 2.1
WIN_ALONG, WIN_W, WIN_Z0, WIN_Z1 = 9.0, 3.4, 1.2, 2.8


def coll(name, parent):
    found = bpy.data.collections.get(name)
    c = found if found is not None and found.name == name else bpy.data.collections.new(name)
    # bpy_prop_collection 的 in 只认名字字符串（brief 原文写 `c not in parent.children`
    # 在 Blender 5.2.2 直接 TypeError），问的仍是同一个问题：这个集合挂到这个父级上了没。
    if name not in parent.children:
        parent.children.link(c)
    return c


def move_to(ob, collection):
    for c in list(ob.users_collection):
        c.objects.unlink(ob)
    collection.objects.link(ob)


def bx(name, collection, center, size):
    """显式中心＋尺寸（x=沿程, y=横向, z=高度）。缩放当场烘进网格，节点只留位置。"""
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=center)
    ob = bpy.context.active_object
    ob.name = name
    ob.scale = size
    bpy.ops.object.transform_apply(scale=True)
    move_to(ob, collection)
    return ob


def box(name, collection, along, height, side, size):
    """旧签名（gen_anomalies.py 在用）：沿程/离地/横向 ＋ (x,y,z) 尺寸。"""
    return bx(name, collection, (along, side, height), size)


def cyl(name, collection, center, radius, depth, axis="Z"):
    """管子/车轮。axis 决定圆柱轴向；旋转当场烘焙，节点不带旋转
    （FBX 侧的 lossyScale=100 会污染未烘焙变换的读数，见 spec §9.2）。"""
    rot = {"Z": (0.0, 0.0, 0.0), "Y": (1.5707963, 0.0, 0.0), "X": (0.0, 1.5707963, 0.0)}[axis]
    bpy.ops.mesh.primitive_cylinder_add(vertices=20, radius=radius, depth=depth,
                                        location=center, rotation=rot)
    ob = bpy.context.active_object
    ob.name = name
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    bpy.ops.object.shade_smooth()
    move_to(ob, collection)
    return ob


def join_into(target, parts):
    """把零件并成一个网格（自行车＝两轮＋车架＋车座＋横把 → 单个 BikeUp）。
    实测（Blender 5.2.2 headless）：join 后只剩一个对象、UV 层不丢，
    于是一个异常只需一个 hide 代理指认它，不必为每个零件补代理。"""
    bpy.ops.object.select_all(action="DESELECT")
    for o in [target] + list(parts):
        o.select_set(True)
    bpy.context.view_layer.objects.active = target
    bpy.ops.object.join()
    return bpy.context.active_object


def empty(name, collection, along, height, side):
    ob = bpy.data.objects.new(name, None)
    ob.location = (along, side, height)
    collection.objects.link(ob)
    return ob


def build(scene_coll):
    base = coll("base", scene_coll)
    half_w = WIDTH * 0.5
    half_l = LENGTH * 0.5

    # ---------- 楼板 / 天花 / 端墙 ----------
    bx("Floor", base, (half_l, 0.0, 0.05), (LENGTH, WIDTH, WALL_T))
    bx("Ceiling", base, (half_l, 0.0, HEIGHT + 0.05), (LENGTH, WIDTH, WALL_T))
    bx("EndWall", base, (LENGTH, 0.0, HEIGHT * 0.5), (WALL_T, WIDTH, HEIGHT))

    # ---------- 左右墙：真开洞 ⇒ 分段 ----------
    _walls_left(base)
    _walls_right(base)

    # ---------- 墙裙（绿漆瓷砖）＋ 顶线 ----------
    bx("WainscotL", base, (half_l, -(IN_Y - 0.006), 0.6), (LENGTH, 0.012, 1.2))
    bx("WainscotR", base, (half_l, IN_Y - 0.006, 0.6), (LENGTH, 0.012, 1.2))
    bx("WainscotCapL", base, (half_l, -(IN_Y - 0.010), 1.23), (LENGTH, 0.02, 0.06))
    bx("WainscotCapR", base, (half_l, IN_Y - 0.010, 1.23), (LENGTH, 0.02, 0.06))

    # ---------- 门（左墙两户）＋ 窗（右墙）＋ 尽头铁门 ----------
    for a in DOOR_ALONGS:
        _door(base, a)
    _window(base)
    _end_door(base)

    # ---------- 灯具：灯座（白）＋ 发光灯管（自发光，Unity 侧 Unlit）----------
    # 名字刻意用 Lamp_ 而不是 Light_：AdoptLights 按 StartsWith("Light_") 收养点光，
    # 灯具网格若撞上那个前缀会被挂上 Light 组件。
    for z in LIGHT_ZS:
        tag = int(z)
        bx("LampBase_%d" % tag, base, (z, 0.0, HEIGHT - 0.025), (0.36, 0.16, 0.05))
        bx("Lamp_%d" % tag, base, (z, 0.0, HEIGHT - 0.0775), (0.92, 0.09, 0.055))

    # ---------- 立面管线与地面分格（老楼最认得出的两样）----------
    bx("CableTrunk_L", base, (half_l, -(IN_Y - 0.03), 2.72), (LENGTH, 0.06, 0.08))
    for i, a in enumerate([5.2, 13.2], start=1):
        cyl("Pipe_%d" % i, base, (a, -(IN_Y - 0.055), HEIGHT * 0.5), 0.055, HEIGHT, "Z")
    for i, a in enumerate([6.0, 12.0, 18.0], start=1):
        bx("FloorStrip_%d" % i, base, (a, 0.0, FLOOR_TOP + 0.006), (0.03, WIDTH, 0.012))

    # ---------- 可被异常指认的本体（名字与 Task 5 灰盒逐字一致）----------
    # 贴墙件中心＝内表面 ∓ 半厚，让它贴着墙而不是"立在走廊里"（M3 的实机教训）。
    bx("Poster_6", base, (6.0, -(IN_Y - 0.010), 1.60), (0.5, 0.02, 0.7))
    # Poster_10 从 10.0 挪到 11.4：窗洞占右墙 7.3..10.7，原位置会糊在窗框上。
    bx("Poster_10", base, (11.4, IN_Y - 0.010, 1.60), (0.5, 0.02, 0.7))
    bx("Poster_18", base, (18.0, -(IN_Y - 0.010), 1.55), (0.44, 0.02, 0.6))

    bx("MailboxBody", base, (12.6, 1.08, 1.25), (1.0, 0.22, 0.8))
    bx("MailboxDoor", base, (12.6, 0.955, 1.25), (0.95, 0.03, 0.75))
    bx("Mailbox_Div1", base, (12.28, 0.9385, 1.25), (0.02, 0.005, 0.75))
    bx("Mailbox_Div2", base, (12.92, 0.9385, 1.25), (0.02, 0.005, 0.75))

    _bike(base)

    # 窗外衬板：落在墙体厚度内（1.23..1.25），透过真窗洞看见的一小片夜色。
    bx("WindowBuildings", base, (WIN_ALONG, OUT_Y - 0.010, 0.5 * (WIN_Z0 + WIN_Z1)),
       (WIN_W, 0.02, WIN_Z1 - WIN_Z0))

    for z in LIGHT_ZS:
        empty("Light_%d" % int(z), base, z, HEIGHT - 0.25, 0.0)

    return base


def _segment(collection, name, y_center, x0, x1, z0, z1):
    """把一面墙切成一块矩形：只填 [x0,x1]×[z0,z1]，其余留给洞口。"""
    bx(name, collection, (0.5 * (x0 + x1), y_center, 0.5 * (z0 + z1)),
       (x1 - x0, WALL_T, z1 - z0))


def _walls_left(base):
    """左墙：两扇门洞 [a-0.45, a+0.45]×[0, 2.1]。WallLeft 这个名字留给最长那段（契约）。"""
    yc = -WIDTH * 0.5
    a1, a2 = DOOR_ALONGS
    e1, e2 = a1 - DOOR_W * 0.5, a2 + DOOR_W * 0.5
    _segment(base, "WallLeft_A", yc, 0.0, a1 - DOOR_W * 0.5, 0.0, HEIGHT)
    _segment(base, "WallLeft_B", yc, a1 + DOOR_W * 0.5, a2 - DOOR_W * 0.5, 0.0, HEIGHT)
    _segment(base, "WallLeft", yc, e2, LENGTH, 0.0, HEIGHT)
    for a in DOOR_ALONGS:
        _segment(base, "WallLeft_Lintel_%d" % int(a), yc,
                 a - DOOR_W * 0.5, a + DOOR_W * 0.5, DOOR_H, HEIGHT)


def _walls_right(base):
    """右墙：窗洞 [7.3,10.7]×[1.2,2.8]。窗下那段仍要实体（墙裙背后是墙，不是空的）。"""
    yc = WIDTH * 0.5
    x0, x1 = WIN_ALONG - WIN_W * 0.5, WIN_ALONG + WIN_W * 0.5
    _segment(base, "WallRight_A", yc, 0.0, x0, 0.0, HEIGHT)
    _segment(base, "WallRight", yc, x1, LENGTH, 0.0, HEIGHT)
    _segment(base, "WallRight_Sill", yc, x0, x1, 0.0, WIN_Z0)
    _segment(base, "WallRight_Lintel", yc, x0, x1, WIN_Z1, HEIGHT)


def _door(base, a):
    """一户门：门洞里的黑衬板（＝门斗）＋ 门套 ＋ 退进 3.5cm 的门板 ＋ 把手 ＋ 门牌。
    衬板落在墙厚内（-1.25..-1.24），所以聚合宽度仍是 2.5m 净宽。"""
    tag = int(a)
    yc_in = -IN_Y
    bx("Doorway_%d" % tag, base, (a, -OUT_Y + 0.005, 0.5 * DOOR_H),
       (DOOR_W, 0.01, DOOR_H))
    bx("DoorFrame_%d_JambL" % tag, base, (a - DOOR_W * 0.5 + 0.04, yc_in + 0.035, DOOR_H * 0.5),
       (0.08, 0.07, DOOR_H))
    bx("DoorFrame_%d_JambR" % tag, base, (a + DOOR_W * 0.5 - 0.04, yc_in + 0.035, DOOR_H * 0.5),
       (0.08, 0.07, DOOR_H))
    bx("DoorFrame_%d_Head" % tag, base, (a, yc_in + 0.035, DOOR_H - 0.04),
       (DOOR_W, 0.07, 0.08))
    # 门板：契约名逐字保留 Door_3 / Door_9（anom03 指认 Door_3）
    bx("Door_%d" % tag, base, (a, yc_in - 0.015, 0.5 * (DOOR_H - 0.1)),
       (DOOR_W - 0.1, 0.04, DOOR_H - 0.1))
    bx("Door_%d_Handle" % tag, base, (a + DOOR_W * 0.5 - 0.13, yc_in + 0.02, 1.0),
       (0.05, 0.04, 0.14))
    bx("DoorPlate_%d" % tag, base, (a, yc_in - 0.008, DOOR_H + 0.20),
       (0.16, 0.016, 0.10))


def _window(base):
    """一扇真窗：窗台板＋上亮＋两侧竖框＋中竖＋玻璃＋四根防盗栏。全部在 |y| ≤ 1.15 一侧。"""
    x0, x1 = WIN_ALONG - WIN_W * 0.5, WIN_ALONG + WIN_W * 0.5
    zc = 0.5 * (WIN_Z0 + WIN_Z1)
    bx("WindowFrame_Sill", base, (WIN_ALONG, IN_Y - 0.050, WIN_Z0 + 0.025), (WIN_W + 0.10, 0.10, 0.05))
    bx("WindowFrame_Head", base, (WIN_ALONG, IN_Y - 0.035, WIN_Z1 - 0.025), (WIN_W + 0.10, 0.07, 0.05))
    bx("WindowFrame_JambL", base, (x0 + 0.04, IN_Y - 0.035, zc), (0.08, 0.07, WIN_Z1 - WIN_Z0))
    bx("WindowFrame_JambR", base, (x1 - 0.04, IN_Y - 0.035, zc), (0.08, 0.07, WIN_Z1 - WIN_Z0))
    bx("WindowFrame_Mullion", base, (WIN_ALONG, IN_Y - 0.015, zc), (0.06, 0.03, WIN_Z1 - WIN_Z0))
    bx("Glass_R", base, (WIN_ALONG, IN_Y - 0.022, zc), (WIN_W - 0.12, 0.006, WIN_Z1 - WIN_Z0 - 0.08))
    for i, a in enumerate([7.9, 8.6, 9.4, 10.1], start=1):
        bx("WindowBar_%d" % i, base, (a, IN_Y - 0.035, zc), (0.035, 0.035, WIN_Z1 - WIN_Z0))
    bx("WindowRail", base, (WIN_ALONG, IN_Y - 0.035, zc), (WIN_W - 0.10, 0.03, 0.03))


def _end_door(base):
    """尽头：绿漆铁门（楼梯间）＋ 把手 ＋ 自发光"安全出口"牌。
    牌子在正常态就常亮，是暗走廊远端唯一的目标点。"""
    bx("EndDoor", base, (LENGTH - 0.10, 0.0, 1.05), (0.06, 1.0, 2.1))
    bx("EndDoor_Handle", base, (LENGTH - 0.145, 0.35, 1.05), (0.05, 0.12, 0.05))
    bx("ExitSign", base, (LENGTH - 0.115, 0.0, 2.42), (0.04, 0.5, 0.16))


def _bike(base):
    """旧自行车＝一个网格（两轮＋上梁＋车座＋横把），契约名 BikeUp 不变，
    anom06 的单个 hide 代理照样指认得动。"""
    yc = 1.02
    wf = cyl("_WheelF", base, (15.05, yc, FLOOR_TOP + 0.33), 0.33, 0.05, "Y")
    wr = cyl("_WheelR", base, (15.95, yc, FLOOR_TOP + 0.33), 0.33, 0.05, "Y")
    frame = bx("_Frame", base, (15.5, yc, 0.70), (0.72, 0.05, 0.06))
    seat = bx("_Seat", base, (15.22, yc, 0.93), (0.26, 0.06, 0.05))
    bar = bx("_Bar", base, (15.82, yc, 0.95), (0.06, 0.22, 0.05))
    ob = join_into(frame, [wf, wr, seat, bar])
    ob.name = "BikeUp"
    move_to(ob, base)
    return ob
