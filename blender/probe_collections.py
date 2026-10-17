# blender/probe_collections.py
# M0 探针：三个小立方体放进 base / anom/01/show / anom/01/hide，导出 FBX。
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


# 间距 10m 用来验单位尺度：Unity 侧读到的 localPosition 差值应≈10
cube("wallL", base, (0.0, 0.0, 0.0))
cube("floor", base, (0.0, 0.0, 10.0))
cube("shadeFigure", show, (0.0, 0.0, 20.0))
cube("floor_off", hide, (0.0, 0.0, 30.0))

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

# 自检输出：这些 print 就是探针的"真实模型 oracle"，Unity 侧结论必须与它对照
print("[PROBE] exported:", OUT, "exists=", os.path.exists(OUT))
for coll, names in [(base, "base"), (show, "anom/01/show"), (hide, "anom/01/hide")]:
    print("[PROBE] coll<%s> objects=%s" % (names, [o.name for o in coll.objects]))
print("[PROBE] total_objects=", len(bpy.data.objects))
