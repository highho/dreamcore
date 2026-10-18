# blender/gen_body.py
# 腿脚段（M7）：程序化第一人称身体——两条锥化裤腿 + 两只鞋，导出成**独立的 Body.fbx**。
#
# 为什么不并进 Corridor.fbx：那棵树被"base 网格逐字保真"和"未登记 base 名必须为 0"两道门
# 按名字管着，而腿脚是玩家的身体、不是走廊的构件。并进去要么把两套契约搅在一棵树上，
# 要么让验收把身体件当成漏登记的杂物判红。所以独立文件、独立装配（Bootstrap3D.AssembleBody），
# 边界由 Verify3D 那条"Corridor(Clone) 子树里不许出现身体件"守。
#
# 轴向口径与 gen_corridor.py 逐字一致：+X=沿程（这里＝脚尖朝的方向）、+Y=横向、+Z=离地。
# Unity 侧套同一个 Bootstrap3D.FbxYaw，于是"脚尖朝 Blender +X"＝"脚尖朝玩家 forward"。
# 左右：FbxYaw 之后 Blender +Y 落到 Unity -X（README 轴映射小节），所以 +Y 那件是玩家的**左**腿，
# 名字按 Unity 侧的左右命名（腿脚对称，名字只用于验收点齐）。
#
# 枢轴：每件的原点当场用 3D 光标设到髋/踝。运行期摆腿是**绕原点**转的（FirstPersonController.SwingLeg），
# 原点落在脚底会像圆规一样从地面扫起来，落在髋部才是髋关节。这是本文件唯一不能"随便挪一下"的地方。
#
# 尺寸口径：脚底 z=0 对应身体空间的地面，Unity 侧 Player 根＝脚底（CharacterController 的 center
# 抬到半高，见 Bootstrap3D），所以 z 值就是"离地米数"，与走廊的 FLOOR_TOP=0.10 不需要任何换算。
import bpy
from mathutils import Vector

HIP_Z = 0.95          # 髋关节离地：腿的枢轴
LEG_Y = 0.14          # 两腿横向半距（髋宽 0.28m）
ANKLE_Z = 0.09        # 踝离地＝鞋枢轴
SOLE_TOP = 0.04       # 鞋底上表面
R_HIP, R_ANKLE, R_CUFF = 0.115, 0.062, 0.075   # 大腿／脚踝／裤脚口的半径（锥化靠这三个数）
SEG = 20              # 回转体分段：4 件合计约 260 三角面，第一人称贴脸也不糊

TOE_X = 0.205         # 脚尖前缘（相对腿轴）
HEEL_X = -0.105       # 鞋跟后缘
SHOE_W = 0.12

NAMES = ("Leg_L", "Leg_R", "Shoe_L", "Shoe_R")


def coll(name, parent):
    found = bpy.data.collections.get(name)
    c = found if found is not None and found.name == name else bpy.data.collections.new(name)
    if name not in parent.children:
        parent.children.link(c)
    return c


def move_to(ob, collection):
    for c in list(ob.users_collection):
        c.objects.unlink(ob)
    collection.objects.link(ob)


def box(name, collection, center, size):
    """显式中心＋尺寸；缩放当场烘进网格（lossyScale 的坑同 gen_corridor.bx）。"""
    bpy.ops.mesh.primitive_cube_add(size=1.0, location=center)
    ob = bpy.context.active_object
    ob.name = name
    ob.scale = size
    bpy.ops.object.transform_apply(scale=True)
    move_to(ob, collection)
    return ob


def cone(name, collection, center, r_bottom, r_top, depth):
    """锥化裤腿。Blender 的 radius1 在 -Z（底）、radius2 在 +Z（顶），
    这里参数按"底/顶"命名，调用处就不会把大小头写反（写反＝倒三角腿）。"""
    bpy.ops.mesh.primitive_cone_add(vertices=SEG, radius1=r_bottom, radius2=r_top,
                                    depth=depth, location=center)
    ob = bpy.context.active_object
    ob.name = name
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    bpy.ops.object.shade_smooth()
    move_to(ob, collection)
    return ob


def cylinder(name, collection, center, radius, depth):
    bpy.ops.mesh.primitive_cylinder_add(vertices=SEG, radius=radius, depth=depth, location=center)
    ob = bpy.context.active_object
    ob.name = name
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    bpy.ops.object.shade_smooth()
    move_to(ob, collection)
    return ob


def join_into(target, parts):
    bpy.ops.object.select_all(action="DESELECT")
    for o in [target] + list(parts):
        o.select_set(True)
    bpy.context.view_layer.objects.active = target
    bpy.ops.object.join()
    return bpy.context.active_object


def set_origin(ob, location):
    """把原点挪到 location（世界坐标），网格数据不动。摆腿绕原点转，原点即关节。"""
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.context.scene.cursor.location = location
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR")
    bpy.context.scene.cursor.location = (0.0, 0.0, 0.0)


