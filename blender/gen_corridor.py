# blender/gen_corridor.py
# base 走廊几何的唯一真相：宽 2.4 × 高 3 × 长 24，中式筒子楼。
# 建模轴：长度沿 +X，高度沿 +Z（Blender Z-up）。Unity 侧用 -90° Y 旋转对齐，见 README。
#
# 命名契约（spec §9.1 实测裁定 + Task 1 修复轮）：Blender 集合进不了 FBX，
# 所以"契约名"必须是**对象名**。base 物件一律裸名（下面这批名字与 Task 5 灰盒逐字一致）：
#   Floor WallLeft WallRight Ceiling EndWall WainscotL WainscotR
#   Door_3 Door_9 Poster_6 Poster_10 Poster_18 MailboxBody MailboxDoor BikeUp WindowBuildings
#   Light_2 Light_6 Light_10 Light_14 Light_18 Light_22（六盏是 EMPTY 灯位锚点）
# coll() 只作本侧编排容器用，导出后不存在，任何下游读取都不得依赖它。
import bpy

WIDTH, HEIGHT, LENGTH = 2.4, 3.0, 24.0
LIGHT_ZS = [2.0, 6.0, 10.0, 14.0, 18.0, 22.0]   # 沿走廊长度的位置，Unity 侧按此建点光


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


def box(name, collection, along, height, side, size):
    """along=沿走廊长度(X), height=离地(Z), side=横向(Y, 走廊中线为 0)。"""
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=(along, side, height))
    ob = bpy.context.active_object
    ob.name = name
    ob.scale = size
    bpy.ops.object.transform_apply(scale=True)
    move_to(ob, collection)
    return ob


def empty(name, collection, along, height, side):
    ob = bpy.data.objects.new(name, None)
    ob.location = (along, side, height)
    collection.objects.link(ob)
    return ob


def build(scene_coll):
    base = coll("base", scene_coll)
    half_w = WIDTH * 0.5
    half_l = LENGTH * 0.5

    box("Floor", base, half_l, 0.05, 0.0, (LENGTH, WIDTH, 0.1))
    box("WallLeft", base, half_l, HEIGHT * 0.5, -half_w, (LENGTH, 0.1, HEIGHT))
    box("WallRight", base, half_l, HEIGHT * 0.5, half_w, (LENGTH, 0.1, HEIGHT))
    box("Ceiling", base, half_l, HEIGHT + 0.05, 0.0, (LENGTH, WIDTH, 0.1))
    box("EndWall", base, LENGTH, HEIGHT * 0.5, 0.0, (0.1, WIDTH, HEIGHT))
    box("WainscotL", base, half_l, 0.6, -half_w + 0.06, (LENGTH, 0.012, 1.2))   # 绿漆墙裙
    box("WainscotR", base, half_l, 0.6, half_w - 0.06, (LENGTH, 0.012, 1.2))

    # 可被异常指认的本体（命名与 Task 5 灰盒逐字一致）
    box("Door_3", base, 3.4, 1.05, -half_w + 0.06, (0.9, 0.04, 2.1))
    box("Door_9", base, 9.0, 1.05, -half_w + 0.06, (0.9, 0.04, 2.1))
    box("Poster_6", base, 6.0, 1.5, -half_w + 0.07, (0.5, 0.02, 0.7))
    box("Poster_10", base, 10.0, 1.5, half_w - 0.07, (0.5, 0.02, 0.7))
    box("Poster_18", base, 18.0, 1.45, -half_w + 0.07, (0.44, 0.02, 0.6))
    box("MailboxBody", base, 12.6, 1.25, half_w - 0.12, (1.0, 0.22, 0.8))
    box("MailboxDoor", base, 12.6, 1.25, half_w - 0.02, (0.95, 0.03, 0.75))
    box("BikeUp", base, 15.5, 0.55, half_w - 0.30, (0.2, 0.55, 1.1))
    box("WindowBuildings", base, 9.0, 1.9, half_w + 0.45, (3.4, 0.02, 1.6))  # 窗外该有楼的地方

    for z in LIGHT_ZS:
        empty("Light_%d" % int(z), base, z, HEIGHT - 0.25, 0.0)

    return base
