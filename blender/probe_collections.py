# blender/probe_collections.py
# M0 探针：5 个 1m 立方体。base 集合放裸名 wallL/floor；anom/01/show 放两个 wire 名
# anom01_show_shadeFigure / anom01_show_probeCubeB；anom/01/hide 放代理 anom01_hide_floor_off。
# 集合仅做编排（进不了 FBX），异常语义由对象名前缀语法承载 —— 见主控的前缀式裁定。
# 用法（headless）:
#   blender --background --python blender/probe_collections.py
import os
import bpy

OUT = os.environ.get(
    "PROBE_FBX_OUT",
    r"G:\AIgame\jam\dreamcore\Assets\Content\3D\Probe.fbx")

bpy.ops.wm.read_factory_settings(use_empty=True)
scene_coll = bpy.context.scene.collection


def new_coll(name, parent):
    coll = bpy.data.collections.new(name)
    parent.children.link(coll)
    return coll


base = new_coll("base", scene_coll)
anom = new_coll("anom", scene_coll)
a01 = new_coll("01", anom)
show = new_coll("show", a01)
hide = new_coll("hide", a01)


def cube(name, coll, loc, size=1.0):
    bpy.ops.mesh.primitive_cube_add(size=size, location=loc)
    ob = bpy.context.active_object
    ob.name = name
    for c in list(ob.users_collection):
        c.objects.unlink(ob)
    coll.objects.link(ob)
    return ob


# 前缀式 wire 契约（主控裁定）：集合仍只做 Blender 侧的编排容器，进不了 FBX，
# 异常语义改由对象名的前缀语法承载。base 物件保持裸名；异常物件用 wire 名：
#   anom<NN>_show_<leaf>          出现物件
#   anom<NN>_hide_<base目标>_off  消失代理（去掉 _off 即 base 里的目标名）
# 沿 Blender +Z 每隔 10 排开用来验单位尺度（注意：Unity 侧会重排到 +Y）。
cube("wallL", base, (0.0, 0.0, 0.0))
cube("floor", base, (0.0, 0.0, 10.0))
cube("anom01_show_shadeFigure", show, (0.0, 0.0, 20.0))
cube("anom01_show_probeCubeB", show, (0.0, 0.0, 30.0))
cube("anom01_hide_floor_off", hide, (0.0, 0.0, 40.0))

os.makedirs(os.path.dirname(OUT), exist_ok=True)
bpy.ops.export_scene.fbx(
    filepath=OUT,
    use_selection=False,
    use_visible=True,
    object_types={"MESH", "EMPTY"},
    use_mesh_modifiers=True,
    apply_unit_scale=True,
    bake_space_transform=False,
    path_mode="COPY",
    embed_textures=False,
    add_leaf_bones=False,
    use_metadata=False,
)

# 自检输出：这些 print 就是探针的"真实模型 oracle"，Unity 侧结论必须与它对照。
# 打印的是 wire 名（前缀式契约），供 tools/blender_probe.sh 与 Verify3D 逐字节比对。
print("[PROBE] exported:", OUT, "exists=", os.path.exists(OUT))
for coll, names in [(base, "base"), (show, "anom/01/show"), (hide, "anom/01/hide")]:
    print("[PROBE] coll<%s> objects=%s" % (names, [o.name for o in coll.objects]))
print("[PROBE] wire_names=%s" % [o.name for o in bpy.data.objects
                                  if o.name.startswith("anom")])
print("[PROBE] total_objects=", len(bpy.data.objects))