def world_bounds(ob):
    bpy.context.view_layer.update()   # matrix_world 在 view_layer.update() 前是陈旧值（实测坑）
    pts = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi


def tris(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)


def _leg(collection, name, hip_y):
    """一条腿：锥化裤腿（大腿 0.115 → 脚踝 0.062）＋ 裤脚口一圈。"""
    shaft = cone(name + "_shaft", collection, (0.0, hip_y, (ANKLE_Z + HIP_Z) * 0.5),
                 R_ANKLE, R_HIP, HIP_Z - ANKLE_Z)
    cuff = cylinder(name + "_cuff", collection, (0.0, hip_y, ANKLE_Z + 0.045), R_CUFF, 0.07)
    ob = join_into(shaft, [cuff])
    ob.name = name
    set_origin(ob, (0.0, hip_y, HIP_Z))
    return ob


def _shoe(collection, name, parent, hip_y):
    """一只鞋：薄底向前延伸出脚尖（平头往前），帮面偏后偏窄——两个盒子叠出来的不是方块，
    是"脚尖朝前踩在地上"。"""
    sole = box(name + "_sole", collection, (0.055, hip_y, SOLE_TOP * 0.5),
               (TOE_X + 0.095, SHOE_W, SOLE_TOP))
    upper = box(name + "_upper", collection, (-0.01, hip_y, SOLE_TOP + 0.0375),
                (0.19, 0.10, 0.075))
    ob = join_into(sole, [upper])
    ob.name = name
    set_origin(ob, (0.0, hip_y, ANKLE_Z))
    # 鞋挂在腿下：摆腿时鞋跟着走，运行期只需驱动两个 Transform（而不是四件各算一次相位）
    ob.parent = parent
    bpy.context.view_layer.update()
    ob.matrix_parent_inverse = parent.matrix_world.inverted()
    return ob


def build(scene_coll):
    body = coll("body", scene_coll)
    leg_l = _leg(body, "Leg_L", LEG_Y)
    leg_r = _leg(body, "Leg_R", -LEG_Y)
    shoe_l = _shoe(body, "Shoe_L", leg_l, LEG_Y)
    shoe_r = _shoe(body, "Shoe_R", leg_r, -LEG_Y)
    parts = (leg_l, leg_r, shoe_l, shoe_r)

    # ---- 落位自检（当场判红，不必等 Unity 那侧的门）----
    # 关节高度用 matrix_world.translation（原点），外沿用包围盒：鞋挂了父级之后
    # ob.location 读的是 matrix_basis，不再等于世界位置，拿它当关节高度会自欺。
    for ob in parts:
        lo, hi = world_bounds(ob)
        pivot = ob.matrix_world.translation
        _near(pivot.x, 0.0, 1e-4, ob.name + " 原点沿程（关节必须在中线上）")
        _near(abs(pivot.y), LEG_Y, 1e-4, ob.name + " 原点横向（两腿半距）")
        if ob.name.startswith("Leg"):
            _near(pivot.z, HIP_Z, 1e-4, ob.name + " 髋枢轴")
            _near(hi.z, HIP_Z, 0.02, ob.name + " 腿顶（＝髋）")
            _near(lo.z, ANKLE_Z, 0.006, ob.name + " 裤腿下缘")
        else:
            _near(pivot.z, ANKLE_Z, 1e-4, ob.name + " 踝枢轴")
            _near(lo.z, 0.0, 0.005, ob.name + " 鞋底（脚底＝身体空间的地面）")
            _near(hi.x, TOE_X, 0.01, ob.name + " 脚尖前缘")
            if hi.x < 0.15:
                raise RuntimeError("%s 脚尖没朝前（max.x=%.3f）＝鞋退化成对称方块" % (ob.name, hi.x))
            if lo.x > -0.05:
                raise RuntimeError("%s 鞋跟没落到腿轴之后（min.x=%.3f）" % (ob.name, lo.x))
    for leg, shoe in ((leg_l, shoe_l), (leg_r, shoe_r)):
        if shoe.parent != leg:
            raise RuntimeError("%s 没挂在 %s 下面＝摆腿时鞋会留在原地" % (shoe.name, leg.name))
    if tris(leg_l) <= 0 or tris(shoe_l) <= 0:
        raise RuntimeError("腿或鞋没有面（join 失败？）")

    return ["%s pivot=(%.3f,%.3f,%.3f) bounds.z=[%.3f,%.3f] tris=%d"
            % (ob.name, ob.matrix_world.translation.x, ob.matrix_world.translation.y,
               ob.matrix_world.translation.z, world_bounds(ob)[0].z, world_bounds(ob)[1].z,
               tris(ob)) for ob in parts]


def _near(actual, want, tol, what):
    if abs(actual - want) > tol:
        raise RuntimeError("%s 期望 %.3f±%.3f，实测 %.3f" % (what, want, tol, actual))
